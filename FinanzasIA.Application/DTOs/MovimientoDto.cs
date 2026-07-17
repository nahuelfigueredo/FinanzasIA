using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.DTOs;

public class MovimientoDto
{
    public int Id { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
}
