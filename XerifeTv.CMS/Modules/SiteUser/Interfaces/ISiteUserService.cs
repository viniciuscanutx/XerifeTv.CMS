using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.SiteUser.Dtos.Request;
using XerifeTv.CMS.Modules.SiteUser.Dtos.Response;

namespace XerifeTv.CMS.Modules.SiteUser.Interfaces;

public interface ISiteUserService
{
    Task<Result<IEnumerable<GetSiteUserResponseDto>>> GetAllAsync();
    Task<Result<GetSiteUserResponseDto?>> GetByIdAsync(string id);
    Task<Result<GetSiteUserResponseDto?>> GetByEmailAsync(string email);
    Task<Result<bool>> IsPasswordCorrect(string userId, string password);
    Task<Result<GetSiteUserResponseDto>> CreateAsync(CreateSiteUserRequestDto dto);
    Task<Result<GetSiteUserResponseDto>> UpdateAsync(UpdateSiteUserRequestDto dto);
    Task<Result<bool>> DeleteAsync(string id);
}
