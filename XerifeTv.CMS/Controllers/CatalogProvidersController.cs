using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.CatalogProvider.Dtos.Request;
using XerifeTv.CMS.Modules.CatalogProvider.Interfaces;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Controllers;

[Authorize(Roles = "admin")]
public class CatalogProvidersController(
    ICatalogProviderService _service,
    IActivityLogService _activityLogService,
    ILogger<CatalogProvidersController> _logger) : Controller
{
    public async Task<IActionResult> Create(CreateCatalogProviderRequestDto dto)
    {
        var response = await _service.CreateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Provedor de catálogo cadastrado com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} registered the catalog provider {dto.Name}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "created", $"cadastrou o provedor de catálogo \"{dto.Name}\"");

        return Redirect(Url.Action("Index", "Settings") + "#catalog-providers");
    }

    public async Task<IActionResult> Update(UpdateCatalogProviderRequestDto dto)
    {
        var response = await _service.UpdateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Provedor de catálogo atualizado com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} updated the catalog provider {dto.Name}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "updated", $"atualizou o provedor de catálogo \"{dto.Name}\"");

        return Redirect(Url.Action("Index", "Settings") + "#catalog-providers");
    }

    public async Task<IActionResult> Delete(string? id)
    {
        if (id is not null)
        {
            var response = await _service.DeleteAsync(id);

            TempData["Notification"] = response.IsFailure
              ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
              : MessageViewHelper.SuccessJson("Provedor de catálogo deletado com sucesso");

            _logger.LogInformation($"{User.Identity?.Name} removed the catalog provider with id = {id}");
            await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "deleted", $"removeu o provedor de catálogo com id = {id}");
        }

        return Redirect(Url.Action("Index", "Settings") + "#catalog-providers");
    }
}
