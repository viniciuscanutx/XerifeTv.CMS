using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Common.Enums;
using XerifeTv.CMS.Modules.Integrations.Webhook;
using XerifeTv.CMS.Modules.Integrations.Webhook.Dtos.Request;
using XerifeTv.CMS.Modules.Integrations.Webhook.Dtos.Response;
using XerifeTv.CMS.Modules.Integrations.Webhook.Enums;
using XerifeTv.CMS.Modules.Integrations.Webhook.Interfaces;
using XerifeTv.CMS.Modules.Media.Delivery.Intefaces;
using XerifeTv.CMS.Modules.User.Dtos.Request;
using XerifeTv.CMS.Modules.User.Enums;
using XerifeTv.CMS.Modules.User.Interfaces;
using XerifeTv.CMS.Shared.Helpers;
using XerifeTv.CMS.Views.Settings.Models;

using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Controllers;

public class SettingsController(
    IUserService _userService,
    IWebhookService _webhookService,
    IMediaDeliveryProfileService _mediaDeliveryProfileService,
    ISystemSettingsService _systemSettingsService,
    ICacheService _cacheService,
    ILogger<SettingsController> _logger) : Controller
{
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var userResponse = await _userService.GetByUsernameAsync(User.Identity?.Name ?? string.Empty);
        if (userResponse.IsFailure) return RedirectToAction("Logout", "Users");

        var webhooksResponse = await _webhookService.GetAsync(currentPage: 1, limit: 50);
        if (webhooksResponse.IsFailure) return RedirectToAction("Index", "Home");

        var mediaDeliveryProfilesResponse = await _mediaDeliveryProfileService.GetAllAsync(isIncludeDisabled: true);
        if (mediaDeliveryProfilesResponse.IsFailure) return RedirectToAction("Index", "Home");

        ViewBag.EnableMoviesSpreadsheetImport = userResponse.Data?.EnableMoviesSpreadsheetImport ?? _systemSettingsService.IsMoviesSpreadsheetImportEnabled();
        ViewBag.EnableSeriesSpreadsheetImport = userResponse.Data?.EnableSeriesSpreadsheetImport ?? _systemSettingsService.IsSeriesSpreadsheetImportEnabled();
        ViewBag.EnableChannelsSpreadsheetImport = userResponse.Data?.EnableChannelsSpreadsheetImport ?? _systemSettingsService.IsChannelsSpreadsheetImportEnabled();
        ViewBag.ImdbSearchMode = userResponse.Data?.ImdbSearchMode ?? _systemSettingsService.GetDefaultImdbSearchMode();

        SettingsModelView model = new(userResponse.Data!, webhooksResponse.Data?.Items ?? [], mediaDeliveryProfilesResponse.Data ?? []);

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "admin, common")]
    public async Task<IActionResult> UpdateImportSettings(
        bool enableMoviesSpreadsheetImport = false,
        bool enableSeriesSpreadsheetImport = false,
        bool enableChannelsSpreadsheetImport = false)
    {
        _systemSettingsService.SetSpreadsheetImportSettings(
            enableMoviesSpreadsheetImport,
            enableSeriesSpreadsheetImport,
            enableChannelsSpreadsheetImport);

        var userResponse = await _userService.GetByUsernameAsync(User.Identity?.Name ?? string.Empty);
        if (userResponse.IsSuccess && userResponse.Data != null)
        {
            var updateUserDto = new UpdateUserRequestDto
            {
                Id = userResponse.Data.Id,
                Email = userResponse.Data.Email,
                UserName = userResponse.Data.UserName,
                Role = userResponse.Data.Role,
                Blocked = userResponse.Data.Blocked,
                EnableMoviesSpreadsheetImport = enableMoviesSpreadsheetImport,
                EnableSeriesSpreadsheetImport = enableSeriesSpreadsheetImport,
                EnableChannelsSpreadsheetImport = enableChannelsSpreadsheetImport
            };
            await _userService.UpdateAsync(updateUserDto);
        }

        TempData["Notification"] = MessageViewHelper.SuccessJson("Configurações de importação atualizadas com sucesso!");

        _logger.LogInformation($"{User.Identity?.Name} updated import settings (Movies:{enableMoviesSpreadsheetImport}, Series:{enableSeriesSpreadsheetImport}, Channels:{enableChannelsSpreadsheetImport})");

        return Redirect(Url.Action("Index") + "#v-pills-import");
    }

    [HttpPost]
    [Authorize(Roles = "admin, common")]
    public async Task<IActionResult> UpdateSearchSettings(EImdbSearchMode imdbSearchMode)
    {
        _systemSettingsService.SetImdbSearchMode(imdbSearchMode);

        var userResponse = await _userService.GetByUsernameAsync(User.Identity?.Name ?? string.Empty);
        if (userResponse.IsSuccess && userResponse.Data != null)
        {
            var updateUserDto = new UpdateUserRequestDto
            {
                Id = userResponse.Data.Id,
                Email = userResponse.Data.Email,
                UserName = userResponse.Data.UserName,
                Role = userResponse.Data.Role,
                Blocked = userResponse.Data.Blocked,
                EnableMoviesSpreadsheetImport = userResponse.Data.EnableMoviesSpreadsheetImport,
                EnableSeriesSpreadsheetImport = userResponse.Data.EnableSeriesSpreadsheetImport,
                EnableChannelsSpreadsheetImport = userResponse.Data.EnableChannelsSpreadsheetImport,
                ImdbSearchMode = imdbSearchMode
            };
            await _userService.UpdateAsync(updateUserDto);
        }

        TempData["Notification"] = MessageViewHelper.SuccessJson("Preferência de busca atualizada com sucesso!");

        _logger.LogInformation($"{User.Identity?.Name} updated the default content search mode to {imdbSearchMode}");

        return Redirect(Url.Action("Index") + "#v-pills-import");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UserUpdateProfile(UpdateUserProfileRequestDto dto)
    {
        var updateUserRequestDto = new UpdateUserRequestDto
        {
            Id = dto.Id,
            Email = dto.Email,
            UserName = dto.UserName,
            Role = null,
            Blocked = null
        };

        var response = await _userService.UpdateAsync(updateUserRequestDto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Perfil atualizado com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} updated your own profile");

        return Redirect(Url.Action("Index") + "#profile");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UserUpdatePassword(UpdatePasswordUserRequestDto dto)
    {
        if (dto.NewPassword != dto.NewPasswordConfirm)
        {
            TempData["Notification"] = MessageViewHelper.ErrorJson("Confirmacao de senha incorreta");
            return Redirect(Url.Action("Index") + "#password");
        }

        var response = await _userService.UpdatePasswordAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Senha atualizada com sucesso");

        _logger.LogInformation($"{User.Identity?.Name} updated your password");

        return Redirect(Url.Action("Index") + "#password");
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RegisterWebhook(CreateWebhookRequestDto dto)
    {
        var response = await _webhookService.CreateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Webhook cadastrado com sucesso");

        return Redirect(Url.Action("Index") + "#webhook");
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateWebhook(UpdateWebhookRequestDto dto)
    {
        var response = await _webhookService.UpdateAsync(dto);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Webhook atualizado com sucesso");

        return Redirect(Url.Action("Index") + "#webhook");
    }

    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteWebhook(string id)
    {
        var response = await _webhookService.DeleteAsync(id);

        TempData["Notification"] = response.IsFailure
          ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
          : MessageViewHelper.SuccessJson("Webhook deletado com sucesso");

        return Redirect(Url.Action("Index") + "#webhook");
    }

    [HttpPost]
    [Authorize(Roles = "admin, common")]
    public IActionResult ClearCache()
    {
        _cacheService.Clear();

        TempData["Notification"] = MessageViewHelper.SuccessJson("Cache da API limpo com sucesso!");

        _logger.LogInformation($"{User.Identity?.Name} cleared the API cache");

        return Redirect(Url.Action("Index") + "#v-pills-cache");
    }
}