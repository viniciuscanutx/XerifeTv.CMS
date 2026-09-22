using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.CatalogProvider.Interfaces;

public interface ICatalogProviderRepository : IBaseRepository<CatalogProviderEntity>
{
    Task<IEnumerable<CatalogProviderEntity>> GetAsync(bool isIncludeDisabled = false);
    Task<CatalogProviderEntity?> GetByNameAsync(string name, bool isIncludeDisabled = false);
}
