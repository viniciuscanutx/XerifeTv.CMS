using XerifeTv.CMS.Modules.Abstractions.Entities;

namespace XerifeTv.CMS.Modules.LinkTemplate;

public class LinkTemplateEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Modelo da URL contendo o placeholder {id}, que sera substituido pelo numero
    /// sequencial do episodio. Ex: http://sventank.com/series/11982237031/luciano/{id}.mp4
    /// </summary>
    public string UrlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Quantidade minima de digitos do {id} (preenche com zeros a esquerda).
    /// 0 = sem preenchimento (usa o numero como esta). Ex: Padding=5 -> 00042.
    /// </summary>
    public int Padding { get; set; } = 0;

    public bool IsDisabled { get; set; } = false;
}
