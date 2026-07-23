using FinanzasIA.Core.Common;

namespace FinanzasIA.Core.Entities;

/// <summary>Preferencias de automatizaciones de un usuario (una fila por usuario).</summary>
public class ConfiguracionAutomatizacion : BaseEntity
{
    public string UsuarioId { get; set; } = string.Empty;

    public bool RegistroAutomaticoWhatsapp { get; set; } = true;

    public bool OcrAutomatico { get; set; } = true;

    public bool ClasificacionAutomatica { get; set; } = true;

    public bool RespuestasAutomaticas { get; set; } = true;

    public bool Recordatorios { get; set; }

    public bool AlertasPresupuesto { get; set; } = true;

    public bool ResumenDiario { get; set; }

    public bool ResumenSemanal { get; set; }
}
