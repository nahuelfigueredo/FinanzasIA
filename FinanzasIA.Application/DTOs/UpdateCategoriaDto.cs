using FinanzasIA.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinanzasIA.Application.DTOs;

public class UpdateCategoriaDto
{
    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    public TipoMovimiento TipoMovimiento { get; set; }
}
