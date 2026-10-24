namespace XerifeTv.CMS.Modules.Abstractions.Interfaces;

public interface ICacheService
{
    T? GetValue<T>(string key);
    void SetValue<T>(string key, T value);
    void SetContentValue<T>(string key, T value);
    void ClearContent();
    Task InvalidateContentAsync();
    void Remove(string key);
    void Clear();
}