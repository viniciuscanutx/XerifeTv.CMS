using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.LinkTemplate.Interfaces;

public interface ILinkTemplateRepository : IBaseRepository<LinkTemplateEntity>
{
    Task<IEnumerable<LinkTemplateEntity>> GetAsync(bool isIncludeDisabled = false);
    Task<LinkTemplateEntity?> GetByNameAsync(string name, bool isIncludeDisabled = false);
}
