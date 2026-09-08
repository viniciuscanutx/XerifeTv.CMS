using XerifeTv.CMS.Modules.SiteBadge;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.SiteProfile;
using XerifeTv.CMS.Modules.SiteReview;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;
using XerifeTv.CMS.Modules.SiteWatchProgress.Dtos.Response;
using XerifeTv.CMS.Modules.SiteWatchProgress.Interfaces;

namespace XerifeTv.CMS.Controllers.ContentAPI;

[Route("Api/Profile")]
public sealed class ProfileController(ISiteUserRepository users, ISiteProfileService profiles,
    ISiteReviewService reviews, ISiteWatchProgressRepository progress, ISiteBadgeService badges) : SiteAccountController(users)
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await profiles.GetAsync(CurrentUserId));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateSiteProfileRequest request)
        => Ok(await profiles.UpdateAsync(CurrentUserId, request));

    [HttpPut("Badge")]
    public async Task<IActionResult> SelectBadge(SelectSiteBadgeRequest request)
    {
        await badges.SelectAsync(CurrentUserId, request.BadgeId);
        return Ok(await profiles.GetAsync(CurrentUserId));
    }

    [HttpGet("Favorites")]
    public async Task<IActionResult> Favorites([FromQuery, Range(1, 10000)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 24,
        [FromQuery, RegularExpression("^(movie|series)$")] string contentType = "movie")
        => Ok(await profiles.GetFavoritesAsync(CurrentUserId, page, pageSize, contentType));

    [HttpGet("Favorites/{movieId}")]
    public async Task<IActionResult> IsFavorite(string movieId,
        [FromQuery, RegularExpression("^(movie|series)$")] string contentType = "movie")
        => Ok(new { isFavorite = await profiles.IsFavoriteAsync(CurrentUserId, movieId, contentType) });

    [HttpPut("Favorites/{movieId}")]
    public async Task<IActionResult> AddFavorite(string movieId,
        [FromQuery, RegularExpression("^(movie|series)$")] string contentType = "movie")
    {
        await profiles.AddFavoriteAsync(CurrentUserId, movieId, contentType);
        return NoContent();
    }

    [HttpDelete("Favorites/{movieId}")]
    public async Task<IActionResult> DeleteFavorite(string movieId,
        [FromQuery, RegularExpression("^(movie|series)$")] string contentType = "movie")
    {
        await profiles.DeleteFavoriteAsync(CurrentUserId, movieId, contentType);
        return NoContent();
    }

    [HttpGet("Recent")]
    public async Task<IActionResult> Recent([FromQuery, Range(1, 10000)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 24,
        [FromQuery, RegularExpression("^(movie|series|all)$")] string contentType = "movie")
    {
        var entries = await progress.GetRecentMoviesAsync(CurrentUserId, page, pageSize, contentType);
        return Ok(new ProfilePage<GetWatchProgressResponseDto>(
            entries.Take(pageSize).Select(GetWatchProgressResponseDto.FromEntity).ToArray(),
            page, pageSize, entries.Count > pageSize));
    }

    [HttpGet("Reviews")]
    public async Task<IActionResult> Reviews([FromQuery, Range(1, 10000)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 12,
        [FromQuery, RegularExpression("^(movie|series|all)$")] string contentType = "movie")
        => Ok(await reviews.GetByUserAsync(CurrentUserId, page, pageSize, contentType));
}
