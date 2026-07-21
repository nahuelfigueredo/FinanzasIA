using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Intérprete de mensajes: detecta la intención y extrae los datos del texto.
/// Es el único punto de acoplamiento con el motor de comprensión:
/// hoy <c>RuleBasedMessageInterpreter</c> (reglas + regex), en el futuro
/// <c>OpenAIMessageInterpreter</c>. Para cambiar de motor solo se cambia el
/// registro en Dependency Injection; nada más conoce la implementación.
/// </summary>
public interface IMessageInterpreter
{
    Task<MensajeInterpretadoDto> InterpretarAsync(string texto, CancellationToken cancellationToken = default);
}
