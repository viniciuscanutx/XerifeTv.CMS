using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.SiteAuthentication.Dtos.Request;
using XerifeTv.CMS.Modules.SiteAuthentication.Dtos.Response;
using XerifeTv.CMS.Modules.SiteAuthentication.Interfaces;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;

namespace XerifeTv.CMS.Modules.SiteAuthentication.Services;

public class SiteAuthService(
    ISiteUserService _siteUserService,
    ISiteTokenService _siteTokenService) : ISiteAuthService
{
    public async Task<Result<SiteLoginResponseDto>> LoginAsync(SiteLoginRequestDto dto)
    {
        try
        {
            var userResponse = await _siteUserService.GetByEmailAsync(dto.Email);

            if (userResponse.IsFailure)
                return Result<SiteLoginResponseDto>.Failure(new Error("401", "Credenciais invalidas"));

            var user = userResponse.Data!;

            var isPasswordCorrectResponse = await _siteUserService.IsPasswordCorrect(user.Id, dto.Password);

            if (isPasswordCorrectResponse.IsFailure || !isPasswordCorrectResponse.Data)
                return Result<SiteLoginResponseDto>.Failure(new Error("401", "Credenciais invalidas"));

            return Result<SiteLoginResponseDto>.Success(
                new SiteLoginResponseDto(
                    _siteTokenService.GenerateToken(user.Id),
                    _siteTokenService.GenerateRefreshToken(user.Id)));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<SiteLoginResponseDto>.Failure(error);
        }
    }

    public async Task<Result<(string? newToken, string? newRefreshToken)>> TryRefreshSessionAsync(string refreshToken)
    {
        try
        {
            var (isValid, userId) = await _siteTokenService.ValidateTokenAsync(refreshToken);

            if (!isValid)
                return Result<(string?, string?)>.Failure(new Error("401", "Token invalido"));

            var userResponse = await _siteUserService.GetByIdAsync(userId!);

            if (userResponse.IsFailure)
                return Result<(string? newToken, string? newRefreshToken)>.Failure(userResponse.Error);

            return Result<(string?, string?)>.Success((
                _siteTokenService.GenerateToken(userId!),
                _siteTokenService.GenerateRefreshToken(userId!)));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<(string?, string?)>.Failure(error);
        }
    }
}
