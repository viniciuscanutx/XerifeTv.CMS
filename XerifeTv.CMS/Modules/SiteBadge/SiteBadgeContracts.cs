using System.ComponentModel.DataAnnotations;
namespace XerifeTv.CMS.Modules.SiteBadge;
public sealed class SaveSiteBadgeRequest
{
    public string? Id { get; set; }
    [Required, StringLength(40)]
    public string Name { get; set; } = string.Empty;
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string Color { get; set; } = "#9b68ff";
}
public sealed class AssignSiteBadgesRequest
{
    [Required] public string UserId { get; set; } = string.Empty;
    public List<string> BadgeIds { get; set; } = [];
}
public record SelectSiteBadgeRequest([StringLength(100)] string? BadgeId);
public record SiteBadgeResponse(string Id, string Name, string Color)
{
    public static SiteBadgeResponse FromEntity(SiteBadgeEntity badge) => new(badge.Id, badge.Name, badge.Color);
}
