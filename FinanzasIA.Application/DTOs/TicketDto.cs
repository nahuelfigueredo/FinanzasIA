namespace FinanzasIA.Application.DTOs;

/// <summary>Imagen de un ticket recibida desde un canal de mensajería.</summary>
public class TicketImagenDto
{
    /// <summary>Contenido binario de la imagen.</summary>
    public byte[] Contenido { get; set; } = [];

    /// <summary>Tipo MIME de la imagen (image/jpeg, image/png).</summary>
    public string MimeType { get; set; } = "image/jpeg";

    /// <summary>Id del usuario dueño del ticket.</summary>
    public string? UsuarioId { get; set; }
}

/// <summary>Datos extraídos de un ticket por OCR + parsing.</summary>
public class TicketDatosDto
{
    public decimal? Monto { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Comercio { get; set; }
    public string TextoCompleto { get; set; } = string.Empty;
}

/// <summary>Resultado del procesamiento de un ticket (o de un dato faltante).</summary>
public class TicketResultDto
{
    /// <summary>Respuesta en lenguaje natural para el usuario.</summary>
    public string Respuesta { get; set; } = string.Empty;

    /// <summary>Indica si el movimiento quedó registrado.</summary>
    public bool MovimientoCreado { get; set; }

    /// <summary>Id del movimiento creado, si corresponde.</summary>
    public int? MovimientoId { get; set; }

    /// <summary>Indica si se está esperando un dato del usuario.</summary>
    public bool EsperandoDato { get; set; }
}
