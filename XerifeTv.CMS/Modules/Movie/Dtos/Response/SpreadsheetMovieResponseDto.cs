using XerifeTv.CMS.Modules.Abstractions.Exceptions;
using XerifeTv.CMS.Modules.Abstractions.ValueObjects;
using XerifeTv.CMS.Shared.Helpers;

namespace XerifeTv.CMS.Modules.Movie.Dtos.Response;

public sealed class SpreadsheetMovieResponseDto
{
    public string ImdbId { get; private set; } = string.Empty;
    public int ParentalRating { get; private set; }
    public Video? Video { get; private set; }
    public string? AlternativeVideoUrl { get; private set; }
    public string? MediaDeliveryProfileName { get; private set; }
    public string? MediaRoute { get; private set; }
    public string? MediaDeliveryProfileId { get; set; }
    public string? TrailerVideoYoutubeId { get; set; }

    public static SpreadsheetMovieResponseDto FromCollunsStr(string[] cols)
    {
        if (cols.Length >= 8 && (cols[2].Contains("PROFILE") || cols[2].Contains("MEDIA") || !string.IsNullOrWhiteSpace(cols[2]) || !string.IsNullOrWhiteSpace(cols[3])))
        {
            string? imdbId = cols[0]?.Trim();
            string? parentalRating = cols[1]?.Trim();
            string? mediaDeliveryProfileName = cols[2]?.Trim();
            string? mediaPath = cols[3]?.Trim();
            string? videoUrl = cols[4]?.Trim();
            string? videoStreamFormat = cols[5]?.Trim();
            string? videoSubtitleUrl = cols[6]?.Trim();
            string? trailerVideoYoutubeId = cols[7]?.Trim();

            if (!string.IsNullOrWhiteSpace(imdbId))
            {
                if (imdbId.Contains('.')) imdbId = imdbId.Split('.')[0];
                if (!imdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase) && long.TryParse(imdbId, out _))
                    imdbId = $"tt{imdbId}";
            }

            List<string?> requiredValues = [imdbId, parentalRating];

            if (requiredValues.Any(string.IsNullOrWhiteSpace))
                throw new SpreadsheetInvalidException($"[{imdbId}] algum campo obrigatorio esta vazio");

            int parentalRatingResult = 0;
            if (!string.IsNullOrWhiteSpace(parentalRating))
            {
                if (parentalRating.Equals("L", StringComparison.OrdinalIgnoreCase))
                    parentalRatingResult = 0;
                else
                    int.TryParse(parentalRating, out parentalRatingResult);
            }

            if (string.IsNullOrWhiteSpace(videoStreamFormat))
                videoStreamFormat = "mp4";
            else
                videoStreamFormat = videoStreamFormat.ToLower();

            var hasMediaDeliveryProfile =
                !string.IsNullOrWhiteSpace(mediaDeliveryProfileName) &&
                !string.IsNullOrWhiteSpace(mediaPath);

            var hasFixedVideo =
                !string.IsNullOrWhiteSpace(videoUrl) &&
                !string.IsNullOrWhiteSpace(videoStreamFormat);

            if (!hasMediaDeliveryProfile && !hasFixedVideo)
                throw new SpreadsheetInvalidException($"[{imdbId}] obrigatorio informar Media Delivery Profile/Media Path ou URL Video Fixed/Stream Format");

            return new SpreadsheetMovieResponseDto
            {
                ImdbId = imdbId!,
                ParentalRating = parentalRatingResult,
                Video = new Video(videoUrl ?? string.Empty, 0, videoStreamFormat, videoSubtitleUrl),
                MediaDeliveryProfileName = mediaDeliveryProfileName,
                MediaRoute = mediaPath,
                TrailerVideoYoutubeId = trailerVideoYoutubeId
            };
        }
        else
        {
            string? imdbId = cols.Length > 0 ? cols[0]?.Trim() : null;
            string? parentalRating = cols.Length > 1 ? cols[1]?.Trim() : null;
            string? videoUrl = cols.Length > 2 ? cols[2]?.Trim() : null;
            string? videoStreamFormat = cols.Length > 3 ? cols[3]?.Trim() : null;
            string? alternativeVideoUrl = cols.Length > 4 ? cols[4]?.Trim() : null;

            if (!string.IsNullOrWhiteSpace(imdbId))
            {
                if (imdbId.Contains('.')) imdbId = imdbId.Split('.')[0];
                if (!imdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase) && long.TryParse(imdbId, out _))
                    imdbId = $"tt{imdbId}";
            }

            List<string?> requiredValues = [
                imdbId,
                parentalRating,
                videoUrl
            ];

            if (requiredValues.Any(string.IsNullOrWhiteSpace))
                throw new SpreadsheetInvalidException($"[{imdbId ?? "Linha inválida"}] algum campo obrigatório está vazio (IMDB ID, Classificação ou Link)");

            int parentalRatingResult = 0;
            if (!string.IsNullOrWhiteSpace(parentalRating))
            {
                if (parentalRating.Equals("L", StringComparison.OrdinalIgnoreCase))
                    parentalRatingResult = 0;
                else
                    int.TryParse(parentalRating, out parentalRatingResult);
            }

            if (string.IsNullOrWhiteSpace(videoStreamFormat))
                videoStreamFormat = "mp4";
            else
                videoStreamFormat = videoStreamFormat.ToLower();

            return new SpreadsheetMovieResponseDto
            {
                ImdbId = imdbId!,
                ParentalRating = parentalRatingResult,
                Video = new Video(videoUrl!, 0, videoStreamFormat, null),
                AlternativeVideoUrl = string.IsNullOrWhiteSpace(alternativeVideoUrl) ? null : alternativeVideoUrl
            };
        }
    }
}
