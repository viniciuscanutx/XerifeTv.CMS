using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.SiteWatchProgress.Dtos.Request;
using XerifeTv.CMS.Modules.SiteWatchProgress.Dtos.Response;
using XerifeTv.CMS.Modules.SiteWatchProgress.Interfaces;

namespace XerifeTv.CMS.Modules.SiteWatchProgress;

public sealed class SiteWatchProgressService(
    ISiteWatchProgressRepository _repository) : ISiteWatchProgressService
{
    public async Task<Result<IEnumerable<GetWatchProgressResponseDto>>> GetContinueWatchingAsync(string siteUserId, int limit)
    {
        try
        {
            var response = await _repository.GetBySiteUserIdAsync(siteUserId, limit);

            return Result<IEnumerable<GetWatchProgressResponseDto>>.Success(
                response.Select(GetWatchProgressResponseDto.FromEntity));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<IEnumerable<GetWatchProgressResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<GetWatchProgressResponseDto>> UpsertAsync(string siteUserId, UpsertWatchProgressRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.ContentId))
                return Result<GetWatchProgressResponseDto>.Failure(new Error("400", "ContentId obrigatorio"));

            if (dto.Duration <= 0)
                return Result<GetWatchProgressResponseDto>.Failure(new Error("400", "Duration invalida"));

            var progressPercentage = (int)Math.Round(Math.Clamp(dto.CurrentTime / dto.Duration * 100, 0, 100));
            var now = DateTime.UtcNow;

            var existing = await _repository.GetBySiteUserAndContentAsync(siteUserId, dto.ContentId);

            if (existing != null)
            {
                existing.Type = dto.Type;
                existing.Title = dto.Title;
                existing.Poster = dto.Poster;
                existing.Backdrop = dto.Backdrop;
                existing.CurrentTime = dto.CurrentTime;
                existing.Duration = dto.Duration;
                existing.ProgressPercentage = progressPercentage;
                existing.UpdateAt = now;

                await _repository.UpdateAsync(existing);

                return Result<GetWatchProgressResponseDto>.Success(GetWatchProgressResponseDto.FromEntity(existing));
            }

            var entity = new WatchProgressEntity
            {
                SiteUserId = siteUserId,
                ContentId = dto.ContentId,
                Type = dto.Type,
                Title = dto.Title,
                Poster = dto.Poster,
                Backdrop = dto.Backdrop,
                CurrentTime = dto.CurrentTime,
                Duration = dto.Duration,
                ProgressPercentage = progressPercentage,
                UpdateAt = now
            };

            await _repository.CreateAsync(entity);

            return Result<GetWatchProgressResponseDto>.Success(GetWatchProgressResponseDto.FromEntity(entity));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetWatchProgressResponseDto>.Failure(error);
        }
    }

    public async Task<Result<bool>> DeleteAsync(string siteUserId, string contentId)
    {
        try
        {
            await _repository.DeleteBySiteUserAndContentAsync(siteUserId, contentId);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<bool>.Failure(error);
        }
    }
}
