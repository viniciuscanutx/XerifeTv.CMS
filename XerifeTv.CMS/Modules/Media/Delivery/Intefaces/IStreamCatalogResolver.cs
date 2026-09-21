using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;

namespace XerifeTv.CMS.Modules.Media.Delivery.Intefaces;

public interface IStreamCatalogResolver
{
    bool CanHandle(string url);
    Task<Result<GetResolveUrlResponseDto>> ResolveAsync(string url, string fallbackStreamFormat, CancellationToken cancellationToken = default);
}
