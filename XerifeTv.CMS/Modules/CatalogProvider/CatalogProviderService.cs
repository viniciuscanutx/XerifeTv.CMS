using XerifeTv.CMS.Modules.CatalogProvider.Dtos.Request;
using XerifeTv.CMS.Modules.CatalogProvider.Dtos.Response;
using XerifeTv.CMS.Modules.CatalogProvider.Interfaces;
using XerifeTv.CMS.Modules.Common;

namespace XerifeTv.CMS.Modules.CatalogProvider;

public class CatalogProviderService(ICatalogProviderRepository _repository) : ICatalogProviderService
{
    public async Task<Result<IEnumerable<GetCatalogProviderResponseDto>>> GetAllAsync(bool isIncludeDisabled = false)
    {
        try
        {
            var response = await _repository.GetAsync(isIncludeDisabled);

            return Result<IEnumerable<GetCatalogProviderResponseDto>>.Success(
                response
                .OrderBy(p => p.Order)
                .ThenByDescending(p => p.IsDefault)
                .ThenBy(p => p.Name)
                .Select(GetCatalogProviderResponseDto.FromEntity));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<IEnumerable<GetCatalogProviderResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<GetCatalogProviderResponseDto?>> GetAsync(string id)
    {
        try
        {
            var response = await _repository.GetAsync(id);

            if (response == null)
                return Result<GetCatalogProviderResponseDto?>.Failure(new Error("404", "Provedor de catálogo não encontrado"));

            return Result<GetCatalogProviderResponseDto?>.Success(GetCatalogProviderResponseDto.FromEntity(response));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetCatalogProviderResponseDto?>.Failure(error);
        }
    }

    public async Task<Result<string>> CreateAsync(CreateCatalogProviderRequestDto dto)
    {
        try
        {
            var entity = dto.ToEntity();

            if (entity.IsDefault) await ClearDefaultAsync();

            var response = await _repository.CreateAsync(entity);

            return Result<string>.Success(response);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<string>.Failure(error);
        }
    }

    public async Task<Result<string>> UpdateAsync(UpdateCatalogProviderRequestDto dto)
    {
        try
        {
            var entity = dto.ToEntity();
            var response = await _repository.GetAsync(dto.Id);

            if (response == null)
                return Result<string>.Failure(new Error("404", "Provedor de catálogo não encontrado"));

            if (entity.IsDefault) await ClearDefaultAsync(exceptId: entity.Id);

            entity.CreateAt = response.CreateAt;
            await _repository.UpdateAsync(entity);
            return Result<string>.Success(entity.Id);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<string>.Failure(error);
        }
    }

    public async Task<Result<bool>> DeleteAsync(string id)
    {
        try
        {
            var response = await _repository.GetAsync(id);

            if (response == null)
                return Result<bool>.Failure(new Error("404", "Provedor de catálogo não encontrado"));

            await _repository.DeleteAsync(id);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<bool>.Failure(error);
        }
    }

    // Só um provedor pode ser o padrão: ao marcar um, desmarca os demais.
    private async Task ClearDefaultAsync(string? exceptId = null)
    {
        var providers = await _repository.GetAsync(isIncludeDisabled: true);
        foreach (var provider in providers.Where(p => p.IsDefault && p.Id != exceptId))
        {
            provider.IsDefault = false;
            await _repository.UpdateAsync(provider);
        }
    }
}
