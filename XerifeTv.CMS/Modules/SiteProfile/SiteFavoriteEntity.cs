using XerifeTv.CMS.Modules.Abstractions.Entities;

namespace XerifeTv.CMS.Modules.SiteProfile;

public class SiteFavoriteEntity : BaseEntity
{
    public string SiteUserId { get; set; } = string.Empty;
    public string MovieId { get; set; } = string.Empty;
    public string ContentType { get; set; } = "movie";
    public string Title { get; set; } = string.Empty;
    public string Poster { get; set; } = string.Empty;
}
