using FinanzasIA.Core.Common;

namespace FinanzasIA.Core.Entities;

/// <summary>
/// Ticket procesado por OCR al que le falta algún dato (importe, fecha o
/// comercio) para poder registrarse como movimiento. Guarda el estado de la
/// conversación hasta que el usuario complete el dato faltante.
/// </summary>
public class TicketPendiente : BaseEntity
{
    /// <summary>Id del usuario de FinanzasIA dueño del ticket.</summary>
    public string UsuarioId { get; set; } = string.Empty;

    /// <summary>Texto completo extraído por OCR (para auditoría y re-parseo).</summary>
    public string TextoOcr { get; set; } = string.Empty;

    /// <summary>Importe detectado, si se detectó con confianza.</summary>
    public decimal? Monto { get; set; }

    /// <summary>Fecha detectada, si se detectó con confianza.</summary>
    public DateTime? Fecha { get; set; }

    /// <summary>Nombre del comercio detectado, si se detectó con confianza.</summary>
    public string? Comercio { get; set; }

    /// <summary>Categoría sugerida a partir del comercio/contenido.</summary>
    public string? CategoriaSugerida { get; set; }

    /// <summary>Dato que se le pidió al usuario: "monto", "fecha" o "comercio".</summary>
    public string? DatoSolicitado { get; set; }

    /// <summary>Indica si el ticket sigue esperando datos del usuario.</summary>
    public bool Activo { get; set; } = true;
}
