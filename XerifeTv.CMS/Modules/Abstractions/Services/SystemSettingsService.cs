using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.User.Enums;
using XerifeTv.CMS.Modules.User.Interfaces;

namespace XerifeTv.CMS.Modules.Abstractions.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private static readonly string SettingsFileName = "system_settings.json";
    private static string SettingsFilePath =>
        File.Exists(Path.Combine(Directory.GetCurrentDirectory(), SettingsFileName))
            ? Path.Combine(Directory.GetCurrentDirectory(), SettingsFileName)
            : Path.Combine(AppContext.BaseDirectory, SettingsFileName);

    private static bool _enableMoviesSpreadsheetImport = true;
    private static bool _enableSeriesSpreadsheetImport = true;
    private static bool _enableChannelsSpreadsheetImport = true;
    private static EImdbSearchMode _defaultImdbSearchMode = EImdbSearchMode.IMDB_ID;
    private static readonly object _lock = new();

    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IServiceProvider? _serviceProvider;

    public SystemSettingsService(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;

        lock (_lock)
        {
            LoadSettingsFromDisk();
        }
    }

    public SystemSettingsService()
    {
        lock (_lock)
        {
            LoadSettingsFromDisk();
        }
    }

    private static void LoadSettingsFromDisk()
    {
        try
        {
            var filePath = SettingsFilePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("EnableMoviesSpreadsheetImport", out var p1))
                    _enableMoviesSpreadsheetImport = p1.GetBoolean();

                if (doc.RootElement.TryGetProperty("EnableSeriesSpreadsheetImport", out var p2))
                    _enableSeriesSpreadsheetImport = p2.GetBoolean();

                if (doc.RootElement.TryGetProperty("EnableChannelsSpreadsheetImport", out var p3))
                    _enableChannelsSpreadsheetImport = p3.GetBoolean();

                if (doc.RootElement.TryGetProperty("ImdbSearchMode", out var p4) &&
                    Enum.TryParse<EImdbSearchMode>(p4.GetString(), out var parsedImdbSearchMode))
                    _defaultImdbSearchMode = parsedImdbSearchMode;
            }
            else
            {
                SaveSettingsToDisk(_enableMoviesSpreadsheetImport, _enableSeriesSpreadsheetImport, _enableChannelsSpreadsheetImport, _defaultImdbSearchMode);
            }
        }
        catch { }
    }

    private static void SaveSettingsToDisk(bool movies, bool series, bool channels, EImdbSearchMode imdbSearchMode)
    {
        try
        {
            var dto = new
            {
                EnableMoviesSpreadsheetImport = movies,
                EnableSeriesSpreadsheetImport = series,
                EnableChannelsSpreadsheetImport = channels,
                ImdbSearchMode = imdbSearchMode.ToString()
            };
            var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var targetPath = Path.Combine(Directory.GetCurrentDirectory(), SettingsFileName);
            File.WriteAllText(targetPath, json);
        }
        catch { }
    }

    private User.UserEntity? GetCurrentLoggedInUser()
    {
        try
        {
            var username = _httpContextAccessor?.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username) || _serviceProvider == null)
                return null;

            using var scope = _serviceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetService<IUserRepository>();
            if (repo == null) return null;

            return repo.GetByUsernameAsync(username).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    public bool IsMoviesSpreadsheetImportEnabled()
    {
        var user = GetCurrentLoggedInUser();
        return user != null ? user.EnableMoviesSpreadsheetImport : _enableMoviesSpreadsheetImport;
    }

    public bool IsSeriesSpreadsheetImportEnabled()
    {
        var user = GetCurrentLoggedInUser();
        return user != null ? user.EnableSeriesSpreadsheetImport : _enableSeriesSpreadsheetImport;
    }

    public bool IsChannelsSpreadsheetImportEnabled()
    {
        var user = GetCurrentLoggedInUser();
        return user != null ? user.EnableChannelsSpreadsheetImport : _enableChannelsSpreadsheetImport;
    }

    public void SetSpreadsheetImportSettings(bool movies, bool series, bool channels)
    {
        lock (_lock)
        {
            _enableMoviesSpreadsheetImport = movies;
            _enableSeriesSpreadsheetImport = series;
            _enableChannelsSpreadsheetImport = channels;
            SaveSettingsToDisk(movies, series, channels, _defaultImdbSearchMode);
        }
    }

    public EImdbSearchMode GetDefaultImdbSearchMode()
    {
        var user = GetCurrentLoggedInUser();
        return user != null ? user.ImdbSearchMode : _defaultImdbSearchMode;
    }

    public void SetImdbSearchMode(EImdbSearchMode mode)
    {
        lock (_lock)
        {
            _defaultImdbSearchMode = mode;
            SaveSettingsToDisk(_enableMoviesSpreadsheetImport, _enableSeriesSpreadsheetImport, _enableChannelsSpreadsheetImport, mode);
        }
    }
}
