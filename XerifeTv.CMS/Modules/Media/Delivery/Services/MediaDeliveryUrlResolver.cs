using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;
using XerifeTv.CMS.Modules.Media.Delivery.Intefaces;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Modules.Media.Delivery.Services;

public class MediaDeliveryUrlResolver(
    IEnumerable<IMediaDeliveryTokenStrategy> _mediaTokenStrategies,
    IMediaDeliveryProfileService _service,
    IRedirectUrlResolver _redirectUrlResolver,
    IConfiguration _configuration) : IMediaDeliveryUrlResolver
{
    // hls/m3u8 sao playlists que referenciam outras URLs (segmentos/sub-playlists) - proxiar
    // so o manifesto nao resolve o mixed content dos segmentos. Streaming proxy cobre apenas
    // arquivo progressivo (mp4/mkv/mov/webm), que e o caso real hoje.
    private static readonly HashSet<string> _playlistFormats = new(StringComparer.OrdinalIgnoreCase) { "hls", "m3u8" };

    private GetResolveUrlResponseDto AvoidMixedContent(string url, string streamFormat)
    {
        if (string.IsNullOrWhiteSpace(url) || _playlistFormats.Contains(streamFormat ?? string.Empty))
            return new(url, streamFormat);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp)
            return new(url, streamFormat);

        // Pagina roda em https em producao - <video src="http://..."> e bloqueado pelo browser
        // como mixed content. Encaminha pelo proxy de streaming do proprio servidor (https)
        // em vez de devolver a URL externa direto. A URL vai cifrada pra evitar SSRF via
        // manipulacao do parametro pelo cliente.
        string encryptedUrl = CryptographyHelper.Encrypt(url, _configuration["SecuritySettings:ContentEncryptionKey"]!);
        string proxyPath = $"/MediaDeliveryProfiles/StreamMedia?u={Uri.EscapeDataString(encryptedUrl)}";

        return new(proxyPath, streamFormat);
    }

    public async Task<Result<GetResolveUrlResponseDto>> ResolveUrlAsync(string mediaPath, string mediaDeliveryProfileId)
    {
        try
        {
            var response = await _service.GetAsync(mediaDeliveryProfileId);

            if (response.IsFailure)
                return Result<GetResolveUrlResponseDto>.Failure(response.Error);

            var mediaProfile = response.Data!;

            var tokenStrategy = _mediaTokenStrategies.FirstOrDefault(s => s.CanHandle(mediaProfile.TokenStrategy));

            if (tokenStrategy == null)
                return Result<GetResolveUrlResponseDto>.Failure(new Error("400", "No token strategy found for the specified type"));

            var tokenResult = tokenStrategy.Resolve(mediaProfile.QueryParameters);

            if (tokenResult.IsFailure)
                return Result<GetResolveUrlResponseDto>.Failure(tokenResult.Error);

            var baseUri = new Uri(mediaProfile.BaseUrl);
            var combinedPath = $"{baseUri.AbsolutePath.TrimEnd('/')}/{mediaPath.TrimStart('/')}";

            var urlBuilder = new UriBuilder(baseUri)
            {
                Path = combinedPath,
                Query = tokenResult.Data
            };

            return Result<GetResolveUrlResponseDto>.Success(AvoidMixedContent(urlBuilder.ToString(), mediaProfile.StreamFormat));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetResolveUrlResponseDto>.Failure(error);
        }
    }

    public async Task<Result<GetResolveUrlResponseDto>> ResolveUrlFixedAsync(string urlFixed, string streamFormat, bool followRedirect = false)
    {
        if (!followRedirect || string.IsNullOrWhiteSpace(urlFixed))
            return Result<GetResolveUrlResponseDto>.Success(AvoidMixedContent(urlFixed, streamFormat));

        var finalUrlResult = await _redirectUrlResolver.ResolveFinalUrlAsync(urlFixed);

        if (finalUrlResult.IsFailure)
            return Result<GetResolveUrlResponseDto>.Failure(finalUrlResult.Error);

        return Result<GetResolveUrlResponseDto>.Success(AvoidMixedContent(finalUrlResult.Data!, streamFormat));
    }
}