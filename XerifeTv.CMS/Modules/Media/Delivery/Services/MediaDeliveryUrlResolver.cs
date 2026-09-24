using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;
using XerifeTv.CMS.Modules.Media.Delivery.Intefaces;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Modules.Media.Delivery.Services;

public class MediaDeliveryUrlResolver(
    IEnumerable<IMediaDeliveryTokenStrategy> _mediaTokenStrategies,
    IMediaDeliveryProfileService _service,
    IRedirectUrlResolver _redirectUrlResolver,
    IStreamCatalogResolver _streamCatalogResolver,
    IConfiguration _configuration,
    IHttpContextAccessor _httpContextAccessor) : IMediaDeliveryUrlResolver
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
        //
        // O segmento "media.{ext}" no path (nao so na query) e obrigatorio: sem "type"
        // explicito no <video>, o video.js so aceita tentar carregar uma fonte se conseguir
        // adivinhar um formato plausivel pela EXTENSAO da URL. Uma URL so com query string
        // (?u=...) e rejeitada de cara com "No compatible source", sem nenhuma requisicao de
        // rede - foi exatamente o sintoma visto em producao. A extensao so serve pra passar
        // nesse pre-check do video.js; quem manda na reproducao de verdade e o Content-Type
        // real que o proxy repassa do servidor de origem.
        string safeExtension = System.Text.RegularExpressions.Regex.IsMatch(streamFormat ?? string.Empty, "^[a-z0-9]{1,10}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            ? streamFormat!.ToLowerInvariant()
            : "mp4";

        // Absoluta, nao relativa: quem consome isso nao e so o admin (mesma origem do CMS) -
        // e tambem o site publico (Angular, hospedado em outra origem/dominio), que so recebe
        // o {url, streamFormat} de volta e faz video.src = url direto. Um path relativo iria
        // resolver contra a origem do SITE, nao da API, e daria 404 la.
        //
        // Deriva host/esquema da propria requisicao em vez de confiar em appsettings "baseUrl" -
        // esse valor so tem o placeholder de dev (localhost:5003) e nao ha variavel de ambiente
        // equivalente configurada no Render, o que ja gerou uma URL absoluta porem apontando
        // pro lugar errado. Detalhe de como isso e derivado em GetPublicBaseUrl().
        string baseUrl = GetPublicBaseUrl();
        string encryptedUrl = CryptographyHelper.Encrypt(url, _configuration["SecuritySettings:ContentEncryptionKey"]!);
        string proxyPath = $"{baseUrl}/MediaDeliveryProfiles/StreamMedia/media.{safeExtension}?u={Uri.EscapeDataString(encryptedUrl)}";

        return new(proxyPath, streamFormat);
    }

    private string GetPublicBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        if (request is null)
            return (_configuration["baseUrl"] ?? string.Empty).TrimEnd('/');

        string scheme = request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && proto.Count > 0
            ? proto[0]!
            : request.Scheme;

        string host = request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost) && forwardedHost.Count > 0
            ? forwardedHost[0]!
            : request.Host.Value;

        return $"{scheme}://{host}";
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

            var resolvedUrl = urlBuilder.ToString();
            if (_streamCatalogResolver.CanHandle(resolvedUrl))
                return await ResolveStreamCatalogAsync(resolvedUrl, mediaProfile.StreamFormat);

            return Result<GetResolveUrlResponseDto>.Success(AvoidMixedContent(resolvedUrl, mediaProfile.StreamFormat));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetResolveUrlResponseDto>.Failure(error);
        }
    }

    public async Task<Result<GetResolveUrlResponseDto>> ResolveUrlFixedAsync(string urlFixed, string streamFormat, bool followRedirect = false)
    {
        if (_streamCatalogResolver.CanHandle(urlFixed))
            return await ResolveStreamCatalogAsync(urlFixed, streamFormat);

        if (!followRedirect || string.IsNullOrWhiteSpace(urlFixed))
            return Result<GetResolveUrlResponseDto>.Success(AvoidMixedContent(urlFixed, streamFormat));

        var finalUrlResult = await _redirectUrlResolver.ResolveFinalUrlAsync(urlFixed);

        if (finalUrlResult.IsFailure)
            return Result<GetResolveUrlResponseDto>.Failure(finalUrlResult.Error);

        return Result<GetResolveUrlResponseDto>.Success(AvoidMixedContent(finalUrlResult.Data!, streamFormat));
    }

    public async Task<Result<GetResolveUrlResponseDto>> ResolveStreamCatalogFromPayloadAsync(string payload, string streamFormat, string? providerName = null, string? catalogUrl = null)
    {
        var catalogResult = await _streamCatalogResolver.ResolveFromPayloadAsync(payload, streamFormat, providerName, catalogUrl);
        return BuildCatalogResponse(catalogResult);
    }

    private async Task<Result<GetResolveUrlResponseDto>> ResolveStreamCatalogAsync(string url, string streamFormat)
    {
        var catalogResult = await _streamCatalogResolver.ResolveAsync(url, streamFormat);
        return BuildCatalogResponse(catalogResult);
    }

    private Result<GetResolveUrlResponseDto> BuildCatalogResponse(Result<GetResolveUrlResponseDto> catalogResult)
    {
        if (catalogResult.IsFailure || catalogResult.Data is null)
            return catalogResult;

        var primary = AvoidMixedContent(catalogResult.Data.Url, catalogResult.Data.StreamFormat);
        var sources = catalogResult.Data.Sources
            .Select(source =>
            {
                var resolvedSource = AvoidMixedContent(source.Url, source.StreamFormat);
                return new GetResolveUrlSourceResponseDto(
                    resolvedSource.Url,
                    resolvedSource.StreamFormat,
                    source.Quality);
            })
            .ToArray();

        return Result<GetResolveUrlResponseDto>.Success(primary with { Sources = sources });
    }
}
