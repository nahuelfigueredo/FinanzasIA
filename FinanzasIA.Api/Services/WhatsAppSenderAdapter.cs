using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Api.Services;

/// <summary>
/// Adapter que conecta la abstracción <see cref="ICanalMensajeriaSender"/> de
/// Application con la implementación concreta de WhatsApp Cloud API.
/// Permite agregar Telegram/Messenger sin tocar la capa de aplicación.
/// </summary>
public class WhatsAppSenderAdapter : ICanalMensajeriaSender
{
    private readonly IWhatsAppService _whatsAppService;

    public WhatsAppSenderAdapter(IWhatsAppService whatsAppService)
    {
        _whatsAppService = whatsAppService;
    }

    public CanalMensajeria Canal => CanalMensajeria.WhatsApp;

    public Task EnviarTextoAsync(string numeroTelefono, string mensaje, CancellationToken cancellationToken = default)
        => _whatsAppService.SendTextMessageAsync(numeroTelefono, mensaje, cancellationToken);
}
