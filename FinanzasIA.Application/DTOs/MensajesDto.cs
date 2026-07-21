using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.DTOs;

/// <summary>
/// Intenciones que el motor de mensajes puede detectar.
/// Para agregar una nueva intención: agregar el valor acá, registrar sus
/// patrones en el intérprete y su acción en el executor.
/// </summary>
public enum MessageIntent
{
    Desconocido = 0,
    RegistrarGasto = 1,
    RegistrarIngreso = 2,
    Transferencia = 3,
    ConsultaSaldo = 4,
    ResumenMensual = 5,
    ConsultaCategoria = 6,
    ConsultaPresupuesto = 7,
    Ayuda = 8,
    Saludo = 9,
    GastosHoy = 10,
    GastosMes = 11,
    IngresosMes = 12,
    UltimosMovimientos = 13
}

/// <summary>Canal de origen de un mensaje entrante.</summary>
public enum MessageOrigen
{
    WhatsApp = 0,
    Asistente = 1,
    Api = 2,
    Otro = 3
}

/// <summary>
/// Mensaje entrante crudo, independiente del canal por el que llegó.
/// </summary>
public class MensajeEntranteDto
{
    public string Texto { get; set; } = string.Empty;
    public MessageOrigen Origen { get; set; } = MessageOrigen.Otro;
    public string? UsuarioId { get; set; }
}

/// <summary>
/// Resultado de la interpretación de un mensaje: intención detectada más los
/// datos extraídos del texto. Es el contrato entre el intérprete (reglas u
/// OpenAI) y el executor: ninguno conoce al otro.
/// </summary>
public class MensajeInterpretadoDto
{
    public MessageIntent Intent { get; set; } = MessageIntent.Desconocido;

    /// <summary>Tipo de movimiento a registrar (si la intención lo requiere).</summary>
    public TipoMovimiento? TipoMovimiento { get; set; }

    /// <summary>Monto extraído del texto (null si no se pudo identificar).</summary>
    public decimal? Monto { get; set; }

    /// <summary>Nombre de categoría detectado en el texto (sin resolver contra la BD).</summary>
    public string? Categoria { get; set; }

    /// <summary>Nombre de cuenta detectado en el texto (sin resolver contra la BD).</summary>
    public string? Cuenta { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Fecha del movimiento; si el texto no la especifica se usa la actual.</summary>
    public DateTime Fecha { get; set; } = DateTime.Now;
}

/// <summary>
/// Resultado final del procesamiento de un mensaje: respuesta en lenguaje
/// natural más metadatos para el canal que lo invocó.
/// </summary>
public class MensajeProcesadoResultDto
{
    public MessageIntent Intent { get; set; }

    /// <summary>Respuesta en lenguaje natural para devolver al usuario.</summary>
    public string Respuesta { get; set; } = string.Empty;

    /// <summary>Indica si el procesamiento terminó con éxito (acción ejecutada o consulta respondida).</summary>
    public bool Exito { get; set; }

    /// <summary>Id del movimiento creado, si la acción generó uno.</summary>
    public int? MovimientoId { get; set; }

    /// <summary>Indica si el motor pudo manejar el mensaje. Si es false, el canal puede continuar con su flujo propio (por ejemplo la IA del Asistente).</summary>
    public bool Manejado { get; set; }
}

/// <summary>
/// Registro histórico de un mensaje procesado, para el Centro de Mensajes.
/// </summary>
public class MensajeLogDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public MessageOrigen Origen { get; set; }
    public MessageIntent Intent { get; set; }
    public bool Exito { get; set; }
    public int? MovimientoId { get; set; }
    public string Respuesta { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public long DuracionMs { get; set; }
}
