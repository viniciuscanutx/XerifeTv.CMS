namespace XerifeTv.CMS.Modules.SiteRole.Dtos.Response;

public class GetSiteRoleResponseDto
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public IEnumerable<string> Permissions { get; private set; } = [];

    public static GetSiteRoleResponseDto FromEntity(SiteRoleEntity entity)
    {
        return new GetSiteRoleResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Permissions = entity.Permissions
        };
    }
}
