using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.Abstractions.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "system_settings.json");
    private static bool _enableMoviesSpreadsheetImport = true;
    private static bool _enableSeriesSpreadsheetImport = true;
    private static bool _enableChannelsSpreadsheetImport = true;
    private static readonly object _lock = new();

    static SystemSettingsService()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("EnableMoviesSpreadsheetImport", out var p1))
                    _enableMoviesSpreadsheetImport = p1.GetBoolean();

                if (doc.RootElement.TryGetProperty("EnableSeriesSpreadsheetImport", out var p2))
                    _enableSeriesSpreadsheetImport = p2.GetBoolean();

                if (doc.RootElement.TryGetProperty("EnableChannelsSpreadsheetImport", out var p3))
                    _enableChannelsSpreadsheetImport = p3.GetBoolean();
            }
        }
        catch { }
    }

    public bool IsMoviesSpreadsheetImportEnabled() => _enableMoviesSpreadsheetImport;
    public bool IsSeriesSpreadsheetImportEnabled() => _enableSeriesSpreadsheetImport;
    public bool IsChannelsSpreadsheetImportEnabled() => _enableChannelsSpreadsheetImport;

    public void SetSpreadsheetImportSettings(bool movies, bool series, bool channels)
    {
        lock (_lock)
        {
            _enableMoviesSpreadsheetImport = movies;
            _enableSeriesSpreadsheetImport = series;
            _enableChannelsSpreadsheetImport = channels;
            try
            {
                var dto = new
                {
                    EnableMoviesSpreadsheetImport = movies,
                    EnableSeriesSpreadsheetImport = series,
                    EnableChannelsSpreadsheetImport = channels
                };
                var json = System.Text.Json.JsonSerializer.Serialize(dto);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}
