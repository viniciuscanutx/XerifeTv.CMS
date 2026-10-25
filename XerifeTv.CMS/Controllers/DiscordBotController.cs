using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.DiscordBot;

namespace XerifeTv.CMS.Controllers;

public class DiscordBotController(
    DiscordBotService discordBotService,
    ILogger<DiscordBotController> logger) : Controller
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("Api/Discord/Interactions")]
    public async Task<IActionResult> Interactions()
    {
        if (!discordBotService.IsConfigured)
        {
            logger.LogWarning("Discord bot não configurado, ignorando interação");
            return NotFound();
        }

        var signature = Request.Headers["X-Signature-Ed25519"].ToString();
        var timestamp = Request.Headers["X-Signature-Timestamp"].ToString();

        Request.EnableBuffering();
        var body = string.Empty;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
        }

        if (!discordBotService.ValidateSignature(signature, timestamp, body))
        {
            logger.LogWarning("Assinatura Discord inválida");
            return Unauthorized();
        }

        JsonElement interaction;
        try
        {
            interaction = JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON inválido na interação Discord");
            return BadRequest("Invalid JSON");
        }

        var response = await discordBotService.HandleInteractionAsync(interaction);

        if (response.HasValue)
        {
            return Json(response.Value);
        }

        return Ok();
    }

    [Authorize(Roles = "admin")]
    [HttpGet("DiscordBot/RegisterSlashCommand")]
    public async Task<IActionResult> RegisterSlashCommand()
    {
        if (!discordBotService.CanRegisterSlashCommand)
            return BadRequest("Configure Discord__BotToken no Render.");

        var response = await discordBotService.RegisterSlashCommandAsync();
        return Content(response, "text/plain");
    }
}
