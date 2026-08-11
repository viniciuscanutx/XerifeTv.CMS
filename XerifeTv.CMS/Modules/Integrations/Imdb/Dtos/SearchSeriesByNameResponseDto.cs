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

    private string _releaseYear = string.Empty;
    [JsonProperty("first_air_date")]
    public string? ReleaseYear
    {
        get => _releaseYear.Split("-").FirstOrDefault();
        set => _releaseYear = value ?? string.Empty;
    }
}
