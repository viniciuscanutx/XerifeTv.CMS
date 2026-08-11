using XerifeTv.CMS.Modules.User.Enums;

namespace XerifeTv.CMS.Modules.User.Dtos.Response;

public class GetUserResponseDto
{
    public string Id { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public EUserRole Role { get; private set; } = EUserRole.COMMON;
    public string RoleName => GetRoleName(Role);
    public bool Blocked { get; private set; } = false;
    public int FailedLoginAttempts { get; private set; }
    public bool EnableMoviesSpreadsheetImport { get; private set; } = true;
    public bool EnableSeriesSpreadsheetImport { get; private set; } = true;
    public bool EnableChannelsSpreadsheetImport { get; private set; } = true;
    public EImdbSearchMode ImdbSearchMode { get; private set; } = EImdbSearchMode.IMDB_ID;

    public static GetUserResponseDto FromEntity(UserEntity entity)
    {
        return new GetUserResponseDto
        {
            Id = entity.Id,
            UserName = entity.UserName,
            Email = entity.Email,
            Role = entity.Role,
			Blocked = entity.Blocked,
            FailedLoginAttempts = entity.FailedLoginAttempts,
            EnableMoviesSpreadsheetImport = entity.EnableMoviesSpreadsheetImport,
            EnableSeriesSpreadsheetImport = entity.EnableSeriesSpreadsheetImport,
            EnableChannelsSpreadsheetImport = entity.EnableChannelsSpreadsheetImport,
            ImdbSearchMode = entity.ImdbSearchMode
		};
    }

    private static string GetRoleName(EUserRole role)
      => role switch
      {
          EUserRole.ADMIN => "Administrador",
          EUserRole.COMMON => "Moderador",
          _ => "Visitante"
      };
}
