using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using NSec.Cryptography;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Modules.CatalogProvider.Dtos.Response;
using XerifeTv.CMS.Modules.CatalogProvider.Interfaces;
using XerifeTv.CMS.Modules.Integrations.Imdb.Services;
using XerifeTv.CMS.Modules.Movie.Dtos.Request;
using XerifeTv.CMS.Modules.Movie.Interfaces;

namespace XerifeTv.CMS.Modules.DiscordBot;

public sealed class DiscordBotService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMovieService movieService,
    IImdbService imdbService,
    ICatalogProviderService catalogProviderService,
    IActivityLogService activityLogService,
    ILogger<DiscordBotService> logger,
    IServiceScopeFactory serviceScopeFactory)
{
    private const int InteractionTypePing = 1;
    private const int InteractionTypeApplicationCommand = 2;
    private const int InteractionTypeMessageComponent = 3;
    private const int InteractionTypeModalSubmit = 5;

    private const int CallbackTypePong = 1;
    private const int CallbackTypeChannelMessageWithSource = 4;
    private const int CallbackTypeDeferredChannelMessageWithSource = 5;
    private const int CallbackTypeDeferredUpdateMessage = 6;
    private const int CallbackTypeUpdateMessage = 7;
    private const int CallbackTypeModal = 9;

    private const int ComponentTypeActionRow = 1;
    private const int ComponentTypeButton = 2;
    private const int ComponentTypeSelectMenu = 3;

    private const int ButtonStylePrimary = 1;
    private const int ButtonStyleSecondary = 2;
    private const int ButtonStyleSuccess = 3;
    private const int ButtonStyleDanger = 4;

    private static readonly string[] ParentalRatings = ["L", "10", "12", "14", "16", "18"];
    private static readonly Regex ImdbIdRegex = new(@"tt\d{5,10}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string BotToken => configuration["Discord:BotToken"] ?? string.Empty;
    private string ApplicationId => configuration["Discord:ApplicationId"] ?? string.Empty;
    private string PublicKey => configuration["Discord:PublicKey"] ?? string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(ApplicationId);
    public bool CanRegisterSlashCommand => !string.IsNullOrWhiteSpace(BotToken);

    public bool ValidateSignature(string signature, string timestamp, string body)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(body)
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            return false;

        try
        {
            var requestTime = DateTimeOffset.FromUnixTimeSeconds(seconds);
            if ((DateTimeOffset.UtcNow - requestTime).Duration() > TimeSpan.FromMinutes(5))
                return false;

            var publicKeyBytes = Convert.FromHexString(PublicKey);
            var signatureBytes = Convert.FromHexString(signature);
            var messageBytes = Encoding.UTF8.GetBytes(timestamp + body);

            var publicKey = NSec.Cryptography.PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            return SignatureAlgorithm.Ed25519.Verify(publicKey, messageBytes, signatureBytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao validar assinatura Discord");
            return false;
        }
    }

    public async Task<JsonElement?> HandleInteractionAsync(JsonElement interaction)
    {
        try
        {
            var type = interaction.GetProperty("type").GetInt32();

            return type switch
            {
                InteractionTypePing => CreatePongResponse(),
                InteractionTypeApplicationCommand => await HandleSlashCommandAsync(interaction),
                InteractionTypeMessageComponent => await HandleComponentAsync(interaction),
                InteractionTypeModalSubmit => await HandleModalSubmitAsync(interaction),
                _ => CreateErrorResponse("Tipo de interação não suportado")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao processar interação Discord");
            return CreateErrorResponse("Erro interno");
        }
    }

    public async Task<string> RegisterSlashCommandAsync()
    {
        if (!CanRegisterSlashCommand)
            return "Bot não configurado: Discord:BotToken ausente.";

        var command = new
        {
            name = "filme",
            description = "Cadastrar um filme no CMS via IMDb",
            default_member_permissions = "8",
            options = new[]
            {
                new
                {
                    name = "imdb",
                    description = "IMDb ID do filme (ex.: tt0111161) ou link do IMDb",
                    type = 3
                    required = true
                }
            }
        };

        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", BotToken);

            var response = await client.PostAsJsonAsync(
                $"https://discord.com/api/v10/applications/{ApplicationId}/commands",
                command);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord RegisterSlashCommand falhou: {Body}", body);
                return $"Falha ao registrar comando: {body}";
            }

            logger.LogInformation("Slash command /filme registrado com sucesso");
            return $"Comando registrado com sucesso:\n{body}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao registrar slash command");
            return $"Erro: {ex.Message}";
        }
    }

    private JsonElement CreatePongResponse()
    {
        var response = new { type = CallbackTypePong };
        return JsonSerializer.SerializeToElement(response);
    }

    private async Task<JsonElement?> HandleSlashCommandAsync(JsonElement interaction)
    {
        var commandName = interaction.GetProperty("data").GetProperty("name").GetString();
        if (commandName != "filme")
            return CreateErrorResponse("Comando desconhecido");

        var deferredResponse = new
        {
            type = CallbackTypeDeferredChannelMessageWithSource,
            data = new { flags = 64 }
        };

        RunInBackground(service => service.ProcessFilmeCommandAsync(interaction.Clone()));
        return JsonSerializer.SerializeToElement(deferredResponse);
    }

    private void RunInBackground(Func<DiscordBotService, Task> action)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<DiscordBotService>();
                await action(service);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro em processamento assíncrono do Discord");
            }
        });
    }

    private async Task ProcessFilmeCommandAsync(JsonElement interaction)
    {
        var interactionToken = interaction.GetProperty("token").GetString()!;
        var applicationId = interaction.GetProperty("application_id").GetString()!;
        var userId = interaction.GetProperty("member").GetProperty("user").GetProperty("id").GetString()!;
        var username = interaction.GetProperty("member").GetProperty("user").GetProperty("username").GetString()!;

        var options = interaction.GetProperty("data").GetProperty("options");
        var imdbInput = options[0].GetProperty("value").GetString() ?? string.Empty;

        var match = ImdbIdRegex.Match(imdbInput);
        if (!match.Success)
        {
            await SendFollowupAsync(interactionToken, applicationId, "🎬 Manda o IMDb ID do filme (ex.: tt0111161) ou o link do IMDb.", ephemeral: true);
            return;
        }

        var imdbId = match.Value.ToLowerInvariant();
        var imdbInfo = await imdbService.GetMovieByImdbIdAsync(imdbId);

        if (imdbInfo.IsFailure || imdbInfo.Data is null)
        {
            await SendFollowupAsync(interactionToken, applicationId, $"❌ Não achei o filme {imdbId} no IMDb/TMDB.", ephemeral: true);
            return;
        }

        var existing = await movieService.GetByImdbIdAsync(imdbId);
        var alreadyExists = existing.IsSuccess && existing.Data is not null;

        var caption = $"🎬 **{imdbInfo.Data.Title}** ({imdbInfo.Data.ReleaseYear})\n`{imdbId}`"
            + (alreadyExists ? "\n⚠️ Já está no CMS: confirmar vai atualizar classificação e fonte." : string.Empty)
            + "\n\n**Qual a classificação?**";

        var ratingComponents = CreateRatingComponents(imdbId);

        var followup = new
        {
            content = caption,
            components = ratingComponents,
            flags = 64
        };

        await SendFollowupAsync(interactionToken, applicationId, followup);
    }

    private object[] CreateRatingComponents(string imdbId)
    {
        var rows = new List<object>();

        var chunks = ParentalRatings.Chunk(3);
        foreach (var chunk in chunks)
        {
            var buttons = chunk.Select(r => new
            {
                type = ComponentTypeButton,
                style = ButtonStylePrimary,
                label = r,
                custom_id = $"r|{imdbId}|{r}"
            }).Cast<object>().ToArray();

            rows.Add(new { type = ComponentTypeActionRow, components = buttons });
        }

        rows.Add(new
        {
            type = ComponentTypeActionRow,
            components = new[]
            {
                new
                {
                    type = ComponentTypeButton,
                    style = ButtonStyleDanger,
                    label = "Cancelar",
                    custom_id = "x"
                }
            }
        });

        return rows.Cast<object>().ToArray();
    }

    private async Task ShowProvidersAsync(string interactionToken, string applicationId, string messageId, string imdbId, string rating)
    {
        var providers = await GetProvidersAsync();
        if (providers.Count == 0)
        {
            await EditFollowupAsync(interactionToken, applicationId, messageId, "❌ Nenhum provedor de catálogo ativo no CMS.");
            return;
        }

        var selectMenu = new
        {
            type = ComponentTypeSelectMenu,
            custom_id = $"p|{imdbId}|{rating}",
            placeholder = "Qual a fonte?",
            min_values = 1,
            max_values = 1,
            options = providers.Take(25).Select(p => new
            {
                label = (p.IsDefault ? "⭐ " : "") + p.Name,
                value = p.Id,
                description = $"ID: {p.Id}"
            }).ToArray()
        };
        var components = new object[]
        {
            new { type = ComponentTypeActionRow, components = new object[] { selectMenu } },
            new
            {
                type = ComponentTypeActionRow,
                components = new object[]
                {
                    new { type = ComponentTypeButton, style = ButtonStyleDanger, label = "Cancelar", custom_id = "x" }
                }
            }
        };

        await EditFollowupAsync(interactionToken, applicationId, messageId,
            $"{imdbId} · classificação {rating}\n\n**Qual a fonte?**", components);
    }

    private Task<JsonElement?> HandleComponentAsync(JsonElement interaction)
    {
        var data = interaction.GetProperty("data");
        var customId = data.GetProperty("custom_id").GetString() ?? string.Empty;
        var interactionToken = interaction.GetProperty("token").GetString()!;
        var applicationId = interaction.GetProperty("application_id").GetString()!;
        var messageId = interaction.GetProperty("message").GetProperty("id").GetString()!;
        var parts = customId.Split('|');

        if (parts[0] == "x")
            return Task.FromResult<JsonElement?>(CreateUpdateResponse("🚫 Cancelado."));

        if (parts[0] == "r" && parts.Length == 3)
        {
            var imdbId = parts[1];
            var rating = parts[2];
            RunInBackground(service => service.ShowProvidersAsync(interactionToken, applicationId, messageId, imdbId, rating));
            return Task.FromResult<JsonElement?>(CreateDeferredUpdateResponse());
        }

        if (parts[0] == "p" && parts.Length == 3 && data.TryGetProperty("values", out var values) && values.GetArrayLength() > 0)
        {
            var providerId = values[0].GetString() ?? string.Empty;
            RunInBackground(service => service.SaveMovieAsync(interactionToken, applicationId, messageId, parts[1], parts[2], providerId));
            return Task.FromResult<JsonElement?>(CreateDeferredUpdateResponse());
        }

        return Task.FromResult<JsonElement?>(CreateErrorResponse("Interação inválida"));
    }

    private Task<JsonElement?> HandleModalSubmitAsync(JsonElement interaction) =>
        Task.FromResult<JsonElement?>(CreateErrorResponse("Modal não suportado"));

    private async Task SaveMovieAsync(string interactionToken, string applicationId, string messageId, string imdbId, string rating, string providerId)
    {
        var provider = (await GetProvidersAsync()).FirstOrDefault(p => p.Id == providerId);
        if (provider is null)
        {
            await EditFollowupAsync(interactionToken, applicationId, messageId, "❌ Provedor não encontrado ou desativado.", components: null);
            return;
        }

        var videoUrl = await BuildCatalogUrlAsync(provider, imdbId);
        if (videoUrl is null)
        {
            await EditFollowupAsync(interactionToken, applicationId, messageId, $"❌ Não consegui montar o link do {provider.Name} (sem TMDB ID).", components: null);
            return;
        }

        await EditFollowupAsync(interactionToken, applicationId, messageId, $"⏳ Cadastrando {imdbId}...", components: null);

        var result = await movieService.BatchAddMoviesAsync(new BatchMoviesRequestDto
        {
            ImdbIdsText = imdbId,
            ParentalRatingsText = rating,
            VideoUrlsText = videoUrl
        });

        if (result.IsFailure || result.Data == 0)
        {
            logger.LogWarning("Discord bot failed to save movie {ImdbId}: {Error}", imdbId, result.Error?.Description);
            await EditFollowupAsync(interactionToken, applicationId, messageId, $"❌ Falhou ao cadastrar {imdbId}: {result.Error?.Description ?? "erro desconhecido"}", components: null);
            return;
        }

        var saved = await movieService.GetByImdbIdAsync(imdbId);
        var title = saved.Data?.Title ?? imdbId;
        await activityLogService.LogAsync("discord", "Filmes", "created", $"cadastrou o filme \"{title}\" pelo Discord");
        await EditFollowupAsync(interactionToken, applicationId, messageId, $"✅ {title}\nClassificação {rating} · fonte {provider.Name}", components: null);
    }

    private async Task<List<GetCatalogProviderResponseDto>> GetProvidersAsync()
    {
        var result = await catalogProviderService.GetAllAsync(isIncludeDisabled: false);
        return (result.Data ?? []).OrderBy(p => p.Order).ToList();
    }

    private async Task<string?> BuildCatalogUrlAsync(GetCatalogProviderResponseDto provider, string imdbId)
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');
        if (!provider.IdType.Equals("tmdb", StringComparison.OrdinalIgnoreCase))
            return $"{baseUrl}/stream/movie/{imdbId}.json";

        var imdbInfo = await imdbService.GetMovieByImdbIdAsync(imdbId);
        return imdbInfo.IsSuccess && imdbInfo.Data is { Id: > 0 }
            ? $"{baseUrl}/api/gaiaflix-movie-source?id={imdbInfo.Data.Id}"
            : null;
    }

    private JsonElement CreateUpdateResponse(string content, object? components = null)
    {
        var response = new
        {
            type = CallbackTypeUpdateMessage,
            data = new
            {
                content,
                components = components ?? Array.Empty<object>()
            }
        };
        return JsonSerializer.SerializeToElement(response);
    }

    private JsonElement CreateDeferredUpdateResponse()
    {
        var response = new { type = CallbackTypeDeferredUpdateMessage };
        return JsonSerializer.SerializeToElement(response);
    }

    private JsonElement CreateErrorResponse(string message)
    {
        var response = new
        {
            type = CallbackTypeChannelMessageWithSource,
            data = new { content = message, flags = 64 }
        };
        return JsonSerializer.SerializeToElement(response);
    }

    private async Task SendFollowupAsync(string interactionToken, string applicationId, string content, bool ephemeral = true)
    {
        var payload = new { content, flags = ephemeral ? 64 : 0 };
        await SendFollowupAsync(interactionToken, applicationId, payload);
    }

    private async Task SendFollowupAsync(string interactionToken, string applicationId, object payload)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync($"https://discord.com/api/v10/webhooks/{applicationId}/{interactionToken}", payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao enviar follow-up Discord");
        }
    }

    private async Task EditFollowupAsync(string interactionToken, string applicationId, string messageId, string content, object? components = null)
    {
        var payload = new
        {
            content,
            components = components ?? Array.Empty<object>()
        };

        try
        {
            var client = httpClientFactory.CreateClient();
            await client.PatchAsJsonAsync($"https://discord.com/api/v10/webhooks/{applicationId}/{interactionToken}/messages/{messageId}", payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao editar follow-up Discord");
        }
    }
}
