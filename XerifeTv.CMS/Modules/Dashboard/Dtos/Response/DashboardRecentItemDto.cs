namespace XerifeTv.CMS.Modules.Dashboard.Dtos.Response;

public record class DashboardRecentItemDto(
    string Id,
    string Title,
    string PosterUrl,
    int ReleaseYear,
    float Review,
    DateTime CreateAt,
    string Type);
