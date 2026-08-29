using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.LinkTemplate.Dtos.Request;
using XerifeTv.CMS.Modules.LinkTemplate.Interfaces;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Controllers;

[Authorize(Roles = "admin")]
public class LinkTemplatesController(
    ILinkTemplateService _service,
    IActivityLogService _activityLogService,
    ILogger<LinkTemplatesController> _logger) : Controller
{
    public async Task<IActionResult> Create(CreateLinkTemplateRequestDto dto)
    {
        var response = await _service.CreateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Tipo de link cadastrado com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} registered the link template {dto.Name}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "created", $"cadastrou o tipo de link \"{dto.Name}\"");

        return Redirect(Url.Action("Index", "Settings") + "#link-templates");
    }

    public async Task<IActionResult> Update(UpdateLinkTemplateRequestDto dto)
    {
        var response = await _service.UpdateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Tipo de link atualizado com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} updated the link template {dto.Name}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "updated", $"atualizou o tipo de link \"{dto.Name}\"");

        return Redirect(Url.Action("Index", "Settings") + "#link-templates");
    }

    public async Task<IActionResult> Delete(string? id)
    {
        if (id is not null)
        {
            var response = await _service.DeleteAsync(id);

            TempData["Notification"] = response.IsFailure
              ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
              : MessageViewHelper.SuccessJson("Tipo de link deletado com sucesso");

            _logger.LogInformation($"{User.Identity?.Name} removed the link template with id = {id}");
            await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Configurações", "deleted", $"removeu o tipo de link com id = {id}");
        }

        return Redirect(Url.Action("Index", "Settings") + "#link-templates");
    }
}
