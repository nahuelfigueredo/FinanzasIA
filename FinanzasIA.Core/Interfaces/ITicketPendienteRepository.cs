using FinanzasIA.Core.Entities;

namespace FinanzasIA.Core.Interfaces;

/// <summary>
/// Persistencia de tickets pendientes de completar datos.
/// </summary>
public interface ITicketPendienteRepository
{
    /// <summary>Devuelve el ticket activo más reciente del usuario, si lo hay.</summary>
    Task<TicketPendiente?> ObtenerActivoAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<TicketPendiente> AgregarAsync(TicketPendiente ticket, CancellationToken cancellationToken = default);
    Task ActualizarAsync(TicketPendiente ticket, CancellationToken cancellationToken = default);
}
