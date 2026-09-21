namespace XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;

public record GetResolveUrlResponseDto(string Url, string StreamFormat)
{
    public IReadOnlyCollection<GetResolveUrlSourceResponseDto> Sources { get; init; } = [];
}

public record GetResolveUrlSourceResponseDto(string Url, string StreamFormat, string Quality);
