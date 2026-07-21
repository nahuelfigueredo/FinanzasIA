using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Repositories;

/// <summary>
/// Implementación EF Core de <see cref="IMensajeProcesadoRepository"/>.
/// </summary>
public class MensajeProcesadoRepository : IMensajeProcesadoRepository
{
    private readonly FinanzasDbContext _context;

    public MensajeProcesadoRepository(FinanzasDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<MensajeProcesado>> GetUltimosAsync(string? usuarioId = null, int cantidad = 100, CancellationToken cancellationToken = default)
    {
        var query = _context.MensajesProcesados.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(usuarioId))
        {
            query = query.Where(x => x.UsuarioId == usuarioId || x.UsuarioId == null);
        }

        return await query
            .OrderByDescending(x => x.FechaCreacion)
            .Take(cantidad)
            .ToListAsync(cancellationToken);
    }

    public async Task<MensajeProcesado> AddAsync(MensajeProcesado mensaje, CancellationToken cancellationToken = default)
    {
        await _context.MensajesProcesados.AddAsync(mensaje, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return mensaje;
    }
}
