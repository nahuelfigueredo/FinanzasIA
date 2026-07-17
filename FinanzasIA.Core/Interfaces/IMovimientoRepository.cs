using FinanzasIA.Core.Entities;

namespace FinanzasIA.Core.Interfaces;

public interface IMovimientoRepository
{
    Task<IReadOnlyCollection<Movimiento>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<Movimiento?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Movimiento> AddAsync(Movimiento movimiento, CancellationToken cancellationToken = default);
    Task UpdateAsync(Movimiento movimiento, CancellationToken cancellationToken = default);
    Task DeleteAsync(Movimiento movimiento, CancellationToken cancellationToken = default);
}
