using FinanzasIA.Application.DTOs;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Lógica de vinculación de números de mensajería con usuarios de FinanzasIA:
/// alta directa, baja e identificación del usuario a partir del número del remitente.
/// </summary>
public interface IUsuarioWhatsappService
{
    /// <summary>Vincula un número directamente al usuario autenticado.</summary>
    Task<VinculacionResultDto> VincularAsync(VincularNumeroDto dto, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Desvincula (elimina) un número del usuario.</summary>
    Task<bool> DesvincularAsync(int id, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Devuelve los números vinculados del usuario.</summary>
    Task<IReadOnlyCollection<UsuarioWhatsappDto>> ObtenerNumerosAsync(string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifica al usuario dueño de un número vinculado y activo, y actualiza
    /// la fecha de último uso. Devuelve null si el número no está vinculado.
    /// </summary>
    Task<string?> BuscarUsuarioPorNumeroAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default);
}
