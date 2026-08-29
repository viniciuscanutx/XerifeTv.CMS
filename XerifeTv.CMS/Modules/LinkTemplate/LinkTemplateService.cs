using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.LinkTemplate.Dtos.Request;
using XerifeTv.CMS.Modules.LinkTemplate.Dtos.Response;
using XerifeTv.CMS.Modules.LinkTemplate.Interfaces;

namespace XerifeTv.CMS.Modules.LinkTemplate;

public class LinkTemplateService(ILinkTemplateRepository _repository) : ILinkTemplateService
{
    public async Task<Result<IEnumerable<GetLinkTemplateResponseDto>>> GetAllAsync(bool isIncludeDisabled = false)
    {
        try
        {
            var response = await _repository.GetAsync(isIncludeDisabled);

            return Result<IEnumerable<GetLinkTemplateResponseDto>>.Success(
                response
                .OrderBy(p => p.Name)
                .Select(GetLinkTemplateResponseDto.FromEntity));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<IEnumerable<GetLinkTemplateResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<GetLinkTemplateResponseDto?>> GetAsync(string id)
    {
        try
        {
            var response = await _repository.GetAsync(id);

            if (response == null)
                return Result<GetLinkTemplateResponseDto?>.Failure(new Error("404", "Tipo de link nao encontrado"));

            return Result<GetLinkTemplateResponseDto?>.Success(GetLinkTemplateResponseDto.FromEntity(response));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetLinkTemplateResponseDto?>.Failure(error);
        }
    }

    public async Task<Result<string>> CreateAsync(CreateLinkTemplateRequestDto dto)
    {
        try
        {
            var entity = dto.ToEntity();
            var response = await _repository.CreateAsync(entity);

            return Result<string>.Success(response);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<string>.Failure(error);
        }
    }

    public async Task<Result<string>> UpdateAsync(UpdateLinkTemplateRequestDto dto)
    {
        try
        {
            var entity = dto.ToEntity();
            var response = await _repository.GetAsync(dto.Id);

            if (response == null)
                return Result<string>.Failure(new Error("404", "Tipo de link nao encontrado"));

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
                return Result<bool>.Failure(new Error("404", "Tipo de link nao encontrado"));

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
