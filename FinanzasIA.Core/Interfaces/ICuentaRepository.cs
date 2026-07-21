using FinanzasIA.Core.Entities;

namespace FinanzasIA.Core.Interfaces;

public interface ICuentaRepository
{
    Task<IReadOnlyCollection<Cuenta>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<Cuenta?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Cuenta> AddAsync(Cuenta cuenta, CancellationToken cancellationToken = default);
    Task UpdateAsync(Cuenta cuenta, CancellationToken cancellationToken = default);
    Task DeleteAsync(Cuenta cuenta, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
