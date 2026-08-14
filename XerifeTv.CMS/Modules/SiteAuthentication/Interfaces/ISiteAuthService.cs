using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.SiteAuthentication.Dtos.Request;
using XerifeTv.CMS.Modules.SiteAuthentication.Dtos.Response;

namespace XerifeTv.CMS.Modules.SiteAuthentication.Interfaces;

public interface ISiteAuthService
{
    Task<Result<SiteLoginResponseDto>> LoginAsync(SiteLoginRequestDto dto);
    Task<Result<(string? newToken, string? newRefreshToken)>> TryRefreshSessionAsync(string refreshToken);
}
