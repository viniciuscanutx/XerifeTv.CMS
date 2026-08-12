using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.SystemSettings;
using XerifeTv.CMS.Modules.SystemSettings.Interfaces;
using XerifeTv.CMS.Modules.User.Enums;
using XerifeTv.CMS.Modules.User.Interfaces;

namespace XerifeTv.CMS.Modules.Abstractions.Services;

public class SystemSettingsService(IHttpContextAccessor _httpContextAccessor, IServiceProvider _serviceProvider) : ISystemSettingsService
{
    private User.UserEntity? GetCurrentLoggedInUser()
    {
        try
        {
            var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
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

    private SystemSettingsEntity GetOrCreateSystemDoc()
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISystemSettingsRepository>();

        var existing = repo.GetAsync(SystemSettingsEntity.SingletonId).GetAwaiter().GetResult();
        if (existing != null) return existing;

        var defaultSettings = new SystemSettingsEntity { Id = SystemSettingsEntity.SingletonId };
        repo.CreateAsync(defaultSettings).GetAwaiter().GetResult();
        return defaultSettings;
    }

    private void UpdateSystemDoc(Action<SystemSettingsEntity> mutate)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISystemSettingsRepository>();

        var existing = repo.GetAsync(SystemSettingsEntity.SingletonId).GetAwaiter().GetResult();

        if (existing is null)
        {
            var newSettings = new SystemSettingsEntity { Id = SystemSettingsEntity.SingletonId };
            mutate(newSettings);
            repo.CreateAsync(newSettings).GetAwaiter().GetResult();
            return;
        }

        mutate(existing);
        repo.UpdateAsync(existing).GetAwaiter().GetResult();
    }

    public bool IsMoviesSpreadsheetImportEnabled()
    {
        var user = GetCurrentLoggedInUser();
        return user != null ? user.EnableMoviesSpreadsheetImport : GetOrCreateSystemDoc().EnableMoviesSpreadsheetImport;
    }

    public bool IsSeriesSpreadsheetImportEnabled()
    {
        var user = GetCurrentLoggedInUser();
        return user != null ? user.EnableSeriesSpreadsheetImport : GetOrCreateSystemDoc().EnableSeriesSpreadsheetImport;
    }

    public bool IsChannelsSpreadsheetImportEnabled()
    {
        var user = GetCurrentLoggedInUser();
        return user != null ? user.EnableChannelsSpreadsheetImport : GetOrCreateSystemDoc().EnableChannelsSpreadsheetImport;
    }

    public void SetSpreadsheetImportSettings(bool movies, bool series, bool channels)
    {
        UpdateSystemDoc(settings =>
        {
            settings.EnableMoviesSpreadsheetImport = movies;
            settings.EnableSeriesSpreadsheetImport = series;
            settings.EnableChannelsSpreadsheetImport = channels;
        });
    }

    public EImdbSearchMode GetDefaultImdbSearchMode()
    {
        var user = GetCurrentLoggedInUser();
        return user != null ? user.ImdbSearchMode : GetOrCreateSystemDoc().ImdbSearchMode;
    }

    public void SetImdbSearchMode(EImdbSearchMode mode)
    {
        UpdateSystemDoc(settings => settings.ImdbSearchMode = mode);
    }
}
