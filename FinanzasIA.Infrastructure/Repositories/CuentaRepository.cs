using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Repositories;

public class CuentaRepository : ICuentaRepository
{
    private readonly FinanzasDbContext _context;

    public CuentaRepository(FinanzasDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Cuenta>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Cuentas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(usuarioId))
        {
            query = query.Where(x => x.UsuarioId == usuarioId || x.UsuarioId == null);
        }

        return await query
            .OrderBy(x => x.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<Cuenta?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Cuentas
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Cuenta> AddAsync(Cuenta cuenta, CancellationToken cancellationToken = default)
    {
        await _context.Cuentas.AddAsync(cuenta, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return cuenta;
    }

    public async Task UpdateAsync(Cuenta cuenta, CancellationToken cancellationToken = default)
    {
        cuenta.FechaModificacion = DateTime.UtcNow;
        _context.Cuentas.Update(cuenta);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Cuenta cuenta, CancellationToken cancellationToken = default)
    {
        _context.Cuentas.Remove(cuenta);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Cuentas.AnyAsync(x => x.Id == id, cancellationToken);
    }
}
