namespace XerifeTv.CMS.Modules.Dashboard.Dtos.Response;

public record class DashboardRecentJobDto(
    string Id,
    string JobName,
    string Status,
    int TotalRecordsToProcess,
    int TotalProcessedRecords,
    DateTime CreateAt);
