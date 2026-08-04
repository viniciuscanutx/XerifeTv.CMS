namespace XerifeTv.CMS.Modules.Series.Dtos.Request;

public class BatchEpisodeLinksRequestDto
{
    public string SerieId { get; set; } = string.Empty;
    public int Season { get; set; } = 1;
    public int StartEpisodeNumber { get; set; } = 1;
    public string VideoStreamFormat { get; set; } = "hls";
    public bool HighQuality { get; set; } = false;
    public bool OnlyExistingEpisodes { get; set; } = true;
    public string VideoUrlsText { get; set; } = string.Empty;
    public string? AlternativeVideoUrlsText { get; set; }
}
