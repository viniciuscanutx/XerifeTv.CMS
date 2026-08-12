using XerifeTv.CMS.Modules.Abstractions.Entities;
using XerifeTv.CMS.Modules.User.Enums;

namespace XerifeTv.CMS.Modules.SystemSettings;

public class SystemSettingsEntity : BaseEntity
{
    public const string SingletonId = "system-settings";

    public bool EnableMoviesSpreadsheetImport { get; set; } = true;
    public bool EnableSeriesSpreadsheetImport { get; set; } = true;
    public bool EnableChannelsSpreadsheetImport { get; set; } = true;
    public EImdbSearchMode ImdbSearchMode { get; set; } = EImdbSearchMode.IMDB_ID;
}
