namespace XerifeTv.CMS.Modules.SiteRole.Dtos.Request;

public class CreateSiteRoleRequestDto
{
    public string Name { get; init; } = string.Empty;
    public List<string> Permissions { get; init; } = [];

    public SiteRoleEntity ToEntity()
    {
        return new SiteRoleEntity
        {
            Name = Name.Trim(),
            Permissions = Permissions.Where(SitePermissions.All.ContainsKey).ToList()
        };
    }
}
