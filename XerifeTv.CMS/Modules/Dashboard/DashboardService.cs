using XerifeTv.CMS.Modules.BackgroundJobQueue.Dtos.Request;
using XerifeTv.CMS.Modules.BackgroundJobQueue.Enums;
using XerifeTv.CMS.Modules.BackgroundJobQueue.Interfaces;
using XerifeTv.CMS.Modules.Channel.Interfaces;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Common.Dtos;
using XerifeTv.CMS.Modules.Dashboard.Dtos.Response;
using XerifeTv.CMS.Modules.Dashboard.Interfaces;
using XerifeTv.CMS.Modules.Movie.Interfaces;
using XerifeTv.CMS.Modules.Series.Interfaces;

namespace XerifeTv.CMS.Modules.Dashboard;

public sealed class DashboardService(
  IMovieRepository _movieRepository,
  ISeriesRepository _seriesRepository,
  IChannelRepository _channelRepository,
  IBackgroundJobQueueRepository _backgroundJobQueueRepository) : IDashboardService
{
    private const int RecentItemsLimit = 8;
    private const int TopCategoriesLimit = 5;
    private const int RecentJobsLimit = 5;

    public async Task<Result<GetDashboardDataRequestDto>> GetAsync()
    {
        var moviesCountTask = _movieRepository.CountAsync();
        var seriesCountTask = _seriesRepository.CountAsync();
        var channelsCountTask = _channelRepository.CountAsync();
        var recentMoviesTask = _movieRepository.GetAsync(1, RecentItemsLimit);
        var recentSeriesTask = _seriesRepository.GetAsync(1, RecentItemsLimit);
        var movieCategoriesTask = _movieRepository.GetCategoriesWithCountAsync();
        var seriesCategoriesTask = _seriesRepository.GetCategoriesWithCountAsync();
        var recentJobsTask = _backgroundJobQueueRepository.GetByFilterAsync(
          new GetBackgroundJobsByFilterRequestDto(EBackgroundJobOrderFilter.REGISTRATION_DATE_DESC, RecentJobsLimit, 1));

        await Task.WhenAll(
          moviesCountTask, seriesCountTask, channelsCountTask,
          recentMoviesTask, recentSeriesTask,
          movieCategoriesTask, seriesCategoriesTask,
          recentJobsTask);

        var recentItems = recentMoviesTask.Result.Items
          .Select(m => new DashboardRecentItemDto(m.Id, m.Title, m.PosterUrl, m.ReleaseYear, m.Review, m.CreateAt, "movie"))
          .Concat(recentSeriesTask.Result.Items
            .Select(s => new DashboardRecentItemDto(s.Id, s.Title, s.PosterUrl, s.ReleaseYear, s.Review, s.CreateAt, "series")))
          .OrderByDescending(i => i.CreateAt)
          .Take(RecentItemsLimit)
          .ToList();

        var topCategories = movieCategoriesTask.Result
          .Concat(seriesCategoriesTask.Result)
          .GroupBy(c => c.Category)
          .Select(g => new CategoryCountDto { Category = g.Key, Count = g.Sum(c => c.Count) })
          .OrderByDescending(c => c.Count)
          .Take(TopCategoriesLimit)
          .ToList();

        var recentJobs = recentJobsTask.Result.Items
          .Select(j => new DashboardRecentJobDto(j.Id, j.JobName, j.Status.ToString(), j.TotalRecordsToProcess, j.TotalProcessedRecords, j.CreateAt))
          .ToList();

        return Result<GetDashboardDataRequestDto>.Success(
          new GetDashboardDataRequestDto(
            moviesCountTask.Result,
            seriesCountTask.Result,
            channelsCountTask.Result,
            recentItems,
            topCategories,
            recentJobs));
    }
}
