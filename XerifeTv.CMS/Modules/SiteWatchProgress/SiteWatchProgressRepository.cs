using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.SiteWatchProgress.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SiteWatchProgress;

public sealed class SiteWatchProgressRepository(IOptions<DBSettings> options)
    : BaseRepository<WatchProgressEntity>(ECollection.SITE_WATCH_PROGRESS, options), ISiteWatchProgressRepository
{
    public async Task<IEnumerable<WatchProgressEntity>> GetBySiteUserIdAsync(string siteUserId, int limit)
    {
        return await _collection
            .Find(r => r.SiteUserId == siteUserId)
            .SortByDescending(r => r.UpdateAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<WatchProgressEntity?> GetBySiteUserAndContentAsync(string siteUserId, string contentId)
    {
        return await _collection
            .Find(r => r.SiteUserId == siteUserId && r.ContentId == contentId)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteBySiteUserAndContentAsync(string siteUserId, string contentId)
    {
        await _collection.DeleteOneAsync(r => r.SiteUserId == siteUserId && r.ContentId == contentId);
    }
}
