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

    private string _releaseYear = string.Empty;
    [JsonProperty("release_date")]
    public string? ReleaseYear
    {
        get => _releaseYear.Split("-").FirstOrDefault();
        set => _releaseYear = value ?? string.Empty;
    }
}
