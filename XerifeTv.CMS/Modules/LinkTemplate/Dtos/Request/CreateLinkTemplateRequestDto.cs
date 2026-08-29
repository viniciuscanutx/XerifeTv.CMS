namespace XerifeTv.CMS.Modules.LinkTemplate.Dtos.Request;

public class CreateLinkTemplateRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string UrlTemplate { get; init; } = string.Empty;
    public int Padding { get; init; } = 0;

    public LinkTemplateEntity ToEntity()
    {
        return new LinkTemplateEntity
        {
            Name = Name.Trim(),
            UrlTemplate = UrlTemplate.Trim(),
            Padding = Padding < 0 ? 0 : Padding
        };
    }
}
