namespace XerifeTv.CMS.Modules.SiteUser.Dtos.Request;

public class CreateSiteUserRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? RoleId { get; init; }

    public SiteUserEntity ToEntity()
    {
        return new SiteUserEntity
        {
            Name = Name.Trim(),
            Email = Email.Trim(),
            RoleId = string.IsNullOrWhiteSpace(RoleId) ? null : RoleId
        };
    }
}
