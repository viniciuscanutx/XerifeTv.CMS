using XerifeTv.CMS.Modules.Abstractions.Entities;
namespace XerifeTv.CMS.Modules.SiteBadge;
public sealed class SiteBadgeEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#9b68ff";
}
