using XerifeTv.CMS.Modules.Abstractions.Entities;

namespace XerifeTv.CMS.Modules.SiteRole;

public class SiteRoleEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
}
