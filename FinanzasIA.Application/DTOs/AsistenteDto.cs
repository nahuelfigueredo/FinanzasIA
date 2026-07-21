namespace FinanzasIA.Application.DTOs;

/// <summary>
/// Pregunta que el usuario envía al asistente financiero.
/// </summary>
public class AsistentePreguntaDto
{
    public string Pregunta { get; set; } = string.Empty;
}

/// <summary>
/// Respuesta generada por el asistente financiero.
/// </summary>
public class AsistenteRespuestaDto
{
    public string Respuesta { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Contexto financiero agregado del usuario. Es la "foto" de sus datos
/// que se le pasa al proveedor de IA para generar la respuesta.
/// </summary>
public class ContextoFinancieroDto
{
    public decimal IngresosMesActual { get; set; }
    public decimal EgresosMesActual { get; set; }
    public decimal IngresosMesAnterior { get; set; }
    public decimal EgresosMesAnterior { get; set; }
    public decimal BalanceTotal { get; set; }
    public int CantidadMovimientosMesActual { get; set; }
    public IReadOnlyCollection<GastoPorCategoriaDto> GastosPorCategoriaMesActual { get; set; } = [];
    public MovimientoResumenDto? MayorGastoMesActual { get; set; }
}

public class GastoPorCategoriaDto
{
    public string Categoria { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class MovimientoResumenDto
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
}
