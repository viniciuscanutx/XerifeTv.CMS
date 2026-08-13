using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.Activity;

public sealed class ActivityLogRepository(IOptions<DBSettings> options)
    : BaseRepository<ActivityLogEntity>(ECollection.ACTIVITIES, options), IActivityLogRepository
{
    public async Task<PagedList<ActivityLogEntity>> GetByUserAsync(string? userName, int currentPage, int limit)
    {
        var filter = string.IsNullOrWhiteSpace(userName)
            ? Builders<ActivityLogEntity>.Filter.Empty
            : Builders<ActivityLogEntity>.Filter.Eq(x => x.UserName, userName);

        var count = await _collection.CountDocumentsAsync(filter);
        var items = await _collection.Find(filter)
            .SortByDescending(r => r.CreateAt)
            .Skip(limit * (currentPage - 1))
            .Limit(limit)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(count / (decimal)limit);

        return new PagedList<ActivityLogEntity>(currentPage, totalPages, items);
    }
}
