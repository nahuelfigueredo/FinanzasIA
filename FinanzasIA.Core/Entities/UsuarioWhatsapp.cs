using FinanzasIA.Core.Common;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Core.Entities;

/// <summary>
/// Vinculación entre un usuario de FinanzasIA y un número de teléfono de un
/// canal de mensajería (WhatsApp hoy; Telegram/Messenger en el futuro).
/// Un usuario puede tener múltiples números vinculados.
/// </summary>
public class UsuarioWhatsapp : BaseEntity
{
    /// <summary>Id del usuario de FinanzasIA (Identity) dueño del número.</summary>
    public string UsuarioId { get; set; } = string.Empty;

    /// <summary>Número de teléfono normalizado (solo dígitos).</summary>
    public string NumeroTelefono { get; set; } = string.Empty;

    /// <summary>Nombre descriptivo opcional (ej. "Mi celular").</summary>
    public string? Nombre { get; set; }

    /// <summary>Canal de mensajería al que pertenece el número.</summary>
    public CanalMensajeria Canal { get; set; } = CanalMensajeria.WhatsApp;

    /// <summary>Indica si el número está habilitado para operar.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Fecha de alta del vínculo.</summary>
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    /// <summary>Última vez que se recibió un mensaje desde este número.</summary>
    public DateTime? FechaUltimoUso { get; set; }
}
