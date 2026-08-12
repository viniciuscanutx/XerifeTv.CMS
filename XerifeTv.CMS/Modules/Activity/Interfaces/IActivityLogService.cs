using XerifeTv.CMS.Modules.Activity.Dtos.Response;
using XerifeTv.CMS.Modules.Common;

namespace XerifeTv.CMS.Modules.Activity.Interfaces;

public interface IActivityLogService
{
    Task LogAsync(string userName, string category, string action, string description);
    Task<Result<PagedList<GetActivityLogResponseDto>>> GetAsync(int currentPage, int limit);
}
