using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;
using XerifeTv.CMS.Modules.Media.Delivery.Intefaces;

namespace XerifeTv.CMS.Modules.Media.Delivery.Services;

public sealed class StreamCatalogResolver(
    IHttpClientFactory _httpClientFactory,
    IConfiguration _configuration,
    ILogger<StreamCatalogResolver> _logger) : IStreamCatalogResolver
{
    public const string HttpClientName = "stream-catalog-resolver";

    private static readonly Regex _qualityRegex = new(
        @"(?<![a-z0-9])(?<quality>4k|2160p|1440p|1080p|720p|576p|480p|360p)(?![a-z0-9])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool CanHandle(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var normalizedUrl = url.Trim();
        return normalizedUrl.Contains("/stream/", StringComparison.OrdinalIgnoreCase)
            || normalizedUrl.StartsWith("stream/", StringComparison.OrdinalIgnoreCase)
            || normalizedUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Result<GetResolveUrlResponseDto>> ResolveAsync(
        string url,
        string fallbackStreamFormat,
        CancellationToken cancellationToken = default)
    {
        var catalogUriResult = CreateCatalogUri(url);
        if (catalogUriResult.IsFailure)
            return Result<GetResolveUrlResponseDto>.Failure(catalogUriResult.Error);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            var fetchUri = BuildFetchUri(catalogUriResult.Data!);
            var payloadResult = await FetchCatalogPayloadAsync(client, fetchUri, cancellationToken);
            if (payloadResult.IsFailure)
                return Result<GetResolveUrlResponseDto>.Failure(payloadResult.Error);

            var catalog = JsonSerializer.Deserialize<StreamCatalogResponse>(payloadResult.Data!, _jsonOptions);

            if (catalog?.Streams is null || catalog.Streams.Count == 0)
                return Result<GetResolveUrlResponseDto>.Failure(new Error("404", "O catálogo não possui streams"));

            var candidates = catalog.Streams
                .Select((stream, index) => CreateCandidate(stream, index, fallbackStreamFormat))
                .Where(candidate => candidate is not null)
                .Cast<StreamCandidate>()
                .ToArray();

            if (candidates.Length == 0)
                return Result<GetResolveUrlResponseDto>.Failure(new Error("404", "O catálogo não possui URLs de vídeo válidas"));

            var probes = await Task.WhenAll(candidates.Select(candidate => ProbeAsync(client, candidate, cancellationToken)));
            var functionalSources = probes
                .Where(probe => probe.IsFunctional)
                .Select(probe => probe.Candidate)
                .GroupBy(candidate => candidate.Quality, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(candidate => candidate.Index).First())
                .OrderByDescending(candidate => candidate.QualityRank)
                .ThenBy(candidate => candidate.Index)
                .ToArray();

            if (functionalSources.Length == 0)
                return Result<GetResolveUrlResponseDto>.Failure(new Error("502", "Nenhum stream do catálogo está funcional"));

            var sources = functionalSources
                .Select(candidate => new GetResolveUrlSourceResponseDto(
                    candidate.Url,
                    candidate.StreamFormat,
                    candidate.Quality))
                .ToArray();

            var primary = sources[0];
            return Result<GetResolveUrlResponseDto>.Success(
                new GetResolveUrlResponseDto(primary.Url, primary.StreamFormat)
                {
                    Sources = sources
                });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<GetResolveUrlResponseDto>.Failure(new Error("504", "Tempo esgotado ao consultar o catálogo de streams"));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Invalid stream catalog response: {Message}", ex.Message);
            return Result<GetResolveUrlResponseDto>.Failure(new Error("502", "O catálogo de streams retornou JSON inválido"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to resolve stream catalog: {Message}", ex.Message);
            return Result<GetResolveUrlResponseDto>.Failure(new Error("502", ex.InnerException?.Message ?? ex.Message));
        }
    }

    // O catálogo fica atrás do Cloudflare com cache de edge. Num cache HIT ele
    // devolve 200 na hora; num MISS a origem aplica a regra anti-bot e às vezes
    // responde 403 para o IP de datacenter do Render. Como é intermitente, uma
    // nova tentativa quase sempre pega um HIT ou passa - por isso o retry.
    private static readonly HttpStatusCode[] _retryableStatusCodes =
    {
        HttpStatusCode.Forbidden,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    };

    private const int MaxCatalogAttempts = 3;

    private async Task<Result<string>> FetchCatalogPayloadAsync(
        HttpClient client,
        Uri catalogUri,
        CancellationToken cancellationToken)
    {
        HttpStatusCode lastStatusCode = default;

        for (var attempt = 1; attempt <= MaxCatalogAttempts; attempt++)
        {
            using var response = await client.GetAsync(
                catalogUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
                return Result<string>.Success(await response.Content.ReadAsStringAsync(cancellationToken));

            lastStatusCode = response.StatusCode;

            if (attempt == MaxCatalogAttempts || !_retryableStatusCodes.Contains(response.StatusCode))
                break;

            _logger.LogDebug(
                "Stream catalog returned {StatusCode} (attempt {Attempt}/{Max}), retrying",
                (int)response.StatusCode, attempt, MaxCatalogAttempts);

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
        }

        return Result<string>.Failure(new Error(
            "502",
            $"O catálogo de streams respondeu {(int)lastStatusCode} [fetch host: {catalogUri.Host}]"));
    }

    // O IP de datacenter do Render toma 403 do Cloudflare do froststream de forma
    // intermitente e pegajosa. Se StreamCatalog:ProxyBaseUrl estiver configurado,
    // o GET do catálogo sai por um proxy (Cloudflare Worker) que tem IP confiável,
    // contornando o bloqueio. Sem a config, busca direto (comportamento local).
    private Uri BuildFetchUri(Uri catalogUri)
    {
        var proxyBaseUrl = _configuration["StreamCatalog:ProxyBaseUrl"];
        if (string.IsNullOrWhiteSpace(proxyBaseUrl)
            || !Uri.TryCreate(proxyBaseUrl.Trim(), UriKind.Absolute, out var proxyBase))
            return catalogUri;

        var separator = string.IsNullOrEmpty(proxyBase.Query) ? "?" : "&";
        var proxied = $"{proxyBase.AbsoluteUri}{separator}url={Uri.EscapeDataString(catalogUri.AbsoluteUri)}";
        return new Uri(proxied);
    }

    private Result<Uri> CreateCatalogUri(string url)
    {
        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
                return Result<Uri>.Success(absoluteUri);

            return Result<Uri>.Failure(new Error("400", "A URL do catálogo precisa usar HTTP ou HTTPS"));
        }

        var baseUrl = _configuration["StreamCatalog:BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result<Uri>.Failure(new Error(
                "400",
                "Configure StreamCatalog:BaseUrl para usar caminhos relativos de catálogo"));
        }

        var normalizedBaseUri = new Uri($"{baseUri.AbsoluteUri.TrimEnd('/')}/");
        return Result<Uri>.Success(new Uri(normalizedBaseUri, url.Trim().TrimStart('/')));
    }

    private static StreamCandidate? CreateCandidate(StreamCatalogItem stream, int index, string fallbackStreamFormat)
    {
        var streamUrl = ExtractUrl(stream.Url);
        if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri))
        {
            streamUrl = streamUrl.Replace("[", "%5B").Replace("]", "%5D");
            if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out uri))
                return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        var quality = FindQuality(stream.Name, stream.Title, streamUrl);
        var streamFormat = GetStreamFormat(uri, fallbackStreamFormat);

        return new StreamCandidate(
            streamUrl,
            streamFormat,
            quality.Label,
            quality.Rank,
            index);
    }

    private async Task<StreamProbe> ProbeAsync(
        HttpClient client,
        StreamCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, candidate.Url);
            using var headResponse = await client.SendAsync(
                headRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (IsFunctional(headResponse))
                return new StreamProbe(candidate, true);

            if (headResponse.StatusCode is not (HttpStatusCode.MethodNotAllowed
                or HttpStatusCode.NotImplemented
                or HttpStatusCode.Forbidden))
                return new StreamProbe(candidate, false);

            using var getRequest = new HttpRequestMessage(HttpMethod.Get, candidate.Url);
            getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var getResponse = await client.SendAsync(
                getRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return new StreamProbe(candidate, IsFunctional(getResponse));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug("Stream probe failed for {Url}: {Message}", candidate.Url, ex.Message);
            return new StreamProbe(candidate, false);
        }
    }

    private static bool IsFunctional(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return false;

        var contentType = response.Content.Headers.ContentType?.MediaType;
        return string.IsNullOrWhiteSpace(contentType)
            || (!contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                && !contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalizedValue = value.Trim()
            .Replace("\\[", "[")
            .Replace("\\]", "]")
            .Replace("\\_", "_");

        var markdownStart = normalizedValue.IndexOf("](", StringComparison.Ordinal);
        if (normalizedValue.StartsWith("[", StringComparison.Ordinal) && markdownStart > 0)
        {
            var markdownUrl = normalizedValue[(markdownStart + 2)..].TrimEnd(')');
            return markdownUrl.Trim();
        }

        return normalizedValue.Trim('<', '>');
    }

    private static StreamQuality FindQuality(params string?[] values)
    {
        foreach (var value in values)
        {
            var match = _qualityRegex.Match(value ?? string.Empty);
            if (!match.Success)
                continue;

            var label = match.Groups["quality"].Value.ToUpperInvariant();
            var rank = label == "4K"
                ? 2160
                : int.Parse(label[..^1]);

            return new StreamQuality(label, rank);
        }

        return new StreamQuality("Auto", 0);
    }

    private static string GetStreamFormat(Uri uri, string fallbackStreamFormat)
    {
        var extension = Path.GetExtension(uri.AbsolutePath).TrimStart('.');
        if (!string.IsNullOrWhiteSpace(extension))
            return extension.ToLowerInvariant();

        return string.IsNullOrWhiteSpace(fallbackStreamFormat) ? "mp4" : fallbackStreamFormat;
    }

    private sealed record StreamCatalogResponse(
        [property: JsonPropertyName("streams")] IReadOnlyCollection<StreamCatalogItem> Streams);

    private sealed record StreamCatalogItem(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("url")] string? Url);

    private sealed record StreamCandidate(
        string Url,
        string StreamFormat,
        string Quality,
        int QualityRank,
        int Index);

    private sealed record StreamProbe(StreamCandidate Candidate, bool IsFunctional);

    private sealed record StreamQuality(string Label, int Rank);
}
