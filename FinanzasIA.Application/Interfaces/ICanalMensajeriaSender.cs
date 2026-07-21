using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Abstracción para enviar mensajes salientes por un canal de mensajería.
/// Application no conoce la implementación concreta (WhatsApp Cloud API,
/// Telegram Bot API, etc.); la capa Api registra el adapter correspondiente.
/// </summary>
public interface ICanalMensajeriaSender
{
    /// <summary>Canal que este sender sabe manejar.</summary>
    CanalMensajeria Canal { get; }

    /// <summary>Envía un mensaje de texto al número indicado.</summary>
    Task EnviarTextoAsync(string numeroTelefono, string mensaje, CancellationToken cancellationToken = default);
}
