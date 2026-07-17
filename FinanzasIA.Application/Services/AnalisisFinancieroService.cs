using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

public class AnalisisFinancieroService : IAnalisisFinancieroService
{
    private readonly IMovimientoRepository _movimientoRepository;

    public AnalisisFinancieroService(IMovimientoRepository movimientoRepository)
    {
        _movimientoRepository = movimientoRepository;
    }

    public async Task<AnalisisFinancieroDto> ObtenerAnalisisAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var movimientos = await _movimientoRepository.GetAllAsync(usuarioId, cancellationToken);
        var ingresos = movimientos.Where(x => x.Tipo == TipoMovimiento.Ingreso).ToList();
        var egresos = movimientos.Where(x => x.Tipo == TipoMovimiento.Egreso).ToList();

        var totalIngresos = ingresos.Sum(x => x.Monto);
        var totalEgresos = egresos.Sum(x => x.Monto);
        var balance = totalIngresos - totalEgresos;

        var tasaAhorro = totalIngresos > 0
            ? decimal.Round((balance / totalIngresos) * 100m, 2)
            : 0m;

        var categoriaMayorGasto = egresos
            .GroupBy(x => x.Categoria.Nombre)
            .Select(group => new { Categoria = group.Key, Monto = group.Sum(m => m.Monto) })
            .OrderByDescending(x => x.Monto)
            .Select(x => x.Categoria)
            .FirstOrDefault() ?? "Sin datos";

        var proyeccionBalance = CalcularProyeccionBalance(movimientos);
        var recomendaciones = GenerarRecomendaciones(balance, tasaAhorro, categoriaMayorGasto, totalIngresos, totalEgresos);

        return new AnalisisFinancieroDto
        {
            TotalIngresos = decimal.Round(totalIngresos, 2),
            TotalEgresos = decimal.Round(totalEgresos, 2),
            BalanceNeto = decimal.Round(balance, 2),
            TasaAhorroPorcentaje = tasaAhorro,
            CategoriaMayorGasto = categoriaMayorGasto,
            ProyeccionBalanceProximoMes = proyeccionBalance,
            Recomendaciones = recomendaciones
        };
    }

    private static decimal CalcularProyeccionBalance(IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
    {
        var balanceMensual = movimientos
            .GroupBy(x => new DateTime(x.Fecha.Year, x.Fecha.Month, 1))
            .OrderBy(x => x.Key)
            .Select(group => new
            {
                Mes = group.Key,
                Balance = group.Sum(m => m.Tipo == TipoMovimiento.Ingreso ? m.Monto : -m.Monto)
            })
            .TakeLast(6)
            .ToList();

        if (balanceMensual.Count == 0)
        {
            return 0m;
        }

        if (balanceMensual.Count == 1)
        {
            return decimal.Round(balanceMensual[0].Balance, 2);
        }

        var x = Enumerable.Range(1, balanceMensual.Count).Select(i => (decimal)i).ToList();
        var y = balanceMensual.Select(m => m.Balance).ToList();

        var xPromedio = x.Average();
        var yPromedio = y.Average();

        var numerador = x.Zip(y, (xi, yi) => (xi - xPromedio) * (yi - yPromedio)).Sum();
        var denominador = x.Sum(xi => (xi - xPromedio) * (xi - xPromedio));

        if (denominador == 0)
        {
            return decimal.Round(y.Last(), 2);
        }

        var pendiente = numerador / denominador;
        var intercepto = yPromedio - (pendiente * xPromedio);
        var siguienteX = balanceMensual.Count + 1;
        var prediccion = (pendiente * siguienteX) + intercepto;

        return decimal.Round(prediccion, 2);
    }

    private static IReadOnlyCollection<string> GenerarRecomendaciones(
        decimal balance,
        decimal tasaAhorro,
        string categoriaMayorGasto,
        decimal totalIngresos,
        decimal totalEgresos)
    {
        var recomendaciones = new List<string>();

        if (totalIngresos == 0 && totalEgresos == 0)
        {
            recomendaciones.Add("No hay movimientos cargados todavía. Registrá ingresos y egresos para generar recomendaciones.");
            return recomendaciones;
        }

        if (balance < 0)
        {
            recomendaciones.Add("Estás cerrando en negativo. Priorizá recortar gastos variables este mes.");
        }

        if (tasaAhorro < 20)
        {
            recomendaciones.Add("Tu tasa de ahorro está por debajo del 20%. Intentá fijar un objetivo de ahorro automático.");
        }

        if (categoriaMayorGasto != "Sin datos")
        {
            recomendaciones.Add($"Tu mayor gasto está en '{categoriaMayorGasto}'. Revisá ese rubro para encontrar recortes.");
        }

        if (totalEgresos > totalIngresos * 0.8m)
        {
            recomendaciones.Add("Tus egresos superan el 80% de tus ingresos. Considerá un presupuesto semanal de control.");
        }

        if (recomendaciones.Count == 0)
        {
            recomendaciones.Add("Tu salud financiera viene estable. Mantené el hábito y evaluá invertir parte del excedente.");
        }

        return recomendaciones;
    }
}
