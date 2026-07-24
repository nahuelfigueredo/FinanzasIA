using FinanzasIA.Core.Entities;

namespace FinanzasIA.Core.Interfaces;

public interface IPresupuestoRepository
{
    Task<IReadOnlyCollection<Presupuesto>> GetAllAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<Presupuesto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Presupuesto?> GetVigenteAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken = default);
    Task<Presupuesto> AddAsync(Presupuesto presupuesto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Presupuesto presupuesto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Presupuesto presupuesto, CancellationToken cancellationToken = default);
}
