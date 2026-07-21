using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Procesamiento de tickets/comprobantes enviados como imagen:
/// OCR → extracción de datos → creación del movimiento, o solicitud del
/// dato faltante cuando la confianza no es suficiente.
/// </summary>
public interface ITicketProcessor
{
    /// <summary>Procesa la imagen de un ticket y registra el gasto o pide el dato faltante.</summary>
    Task<TicketResultDto> ProcesarImagenAsync(TicketImagenDto imagen, CancellationToken cancellationToken = default);

    /// <summary>Indica si el usuario tiene un ticket pendiente esperando un dato.</summary>
    Task<bool> TienePendienteAsync(string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Completa el dato faltante de un ticket pendiente con la respuesta del usuario.</summary>
    Task<TicketResultDto> CompletarDatoAsync(string usuarioId, string respuestaUsuario, CancellationToken cancellationToken = default);
}
