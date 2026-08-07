using XerifeTv.CMS.Modules.Common.Dtos;

namespace XerifeTv.CMS.Modules.Dashboard.Dtos.Response;

public record class GetDashboardDataRequestDto(
    long NumberOfMovies,
    long NumberOfSeries,
    long NumberOfChannels,
    IReadOnlyCollection<DashboardRecentItemDto>? RecentItems = null,
    IReadOnlyCollection<CategoryCountDto>? TopCategories = null,
    IReadOnlyCollection<DashboardRecentJobDto>? RecentJobs = null);
