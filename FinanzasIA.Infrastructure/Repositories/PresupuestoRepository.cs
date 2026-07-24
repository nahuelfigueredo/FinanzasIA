using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Repositories;

public class PresupuestoRepository : IPresupuestoRepository
{
    private readonly FinanzasDbContext _context;

    public PresupuestoRepository(FinanzasDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Presupuesto>> GetAllAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _context.Presupuestos
            .AsNoTracking()
            .Include(x => x.Categoria)
            .Where(x => x.UsuarioId == usuarioId)
            .OrderByDescending(x => x.Año)
            .ThenByDescending(x => x.Mes)
            .ThenBy(x => x.Categoria!.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<Presupuesto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Presupuestos
            .Include(x => x.Categoria)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Presupuesto?> GetVigenteAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken = default)
    {
        return await _context.Presupuestos
            .Include(x => x.Categoria)
            .FirstOrDefaultAsync(
                x => x.UsuarioId == usuarioId && x.CategoriaId == categoriaId &&
                     x.Mes == mes && x.Año == año && x.Activo,
                cancellationToken);
    }

    public async Task<Presupuesto> AddAsync(Presupuesto presupuesto, CancellationToken cancellationToken = default)
    {
        await _context.Presupuestos.AddAsync(presupuesto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return presupuesto;
    }

    public async Task UpdateAsync(Presupuesto presupuesto, CancellationToken cancellationToken = default)
    {
        presupuesto.FechaModificacion = DateTime.UtcNow;
        _context.Presupuestos.Update(presupuesto);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Presupuesto presupuesto, CancellationToken cancellationToken = default)
    {
        _context.Presupuestos.Remove(presupuesto);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
