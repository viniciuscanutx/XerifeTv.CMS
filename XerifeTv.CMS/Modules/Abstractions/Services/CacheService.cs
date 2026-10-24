using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.Abstractions.Services;

public sealed class CacheService(IMemoryCache _cache) : ICacheService
{
    private readonly MemoryCacheEntryOptions _cacheOptions = new MemoryCacheEntryOptions()
      .SetSlidingExpiration(TimeSpan.FromMinutes(10))
      .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
      .SetPriority(CacheItemPriority.Normal);

    public T? GetValue<T>(string key)
    {
        _cache.TryGetValue(key, out T? result);
        return result;
    }

    // Respostas da API de conteúdo ficam presas a este token; cancelar derruba só elas
    private CancellationTokenSource _contentToken = new();

    public void SetValue<T>(string key, T value)
      => _cache.Set(key, value, _cacheOptions);

    public void SetContentValue<T>(string key, T value)
    {
        var options = new MemoryCacheEntryOptions()
          .SetSlidingExpiration(TimeSpan.FromMinutes(10))
          .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
          .SetPriority(CacheItemPriority.Normal)
          .AddExpirationToken(new CancellationChangeToken(_contentToken.Token));

        _cache.Set(key, value, options);
    }

    // Não mexe no progresso de importação nem nos links resolvidos
    public void ClearContent()
      => Interlocked.Exchange(ref _contentToken, new CancellationTokenSource()).Cancel();

    public void Remove(string key)
      => _cache.Remove(key);

    public void Clear()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Clear();
        }
    }
}