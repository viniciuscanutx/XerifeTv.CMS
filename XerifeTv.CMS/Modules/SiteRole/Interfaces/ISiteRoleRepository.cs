using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.SiteRole.Interfaces;

public interface ISiteRoleRepository : IBaseRepository<SiteRoleEntity>
{
    Task<IEnumerable<SiteRoleEntity>> GetAllAsync();
    Task<SiteRoleEntity?> GetByNameAsync(string name);
    Task<SiteRoleEntity?> GetByNameAsync(string name, string ignoreId);
}
