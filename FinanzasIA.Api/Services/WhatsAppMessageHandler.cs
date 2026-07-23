using FinanzasIA.Api.Options;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace FinanzasIA.Api.Services;

/// <summary>
/// Pipeline compartido de procesamiento de mensajes de WhatsApp:
/// resolución de usuario → texto/imagen → OCR/motor de mensajes → respuesta.
/// Lo usan tanto el webhook real como el modo de simulación del admin,
/// garantizando que ambos ejecutan exactamente la misma lógica.
/// </summary>
public class WhatsAppMessageHandler
{
    private readonly WhatsAppOptions _options;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IMessageProcessor _messageProcessor;
    private readonly IUsuarioWhatsappService _usuarioWhatsappService;
    private readonly ITicketProcessor _ticketProcessor;
    private readonly WhatsAppDiagnosticsStore _diagnostics;
    private readonly ILogger<WhatsAppMessageHandler> _logger;

    public WhatsAppMessageHandler(
        IOptions<WhatsAppOptions> options,
        IWhatsAppService whatsAppService,
        IMessageProcessor messageProcessor,
        IUsuarioWhatsappService usuarioWhatsappService,
        ITicketProcessor ticketProcessor,
        WhatsAppDiagnosticsStore diagnostics,
        ILogger<WhatsAppMessageHandler> logger)
    {
        _options = options.Value;
        _whatsAppService = whatsAppService;
        _messageProcessor = messageProcessor;
        _usuarioWhatsappService = usuarioWhatsappService;
        _ticketProcessor = ticketProcessor;
        _diagnostics = diagnostics;
        _logger = logger;
    }

    /// <summary>
    /// Procesa un mensaje entrante (texto o imagen ya descargable por mediaId).
    /// Nunca lanza: cualquier error queda registrado en el diagnóstico y se
    /// responde un mensaje amigable. Devuelve la entrada de diagnóstico.
    /// </summary>
    public async Task<WhatsAppDiagnosticEntry> ProcesarAsync(
        string from,
        string? incomingText,
        string? imageId,
        string origen,
        string? payloadJson,
        bool enviarRespuesta,
        byte[]? imagenContenido = null,
        string? imagenMimeType = null,
        CancellationToken cancellationToken = default)
    {
        var total = Stopwatch.StartNew();
        var entry = new WhatsAppDiagnosticEntry
        {
            Origen = origen,
            Telefono = from,
            PayloadJson = payloadJson,
            TextoEntrante = incomingText,
            ImagenMediaId = imageId
        };

        _logger.LogInformation("Mensaje de WhatsApp ({Origen}) de {Phone} (Imagen={EsImagen}).", origen, from, imageId is not null || imagenContenido is not null);

        string response;
        try
        {
            var usuarioId = await _usuarioWhatsappService.BuscarUsuarioPorNumeroAsync(from, cancellationToken: cancellationToken);
            entry.UsuarioId = usuarioId;
            _logger.LogInformation("Usuario resuelto para {Phone}: {UsuarioId}", from, usuarioId ?? "(no vinculado)");

            if (usuarioId is null)
            {
                _logger.LogInformation("Mensaje de WhatsApp recibido de número no vinculado: {Phone}.", from);
                response = "Tu número todavía no está vinculado a una cuenta de FinanzasIA.\n\nIngresá al sistema y vinculá tu número desde Configuración.";
            }
            else if (imagenContenido is not null || !string.IsNullOrWhiteSpace(imageId))
            {
                response = await ProcesarImagenTicketAsync(entry, imageId, imagenContenido, imagenMimeType, usuarioId, cancellationToken);
            }
            else if (await _ticketProcessor.TienePendienteAsync(usuarioId, cancellationToken))
            {
                var ticketResultado = await _ticketProcessor.CompletarDatoAsync(usuarioId, incomingText!, cancellationToken);
                entry.MovimientoId = ticketResultado.MovimientoId;
                response = ticketResultado.Respuesta;
            }
            else
            {
                var resultado = await _messageProcessor.ProcesarAsync(new MensajeEntranteDto
                {
                    Texto = incomingText!,
                    Origen = MessageOrigen.WhatsApp,
                    UsuarioId = usuarioId
                }, cancellationToken);

                entry.MovimientoId = resultado.MovimientoId;
                response = resultado.Respuesta;
                _logger.LogInformation(
                    "Resultado del procesamiento. Intent: {Intent}, Exito: {Exito}, MovimientoId: {MovimientoId}",
                    resultado.Intent, resultado.Exito, resultado.MovimientoId);
            }

            entry.Exito = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando el mensaje de {Phone}: {Texto}", from, incomingText ?? $"[imagen {imageId}]");
            entry.Exito = false;
            entry.Error = ex.Message;
            response = "Ups, algo salió mal al procesar tu mensaje. Intentá de nuevo en unos segundos. 🙏";
        }

        entry.Respuesta = response;

        if (enviarRespuesta && !string.IsNullOrWhiteSpace(response))
        {
            await EnviarRespuestaAsync(from, response, cancellationToken);
        }

        _diagnostics.Registrar(entry);
        _logger.LogInformation("Mensaje de {Phone} atendido en {Duracion} ms.", from, total.ElapsedMilliseconds);
        return entry;
    }

    private async Task<string> ProcesarImagenTicketAsync(
        WhatsAppDiagnosticEntry entry,
        string? imageId,
        byte[]? contenido,
        string? mimeType,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (contenido is null)
            {
                (contenido, mimeType) = await _whatsAppService.DownloadMediaAsync(imageId!, cancellationToken);
            }

            var resultado = await _ticketProcessor.ProcesarImagenAsync(new TicketImagenDto
            {
                Contenido = contenido,
                MimeType = mimeType ?? "image/jpeg",
                UsuarioId = usuarioId
            }, cancellationToken);

            entry.MovimientoId = resultado.MovimientoId;
            return resultado.Respuesta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar imagen de ticket {MediaId} del usuario {UsuarioId}.", imageId, usuarioId);
            entry.Error = ex.Message;
            return "No pude procesar la imagen del ticket. 🙏\n\nIntentá de nuevo en unos segundos.";
        }
    }

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
}
