using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Punto de entrada único del motor de mensajes. Cualquier canal (WhatsApp,
/// Asistente Financiero, API, apps futuras) envía el mensaje acá y recibe la
/// respuesta lista para el usuario. Orquesta interpretación, ejecución,
/// logging y persistencia del historial.
/// </summary>
public interface IMessageProcessor
{
    Task<MensajeProcesadoResultDto> ProcesarAsync(MensajeEntranteDto mensaje, CancellationToken cancellationToken = default);
}
