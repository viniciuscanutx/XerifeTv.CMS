using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.LinkTemplate.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.LinkTemplate;

public class LinkTemplateRepository : BaseRepository<LinkTemplateEntity>, ILinkTemplateRepository
{
    public LinkTemplateRepository(IOptions<DBSettings> dbSettings) : base(ECollection.LINK_TEMPLATES, dbSettings) { }

    public async Task<IEnumerable<LinkTemplateEntity>> GetAsync(bool isIncludeDisabled = false)
    {
        return await _collection.Find(r => (isIncludeDisabled) || (!isIncludeDisabled && !r.IsDisabled))
            .ToListAsync();
    }

    public async Task<LinkTemplateEntity?> GetByNameAsync(string name, bool isIncludeDisabled = false)
    {
        return await _collection.Find(r
            => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
               ((isIncludeDisabled) || (!isIncludeDisabled && !r.IsDisabled)))
            .FirstOrDefaultAsync();
    }
}
