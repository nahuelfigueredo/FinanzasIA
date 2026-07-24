namespace FinanzasIA.Application.DTOs;

public class PresupuestoDto
{
    public int Id { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public decimal MontoMensual { get; set; }
    public int Mes { get; set; }
    public int Año { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class CreatePresupuestoDto
{
    public int CategoriaId { get; set; }
    public decimal MontoMensual { get; set; }
    public int Mes { get; set; }
    public int Año { get; set; }
}

public class UpdatePresupuestoDto
{
    public int CategoriaId { get; set; }
    public decimal MontoMensual { get; set; }
    public int Mes { get; set; }
    public int Año { get; set; }
    public bool Activo { get; set; }
}

/// <summary>
/// Estado de un presupuesto vigente: monto presupuestado, gasto acumulado
/// del mes, porcentaje utilizado, saldo restante y si fue superado.
/// </summary>
public class PresupuestoEstadoDto
{
    public int PresupuestoId { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public decimal MontoMensual { get; set; }
    public decimal GastoAcumulado { get; set; }
    public decimal SaldoRestante => MontoMensual - GastoAcumulado;
    public decimal PorcentajeUtilizado => MontoMensual > 0
        ? Math.Round(GastoAcumulado / MontoMensual * 100m, 1)
        : 0m;
    public bool Superado => GastoAcumulado > MontoMensual;
    public decimal MontoExcedido => Superado ? GastoAcumulado - MontoMensual : 0m;
    public int Mes { get; set; }
    public int Año { get; set; }
}
