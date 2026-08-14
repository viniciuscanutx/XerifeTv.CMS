using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.SiteWatchProgress.Dtos.Request;
using XerifeTv.CMS.Modules.SiteWatchProgress.Interfaces;

namespace XerifeTv.CMS.Controllers.ContentAPI;

[Route("Api/WatchProgress")]
[ApiController]
[EnableCors("AuthApi")]
[Authorize(AuthenticationSchemes = "SiteJwt")]
public class WatchProgressController(
	ISiteWatchProgressService _service) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> Get([FromQuery] int limit = 20)
	{
		var response = await _service.GetContinueWatchingAsync(CurrentUserId, limit);

		if (response.IsFailure)
			return BadRequest(response.Error.Description);

		return Ok(response.Data);
	}

	[HttpPost]
	public async Task<IActionResult> Upsert(UpsertWatchProgressRequestDto dto)
	{
		var response = await _service.UpsertAsync(CurrentUserId, dto);

		if (response.IsFailure)
			return BadRequest(response.Error.Description);

		return Ok(response.Data);
	}

	[HttpDelete("{contentId}")]
	public async Task<IActionResult> Delete(string contentId)
	{
		var response = await _service.DeleteAsync(CurrentUserId, contentId);

		if (response.IsFailure)
			return BadRequest(response.Error.Description);

		return Ok();
	}

	private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
