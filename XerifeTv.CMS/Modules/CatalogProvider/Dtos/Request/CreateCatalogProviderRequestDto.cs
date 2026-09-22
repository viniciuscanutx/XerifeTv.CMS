namespace XerifeTv.CMS.Modules.CatalogProvider.Dtos.Request;

public class CreateCatalogProviderRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public bool IsDefault { get; init; } = false;
    public int Order { get; init; } = 0;

    public CatalogProviderEntity ToEntity()
    {
        return new CatalogProviderEntity
        {
            Name = Name.Trim(),
            BaseUrl = BaseUrl.Trim().TrimEnd('/'),
            IsDefault = IsDefault,
            Order = Order
        };
    }
}
