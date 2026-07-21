using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de vínculos usuario ↔ número de mensajería.
/// </summary>
public class UsuarioWhatsappRepository : IUsuarioWhatsappRepository
{
    private readonly FinanzasDbContext _context;

    public UsuarioWhatsappRepository(FinanzasDbContext context)
    {
        _context = context;
    }

    public async Task<UsuarioWhatsapp?> BuscarPorNumeroAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default)
    {
        return await _context.UsuariosWhatsapp
            .FirstOrDefaultAsync(x => x.NumeroTelefono == numeroTelefono && x.Canal == canal, cancellationToken);
    }

    public async Task<IReadOnlyCollection<UsuarioWhatsapp>> BuscarPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _context.UsuariosWhatsapp
            .AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId)
            .OrderByDescending(x => x.FechaAlta)
            .ToListAsync(cancellationToken);
    }

    public async Task<UsuarioWhatsapp> AgregarAsync(UsuarioWhatsapp vinculo, CancellationToken cancellationToken = default)
    {
        _context.UsuariosWhatsapp.Add(vinculo);
        await _context.SaveChangesAsync(cancellationToken);
        return vinculo;
    }

    public async Task ActualizarAsync(UsuarioWhatsapp vinculo, CancellationToken cancellationToken = default)
    {
        vinculo.FechaModificacion = DateTime.UtcNow;
        _context.UsuariosWhatsapp.Update(vinculo);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        var vinculo = await _context.UsuariosWhatsapp.FindAsync([id], cancellationToken);
        if (vinculo is not null)
        {
            _context.UsuariosWhatsapp.Remove(vinculo);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExisteNumeroAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default)
    {
        return await _context.UsuariosWhatsapp
            .AnyAsync(x => x.NumeroTelefono == numeroTelefono && x.Canal == canal, cancellationToken);
    }
}
