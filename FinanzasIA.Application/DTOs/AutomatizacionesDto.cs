namespace FinanzasIA.Application.DTOs;

public class AutomatizacionesDto
{
    public bool RegistroAutomaticoWhatsapp { get; set; } = true;

    public bool OcrAutomatico { get; set; } = true;

    public bool ClasificacionAutomatica { get; set; } = true;

    public bool RespuestasAutomaticas { get; set; } = true;

    public bool Recordatorios { get; set; }

    public bool AlertasPresupuesto { get; set; } = true;

    public bool ResumenDiario { get; set; }

    public bool ResumenSemanal { get; set; }
}
