using XerifeTv.CMS.Modules.Common;

namespace XerifeTv.CMS.Modules.Media.Delivery.Intefaces;

public interface IRedirectUrlResolver
{
    Task<Result<string>> ResolveFinalUrlAsync(string url, CancellationToken cancellationToken = default);
}
