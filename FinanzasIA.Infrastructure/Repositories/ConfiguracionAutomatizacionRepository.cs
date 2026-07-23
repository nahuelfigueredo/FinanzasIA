using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Infrastructure.Repositories;

public class ConfiguracionAutomatizacionRepository : IConfiguracionAutomatizacionRepository
{
    private readonly FinanzasDbContext _context;

    public ConfiguracionAutomatizacionRepository(FinanzasDbContext context)
    {
        _context = context;
    }

    public async Task<ConfiguracionAutomatizacion?> GetByUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _context.ConfiguracionesAutomatizacion
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, cancellationToken);
    }

    public async Task<ConfiguracionAutomatizacion> AddAsync(ConfiguracionAutomatizacion configuracion, CancellationToken cancellationToken = default)
    {
        await _context.ConfiguracionesAutomatizacion.AddAsync(configuracion, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return configuracion;
    }

    public async Task UpdateAsync(ConfiguracionAutomatizacion configuracion, CancellationToken cancellationToken = default)
    {
        configuracion.FechaModificacion = DateTime.UtcNow;
        _context.ConfiguracionesAutomatizacion.Update(configuracion);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
