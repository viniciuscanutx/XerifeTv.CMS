using Newtonsoft.Json;

namespace XerifeTv.CMS.Modules.Integrations.Imdb.Dtos;

public class SearchSeriesByNameResponseDto
{
    [JsonProperty("results")]
    public List<SeriesSearchResultDto> Results { get; set; } = [];
}

public class SeriesSearchResultDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Title { get; set; } = string.Empty;

    private string _posterUrl = string.Empty;
    [JsonProperty("poster_path")]
    public string PosterUrl
    {
        get => _posterUrl;
        set => _posterUrl = string.IsNullOrEmpty(value)
            ? string.Empty
            : $"https://images.plex.tv/photo?size=medium-360&scale=1&url=https://image.tmdb.org/t/p/original{value}";
    }

    private string _releaseYear = string.Empty;
    [JsonProperty("first_air_date")]
    public string? ReleaseYear
    {
        get => _releaseYear.Split("-").FirstOrDefault();
        set => _releaseYear = value ?? string.Empty;
    }
}
