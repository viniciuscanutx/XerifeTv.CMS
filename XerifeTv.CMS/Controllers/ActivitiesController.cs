using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Activity.Dtos.Response;
using XerifeTv.CMS.Modules.Activity.Interfaces;

namespace XerifeTv.CMS.Controllers;

[Authorize(Roles = "admin")]
public class ActivitiesController(IActivityLogService _service) : Controller
{
    private const int limitResultsPage = 20;

    public async Task<IActionResult> Index(int? currentPage)
    {
        var result = await _service.GetAsync(currentPage ?? 1, limitResultsPage);

        if (result.IsSuccess)
        {
            ViewBag.CurrentPage = result.Data?.CurrentPage;
            ViewBag.TotalPages = result.Data?.TotalPageCount ?? 1;
            ViewBag.HasNextPage = result.Data?.HasNext;
            ViewBag.HasPrevPage = result.Data?.HasPrevious;

            return View(result.Data?.Items);
        }

        return View(Enumerable.Empty<GetActivityLogResponseDto>());
    }
}
