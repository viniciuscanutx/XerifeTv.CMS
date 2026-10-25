using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.CatalogProvider.Dtos.Response;
using XerifeTv.CMS.Modules.CatalogProvider.Interfaces;
using XerifeTv.CMS.Modules.Integrations.Imdb.Services;
using XerifeTv.CMS.Modules.Movie.Dtos.Request;
using XerifeTv.CMS.Modules.Movie.Interfaces;

namespace XerifeTv.CMS.Modules.TelegramBot;

public sealed class TelegramBotService(
    IHttpClientFactory _httpClientFactory,
    IConfiguration _configuration,
    IMovieService _movieService,
    IImdbService _imdbService,
    ICatalogProviderService _catalogProviderService,
    IActivityLogService _activityLogService,
    ILogger<TelegramBotService> _logger)
{
    private static readonly string[] ParentalRatings = ["L", "10", "12", "14", "16", "18"];
    private static readonly Regex ImdbIdRegex = new(@"tt\d{5,10}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string BotToken => _configuration["Telegram:BotToken"] ?? string.Empty;
    private string AllowedChatId => _configuration["Telegram:AllowedChatId"] ?? string.Empty;
    public string WebhookSecret => _configuration["Telegram:WebhookSecret"] ?? string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(AllowedChatId) && !string.IsNullOrWhiteSpace(WebhookSecret);

    public async Task HandleUpdateAsync(JsonElement update)
    {
        if (update.TryGetProperty("message", out var message))
            await HandleMessageAsync(message);
        else if (update.TryGetProperty("callback_query", out var callback))
            await HandleCallbackAsync(callback);
    }

    public async Task<string> RegisterWebhookAsync(string webhookUrl)
    {
        var response = await CallAsync("setWebhook", new
        {
            url = webhookUrl,
            secret_token = WebhookSecret,
            allowed_updates = new[] { "message", "callback_query" }
        });
        return response;
    }

    private async Task HandleMessageAsync(JsonElement message)
    {
        var chatId = message.GetProperty("chat").GetProperty("id").GetRawText();
        if (chatId != AllowedChatId) return;

        var text = message.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        var match = ImdbIdRegex.Match(text);

        if (!match.Success)
        {
            await SendMessageAsync(chatId, "🎬 Manda o IMDb ID do filme (ex.: tt0111161) ou o link do IMDb.");
            return;
        }

        var imdbId = match.Value.ToLowerInvariant();
        var imdbInfo = await _imdbService.GetMovieByImdbIdAsync(imdbId);
        if (imdbInfo.IsFailure || imdbInfo.Data is null)
        {
            await SendMessageAsync(chatId, $"❌ Não achei o filme {imdbId} no IMDb/TMDB.");
            return;
        }

        var existing = await _movieService.GetByImdbIdAsync(imdbId);
        var alreadyExists = existing.IsSuccess && existing.Data is not null;

        var caption = $"🎬 {imdbInfo.Data.Title} ({imdbInfo.Data.ReleaseYear})\n{imdbId}"
            + (alreadyExists ? "\n⚠️ Já está no CMS: confirmar vai atualizar classificação e fonte." : string.Empty)
            + "\n\nQual a classificação?";

        // callback sem estado: o próprio botão carrega o imdb (sobrevive ao Render dormir)
        var ratingButtons = ParentalRatings
            .Select(r => new { text = r, callback_data = $"r|{imdbId}|{r}" })
            .Chunk(3)
            .Select(row => row.Cast<object>().ToArray())
            .Append([new { text = "Cancelar", callback_data = "x" }])
            .ToArray();

        var markup = new { inline_keyboard = ratingButtons };
        if (!string.IsNullOrWhiteSpace(imdbInfo.Data.PosterUrl))
            await CallAsync("sendPhoto", new { chat_id = chatId, photo = imdbInfo.Data.PosterUrl, caption, reply_markup = markup });
        else
            await CallAsync("sendMessage", new { chat_id = chatId, text = caption, reply_markup = markup });
    }

    private async Task HandleCallbackAsync(JsonElement callback)
    {
        var callbackId = callback.GetProperty("id").GetString();
        var message = callback.GetProperty("message");
        var chatId = message.GetProperty("chat").GetProperty("id").GetRawText();
        var messageId = message.GetProperty("message_id").GetInt32();
        var data = callback.TryGetProperty("data", out var d) ? d.GetString() ?? string.Empty : string.Empty;

        await CallAsync("answerCallbackQuery", new { callback_query_id = callbackId });
        if (chatId != AllowedChatId) return;

        var parts = data.Split('|');
        var hasPhoto = message.TryGetProperty("photo", out _);

        if (parts[0] == "x")
        {
            await EditAsync(chatId, messageId, hasPhoto, "🚫 Cancelado.", null);
            return;
        }

        if (parts[0] == "r" && parts.Length == 3)
        {
            var providers = await GetProvidersAsync();
            if (providers.Count == 0)
            {
                await EditAsync(chatId, messageId, hasPhoto, "❌ Nenhum provedor de catálogo ativo no CMS.", null);
                return;
            }

            var buttons = providers
                .Select(p => new[] { (object)new { text = (p.IsDefault ? "⭐ " : string.Empty) + p.Name, callback_data = $"p|{parts[1]}|{parts[2]}|{p.Id}" } })
                .Append([new { text = "Cancelar", callback_data = "x" }])
                .ToArray();

            await EditAsync(chatId, messageId, hasPhoto, $"{parts[1]} · classificação {parts[2]}\n\nQual a fonte?", new { inline_keyboard = buttons });
            return;
        }

        if (parts[0] == "p" && parts.Length == 4)
            await SaveMovieAsync(chatId, messageId, hasPhoto, parts[1], parts[2], parts[3]);
    }

    private async Task SaveMovieAsync(string chatId, int messageId, bool hasPhoto, string imdbId, string rating, string providerId)
    {
        var provider = (await GetProvidersAsync()).FirstOrDefault(p => p.Id == providerId);
        if (provider is null)
        {
            await EditAsync(chatId, messageId, hasPhoto, "❌ Provedor não encontrado ou desativado.", null);
            return;
        }

        var videoUrl = await BuildCatalogUrlAsync(provider, imdbId);
        if (videoUrl is null)
        {
            await EditAsync(chatId, messageId, hasPhoto, $"❌ Não consegui montar o link do {provider.Name} (sem TMDB ID).", null);
            return;
        }

        await EditAsync(chatId, messageId, hasPhoto, $"⏳ Cadastrando {imdbId}...", null);

        var result = await _movieService.BatchAddMoviesAsync(new BatchMoviesRequestDto
        {
            ImdbIdsText = imdbId,
            ParentalRatingsText = rating,
            VideoUrlsText = videoUrl
        });

        if (result.IsFailure || result.Data == 0)
        {
            _logger.LogWarning("Telegram bot failed to save movie {ImdbId}: {Error}", imdbId, result.Error?.Description);
            await EditAsync(chatId, messageId, hasPhoto, $"❌ Falhou ao cadastrar {imdbId}: {result.Error?.Description ?? "erro desconhecido"}", null);
            return;
        }

        var saved = await _movieService.GetByImdbIdAsync(imdbId);
        var title = saved.Data?.Title ?? imdbId;
        await _activityLogService.LogAsync("telegram", "Filmes", "created", $"cadastrou o filme \"{title}\" pelo Telegram");
        await EditAsync(chatId, messageId, hasPhoto, $"✅ {title}\nClassificação {rating} · fonte {provider.Name}", null);
    }

    private async Task<string?> BuildCatalogUrlAsync(GetCatalogProviderResponseDto provider, string imdbId)
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');
        if (!provider.IdType.Equals("tmdb", StringComparison.OrdinalIgnoreCase))
            return $"{baseUrl}/stream/movie/{imdbId}.json";

        // provedor TMDB (gaiaflix) usa o id do TMDB, igual ao video-setting.js
        var imdbInfo = await _imdbService.GetMovieByImdbIdAsync(imdbId);
        return imdbInfo.IsSuccess && imdbInfo.Data is { Id: > 0 }
            ? $"{baseUrl}/api/gaiaflix-movie-source?id={imdbInfo.Data.Id}"
            : null;
    }

    private async Task<List<GetCatalogProviderResponseDto>> GetProvidersAsync()
    {
        var result = await _catalogProviderService.GetAllAsync(isIncludeDisabled: false);
        return (result.Data ?? []).OrderBy(p => p.Order).ToList();
    }

    private Task EditAsync(string chatId, int messageId, bool hasPhoto, string text, object? markup)
    {
        object payload = hasPhoto
            ? new { chat_id = chatId, message_id = messageId, caption = text, reply_markup = markup ?? new { inline_keyboard = Array.Empty<object>() } }
            : new { chat_id = chatId, message_id = messageId, text, reply_markup = markup ?? new { inline_keyboard = Array.Empty<object>() } };
        return CallAsync(hasPhoto ? "editMessageCaption" : "editMessageText", payload);
    }

    private Task SendMessageAsync(string chatId, string text) =>
        CallAsync("sendMessage", new { chat_id = chatId, text });

    private async Task<string> CallAsync(string method, object payload)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync($"https://api.telegram.org/bot{BotToken}/{method}", payload);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Telegram {Method} failed: {Body}", method, body);
            return body;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram {Method} error", method);
            return ex.Message;
        }
    }
}
