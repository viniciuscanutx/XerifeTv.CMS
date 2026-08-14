using XerifeTv.CMS.Modules.SiteRole.Dtos.Response;

namespace XerifeTv.CMS.Modules.SiteUser.Dtos.Response;

public class GetSiteUserResponseDto
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? RoleId { get; private set; }
    public string? RoleName { get; private set; }
    public IEnumerable<string> Permissions { get; private set; } = [];

    public static GetSiteUserResponseDto FromEntity(SiteUserEntity entity, GetSiteRoleResponseDto? role = null)
    {
        return new GetSiteUserResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Email = entity.Email,
            RoleId = entity.RoleId,
            RoleName = role?.Name,
            Permissions = role?.Permissions ?? []
        };
    }
}
