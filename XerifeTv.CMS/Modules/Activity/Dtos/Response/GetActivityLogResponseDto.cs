namespace XerifeTv.CMS.Modules.Activity.Dtos.Response;

public class GetActivityLogResponseDto
{
    public string UserName { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public static GetActivityLogResponseDto FromEntity(ActivityLogEntity entity)
    {
        return new GetActivityLogResponseDto
        {
            UserName = entity.UserName,
            Category = entity.Category,
            Action = entity.Action,
            Description = entity.Description,
            CreatedAt = entity.CreateAt
        };
    }
}
