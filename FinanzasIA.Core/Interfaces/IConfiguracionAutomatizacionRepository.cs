using FinanzasIA.Core.Entities;

namespace FinanzasIA.Core.Interfaces;

public interface IConfiguracionAutomatizacionRepository
{
    Task<ConfiguracionAutomatizacion?> GetByUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default);

    Task<ConfiguracionAutomatizacion> AddAsync(ConfiguracionAutomatizacion configuracion, CancellationToken cancellationToken = default);

    Task UpdateAsync(ConfiguracionAutomatizacion configuracion, CancellationToken cancellationToken = default);
}
