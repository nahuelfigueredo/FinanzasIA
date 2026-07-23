using FinanzasIA.Api.DTOs;
using FinanzasIA.Api.Options;
using FinanzasIA.Api.Services;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;


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
    private readonly WhatsAppMessageHandler _messageHandler;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IOptions<WhatsAppOptions> options,
        IWhatsAppService whatsAppService,
        WhatsAppMessageHandler messageHandler,
        ILogger<WhatsAppWebhookController> logger)
    {
        _options = options.Value;
        _whatsAppService = whatsAppService;
        _messageHandler = messageHandler;
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

    // TODO: Endpoint temporal de diagnóstico. Eliminar al terminar las pruebas.
    [HttpGet("debug-config")]
    public IActionResult DebugConfig()
    {
        return Ok(new
        {
            verifyTokenVacio = string.IsNullOrWhiteSpace(_options.VerifyToken),
            verifyTokenLongitud = _options.VerifyToken?.Length ?? 0,
            accessTokenVacio = string.IsNullOrWhiteSpace(_options.AccessToken),
            accessTokenLongitud = _options.AccessToken?.Length ?? 0,
            phoneNumberId = _options.PhoneNumberId
        });
    }

    // TODO: Endpoint temporal de diagnóstico de autenticación contra Meta. Eliminar al terminar las pruebas.
    [HttpGet("test-meta")]
    public async Task<IActionResult> TestMeta(CancellationToken cancellationToken)
    {
        var (statusCode, responseBody) = await _whatsAppService.TestMetaAuthAsync(cancellationToken);

        return new ContentResult
        {
            StatusCode = StatusCodes.Status200OK,
            ContentType = "application/json",
            Content = $"{{\"statusCode\":{statusCode},\"responseBody\":{JsonSerializer.Serialize(responseBody)}}}"
        };
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook([FromBody] JsonDocument payload, CancellationToken cancellationToken)
    {
        var root = payload.RootElement;
        var payloadJson = root.GetRawText();
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
                    var imageId = ExtractImageMediaId(message);
                    if (string.IsNullOrWhiteSpace(from) || (string.IsNullOrWhiteSpace(incomingText) && string.IsNullOrWhiteSpace(imageId)))
                    {
                        continue;
                    }

                    // Un fallo procesando un mensaje nunca debe abortar el batch ni
                    // devolver error al webhook (Meta reintentaría el payload completo).
                    await _messageHandler.ProcesarAsync(
                        from, incomingText, imageId,
                        origen: "webhook",
                        payloadJson: payloadJson,
                        enviarRespuesta: true,
                        cancellationToken: cancellationToken);
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

    private static string? ExtractImageMediaId(JsonElement message)
    {
        if (message.TryGetProperty("type", out var typeNode) &&
            typeNode.GetString() == "image" &&
            message.TryGetProperty("image", out var imageNode) &&
            imageNode.TryGetProperty("id", out var idNode))
        {
            return idNode.GetString();
        }

        return null;
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
