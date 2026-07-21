using FinanzasIA.Core.Common;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Core.Entities;

public class Movimiento : BaseEntity
{
    public TipoMovimiento Tipo { get; set; }

    public int CategoriaId { get; set; }

    public Categoria Categoria { get; set; } = null!;

    public int? CuentaId { get; set; }

    public Cuenta? Cuenta { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public decimal Monto { get; set; }

    public DateTime Fecha { get; set; }

    public string? UsuarioId { get; set; }
}