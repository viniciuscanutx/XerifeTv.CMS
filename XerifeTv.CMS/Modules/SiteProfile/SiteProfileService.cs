using XerifeTv.CMS.Modules.SiteBadge;
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
    Task<string> UploadImageAsync(string userId, string kind, Stream content, long length, string? contentType, string? fileName);
    Task<SiteProfileImageEntity?> GetImageAsync(string imageId);
}

public sealed class SiteProfileService(
    ISiteUserRepository users,
    ISiteFavoriteRepository favorites,
    IMovieRepository movies, ISeriesRepository series, ISiteBadgeService badges,
    ISiteProfileImageRepository images) : ISiteProfileService
{
    public const long MaxProfileImageBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageKinds = ["avatar", "banner"];

    // Content-Type aceito -> extensão equivalente (para quando o envio não informa o tipo)
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    // Guarda a imagem no MongoDB e devolve o id; o perfil muda quando o usuário salva (PUT)
    public async Task<string> UploadImageAsync(string userId, string kind, Stream content, long length, string? contentType, string? fileName)
    {
        if (!ImageKinds.Contains(kind))
            throw new ArgumentException("Tipo de imagem inválido.");
        if (length <= 0)
            throw new ArgumentException("Envie uma imagem.");
        if (length > MaxProfileImageBytes)
            throw new ArgumentException("A imagem deve ter no máximo 5 MB.");

        var resolvedType = ResolveImageType(contentType, fileName)
            ?? throw new ArgumentException("Envie uma imagem JPG, PNG, WEBP ou GIF.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        return await images.CreateAsync(new SiteProfileImageEntity
        {
            SiteUserId = userId,
            Kind = kind,
            ContentType = resolvedType,
            Data = buffer.ToArray(),
        });
    }

    public Task<SiteProfileImageEntity?> GetImageAsync(string imageId) => images.GetAsync(imageId);

    // Prefere o Content-Type enviado; sem ele, deduz pela extensão do arquivo
    private static string? ResolveImageType(string? contentType, string? fileName)
    {
        if (contentType is not null && ImageTypes.ContainsKey(contentType))
            return contentType;
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (extension == ".jpeg") extension = ".jpg";
        return ImageTypes.FirstOrDefault(pair => pair.Value == extension).Key;
    }

    public async Task<SiteProfileResponse> GetAsync(string userId)
    {
        var user = await users.GetAsync(userId) ?? throw new KeyNotFoundException("Perfil não encontrado.");
        var assigned = await badges.GetAssignedAsync(user.BadgeIds ?? []);
        return SiteProfileResponse.FromEntity(user) with { Badges = assigned, SelectedBadge = assigned.FirstOrDefault(x => x.Id == user.SelectedBadgeId) };
    }

    public async Task<SiteProfileResponse> UpdateAsync(string userId, UpdateSiteProfileRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 100)
            throw new ArgumentException("Informe um nome com até 100 caracteres.");

        var avatarUrl = NormalizeHttpUrl(request.AvatarUrl, "Informe um link HTTP ou HTTPS válido para a foto.");
        var bannerUrl = NormalizeHttpUrl(request.BannerUrl, "Informe um link HTTP ou HTTPS válido para a imagem de fundo.");

        var avatarGiphyId = string.IsNullOrWhiteSpace(request.AvatarGiphyId) ? null : request.AvatarGiphyId.Trim();
        if (avatarGiphyId is not null && (avatarGiphyId.Length > 100
            || !System.Text.RegularExpressions.Regex.IsMatch(avatarGiphyId, "^[a-zA-Z0-9]+$")))
            throw new ArgumentException("O GIF selecionado é inválido.");
        if (avatarGiphyId is not null && avatarUrl is not null)
            throw new ArgumentException("Escolha um GIF ou um link para a foto de perfil.");

        var user = await users.UpdateProfileAsync(userId, name, avatarUrl, avatarGiphyId, bannerUrl)
            ?? throw new KeyNotFoundException("Perfil não encontrado.");
        return await GetAsync(user.Id);
    }

    private static string? NormalizeHttpUrl(string? value, string errorMessage)
    {
        var url = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (url is not null && (url.Length > 2048
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)))
            throw new ArgumentException(errorMessage);
        return url;
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
