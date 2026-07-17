using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

public interface IAnalisisFinancieroService
{
    Task<AnalisisFinancieroDto> ObtenerAnalisisAsync(string? usuarioId = null, CancellationToken cancellationToken = default);
}
