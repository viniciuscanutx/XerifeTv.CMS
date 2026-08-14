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
	ISiteUserService _siteUserService) : ControllerBase
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

		return Ok(new
		{
			user = ToUserResponse(userResult.Data),
			accessToken = loginResult.Data!.Token,
			refreshToken = loginResult.Data.RefreshToken
		});
	}

	[HttpPost("Refresh")]
	[AllowAnonymous]
	public async Task<IActionResult> Refresh(RefreshTokenRequestDto dto)
	{
		if (string.IsNullOrEmpty(dto.RefreshToken))
			return Unauthorized();

		var refreshResult = await _siteAuthService.TryRefreshSessionAsync(dto.RefreshToken);

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

		return Ok(new
		{
			user = ToUserResponse(userResult.Data),
			accessToken = newToken,
			refreshToken = newRefreshToken
		});
	}

	[HttpPost("Logout")]
	[AllowAnonymous]
	public IActionResult Logout() => Ok();

	[HttpGet("Me")]
	[AllowAnonymous]
	public async Task<IActionResult> Me()
	{
		var accessToken = GetBearerToken();

		var (isValid, userId) = await _siteTokenService.ValidateTokenAsync(accessToken ?? string.Empty);

		if (!isValid)
			return Unauthorized();

		var userResult = await _siteUserService.GetByIdAsync(userId!);

		if (userResult.IsFailure || userResult.Data is null)
			return Unauthorized();

		return Ok(new { user = ToUserResponse(userResult.Data) });
	}

	private string? GetBearerToken()
	{
		var header = Request.Headers.Authorization.ToString();

		if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
			return null;

		return header["Bearer ".Length..].Trim();
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
