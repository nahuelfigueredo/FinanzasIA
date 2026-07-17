using FinanzasIA.Core.Common;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Core.Entities;

public class Categoria : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    public TipoMovimiento TipoMovimiento { get; set; }

    public string? UsuarioId { get; set; }

    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}