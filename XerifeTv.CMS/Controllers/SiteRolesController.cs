using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.SiteRole.Dtos.Request;
using XerifeTv.CMS.Modules.SiteRole.Interfaces;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Controllers;

[Authorize(Roles = "admin")]
public class SiteRolesController(
    ISiteRoleService _service,
    IActivityLogService _activityLogService,
    ILogger<SiteRolesController> _logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var response = await _service.GetAllAsync();

        return View(response.Data ?? []);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSiteRoleRequestDto dto)
    {
        var response = await _service.CreateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson($"Role \"{dto.Name}\" cadastrada com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} created site role {dto.Name}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Roles do Site", "created", $"cadastrou a role \"{dto.Name}\"");

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Update(UpdateSiteRoleRequestDto dto)
    {
        var response = await _service.UpdateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson($"Role \"{dto.Name}\" atualizada com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} updated site role {dto.Id}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Roles do Site", "updated", $"atualizou a role \"{dto.Name}\"");

        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Delete(string id)
    {
        var response = await _service.DeleteAsync(id);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Role removida com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} deleted site role {id}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Roles do Site", "deleted", $"removeu a role com id = {id}");

        return RedirectToAction("Index");
    }
}
