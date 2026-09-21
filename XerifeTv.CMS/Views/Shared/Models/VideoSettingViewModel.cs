using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;

namespace XerifeTv.CMS.Views.Shared.Models;

public record VideoSettingViewModel
{
    public string IdPrefix { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }
    public string? AlternativeVideoUrl { get; set; }
    public string? VideoStreamFormat { get; set; }
    public string? VideoSubtitle { get; set; }
    public string? MediaDeliveryProfileId { get; set; }
    public string? MediaRoute { get; set; }
    public bool FollowRedirect { get; set; } = false;
    public string? CatalogContentType { get; set; }
    public string? CatalogImdbId { get; set; }
    public int? CatalogSeason { get; set; }
    public int? CatalogEpisode { get; set; }

    public IEnumerable<GetMediaDeliveryProfileResponseDto> MediaDeliveryProfiles { get; set; } = [];
    public IEnumerable<string> StreamFormats { get; set; } = [];
    public bool IsShowVideoSubtitleInput { get; set; } = true;
    public bool IsShowAlternativeVideoUrlInput { get; set; } = true;
}
