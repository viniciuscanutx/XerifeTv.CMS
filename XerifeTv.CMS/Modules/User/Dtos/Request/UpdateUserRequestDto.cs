using XerifeTv.CMS.Modules.User.Enums;

namespace XerifeTv.CMS.Modules.User.Dtos.Request;

public class UpdateUserRequestDto
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public EUserRole? Role { get; init; }
    public bool? Blocked { get; set; } = false;
    public bool? EnableMoviesSpreadsheetImport { get; set; }
    public bool? EnableSeriesSpreadsheetImport { get; set; }
    public bool? EnableChannelsSpreadsheetImport { get; set; }
    public EImdbSearchMode? ImdbSearchMode { get; set; }

    public UserEntity ToEntity()
    {
        return new UserEntity
        {
            Id = Id,
            UserName = UserName,
            Email = Email,
            Role = Role ?? EUserRole.VISITOR,
            Blocked = Blocked ?? false,
            EnableMoviesSpreadsheetImport = EnableMoviesSpreadsheetImport ?? true,
            EnableSeriesSpreadsheetImport = EnableSeriesSpreadsheetImport ?? true,
            EnableChannelsSpreadsheetImport = EnableChannelsSpreadsheetImport ?? true,
            ImdbSearchMode = ImdbSearchMode ?? EImdbSearchMode.IMDB_ID
        };
    }
}