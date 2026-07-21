using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Generador de sugerencias inteligentes. Recibe el análisis financiero ya
/// calculado y devuelve una lista de sugerencias ordenadas por prioridad.
/// Es el punto de extensión para OpenAI: una futura implementación
/// (por ejemplo <c>OpenAISugerenciasService</c>) puede enriquecer o reemplazar
/// las sugerencias basadas en reglas sin modificar el analyzer, el controller
/// ni la interfaz de usuario.
/// </summary>
public interface ISugerenciasService
{
    Task<IReadOnlyCollection<SugerenciaDto>> GenerarSugerenciasAsync(AnalisisFinancieroCompletoDto analisis, CancellationToken cancellationToken = default);
}
