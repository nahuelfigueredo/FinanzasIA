using FinanzasIA.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FinanzasIA.Api.Controllers;

/// <summary>
/// Modo de prueba/administración de la integración con WhatsApp.
/// Permite ver los últimos eventos (webhook real o simulado) con su JSON,
/// resultado de OCR y movimiento generado, simular mensajes/tickets sin usar
/// WhatsApp y reprocesar manualmente un evento anterior.
/// </summary>
[ApiController]
[Route("api/whatsapp/admin")]
public class WhatsAppAdminController : ControllerBase
{
    private readonly WhatsAppDiagnosticsStore _diagnostics;
    private readonly WhatsAppMessageHandler _messageHandler;

    public WhatsAppAdminController(WhatsAppDiagnosticsStore diagnostics, WhatsAppMessageHandler messageHandler)
    {
        _diagnostics = diagnostics;
        _messageHandler = messageHandler;
    }

    /// <summary>Últimos eventos de WhatsApp con todo su detalle diagnóstico.</summary>
    [HttpGet("eventos")]
    public IActionResult GetEventos([FromQuery] int cantidad = 50) =>
        Ok(_diagnostics.GetUltimos(cantidad));

    /// <summary>Detalle de un evento puntual (incluye JSON crudo de Meta).</summary>
    [HttpGet("eventos/{id:int}")]
    public IActionResult GetEvento(int id)
    {
        var entry = _diagnostics.GetPorId(id);
        return entry is null ? NotFound() : Ok(entry);
    }

    /// <summary>
    /// Simula la llegada de un mensaje de texto como si viniera de WhatsApp.
    /// Ejecuta exactamente el mismo pipeline que el webhook real, pero sin
    /// enviar la respuesta por WhatsApp (se devuelve en el resultado).
    /// </summary>
    [HttpPost("simular-mensaje")]
    public async Task<IActionResult> SimularMensaje([FromBody] SimularMensajeDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Telefono) || string.IsNullOrWhiteSpace(dto.Texto))
        {
            return BadRequest(new { error = "Se requieren 'telefono' y 'texto'." });
        }

        var entry = await _messageHandler.ProcesarAsync(
            dto.Telefono, dto.Texto, imageId: null,
            origen: "simulado",
            payloadJson: JsonSerializer.Serialize(dto),
            enviarRespuesta: dto.EnviarRespuestaReal,
            cancellationToken: cancellationToken);

        return Ok(entry);
    }

    /// <summary>
    /// Simula la llegada de una foto de ticket (imagen en base64) como si
    /// viniera de WhatsApp: pasa por OCR y crea el movimiento si es posible.
    /// </summary>
    [HttpPost("simular-ticket")]
    public async Task<IActionResult> SimularTicket([FromBody] SimularTicketDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Telefono) || string.IsNullOrWhiteSpace(dto.ImagenBase64))
        {
            return BadRequest(new { error = "Se requieren 'telefono' e 'imagenBase64'." });
        }

        byte[] contenido;
        try
        {
            contenido = Convert.FromBase64String(dto.ImagenBase64);
        }
        catch (FormatException)
        {
            return BadRequest(new { error = "'imagenBase64' no es base64 válido." });
        }

        var entry = await _messageHandler.ProcesarAsync(
            dto.Telefono, incomingText: null, imageId: null,
            origen: "simulado",
            payloadJson: $"{{\"simulacion\":\"ticket\",\"telefono\":\"{dto.Telefono}\",\"bytes\":{contenido.Length}}}",
            enviarRespuesta: dto.EnviarRespuestaReal,
            imagenContenido: contenido,
            imagenMimeType: string.IsNullOrWhiteSpace(dto.MimeType) ? "image/jpeg" : dto.MimeType,
            cancellationToken: cancellationToken);

        return Ok(entry);
    }

    /// <summary>
    /// Reprocesa manualmente un evento anterior (por ejemplo, uno que falló).
    /// Reutiliza el texto o media id originales; no reenvía la respuesta por
    /// WhatsApp salvo que se indique lo contrario.
    /// </summary>
    [HttpPost("reprocesar/{id:int}")]
    public async Task<IActionResult> Reprocesar(int id, [FromQuery] bool enviarRespuesta = false, CancellationToken cancellationToken = default)
    {
        var original = _diagnostics.GetPorId(id);
        if (original is null)
        {
            return NotFound(new { error = $"No existe el evento {id} (el registro es en memoria y se pierde al reiniciar)." });
        }

        if (string.IsNullOrWhiteSpace(original.Telefono) ||
            (string.IsNullOrWhiteSpace(original.TextoEntrante) && string.IsNullOrWhiteSpace(original.ImagenMediaId)))
        {
            return BadRequest(new { error = "El evento no tiene datos suficientes para reprocesarse." });
        }

        var entry = await _messageHandler.ProcesarAsync(
            original.Telefono, original.TextoEntrante, original.ImagenMediaId,
            origen: "reproceso",
            payloadJson: original.PayloadJson,
            enviarRespuesta: enviarRespuesta,
            cancellationToken: cancellationToken);

        return Ok(entry);
    }
}

public class SimularMensajeDto
{
    public string Telefono { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;

    /// <summary>Si es true, además envía la respuesta real por WhatsApp.</summary>
    public bool EnviarRespuestaReal { get; set; }
}

public class SimularTicketDto
{
    public string Telefono { get; set; } = string.Empty;
    public string ImagenBase64 { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public bool EnviarRespuestaReal { get; set; }
}
