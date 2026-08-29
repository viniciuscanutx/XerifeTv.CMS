using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.LinkTemplate.Dtos.Request;
using XerifeTv.CMS.Modules.LinkTemplate.Dtos.Response;

namespace XerifeTv.CMS.Modules.LinkTemplate.Interfaces;

public interface ILinkTemplateService
{
    Task<Result<IEnumerable<GetLinkTemplateResponseDto>>> GetAllAsync(bool isIncludeDisabled = false);
    Task<Result<GetLinkTemplateResponseDto?>> GetAsync(string id);
    Task<Result<string>> CreateAsync(CreateLinkTemplateRequestDto dto);
    Task<Result<string>> UpdateAsync(UpdateLinkTemplateRequestDto dto);
    Task<Result<bool>> DeleteAsync(string id);
}
