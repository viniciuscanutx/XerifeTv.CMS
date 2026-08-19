using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.Activity.Interfaces;
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
            return Ok(new { responseCache?.Url, responseCache?.StreamFormat });

        var response = await _urlResolver.ResolveUrlAsync(mediaPath, mediaDeliveryProfileId);

        if (response.IsFailure)
            return StatusCode(int.Parse(response.Error.Code), response.Error.Description);
        
        _cacheService.SetValue<GetResolveUrlResponseDto?>(cacheKey, response.Data);

        return Ok(new { response.Data?.Url, response.Data?.StreamFormat });
    }

    [Authorize(Roles = "admin, common")]
    [HttpGet]
    public async Task<IActionResult> ResolveUrlFixed(string urlFixed, string streamFormat, bool followRedirect = false)
    {
        var response = await _urlResolver.ResolveUrlFixedAsync(urlFixed, streamFormat, followRedirect);

        if (response.IsFailure)
            return StatusCode(int.Parse(response.Error.Code), response.Error.Description);

        return Ok(new { response.Data?.Url, response.Data?.StreamFormat });
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
            return Ok(new { responseCache?.Url, responseCache?.StreamFormat });

        var response = await _urlResolver.ResolveUrlAsync(mediaPath, mediaDeliveryProfileId);

        if (response.IsFailure)
            return StatusCode(int.Parse(response.Error.Code), response.Error.Description);
        
        _cacheService.SetValue<GetResolveUrlResponseDto?>(cacheKey, response.Data);

        return Ok(new { response.Data?.Url, response.Data?.StreamFormat });
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

        return Ok(new { response.Data?.Url, response.Data?.StreamFormat });
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
}