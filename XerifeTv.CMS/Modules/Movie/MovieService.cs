using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Franchise.Interfaces;
using XerifeTv.CMS.Modules.Integrations.Webhook.Enums;
using XerifeTv.CMS.Modules.Integrations.Webhook.Interfaces;
using XerifeTv.CMS.Modules.Movie.Dtos.Request;
using XerifeTv.CMS.Modules.Movie.Dtos.Response;
using XerifeTv.CMS.Modules.Movie.Interfaces;
using XerifeTv.CMS.Modules.Movie.Specifications;

using XerifeTv.CMS.Modules.Integrations.Imdb.Services;

namespace XerifeTv.CMS.Modules.Movie;

public sealed class MovieService(
    IMovieRepository _repository,
    IWebhookService _webhookService,
    IFranchiseService _franchiseService,
    IImdbService _imdbService) : IMovieService
{
    public async Task<Result<PagedList<GetMovieResponseDto>>> GetAsync(int currentPage, int limit)
    {
        try
        {
            var response = await _repository.GetAsync(currentPage, limit);

            var result = new PagedList<GetMovieResponseDto>(
              response.CurrentPage,
              response.TotalPageCount,
              response.Items.Select(GetMovieResponseDto.FromEntity));

            return Result<PagedList<GetMovieResponseDto>>.Success(result);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<PagedList<GetMovieResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<GetMovieResponseDto?>> GetAsync(string id)
    {
        try
        {
            var response = await _repository.GetAsync(id);

            if (response is null)
                return Result<GetMovieResponseDto?>
                  .Failure(new Error("404", "Conteudo nao encontrado"));

            return Result<GetMovieResponseDto?>
              .Success(GetMovieResponseDto.FromEntity(response));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetMovieResponseDto?>.Failure(error);
        }
    }

    public async Task<Result<GetMovieResponseDto?>> GetByImdbIdAsync(string imdbId)
    {
        try
        {
            var response = await _repository.GetByImdbIdAsync(imdbId);

            if (response is null)
                return Result<GetMovieResponseDto?>
                  .Failure(new Error("404", "Conteudo nao encontrado"));

            return Result<GetMovieResponseDto?>
              .Success(GetMovieResponseDto.FromEntity(response));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetMovieResponseDto?>.Failure(error);
        }
    }

    public async Task<Result<string>> CreateAsync(CreateMovieRequestDto dto)
    {
        try
        {
            var entity = dto.ToEntity();
            var imdbIdSpec = new UniqueImdbIdSpecification(_repository);

            if (!await imdbIdSpec.IsSatisfiedByAsync(entity))
                return Result<string>.Failure(
                  new Error("409", $"Filme nao cadastrado. Imdb ID {entity.ImdbId} duplicado"));

            var response = await _repository.CreateAsync(entity);

            _ = Task.Run(() => _webhookService.DispacthWebhooksByTriggerEventAsync(EWebhookTriggerEvent.MOVIE_PUBLISHED, response));

            return Result<string>.Success(response);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<string>.Failure(error);
        }
    }

    public async Task<Result<string>> UpdateAsync(UpdateMovieRequestDto dto)
    {
        try
        {
            var entity = dto.ToEntity();
            var response = await _repository.GetAsync(entity.Id);

            if (response is null)
                return Result<string>.Failure(new Error("404", "Conteudo nao encontrado"));

            var imdbIdSpec = new UniqueImdbIdSpecification(_repository);

            if (!await imdbIdSpec.IsSatisfiedByAsync(entity))
                return Result<string>.Failure(
                  new Error("409", $"Filme nao atualizado. Imdb ID {entity.ImdbId} duplicado"));

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

            if (response is null)
                return Result<bool>.Failure(new Error("404", "Conteudo nao encontrado"));

            await _repository.DeleteAsync(id);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<bool>.Failure(error);
        }
    }

    public async Task<Result<PagedList<GetMovieResponseDto>>> GetByFilterAsync(GetMoviesByFilterRequestDto dto)
    {
        try
        {
            if (dto.Filter == Enums.EMovieSearchFilter.FRANCHISE)
            {
                var franchiseResult = await _franchiseService.GetByNameAsync(dto.Search);

                if (franchiseResult.IsFailure)
                    return Result<PagedList<GetMovieResponseDto>>.Success(new PagedList<GetMovieResponseDto>(0, 0, []));

                dto.Search = franchiseResult.Data!.Id;
            }

            var response = await _repository.GetByFilterAsync(dto);

            var result = new PagedList<GetMovieResponseDto>(
              response.CurrentPage,
              response.TotalPageCount,
              response.Items.Select(GetMovieResponseDto.FromEntity));

            return Result<PagedList<GetMovieResponseDto>>.Success(result);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<PagedList<GetMovieResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<int>> BatchAddMoviesAsync(BatchMoviesRequestDto dto)
    {
        try
        {
            var imdbIds = (dto.ImdbIdsText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var videoUrls = (dto.VideoUrlsText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var altVideoUrls = (dto.AlternativeVideoUrlsText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var parentalRatings = (dto.ParentalRatingsText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (imdbIds.Length == 0)
                return Result<int>.Failure(new Error("400", "Nenhum IMDB ID fornecido"));

            int processedCount = 0;

            for (int i = 0; i < imdbIds.Length; i++)
            {
                string rawImdbId = imdbIds[i];
                if (string.IsNullOrWhiteSpace(rawImdbId)) continue;

                if (rawImdbId.Contains('.')) rawImdbId = rawImdbId.Split('.')[0];
                string imdbId = (!rawImdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase) && long.TryParse(rawImdbId, out _))
                    ? $"tt{rawImdbId}"
                    : rawImdbId;

                string videoUrl = i < videoUrls.Length ? videoUrls[i] : string.Empty;
                string? altVideoUrl = i < altVideoUrls.Length ? altVideoUrls[i] : null;

                int parentalRating = dto.DefaultParentalRating;
                if (i < parentalRatings.Length && !string.IsNullOrWhiteSpace(parentalRatings[i]))
                {
                    if (parentalRatings[i].Equals("L", StringComparison.OrdinalIgnoreCase))
                        parentalRating = 0;
                    else if (int.TryParse(parentalRatings[i], out var parsedRating))
                        parentalRating = parsedRating;
                }

                var existingMovieResponse = await GetByImdbIdAsync(imdbId);

                if (existingMovieResponse.IsSuccess && existingMovieResponse.Data != null)
                {
                    var existingMovie = existingMovieResponse.Data;
                    var updateDto = new UpdateMovieRequestDto
                    {
                        Id = existingMovie.Id,
                        ImdbId = imdbId,
                        Title = existingMovie.Title,
                        Synopsis = existingMovie.Synopsis,
                        Categories = existingMovie.Categories,
                        PosterUrl = existingMovie.PosterUrl,
                        BannerUrl = existingMovie.BannerUrl,
                        LogoUrl = existingMovie.LogoUrl,
                        ReleaseYear = existingMovie.ReleaseYear,
                        Review = existingMovie.Review,
                        ParentalRating = parentalRating,
                        VideoUrl = !string.IsNullOrWhiteSpace(videoUrl) ? videoUrl : (existingMovie.Video?.Url ?? string.Empty),
                        AlternativeVideoUrl = !string.IsNullOrWhiteSpace(altVideoUrl) ? altVideoUrl : existingMovie.AlternativeVideoUrl,
                        VideoDuration = existingMovie.Video?.Duration ?? 0,
                        VideoStreamFormat = dto.VideoStreamFormat,
                        VideoSubtitle = existingMovie.Video?.Subtitle,
                        MediaDeliveryProfileId = existingMovie.MediaDeliveryProfileId,
                        MediaRoute = existingMovie.MediaRoute,
                        TrailerVideoYoutubeId = existingMovie.TrailerVideoYoutubeId,
                        HighQuality = dto.HighQuality,
                        FollowRedirect = dto.FollowRedirect
                    };

                    var updateResult = await UpdateAsync(updateDto);
                    if (updateResult.IsSuccess) processedCount++;
                }
                else
                {
                    var imdbInfoResult = await _imdbService.GetMovieByImdbIdAsync(imdbId);

                    var createDto = new CreateMovieRequestDto
                    {
                        ImdbId = imdbId,
                        Title = imdbInfoResult.IsSuccess ? (imdbInfoResult.Data?.Title ?? string.Empty) : imdbId,
                        Synopsis = imdbInfoResult.IsSuccess ? (imdbInfoResult.Data?.Overview ?? string.Empty) : string.Empty,
                        Categories = imdbInfoResult.IsSuccess ? string.Join(", ", imdbInfoResult.Data?.Genres.Select(g => g.Name.ToLower()) ?? []) : string.Empty,
                        PosterUrl = imdbInfoResult.IsSuccess ? (imdbInfoResult.Data?.PosterUrl ?? string.Empty) : string.Empty,
                        BannerUrl = imdbInfoResult.IsSuccess ? (imdbInfoResult.Data?.BannerUrl ?? string.Empty) : string.Empty,
                        LogoUrl = imdbInfoResult.IsSuccess ? imdbInfoResult.Data?.LogoUrl : null,
                        ReleaseYear = imdbInfoResult.IsSuccess ? int.Parse(imdbInfoResult.Data?.ReleaseYear ?? "0") : 0,
                        Review = imdbInfoResult.IsSuccess ? (imdbInfoResult.Data?.VoteAverage ?? 0) : 0,
                        ParentalRating = parentalRating,
                        VideoUrl = videoUrl,
                        AlternativeVideoUrl = altVideoUrl,
                        VideoDuration = imdbInfoResult.IsSuccess ? (imdbInfoResult.Data?.DurationInSeconds ?? 0) : 0,
                        VideoStreamFormat = dto.VideoStreamFormat,
                        HighQuality = dto.HighQuality,
                        FollowRedirect = dto.FollowRedirect
                    };

                    var createResult = await CreateAsync(createDto);
                    if (createResult.IsSuccess) processedCount++;
                }
            }

            return Result<int>.Success(processedCount);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<int>.Failure(error);
        }
    }
}
