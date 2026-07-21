using FinanzasIA.Core.Common;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Core.Entities;

public class Cuenta : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    public TipoCuenta Tipo { get; set; }

    public string? UsuarioId { get; set; }

    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}
