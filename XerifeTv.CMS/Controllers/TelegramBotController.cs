using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.TelegramBot;

namespace XerifeTv.CMS.Controllers;

public class TelegramBotController(
    TelegramBotService _telegramBotService,
    ILogger<TelegramBotController> _logger) : Controller
{
    // Recebe os updates do Telegram; o secret no header garante que veio do Telegram
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("Api/Telegram/Webhook")]
    public async Task<IActionResult> Webhook([FromBody] JsonElement update)
    {
        if (!_telegramBotService.IsConfigured) return NotFound();

        var secret = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (secret != _telegramBotService.WebhookSecret) return Unauthorized();

        try
        {
            await _telegramBotService.HandleUpdateAsync(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram webhook error");
        }

        return Ok();
    }

    // Abrir uma vez logado como admin, no endereço público do CMS, para ligar o webhook
    [Authorize(Roles = "admin")]
    [HttpGet("TelegramBot/RegisterWebhook")]
    public async Task<IActionResult> RegisterWebhook()
    {
        if (!_telegramBotService.IsConfigured)
            return BadRequest("Configure Telegram__BotToken, Telegram__AllowedChatId e Telegram__WebhookSecret.");

        var webhookUrl = $"https://{Request.Host}/Api/Telegram/Webhook";
        var response = await _telegramBotService.RegisterWebhookAsync(webhookUrl);
        return Content($"{webhookUrl}\n\n{response}", "text/plain");
    }
}
