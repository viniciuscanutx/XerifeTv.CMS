using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.SiteWatchProgress.Interfaces;

public interface ISiteWatchProgressRepository : IBaseRepository<WatchProgressEntity>
{
    Task<IEnumerable<WatchProgressEntity>> GetBySiteUserIdAsync(string siteUserId, int limit);
    Task<WatchProgressEntity?> GetBySiteUserAndContentAsync(string siteUserId, string contentId);
    Task DeleteBySiteUserAndContentAsync(string siteUserId, string contentId);
}
