using FinanzasIA.Application.DTOs;
using FinanzasIA.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

/// <summary>
/// Expone el historial de mensajes procesados por el motor de mensajes,
/// para el Centro de Mensajes del Backoffice.
/// </summary>
[ApiController]
[Route("api/mensajes")]
public class MensajesController : ControllerBase
{
    private readonly IMensajeProcesadoRepository _repository;

    public MensajesController(IMensajeProcesadoRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<MensajeLogDto>>> GetUltimos([FromQuery] int cantidad = 100, CancellationToken cancellationToken = default)
    {
        var userId = HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;
        var mensajes = await _repository.GetUltimosAsync(userId, cantidad, cancellationToken);

        var dtos = mensajes.Select(m => new MensajeLogDto
        {
            Id = m.Id,
            Texto = m.Texto,
            Origen = (MessageOrigen)m.Origen,
            Intent = (MessageIntent)m.Intent,
            Exito = m.Exito,
            MovimientoId = m.MovimientoId,
            Respuesta = m.Respuesta,
            Fecha = m.FechaCreacion,
            DuracionMs = m.DuracionMs
        }).ToList();

        return Ok(dtos);
    }
}
