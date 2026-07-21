using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Implementación de <see cref="IFinanzasAnalyzer"/>. Obtiene todos los
/// movimientos del usuario desde SQL Server (vía EF Core / repositorios) y
/// calcula las métricas del <see cref="AnalisisFinancieroCompletoDto"/>.
/// Responsabilidad única: números. No genera texto ni decide qué mostrar.
/// </summary>
public class FinanzasAnalyzer : IFinanzasAnalyzer
{
    private readonly IMovimientoRepository _movimientoRepository;

    public FinanzasAnalyzer(IMovimientoRepository movimientoRepository)
    {
        _movimientoRepository = movimientoRepository;
    }

    public async Task<AnalisisFinancieroCompletoDto> AnalizarAsync(string? usuarioId = null, decimal? presupuestoMensual = null, CancellationToken cancellationToken = default)
    {
        var movimientos = await _movimientoRepository.GetAllAsync(usuarioId, cancellationToken);

        var hoy = DateTime.Today;
        var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioMesAnterior = inicioMesActual.AddMonths(-1);

        var mesActual = movimientos.Where(m => m.Fecha >= inicioMesActual).ToList();
        var mesAnterior = movimientos.Where(m => m.Fecha >= inicioMesAnterior && m.Fecha < inicioMesActual).ToList();

        var gastosMesActual = mesActual.Where(m => m.Tipo == TipoMovimiento.Egreso).ToList();
        var ingresosMesActual = mesActual.Where(m => m.Tipo == TipoMovimiento.Ingreso).ToList();
        var gastosMesAnterior = mesAnterior.Where(m => m.Tipo == TipoMovimiento.Egreso).ToList();

        var totalGastosMes = gastosMesActual.Sum(m => m.Monto);
        var totalIngresosMes = ingresosMesActual.Sum(m => m.Monto);
        var totalGastosMesAnterior = gastosMesAnterior.Sum(m => m.Monto);

        var gastosPorCategoria = gastosMesActual
            .GroupBy(m => m.Categoria.Nombre)
            .Select(g => new GastoPorCategoriaDto { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
            .OrderByDescending(g => g.Total)
            .ToList();

        var diasTranscurridos = Math.Max(hoy.Day, 1);

        return new AnalisisFinancieroCompletoDto
        {
            BalanceTotal = movimientos.Sum(m => m.Tipo == TipoMovimiento.Ingreso ? m.Monto : -m.Monto),
            IngresosMes = totalIngresosMes,
            GastosMes = totalGastosMes,
            BalanceMensual = totalIngresosMes - totalGastosMes,
            GastosPorCategoria = gastosPorCategoria,
            CategoriaMayorGasto = gastosPorCategoria.FirstOrDefault()?.Categoria,
            CategoriaMayorCrecimiento = CalcularCategoriaMayorCrecimiento(gastosMesActual, gastosMesAnterior),
            PromedioGastoDiario = decimal.Round(totalGastosMes / diasTranscurridos, 2),
            MayorIngreso = MapResumen(ingresosMesActual.OrderByDescending(m => m.Monto).FirstOrDefault()),
            MayorGasto = MapResumen(gastosMesActual.OrderByDescending(m => m.Monto).FirstOrDefault()),
            CantidadMovimientos = mesActual.Count,
            GastosMesAnterior = totalGastosMesAnterior,
            VariacionGastosPorcentaje = totalGastosMesAnterior > 0
                ? decimal.Round((totalGastosMes - totalGastosMesAnterior) / totalGastosMesAnterior * 100, 2)
                : null,
            PresupuestoMensual = presupuestoMensual,
            PresupuestoUtilizadoPorcentaje = presupuestoMensual is > 0
                ? decimal.Round(totalGastosMes / presupuestoMensual.Value * 100, 2)
                : null
        };
    }

    /// <summary>
    /// Busca la categoría cuyo gasto más creció (en porcentaje) respecto al mes
    /// anterior. Solo considera categorías con gasto en ambos meses para evitar
    /// crecimientos "infinitos" de categorías nuevas.
    /// </summary>
    private static CrecimientoCategoriaDto? CalcularCategoriaMayorCrecimiento(
        IReadOnlyCollection<Movimiento> gastosMesActual,
        IReadOnlyCollection<Movimiento> gastosMesAnterior)
    {
        var actualPorCategoria = gastosMesActual
            .GroupBy(m => m.Categoria.Nombre)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Monto));

        var anteriorPorCategoria = gastosMesAnterior
            .GroupBy(m => m.Categoria.Nombre)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Monto));

        return actualPorCategoria
            .Where(kvp => anteriorPorCategoria.TryGetValue(kvp.Key, out var anterior) && anterior > 0 && kvp.Value > anterior)
            .Select(kvp => new CrecimientoCategoriaDto
            {
                Categoria = kvp.Key,
                GastoMesActual = kvp.Value,
                GastoMesAnterior = anteriorPorCategoria[kvp.Key],
                CrecimientoPorcentaje = decimal.Round((kvp.Value - anteriorPorCategoria[kvp.Key]) / anteriorPorCategoria[kvp.Key] * 100, 2)
            })
            .OrderByDescending(c => c.CrecimientoPorcentaje)
            .FirstOrDefault();
    }

    private static MovimientoResumenDto? MapResumen(Movimiento? movimiento)
    {
        return movimiento is null
            ? null
            : new MovimientoResumenDto
            {
                Descripcion = movimiento.Descripcion,
                Monto = movimiento.Monto,
                Fecha = movimiento.Fecha
            };
    }
}
