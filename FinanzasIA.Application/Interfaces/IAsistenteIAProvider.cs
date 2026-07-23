using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Abstracción del proveedor de IA que genera la respuesta en lenguaje natural.
/// La implementación por defecto usa reglas locales; para integrar OpenAI
/// alcanza con crear otra implementación (por ejemplo OpenAIAsistenteProvider)
/// y cambiar el registro en la inyección de dependencias.
/// </summary>
public interface IAsistenteIAProvider
{
    Task<string> GenerarRespuestaAsync(
        string pregunta,
        ContextoFinancieroDto contexto,
        IReadOnlyCollection<AsistenteMensajeDto>? historial = null,
        CancellationToken cancellationToken = default);
}
