using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.SiteAuthentication.Dtos.Request;
using XerifeTv.CMS.Modules.SiteAuthentication.Interfaces;
using XerifeTv.CMS.Modules.SiteUser.Dtos.Response;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;

namespace XerifeTv.CMS.Controllers.ContentAPI;

[Route("Api/Auth")]
[ApiController]
[EnableCors("AuthApi")]
public class AuthController(
	ISiteAuthService _siteAuthService,
	ISiteTokenService _siteTokenService,
	ISiteUserService _siteUserService,
	IConfiguration _configuration) : ControllerBase
{
	[HttpPost("Login")]
	[AllowAnonymous]
	public async Task<IActionResult> Login(SiteLoginRequestDto dto)
	{
		var loginResult = await _siteAuthService.LoginAsync(dto);

		if (loginResult.IsFailure)
			return Unauthorized(new { message = loginResult.Error.Description });

		var userResult = await _siteUserService.GetByEmailAsync(dto.Email);

		if (userResult.IsFailure || userResult.Data is null)
			return Unauthorized();

		AppendAuthCookies(loginResult.Data!.Token, loginResult.Data.RefreshToken);

		return Ok(new { user = ToUserResponse(userResult.Data) });
	}

	[HttpPost("Refresh")]
	[AllowAnonymous]
	public async Task<IActionResult> Refresh()
	{
		var refreshToken = Request.Cookies["refresh_token"];

		if (string.IsNullOrEmpty(refreshToken))
			return Unauthorized();

		var refreshResult = await _siteAuthService.TryRefreshSessionAsync(refreshToken);

		if (refreshResult.IsFailure)
			return Unauthorized();

		var (newToken, newRefreshToken) = refreshResult.Data;

		if (string.IsNullOrEmpty(newToken) || string.IsNullOrEmpty(newRefreshToken))
			return Unauthorized();

		var (isValid, userId) = await _siteTokenService.ValidateTokenAsync(newToken);

		if (!isValid)
			return Unauthorized();

		var userResult = await _siteUserService.GetByIdAsync(userId!);

		if (userResult.IsFailure || userResult.Data is null)
			return Unauthorized();

		AppendAuthCookies(newToken, newRefreshToken);

		return Ok(new { user = ToUserResponse(userResult.Data) });
	}

	[HttpPost("Logout")]
	[AllowAnonymous]
	public IActionResult Logout()
	{
		Response.Cookies.Delete("access_token");
		Response.Cookies.Delete("refresh_token");

		return Ok();
	}

	[HttpGet("Me")]
	[AllowAnonymous]
	public async Task<IActionResult> Me()
	{
		var accessToken = Request.Cookies["access_token"];

		var (isValid, userId) = await _siteTokenService.ValidateTokenAsync(accessToken ?? string.Empty);

		if (!isValid)
			return Unauthorized();

		var userResult = await _siteUserService.GetByIdAsync(userId!);

		if (userResult.IsFailure || userResult.Data is null)
			return Unauthorized();

		return Ok(new { user = ToUserResponse(userResult.Data) });
	}

	private void AppendAuthCookies(string accessToken, string refreshToken)
	{
		_ = int.TryParse(_configuration["SiteJwt:ExpirationTimeInMinutes"], out var accessExpirationMinutes);
		_ = int.TryParse(_configuration["SiteJwt:RefreshExpirationTimeInMinutes"], out var refreshExpirationMinutes);

		Response.Cookies.Append("access_token", accessToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = true,
			SameSite = SameSiteMode.None,
			Expires = DateTimeOffset.UtcNow.AddMinutes(accessExpirationMinutes)
		});

		Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = true,
			SameSite = SameSiteMode.None,
			Expires = DateTimeOffset.UtcNow.AddMinutes(refreshExpirationMinutes)
		});
	}

	private static object ToUserResponse(GetSiteUserResponseDto user)
		=> new
		{
			id = user.Id,
			name = user.Name,
			email = user.Email,
			roleId = user.RoleId,
			roleName = user.RoleName,
			permissions = user.Permissions
		};
}
