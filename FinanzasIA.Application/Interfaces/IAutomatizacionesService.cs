using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

public interface IAutomatizacionesService
{
    /// <summary>Obtiene la configuración del usuario, creando una con valores por defecto si no existe.</summary>
    Task<AutomatizacionesDto> GetAsync(string usuarioId, CancellationToken cancellationToken = default);

    Task<AutomatizacionesDto> GuardarAsync(string usuarioId, AutomatizacionesDto dto, CancellationToken cancellationToken = default);
}
