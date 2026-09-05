using XerifeTv.CMS.Modules.Movie.Interfaces;
using XerifeTv.CMS.Modules.Series.Interfaces;
using XerifeTv.CMS.Modules.SiteProfile;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;

namespace XerifeTv.CMS.Modules.SiteReview;

public interface ISiteReviewService
{
    Task<ProfilePage<SiteReviewResponse>> GetByContentAsync(string movieId, int page, int pageSize, string contentType = "movie");
    Task<ProfilePage<SiteReviewResponse>> GetByUserAsync(string userId, int page, int pageSize, string contentType = "movie");
    Task<SiteReviewResponse?> GetOwnAsync(string userId, string movieId, string contentType = "movie");
    Task<SiteReviewResponse> SaveAsync(string userId, string movieId, SaveSiteReviewRequest request, string contentType = "movie");
    Task DeleteAsync(string userId, string movieId, string contentType = "movie");
}

public sealed class SiteReviewService(ISiteReviewRepository reviews, ISiteUserRepository users,
    IMovieRepository movies, ISeriesRepository series) : ISiteReviewService
{
    public async Task<ProfilePage<SiteReviewResponse>> GetByContentAsync(string movieId, int page, int pageSize, string contentType = "movie")
    {
        await RequireContentAsync(movieId, contentType);
        return await ToPageAsync(await reviews.GetByContentAsync(movieId, page, pageSize, contentType), page, pageSize);
    }

    public async Task<ProfilePage<SiteReviewResponse>> GetByUserAsync(string userId, int page, int pageSize, string contentType = "movie")
        => await ToPageAsync(await reviews.GetByUserAsync(userId, page, pageSize, contentType), page, pageSize);

    public async Task<SiteReviewResponse?> GetOwnAsync(string userId, string movieId, string contentType = "movie")
    {
        var review = await reviews.GetOwnAsync(userId, movieId, contentType);
        return review is null ? null : await ToResponseAsync(review);
    }

    public async Task<SiteReviewResponse> SaveAsync(string userId, string movieId, SaveSiteReviewRequest request, string contentType = "movie")
    {
        if (request.Rating is < 1 or > 5)
            throw new ArgumentException("A nota deve ser de 1 a 5.");
        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length is < 1 or > 2000)
            throw new ArgumentException("Escreva uma avaliação com até 2000 caracteres.");
        var movie = await RequireContentAsync(movieId, contentType);
        var review = await reviews.SaveAsync(new SiteReviewEntity
        {
            SiteUserId = userId, MovieId = movie.Id, Title = movie.Title, Poster = movie.Poster, ContentType = contentType,
            Rating = request.Rating, Text = text
        });
        return await ToResponseAsync(review);
    }

    public Task DeleteAsync(string userId, string movieId, string contentType = "movie") => reviews.DeleteAsync(userId, movieId, contentType);

    private async Task<ProfileMovieResponse> RequireContentAsync(string contentId, string contentType)
    {
        var content = contentType == "series"
            ? ProfileMovieResponse.FromSeries(await series.GetAsync(contentId), contentId, "", "")
            : ProfileMovieResponse.FromEntity(await movies.GetAsync(contentId), contentId, "", "");
        return content.IsAvailable ? content
            : throw new KeyNotFoundException(contentType == "series" ? "Série não encontrada." : "Filme não encontrado.");
    }

    private async Task<ProfilePage<SiteReviewResponse>> ToPageAsync(List<SiteReviewEntity> entries, int page, int pageSize)
        => new(await Task.WhenAll(entries.Take(pageSize).Select(ToResponseAsync)), page, pageSize, entries.Count > pageSize);

    private async Task<SiteReviewResponse> ToResponseAsync(SiteReviewEntity review)
    {
        var author = await users.GetAsync(review.SiteUserId);
        var movie = review.ContentType == "series"
            ? ProfileMovieResponse.FromSeries(await series.GetAsync(review.MovieId), review.MovieId, review.Title, review.Poster)
            : ProfileMovieResponse.FromEntity(await movies.GetAsync(review.MovieId), review.MovieId, review.Title, review.Poster);
        return new(review.Id, movie,
            new(review.SiteUserId, author?.Name ?? "Usuário removido", author?.AvatarUrl, author?.AvatarGiphyId),
            review.Rating, review.Text, review.CreateAt, review.UpdateAt);
    }
}
