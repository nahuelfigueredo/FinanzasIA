using FinanzasIA.Core.Entities;

namespace FinanzasIA.Core.Interfaces;

/// <summary>
/// Persistencia del historial de mensajes procesados por el motor de mensajes.
/// </summary>
public interface IMensajeProcesadoRepository
{
    Task<IReadOnlyCollection<MensajeProcesado>> GetUltimosAsync(string? usuarioId = null, int cantidad = 100, CancellationToken cancellationToken = default);
    Task<MensajeProcesado> AddAsync(MensajeProcesado mensaje, CancellationToken cancellationToken = default);
}
