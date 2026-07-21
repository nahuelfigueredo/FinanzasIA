using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.DTOs;

/// <summary>
/// Resultado del análisis de un mensaje financiero de texto libre:
/// indica si el mensaje describe un movimiento y sus datos extraídos.
/// </summary>
public class ResultadoAnalisis
{
    public bool EsMovimiento { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public decimal Monto { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}
