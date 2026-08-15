using System.Net;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Media.Delivery.Intefaces;

namespace XerifeTv.CMS.Modules.Media.Delivery.Services;

public class RedirectUrlResolver(
    IHttpClientFactory _httpClientFactory,
    ILogger<RedirectUrlResolver> _logger) : IRedirectUrlResolver
{
    public const string HttpClientName = "redirect-url-resolver";

    public async Task<Result<string>> ResolveFinalUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Result<string>.Failure(new Error("400", "URL nao informada"));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var requestUri)
            || (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps))
            return Result<string>.Failure(new Error("400", $"URL invalida: {url}"));

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            var response = await SendFollowingRedirectsAsync(client, requestUri, cancellationToken);

            // O handler segue a cadeia automaticamente, entao RequestMessage.RequestUri
            // guarda o endereco do ultimo salto - o arquivo real.
            var finalUrl = response.RequestMessage?.RequestUri?.ToString();

            if (string.IsNullOrWhiteSpace(finalUrl))
                return Result<string>.Failure(new Error("502", $"Nao foi possivel resolver o redirecionamento de: {url}"));

            if (!response.IsSuccessStatusCode)
                return Result<string>.Failure(
                    new Error("502", $"O destino respondeu {(int)response.StatusCode} ao resolver: {url}"));

            return Result<string>.Success(finalUrl);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning($"Timeout following redirect for {url}");
            return Result<string>.Failure(new Error("504", $"Tempo esgotado ao resolver o redirecionamento de: {url}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed following redirect for {url}: {ex.Message}");
            return Result<string>.Failure(new Error("502", ex.InnerException?.Message ?? ex.Message));
        }
    }

    /// <summary>
    /// Tenta HEAD primeiro para nao baixar o arquivo. CDNs que nao aceitam HEAD
    /// respondem 405/501 - nesses casos refaz com GET lendo apenas os headers.
    /// </summary>
    private static async Task<HttpResponseMessage> SendFollowingRedirectsAsync(
        HttpClient client,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, requestUri);
        var response = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode is not (HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented or HttpStatusCode.Forbidden))
            return response;

        response.Dispose();

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        return await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}
