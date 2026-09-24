using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.CatalogProvider.Interfaces;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Request;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;
using XerifeTv.CMS.Modules.Media.Delivery.Intefaces;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Controllers;

public class MediaDeliveryProfilesController(
    IMediaDeliveryProfileService _service,
    IMediaDeliveryUrlResolver _urlResolver,
    IActivityLogService _activityLogService,
    ILogger<MediaDeliveryProfilesController> _logger,
    ICacheService _cacheService,
    IConfiguration _configuration,
    ICatalogProviderService _catalogProviderService,
    IHttpClientFactory _httpClientFactory) : Controller
{
    public const string StreamHttpClientName = "media-stream-proxy";

    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create(CreateMediaDeliveryProfileRequestDto dto)
    {
        var response = await _service.CreateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson($"Perfil Entrega de Midia cadastrado com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} registered the media delivery profile {dto.Name}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "created", $"cadastrou o perfil de entrega de mídia \"{dto.Name}\"");

        return Redirect(Url.Action("Index", "Settings") + "#media-delivery");
    }

    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(UpdateMediaDeliveryProfileRequestDto dto)
    {
        var response = await _service.UpdateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson($"Perfil Entrega de Midia atualizado com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} updated the media delivery profile {dto.Name}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "updated", $"atualizou o perfil de entrega de mídia \"{dto.Name}\"");

        return Redirect(Url.Action("Index", "Settings") + "#media-delivery");
    }

    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string? id)
    {
        if (id is not null)
        {
            var response = await _service.DeleteAsync(id);

            TempData["Notification"] = response.IsFailure
              ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
              : MessageViewHelper.SuccessJson($"Perfil Entrega de Midia deletado com sucesso");

            _logger.LogInformation($"{User.Identity?.Name} removed the media delivery profile with id = {id}");
            await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "deleted", $"removeu o perfil de entrega de mídia com id = {id}");
        }

        return Redirect(Url.Action("Index", "Settings") + "#media-delivery");
    }

    [Authorize(Roles = "admin, common")]
    [HttpGet]
    public async Task<IActionResult> ResolveUrl(string mediaPath, string mediaDeliveryProfileId, bool isCached = false)
    {
        var normalizedPath = mediaPath.Trim().ToLowerInvariant();
        var cacheKey = $"resolve-url:{normalizedPath}:{mediaDeliveryProfileId}";
        var responseCache = _cacheService.GetValue<GetResolveUrlResponseDto?>(cacheKey);

        if (responseCache != null && isCached)
            return Ok(responseCache);

        var response = await _urlResolver.ResolveUrlAsync(mediaPath, mediaDeliveryProfileId);

        if (response.IsFailure)
            return StatusCode(int.Parse(response.Error.Code), response.Error.Description);
        
        _cacheService.SetValue<GetResolveUrlResponseDto?>(cacheKey, response.Data);

        return Ok(response.Data);
    }

    [Authorize(Roles = "admin, common")]
    [HttpGet]
    public async Task<IActionResult> ResolveUrlFixed(string urlFixed, string streamFormat, bool followRedirect = false, bool isCached = true)
    {
        // Cache curto do catálogo resolvido: o 1o play bate no froststream (via Worker proxy),
        // os próximos vêm da memória - menos exposição ao 403 e menos latência. TTL fica bem
        // abaixo do max-age=3600 que o froststream marca no catálogo, então os tokens das
        // URLs ainda são válidos quando servidos do cache.
        var cacheKey = $"resolve-url-fixed:{urlFixed.Trim().ToLowerInvariant()}:{streamFormat}:{followRedirect}";
        var responseCache = _cacheService.GetValue<GetResolveUrlResponseDto?>(cacheKey);

        if (responseCache != null && isCached)
            return Ok(responseCache);

        var response = await _urlResolver.ResolveUrlFixedAsync(urlFixed, streamFormat, followRedirect);

        if (response.IsFailure)
            return StatusCode(int.Parse(response.Error.Code), response.Error.Description);

        _cacheService.SetValue<GetResolveUrlResponseDto?>(cacheKey, response.Data);

        return Ok(response.Data);
    }

    // O navegador do admin busca o catálogo (.json) direto do froststream - IP residencial
    // nunca toma 403 - e manda o JSON aqui. O servidor só faz o probe das fontes e monta
    // as URLs, sem precisar falar com o froststream (que bloqueia o IP do Render/Worker).
    // Compartilha a mesma chave de cache do ResolveUrlFixed.
    [Authorize(Roles = "admin, common")]
    [HttpPost]
    public async Task<IActionResult> ResolveCatalogFromPayload([FromBody] ResolveCatalogFromPayloadRequestDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Payload) || string.IsNullOrWhiteSpace(dto.UrlFixed))
            return BadRequest();

        var cacheKey = $"resolve-url-fixed:{dto.UrlFixed.Trim().ToLowerInvariant()}:{dto.StreamFormat}:{dto.FollowRedirect}";
        var responseCache = _cacheService.GetValue<GetResolveUrlResponseDto?>(cacheKey);

        if (responseCache != null)
            return Ok(responseCache);

        var providerName = await GetProviderNameForUrlAsync(dto.UrlFixed);
        var response = await _urlResolver.ResolveStreamCatalogFromPayloadAsync(dto.Payload, dto.StreamFormat, providerName, dto.UrlFixed);

        if (response.IsFailure)
            return StatusCode(int.Parse(response.Error.Code), response.Error.Description);

        _cacheService.SetValue<GetResolveUrlResponseDto?>(cacheKey, response.Data);

        return Ok(response.Data);
    }

    // Descobre o nome do provedor (CMS) a partir da URL do catálogo, casando pela Base URL.
    private async Task<string?> GetProviderNameForUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var providersResult = await _catalogProviderService.GetAllAsync(isIncludeDisabled: true);
        if (providersResult.IsFailure || providersResult.Data is null) return null;

        return providersResult.Data
            .Where(p => !string.IsNullOrWhiteSpace(p.BaseUrl)
                        && url.StartsWith(p.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.BaseUrl.Length)
            .Select(p => p.Name)
            .FirstOrDefault();
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResolveUrlMdp(string mp, string mdp)
    {
        string mediaDeliveryProfileId = CryptographyHelper.Decrypt(mdp, _configuration["SecuritySettings:ContentEncryptionKey"]!);
        string mediaPath = CryptographyHelper.Decrypt(mp, _configuration["SecuritySettings:ContentEncryptionKey"]!);

        var normalizedPath = mediaPath.Trim().ToLowerInvariant();
        var cacheKey = $"resolve-url:{normalizedPath}:{mediaDeliveryProfileId}";
        var responseCache = _cacheService.GetValue<GetResolveUrlResponseDto?>(cacheKey);

        if (responseCache != null)
            return Ok(responseCache);

        var response = await _urlResolver.ResolveUrlAsync(mediaPath, mediaDeliveryProfileId);

        if (response.IsFailure)
            return StatusCode(int.Parse(response.Error.Code), response.Error.Description);
        
        _cacheService.SetValue<GetResolveUrlResponseDto?>(cacheKey, response.Data);

        return Ok(response.Data);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResolveUrlFx(string uf, string sf, bool fr = false)
    {
        string urlFixed = CryptographyHelper.Decrypt(uf, _configuration["SecuritySettings:ContentEncryptionKey"]!);
        string streamFormat = CryptographyHelper.Decrypt(sf, _configuration["SecuritySettings:ContentEncryptionKey"]!);

        var response = await _urlResolver.ResolveUrlFixedAsync(urlFixed, streamFormat, fr);

        if (response.IsFailure)
            return StatusCode(int.Parse(response.Error.Code), response.Error.Description);

        return Ok(response.Data);
    }

    /// <summary>
    /// Repassa os bytes de uma URL http:// externa pela origem https do proprio servidor.
    /// Existe porque paginas https bloqueiam &lt;video src="http://..."&gt; (mixed content) -
    /// esse bloqueio nao tem contorno client-side. "u" chega cifrado (ver AvoidMixedContent
    /// em MediaDeliveryUrlResolver) para a URL de destino nunca ser controlavel pelo cliente.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("MediaDeliveryProfiles/StreamMedia/{fileName}")]
    public async Task<IActionResult> StreamMedia(string fileName, string u)
    {
        string url;
        try
        {
            url = CryptographyHelper.Decrypt(u, _configuration["SecuritySettings:ContentEncryptionKey"]!);
        }
        catch
        {
            return BadRequest();
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri)
            || (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
            return BadRequest();

        var client = _httpClientFactory.CreateClient(StreamHttpClientName);

        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Get, targetUri);

        if (Request.Headers.TryGetValue("Range", out var rangeValues) && rangeValues.Count > 0)
            upstreamRequest.Headers.TryAddWithoutValidation("Range", rangeValues.ToArray());

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to stream media from {targetUri}: {ex.Message}");
            return StatusCode(502);
        }

        using (upstreamResponse)
        {
            Response.StatusCode = (int)upstreamResponse.StatusCode;

            var contentType = upstreamResponse.Content.Headers.ContentType?.ToString();
            Response.ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

            if (upstreamResponse.Content.Headers.ContentLength is long contentLength)
                Response.ContentLength = contentLength;

            foreach (var acceptRangesValue in upstreamResponse.Headers.AcceptRanges)
                Response.Headers.Append("Accept-Ranges", acceptRangesValue);

            if (upstreamResponse.Content.Headers.ContentRange is not null)
                Response.Headers["Content-Range"] = upstreamResponse.Content.Headers.ContentRange.ToString();

            try
            {
                await using var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
                await upstreamStream.CopyToAsync(Response.Body, HttpContext.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // cliente cancelou (fechou o player, fez seek) - esperado, nao e erro
            }
        }

        return new EmptyResult();
    }

    [AllowAnonymous]
    [HttpGet("MediaDeliveryProfiles/StreamGaiaflixHls/{fileName}")]
    public async Task<IActionResult> StreamGaiaflixHls(string fileName, string u)
    {
        string url;
        try
        {
            url = CryptographyHelper.Decrypt(u, _configuration["SecuritySettings:ContentEncryptionKey"]!);
        }
        catch
        {
            return BadRequest();
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri)
            || !targetUri.Host.Equals("gaiaflix.live", StringComparison.OrdinalIgnoreCase)
            || !targetUri.AbsolutePath.Equals("/api/gaiaflix-hls", StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        var client = _httpClientFactory.CreateClient(StreamHttpClientName);
        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Get, targetUri);

        if (Request.Headers.TryGetValue("Range", out var rangeValues) && rangeValues.Count > 0)
            upstreamRequest.Headers.TryAddWithoutValidation("Range", rangeValues.ToArray());

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to fetch Gaiaflix HLS from {targetUri}: {ex.Message}");
            return StatusCode(502);
        }

        using (upstreamResponse)
        {
            if (!upstreamResponse.IsSuccessStatusCode)
                return StatusCode((int)upstreamResponse.StatusCode);

            var contentType = upstreamResponse.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = await upstreamResponse.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                var rewrittenManifest = RewriteGaiaflixManifest(manifest, targetUri);
                Response.Headers.CacheControl = "no-cache, no-store";
                return Content(rewrittenManifest, "application/vnd.apple.mpegurl", System.Text.Encoding.UTF8);
            }

            Response.StatusCode = (int)upstreamResponse.StatusCode;
            Response.ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

            if (upstreamResponse.Content.Headers.ContentLength is long contentLength)
                Response.ContentLength = contentLength;

            foreach (var acceptRangesValue in upstreamResponse.Headers.AcceptRanges)
                Response.Headers.Append("Accept-Ranges", acceptRangesValue);

            if (upstreamResponse.Content.Headers.ContentRange is not null)
                Response.Headers["Content-Range"] = upstreamResponse.Content.Headers.ContentRange.ToString();

            try
            {
                await using var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
                await upstreamStream.CopyToAsync(Response.Body, HttpContext.RequestAborted);
            }
            catch (OperationCanceledException)
            {
            }
        }

        return new EmptyResult();
    }

    private string RewriteGaiaflixManifest(string manifest, Uri baseUri)
    {
        var lines = manifest.Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith('#'))
            {
                var isMediaTrack = line.Contains("TYPE=SUBTITLES", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("TYPE=AUDIO", StringComparison.OrdinalIgnoreCase);
                lines[index] = System.Text.RegularExpressions.Regex.Replace(
                    line,
                    "URI=\"(?<uri>[^\"]+)\"",
                    match =>
                    {
                        var absoluteUrl = new Uri(baseUri, match.Groups["uri"].Value).AbsoluteUri;
                        var resolvedUrl = isMediaTrack ? absoluteUrl : BuildGaiaflixProxyUrl(absoluteUrl);
                        return $"URI=\"{resolvedUrl}\"";
                    },
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                continue;
            }

            lines[index] = BuildGaiaflixProxyUrl(new Uri(baseUri, line.Trim()).AbsoluteUri);
        }

        return string.Join("\n", lines);
    }

    private string BuildGaiaflixProxyUrl(string url)
    {
        var encryptedUrl = CryptographyHelper.Encrypt(url, _configuration["SecuritySettings:ContentEncryptionKey"]!);
        return $"{GetPublicBaseUrl()}/MediaDeliveryProfiles/StreamGaiaflixHls/playlist.m3u8?u={Uri.EscapeDataString(encryptedUrl)}";
    }

    private string GetPublicBaseUrl()
    {
        string scheme = Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && proto.Count > 0
            ? proto[0]!
            : Request.Scheme;

        string host = Request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost) && forwardedHost.Count > 0
            ? forwardedHost[0]!
            : Request.Host.Value;

        return $"{scheme}://{host}";
    }
}
