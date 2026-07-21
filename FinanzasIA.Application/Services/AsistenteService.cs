using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Servicio de negocio del Asistente Financiero. Obtiene los datos del usuario
/// desde SQL Server (vía repositorios EF Core), construye el contexto financiero
/// y delega la generación de la respuesta al proveedor de IA configurado.
/// </summary>
public class AsistenteService : IAsistenteService
{
    private readonly IMovimientoRepository _movimientoRepository;
    private readonly IAsistenteIAProvider _iaProvider;
    private readonly IMessageProcessor _messageProcessor;

    public AsistenteService(
        IMovimientoRepository movimientoRepository,
        IAsistenteIAProvider iaProvider,
        IMessageProcessor messageProcessor)
    {
        _movimientoRepository = movimientoRepository;
        _iaProvider = iaProvider;
        _messageProcessor = messageProcessor;
    }

    public async Task<AsistenteRespuestaDto> PreguntarAsync(string pregunta, string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
        {
            return new AsistenteRespuestaDto
            {
                Respuesta = "Escribí una pregunta sobre tus finanzas y te ayudo. Por ejemplo: \"¿Cuánto gasté este mes?\" 😊"
            };
        }

        // Primero se intenta interpretar el mensaje con el motor de mensajes.
        // Si corresponde a registrar un movimiento, se crea automáticamente y
        // no se envía la consulta a la IA.
        var resultado = await _messageProcessor.ProcesarAsync(new MensajeEntranteDto
        {
            Texto = pregunta.Trim(),
            Origen = MessageOrigen.Asistente,
            UsuarioId = usuarioId
        }, cancellationToken);

        if (EsRegistroMovimiento(resultado.Intent))
        {
            return new AsistenteRespuestaDto { Respuesta = resultado.Respuesta };
        }

        var movimientos = await _movimientoRepository.GetAllAsync(usuarioId, cancellationToken);
        var contexto = ConstruirContexto(movimientos);
        var respuesta = await _iaProvider.GenerarRespuestaAsync(pregunta.Trim(), contexto, cancellationToken);

        return new AsistenteRespuestaDto { Respuesta = respuesta };
    }

    /// <summary>Determina si la intención corresponde a registrar un movimiento.</summary>
    private static bool EsRegistroMovimiento(MessageIntent intent) =>
        intent is MessageIntent.RegistrarGasto or MessageIntent.RegistrarIngreso or MessageIntent.Transferencia;

    private static ContextoFinancieroDto ConstruirContexto(IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
    {
        var hoy = DateTime.Today;
        var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioMesAnterior = inicioMesActual.AddMonths(-1);

        var mesActual = movimientos.Where(m => m.Fecha >= inicioMesActual).ToList();
        var mesAnterior = movimientos.Where(m => m.Fecha >= inicioMesAnterior && m.Fecha < inicioMesActual).ToList();

        var egresosMesActual = mesActual.Where(m => m.Tipo == TipoMovimiento.Egreso).ToList();

        var mayorGasto = egresosMesActual
            .OrderByDescending(m => m.Monto)
            .Select(m => new MovimientoResumenDto
            {
                Descripcion = m.Descripcion,
                Monto = m.Monto,
                Fecha = m.Fecha
            })
            .FirstOrDefault();

        return new ContextoFinancieroDto
        {
            IngresosMesActual = mesActual.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.Monto),
            EgresosMesActual = egresosMesActual.Sum(m => m.Monto),
            IngresosMesAnterior = mesAnterior.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.Monto),
            EgresosMesAnterior = mesAnterior.Where(m => m.Tipo == TipoMovimiento.Egreso).Sum(m => m.Monto),
            BalanceTotal = movimientos.Sum(m => m.Tipo == TipoMovimiento.Ingreso ? m.Monto : -m.Monto),
            CantidadMovimientosMesActual = mesActual.Count,
            GastosPorCategoriaMesActual = egresosMesActual
                .GroupBy(m => m.Categoria.Nombre)
                .Select(g => new GastoPorCategoriaDto { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
                .OrderByDescending(g => g.Total)
                .ToList(),
            MayorGastoMesActual = mayorGasto
        };
    }
}
