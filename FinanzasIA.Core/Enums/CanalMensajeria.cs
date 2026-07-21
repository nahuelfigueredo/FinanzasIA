namespace FinanzasIA.Core.Enums;

/// <summary>
/// Canal de mensajería vinculado a un usuario. Preparado para soportar
/// otros canales (Telegram, Messenger) en el futuro sin cambiar el modelo.
/// </summary>
public enum CanalMensajeria
{
    WhatsApp = 0,
    Telegram = 1,
    Messenger = 2
}
