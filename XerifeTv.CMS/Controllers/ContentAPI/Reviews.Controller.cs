using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.SiteReview;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;

namespace XerifeTv.CMS.Controllers.ContentAPI;

[Route("Api/Reviews")]
public sealed class ReviewsController(ISiteUserRepository users, ISiteReviewService reviews) : SiteAccountController(users)
{
    [HttpGet("Movies/{movieId}")]
    public async Task<IActionResult> GetByMovie(string movieId, [FromQuery, Range(1, 10000)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 12)
        => Ok(await reviews.GetByContentAsync(movieId, page, pageSize));

    [HttpGet("Movies/{movieId}/Me")]
    public async Task<IActionResult> GetOwn(string movieId)
        => Ok(new { review = await reviews.GetOwnAsync(CurrentUserId, movieId) });

    [HttpPut("Movies/{movieId}/Me")]
    public async Task<IActionResult> Save(string movieId, SaveSiteReviewRequest request)
        => Ok(await reviews.SaveAsync(CurrentUserId, movieId, request));

    [HttpDelete("Movies/{movieId}/Me")]
    public async Task<IActionResult> Delete(string movieId)
    {
        await reviews.DeleteAsync(CurrentUserId, movieId);
        return NoContent();
    }

    [HttpGet("Series/{seriesId}")]
    public async Task<IActionResult> GetBySeries(string seriesId, [FromQuery, Range(1, 10000)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 12)
        => Ok(await reviews.GetByContentAsync(seriesId, page, pageSize, "series"));

    [HttpGet("Series/{seriesId}/Me")]
    public async Task<IActionResult> GetOwnSeries(string seriesId)
        => Ok(new { review = await reviews.GetOwnAsync(CurrentUserId, seriesId, "series") });

    [HttpPut("Series/{seriesId}/Me")]
    public async Task<IActionResult> SaveSeries(string seriesId, SaveSiteReviewRequest request)
        => Ok(await reviews.SaveAsync(CurrentUserId, seriesId, request, "series"));

    [HttpDelete("Series/{seriesId}/Me")]
    public async Task<IActionResult> DeleteSeries(string seriesId)
    {
        await reviews.DeleteAsync(CurrentUserId, seriesId, "series");
        return NoContent();
    }
}
