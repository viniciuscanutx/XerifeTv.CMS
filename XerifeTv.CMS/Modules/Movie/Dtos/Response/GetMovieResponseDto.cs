using XerifeTv.CMS.Modules.Abstractions.ValueObjects;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Modules.Movie.Dtos.Response;

public sealed class GetMovieResponseDto
{
    public string Id { get; private set; } = string.Empty;
    public string ImdbId { get; private set; } = string.Empty;
    public string? FranchiseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Synopsis { get; private set; } = string.Empty;
    public string Categories { get; private set; } = string.Empty;
    public string PosterUrl { get; private set; } = string.Empty;
    public string BannerUrl { get; private set; } = string.Empty;
    public int ReleaseYear { get; private set; }
    public int ParentalRating { get; private set; }
    public float Review { get; private set; }
    public DateTime RegistrationDate { get; private set; }
    public Video? Video { get; private set; }
    public string? AlternativeVideoUrl { get; private set; }
    public string? MediaDeliveryProfileId { get; private set; }
    public string? MediaRoute { get; private set; }
    public string? TrailerVideoYoutubeId { get; private set; }
    public bool HighQuality { get; private set; } = false;
    public string DurationHHmm => DateTimeHelper.ConvertSecondsToHHmm(Video?.Duration ?? 0);
    public bool Disabled { get; private set; } = false;

    public string? UrlResolverPath
        => !string.IsNullOrWhiteSpace(MediaDeliveryProfileId)
            ? $"/MediaDeliveryProfiles/ResolveUrl?mediaDeliveryProfileId={MediaDeliveryProfileId}&mediaPath={Uri.EscapeDataString(MediaRoute ?? "")}&isCached=false"
            : $"/MediaDeliveryProfiles/ResolveUrlFixed?urlFixed={Uri.EscapeDataString(Video?.Url ?? "")}&streamFormat={Video?.StreamFormat}&followRedirect={Video?.FollowRedirect ?? false}&isCached=false";

    public string? AlternativeUrlResolverPath
        => !string.IsNullOrWhiteSpace(AlternativeVideoUrl)
            ? $"/MediaDeliveryProfiles/ResolveUrlFixed?urlFixed={Uri.EscapeDataString(AlternativeVideoUrl)}&streamFormat={Video?.StreamFormat ?? "hls"}&followRedirect={Video?.FollowRedirect ?? false}&isCached=false"
            : null;

    public static GetMovieResponseDto FromEntity(MovieEntity entity)
    {
        return new GetMovieResponseDto
        {
            Id = entity.Id,
            ImdbId = entity.ImdbId,
            FranchiseId = entity.FranchiseId,
            Title = entity.Title,
            Synopsis = entity.Synopsis,
            Categories = string.Join(", ", entity.Categories),
            PosterUrl = entity.PosterUrl,
            BannerUrl = entity.BannerUrl,
            ReleaseYear = entity.ReleaseYear,
            ParentalRating = entity.ParentalRating,
            Review = entity.Review,
            RegistrationDate = entity.CreateAt,
            Video = entity.Video,
            AlternativeVideoUrl = entity.AlternativeVideoUrl,
            Disabled = entity.Disabled,
            MediaRoute = entity.MediaRoute,
            MediaDeliveryProfileId = entity.MediaDeliveryProfileId,
            TrailerVideoYoutubeId = entity.TrailerVideoYoutubeId,
            HighQuality = entity.HighQuality
        };
    }
}