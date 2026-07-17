namespace FinanzasIA.Application.DTOs;

public class AnalisisFinancieroDto
{
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal BalanceNeto { get; set; }
    public decimal TasaAhorroPorcentaje { get; set; }
    public string CategoriaMayorGasto { get; set; } = "Sin datos";
    public decimal ProyeccionBalanceProximoMes { get; set; }
    public IReadOnlyCollection<string> Recomendaciones { get; set; } = [];
}
