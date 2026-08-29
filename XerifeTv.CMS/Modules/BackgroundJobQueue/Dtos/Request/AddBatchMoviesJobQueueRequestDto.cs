using XerifeTv.CMS.Modules.Movie.Dtos.Request;

namespace XerifeTv.CMS.Modules.BackgroundJobQueue.Dtos.Request;

public class AddBatchMoviesJobQueueRequestDto
{
    public string RequestedByUsername { get; set; } = string.Empty;
    public BatchMoviesRequestDto Payload { get; init; } = new();
}
