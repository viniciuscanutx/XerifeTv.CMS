using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.SiteRole.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SiteRole;

public sealed class SiteRoleRepository(IOptions<DBSettings> options)
    : BaseRepository<SiteRoleEntity>(ECollection.SITE_ROLES, options), ISiteRoleRepository
{
    public async Task<IEnumerable<SiteRoleEntity>> GetAllAsync()
    {
        return await _collection
            .Find(_ => true)
            .SortBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<SiteRoleEntity?> GetByNameAsync(string name)
    {
        return await _collection
            .Find(r => r.Name.ToLower() == name.Trim().ToLower())
            .FirstOrDefaultAsync();
    }

    public async Task<SiteRoleEntity?> GetByNameAsync(string name, string ignoreId)
    {
        return await _collection
            .Find(r => r.Name.ToLower() == name.Trim().ToLower() && r.Id != ignoreId)
            .FirstOrDefaultAsync();
    }
}
