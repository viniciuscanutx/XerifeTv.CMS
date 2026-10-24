using XerifeTv.CMS.Modules.Abstractions.Entities;

namespace XerifeTv.CMS.Modules.SiteProfile;

// Foto ou banner enviados pelo usuário do site; o próprio arquivo fica no documento (até 5 MB)
public class SiteProfileImageEntity : BaseEntity
{
    public string SiteUserId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
}
