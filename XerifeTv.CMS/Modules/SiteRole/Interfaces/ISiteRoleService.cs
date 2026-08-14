using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.SiteRole.Dtos.Request;
using XerifeTv.CMS.Modules.SiteRole.Dtos.Response;

namespace XerifeTv.CMS.Modules.SiteRole.Interfaces;

public interface ISiteRoleService
{
    Task<Result<IEnumerable<GetSiteRoleResponseDto>>> GetAllAsync();
    Task<Result<GetSiteRoleResponseDto?>> GetAsync(string id);
    Task<Result<GetSiteRoleResponseDto>> CreateAsync(CreateSiteRoleRequestDto dto);
    Task<Result<GetSiteRoleResponseDto>> UpdateAsync(UpdateSiteRoleRequestDto dto);
    Task<Result<bool>> DeleteAsync(string id);
}
