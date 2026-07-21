using FinanzasIA.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinanzasIA.Application.DTOs;

public class CreateMovimientoDto
{
    [Required]
    public TipoMovimiento Tipo { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int CategoriaId { get; set; }

    public int? CuentaId { get; set; }

    [MaxLength(300)]
    public string Descripcion { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Monto { get; set; }

    [Required]
    public DateTime Fecha { get; set; }
}
