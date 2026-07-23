namespace FinanzasIA.Application.DTOs;

/// <summary>
/// Pregunta que el usuario envía al asistente financiero, junto con el
/// historial de la conversación para que la IA mantenga memoria.
/// </summary>
public class AsistentePreguntaDto
{
	public string Pregunta { get; set; } = string.Empty;

	/// <summary>Mensajes previos de la conversación (memoria de la IA).</summary>
	public List<AsistenteMensajeDto> Historial { get; set; } = [];
}

/// <summary>Un mensaje previo de la conversación.</summary>
public class AsistenteMensajeDto
{
	/// <summary>true si el mensaje es del usuario; false si es del asistente.</summary>
	public bool EsUsuario { get; set; }
	public string Texto { get; set; } = string.Empty;
}

/// <summary>Tipo de contenido devuelto por el asistente.</summary>
public enum AsistenteRespuestaTipo
{
	Text = 0,
	Chart = 1,
	Table = 2,
	Card = 3
}

/// <summary>Tipo de visualización del gráfico.</summary>
public enum TipoGrafico
{
	Barras = 0,
	Torta = 1,
	Lineas = 2
}

/// <summary>
/// Payload estructurado para renderizar un gráfico dentro del chat.
/// </summary>
public class GraficoDto
{
	public TipoGrafico Tipo { get; set; }
	public string Titulo { get; set; } = string.Empty;
	public string? Subtitulo { get; set; }
	public List<string> Etiquetas { get; set; } = [];
	public List<decimal> Valores { get; set; } = [];

	/// <summary>Serie secundaria opcional (ej. egresos en comparativa ingresos vs egresos).</summary>
	public List<decimal>? ValoresSecundarios { get; set; }
	public string? EtiquetaSerie { get; set; }
	public string? EtiquetaSerieSecundaria { get; set; }

	/// <summary>Colores en formato CSS (opcional, el frontend usa su paleta por defecto).</summary>
	public List<string>? Colores { get; set; }
	public decimal Total { get; set; }
	public string Periodo { get; set; } = string.Empty;
}

/// <summary>
/// Respuesta generada por el asistente financiero.
/// </summary>
public class AsistenteRespuestaDto
{
	public string Respuesta { get; set; } = string.Empty;
	public DateTime FechaUtc { get; set; } = DateTime.UtcNow;

	/// <summary>Indica si la respuesta incluyó la ejecución de una acción (ej. registrar un gasto).</summary>
	public bool AccionEjecutada { get; set; }

	/// <summary>Tipo de contenido de la respuesta (texto, gráfico, tabla o tarjeta).</summary>
	public AsistenteRespuestaTipo Tipo { get; set; } = AsistenteRespuestaTipo.Text;

	/// <summary>Datos del gráfico cuando <see cref="Tipo"/> es Chart.</summary>
	public GraficoDto? Grafico { get; set; }
}

/// <summary>
/// Contexto financiero agregado del usuario. Es la "foto" completa de sus datos
/// que se le pasa al proveedor de IA para generar la respuesta.
/// </summary>
public class ContextoFinancieroDto
{
	public decimal BalanceTotal { get; set; }

	public decimal IngresosMesActual { get; set; }
	public decimal EgresosMesActual { get; set; }
	public decimal IngresosMesAnterior { get; set; }
	public decimal EgresosMesAnterior { get; set; }

	public decimal GastosHoy { get; set; }
	public decimal GastosSemana { get; set; }
	public decimal GastosMes { get; set; }
	public decimal GastosAnio { get; set; }
	public decimal IngresosAnio { get; set; }

	public decimal PromedioDiarioGastoMesActual { get; set; }
	public decimal PromedioMensualGasto { get; set; }

	/// <summary>Variación porcentual de gastos respecto al mes anterior (positivo = aumentaron).</summary>
	public decimal? VariacionGastosVsMesAnteriorPorcentaje { get; set; }

	/// <summary>Proyección de gasto total del mes actual manteniendo el ritmo actual.</summary>
	public decimal ProyeccionGastoFinDeMes { get; set; }

	/// <summary>Presupuesto mensual configurado por el usuario, si existe.</summary>
	public decimal? PresupuestoMensual { get; set; }

	public int CantidadMovimientosMesActual { get; set; }

	public IReadOnlyCollection<GastoPorCategoriaDto> GastosPorCategoriaMesActual { get; set; } = [];
	public IReadOnlyCollection<GastoPorCategoriaDto> GastosPorCategoriaMesAnterior { get; set; } = [];

	/// <summary>Totales de gasto de los últimos 6 meses (para tendencias).</summary>
	public IReadOnlyCollection<TotalMensualDto> GastosUltimosMeses { get; set; } = [];

	public MovimientoResumenDto? MayorGastoMesActual { get; set; }

	/// <summary>Movimientos más recientes del usuario.</summary>
	public IReadOnlyCollection<MovimientoResumenDto> MovimientosRecientes { get; set; } = [];

	/// <summary>Alertas detectadas automáticamente (excesos, aumentos bruscos, etc.).</summary>
	public IReadOnlyCollection<string> Alertas { get; set; } = [];
}

public class GastoPorCategoriaDto
{
	public string Categoria { get; set; } = string.Empty;
	public decimal Total { get; set; }
}

public class TotalMensualDto
{
	public string Mes { get; set; } = string.Empty;
	public decimal Ingresos { get; set; }
	public decimal Egresos { get; set; }
}

public class MovimientoResumenDto
{
	public string Descripcion { get; set; } = string.Empty;
	public string? Categoria { get; set; }
	public string? Tipo { get; set; }
	public decimal Monto { get; set; }
	public DateTime Fecha { get; set; }
}
