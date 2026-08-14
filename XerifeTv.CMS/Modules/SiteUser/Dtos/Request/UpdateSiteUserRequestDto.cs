namespace XerifeTv.CMS.Modules.SiteUser.Dtos.Request;

public class UpdateSiteUserRequestDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? RoleId { get; init; }
}
