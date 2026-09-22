namespace XerifeTv.CMS.Modules.Media.Delivery.Dtos.Request;

public sealed class ResolveCatalogFromPayloadRequestDto
{
    // URL original do catálogo (.json) - usada só como chave de cache, o servidor
    // não a busca; o payload já vem baixado pelo navegador do admin.
    public string UrlFixed { get; set; } = string.Empty;
    public string StreamFormat { get; set; } = "mp4";
    public bool FollowRedirect { get; set; } = false;

    // JSON cru do catálogo que o navegador buscou direto do froststream.
    public string Payload { get; set; } = string.Empty;
}
