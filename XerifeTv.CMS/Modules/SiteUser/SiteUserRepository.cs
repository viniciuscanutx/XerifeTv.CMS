using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SiteUser;

public sealed class SiteUserRepository(IOptions<DBSettings> options)
  : BaseRepository<SiteUserEntity>(ECollection.SITE_USERS, options), ISiteUserRepository
{
    public async Task<IEnumerable<SiteUserEntity>> GetAllAsync()
      => await _collection
              .Find(_ => true)
              .SortByDescending(r => r.CreateAt)
              .ToListAsync();

    public async Task<SiteUserEntity?> GetByEmailAsync(string email)
      => await _collection
              .Find(r => r.Email.Equals(email))
              .FirstOrDefaultAsync();

    public async Task<SiteUserEntity?> GetByEmailAsync(string email, string ignoreId)
      => await _collection
              .Find(r => r.Email.Equals(email) && r.Id != ignoreId)
              .FirstOrDefaultAsync();

    public async Task<long> CountByRoleIdAsync(string roleId)
      => await _collection.CountDocumentsAsync(r => r.RoleId == roleId);
}
