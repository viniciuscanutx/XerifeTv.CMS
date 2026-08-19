using MongoDB.Driver.Linq;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Franchise.Interfaces;
using XerifeTv.CMS.Modules.Integrations.Webhook.Enums;
using XerifeTv.CMS.Modules.Integrations.Webhook.Interfaces;
using XerifeTv.CMS.Modules.Movie.Dtos.Response;
using XerifeTv.CMS.Modules.Series.Dtos.Request;
using XerifeTv.CMS.Modules.Series.Dtos.Response;
using XerifeTv.CMS.Modules.Series.Interfaces;
using XerifeTv.CMS.Modules.Series.Specifications;

namespace XerifeTv.CMS.Modules.Series;

public class SeriesService(
    ISeriesRepository _repository,
    IWebhookService _webhookService,
    IFranchiseService _franchiseService,
    IConfiguration _configuration) : ISeriesService
{
    public async Task<Result<PagedList<GetSeriesResponseDto>>> GetAsync(int currentPage, int limit)
    {
        try
        {
            var response = await _repository.GetAsync(currentPage, limit);

            var result = new PagedList<GetSeriesResponseDto>(
              response.CurrentPage,
              response.TotalPageCount,
              response.Items.Select(GetSeriesResponseDto.FromEntity));

            return Result<PagedList<GetSeriesResponseDto>>.Success(result);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<PagedList<GetSeriesResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<GetSeriesResponseDto?>> GetAsync(string id)
    {
        try
        {
            var response = await _repository.GetAsync(id);

            if (response is null)
                return Result<GetSeriesResponseDto?>
                  .Failure(new Error("404", "Conteudo nao encontrado"));

            return Result<GetSeriesResponseDto?>
              .Success(GetSeriesResponseDto.FromEntity(response));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSeriesResponseDto?>.Failure(error);
        }
    }

    public async Task<Result<GetSeriesResponseDto?>> GetByImdbIdAsync(string imdbId)
    {
        try
        {
            var response = await _repository.GetByImdbIdAsync(imdbId);

            if (response is null)
                return Result<GetSeriesResponseDto?>
                  .Failure(new Error("404", "Conteudo nao encontrado"));

            return Result<GetSeriesResponseDto?>
              .Success(GetSeriesResponseDto.FromEntity(response));
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetSeriesResponseDto?>.Failure(error);
        }
    }

    public async Task<Result<string>> CreateAsync(CreateSeriesRequestDto dto)
    {
        try
        {
            var entity = dto.ToEntity();
            var imdbIdSpec = new UniqueImdbIdSpecification(_repository);

            if (!await imdbIdSpec.IsSatisfiedByAsync(entity))
                return Result<string>.Failure(
                  new Error("409", $"Serie nao cadastrada. Imdb ID {entity.ImdbId} duplicado"));

            var response = await _repository.CreateAsync(entity);

            _ = Task.Run(() => _webhookService.DispacthWebhooksByTriggerEventAsync(EWebhookTriggerEvent.SERIES_PUBLISHED, response));

            return Result<string>.Success(entity.Id);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<string>.Failure(error);
        }
    }

    public async Task<Result<string>> UpdateAsync(UpdateSeriesRequestDto dto)
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
                  new Error("409", $"Serie nao atualizada. Imdb ID {entity.ImdbId} duplicado"));


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

    public async Task<Result<PagedList<GetSeriesResponseDto>>> GetByFilterAsync(GetSeriesByFilterRequestDto dto)
    {
        try
        {
            if (dto.Filter == Enums.ESeriesSearchFilter.FRANCHISE)
            {
                var franchiseResult = await _franchiseService.GetByNameAsync(dto.Search);

                if (franchiseResult.IsFailure)
                    return Result<PagedList<GetSeriesResponseDto>>.Success(new PagedList<GetSeriesResponseDto>(0, 0, []));

                dto.Search = franchiseResult.Data!.Id;
            }

            var response = await _repository.GetByFilterAsync(dto);

            var result = new PagedList<GetSeriesResponseDto>(
              response.CurrentPage,
              response.TotalPageCount,
              response.Items.Select(GetSeriesResponseDto.FromEntity));

            return Result<PagedList<GetSeriesResponseDto>>.Success(result);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<PagedList<GetSeriesResponseDto>>.Failure(error);
        }
    }

    public async Task<Result<GetEpisodesResponseDto>> GetEpisodesBySeasonAsync(
      string serieId, int season, bool includeDisabled, int? specificEpisode = null)
    {
        try
        {
            var response = await _repository.GetEpisodesBySeasonAsync(serieId, season, includeDisabled, specificEpisode);

            if (response is null)
                return Result<GetEpisodesResponseDto>
                  .Failure(new Error("404", "Conteudo nao encontrado"));

            var result = GetEpisodesResponseDto.FromEntity(response);
            result.SetUrlResolverPathEpisodes(_configuration["SecuritySettings:ContentEncryptionKey"]!);

            return Result<GetEpisodesResponseDto>.Success(result);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<GetEpisodesResponseDto>.Failure(error);
        }
    }

    public async Task<Result<string>> CreateEpisodeAsync(CreateEpisodeRequestDto dto)
    {
        try
        {
            var seriesResponse = await _repository.GetAsync(dto.SerieId);

            if (seriesResponse is null)
                return Result<string>.Failure(new Error("404", "Conteudo nao encontrado"));

            var episodesResult = await GetEpisodesBySeasonAsync(dto.SerieId, dto.Season, includeDisabled: true);
            if (episodesResult.IsFailure)
                return Result<string>.Failure(episodesResult.Error);

            var existingEpisode = episodesResult.Data?.Episodes?
                .Any(e => e.Season == dto.Season && e.Number == dto.Number) ?? false;

            if (existingEpisode)
                return Result<string>.Failure(
                    new Error("409", $"Episodio nao cadastrado. [{seriesResponse.ImdbId}|T{dto.Season}:EP{dto.Number}] duplicado"));

            await _repository.CreateEpisodeAsync(seriesResponse.Id, dto.ToEntity());

            return Result<string>.Success(dto.ToEntity().Id);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<string>.Failure(error);
        }
    }

    public async Task<Result<string>> UpdateEpisodeAsync(UpdateEpisodeRequestDto dto)
    {
        try
        {
            var seriesResponse = await _repository.GetAsync(dto.SerieId);

            if (seriesResponse is null)
                return Result<string>.Failure(new Error("404", "Conteudo nao encontrado"));

            var episodesResult = await GetEpisodesBySeasonAsync(dto.SerieId, dto.Season, includeDisabled: true);
            if (episodesResult.IsFailure)
                return Result<string>.Failure(episodesResult.Error);

            var existingEpisode = episodesResult.Data?.Episodes?
                .Any(e => e.Season == dto.Season && e.Number == dto.Number && e.Id != dto.Id) ?? false;

            if (existingEpisode)
                return Result<string>.Failure(
                    new Error("409", $"Episodio nao atualizado. [{seriesResponse.ImdbId}|T{dto.Season}:EP{dto.Number}] duplicado"));

            await _repository.UpdateEpisodeAsync(seriesResponse.Id, dto.ToEntity());

            return Result<string>.Success(seriesResponse.Id);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<string>.Failure(error);
        }
    }

    public async Task<Result<bool>> DeleteEpisodeAsync(string serieId, string id)
    {
        try
        {
            var response = await _repository.GetAsync(serieId);

            if (response is null)
                return Result<bool>.Failure(new Error("404", "Serie nao encontrada"));

            await _repository.DeleteEpisodeAsync(serieId, id);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<bool>.Failure(error);
        }
    }

    public async Task<Result<int>> BatchAddEpisodeLinksAsync(BatchEpisodeLinksRequestDto dto)
    {
        try
        {
            var seriesResponse = await _repository.GetAsync(dto.SerieId);
            if (seriesResponse is null)
                return Result<int>.Failure(new Error("404", "Série não encontrada"));

            var videoUrls = (dto.VideoUrlsText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var altVideoUrls = (dto.AlternativeVideoUrlsText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var isRemoveMode = dto.Mode != null && dto.Mode.Equals("remove", StringComparison.OrdinalIgnoreCase);

            if (isRemoveMode)
            {
                var existingEpisodesResult = await GetEpisodesBySeasonAsync(dto.SerieId, dto.Season, includeDisabled: true);
                var existingEpisodes = existingEpisodesResult.IsSuccess && existingEpisodesResult.Data?.Episodes != null
                    ? existingEpisodesResult.Data.Episodes.ToList()
                    : [];

                int startNumber = dto.StartEpisodeNumber > 0 ? dto.StartEpisodeNumber : 1;
                int lineCount = Math.Max(videoUrls.Length, altVideoUrls.Length);
                int episodesToClear = lineCount > 0 ? lineCount : (dto.RemoveEpisodesCount > 0 ? dto.RemoveEpisodesCount : 1);

                bool removeDublado = string.IsNullOrWhiteSpace(dto.RemoveTarget) || dto.RemoveTarget.Equals("dublado", StringComparison.OrdinalIgnoreCase) || dto.RemoveTarget.Equals("both", StringComparison.OrdinalIgnoreCase);
                bool removeLegendado = string.IsNullOrWhiteSpace(dto.RemoveTarget) || dto.RemoveTarget.Equals("legendado", StringComparison.OrdinalIgnoreCase) || dto.RemoveTarget.Equals("both", StringComparison.OrdinalIgnoreCase);

                int removedCount = 0;

                for (int i = 0; i < episodesToClear; i++)
                {
                    int episodeNumber = startNumber + i;
                    var existingEpisode = existingEpisodes.FirstOrDefault(e => e.Season == dto.Season && e.Number == episodeNumber);

                    if (existingEpisode != null)
                    {
                        var updateDto = new UpdateEpisodeRequestDto
                        {
                            Id = existingEpisode.Id,
                            SerieId = dto.SerieId,
                            Title = existingEpisode.Title,
                            BannerUrl = existingEpisode.BannerUrl,
                            Number = existingEpisode.Number,
                            Season = existingEpisode.Season,
                            VideoUrl = removeDublado ? string.Empty : (existingEpisode.Video?.Url ?? string.Empty),
                            AlternativeVideoUrl = removeLegendado ? null : existingEpisode.AlternativeVideoUrl,
                            VideoDuration = existingEpisode.Video?.Duration ?? 0,
                            VideoStreamFormat = dto.VideoStreamFormat,
                            VideoSubtitle = existingEpisode.Video?.Subtitle,
                            FollowRedirect = existingEpisode.Video?.FollowRedirect ?? false,
                            MediaDeliveryProfileId = existingEpisode.MediaDeliveryProfileId,
                            MediaRoute = existingEpisode.MediaRoute,
                            HighQuality = existingEpisode.HighQuality,
                            Disabled = existingEpisode.Disabled
                        };

                        var updateResult = await UpdateEpisodeAsync(updateDto);
                        if (updateResult.IsSuccess) removedCount++;
                    }
                }

                return Result<int>.Success(removedCount);
            }

            int maxCount = Math.Max(videoUrls.Length, altVideoUrls.Length);

            if (maxCount == 0)
                return Result<int>.Failure(new Error("400", "Nenhum link fornecido"));

            var existingEpisodesResultAdd = await GetEpisodesBySeasonAsync(dto.SerieId, dto.Season, includeDisabled: true);
            var existingEpisodesAdd = existingEpisodesResultAdd.IsSuccess && existingEpisodesResultAdd.Data?.Episodes != null
                ? existingEpisodesResultAdd.Data.Episodes.ToList()
                : [];

            int updatedCount = 0;
            int maxEpisodeNumber = existingEpisodesAdd.Any() ? existingEpisodesAdd.Max(e => e.Number) : 1;
            int startNumberAdd = dto.StartEpisodeNumber > 0 ? dto.StartEpisodeNumber : 1;

            if (dto.OnlyExistingEpisodes && startNumberAdd > maxEpisodeNumber)
            {
                startNumberAdd = maxEpisodeNumber;
            }

            for (int i = 0; i < maxCount; i++)
            {
                int episodeNumber = startNumberAdd + i;
                string url = i < videoUrls.Length ? videoUrls[i] : string.Empty;
                string? altUrl = i < altVideoUrls.Length ? altVideoUrls[i] : null;

                var existingEpisode = existingEpisodesAdd.FirstOrDefault(e => e.Season == dto.Season && e.Number == episodeNumber);

                if (existingEpisode != null)
                {
                    var updateDto = new UpdateEpisodeRequestDto
                    {
                        Id = existingEpisode.Id,
                        SerieId = dto.SerieId,
                        Title = existingEpisode.Title,
                        BannerUrl = existingEpisode.BannerUrl,
                        Number = existingEpisode.Number,
                        Season = existingEpisode.Season,
                        VideoUrl = !string.IsNullOrWhiteSpace(url) ? url : (existingEpisode.Video?.Url ?? string.Empty),
                        AlternativeVideoUrl = !string.IsNullOrWhiteSpace(altUrl) ? altUrl : existingEpisode.AlternativeVideoUrl,
                        VideoDuration = existingEpisode.Video?.Duration ?? 0,
                        VideoStreamFormat = dto.VideoStreamFormat,
                        VideoSubtitle = existingEpisode.Video?.Subtitle,
                        FollowRedirect = existingEpisode.Video?.FollowRedirect ?? false,
                        MediaDeliveryProfileId = existingEpisode.MediaDeliveryProfileId,
                        MediaRoute = existingEpisode.MediaRoute,
                        HighQuality = dto.HighQuality,
                        Disabled = false
                    };

                    var updateResult = await UpdateEpisodeAsync(updateDto);
                    if (updateResult.IsSuccess) updatedCount++;
                }
                else if (!dto.OnlyExistingEpisodes)
                {
                    var createDto = new CreateEpisodeRequestDto
                    {
                        SerieId = dto.SerieId,
                        Title = $"Episódio {episodeNumber}",
                        BannerUrl = seriesResponse.BannerUrl,
                        Number = episodeNumber,
                        Season = dto.Season,
                        VideoUrl = url,
                        AlternativeVideoUrl = altUrl,
                        VideoDuration = 0,
                        VideoStreamFormat = dto.VideoStreamFormat,
                        HighQuality = dto.HighQuality,
                        IsDisabled = false
                    };

                    var createResult = await CreateEpisodeAsync(createDto);
                    if (createResult.IsSuccess) updatedCount++;
                }
            }

            return Result<int>.Success(updatedCount);
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<int>.Failure(error);
        }
    }
}
