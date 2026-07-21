using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Servicio de negocio del Asistente Financiero: arma el contexto con datos
/// del usuario y delega la generación de la respuesta al proveedor de IA.
/// </summary>
public interface IAsistenteService
{
    Task<AsistenteRespuestaDto> PreguntarAsync(string pregunta, string? usuarioId = null, CancellationToken cancellationToken = default);
}
