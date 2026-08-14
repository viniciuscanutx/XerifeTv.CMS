namespace XerifeTv.CMS.Modules.SiteRole.Dtos.Request;

public class UpdateSiteRoleRequestDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<string> Permissions { get; init; } = [];
}
