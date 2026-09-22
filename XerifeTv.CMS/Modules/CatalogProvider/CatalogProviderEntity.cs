using XerifeTv.CMS.Modules.Abstractions.Entities;

namespace XerifeTv.CMS.Modules.CatalogProvider;

public class CatalogProviderEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base da URL do catálogo, incluindo qualquer prefixo antes de "/stream/".
    /// O link é montado como {BaseUrl}/stream/movie/{imdb}.json (filme) ou
    /// {BaseUrl}/stream/series/{imdb}:{season}:{episode}.json (série).
    /// Ex froststream: https://froststream.cloutteam.com
    /// Ex fenixflix:   https://fenixflix.fenixhub.online/qualities=4k,1080p,720p,sd,cam|audio=dublado,legendado
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Provedor padrão (pré-selecionado nas telas de cadastro).</summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>Ordem de fallback (menor = tentado primeiro). Ex: 1, 2, 3.</summary>
    public int Order { get; set; } = 0;

    public bool IsDisabled { get; set; } = false;
}
