using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.SiteRole.Dtos.Request;
using XerifeTv.CMS.Modules.SiteRole.Dtos.Response;
using XerifeTv.CMS.Modules.SiteRole.Interfaces;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;

namespace XerifeTv.CMS.Modules.SiteRole;

public class SiteRoleService(
    ISiteRoleRepository _repository,
    ISiteUserRepository _siteUserRepository) : ISiteRoleService
{
    public async Task<Result<IEnumerable<GetSiteRoleResponseDto>>> GetAllAsync()
    {
        try
        {
            var response = await _repository.GetAllAsync();

            return Result<IEnumerable<GetSiteRoleResponseDto>>.Success(
                response.Select(GetSiteRoleResponseDto.FromEntity));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<IEnumerable<GetSiteRoleResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<GetSiteRoleResponseDto?>> GetAsync(string id)
    {
        try
        {
            var response = await _repository.GetAsync(id);

            if (response == null)
                return Result<GetSiteRoleResponseDto?>.Failure(
                    new Error("404", "Role nao encontrada"));

            return Result<GetSiteRoleResponseDto?>.Success(
                GetSiteRoleResponseDto.FromEntity(response));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSiteRoleResponseDto?>.Failure(error);
        }
    }

    public async Task<Result<GetSiteRoleResponseDto>> CreateAsync(CreateSiteRoleRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Result<GetSiteRoleResponseDto>.Failure(
                    new Error("400", "Nome da role obrigatorio"));

            var existingRole = await _repository.GetByNameAsync(dto.Name);
            if (existingRole != null)
                return Result<GetSiteRoleResponseDto>.Failure(
                    new Error("409", "Ja existe uma role com este nome"));

            var entity = dto.ToEntity();
            await _repository.CreateAsync(entity);

            return Result<GetSiteRoleResponseDto>.Success(
                GetSiteRoleResponseDto.FromEntity(entity));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSiteRoleResponseDto>.Failure(error);
        }
    }

    public async Task<Result<GetSiteRoleResponseDto>> UpdateAsync(UpdateSiteRoleRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Id))
                return Result<GetSiteRoleResponseDto>.Failure(
                    new Error("400", "Role invalida"));

            if (string.IsNullOrWhiteSpace(dto.Name))
                return Result<GetSiteRoleResponseDto>.Failure(
                    new Error("400", "Nome da role obrigatorio"));

            var existingRole = await _repository.GetAsync(dto.Id);
            if (existingRole == null)
                return Result<GetSiteRoleResponseDto>.Failure(
                    new Error("404", "Role nao encontrada"));

            var duplicatedRole = await _repository.GetByNameAsync(dto.Name, dto.Id);
            if (duplicatedRole != null)
                return Result<GetSiteRoleResponseDto>.Failure(
                    new Error("409", "Ja existe uma role com este nome"));

            existingRole.Name = dto.Name.Trim();
            existingRole.Permissions = dto.Permissions.Where(SitePermissions.All.ContainsKey).ToList();
            await _repository.UpdateAsync(existingRole);

            return Result<GetSiteRoleResponseDto>.Success(
                GetSiteRoleResponseDto.FromEntity(existingRole));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSiteRoleResponseDto>.Failure(error);
        }
    }

    public async Task<Result<bool>> DeleteAsync(string id)
    {
        try
        {
            var existingRole = await _repository.GetAsync(id);
            if (existingRole == null)
                return Result<bool>.Failure(new Error("404", "Role nao encontrada"));

            var usersCount = await _siteUserRepository.CountByRoleIdAsync(id);
            if (usersCount > 0)
                return Result<bool>.Failure(
                    new Error("409", "Nao foi possivel excluir! A role esta vinculada a usuarios do site"));

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
