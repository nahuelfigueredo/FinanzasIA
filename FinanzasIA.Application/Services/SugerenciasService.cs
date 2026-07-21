using System.Globalization;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Implementación por reglas de negocio de <see cref="ISugerenciasService"/>.
/// Recibe el <see cref="AnalisisFinancieroCompletoDto"/> ya calculado y evalúa
/// una serie de reglas; cada regla que aplica agrega una sugerencia.
/// No consulta la base de datos ni servicios externos, lo que la hace
/// determinística y fácil de testear. En el futuro, OpenAI podrá enriquecer
/// o reemplazar esta implementación registrando otro ISugerenciasService en DI.
/// </summary>
public class SugerenciasService : ISugerenciasService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    public Task<IReadOnlyCollection<SugerenciaDto>> GenerarSugerenciasAsync(AnalisisFinancieroCompletoDto analisis, CancellationToken cancellationToken = default)
    {
        var sugerencias = new List<SugerenciaDto>();

        if (analisis.CantidadMovimientos == 0)
        {
            sugerencias.Add(new SugerenciaDto
            {
                Titulo = "Sin movimientos este mes",
                Mensaje = "Todavía no registraste movimientos este mes. Cargá tus ingresos y gastos para recibir sugerencias personalizadas.",
                Tipo = TipoSugerencia.Info,
                Icono = "📭",
                Prioridad = 3
            });
            return Task.FromResult<IReadOnlyCollection<SugerenciaDto>>(sugerencias);
        }

        // Regla: presupuesto casi agotado o superado.
        if (analisis.PresupuestoUtilizadoPorcentaje is >= 90)
        {
            var pct = analisis.PresupuestoUtilizadoPorcentaje.Value;
            sugerencias.Add(new SugerenciaDto
            {
                Titulo = pct >= 100 ? "Presupuesto superado" : "Presupuesto casi agotado",
                Mensaje = $"Ya utilizaste el {pct:0}% de tu presupuesto mensual de {Moneda(analisis.PresupuestoMensual!.Value)}.",
                Tipo = TipoSugerencia.Advertencia,
                Icono = pct >= 100 ? "⛔" : "⚠️",
                Prioridad = 1
            });
        }

        // Regla: balance mensual negativo.
        if (analisis.BalanceMensual < 0)
        {
            sugerencias.Add(new SugerenciaDto
            {
                Titulo = "Balance negativo",
                Mensaje = $"Este mes tus gastos ({Moneda(analisis.GastosMes)}) superan a tus ingresos ({Moneda(analisis.IngresosMes)}).",
                Tipo = TipoSugerencia.Advertencia,
                Icono = "📉",
                Prioridad = 1
            });
        }

        // Regla: los gastos aumentaron más del 20% respecto al mes anterior.
        if (analisis.VariacionGastosPorcentaje is > 20)
        {
            sugerencias.Add(new SugerenciaDto
            {
                Titulo = "Gastos en aumento",
                Mensaje = $"Este mes tus gastos aumentaron un {analisis.VariacionGastosPorcentaje.Value:0}% respecto al anterior ({Moneda(analisis.GastosMesAnterior)} → {Moneda(analisis.GastosMes)}).",
                Tipo = TipoSugerencia.Advertencia,
                Icono = "📈",
                Prioridad = 2
            });
        }

        // Regla: una categoría concentra más del 40% del gasto total.
        var categoriaTop = analisis.GastosPorCategoria.FirstOrDefault();
        if (categoriaTop is not null && analisis.GastosMes > 0)
        {
            var participacion = categoriaTop.Total / analisis.GastosMes * 100;
            if (participacion > 40)
            {
                sugerencias.Add(new SugerenciaDto
                {
                    Titulo = "Gasto concentrado",
                    Mensaje = $"La categoría {categoriaTop.Categoria} representa el {participacion:0}% de todos tus gastos ({Moneda(categoriaTop.Total)}).",
                    Tipo = TipoSugerencia.Info,
                    Icono = "🎯",
                    Prioridad = 2
                });
            }
        }

        // Regla: una categoría creció más del 30% respecto al mes anterior.
        if (analisis.CategoriaMayorCrecimiento is { CrecimientoPorcentaje: > 30 } crecimiento)
        {
            sugerencias.Add(new SugerenciaDto
            {
                Titulo = "Categoría en crecimiento",
                Mensaje = $"{crecimiento.Categoria} aumentó un {crecimiento.CrecimientoPorcentaje:0}% respecto al mes pasado ({Moneda(crecimiento.GastoMesAnterior)} → {Moneda(crecimiento.GastoMesActual)}).",
                Tipo = TipoSugerencia.Advertencia,
                Icono = "🚀",
                Prioridad = 2
            });
        }

        // Regla: promedio de gasto diario alto en relación a los ingresos del mes.
        if (analisis.IngresosMes > 0)
        {
            var diasDelMes = DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month);
            var promedioDiarioSostenible = analisis.IngresosMes / diasDelMes;
            if (analisis.PromedioGastoDiario > promedioDiarioSostenible)
            {
                sugerencias.Add(new SugerenciaDto
                {
                    Titulo = "Gasto diario elevado",
                    Mensaje = $"Tu gasto promedio diario es de {Moneda(analisis.PromedioGastoDiario)}, por encima del ritmo sostenible según tus ingresos ({Moneda(promedioDiarioSostenible)} por día).",
                    Tipo = TipoSugerencia.Info,
                    Icono = "📅",
                    Prioridad = 3
                });
            }
        }

        // Regla: sin alertas -> mensaje positivo.
        if (sugerencias.Count == 0)
        {
            sugerencias.Add(new SugerenciaDto
            {
                Titulo = "Todo en orden",
                Mensaje = "Excelente. Tus gastos se encuentran dentro de los valores habituales.",
                Tipo = TipoSugerencia.Exito,
                Icono = "✅",
                Prioridad = 3
            });
        }

        return Task.FromResult<IReadOnlyCollection<SugerenciaDto>>(
            sugerencias.OrderBy(s => s.Prioridad).ToList());
    }

    private static string Moneda(decimal valor) => valor.ToString("C", Cultura);
}
