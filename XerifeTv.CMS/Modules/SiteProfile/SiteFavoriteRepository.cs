using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SiteProfile;

public interface ISiteFavoriteRepository
{
    Task<List<SiteFavoriteEntity>> GetAsync(string userId, int page, int pageSize, string contentType = "movie");
    Task<bool> ContainsAsync(string userId, string movieId, string contentType = "movie");
    Task AddAsync(SiteFavoriteEntity entity);
    Task DeleteAsync(string userId, string movieId, string contentType = "movie");
}

public sealed class SiteFavoriteRepository(IOptions<DBSettings> options)
    : BaseRepository<SiteFavoriteEntity>(ECollection.SITE_FAVORITES, options), ISiteFavoriteRepository
{
    public Task<List<SiteFavoriteEntity>> GetAsync(string userId, int page, int pageSize, string contentType = "movie")
        => _collection.Find(Builders<SiteFavoriteEntity>.Filter.Eq(x => x.SiteUserId, userId) & TypeFilter(contentType))
            .SortByDescending(x => x.CreateAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Limit(pageSize + 1).ToListAsync();

    public Task<bool> ContainsAsync(string userId, string movieId, string contentType = "movie")
        => _collection.Find(EntryFilter(userId, movieId, contentType)).AnyAsync();

    public async Task AddAsync(SiteFavoriteEntity entity)
    {
        entity.Id = entity.ContentType == "series" ? $"series:{entity.SiteUserId}:{entity.MovieId}" : $"{entity.SiteUserId}:{entity.MovieId}";
        try
        {
            await _collection.InsertOneAsync(entity);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
        }
    }

    public Task DeleteAsync(string userId, string movieId, string contentType = "movie")
        => _collection.DeleteOneAsync(EntryFilter(userId, movieId, contentType));

    private static FilterDefinition<SiteFavoriteEntity> TypeFilter(string contentType)
        => contentType == "series"
            ? Builders<SiteFavoriteEntity>.Filter.Eq(x => x.ContentType, "series")
            : Builders<SiteFavoriteEntity>.Filter.Ne(x => x.ContentType, "series");

    private static FilterDefinition<SiteFavoriteEntity> EntryFilter(string userId, string contentId, string contentType)
        => Builders<SiteFavoriteEntity>.Filter.Eq(x => x.SiteUserId, userId)
            & Builders<SiteFavoriteEntity>.Filter.Eq(x => x.MovieId, contentId) & TypeFilter(contentType);
}
