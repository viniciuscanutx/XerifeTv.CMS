using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.Common;

namespace XerifeTv.CMS.Modules.Activity.Interfaces;

public interface IActivityLogRepository : IBaseRepository<ActivityLogEntity>
{
    Task<PagedList<ActivityLogEntity>> GetByUserAsync(string? userName, int currentPage, int limit);
}
