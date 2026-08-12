using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.Franchise.Dtos.Response;
using XerifeTv.CMS.Modules.Franchise.Dtos.Request;
using XerifeTv.CMS.Modules.Franchise.Interfaces;

namespace XerifeTv.CMS.Controllers;

[Authorize]
public class FranchisesController(
    IFranchiseService _service,
    IActivityLogService _activityLogService,
    ILogger<FranchisesController> _logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Search(string? term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Ok(Enumerable.Empty<GetFranchiseResponseDto>());

        var response = await _service.SearchByNameAsync(term ?? string.Empty);

        if (response.IsFailure)
            return BadRequest(response.Error.Description ?? string.Empty);

        return Ok(response.Data);
    }

    [Authorize(Roles = "admin, common")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateFranchiseRequestDto dto)
    {
        var response = await _service.CreateAsync(dto);

        if (response.IsFailure)
            return BadRequest(response.Error.Description ?? string.Empty);

        _logger.LogInformation($"{User.Identity?.Name} created franchise {dto.Name}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Franquias", "created", $"cadastrou a franquia \"{dto.Name}\"");

        return Ok(response.Data);
    }

    [Authorize(Roles = "admin, common")]
    [HttpPost]
    public async Task<IActionResult> Update(UpdateFranchiseRequestDto dto)
    {
        var response = await _service.UpdateAsync(dto);

        if (response.IsFailure)
            return BadRequest(response.Error.Description ?? string.Empty);

        _logger.LogInformation($"{User.Identity?.Name} updated franchise {dto.Id}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Franquias", "updated", $"atualizou a franquia \"{dto.Name}\"");

        return Ok(response.Data);
    }

    [Authorize(Roles = "admin, common")]
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var response = await _service.DeleteAsync(id);

        if (response.IsFailure)
            return BadRequest(response.Error.Description ?? string.Empty);

        _logger.LogInformation($"{User.Identity?.Name} deleted franchise {id}");
        await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Franquias", "deleted", $"removeu a franquia com id = {id}");

        return Ok();
    }
}
