using XerifeTv.CMS.Modules.Activity.Dtos.Response;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.Common;

namespace XerifeTv.CMS.Modules.Activity;

public class ActivityLogService(
    IActivityLogRepository _repository,
    ILogger<ActivityLogService> _logger) : IActivityLogService
{
    public async Task LogAsync(string userName, string category, string action, string description)
    {
        try
        {
            await _repository.CreateAsync(new ActivityLogEntity
            {
                UserName = userName,
                Category = category,
                Action = action,
                Description = description
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar atividade: {Description}", description);
        }
    }

    public async Task<Result<PagedList<GetActivityLogResponseDto>>> GetAsync(int currentPage, int limit)
    {
        try
        {
            var response = await _repository.GetAsync(currentPage, limit);

            return Result<PagedList<GetActivityLogResponseDto>>.Success(
                new PagedList<GetActivityLogResponseDto>(
                    response.CurrentPage,
                    response.TotalPageCount,
                    response.Items.Select(GetActivityLogResponseDto.FromEntity)));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<PagedList<GetActivityLogResponseDto>>.Failure(error);
        }
    }
}
