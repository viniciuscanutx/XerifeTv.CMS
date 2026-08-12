using Newtonsoft.Json;

namespace XerifeTv.CMS.Modules.Integrations.Imdb.Dtos;

public class SearchMoviesByNameResponseDto
{
    [JsonProperty("results")]
    public List<MovieSearchResultDto> Results { get; set; } = [];
}

public class MovieSearchResultDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("title")]
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
    [JsonProperty("release_date")]
    public string? ReleaseYear
    {
        get => _releaseYear.Split("-").FirstOrDefault();
        set => _releaseYear = value ?? string.Empty;
    }
}
