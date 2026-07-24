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

    // Endpoint de diagnóstico de configuración (no expone el AccessToken).
    [HttpGet("debug-config")]
    public IActionResult DebugConfig()
    {
        return Ok(new
        {
            phoneNumberId = _options.PhoneNumberId,
            verifyToken = _options.VerifyToken,
            accessTokenVacio = string.IsNullOrWhiteSpace(_options.AccessToken),
            accessTokenLongitud = _options.AccessToken?.Length ?? 0,
            graphApiVersion = _options.GraphApiVersion,
            endpoint = _options.MessagesEndpoint
        });
    }

    // Diagnóstico del token configurado: expone solo longitud y extremos (nunca el token completo).
    [HttpGet("token-info")]
    public IActionResult TokenInfo()
    {
        var token = _options.AccessToken;
        return Ok(new
        {
            longitud = token?.Length,
            primeros4 = token?.Length >= 4 ? token[..4] : token,
            ultimos4 = token?.Length >= 4 ? token[^4..] : token
        });
    }

    // TODO: Endpoint temporal de diagnóstico. Eliminar al terminar las pruebas.
    // Llama a debug_token de Graph API con el mismo AccessToken que usa WhatsAppService
    // y devuelve validez, vencimiento, app y permisos (sin exponer el token).
    [HttpGet("token-debug")]
    public async Task<IActionResult> TokenDebug(
        [FromServices] IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var token = _options.AccessToken?.Trim() ?? string.Empty;
        var version = string.IsNullOrWhiteSpace(_options.GraphApiVersion) ? "v23.0" : _options.GraphApiVersion.Trim();
        var url = $"https://graph.facebook.com/{version}/debug_token?input_token={Uri.EscapeDataString(token)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                bool? isValid = data.TryGetProperty("is_valid", out var v) ? v.GetBoolean() : null;
                long? expiresAtUnix = data.TryGetProperty("expires_at", out var e) && e.TryGetInt64(out var unix) ? unix : null;
                string? expiresAtUtc = expiresAtUnix is > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix.Value).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
                    : expiresAtUnix == 0 ? "nunca (token permanente)" : null;

                return Ok(new
                {
                    is_valid = isValid,
                    expires_at = expiresAtUnix,
                    expires_at_utc = expiresAtUtc,
                    app_id = data.TryGetProperty("app_id", out var a) ? a.GetString() : null,
                    scopes = data.TryGetProperty("scopes", out var s) && s.ValueKind == JsonValueKind.Array
                        ? s.EnumerateArray().Select(x => x.GetString()).ToList()
                        : null,
                    granular_scopes = data.TryGetProperty("granular_scopes", out var g) ? g.Clone() : (object?)null
                });
            }

            // Meta devolvió un error de nivel superior (token inválido para autenticar la llamada).
            return StatusCode((int)response.StatusCode, doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            return StatusCode((int)response.StatusCode, new { error = "Respuesta de debug_token no es JSON válido.", body });
        }
    }

    // Diagnóstico de autenticación contra Meta: valida el token con debug_token,
    // consulta el Phone Number ID y explica el motivo exacto de un error 190.
    [HttpGet("test-meta")]
    public async Task<IActionResult> TestMeta(CancellationToken cancellationToken)
    {
        var resultado = await _whatsAppService.InspectTokenAsync(cancellationToken);
        return Ok(resultado);
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook([FromBody] JsonDocument payload, CancellationToken cancellationToken)
    {
        var root = payload.RootElement;
        var payloadJson = root.GetRawText();
        _logger.LogInformation("Webhook recibido. Payload: {Payload}", payloadJson);

        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Webhook descartado: el payload no contiene 'entry'.");
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
                    // Evento sin mensajes (por ejemplo 'statuses': entregado/leído). Es normal ignorarlo.
                    _logger.LogInformation("Webhook sin campo 'messages' (probablemente un evento de status); se ignora.");
                    continue;
                }

                // TODO: Log temporal de diagnóstico. Eliminar al terminar las pruebas.
                _logger.LogInformation("MESSAGES recibido (JSON crudo): {Messages}", messages.GetRawText());

                foreach (var message in messages.EnumerateArray())
                {
                    if (!message.TryGetProperty("from", out var fromNode))
                    {
                        _logger.LogWarning("Mensaje descartado: no contiene 'from'. Mensaje: {Mensaje}", message.GetRawText());
                        continue;
                    }

                    var from = fromNode.GetString();
                    var incomingText = ExtractIncomingText(message);
                    var imageId = ExtractImageMediaId(message);
                    if (string.IsNullOrWhiteSpace(from) || (string.IsNullOrWhiteSpace(incomingText) && string.IsNullOrWhiteSpace(imageId)))
                    {
                        _logger.LogWarning(
                            "Mensaje descartado: sin texto ni imagen soportados. From: {From}, Tipo: {Tipo}",
                            from,
                            message.TryGetProperty("type", out var t) ? t.GetString() : "desconocido");
                        continue;
                    }

                    _logger.LogInformation(
                        "Mensaje aceptado. Número: {From}, Texto: {Texto}, ImagenMediaId: {ImageId}",
                        from, incomingText, imageId);

                    // TODO: Log temporal de diagnóstico. Eliminar al terminar las pruebas.
                    _logger.LogInformation(
                        "FROM recibido: {From} | BODY: {Body}",
                        from,
                        incomingText);

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

    /// <summary>
    /// Endpoint de prueba: envía un mensaje de texto al número indicado y
    /// devuelve el status code y el JSON completo devuelto por Meta.
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] SendWhatsAppMessageDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.To) || string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest(new { error = "Se requieren 'to' y 'message'." });
        }

        try
        {
            var (statusCode, responseBody) = await _whatsAppService.SendTextMessageRawAsync(dto.To, dto.Message, cancellationToken);

            return new ContentResult
            {
                StatusCode = StatusCodes.Status200OK,
                ContentType = "application/json",
                Content = $"{{\"metaStatusCode\":{statusCode},\"metaResponse\":{responseBody}}}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo el envío de prueba de WhatsApp a {To}.", dto.To);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Meta rechazó el mensaje o el envío falló.",
                detalle = ex.Message
            });
        }
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
