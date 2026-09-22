using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.CatalogProvider.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.CatalogProvider;

public class CatalogProviderRepository : BaseRepository<CatalogProviderEntity>, ICatalogProviderRepository
{
    public CatalogProviderRepository(IOptions<DBSettings> dbSettings) : base(ECollection.CATALOG_PROVIDERS, dbSettings) { }

    public async Task<IEnumerable<CatalogProviderEntity>> GetAsync(bool isIncludeDisabled = false)
    {
        return await _collection.Find(r => (isIncludeDisabled) || (!isIncludeDisabled && !r.IsDisabled))
            .ToListAsync();
    }

    public async Task<CatalogProviderEntity?> GetByNameAsync(string name, bool isIncludeDisabled = false)
    {
        return await _collection.Find(r
            => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
               ((isIncludeDisabled) || (!isIncludeDisabled && !r.IsDisabled)))
            .FirstOrDefaultAsync();
    }
}
