namespace XerifeTv.CMS.Modules.Series.Dtos.Response;

public class GetEpisodesResponseDto
{
    public string SerieId { get; private set; } = string.Empty;
    public string SerieTitle { get; private set; } = string.Empty;
    public string SerieImdbId { get; private set; } = string.Empty;
    public int NumberSeasons { get; private set; }
    public IEnumerable<Episode> Episodes { get; private set; } = [];

    public static GetEpisodesResponseDto FromEntity(SeriesEntity entity)
    {
        return new GetEpisodesResponseDto
        {
            SerieId = entity.Id,
            SerieTitle = entity.Title,
            SerieImdbId = entity.ImdbId,
            NumberSeasons = entity.NumberSeasons,
            Episodes = entity.Episodes
        };
    }

    // CMS (admin): resolver plano pra habilitar o browser-first no preview.
    public void SetUrlResolverPathEpisodes()
    {
        foreach (var episode in Episodes)
        {
            episode.SetUrlResolverPathCms();
        }
    }
}
