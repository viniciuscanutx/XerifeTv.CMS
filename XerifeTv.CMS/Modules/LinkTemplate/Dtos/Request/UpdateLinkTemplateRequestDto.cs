namespace XerifeTv.CMS.Modules.LinkTemplate.Dtos.Request;

public class UpdateLinkTemplateRequestDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string UrlTemplate { get; init; } = string.Empty;
    public int Padding { get; init; } = 0;
    public bool IsDisabled { get; init; } = false;

    public LinkTemplateEntity ToEntity()
    {
        return new LinkTemplateEntity
        {
            Id = Id,
            Name = Name.Trim(),
            UrlTemplate = UrlTemplate.Trim(),
            Padding = Padding < 0 ? 0 : Padding,
            IsDisabled = IsDisabled
        };
    }
}
