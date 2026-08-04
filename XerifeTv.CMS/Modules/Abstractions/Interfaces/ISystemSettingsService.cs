namespace XerifeTv.CMS.Modules.Abstractions.Interfaces;

public interface ISystemSettingsService
{
    bool IsMoviesSpreadsheetImportEnabled();
    bool IsSeriesSpreadsheetImportEnabled();
    bool IsChannelsSpreadsheetImportEnabled();
    void SetSpreadsheetImportSettings(bool movies, bool series, bool channels);
}
