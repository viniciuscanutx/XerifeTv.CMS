namespace XerifeTv.CMS.Modules.SiteWatchProgress.Dtos.Response;

public class GetWatchProgressResponseDto
{
    public string ContentId { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Poster { get; private set; } = string.Empty;
    public string? Backdrop { get; private set; }
    public double CurrentTime { get; private set; }
    public double Duration { get; private set; }
    public int ProgressPercentage { get; private set; }
    public long UpdatedAt { get; private set; }

    public static GetWatchProgressResponseDto FromEntity(WatchProgressEntity entity)
    {
        return new GetWatchProgressResponseDto
        {
            ContentId = entity.ContentId,
            Type = entity.Type,
            Title = entity.Title,
            Poster = entity.Poster,
            Backdrop = entity.Backdrop,
            CurrentTime = entity.CurrentTime,
            Duration = entity.Duration,
            ProgressPercentage = entity.ProgressPercentage,
            UpdatedAt = new DateTimeOffset(entity.UpdateAt ?? entity.CreateAt, TimeSpan.Zero).ToUnixTimeMilliseconds()
        };
    }
}
