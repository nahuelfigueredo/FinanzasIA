using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Repositories;

public class MovimientoRepository : IMovimientoRepository
{
    private readonly FinanzasDbContext _context;

    public MovimientoRepository(FinanzasDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Movimiento>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Movimientos
            .AsNoTracking()
            .Include(x => x.Categoria)
            .Include(x => x.Cuenta)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(usuarioId))
        {
            query = query.Where(x => x.UsuarioId == usuarioId);
        }

        return await query
            .OrderByDescending(x => x.Fecha)
            .ToListAsync(cancellationToken);
    }

    public async Task<Movimiento?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Movimientos
            .Include(x => x.Categoria)
            .Include(x => x.Cuenta)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Movimiento> AddAsync(Movimiento movimiento, CancellationToken cancellationToken = default)
    {
        await _context.Movimientos.AddAsync(movimiento, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(movimiento)
            .Reference(x => x.Categoria)
            .LoadAsync(cancellationToken);

        return movimiento;
    }

    public async Task UpdateAsync(Movimiento movimiento, CancellationToken cancellationToken = default)
    {
        movimiento.FechaModificacion = DateTime.UtcNow;
        _context.Movimientos.Update(movimiento);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(movimiento)
            .Reference(x => x.Categoria)
            .LoadAsync(cancellationToken);
    }

    public async Task DeleteAsync(Movimiento movimiento, CancellationToken cancellationToken = default)
    {
        _context.Movimientos.Remove(movimiento);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
