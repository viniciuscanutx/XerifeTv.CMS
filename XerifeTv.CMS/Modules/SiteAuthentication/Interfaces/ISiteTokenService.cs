namespace XerifeTv.CMS.Modules.SiteAuthentication.Interfaces;

public interface ISiteTokenService
{
    string GenerateToken(string userId);
    string GenerateRefreshToken(string userId);

    Task<(bool isValid, string? userId)> ValidateTokenAsync(string token);
}
