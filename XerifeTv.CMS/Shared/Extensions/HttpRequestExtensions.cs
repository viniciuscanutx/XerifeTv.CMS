namespace XerifeTv.CMS.Shared.Extensions;

public static class HttpRequestExtensions
{
    // Origem pública do CMS atrás do proxy do Render (X-Forwarded-*), igual ao MediaDeliveryUrlResolver
    public static string GetPublicBaseUrl(this HttpRequest request)
    {
        var scheme = request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && proto.Count > 0
            ? proto[0]!
            : request.Scheme;

        var host = request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost) && forwardedHost.Count > 0
            ? forwardedHost[0]!
            : request.Host.Value;

        return $"{scheme}://{host}";
    }
}
