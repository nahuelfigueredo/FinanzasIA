using System.ComponentModel.DataAnnotations;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Backoffice.Models;

public class MovimientoFormModel
{
    public TipoMovimiento Tipo { get; set; } = TipoMovimiento.Egreso;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una categoría válida.")]
    public int CategoriaId { get; set; }

    public int? CuentaId { get; set; }

    [MaxLength(300, ErrorMessage = "La descripción no puede superar los 300 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999.99", ErrorMessage = "El monto debe ser mayor a 0.", ParseLimitsInInvariantCulture = true)]
    public decimal Monto { get; set; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateTime Fecha { get; set; } = DateTime.Today;
}
