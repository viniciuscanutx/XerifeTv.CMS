using System.ComponentModel.DataAnnotations;
using XerifeTv.CMS.Modules.Movie;
using XerifeTv.CMS.Modules.SiteUser;

namespace XerifeTv.CMS.Modules.SiteProfile;

public record UpdateSiteProfileRequest(
    [Required, StringLength(100)] string Name,
    [StringLength(2048)] string? AvatarUrl,
    [StringLength(100), RegularExpression("^[a-zA-Z0-9]+$")] string? AvatarGiphyId = null);

public record SiteProfileResponse(string Id, string Name, string? AvatarUrl, DateTime JoinedAt, string? AvatarGiphyId = null)
{
    public static SiteProfileResponse FromEntity(SiteUserEntity user)
        => new(user.Id, user.Name, user.AvatarUrl, user.CreateAt, user.AvatarGiphyId);
}

public record ProfilePage<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);

public record ProfileMovieResponse(string Id, string Title, string Poster, int Year, bool IsAvailable, string Type = "movie")
{
    public static ProfileMovieResponse FromEntity(MovieEntity? movie, string id, string title, string poster)
        => new(id, movie?.Title ?? title, movie?.PosterUrl ?? poster, movie?.ReleaseYear ?? 0, movie is { Disabled: false });
    public static ProfileMovieResponse FromSeries(XerifeTv.CMS.Modules.Series.SeriesEntity? series, string id, string title, string poster)
        => new(id, series?.Title ?? title, series?.PosterUrl ?? poster, series?.ReleaseYear ?? 0,
            series is { Disabled: false }, "series");
}

public record FavoriteResponse(ProfileMovieResponse Movie, DateTime AddedAt);
