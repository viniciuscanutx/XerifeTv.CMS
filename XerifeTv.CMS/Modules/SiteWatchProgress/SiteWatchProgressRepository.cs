using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.SiteWatchProgress.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SiteWatchProgress;

public sealed class SiteWatchProgressRepository(IOptions<DBSettings> options)
    : BaseRepository<WatchProgressEntity>(ECollection.SITE_WATCH_PROGRESS, options), ISiteWatchProgressRepository
{
    public Task<List<WatchProgressEntity>> GetRecentMoviesAsync(string userId, int page, int pageSize, string contentType = "movie")
        => _collection.Find(x => x.SiteUserId == userId && (contentType == "all" ? x.Type == "movie" || x.Type == "series" : x.Type == contentType) && x.CurrentTime >= 5)
            .SortByDescending(x => x.UpdateAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Limit(pageSize + 1).ToListAsync();

    public async Task<IEnumerable<WatchProgressEntity>> GetBySiteUserIdAsync(string siteUserId, int limit)
    {
        return await _collection
            .Find(r => r.SiteUserId == siteUserId)
            .SortByDescending(r => r.UpdateAt)
            .Limit(Math.Clamp(limit, 1, 100))
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
