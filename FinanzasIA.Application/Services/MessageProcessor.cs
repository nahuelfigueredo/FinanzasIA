using System.Diagnostics;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Orquestador del motor de mensajes. Flujo:
/// mensaje → <see cref="IMessageInterpreter"/> (intención + datos) →
/// <see cref="IMessageActionExecutor"/> (acción + respuesta) → historial + logging.
/// No contiene lógica de interpretación ni de negocio: solo coordina.
/// </summary>
public class MessageProcessor : IMessageProcessor
{
    private readonly IMessageInterpreter _interpreter;
    private readonly IMessageActionExecutor _executor;
    private readonly IMensajeProcesadoRepository _mensajeRepository;
    private readonly ILogger<MessageProcessor> _logger;

    public MessageProcessor(
        IMessageInterpreter interpreter,
        IMessageActionExecutor executor,
        IMensajeProcesadoRepository mensajeRepository,
        ILogger<MessageProcessor> logger)
    {
        _interpreter = interpreter;
        _executor = executor;
        _mensajeRepository = mensajeRepository;
        _logger = logger;
    }

    public async Task<MensajeProcesadoResultDto> ProcesarAsync(MensajeEntranteDto mensaje, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Mensaje recibido desde {Origen}: {Texto}", mensaje.Origen, mensaje.Texto);

        MensajeProcesadoResultDto resultado;
        try
        {
            var interpretado = await _interpreter.InterpretarAsync(mensaje.Texto, cancellationToken);
            _logger.LogInformation("Intent detectado: {Intent}", interpretado.Intent);

            resultado = await _executor.EjecutarAsync(interpretado, mensaje.UsuarioId, cancellationToken);

            if (resultado.MovimientoId is not null)
            {
                _logger.LogInformation("Movimiento creado con Id {MovimientoId}.", resultado.MovimientoId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar el mensaje: {Texto}", mensaje.Texto);
            resultado = new MensajeProcesadoResultDto
            {
                Intent = MessageIntent.Desconocido,
                Manejado = true,
                Exito = false,
                Respuesta = "Ups, algo salió mal al procesar tu mensaje. Intentá de nuevo en unos segundos. 🙏"
            };
        }

        stopwatch.Stop();
        _logger.LogInformation("Mensaje procesado en {Duracion} ms.", stopwatch.ElapsedMilliseconds);

        await GuardarHistorialAsync(mensaje, resultado, stopwatch.ElapsedMilliseconds, cancellationToken);
        return resultado;
    }

    /// <summary>Persiste el registro del mensaje; un fallo acá nunca afecta la respuesta al usuario.</summary>
    private async Task GuardarHistorialAsync(MensajeEntranteDto mensaje, MensajeProcesadoResultDto resultado, long duracionMs, CancellationToken cancellationToken)
    {
        try
        {
            await _mensajeRepository.AddAsync(new MensajeProcesado
            {
                Texto = mensaje.Texto.Length > 1000 ? mensaje.Texto[..1000] : mensaje.Texto,
                Origen = (int)mensaje.Origen,
                Intent = (int)resultado.Intent,
                Exito = resultado.Exito,
                MovimientoId = resultado.MovimientoId,
                Respuesta = resultado.Respuesta.Length > 2000 ? resultado.Respuesta[..2000] : resultado.Respuesta,
                DuracionMs = duracionMs,
                UsuarioId = mensaje.UsuarioId
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar el historial del mensaje.");
        }
    }
}
