namespace XerifeTv.CMS.Modules.LinkTemplate.Dtos.Response;

public class GetLinkTemplateResponseDto
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string UrlTemplate { get; private set; } = string.Empty;
    public int Padding { get; private set; } = 0;
    public bool IsDisabled { get; private set; } = false;

    public static GetLinkTemplateResponseDto FromEntity(LinkTemplateEntity entity)
    {
        return new()
        {
            Id = entity.Id,
            Name = entity.Name,
            UrlTemplate = entity.UrlTemplate,
            Padding = entity.Padding,
            IsDisabled = entity.IsDisabled
        };
    }
}
