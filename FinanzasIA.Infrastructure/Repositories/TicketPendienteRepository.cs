using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de tickets pendientes.
/// </summary>
public class TicketPendienteRepository : ITicketPendienteRepository
{
    private readonly FinanzasDbContext _context;

    public TicketPendienteRepository(FinanzasDbContext context)
    {
        _context = context;
    }

    public async Task<TicketPendiente?> ObtenerActivoAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _context.TicketsPendientes
            .Where(x => x.UsuarioId == usuarioId && x.Activo)
            .OrderByDescending(x => x.FechaCreacion)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TicketPendiente> AgregarAsync(TicketPendiente ticket, CancellationToken cancellationToken = default)
    {
        _context.TicketsPendientes.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task ActualizarAsync(TicketPendiente ticket, CancellationToken cancellationToken = default)
    {
        ticket.FechaModificacion = DateTime.UtcNow;
        _context.TicketsPendientes.Update(ticket);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
