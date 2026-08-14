using XerifeTv.CMS.Modules.Abstractions.Entities;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Modules.SiteUser;

public class SiteUserEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set
        {
            if (!RegexHelper.IsValidEmail(value))
                throw new ArgumentException("Email invalido");

            _email = value;
        }
    }

    public string Password { get; set; } = string.Empty;
    public string? RoleId { get; set; }
}
