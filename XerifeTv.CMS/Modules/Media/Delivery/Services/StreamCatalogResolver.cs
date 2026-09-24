using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XerifeTv.CMS.Modules.CatalogProvider.Interfaces;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;
using XerifeTv.CMS.Modules.Media.Delivery.Intefaces;

namespace XerifeTv.CMS.Modules.Media.Delivery.Services;

public sealed class StreamCatalogResolver(
    IHttpClientFactory _httpClientFactory,
    IConfiguration _configuration,
    ICatalogProviderService _catalogProviderService,
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
            || normalizedUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            // só o endpoint de catálogo do gaiaflix - o de HLS (gaiaflix-hls) NÃO é catálogo,
            // senão o player tentaria parsear o m3u8 como JSON e quebraria.
            || normalizedUrl.Contains("gaiaflix-movie-source", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Result<GetResolveUrlResponseDto>> ResolveAsync(
        string url,
        string fallbackStreamFormat,
        CancellationToken cancellationToken = default)
    {
        // Tenta os provedores na ordem de fallback (1, 2, 3...). Se um não tiver o título
        // (catálogo vazio) ou não tiver fonte funcional, passa pro próximo. A URL cadastrada
        // entra por último como garantia.
        var candidateUrls = await BuildProviderCandidateUrlsAsync(url);

        Result<GetResolveUrlResponseDto> lastResult =
            Result<GetResolveUrlResponseDto>.Failure(new Error("404", "Nenhum provedor de catálogo retornou streams"));

        foreach (var candidate in candidateUrls)
        {
            var catalogUriResult = CreateCatalogUri(candidate.Url);
            if (catalogUriResult.IsFailure)
            {
                lastResult = Result<GetResolveUrlResponseDto>.Failure(catalogUriResult.Error);
                continue;
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var fetchUris = BuildFetchUris(catalogUriResult.Data!);
            var payloadResult = Result<string>.Failure(new Error("502", "O catálogo de streams não respondeu"));

            foreach (var fetchUri in fetchUris)
            {
                try
                {
                    payloadResult = await FetchCatalogPayloadAsync(client, fetchUri, cancellationToken);
                    if (payloadResult.IsSuccess)
                    {
                        var normalizedPayload = NormalizeFetchedPayload(payloadResult.Data!, fetchUri);
                        if (normalizedPayload.IsFailure)
                        {
                            payloadResult = normalizedPayload;
                            continue;
                        }

                        if (IsGaiaflixCatalog(catalogUriResult.Data!)
                            && !HasGaiaflixSources(normalizedPayload.Data!))
                        {
                            payloadResult = Result<string>.Failure(new Error("502", "A resposta da ponte não contém o catálogo Gaiaflix"));
                            continue;
                        }

                        payloadResult = normalizedPayload;
                        break;
                    }
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    payloadResult = Result<string>.Failure(new Error("504", "Tempo esgotado ao consultar o catálogo de streams"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to fetch stream catalog from {Url}: {Message}", fetchUri, ex.Message);
                    payloadResult = Result<string>.Failure(new Error("502", ex.InnerException?.Message ?? ex.Message));
                }
            }

            if (payloadResult.IsFailure)
            {
                lastResult = Result<GetResolveUrlResponseDto>.Failure(payloadResult.Error);
                continue;
            }

            var resolveResult = await ResolveFromPayloadAsync(payloadResult.Data!, fallbackStreamFormat, candidate.ProviderName, candidate.Url, cancellationToken);
            if (resolveResult.IsSuccess)
                return resolveResult;

            lastResult = resolveResult;
        }

        return lastResult;
    }

    // Monta as URLs candidatas: cada provedor (na ordem) com o sufixo /stream/...json
    // da URL cadastrada, + a URL original por último. Assim o fallback busca o mesmo
    // título em cada provedor até achar.
    private async Task<IReadOnlyList<(string Url, string? ProviderName)>> BuildProviderCandidateUrlsAsync(string url)
    {
        var candidates = new List<(string Url, string? ProviderName)>();
        var trimmed = (url ?? string.Empty).Trim();
        bool Exists(string u) => candidates.Any(c => string.Equals(c.Url, u, StringComparison.OrdinalIgnoreCase));

        var providersResult = await _catalogProviderService.GetAllAsync(isIncludeDisabled: false);
        var providers = providersResult.IsSuccess && providersResult.Data is not null
            ? providersResult.Data.ToArray()
            : [];

        // Fallback só faz sentido pros catálogos path-based (stremio): troca a base mantendo
        // o sufixo /stream/...json. gaiaflix (query-based) não entra nesse loop.
        var suffixIndex = trimmed.IndexOf("/stream/", StringComparison.OrdinalIgnoreCase);
        if (suffixIndex >= 0)
        {
            var suffix = trimmed[suffixIndex..];
            foreach (var provider in providers)
            {
                if (string.IsNullOrWhiteSpace(provider.BaseUrl)) continue;

                var candidate = $"{provider.BaseUrl.TrimEnd('/')}{suffix}";
                if (!Exists(candidate))
                    candidates.Add((candidate, provider.Name));
            }
        }

        if (!Exists(trimmed))
            candidates.Add((trimmed, ProviderNameForUrl(providers, trimmed)));

        return candidates;
    }

    private static string? ProviderNameForUrl(
        IEnumerable<XerifeTv.CMS.Modules.CatalogProvider.Dtos.Response.GetCatalogProviderResponseDto> providers, string url)
        => providers
            .Where(p => !string.IsNullOrWhiteSpace(p.BaseUrl)
                        && url.StartsWith(p.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.BaseUrl.Length)
            .Select(p => p.Name)
            .FirstOrDefault();

    // Resolve a partir de um catálogo JÁ baixado (o navegador do admin busca o .json
    // direto do froststream - IP residencial nunca toma 403 - e manda o payload pro
    // servidor, que só faz o probe das fontes e monta as URLs. Assim o cadastro não
    // depende do IP de datacenter do Render pra falar com o froststream).
    public async Task<Result<GetResolveUrlResponseDto>> ResolveFromPayloadAsync(
        string payload,
        string fallbackStreamFormat,
        string? providerName = null,
        string? catalogUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Result<GetResolveUrlResponseDto>.Failure(new Error("400", "Catálogo vazio"));

        try
        {
            var envelope = JsonSerializer.Deserialize<CatalogEnvelope>(payload, _jsonOptions);

            // Formato gaiaflix: tem "sources" (url relativo + quality + type). Já vêm resolvidas,
            // então não precisa probe - só monta as fontes prefixando a base nas URLs relativas.
            if (envelope?.Sources is { Count: > 0 })
                return BuildGaiaflixResult(envelope.Sources, providerName, catalogUrl, fallbackStreamFormat);

            // Formato stremio (froststream/fenixflix): "streams" com arquivos diretos - probe cada um.
            if (envelope?.Streams is null || envelope.Streams.Count == 0)
                return Result<GetResolveUrlResponseDto>.Failure(new Error("404", "O catálogo não possui streams"));

            var candidates = envelope.Streams
                .Select((stream, index) => CreateCandidate(stream, index, fallbackStreamFormat, providerName))
                .Where(candidate => candidate is not null)
                .Cast<StreamCandidate>()
                .ToArray();

            if (candidates.Length == 0)
                return Result<GetResolveUrlResponseDto>.Failure(new Error("404", "O catálogo não possui URLs de vídeo válidas"));

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var probes = await Task.WhenAll(candidates.Select(candidate => ProbeAsync(client, candidate, cancellationToken)));
            // Lista TODAS as fontes funcionais (não deduplica por qualidade) pra o
            // usuário escolher qual assistir. Ordena por qualidade desc, mantendo a
            // ordem do catálogo como desempate.
            var functionalSources = probes
                .Where(probe => probe.IsFunctional)
                .Select(probe => probe.Candidate)
                .OrderByDescending(candidate => candidate.QualityRank)
                .ThenBy(candidate => candidate.Index)
                .ToArray();

            if (functionalSources.Length == 0)
                return Result<GetResolveUrlResponseDto>.Failure(new Error("502", "Nenhum stream do catálogo está funcional"));

            return BuildResult(functionalSources.Select(c => (c.Url, c.StreamFormat, c.SourceName)));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<GetResolveUrlResponseDto>.Failure(new Error("504", "Tempo esgotado ao validar as fontes do catálogo"));
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
            $"O catálogo de streams respondeu {(int)lastStatusCode}"));
    }

    private Uri? BuildProxyUri(Uri catalogUri)
    {
        var proxyBaseUrl = _configuration["StreamCatalog:ProxyBaseUrl"];
        if (string.IsNullOrWhiteSpace(proxyBaseUrl)
            || !Uri.TryCreate(proxyBaseUrl.Trim(), UriKind.Absolute, out var proxyBase))
            return null;

        var separator = string.IsNullOrEmpty(proxyBase.Query) ? "?" : "&";
        var proxied = $"{proxyBase.AbsoluteUri}{separator}url={Uri.EscapeDataString(catalogUri.AbsoluteUri)}";
        return new Uri(proxied);
    }

    private Uri BuildFetchUri(Uri catalogUri)
    {
        if (!catalogUri.AbsolutePath.Contains("/stream/", StringComparison.OrdinalIgnoreCase))
            return catalogUri;

        return BuildProxyUri(catalogUri) ?? catalogUri;
    }

    private IReadOnlyList<Uri> BuildFetchUris(Uri catalogUri)
    {
        if (!IsGaiaflixCatalog(catalogUri))
            return [BuildFetchUri(catalogUri)];

        var fetchUris = new List<Uri>();
        var proxyUri = BuildProxyUri(catalogUri);
        if (proxyUri is not null)
            fetchUris.Add(proxyUri);

        fetchUris.Add(catalogUri);
        fetchUris.Add(new Uri($"https://r.jina.ai/http://{catalogUri.Authority}{catalogUri.PathAndQuery}"));

        return fetchUris
            .DistinctBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsGaiaflixCatalog(Uri uri)
        => uri.Host.Equals("gaiaflix.live", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Equals("/api/gaiaflix-movie-source", StringComparison.OrdinalIgnoreCase);

    private static Result<string> NormalizeFetchedPayload(string payload, Uri fetchUri)
    {
        if (!fetchUri.Host.Equals("r.jina.ai", StringComparison.OrdinalIgnoreCase))
            return Result<string>.Success(payload);

        var jsonStart = payload.IndexOf('{');
        var jsonEnd = payload.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
            return Result<string>.Failure(new Error("502", "A ponte da Gaiaflix não retornou JSON válido"));

        return Result<string>.Success(payload[jsonStart..(jsonEnd + 1)]);
    }

    private static bool HasGaiaflixSources(string payload)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<CatalogEnvelope>(payload, _jsonOptions);
            return envelope?.Sources is { Count: > 0 };
        }
        catch (JsonException)
        {
            return false;
        }
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

    private static StreamCandidate? CreateCandidate(StreamCatalogItem stream, int index, string fallbackStreamFormat, string? providerName)
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
        var sourceName = BuildSourceName(providerName, stream.Name, quality.Label);

        return new StreamCandidate(
            streamUrl,
            streamFormat,
            quality.Label,
            quality.Rank,
            index,
            sourceName);
    }

    // Monta o resultado final a partir de (url, formato, rótulo), garantindo rótulos únicos.
    private static Result<GetResolveUrlResponseDto> BuildResult(IEnumerable<(string Url, string Format, string Label)> items)
    {
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sources = items
            .Select(item =>
            {
                var label = item.Label;
                if (used.TryGetValue(label, out var count))
                {
                    used[label] = count + 1;
                    label = $"{label} ({count + 1})";
                }
                else
                {
                    used[label] = 1;
                }

                return new GetResolveUrlSourceResponseDto(item.Url, item.Format, label);
            })
            .ToArray();

        if (sources.Length == 0)
            return Result<GetResolveUrlResponseDto>.Failure(new Error("502", "Nenhuma fonte funcional"));

        var primary = sources[0];
        return Result<GetResolveUrlResponseDto>.Success(
            new GetResolveUrlResponseDto(primary.Url, primary.StreamFormat) { Sources = sources });
    }

    // gaiaflix: sources[] com url relativo, quality e type. Prefixa a base (origin do catálogo)
    // nas URLs relativas; não faz probe (as URLs já são endpoints de stream do provedor).
    private Result<GetResolveUrlResponseDto> BuildGaiaflixResult(
        IEnumerable<GaiaflixSource> sources, string? providerName, string? catalogUrl, string fallbackStreamFormat)
    {
        var origin = GetOrigin(catalogUrl);

        var items = sources
            .Select(s => (Raw: (s.Url ?? string.Empty).Trim(), s.Quality, s.Type))
            .Where(s => !string.IsNullOrWhiteSpace(s.Raw))
            .Select(s =>
            {
                var abs = MakeAbsolute(s.Raw, origin);
                var format = MapGaiaflixType(s.Type, fallbackStreamFormat);
                var quality = string.IsNullOrWhiteSpace(s.Quality) ? "Auto" : s.Quality!.Trim();
                var label = BuildSourceName(providerName, null, quality);
                return (Url: abs, Format: format, Label: label);
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .Select(s => (s.Url!, s.Format, s.Label))
            .ToArray();

        if (items.Length == 0)
            return Result<GetResolveUrlResponseDto>.Failure(new Error("404", "O catálogo não possui URLs de vídeo válidas"));

        return BuildResult(items);
    }

    private static string? GetOrigin(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? $"{uri.Scheme}://{uri.Authority}" : null;

    private static string? MakeAbsolute(string url, string? origin)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out _)) return url;
        if (string.IsNullOrWhiteSpace(origin)) return null;
        return $"{origin!.TrimEnd('/')}/{url.TrimStart('/')}";
    }

    private static string MapGaiaflixType(string? type, string fallbackStreamFormat)
    {
        var t = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (t is "m3u8" or "hls") return "hls";
        if (!string.IsNullOrWhiteSpace(t)) return t;
        return string.IsNullOrWhiteSpace(fallbackStreamFormat) ? "mp4" : fallbackStreamFormat;
    }

    // Rótulo da fonte mostrado pro usuário. Prioriza o NOME DO PROVEDOR do CMS + qualidade
    // (ex: "FenixFlix 1080p"); sem provedor, cai no "name" do catálogo; por fim, na qualidade.
    private static string BuildSourceName(string? providerName, string? catalogName, string qualityLabel)
    {
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            var suffix = string.IsNullOrWhiteSpace(qualityLabel) ? string.Empty : $" {qualityLabel}";
            return $"{providerName.Trim()}{suffix}".Trim();
        }

        var baseName = (catalogName ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(baseName) ? qualityLabel : baseName;
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

    // Envelope que cobre os dois formatos: stremio ("streams") e gaiaflix ("sources").
    private sealed record CatalogEnvelope(
        [property: JsonPropertyName("streams")] IReadOnlyCollection<StreamCatalogItem>? Streams,
        [property: JsonPropertyName("sources")] IReadOnlyCollection<GaiaflixSource>? Sources);

    private sealed record GaiaflixSource(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("quality")] string? Quality,
        [property: JsonPropertyName("type")] string? Type);

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
        int Index,
        string SourceName);

    private sealed record StreamProbe(StreamCandidate Candidate, bool IsFunctional);

    private sealed record StreamQuality(string Label, int Rank);
}
