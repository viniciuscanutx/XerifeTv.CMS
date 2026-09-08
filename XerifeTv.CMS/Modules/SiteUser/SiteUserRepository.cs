using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SiteUser;

public sealed class SiteUserRepository(IOptions<DBSettings> options)
  : BaseRepository<SiteUserEntity>(ECollection.SITE_USERS, options), ISiteUserRepository
{
    public async Task<bool> AssignBadgesAsync(string userId, List<string> badgeIds)
    {
        var result = await _collection.UpdateOneAsync(x => x.Id == userId,
            Builders<SiteUserEntity>.Update.Set(x => x.BadgeIds, badgeIds).Set(x => x.UpdateAt, DateTime.UtcNow));
        return result.MatchedCount > 0;
    }
    public async Task<bool> SelectBadgeAsync(string userId, string? badgeId)
    {
        var filter = Builders<SiteUserEntity>.Filter.Eq(x => x.Id, userId);
        if (badgeId is not null) filter &= Builders<SiteUserEntity>.Filter.AnyEq(x => x.BadgeIds, badgeId);
        var result = await _collection.UpdateOneAsync(filter,
            Builders<SiteUserEntity>.Update.Set(x => x.SelectedBadgeId, badgeId).Set(x => x.UpdateAt, DateTime.UtcNow));
        return result.MatchedCount > 0;
    }

    public async Task<SiteUserEntity?> UpdateProfileAsync(string userId, string name, string? avatarUrl, string? avatarGiphyId = null)
        => await _collection.FindOneAndUpdateAsync(Builders<SiteUserEntity>.Filter.Eq(x => x.Id, userId),
            Builders<SiteUserEntity>.Update.Set(x => x.Name, name).Set(x => x.AvatarUrl, avatarUrl)
                .Set(x => x.AvatarGiphyId, avatarGiphyId).Set(x => x.UpdateAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<SiteUserEntity> { ReturnDocument = ReturnDocument.After });

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

    public async Task<bool> ChangePasswordAsync(string id, string passwordHash)
    {
        var result = await _collection.UpdateOneAsync(x => x.Id == id,
            Builders<SiteUserEntity>.Update.Set(x => x.Password, passwordHash)
                .Set(x => x.UpdateAt, DateTime.UtcNow));
        return result.MatchedCount > 0;
    }
}
