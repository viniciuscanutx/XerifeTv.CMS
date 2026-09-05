using System.ComponentModel.DataAnnotations;
using XerifeTv.CMS.Modules.Abstractions.Entities;
using XerifeTv.CMS.Modules.SiteProfile;

namespace XerifeTv.CMS.Modules.SiteReview;

public class SiteReviewEntity : BaseEntity
{
    public string SiteUserId { get; set; } = string.Empty;
    public string MovieId { get; set; } = string.Empty;
    public string ContentType { get; set; } = "movie";
    public string Title { get; set; } = string.Empty;
    public string Poster { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
}

public record SaveSiteReviewRequest([Range(1, 5)] int Rating, [Required, StringLength(2000)] string Text);
public record ReviewAuthorResponse(string Id, string Name, string? AvatarUrl, string? AvatarGiphyId = null);
public record SiteReviewResponse(string Id, ProfileMovieResponse Movie, ReviewAuthorResponse Author,
    int Rating, string Text, DateTime CreatedAt, DateTime? UpdatedAt);
