namespace XerifeTv.CMS.Modules.SiteWatchProgress.Dtos.Request;

public record UpsertWatchProgressRequestDto(
    string ContentId,
    string Type,
    string Title,
    string Poster,
    string? Backdrop,
    double CurrentTime,
    double Duration);
