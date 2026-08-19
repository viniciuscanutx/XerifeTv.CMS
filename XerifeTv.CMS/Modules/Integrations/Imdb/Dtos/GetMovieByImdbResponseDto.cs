using Newtonsoft.Json;

namespace XerifeTv.CMS.Modules.Integrations.Imdb.Dtos;

public class GetMovieByImdbResponseDto
{
    [JsonProperty("imdb_id")]
    public string? ImdbId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public List<GenreDto> Genres { get; set; } = [];

    [JsonProperty("vote_average")]
    public float VoteAverage { get; set; }

    private string _posterUrl = string.Empty;
    [JsonProperty("poster_path")]
    public string PosterUrl
    {
        get => _posterUrl;
        set => _posterUrl =
            $"https://images.plex.tv/photo?size=medium-360&scale=1&url=https://image.tmdb.org/t/p/original{value}";
    }

    private string _bannerUrl = string.Empty;
    [JsonProperty("backdrop_path")]
    public string BannerUrl
    {
        get => _bannerUrl;
        set => _bannerUrl = $"https://image.tmdb.org/t/p/original{value}";
    }

    private string _releaseYear = string.Empty;
    [JsonProperty("release_date")]
    public string? ReleaseYear
    {
        get => _releaseYear.Split("-").FirstOrDefault();
        set => _releaseYear = value ?? string.Empty;
    }

    [JsonProperty("runtime")]
    public int DurationInMinutes { get; set; }

    public long DurationInSeconds => DurationInMinutes * 60L;

    [JsonProperty("images")]
    public ImagesDto? Images { get; set; }

    /// <summary>
    /// Logo (wordmark) com o titulo do filme escrito.
    /// Logos sem idioma sao "textless" (so o simbolo), por isso ficam por ultimo.
    /// </summary>
    public string? LogoUrl
    {
        get
        {
            var filePath = Images?.Logos
                .OrderByDescending(GetLogoScore)
                .Select(logo => logo.FilePath)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(filePath)
                ? null
                : $"https://image.tmdb.org/t/p/original{filePath}";
        }
    }

    private static float GetLogoScore(LogoDto logo)
    {
        var score = logo.Language switch
        {
            "pt" => 40f,
            "en" => 30f,
            _ => string.IsNullOrEmpty(logo.Language) ? 10f : 0f
        };

        if (logo.AspectRatio >= 1.6f) score += 20f;

        return score + logo.VoteAverage;
    }

    public record GenreDto(int Id, string Name);

    public class ImagesDto
    {
        [JsonProperty("logos")]
        public List<LogoDto> Logos { get; set; } = [];
    }

    public class LogoDto
    {
        [JsonProperty("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [JsonProperty("aspect_ratio")]
        public float AspectRatio { get; set; }

        [JsonProperty("iso_639_1")]
        public string? Language { get; set; }

        [JsonProperty("vote_average")]
        public float VoteAverage { get; set; }
    }
}