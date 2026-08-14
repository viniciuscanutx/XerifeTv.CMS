using XerifeTv.CMS.Modules.Abstractions.Entities;

namespace XerifeTv.CMS.Modules.SiteWatchProgress;

public class WatchProgressEntity : BaseEntity
{
    public string SiteUserId { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Poster { get; set; } = string.Empty;
    public string? Backdrop { get; set; }
    public double CurrentTime { get; set; }
    public double Duration { get; set; }
    public int ProgressPercentage { get; set; }
}
