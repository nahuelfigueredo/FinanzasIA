using System.Text.Json.Serialization;

namespace FinanzasIA.Application.DTOs;

/// <summary>Tipo de visualización de un gráfico.</summary>
public enum ChartKind
{
    Bar = 0,
    Pie = 1,
    Line = 2,
    Area = 3,
    Column = 4,

    /// <summary>Reservado para futuras versiones (el frontend hace fallback a barras).</summary>
    Radar = 5
}

/// <summary>Tipo de acción interactiva de un <see cref="ActionItemDto"/>.</summary>
public enum ActionKind
{
    /// <summary>Navega a una ruta de la aplicación (Payload = ruta).</summary>
    Navegar = 0,

    /// <summary>Envía un mensaje al asistente (Payload = texto del mensaje).</summary>
    EnviarMensaje = 1
}

/// <summary>
/// Bloque base de una respuesta enriquecida del asistente. Toda respuesta es una
/// colección de bloques que el frontend renderiza de forma polimórfica.
/// El discriminador JSON <c>Type</c> permite agregar nuevos bloques sin romper
/// la arquitectura existente.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlockDto), "Text")]
[JsonDerivedType(typeof(ChartBlockDto), "Chart")]
[JsonDerivedType(typeof(TableBlockDto), "Table")]
[JsonDerivedType(typeof(CardBlockDto), "Card")]
[JsonDerivedType(typeof(SuggestionBlockDto), "Suggestion")]
[JsonDerivedType(typeof(ActionBlockDto), "Action")]
public abstract class ResponseBlockDto
{
    /// <summary>Nombre del tipo de bloque (Text, Chart, Table, Card, Suggestion, Action).</summary>
    [JsonIgnore]
    public abstract string Type { get; }
}

/// <summary>Bloque de texto (soporta Markdown).</summary>
public class TextBlockDto : ResponseBlockDto
{
    public override string Type => "Text";
    public string Texto { get; set; } = string.Empty;
}

/// <summary>Bloque de gráfico con datos reales.</summary>
public class ChartBlockDto : ResponseBlockDto
{
    public override string Type => "Chart";

    public ChartKind TipoGrafico { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Subtitulo { get; set; }
    public List<string> Etiquetas { get; set; } = [];
    public List<decimal> Valores { get; set; } = [];

    /// <summary>Serie secundaria opcional (ej. egresos junto a ingresos).</summary>
    public List<decimal>? ValoresSecundarios { get; set; }
    public string? EtiquetaSerie { get; set; }
    public string? EtiquetaSerieSecundaria { get; set; }

    /// <summary>Colores CSS opcionales; el frontend usa su paleta por defecto.</summary>
    public List<string>? Colores { get; set; }
    public string Periodo { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string? Descripcion { get; set; }
}

/// <summary>Bloque de tabla de datos.</summary>
public class TableBlockDto : ResponseBlockDto
{
    public override string Type => "Table";

    public string? Titulo { get; set; }
    public List<string> Columnas { get; set; } = [];
    public List<List<string>> Filas { get; set; } = [];

    /// <summary>Fila de totales opcional (misma cantidad de celdas que columnas).</summary>
    public List<string>? Totales { get; set; }
}

/// <summary>Bloque de tarjeta de métrica destacada.</summary>
public class CardBlockDto : ResponseBlockDto
{
    public override string Type => "Card";

    public string Titulo { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string? Icono { get; set; }

    /// <summary>Color semántico CSS opcional (ej. #22c55e).</summary>
    public string? Color { get; set; }

    /// <summary>Variación textual opcional (ej. "+12% vs mes anterior").</summary>
    public string? Variacion { get; set; }

    /// <summary>Descripción secundaria (usada también para tarjetas de "sin datos").</summary>
    public string? Descripcion { get; set; }
}

/// <summary>Bloque de sugerencias para continuar la conversación.</summary>
public class SuggestionBlockDto : ResponseBlockDto
{
    public override string Type => "Suggestion";

    /// <summary>Cada sugerencia se envía como nuevo mensaje al asistente.</summary>
    public List<string> Sugerencias { get; set; } = [];
}

/// <summary>Bloque de botones de acción interactivos.</summary>
public class ActionBlockDto : ResponseBlockDto
{
    public override string Type => "Action";
    public List<ActionItemDto> Acciones { get; set; } = [];
}

/// <summary>Acción interactiva individual dentro de un <see cref="ActionBlockDto"/>.</summary>
public class ActionItemDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public string? Icono { get; set; }
    public ActionKind Tipo { get; set; }

    /// <summary>Ruta de navegación o mensaje a enviar, según <see cref="Tipo"/>.</summary>
    public string Payload { get; set; } = string.Empty;
}
