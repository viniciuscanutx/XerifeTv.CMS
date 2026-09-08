using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.SiteUser.Interfaces;

public interface ISiteUserRepository : IBaseRepository<SiteUserEntity>
{
    Task<bool> AssignBadgesAsync(string userId, List<string> badgeIds);
    Task<bool> SelectBadgeAsync(string userId, string? badgeId);
    Task<bool> ChangePasswordAsync(string id, string passwordHash);
    Task<SiteUserEntity?> UpdateProfileAsync(string userId, string name, string? avatarUrl, string? avatarGiphyId = null);
    Task<IEnumerable<SiteUserEntity>> GetAllAsync();
    Task<SiteUserEntity?> GetByEmailAsync(string email);
    Task<SiteUserEntity?> GetByEmailAsync(string email, string ignoreId);
    Task<long> CountByRoleIdAsync(string roleId);
}
