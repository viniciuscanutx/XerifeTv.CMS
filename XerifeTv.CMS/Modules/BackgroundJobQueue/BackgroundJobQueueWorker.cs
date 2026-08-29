
using System.Text.Json;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.BackgroundJobQueue.Dtos.Request;
using XerifeTv.CMS.Modules.BackgroundJobQueue.Enums;
using XerifeTv.CMS.Modules.BackgroundJobQueue.Interfaces;
using XerifeTv.CMS.Modules.Channel.Interfaces;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Movie.Dtos.Request;
using XerifeTv.CMS.Modules.Movie.Interfaces;
using XerifeTv.CMS.Modules.Series.Dtos.Request;
using XerifeTv.CMS.Modules.Series.Interfaces;

namespace XerifeTv.CMS.Modules.BackgroundJobQueue;

public class BackgroundJobQueueWorker(
	IServiceProvider _serviceProvider,
	ILogger<BackgroundJobQueueWorker> _logger) : BackgroundService
{
	private const int MaxConcurrentJobs = 2;
	private readonly SemaphoreSlim _semaphore = new(MaxConcurrentJobs);
	private readonly List<string> _processingJobIds = [];

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			await _semaphore.WaitAsync(stoppingToken);
			await ProcessNextJobAsync(stoppingToken);
			await Task.Delay(1000, stoppingToken);
		}
	}

	private async Task ProcessNextJobAsync(CancellationToken stoppingToken)
	{
		try
		{
			using var scope = _serviceProvider.CreateScope();
			var backgroundJobQueueService = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueueService>();

			var filterJobsPendingDto = new GetBackgroundJobsByFilterRequestDto(
				status: EBackgroundJobStatus.PENDING,
				order: EBackgroundJobOrderFilter.REGISTRATION_DATE_ASC,
				limitResults: 1,
				currentPage: 1);

			var pendingJobsResult = await backgroundJobQueueService.GetByFilterAsync(filterJobsPendingDto);

			if (pendingJobsResult.IsFailure)
				throw new Exception(pendingJobsResult.Error.Description);

			var jobQueue = pendingJobsResult?.Data?.Items.FirstOrDefault();

			if (jobQueue == null || _processingJobIds.Contains(jobQueue.Id))
			{
				_semaphore.Release();
				return;
			}

			switch (jobQueue.Type)
			{
				case EBackgroundJobType.REGISTER_SPREADSHEET_MOVIES:
					_ = ImportSpreadsheetAsync<IMovieService>(backgroundJobQueueService, jobQueue.Id, jobQueue.SpreadsheetFileUrl!);
					break;

				case EBackgroundJobType.REGISTER_SPREADSHEET_SERIES:
					_ = ImportSpreadsheetAsync<ISeriesService>(backgroundJobQueueService, jobQueue.Id, jobQueue.SpreadsheetFileUrl!);
					break;

				case EBackgroundJobType.REGISTER_SPREADSHEET_CHANNELS:
					_ = ImportSpreadsheetAsync<IChannelService>(backgroundJobQueueService, jobQueue.Id, jobQueue.SpreadsheetFileUrl!);
					break;

				case EBackgroundJobType.IMPORT_EPISODES_FROM_SERIES_IMDB:
					_ = ImportEpisodesSeriesAsync(backgroundJobQueueService, jobQueue.Id, jobQueue.SeriesIdImportEpisodes!);
					break;

				case EBackgroundJobType.BATCH_ADD_MOVIES:
					_ = ProcessBatchMoviesAsync(backgroundJobQueueService, jobQueue.Id, jobQueue.PayloadJson ?? string.Empty);
					break;

				case EBackgroundJobType.BATCH_ADD_EPISODE_LINKS:
					_ = ProcessBatchEpisodeLinksAsync(backgroundJobQueueService, jobQueue.Id, jobQueue.PayloadJson ?? string.Empty);
					break;
			}

			_processingJobIds.Add(jobQueue.Id);
			await Task.Delay(1000, stoppingToken);
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, ex.InnerException?.Message ?? ex.Message);
		}
	}

	private async Task ImportSpreadsheetAsync<TService>(IBackgroundJobQueueService backgroundJobQueueService, string backgroundJobId, string spreadsheetUrl)
	{
		try
		{
			using var scope = _serviceProvider.CreateScope();
			var importer = scope.ServiceProvider.GetRequiredService<ISpreadsheetBatchImporter<TService>>();

			var file = await DownloadExcelAsFormFileAsync(spreadsheetUrl);
			var importResult = await importer.ImportAsync(file);

			if (importResult.IsFailure || string.IsNullOrEmpty(importResult.Data)) return;
			var importId = importResult.Data;

			while (true)
			{
				var monitorResult = await importer.MonitorImportAsync(importId);

				if (monitorResult.IsFailure || monitorResult.Data == null) continue;

				var data = monitorResult.Data;

				var updateBackgroundJobDto = new UpdateBackgroundJobRequestDto
				{
					Id = backgroundJobId,
					TotalRecordsToProcess = data.TotalItemsCount ?? 0,
					TotalFailedRecords = data.FailCount ?? 0,
					TotalSuccessfulRecords = data.SuccessCount ?? 0,
					TotalProcessedRecords = data.ProcessedCount ?? 0,
					ErrorList = data.ErrorList,
					Status = EBackgroundJobStatus.PROCESSING
				};

				if (data.ProgressCount == 100)
				{
					updateBackgroundJobDto.Status =
						updateBackgroundJobDto.TotalFailedRecords == updateBackgroundJobDto.TotalRecordsToProcess
						? EBackgroundJobStatus.FAILED
						: EBackgroundJobStatus.COMPLETED;

					await backgroundJobQueueService.UpdateAsync(updateBackgroundJobDto);

					break; //finish
				}

				await backgroundJobQueueService.UpdateAsync(updateBackgroundJobDto);
			}
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, ex.InnerException?.Message ?? ex.Message);
		}
		finally
		{
			_processingJobIds.Remove(backgroundJobId);
			_semaphore.Release();
		}
	}

	private async Task ImportEpisodesSeriesAsync(IBackgroundJobQueueService backgroundJobQueueService, string backgroundJobId, string seriesId)
	{
		try
		{
			using var scope = _serviceProvider.CreateScope();
			var importer = scope.ServiceProvider.GetRequiredService<IEpisodesImporter>();

			var importResult = await importer.ImportAsync(seriesId);

			if (importResult.IsFailure || string.IsNullOrEmpty(importResult.Data)) return;
			var importId = importResult.Data;

			while (true)
			{
				var monitorResult = await importer.MonitorImportAsync(importId);

				if (monitorResult.IsFailure || monitorResult.Data == null) continue;

				var data = monitorResult.Data;

				var updateBackgroundJobDto = new UpdateBackgroundJobRequestDto
				{
					Id = backgroundJobId,
					TotalRecordsToProcess = data.TotalItemsCount,
					TotalSuccessfulRecords = data.ImportedCount,
					TotalProcessedRecords = data.ProcessedCount,
					Status = EBackgroundJobStatus.PROCESSING
				};

				if (data.ProgressCount == 100)
				{
					updateBackgroundJobDto.Status =
						updateBackgroundJobDto.TotalFailedRecords == updateBackgroundJobDto.TotalRecordsToProcess
						? EBackgroundJobStatus.FAILED
						: EBackgroundJobStatus.COMPLETED;

					await backgroundJobQueueService.UpdateAsync(updateBackgroundJobDto);

					break; //finish
				}

				await backgroundJobQueueService.UpdateAsync(updateBackgroundJobDto);
			}
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, ex.InnerException?.Message ?? ex.Message);
		}
		finally
		{
			_processingJobIds.Remove(backgroundJobId);
			_semaphore.Release();
		}
	}

	private async Task ProcessBatchMoviesAsync(IBackgroundJobQueueService backgroundJobQueueService, string backgroundJobId, string payloadJson)
	{
		try
		{
			using var scope = _serviceProvider.CreateScope();
			var movieService = scope.ServiceProvider.GetRequiredService<IMovieService>();

			var dto = JsonSerializer.Deserialize<BatchMoviesRequestDto>(payloadJson) ?? new BatchMoviesRequestDto();

			await RunBatchJobAsync(backgroundJobQueueService, backgroundJobId,
				onProgress => movieService.BatchAddMoviesAsync(dto, onProgress));
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, ex.InnerException?.Message ?? ex.Message);
			await MarkJobFailedSafeAsync(backgroundJobQueueService, backgroundJobId, ex.InnerException?.Message ?? ex.Message);
		}
		finally
		{
			_processingJobIds.Remove(backgroundJobId);
			_semaphore.Release();
		}
	}

	private async Task ProcessBatchEpisodeLinksAsync(IBackgroundJobQueueService backgroundJobQueueService, string backgroundJobId, string payloadJson)
	{
		try
		{
			using var scope = _serviceProvider.CreateScope();
			var seriesService = scope.ServiceProvider.GetRequiredService<ISeriesService>();

			var dto = JsonSerializer.Deserialize<BatchEpisodeLinksRequestDto>(payloadJson) ?? new BatchEpisodeLinksRequestDto();

			await RunBatchJobAsync(backgroundJobQueueService, backgroundJobId,
				onProgress => seriesService.BatchAddEpisodeLinksAsync(dto, onProgress));
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, ex.InnerException?.Message ?? ex.Message);
			await MarkJobFailedSafeAsync(backgroundJobQueueService, backgroundJobId, ex.InnerException?.Message ?? ex.Message);
		}
		finally
		{
			_processingJobIds.Remove(backgroundJobId);
			_semaphore.Release();
		}
	}

	// Executa uma operacao em lote reportando o progresso para o job na fila.
	// A atualizacao do progresso e limitada a cada mudanca de porcentagem para nao
	// sobrecarregar o banco em lotes grandes.
	private static async Task RunBatchJobAsync(
		IBackgroundJobQueueService backgroundJobQueueService,
		string backgroundJobId,
		Func<Func<BatchProgressReport, Task>, Task<Result<int>>> runBatch)
	{
		int lastPct = -1;
		BatchProgressReport last = default;

		Func<BatchProgressReport, Task> onProgress = async report =>
		{
			last = report;
			int pct = report.Total > 0 ? (int)(report.Processed * 100L / report.Total) : 100;
			if (pct == lastPct) return;
			lastPct = pct;

			await backgroundJobQueueService.UpdateAsync(new UpdateBackgroundJobRequestDto
			{
				Id = backgroundJobId,
				TotalRecordsToProcess = report.Total,
				TotalProcessedRecords = report.Processed,
				TotalSuccessfulRecords = report.Successful,
				TotalFailedRecords = report.Processed - report.Successful,
				Status = EBackgroundJobStatus.PROCESSING
			});
		};

		var result = await runBatch(onProgress);

		UpdateBackgroundJobRequestDto finalDto;

		if (result.IsSuccess)
		{
			int total = last.Total;
			int success = result.Data;

			finalDto = new UpdateBackgroundJobRequestDto
			{
				Id = backgroundJobId,
				TotalRecordsToProcess = total,
				TotalProcessedRecords = total,
				TotalSuccessfulRecords = success,
				TotalFailedRecords = total - success < 0 ? 0 : total - success,
				Status = EBackgroundJobStatus.COMPLETED
			};
		}
		else
		{
			finalDto = new UpdateBackgroundJobRequestDto
			{
				Id = backgroundJobId,
				TotalRecordsToProcess = last.Total,
				TotalProcessedRecords = last.Processed,
				TotalSuccessfulRecords = last.Successful,
				TotalFailedRecords = last.Processed - last.Successful,
				ErrorList = [result.Error?.Description ?? "Falha no processamento em lote"],
				Status = EBackgroundJobStatus.FAILED
			};
		}

		await backgroundJobQueueService.UpdateAsync(finalDto);
	}

	private async Task MarkJobFailedSafeAsync(IBackgroundJobQueueService backgroundJobQueueService, string backgroundJobId, string errorMessage)
	{
		try
		{
			await backgroundJobQueueService.UpdateAsync(new UpdateBackgroundJobRequestDto
			{
				Id = backgroundJobId,
				Status = EBackgroundJobStatus.FAILED,
				ErrorList = [errorMessage]
			});
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, ex.InnerException?.Message ?? ex.Message);
		}
	}

	private static async Task<IFormFile> DownloadExcelAsFormFileAsync(string fileUrl, string fileName = "_arquivo.xlsx")
	{
		using var httpClient = new HttpClient()
		{
			Timeout = TimeSpan.FromSeconds(30)
		};

		var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);

		var stream = new MemoryStream(fileBytes);
		var formFile = new FormFile(stream, 0, stream.Length, "file", fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
		};

		return formFile;
	}
}