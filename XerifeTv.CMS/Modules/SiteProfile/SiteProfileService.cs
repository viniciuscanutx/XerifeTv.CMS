using XerifeTv.CMS.Modules.Movie.Interfaces;
using XerifeTv.CMS.Modules.Series.Interfaces;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;

namespace XerifeTv.CMS.Modules.SiteProfile;

public interface ISiteProfileService
{
    Task<SiteProfileResponse> GetAsync(string userId);
    Task<SiteProfileResponse> UpdateAsync(string userId, UpdateSiteProfileRequest request);
    Task<ProfilePage<FavoriteResponse>> GetFavoritesAsync(string userId, int page, int pageSize, string contentType = "movie");
    Task<bool> IsFavoriteAsync(string userId, string movieId, string contentType = "movie");
    Task AddFavoriteAsync(string userId, string movieId, string contentType = "movie");
    Task DeleteFavoriteAsync(string userId, string movieId, string contentType = "movie");
}

public sealed class SiteProfileService(
    ISiteUserRepository users,
    ISiteFavoriteRepository favorites,
    IMovieRepository movies, ISeriesRepository series) : ISiteProfileService
{
    public async Task<SiteProfileResponse> GetAsync(string userId)
        => SiteProfileResponse.FromEntity(await users.GetAsync(userId)
            ?? throw new KeyNotFoundException("Perfil não encontrado."));

    public async Task<SiteProfileResponse> UpdateAsync(string userId, UpdateSiteProfileRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 100)
            throw new ArgumentException("Informe um nome com até 100 caracteres.");

        var avatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        if (avatarUrl is not null && (avatarUrl.Length > 2048
            || !Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)))
            throw new ArgumentException("Informe um link HTTP ou HTTPS válido para a foto.");

        var avatarGiphyId = string.IsNullOrWhiteSpace(request.AvatarGiphyId) ? null : request.AvatarGiphyId.Trim();
        if (avatarGiphyId is not null && (avatarGiphyId.Length > 100
            || !System.Text.RegularExpressions.Regex.IsMatch(avatarGiphyId, "^[a-zA-Z0-9]+$")))
            throw new ArgumentException("O GIF selecionado é inválido.");
        if (avatarGiphyId is not null && avatarUrl is not null)
            throw new ArgumentException("Escolha um GIF ou um link para a foto de perfil.");

        var user = await users.UpdateProfileAsync(userId, name, avatarUrl, avatarGiphyId)
            ?? throw new KeyNotFoundException("Perfil não encontrado.");
        return SiteProfileResponse.FromEntity(user);
    }

    public async Task<ProfilePage<FavoriteResponse>> GetFavoritesAsync(string userId, int page, int pageSize, string contentType = "movie")
    {
        var entries = await favorites.GetAsync(userId, page, pageSize, contentType);
        var items = await Task.WhenAll(entries.Take(pageSize).Select(async entry =>
            new FavoriteResponse(entry.ContentType == "series"
                ? ProfileMovieResponse.FromSeries(await series.GetAsync(entry.MovieId), entry.MovieId, entry.Title, entry.Poster)
                : ProfileMovieResponse.FromEntity(await movies.GetAsync(entry.MovieId), entry.MovieId, entry.Title, entry.Poster),
                entry.CreateAt)));
        return new(items, page, pageSize, entries.Count > pageSize);
    }

    public Task<bool> IsFavoriteAsync(string userId, string movieId, string contentType = "movie")
        => favorites.ContainsAsync(userId, movieId, contentType);

    public async Task AddFavoriteAsync(string userId, string movieId, string contentType = "movie")
    {
        var movie = contentType == "series"
            ? ProfileMovieResponse.FromSeries(await series.GetAsync(movieId), movieId, "", "")
            : ProfileMovieResponse.FromEntity(await movies.GetAsync(movieId), movieId, "", "");
        if (!movie.IsAvailable)
            throw new KeyNotFoundException(contentType == "series" ? "Série não encontrada." : "Filme não encontrado.");
        await favorites.AddAsync(new SiteFavoriteEntity
        {
            SiteUserId = userId, MovieId = movie.Id, Title = movie.Title, Poster = movie.Poster, ContentType = contentType
        });
    }

    public Task DeleteFavoriteAsync(string userId, string movieId, string contentType = "movie")
        => favorites.DeleteAsync(userId, movieId, contentType);
}
