using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Ejecutor de acciones: recibe el mensaje ya interpretado y realiza la acción
/// correspondiente (crear movimiento, responder consultas, etc.), resolviendo
/// categorías y cuentas contra la base de datos y generando la respuesta en
/// lenguaje natural. No sabe cómo se interpretó el mensaje ni por qué canal llegó.
/// </summary>
public interface IMessageActionExecutor
{
    Task<MensajeProcesadoResultDto> EjecutarAsync(MensajeInterpretadoDto interpretado, string? usuarioId, CancellationToken cancellationToken = default);
}
