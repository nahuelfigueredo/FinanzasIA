using FinanzasIA.Application.DTOs;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Lógica de vinculación de números de mensajería con usuarios de FinanzasIA:
/// alta con código de verificación, verificación, baja e identificación del
/// usuario a partir del número del remitente.
/// </summary>
public interface IUsuarioWhatsappService
{
    /// <summary>Vincula un número al usuario, genera el código de 6 dígitos y lo envía por el canal.</summary>
    Task<VinculacionResultDto> VincularAsync(VincularNumeroDto dto, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Verifica el código ingresado por el usuario; si es correcto marca el número como verificado.</summary>
    Task<VinculacionResultDto> VerificarAsync(VerificarNumeroDto dto, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Regenera y reenvía el código de verificación.</summary>
    Task<VinculacionResultDto> ReenviarCodigoAsync(int id, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Desvincula (elimina) un número del usuario.</summary>
    Task<bool> DesvincularAsync(int id, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Devuelve los números vinculados del usuario.</summary>
    Task<IReadOnlyCollection<UsuarioWhatsappDto>> ObtenerNumerosAsync(string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifica al usuario dueño de un número verificado y activo, y actualiza
    /// la fecha de último uso. Devuelve null si el número no está vinculado.
    /// </summary>
    Task<string?> BuscarUsuarioPorNumeroAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el número está vinculado a un usuario pero todavía no fue
    /// verificado (o está inactivo). Permite responder con un mensaje específico.
    /// </summary>
    Task<bool> NumeroPendienteDeVerificacionAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default);
}
