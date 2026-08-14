using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.SiteRole.Dtos.Response;
using XerifeTv.CMS.Modules.SiteRole.Interfaces;
using XerifeTv.CMS.Modules.SiteUser.Dtos.Request;
using XerifeTv.CMS.Modules.SiteUser.Dtos.Response;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;
using XerifeTv.CMS.Modules.User.Interfaces;

namespace XerifeTv.CMS.Modules.SiteUser;

public sealed class SiteUserService(
  IHashPassword _hashPassword,
  ISiteUserRepository _repository,
  ISiteRoleService _siteRoleService) : ISiteUserService
{
    public async Task<Result<IEnumerable<GetSiteUserResponseDto>>> GetAllAsync()
    {
        try
        {
            var response = await _repository.GetAllAsync();
            var result = new List<GetSiteUserResponseDto>();

            foreach (var entity in response)
                result.Add(GetSiteUserResponseDto.FromEntity(entity, await GetRoleAsync(entity.RoleId)));

            return Result<IEnumerable<GetSiteUserResponseDto>>.Success(result);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<IEnumerable<GetSiteUserResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<GetSiteUserResponseDto?>> GetByIdAsync(string id)
    {
        try
        {
            var response = await _repository.GetAsync(id);

            if (response == null)
                return Result<GetSiteUserResponseDto?>.Failure(new Error("404", "Usuario nao encontrado"));

            return Result<GetSiteUserResponseDto?>.Success(
                GetSiteUserResponseDto.FromEntity(response, await GetRoleAsync(response.RoleId)));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSiteUserResponseDto?>.Failure(error);
        }
    }

    public async Task<Result<GetSiteUserResponseDto?>> GetByEmailAsync(string email)
    {
        try
        {
            var response = await _repository.GetByEmailAsync(email);

            if (response == null)
                return Result<GetSiteUserResponseDto?>.Failure(new Error("404", "Usuario nao encontrado"));

            return Result<GetSiteUserResponseDto?>.Success(
                GetSiteUserResponseDto.FromEntity(response, await GetRoleAsync(response.RoleId)));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSiteUserResponseDto?>.Failure(error);
        }
    }

    private async Task<GetSiteRoleResponseDto?> GetRoleAsync(string? roleId)
    {
        if (string.IsNullOrEmpty(roleId))
            return null;

        var roleResponse = await _siteRoleService.GetAsync(roleId);
        return roleResponse.IsSuccess ? roleResponse.Data : null;
    }

    public async Task<Result<bool>> IsPasswordCorrect(string userId, string password)
    {
        try
        {
            var user = await _repository.GetAsync(userId);

            if (user == null)
                return Result<bool>.Failure(new Error("404", "Usuario nao encontrado"));

            return Result<bool>.Success(_hashPassword.Verify(password, user.Password));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<bool>.Failure(error);
        }
    }

    public async Task<Result<GetSiteUserResponseDto>> CreateAsync(CreateSiteUserRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Result<GetSiteUserResponseDto>.Failure(new Error("400", "Nome obrigatorio"));

            if (string.IsNullOrWhiteSpace(dto.Password))
                return Result<GetSiteUserResponseDto>.Failure(new Error("400", "Senha obrigatoria"));

            var existingUser = await _repository.GetByEmailAsync(dto.Email);
            if (existingUser != null)
                return Result<GetSiteUserResponseDto>.Failure(new Error("409", "Ja existe um usuario com este email"));

            var entity = dto.ToEntity();
            entity.Password = _hashPassword.Encrypt(dto.Password);

            await _repository.CreateAsync(entity);

            return Result<GetSiteUserResponseDto>.Success(
                GetSiteUserResponseDto.FromEntity(entity, await GetRoleAsync(entity.RoleId)));
        }
        catch (ArgumentException ex)
        {
            return Result<GetSiteUserResponseDto>.Failure(new Error("400", ex.Message));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSiteUserResponseDto>.Failure(error);
        }
    }

    public async Task<Result<GetSiteUserResponseDto>> UpdateAsync(UpdateSiteUserRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Id))
                return Result<GetSiteUserResponseDto>.Failure(new Error("400", "Usuario invalido"));

            var existingUser = await _repository.GetAsync(dto.Id);
            if (existingUser == null)
                return Result<GetSiteUserResponseDto>.Failure(new Error("404", "Usuario nao encontrado"));

            var duplicatedUser = await _repository.GetByEmailAsync(dto.Email, dto.Id);
            if (duplicatedUser != null)
                return Result<GetSiteUserResponseDto>.Failure(new Error("409", "Ja existe um usuario com este email"));

            existingUser.Name = dto.Name.Trim();
            existingUser.Email = dto.Email.Trim();
            existingUser.RoleId = string.IsNullOrWhiteSpace(dto.RoleId) ? null : dto.RoleId;

            await _repository.UpdateAsync(existingUser);

            return Result<GetSiteUserResponseDto>.Success(
                GetSiteUserResponseDto.FromEntity(existingUser, await GetRoleAsync(existingUser.RoleId)));
        }
        catch (ArgumentException ex)
        {
            return Result<GetSiteUserResponseDto>.Failure(new Error("400", ex.Message));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSiteUserResponseDto>.Failure(error);
        }
    }

    public async Task<Result<bool>> DeleteAsync(string id)
    {
        try
        {
            var existingUser = await _repository.GetAsync(id);
            if (existingUser == null)
                return Result<bool>.Failure(new Error("404", "Usuario nao encontrado"));

            await _repository.DeleteAsync(id);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<bool>.Failure(error);
        }
    }
}
