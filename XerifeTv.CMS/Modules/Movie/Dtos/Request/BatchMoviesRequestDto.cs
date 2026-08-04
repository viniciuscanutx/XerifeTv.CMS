namespace XerifeTv.CMS.Modules.Movie.Dtos.Request;

public class BatchMoviesRequestDto
{
    public int DefaultParentalRating { get; set; } = 18;
    public string VideoStreamFormat { get; set; } = "mp4";
    public bool HighQuality { get; set; } = false;
    public bool IsBackgroundJob { get; set; } = false;
    public string ImdbIdsText { get; set; } = string.Empty;
    public string? ParentalRatingsText { get; set; }
    public string VideoUrlsText { get; set; } = string.Empty;
    public string? AlternativeVideoUrlsText { get; set; }
}
