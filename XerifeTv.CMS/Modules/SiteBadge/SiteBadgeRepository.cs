using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Shared.Database.MongoDB;
namespace XerifeTv.CMS.Modules.SiteBadge;
public interface ISiteBadgeRepository
{
    Task<List<SiteBadgeEntity>> GetAllAsync();
    Task<List<SiteBadgeEntity>> GetByIdsAsync(IEnumerable<string> ids);
    Task<SiteBadgeEntity?> GetAsync(string id);
    Task<string> CreateAsync(SiteBadgeEntity entity);
    Task UpdateAsync(SiteBadgeEntity entity);
    Task DeleteAsync(string id);
}
public sealed class SiteBadgeRepository(IOptions<DBSettings> options)
    : BaseRepository<SiteBadgeEntity>(ECollection.SITE_BADGES, options), ISiteBadgeRepository
{
    public Task<List<SiteBadgeEntity>> GetAllAsync() => _collection.Find(_ => true).SortBy(x => x.Name).ToListAsync();
    public Task<List<SiteBadgeEntity>> GetByIdsAsync(IEnumerable<string> ids)
        => _collection.Find(Builders<SiteBadgeEntity>.Filter.In(x => x.Id, ids)).SortBy(x => x.Name).ToListAsync();
}
