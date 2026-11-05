namespace XerifeTv.CMS.Modules.Media.Delivery.Dtos.Request;

public sealed class ResolveUrlFxFromPayloadRequestDto
{
    // Mesmos "uf" e "sf" cifrados do link ResolveUrlFx
    public string Uf { get; set; } = string.Empty;
    public string Sf { get; set; } = string.Empty;

    // Catálogo que o navegador buscou (cadastrado ou o mesmo /stream/... em outro provedor)
    public string CatalogUrl { get; set; } = string.Empty;

    // JSON cru do catálogo
    public string Payload { get; set; } = string.Empty;
}
