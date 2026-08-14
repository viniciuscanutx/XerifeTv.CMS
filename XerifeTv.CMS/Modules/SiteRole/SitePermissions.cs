namespace XerifeTv.CMS.Modules.SiteRole;

public static class SitePermissions
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["home.view"] = "Início",
        ["movies.view"] = "Filmes",
        ["series.view"] = "Séries",
        ["channels.view"] = "Canais ao vivo",
        ["watch"] = "Assistir (filme/episódio/canal)",
        ["search"] = "Busca"
    };
}
