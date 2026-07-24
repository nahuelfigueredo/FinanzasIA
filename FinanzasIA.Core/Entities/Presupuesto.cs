using FinanzasIA.Core.Common;

namespace FinanzasIA.Core.Entities;

/// <summary>
/// Presupuesto mensual por categoría y usuario.
/// Define el monto máximo que el usuario planea gastar en una categoría durante un mes.
/// </summary>
public class Presupuesto : BaseEntity
{
    public string UsuarioId { get; set; } = string.Empty;

    public int CategoriaId { get; set; }

    public Categoria? Categoria { get; set; }

    public decimal MontoMensual { get; set; }

    /// <summary>Mes del presupuesto (1-12).</summary>
    public int Mes { get; set; }

    /// <summary>Año del presupuesto (ej. 2026).</summary>
    public int Año { get; set; }

    public bool Activo { get; set; } = true;
}
