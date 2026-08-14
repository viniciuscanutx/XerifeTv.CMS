using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.Authentication.Dtos.Request;
using XerifeTv.CMS.Modules.Authentication.Interfaces;
using XerifeTv.CMS.Modules.SiteRole.Interfaces;
using XerifeTv.CMS.Modules.SiteUser.Dtos.Request;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;
using XerifeTv.CMS.Modules.User.Dtos.Request;
using XerifeTv.CMS.Modules.User.Dtos.Response;
using XerifeTv.CMS.Modules.User.Interfaces;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Controllers;

public class UsersController(
	IUserService _userService,
	IAuthService _authService,
	ISiteUserService _siteUserService,
	ISiteRoleService _siteRoleService,
	IActivityLogService _activityLogService,
	IConfiguration _configuration,
	ILogger<UsersController> _logger) : Controller
{
	private readonly CookieOptions _cookieOptions = new()
	{
		HttpOnly = true,
		Secure = true,
		SameSite = SameSiteMode.Strict,
		Expires = DateTime.UtcNow.AddHours(6)
	};

	[Authorize(Roles = "admin")]
	public async Task<IActionResult> Index()
	{
		var response = await _userService.GetAsync(1, 20);
		var siteRolesResponse = await _siteRoleService.GetAllAsync();
		var siteUsersResponse = await _siteUserService.GetAllAsync();

		_logger.LogInformation($"{User.Identity?.Name} accessed the users page");

		ViewBag.SiteRoles = siteRolesResponse.Data ?? [];
		ViewBag.SiteUsers = siteUsersResponse.Data ?? [];

		if (response.IsSuccess)
			return View(response.Data?.Items);

		return View(Enumerable.Empty<GetUserResponseDto>());
	}

	[HttpPost]
	[Authorize(Roles = "admin")]
	public async Task<IActionResult> RegisterSiteUser(CreateSiteUserRequestDto dto)
	{
		var response = await _siteUserService.CreateAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Usuário do site \"{dto.Name}\" cadastrado com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} registered a new site user");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Usuários", "created", $"cadastrou o usuário do site \"{dto.Email}\"");

		return RedirectToAction("Index");
	}

	[HttpPost]
	[Authorize(Roles = "admin")]
	public async Task<IActionResult> UpdateSiteUser(UpdateSiteUserRequestDto dto)
	{
		var response = await _siteUserService.UpdateAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Usuário do site \"{dto.Name}\" atualizado com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} updated site user {dto.Id}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Usuários", "updated", $"atualizou o usuário do site \"{dto.Email}\"");

		return RedirectToAction("Index");
	}

	[Authorize(Roles = "admin")]
	public async Task<IActionResult> DeleteSiteUser(string id)
	{
		var response = await _siteUserService.DeleteAsync(id);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson("Usuário do site removido com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} removed site user with id = {id}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Usuários", "deleted", $"removeu o usuário do site com id = {id}");

		return RedirectToAction("Index");
	}

	[AllowAnonymous]
	public IActionResult SignIn()
	{
		if (User.Identity != null && User.Identity.IsAuthenticated)
			return RedirectToAction("Index", "Home");

		ViewBag.GoogleClientId = _configuration["OAuth2Google:ClientId"];

		return View();
	}

	[HttpPost]
	[AllowAnonymous]
	public async Task<IActionResult> SignIn(LoginRequestDto dto)
	{
		var response = await _authService.LoginAsync(dto);

		if (response.IsFailure)
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty);
			ViewBag.GoogleClientId = _configuration["OAuth2Google:ClientId"];
			_logger.LogInformation("There was an unsuccessful login attempt");
			return View();
		}

		Response.Cookies.Append("token", response.Data?.Token ?? string.Empty, _cookieOptions);
		Response.Cookies.Append("refreshToken", response.Data?.RefreshToken ?? string.Empty, _cookieOptions);

		_logger.LogInformation($"{User.Identity?.Name} logged into the system");
		await _activityLogService.LogAsync(dto.UserNameOrEmail, "Usuários", "login", $"fez login no sistema");

		return RedirectToAction("Index", "Home");
	}

	[AllowAnonymous]
	public IActionResult EmailResetPasswordForm()
	{
		if (User.Identity != null && User.Identity.IsAuthenticated)
			return RedirectToAction("Index", "Home");

		return View();
	}

	[HttpPost]
	[AllowAnonymous]
	public async Task<IActionResult> EmailResetPasswordForm(string email)
	{
		if (User.Identity != null && User.Identity.IsAuthenticated)
			return RedirectToAction("Index", "Home");

		var response = await _userService.SendEmailResetPasswordAsync(email);

		if (response.IsFailure)
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty);
			_logger.LogInformation($"{email} tried to send password reset email and failed");
			return View();
		}

		TempData["Notification"] = MessageViewHelper.SuccessJson("Email enviado com sucesso");
		_logger.LogInformation($"{email} tried to send password reset email");

		return View(model: email);
	}

	[AllowAnonymous]
	public async Task<IActionResult> ResetPassword(string code)
	{
		if (User.Identity != null && User.Identity.IsAuthenticated)
			return RedirectToAction("Index", "Home");

		var response = await _userService.ValidateResetPasswordGuidAsync(new Guid(code));

		if (response.IsFailure)
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty);
			return View();
		}

		return View(model: response.Data);
	}

	[HttpPost]
	[AllowAnonymous]
	public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto dto)
	{
		if (User.Identity != null && User.Identity.IsAuthenticated)
			return RedirectToAction("Index", "Home");

		if (dto.Password != dto.ConfirmPassword)
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson("Confirmacao de senha incorreta");
			return RedirectToAction("ResetPassword", new { code = dto.CodeGuid });
		}

		var response = await _userService.ResetPasswordAsync(dto);

		if (response.IsFailure)
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty);
			return RedirectToAction("ResetPassword", new { code = dto.CodeGuid });
		}

		TempData["Notification"] = MessageViewHelper.SuccessJson("Senha redefinida com sucesso");

		return RedirectToAction("SignIn");
	}

	[AllowAnonymous]
	public async Task<IActionResult> Logout()
	{
		_logger.LogInformation($"{User.Identity?.Name} logged out of the system");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Usuários", "logout", $"fez logout do sistema");

		Response.Cookies.Delete("token");
		Response.Cookies.Delete("refreshToken");
		return RedirectToAction("Index", "Home");
	}

	[HttpPost]
	[Authorize(Roles = "admin")]
	public async Task<IActionResult> Register(RegisterUserRequestDto dto)
	{
		var response = await _userService.RegisterAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Usuario {dto.UserName} cadastrado com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} registered a new user");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Usuários", "created", $"cadastrou o usuário \"{dto.UserName}\"");

		return RedirectToAction("Index");
	}

	[HttpPost]
	[Authorize(Roles = "admin")]
	public async Task<IActionResult> Update(UpdateUserRequestDto dto)
	{
		var response = await _userService.UpdateAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Usuario {dto.UserName} atualizado com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} updated user {dto.Id}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Usuários", "updated", $"atualizou o usuário \"{dto.UserName}\"");
		return RedirectToAction("Index");
	}

	[Authorize(Roles = "admin")]
	public async Task<IActionResult> Delete(string id)
	{
		var currentUserResponse = await _userService.GetByUsernameAsync(User.Identity?.Name ?? string.Empty);

		if (currentUserResponse.IsSuccess && currentUserResponse.Data?.Id == id)
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson("Você não pode excluir o próprio usuário");
			return RedirectToAction("Index");
		}

		var response = await _userService.DeleteAsync(id);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson("Usuario deletado com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} removed user with id = {id}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Usuários", "deleted", $"removeu o usuário com id = {id}");

		return RedirectToAction("Index");
	}

	[AllowAnonymous]
	public IActionResult UserUnauthorized()
	{
		_logger.LogInformation($"{User.Identity?.Name} tried to access a page for which he is not authorized");

		return View();
	}

	[AllowAnonymous]
	public async Task<IActionResult> RefreshSession(string? successRedirectUrl = null)
	{
		var refreshToken = Request.Cookies["refreshToken"];

		if (string.IsNullOrEmpty(refreshToken))
			return RedirectToAction("SignIn");

		var response = await _authService.TryRefreshSessionAsync(refreshToken);

		if (response.IsFailure)
			return RedirectToAction("SignIn");

		var (newToken, newRefreshToken) = response.Data;

		if (!string.IsNullOrEmpty(newToken) && !string.IsNullOrEmpty(newRefreshToken))
		{
			Response.Cookies.Append("token", newToken, _cookieOptions);
			Response.Cookies.Append("refreshToken", newRefreshToken, _cookieOptions);

			if (string.IsNullOrEmpty(successRedirectUrl))
				return RedirectToAction("Index", "Home");

			return Redirect(successRedirectUrl);
		}

		return RedirectToAction("SignIn");
	}
}