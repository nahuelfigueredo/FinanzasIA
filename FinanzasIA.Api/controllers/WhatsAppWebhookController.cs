using System.Text.Json;
using FinanzasIA.Api.DTOs;
using FinanzasIA.Api.Options;
using FinanzasIA.Api.Services;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinanzasIA.Api.Controllers;

/// <summary>
/// Webhook de WhatsApp. ActÃºa Ãºnicamente como pasarela: recibe el mensaje,
/// lo delega a <see cref="IMessageProcessor"/> y envÃ­a la respuesta.
/// Toda la interpretaciÃ³n y lÃ³gica de negocio vive en el motor de mensajes.
/// </summary>
[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly WhatsAppOptions _options;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IMessageProcessor _messageProcessor;
    private readonly IUsuarioWhatsAppResolver _usuarioResolver;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IOptions<WhatsAppOptions> options,
        IWhatsAppService whatsAppService,
        IMessageProcessor messageProcessor,
        IUsuarioWhatsAppResolver usuarioResolver,
        ILogger<WhatsAppWebhookController> logger)
    {
        _options = options.Value;
        _whatsAppService = whatsAppService;
        _messageProcessor = messageProcessor;
        _usuarioResolver = usuarioResolver;
        _logger = logger;
    }

    [HttpGet("webhook")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" &&
            !string.IsNullOrWhiteSpace(_options.VerifyToken) &&
            string.Equals(verifyToken, _options.VerifyToken, StringComparison.Ordinal))
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        return Unauthorized();
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook([FromBody] JsonDocument payload, CancellationToken cancellationToken)
    {
        var root = payload.RootElement;
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return Ok();
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value))
                {
                    continue;
                }

                if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var message in messages.EnumerateArray())
                {
                    if (!message.TryGetProperty("from", out var fromNode))
                    {
                        continue;
                    }

                    var from = fromNode.GetString();
                    var incomingText = ExtractIncomingText(message);
                    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(incomingText))
                    {
                        continue;
                    }

                    var usuarioId = await _usuarioResolver.ResolverUsuarioIdAsync(from, cancellationToken);
                    var resultado = await _messageProcessor.ProcesarAsync(new MensajeEntranteDto
                    {
                        Texto = incomingText,
                        Origen = MessageOrigen.WhatsApp,
                        UsuarioId = usuarioId
                    }, cancellationToken);

                    var response = resultado.Respuesta;
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(_options.AccessToken) && !string.IsNullOrWhiteSpace(_options.PhoneNumberId))
                    {
                        await _whatsAppService.SendTextMessageAsync(from, response, cancellationToken);
                        _logger.LogInformation("WhatsApp reply sent to {Phone}.", from);
                    }
                    else
                    {
                        _logger.LogInformation("WhatsApp response not sent because credentials are missing. Response: {Response}", response);
                    }
                }
            }
        }

        return Ok();
    }

    [HttpPost("send-test")]
    public async Task<IActionResult> SendTestMessage([FromBody] SendWhatsAppMessageDto dto, CancellationToken cancellationToken)
    {
        await _whatsAppService.SendTextMessageAsync(dto.To, dto.Message, cancellationToken);
        return Ok(new { message = "Mensaje enviado a WhatsApp." });
    }

    private static string? ExtractIncomingText(JsonElement message)
    {
        if (message.TryGetProperty("text", out var textNode) &&
            textNode.TryGetProperty("body", out var bodyNode))
        {
            return bodyNode.GetString();
        }

        if (message.TryGetProperty("interactive", out var interactiveNode))
        {
            if (interactiveNode.TryGetProperty("button_reply", out var buttonReply) &&
                buttonReply.TryGetProperty("title", out var buttonTitle))
            {
                return buttonTitle.GetString();
            }

            if (interactiveNode.TryGetProperty("list_reply", out var listReply) &&
                listReply.TryGetProperty("title", out var listTitle))
            {
                return listTitle.GetString();
            }
        }

        return null;
    }
}
