using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Core.Interfaces;

/// <summary>
/// Persistencia de los vínculos entre usuarios y números de mensajería.
/// </summary>
public interface IUsuarioWhatsappRepository
{
    Task<UsuarioWhatsapp?> BuscarPorNumeroAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UsuarioWhatsapp>> BuscarPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<UsuarioWhatsapp> AgregarAsync(UsuarioWhatsapp vinculo, CancellationToken cancellationToken = default);
    Task ActualizarAsync(UsuarioWhatsapp vinculo, CancellationToken cancellationToken = default);
    Task EliminarAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExisteNumeroAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default);
}
