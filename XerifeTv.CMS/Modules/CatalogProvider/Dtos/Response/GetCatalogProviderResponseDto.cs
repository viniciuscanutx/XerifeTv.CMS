namespace XerifeTv.CMS.Modules.CatalogProvider.Dtos.Response;

public class GetCatalogProviderResponseDto
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public string IdType { get; private set; } = "imdb";
    public bool IsDefault { get; private set; } = false;
    public int Order { get; private set; } = 0;
    public bool IsDisabled { get; private set; } = false;

    public static GetCatalogProviderResponseDto FromEntity(CatalogProviderEntity entity)
    {
        return new()
        {
            Id = entity.Id,
            Name = entity.Name,
            BaseUrl = entity.BaseUrl,
            IdType = string.IsNullOrWhiteSpace(entity.IdType) ? "imdb" : entity.IdType,
            IsDefault = entity.IsDefault,
            Order = entity.Order,
            IsDisabled = entity.IsDisabled
        };
    }
}
