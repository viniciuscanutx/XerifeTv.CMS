using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.SiteUser.Interfaces;

public interface ISiteUserRepository : IBaseRepository<SiteUserEntity>
{
    Task<IEnumerable<SiteUserEntity>> GetAllAsync();
    Task<SiteUserEntity?> GetByEmailAsync(string email);
    Task<SiteUserEntity?> GetByEmailAsync(string email, string ignoreId);
    Task<long> CountByRoleIdAsync(string roleId);
}
