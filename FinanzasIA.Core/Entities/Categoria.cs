using FinanzasIA.Core.Common;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Core.Entities;

public class Categoria : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    public TipoMovimiento TipoMovimiento { get; set; }

    public string? UsuarioId { get; set; }

    /// <summary>Categoría creada automáticamente por el sistema; no puede eliminarse, solo desactivarse.</summary>
    public bool EsSistema { get; set; }

    /// <summary>Indica si la categoría está disponible para nuevos movimientos.</summary>
    public bool Activa { get; set; } = true;

    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}