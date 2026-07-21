using FinanzasIA.Core.Common;

namespace FinanzasIA.Core.Entities;

/// <summary>
/// Registro histórico de un mensaje procesado por el motor de mensajes.
/// Alimenta el "Centro de Mensajes" del Backoffice.
/// </summary>
public class MensajeProcesado : BaseEntity
{
    /// <summary>Texto original recibido del usuario.</summary>
    public string Texto { get; set; } = string.Empty;

    /// <summary>Canal de origen (WhatsApp, Asistente, Api, Otro) — valor del enum MessageOrigen.</summary>
    public int Origen { get; set; }

    /// <summary>Intención detectada — valor del enum MessageIntent.</summary>
    public int Intent { get; set; }

    /// <summary>Indica si la acción se ejecutó con éxito.</summary>
    public bool Exito { get; set; }

    /// <summary>Id del movimiento creado, si corresponde.</summary>
    public int? MovimientoId { get; set; }

    /// <summary>Respuesta enviada al usuario.</summary>
    public string Respuesta { get; set; } = string.Empty;

    /// <summary>Duración del procesamiento en milisegundos.</summary>
    public long DuracionMs { get; set; }

    public string? UsuarioId { get; set; }
}
