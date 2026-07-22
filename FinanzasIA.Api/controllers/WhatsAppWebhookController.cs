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
    private readonly IMessageProcessor _messageProcessor;
    private readonly IUsuarioWhatsappService _usuarioWhatsappService;
    private readonly ITicketProcessor _ticketProcessor;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IOptions<WhatsAppOptions> options,
        IWhatsAppService whatsAppService,
        IMessageProcessor messageProcessor,
        IUsuarioWhatsappService usuarioWhatsappService,
        ITicketProcessor ticketProcessor,
        ILogger<WhatsAppWebhookController> logger)
    {
        _options = options.Value;
        _whatsAppService = whatsAppService;
        _messageProcessor = messageProcessor;
        _usuarioWhatsappService = usuarioWhatsappService;
        _ticketProcessor = ticketProcessor;
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
                    await ProcesarMensajeEntranteAsync(from, incomingText, imageId, cancellationToken);
                }
            }
        }

        return Ok();
    }

    /// <summary>
    /// Procesa un mensaje individual del webhook midiendo la duración de cada
    /// etapa. Ante cualquier excepción registra el error y responde un mensaje
    /// amigable: el mensaje recibido nunca se pierde silenciosamente.
    /// </summary>
    private async Task ProcesarMensajeEntranteAsync(string from, string? incomingText, string? imageId, CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        _logger.LogInformation("Webhook recibido de {Phone} (Imagen={EsImagen}).", from, imageId is not null);

        string response;
        try
        {
            var etapa = Stopwatch.StartNew();
            var usuarioId = await _usuarioWhatsappService.BuscarUsuarioPorNumeroAsync(from, cancellationToken: cancellationToken);
            _logger.LogInformation("Resolución de usuario en {Duracion} ms (Encontrado={Encontrado}).", etapa.ElapsedMilliseconds, usuarioId is not null);

            if (usuarioId is null)
            {
                _logger.LogInformation("Mensaje de WhatsApp recibido de número no vinculado: {Phone}.", from);
                response = "Tu número todavía no está vinculado a una cuenta de FinanzasIA.\n\nIngresá al sistema y vinculá tu número desde Configuración.";
            }
            else if (!string.IsNullOrWhiteSpace(imageId))
            {
                // Imagen recibida: procesarla como ticket/comprobante mediante OCR.
                etapa.Restart();
                response = await ProcesarImagenTicketAsync(imageId, usuarioId, cancellationToken);
                _logger.LogInformation("Procesamiento OCR en {Duracion} ms.", etapa.ElapsedMilliseconds);
            }
            else if (await _ticketProcessor.TienePendienteAsync(usuarioId, cancellationToken))
            {
                // Hay un ticket esperando un dato: la respuesta del usuario lo completa.
                var ticketResultado = await _ticketProcessor.CompletarDatoAsync(usuarioId, incomingText!, cancellationToken);
                response = ticketResultado.Respuesta;
            }
            else
            {
                etapa.Restart();
                var resultado = await _messageProcessor.ProcesarAsync(new MensajeEntranteDto
                {
                    Texto = incomingText!,
                    Origen = MessageOrigen.WhatsApp,
                    UsuarioId = usuarioId
                }, cancellationToken);
                _logger.LogInformation("Procesamiento del mensaje en {Duracion} ms.", etapa.ElapsedMilliseconds);

                response = resultado.Respuesta;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando el mensaje de {Phone}: {Texto}", from, incomingText ?? $"[imagen {imageId}]");
            response = "Ups, algo salió mal al procesar tu mensaje. Intentá de nuevo en unos segundos. 🙏";
        }

        if (!string.IsNullOrWhiteSpace(response))
        {
            await EnviarRespuestaAsync(from, response, cancellationToken);
        }

        _logger.LogInformation("Mensaje de {Phone} atendido en {Duracion} ms.", from, total.ElapsedMilliseconds);
    }

    /// <summary>Envía la respuesta al usuario; un fallo del envío solo se loguea.</summary>
    private async Task EnviarRespuestaAsync(string from, string response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            _logger.LogInformation("WhatsApp response not sent because credentials are missing. Response: {Response}", response);
            return;
        }

        try
        {
            await _whatsAppService.SendTextMessageAsync(from, response, cancellationToken);
            _logger.LogInformation("WhatsApp reply sent to {Phone}.", from);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar la respuesta de WhatsApp a {Phone}.", from);
        }
    }

    [HttpPost("send-test")]
    public async Task<IActionResult> SendTestMessage([FromBody] SendWhatsAppMessageDto dto, CancellationToken cancellationToken)
    {
        await _whatsAppService.SendTextMessageAsync(dto.To, dto.Message, cancellationToken);
        return Ok(new { message = "Mensaje enviado a WhatsApp." });
    }

    /// <summary>
    /// Descarga la imagen de WhatsApp y la procesa como ticket con OCR.
    /// Nunca lanza: ante cualquier error devuelve un mensaje amigable.
    /// </summary>
    private async Task<string> ProcesarImagenTicketAsync(string imageId, string usuarioId, CancellationToken cancellationToken)
    {
        try
        {
            var (contenido, mimeType) = await _whatsAppService.DownloadMediaAsync(imageId, cancellationToken);
            var resultado = await _ticketProcessor.ProcesarImagenAsync(new TicketImagenDto
            {
                Contenido = contenido,
                MimeType = mimeType,
                UsuarioId = usuarioId
            }, cancellationToken);

            return resultado.Respuesta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar imagen de ticket {MediaId} del usuario {UsuarioId}.", imageId, usuarioId);
            return "No pude procesar la imagen del ticket. 🙏\n\nIntentá de nuevo en unos segundos.";
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
