namespace FinanzasIA.Application.DTOs;

/// <summary>
/// Resultado del análisis completo de las finanzas del usuario generado por
/// <c>FinanzasAnalyzer</c>. Contiene todas las métricas necesarias para que
/// <c>SugerenciasService</c> (o en el futuro un proveedor OpenAI) genere
/// sugerencias sin volver a consultar la base de datos.
/// </summary>
public class AnalisisFinancieroCompletoDto
{
    /// <summary>Balance histórico total (ingresos - egresos de todos los tiempos).</summary>
    public decimal BalanceTotal { get; set; }

    /// <summary>Ingresos del mes en curso.</summary>
    public decimal IngresosMes { get; set; }

    /// <summary>Gastos (egresos) del mes en curso.</summary>
    public decimal GastosMes { get; set; }

    /// <summary>Balance del mes en curso (ingresos - gastos).</summary>
    public decimal BalanceMensual { get; set; }

    /// <summary>Gastos del mes agrupados por categoría, ordenados de mayor a menor.</summary>
    public IReadOnlyCollection<GastoPorCategoriaDto> GastosPorCategoria { get; set; } = [];

    /// <summary>Nombre de la categoría con mayor gasto del mes (null si no hay gastos).</summary>
    public string? CategoriaMayorGasto { get; set; }

    /// <summary>Categoría cuyo gasto más creció respecto al mes anterior (null si no aplica).</summary>
    public CrecimientoCategoriaDto? CategoriaMayorCrecimiento { get; set; }

    /// <summary>Promedio de gasto diario del mes en curso (gastos / días transcurridos).</summary>
    public decimal PromedioGastoDiario { get; set; }

    /// <summary>Mayor ingreso del mes en curso (null si no hay ingresos).</summary>
    public MovimientoResumenDto? MayorIngreso { get; set; }

    /// <summary>Mayor gasto del mes en curso (null si no hay gastos).</summary>
    public MovimientoResumenDto? MayorGasto { get; set; }

    /// <summary>Cantidad de movimientos del mes en curso.</summary>
    public int CantidadMovimientos { get; set; }

    /// <summary>Gastos del mes anterior (para calcular variación).</summary>
    public decimal GastosMesAnterior { get; set; }

    /// <summary>Variación porcentual de gastos respecto al mes anterior (null si el mes anterior no tiene gastos).</summary>
    public decimal? VariacionGastosPorcentaje { get; set; }

    /// <summary>Presupuesto mensual configurado por el usuario (null si no existe).</summary>
    public decimal? PresupuestoMensual { get; set; }

    /// <summary>Porcentaje del presupuesto utilizado en el mes (null si no hay presupuesto).</summary>
    public decimal? PresupuestoUtilizadoPorcentaje { get; set; }
}

/// <summary>
/// Crecimiento de gasto de una categoría respecto al mes anterior.
/// </summary>
public class CrecimientoCategoriaDto
{
    public string Categoria { get; set; } = string.Empty;
    public decimal GastoMesActual { get; set; }
    public decimal GastoMesAnterior { get; set; }

    /// <summary>Crecimiento porcentual respecto al mes anterior.</summary>
    public decimal CrecimientoPorcentaje { get; set; }
}
