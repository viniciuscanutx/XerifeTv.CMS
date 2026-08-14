using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using XerifeTv.CMS.Modules.SiteAuthentication.Interfaces;

namespace XerifeTv.CMS.Modules.SiteAuthentication.Services;

public class SiteTokenService(IConfiguration _configuration) : ISiteTokenService
{
    public string GenerateToken(string userId)
    {
        var key = _configuration["SiteJwt:Key"] ?? string.Empty;
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var issuer = _configuration["SiteJwt:Issuer"];
        var audience = _configuration["SiteJwt:Audience"];

        var signInCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
        var tokenClaims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };

        _ = int.TryParse(_configuration["SiteJwt:ExpirationTimeInMinutes"], out int expireTimeInMinutes);

        var tokenOptions = new JwtSecurityToken(
            issuer,
            audience,
            tokenClaims,
            signingCredentials: signInCredentials,
            expires: DateTime.UtcNow.AddMinutes(expireTimeInMinutes));

        return new JwtSecurityTokenHandler().WriteToken(tokenOptions);
    }

    public string GenerateRefreshToken(string userId)
    {
        var key = _configuration["SiteJwt:Key"] ?? string.Empty;
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var issuer = _configuration["SiteJwt:Issuer"];
        var audience = _configuration["SiteJwt:Audience"];

        var signInCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
        var tokenClaims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };

        _ = int.TryParse(_configuration["SiteJwt:RefreshExpirationTimeInMinutes"], out var expireTimeInMinutes);

        var tokenOptions = new JwtSecurityToken(
            issuer,
            audience,
            tokenClaims,
            signingCredentials: signInCredentials,
            expires: DateTime.UtcNow.AddMinutes(expireTimeInMinutes));

        return new JwtSecurityTokenHandler().WriteToken(tokenOptions);
    }

    public async Task<(bool isValid, string? userId)> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, null);

        var tokenValidationParams = GetTokenValidationParameters(_configuration);
        var validTokenResult = await new JwtSecurityTokenHandler().ValidateTokenAsync(token, tokenValidationParams);

        if (!validTokenResult.IsValid)
            return (false, null);

        var userId = validTokenResult.Claims
          .FirstOrDefault(x => x.Key == ClaimTypes.NameIdentifier).Value as string;

        return (true, userId);
    }

    public static TokenValidationParameters GetTokenValidationParameters(IConfiguration _configuration)
    {
        var tokenKey = Encoding.UTF8.GetBytes(_configuration["SiteJwt:Key"] ?? string.Empty);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["SiteJwt:Issuer"],
            ValidAudience = _configuration["SiteJwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(tokenKey),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }
}
