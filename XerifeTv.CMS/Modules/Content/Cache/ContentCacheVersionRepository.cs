using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Entities;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.Content.Cache;

// Versão do catálogo compartilhada pelas instâncias do CMS (Render e app desktop usam o mesmo Mongo)
public class ContentCacheVersionEntity : BaseEntity
{
    public long Version { get; set; }
}

public interface IContentCacheVersionRepository
{
    Task<long> GetVersionAsync();
    Task IncrementAsync();
}

public sealed class ContentCacheVersionRepository(IOptions<DBSettings> options)
    : BaseRepository<ContentCacheVersionEntity>(ECollection.CONTENT_CACHE_VERSION, options), IContentCacheVersionRepository
{
    private const string DocumentId = "content";

    public async Task<long> GetVersionAsync()
    {
        var document = await _collection.Find(r => r.Id == DocumentId).FirstOrDefaultAsync();
        return document?.Version ?? 0;
    }

    public Task IncrementAsync() =>
        _collection.UpdateOneAsync(
            r => r.Id == DocumentId,
            Builders<ContentCacheVersionEntity>.Update
                .Inc(r => r.Version, 1)
                .Set(r => r.UpdateAt, DateTime.UtcNow),
            new UpdateOptions { IsUpsert = true });
}
