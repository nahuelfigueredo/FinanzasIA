using FinanzasIA.Core.Entities;

namespace FinanzasIA.Core.Interfaces;

public interface ICategoriaRepository
{
    Task<IReadOnlyCollection<Categoria>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<Categoria?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Categoria> AddAsync(Categoria categoria, CancellationToken cancellationToken = default);
    Task UpdateAsync(Categoria categoria, CancellationToken cancellationToken = default);
    Task DeleteAsync(Categoria categoria, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
