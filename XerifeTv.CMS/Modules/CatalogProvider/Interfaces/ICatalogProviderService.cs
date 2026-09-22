using XerifeTv.CMS.Modules.CatalogProvider.Dtos.Request;
using XerifeTv.CMS.Modules.CatalogProvider.Dtos.Response;
using XerifeTv.CMS.Modules.Common;

namespace XerifeTv.CMS.Modules.CatalogProvider.Interfaces;

public interface ICatalogProviderService
{
    Task<Result<IEnumerable<GetCatalogProviderResponseDto>>> GetAllAsync(bool isIncludeDisabled = false);
    Task<Result<GetCatalogProviderResponseDto?>> GetAsync(string id);
    Task<Result<string>> CreateAsync(CreateCatalogProviderRequestDto dto);
    Task<Result<string>> UpdateAsync(UpdateCatalogProviderRequestDto dto);
    Task<Result<bool>> DeleteAsync(string id);
}
