using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.Content.Cache;

// Cadastro feito em outra instância (ex.: CMS desktop) muda a versão no Mongo: aqui ela é conferida e o cache local de conteúdo é descartado
public sealed class ContentCacheSyncWorker(
    ICacheService _cacheService,
    IContentCacheVersionRepository _versionRepository,
    ILogger<ContentCacheSyncWorker> _logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long? lastVersion = null;
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            try
            {
                var version = await _versionRepository.GetVersionAsync();
                if (lastVersion is not null && version != lastVersion)
                    _cacheService.ClearContent();
                lastVersion = version;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao conferir a versão do cache de conteúdo");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
