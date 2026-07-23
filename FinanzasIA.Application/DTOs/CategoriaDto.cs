using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.DTOs;

public class CategoriaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoMovimiento TipoMovimiento { get; set; }
    public bool EsSistema { get; set; }
    public bool Activa { get; set; } = true;
}
