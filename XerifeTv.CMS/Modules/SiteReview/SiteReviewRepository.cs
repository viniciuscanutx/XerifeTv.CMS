using Microsoft.Extensions.Options;
using MongoDB.Driver;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SiteReview;

public interface ISiteReviewRepository
{
    Task<List<SiteReviewEntity>> GetByContentAsync(string movieId, int page, int pageSize, string contentType = "movie");
    Task<List<SiteReviewEntity>> GetByUserAsync(string userId, int page, int pageSize, string contentType = "movie");
    Task<SiteReviewEntity?> GetOwnAsync(string userId, string movieId, string contentType = "movie");
    Task<SiteReviewEntity> SaveAsync(SiteReviewEntity entity);
    Task DeleteAsync(string userId, string movieId, string contentType = "movie");
}

public sealed class SiteReviewRepository(IOptions<DBSettings> options)
    : BaseRepository<SiteReviewEntity>(ECollection.SITE_REVIEWS, options), ISiteReviewRepository
{
    private Task<List<SiteReviewEntity>> GetPageAsync(FilterDefinition<SiteReviewEntity> filter, int page, int pageSize)
        => _collection.Find(filter).SortByDescending(x => x.UpdateAt).ThenByDescending(x => x.CreateAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Limit(pageSize + 1).ToListAsync();

    public Task<List<SiteReviewEntity>> GetByContentAsync(string movieId, int page, int pageSize, string contentType = "movie")
        => GetPageAsync(Builders<SiteReviewEntity>.Filter.Eq(x => x.MovieId, movieId) & TypeFilter(contentType), page, pageSize);

    public Task<List<SiteReviewEntity>> GetByUserAsync(string userId, int page, int pageSize, string contentType = "movie")
        => GetPageAsync(Builders<SiteReviewEntity>.Filter.Eq(x => x.SiteUserId, userId)
            & (contentType == "all" ? Builders<SiteReviewEntity>.Filter.Empty : TypeFilter(contentType)), page, pageSize);

    public async Task<SiteReviewEntity?> GetOwnAsync(string userId, string movieId, string contentType = "movie")
        => await _collection.Find(EntryFilter(userId, movieId, contentType)).FirstOrDefaultAsync();

    public async Task<SiteReviewEntity> SaveAsync(SiteReviewEntity entity)
    {
        var id = entity.ContentType == "series" ? $"series:{entity.SiteUserId}:{entity.MovieId}" : $"{entity.SiteUserId}:{entity.MovieId}";
        var update = Builders<SiteReviewEntity>.Update
            .SetOnInsert(x => x.Id, id).SetOnInsert(x => x.CreateAt, entity.CreateAt)
            .Set(x => x.SiteUserId, entity.SiteUserId).Set(x => x.MovieId, entity.MovieId)
            .Set(x => x.ContentType, entity.ContentType)
            .Set(x => x.Title, entity.Title).Set(x => x.Poster, entity.Poster)
            .Set(x => x.Rating, entity.Rating).Set(x => x.Text, entity.Text).Set(x => x.UpdateAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<SiteReviewEntity> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
        try
        {
            return await _collection.FindOneAndUpdateAsync(Builders<SiteReviewEntity>.Filter.Eq(x => x.Id, id), update, options);
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            options.IsUpsert = false;
            return await _collection.FindOneAndUpdateAsync(Builders<SiteReviewEntity>.Filter.Eq(x => x.Id, id), update, options);
        }
    }

    public Task DeleteAsync(string userId, string movieId, string contentType = "movie")
        => _collection.DeleteOneAsync(EntryFilter(userId, movieId, contentType));

    private static FilterDefinition<SiteReviewEntity> TypeFilter(string contentType)
        => contentType == "series"
            ? Builders<SiteReviewEntity>.Filter.Eq(x => x.ContentType, "series")
            : Builders<SiteReviewEntity>.Filter.Ne(x => x.ContentType, "series");

    private static FilterDefinition<SiteReviewEntity> EntryFilter(string userId, string contentId, string contentType)
        => Builders<SiteReviewEntity>.Filter.Eq(x => x.SiteUserId, userId)
            & Builders<SiteReviewEntity>.Filter.Eq(x => x.MovieId, contentId) & TypeFilter(contentType);
}
