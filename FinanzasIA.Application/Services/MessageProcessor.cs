using System.Diagnostics;
using System.Text.Json;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Orquestador del motor de mensajes. Pipeline en cascada:
/// 1. <see cref="IMessageInterpreter"/> (intención + datos por reglas).
/// 2. Si no entiende: <see cref="IMensajeFinancieroParser"/> (regex financiero dedicado).
/// 3. Si tampoco: <see cref="IAsistenteIAProvider"/> (IA, respuesta JSON de movimiento).
/// Luego <see cref="IMessageActionExecutor"/> ejecuta la acción y se registra el historial.
/// No contiene lógica de interpretación ni de negocio: solo coordina.
/// </summary>
public class MessageProcessor : IMessageProcessor
{
    private readonly IMessageInterpreter _interpreter;
    private readonly IMensajeFinancieroParser _parserFinanciero;
    private readonly IAsistenteIAProvider _iaProvider;
    private readonly IMessageActionExecutor _executor;
    private readonly IMensajeProcesadoRepository _mensajeRepository;
    private readonly ILogger<MessageProcessor> _logger;

    public MessageProcessor(
        IMessageInterpreter interpreter,
        IMensajeFinancieroParser parserFinanciero,
        IAsistenteIAProvider iaProvider,
        IMessageActionExecutor executor,
        IMensajeProcesadoRepository mensajeRepository,
        ILogger<MessageProcessor> logger)
    {
        _interpreter = interpreter;
        _parserFinanciero = parserFinanciero;
        _iaProvider = iaProvider;
        _executor = executor;
        _mensajeRepository = mensajeRepository;
        _logger = logger;
    }

    public async Task<MensajeProcesadoResultDto> ProcesarAsync(MensajeEntranteDto mensaje, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Mensaje recibido desde {Origen} para usuario {UsuarioId}: {Texto}", mensaje.Origen, mensaje.UsuarioId, mensaje.Texto);

        MensajeProcesadoResultDto resultado;
        try
        {
            var interpretado = await InterpretarEnCascadaAsync(mensaje.Texto, cancellationToken);
            _logger.LogInformation("Intent detectado: {Intent}", interpretado.Intent);

            var ejecucion = Stopwatch.StartNew();
            resultado = await _executor.EjecutarAsync(interpretado, mensaje.UsuarioId, cancellationToken);
            _logger.LogInformation("Acción ejecutada en {Duracion} ms (Exito={Exito}).", ejecucion.ElapsedMilliseconds, resultado.Exito);

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

    /// <summary>
    /// Interpreta el mensaje en tres niveles: reglas generales → parser
    /// financiero dedicado → IA. El primero que entienda gana.
    /// </summary>
    private async Task<MensajeInterpretadoDto> InterpretarEnCascadaAsync(string texto, CancellationToken cancellationToken)
    {
        // Nivel 1: intérprete por reglas (intents de registro y consultas).
        var etapa = Stopwatch.StartNew();
        var interpretado = await _interpreter.InterpretarAsync(texto, cancellationToken);
        _logger.LogInformation("Intérprete por reglas ejecutado en {Duracion} ms (Intent={Intent}).", etapa.ElapsedMilliseconds, interpretado.Intent);
        if (interpretado.Intent != MessageIntent.Desconocido)
        {
            return interpretado;
        }

        // Nivel 2: parser financiero dedicado ("Nafta 18000", "Luz 15000", etc.).
        etapa.Restart();
        var analisis = _parserFinanciero.Analizar(texto);
        _logger.LogInformation("Parser financiero ejecutado en {Duracion} ms (EsMovimiento={EsMovimiento}).", etapa.ElapsedMilliseconds, analisis.EsMovimiento);
        if (analisis.EsMovimiento)
        {
            _logger.LogInformation("Movimiento detectado por el parser financiero: {Tipo} {Monto} ({Categoria}).",
                analisis.Tipo, analisis.Monto, analisis.Categoria);
            return AInterpretado(analisis);
        }

        // Nivel 3: IA. Si devuelve un movimiento JSON válido, se registra automáticamente.
        etapa.Restart();
        var desdeIa = await IntentarConIaAsync(texto, cancellationToken);
        _logger.LogInformation("IA ejecutada en {Duracion} ms (MovimientoDetectado={Detectado}).", etapa.ElapsedMilliseconds, desdeIa is not null);
        if (desdeIa is not null)
        {
            _logger.LogInformation("Movimiento detectado por IA: {Tipo} {Monto} ({Categoria}).",
                desdeIa.TipoMovimiento, desdeIa.Monto, desdeIa.Categoria);
            return desdeIa;
        }

        return interpretado;
    }

    private static MensajeInterpretadoDto AInterpretado(ResultadoAnalisis analisis) => new()
    {
        Intent = analisis.Tipo == TipoMovimiento.Ingreso ? MessageIntent.RegistrarIngreso : MessageIntent.RegistrarGasto,
        TipoMovimiento = analisis.Tipo,
        Monto = analisis.Monto,
        Categoria = analisis.Categoria,
        Descripcion = analisis.Descripcion,
        Fecha = DateTime.Now
    };

    /// <summary>
    /// Pide a la IA que interprete el mensaje como movimiento y devuelva JSON:
    /// {"tipo":"Gasto","monto":25000,"categoria":"Supermercado","descripcion":"..."}.
    /// Si la respuesta no es un movimiento JSON válido, devuelve null y el
    /// mensaje queda como no manejado. Nunca lanza.
    /// </summary>
    private async Task<MensajeInterpretadoDto?> IntentarConIaAsync(string texto, CancellationToken cancellationToken)
    {
        try
        {
            var prompt =
                "Interpretá el siguiente mensaje como un movimiento financiero y respondé únicamente con JSON " +
                "con el formato {\"tipo\":\"Gasto|Ingreso\",\"monto\":0,\"categoria\":\"\",\"descripcion\":\"\"}. " +
                "Si el mensaje no describe un movimiento, respondé {}.\n\nMensaje: " + texto;

            var respuesta = await _iaProvider.GenerarRespuestaAsync(prompt, new ContextoFinancieroDto(), null, cancellationToken);

            var inicio = respuesta.IndexOf('{');
            var fin = respuesta.LastIndexOf('}');
            if (inicio < 0 || fin <= inicio)
            {
                return null;
            }

            using var json = JsonDocument.Parse(respuesta[inicio..(fin + 1)]);
            var root = json.RootElement;

            if (!root.TryGetProperty("monto", out var montoNode) || !montoNode.TryGetDecimal(out var monto) || monto <= 0)
            {
                return null;
            }

            var tipoTexto = root.TryGetProperty("tipo", out var tipoNode) ? tipoNode.GetString() : null;
            var tipo = string.Equals(tipoTexto, "Ingreso", StringComparison.OrdinalIgnoreCase)
                ? TipoMovimiento.Ingreso
                : TipoMovimiento.Egreso;

            return new MensajeInterpretadoDto
            {
                Intent = tipo == TipoMovimiento.Ingreso ? MessageIntent.RegistrarIngreso : MessageIntent.RegistrarGasto,
                TipoMovimiento = tipo,
                Monto = monto,
                Categoria = root.TryGetProperty("categoria", out var cat) ? cat.GetString() : null,
                Descripcion = root.TryGetProperty("descripcion", out var desc) ? desc.GetString() : null,
                Fecha = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "El fallback de IA no pudo interpretar el mensaje.");
            return null;
        }
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
