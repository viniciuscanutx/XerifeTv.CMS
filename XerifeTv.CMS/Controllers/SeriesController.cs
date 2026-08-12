using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Franchise.Dtos.Response;
using XerifeTv.CMS.Modules.Franchise.Interfaces;
using XerifeTv.CMS.Modules.Integrations.Imdb.Dtos;
using XerifeTv.CMS.Modules.Integrations.Imdb.Services;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;
using XerifeTv.CMS.Modules.Media.Delivery.Intefaces;
using XerifeTv.CMS.Modules.Series.Dtos.Request;
using XerifeTv.CMS.Modules.Series.Dtos.Response;
using XerifeTv.CMS.Modules.Series.Enums;
using XerifeTv.CMS.Modules.Series.Interfaces;
using XerifeTv.CMS.Shared.Helpers;
using XerifeTv.CMS.Views.Series.Models;

namespace XerifeTv.CMS.Controllers;

[Authorize]
public class SeriesController(
  ISeriesService _service,
  IImdbService _imdbService,
  IActivityLogService _activityLogService,
  ILogger<SeriesController> _logger,
  IEpisodesImporter _episodesImporter,
  ISpreadsheetBatchImporter<ISeriesService> _spreadsheetBatchImporter,
  IMediaDeliveryProfileService _mediaDeliveryProfileService,
  IFranchiseService _franchiseService) : Controller
{
	private const int limitResultsPage = 20;

	public async Task<IActionResult> Index(int? currentPage, ESeriesSearchFilter? filter, string? search)
	{
		Result<PagedList<GetSeriesResponseDto>> result;

		_logger.LogInformation($"{User.Identity?.Name} accessed the series page");

		if (filter is ESeriesSearchFilter && !string.IsNullOrEmpty(search))
		{
			result = await _service.GetByFilterAsync(
			  new GetSeriesByFilterRequestDto(
				filter,
				search,
				limitResultsPage,
				currentPage,
				isIncludeDisabled: true));

			ViewBag.Search = search;
			ViewBag.Filter = filter.ToString()?.ToLower();
		}
		else
		{
			result = await _service.GetAsync(currentPage ?? 1, limitResultsPage);
		}

		if (result.IsSuccess)
		{
			ViewBag.CurrentPage = result.Data?.CurrentPage;
			ViewBag.TotalPages = result.Data?.TotalPageCount ?? 1;
			ViewBag.HasNextPage = result.Data?.HasNext;
			ViewBag.HasPrevPage = result.Data?.HasPrevious;

			return View(result.Data?.Items);
		}

		return View(Enumerable.Empty<GetSeriesResponseDto>());
	}

	[Authorize(Roles = "admin, common")]
	public async Task<IActionResult> Form(string? id)
	{
        IEnumerable<GetFranchiseResponseDto> franchises = [];
        string? selectedFranchiseName = null;

		if (id is not null)
		{
			var response = await _service.GetAsync(id);
			if (response.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(response.Data?.FranchiseId))
                {
                    var franchiseResponse = await _franchiseService.GetAsync(response.Data.FranchiseId);
                    if (franchiseResponse.IsSuccess && franchiseResponse.Data is not null)
                    {
                        selectedFranchiseName = franchiseResponse.Data.Name;
                        franchises = [franchiseResponse.Data];
                    }
                }

                return View(new SeriesFormModelView(response.Data, franchises, selectedFranchiseName));
            }
		}

		return View(new SeriesFormModelView(null, franchises, selectedFranchiseName));
	}

	[Authorize(Roles = "admin, common")]
	public async Task<IActionResult> Create(CreateSeriesRequestDto dto)
	{
		var response = await _service.CreateAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Serie {dto.ImdbId} cadastrada com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} registered the serie {dto.Title}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Séries", "created", $"cadastrou a série \"{dto.Title}\"");

		return RedirectToAction("Index");
	}

	[Authorize(Roles = "admin, common")]
	public async Task<IActionResult> Update(UpdateSeriesRequestDto dto)
	{
		var response = await _service.UpdateAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Serie {dto.ImdbId} atualizada com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} updated the serie {dto.Title}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Séries", "updated", $"atualizou a série \"{dto.Title}\"");

		return RedirectToAction("Index");
	}

	[Authorize(Roles = "admin, common")]
	public async Task<IActionResult> Delete(string? id)
	{
		if (id is not null)
		{
			var response = await _service.DeleteAsync(id);

			TempData["Notification"] = response.IsFailure
			  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
			  : MessageViewHelper.SuccessJson($"Serie deletada com sucesso");

			_logger.LogInformation($"{User.Identity?.Name} removed the serie with id = {id}");
			await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Séries", "deleted", $"removeu a série com id = {id}");
		}

		return RedirectToAction("Index");
	}

	public async Task<IActionResult> Episodes(string? id, int? seasonFilter)
	{
		if (id is null) return RedirectToAction("Index");

		ViewBag.SerieId = id;
		ViewBag.SeasonFilter = seasonFilter;

		var response = await _service.GetEpisodesBySeasonAsync(id, seasonFilter ?? 1, includeDisabled: true);

		if (response.IsSuccess)
		{
			ViewBag.NumberSeasons = response.Data?.NumberSeasons;
			ViewBag.MaxEpisodeNumber = response.Data?.Episodes?.Any() == true
				? response.Data.Episodes.Max(e => e.Number)
				: 1;
			_logger.LogInformation($"{User.Identity?.Name} accessed the series episodes with id = {id}");

            IEnumerable<GetMediaDeliveryProfileResponseDto> mediaDeliveryProfiles = [];
            var mediaProfilesResponse = await _mediaDeliveryProfileService.GetAllAsync(isIncludeDisabled: false);
            if (mediaProfilesResponse.IsSuccess) mediaDeliveryProfiles = mediaProfilesResponse.Data ?? [];

            return View(new EpisodesModelView(response.Data, mediaDeliveryProfiles));
		}

		return RedirectToAction("Index");
	}

	[Authorize(Roles = "admin, common")]
	public async Task<IActionResult> CreateEpisode(CreateEpisodeRequestDto dto)
	{
		var response = await _service.CreateEpisodeAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Episodio T{dto.Season}:EP{dto.Number} cadastrado com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} registered episode {dto.Number} of season {dto.Season} of the serie with id = {dto.SerieId}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Séries", "created", $"cadastrou o episódio T{dto.Season}:EP{dto.Number} da série com id = {dto.SerieId}");

		return RedirectToAction("Episodes", new { id = dto.SerieId, seasonFilter = dto.Season });
	}

	[Authorize(Roles = "admin, common")]
	public async Task<IActionResult> UpdateEpisode(UpdateEpisodeRequestDto dto)
	{
		var response = await _service.UpdateEpisodeAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Episodio T{dto.Season}:EP{dto.Number} atualizado com sucesso");

		_logger.LogInformation($"{User.Identity?.Name} updated episode {dto.Number} of season {dto.Season} of the serie with id = {dto.SerieId}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Séries", "updated", $"atualizou o episódio T{dto.Season}:EP{dto.Number} da série com id = {dto.SerieId}");

		return RedirectToAction("Episodes", new { id = dto.SerieId, seasonFilter = dto.Season });
	}

	[Authorize(Roles = "admin, common")]
	public async Task<IActionResult> DeleteEpisode(string? serieId, string? id)
	{
		if (serieId is not null && id is not null)
		{
			var response = await _service.DeleteEpisodeAsync(serieId, id);

			TempData["Notification"] = response.IsFailure
			  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
			  : MessageViewHelper.SuccessJson($"Episodio deletado com sucesso");

			_logger.LogInformation($"{User.Identity?.Name} deleted episode with id = {id} of the serie with id = {serieId}");
			await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Séries", "deleted", $"removeu o episódio com id = {id} da série com id = {serieId}");
		}

		return RedirectToAction("Episodes", new { id = serieId });
	}

	[HttpGet]
	public async Task<IActionResult> GetSeriesByImdbId(string imdbId)
	{
		if (string.IsNullOrEmpty(imdbId)) return BadRequest();

		var response = await _imdbService.GetSeriesByImdbIdAsync(imdbId);

		if (response.IsFailure) return BadRequest(response.Error.Description);

		return Ok(response.Data);
	}

	[HttpGet]
	public async Task<IActionResult> SearchByName(string? term)
	{
		if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
			return Ok(Enumerable.Empty<SeriesSearchResultDto>());

		var response = await _imdbService.SearchSeriesByNameAsync(term);

		if (response.IsFailure)
			return BadRequest(response.Error.Description ?? string.Empty);

		return Ok(response.Data?.Results ?? []);
	}

	[HttpGet]
	public async Task<IActionResult> GetSeriesByTmdbId(int tmdbId)
	{
		var response = await _imdbService.GetSeriesByTmdbIdAsync(tmdbId);

		if (response.IsFailure) return BadRequest(response.Error.Description);

		return Ok(response.Data);
	}

	[Authorize(Roles = "admin, common")]
	[HttpPost]
	public async Task<IActionResult> RegisterBySpreadsheet(IFormFile file)
	{
		if (file is null || file.Length == 0) return BadRequest();

		var response = await _spreadsheetBatchImporter.ImportAsync(file);

		if (response.IsFailure)
			return BadRequest(response.Error.Description ?? string.Empty);

		return Ok(response.Data);
	}

	[Authorize(Roles = "admin, common")]
	[HttpGet]
	public async Task<IActionResult> MonitorSpreadsheetRegistration(string importId)
	{
		var response = await _spreadsheetBatchImporter.MonitorImportAsync(importId);

		if (response.IsSuccess && response.Data?.ProgressCount == 100 && response.Data.SuccessCount > 1)
			TempData["Notification"] = MessageViewHelper
			  .SuccessJson($"{response.Data.SuccessCount} series/episodios cadastrados/atualizados com sucesso");

		if (response.IsSuccess)
			return Ok(response.Data);

		return BadRequest(response.Error.Description ?? string.Empty);
	}

	[Authorize(Roles = "admin, common")]
	[HttpPost]
	public async Task<IActionResult> ImportEpisodesByImdbId(ImportEpisodesRequestDto dto)
	{
		if (string.IsNullOrEmpty(dto.SeriesId))
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson("Ops! Houve um problema [serie invalida]");
			return BadRequest();
		}

		var response = await _episodesImporter.ImportAsync(dto.SeriesId);

		if (response.IsFailure)
			return BadRequest(response.Error.Description ?? string.Empty);

		return Ok(response.Data);
	}

	[Authorize(Roles = "admin, common")]
	[HttpGet]
	public async Task<IActionResult> MonitorImdbEpisodesImport(string importId)
	{
		var response = await _episodesImporter.MonitorImportAsync(importId);

		if (response.IsSuccess && response.Data?.ProgressCount == 100 && response.Data.ImportedCount > 1)
			TempData["Notification"] = MessageViewHelper
			  .SuccessJson($"{response.Data.ImportedCount} episodios importados com sucesso");

		if (response.IsSuccess)
			return Ok(response.Data);

		return BadRequest(response.Error.Description ?? string.Empty);
	}

	[Authorize(Roles = "admin, common")]
	[HttpPost]
	public async Task<IActionResult> BatchAddEpisodeLinks(BatchEpisodeLinksRequestDto dto)
	{
		if (dto.IsBackgroundJob)
		{
			_ = Task.Run(async () =>
			{
				await _service.BatchAddEpisodeLinksAsync(dto);
			});

			TempData["Notification"] = MessageViewHelper
				.SuccessJson("Processamento em lote de episódios iniciado em segundo plano com sucesso!");

			return RedirectToAction("Episodes", new { id = dto.SerieId, seasonFilter = dto.Season });
		}

		var response = await _service.BatchAddEpisodeLinksAsync(dto);

		TempData["Notification"] = response.IsFailure
		  ? MessageViewHelper.ErrorJson(response.Error.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"{response.Data} episódio(s) atualizado(s)/cadastrado(s) com sucesso!");

		_logger.LogInformation($"{User.Identity?.Name} batch added links for season {dto.Season} of serie {dto.SerieId}");
		await _activityLogService.LogAsync(User.Identity?.Name ?? "desconhecido", "Séries", "batch_updated", $"atualizou {response.Data} link(s) de episódio em lote da temporada {dto.Season} da série com id = {dto.SerieId}");

		return RedirectToAction("Episodes", new { id = dto.SerieId, seasonFilter = dto.Season });
	}

	[Authorize(Roles = "admin, common")]
	[HttpPost]
	public async Task<IActionResult> QuickUpdateEpisodeVideoUrl(string serieId, string episodeId, int season, int number, string videoUrl, string? alternativeVideoUrl)
	{
		var episodeResult = await _service.GetEpisodesBySeasonAsync(serieId, season, includeDisabled: true);
		if (episodeResult.IsFailure || episodeResult.Data?.Episodes is null)
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson("Episódio não encontrado");
			return RedirectToAction("Episodes", new { id = serieId, seasonFilter = season });
		}

		var episode = episodeResult.Data.Episodes.FirstOrDefault(e => e.Id == episodeId);
		if (episode is null)
		{
			TempData["Notification"] = MessageViewHelper.ErrorJson("Episódio não encontrado");
			return RedirectToAction("Episodes", new { id = serieId, seasonFilter = season });
		}

		var updateDto = new UpdateEpisodeRequestDto
		{
			Id = episode.Id,
			SerieId = serieId,
			Season = episode.Season,
			Number = episode.Number,
			Title = episode.Title,
			BannerUrl = episode.BannerUrl,
			VideoUrl = videoUrl,
			AlternativeVideoUrl = alternativeVideoUrl ?? episode.AlternativeVideoUrl,
			VideoDuration = episode.Video?.Duration ?? 0,
			VideoStreamFormat = episode.Video?.StreamFormat ?? "m3u8",
			VideoSubtitle = episode.Video?.Subtitle,
			MediaDeliveryProfileId = episode.MediaDeliveryProfileId,
			MediaRoute = episode.MediaRoute,
			HighQuality = episode.HighQuality,
			Disabled = episode.Disabled
		};

		var updateResponse = await _service.UpdateEpisodeAsync(updateDto);

		TempData["Notification"] = updateResponse.IsFailure
		  ? MessageViewHelper.ErrorJson(updateResponse.Error?.Description ?? string.Empty)
		  : MessageViewHelper.SuccessJson($"Link do episódio T{season}:EP{number} atualizado com sucesso!");

		return RedirectToAction("Episodes", new { id = serieId, seasonFilter = season });
	}
}
