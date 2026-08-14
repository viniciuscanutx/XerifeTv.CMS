using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.SiteWatchProgress.Dtos.Request;
using XerifeTv.CMS.Modules.SiteWatchProgress.Dtos.Response;

namespace XerifeTv.CMS.Modules.SiteWatchProgress.Interfaces;

public interface ISiteWatchProgressService
{
    Task<Result<IEnumerable<GetWatchProgressResponseDto>>> GetContinueWatchingAsync(string siteUserId, int limit);
    Task<Result<GetWatchProgressResponseDto>> UpsertAsync(string siteUserId, UpsertWatchProgressRequestDto dto);
    Task<Result<bool>> DeleteAsync(string siteUserId, string contentId);
}
