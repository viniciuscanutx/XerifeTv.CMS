using MongoDB.Bson.Serialization.Attributes;
using XerifeTv.CMS.Modules.Abstractions.Entities;
using XerifeTv.CMS.Modules.Abstractions.ValueObjects;

namespace XerifeTv.CMS.Modules.Movie;

public class MovieEntity : MediaContent
{
    public string ImdbId { get; set; } = string.Empty;
    public string? FranchiseId { get; set; }
    public ICollection<string> Categories { get; set; } = [];
    public float Review { get; set; } = 0;
    public Video? Video { get; set; }
    public string? AlternativeVideoUrl { get; set; }
    public string? MediaDeliveryProfileId { get; set; }
    public string? MediaRoute { get; set; }
    public string? LogoUrl { get; set; }
    public string? TrailerVideoYoutubeId { get; set; }
    [BsonElement("high_quality")]
    public bool HighQuality { get; set; } = false;
    public bool Disabled { get; set; } = false;
}
