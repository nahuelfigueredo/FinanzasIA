using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.DTOs;

/// <summary>Vínculo entre un usuario y un número de mensajería, para la UI.</summary>
public class UsuarioWhatsappDto
{
    public int Id { get; set; }
    public string NumeroTelefono { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public CanalMensajeria Canal { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaAlta { get; set; }
    public DateTime? FechaUltimoUso { get; set; }
}

/// <summary>Solicitud para vincular un nuevo número.</summary>
public class VincularNumeroDto
{
    public string NumeroTelefono { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public CanalMensajeria Canal { get; set; } = CanalMensajeria.WhatsApp;
}

/// <summary>Resultado de una operación de vinculación.</summary>
public class VinculacionResultDto
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public UsuarioWhatsappDto? Vinculo { get; set; }
}
