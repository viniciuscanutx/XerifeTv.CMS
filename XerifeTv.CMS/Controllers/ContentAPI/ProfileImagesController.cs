using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.SiteProfile;

namespace XerifeTv.CMS.Controllers.ContentAPI;

// Foto e banner do perfil são públicos (aparecem pra quem vê o perfil); o envio continua exigindo login
[ApiController]
[AllowAnonymous]
[Route("Api/ProfileImages")]
public sealed class ProfileImagesController(ISiteProfileService profiles) : ControllerBase
{
    [HttpGet("{imageId}")]
    public async Task<IActionResult> Get(string imageId)
    {
        var image = await profiles.GetImageAsync(imageId);
        if (image is null || image.Data.Length == 0)
            return NotFound(new { message = "Imagem não encontrada." });

        // Cada envio gera um id novo, então a imagem nunca muda: cache longo
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(image.Data, image.ContentType);
    }
}
