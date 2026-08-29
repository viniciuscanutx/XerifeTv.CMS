using XerifeTv.CMS.Modules.Series.Dtos.Request;

namespace XerifeTv.CMS.Modules.BackgroundJobQueue.Dtos.Request;

public class AddBatchEpisodeLinksJobQueueRequestDto
{
    public string RequestedByUsername { get; set; } = string.Empty;
    public string SerieTitle { get; init; } = string.Empty;
    public BatchEpisodeLinksRequestDto Payload { get; init; } = new();
}
