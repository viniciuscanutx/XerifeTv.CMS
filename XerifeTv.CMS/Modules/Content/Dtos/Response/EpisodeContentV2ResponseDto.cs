using XerifeTv.CMS.Modules.Series;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Modules.Content.Dtos.Response;

public class EpisodeContentV2ResponseDto
{
    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string BannerURL { get; private set; } = string.Empty;
    public string Duration { get; private set; } = string.Empty;
    public long DurationSeconds { get; private set; }
    public string SubtitleURL { get; private set; } = string.Empty;
    public string? VideoResolverURL { get; private set; }
    public string? AlternativeVideoResolverURL { get; private set; }
    public bool HighQuality { get; private set; }

    public static EpisodeContentV2ResponseDto FromEntity(Episode entity, string encryptKey)
    {
        string? videoResolverPath = null;

        if (!string.IsNullOrWhiteSpace(entity.MediaDeliveryProfileId))
        {
            string mdp = CryptographyHelper.Encrypt(entity.MediaDeliveryProfileId, encryptKey);
            string mp = CryptographyHelper.Encrypt(entity.MediaRoute ?? string.Empty, encryptKey);
            videoResolverPath = $"/MediaDeliveryProfiles/ResolveUrlMdp?mdp={Uri.EscapeDataString(mdp)}&mp={Uri.EscapeDataString(mp)}";
        }
        else if (!string.IsNullOrWhiteSpace(entity.Video?.Url))
        {
            string uf = CryptographyHelper.Encrypt(entity.Video.Url, encryptKey);
            string sf = CryptographyHelper.Encrypt(entity.Video?.StreamFormat ?? string.Empty, encryptKey);
            videoResolverPath = $"/MediaDeliveryProfiles/ResolveUrlFx?uf={Uri.EscapeDataString(uf)}&sf={Uri.EscapeDataString(sf)}&fr={entity.Video.FollowRedirect}";
        }

        string? altResolverPath = null;
        if (!string.IsNullOrWhiteSpace(entity.AlternativeVideoUrl))
        {
            string auf = CryptographyHelper.Encrypt(entity.AlternativeVideoUrl, encryptKey);
            string asf = CryptographyHelper.Encrypt(entity.Video?.StreamFormat ?? "hls", encryptKey);
            altResolverPath = $"/MediaDeliveryProfiles/ResolveUrlFx?uf={Uri.EscapeDataString(auf)}&sf={Uri.EscapeDataString(asf)}&fr={entity.Video?.FollowRedirect ?? false}";
        }

        return new()
        {
            Id = entity.Id,
            Title = entity.Title,
            BannerURL = entity.BannerUrl,
            Duration = DateTimeHelper.ConvertSecondsToHHmm(entity.Video?.Duration ?? 0),
            DurationSeconds = entity.Video?.Duration ?? 0,
            SubtitleURL = entity.Video?.Subtitle ?? string.Empty,
            VideoResolverURL = videoResolverPath,
            AlternativeVideoResolverURL = altResolverPath,
            HighQuality = entity.HighQuality
        };
    }
}
