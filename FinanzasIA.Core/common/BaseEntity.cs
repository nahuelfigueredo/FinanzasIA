namespace FinanzasIA.Core.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public DateTime? FechaModificacion { get; set; }
}