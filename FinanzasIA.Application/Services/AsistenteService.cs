using System.Globalization;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Servicio de negocio del Asistente Financiero. Obtiene los datos del usuario,
/// construye el contexto financiero completo (día/semana/mes/año, promedios,
/// tendencias, alertas, movimientos recientes) y delega la generación de la
/// respuesta al proveedor de IA configurado, manteniendo la memoria de la
/// conversación mediante el historial de mensajes.
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

	public async Task<AsistenteRespuestaDto> PreguntarAsync(AsistentePreguntaDto pregunta, string? usuarioId = null, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(pregunta.Pregunta))
		{
			return new AsistenteRespuestaDto
			{
				Respuesta = "Escribí una pregunta sobre tus finanzas y te ayudo. Por ejemplo: \"¿Cuánto gasté este mes?\" 😊"
			};
		}

		// Primero se intenta interpretar el mensaje con el motor de mensajes.
		// Si corresponde a una acción (registrar movimiento, transferencia),
		// se ejecuta automáticamente y se confirma el resultado.
		var resultado = await _messageProcessor.ProcesarAsync(new MensajeEntranteDto
		{
			Texto = pregunta.Pregunta.Trim(),
			Origen = MessageOrigen.Asistente,
			UsuarioId = usuarioId
		}, cancellationToken);

		if (EsRegistroMovimiento(resultado.Intent))
		{
			return new AsistenteRespuestaDto { Respuesta = resultado.Respuesta, AccionEjecutada = true };
		}

		var movimientos = await _movimientoRepository.GetAllAsync(usuarioId, cancellationToken);
		var contexto = ConstruirContexto(movimientos);
		var respuesta = await _iaProvider.GenerarRespuestaAsync(pregunta.Pregunta.Trim(), contexto, pregunta.Historial, cancellationToken);

		return new AsistenteRespuestaDto { Respuesta = respuesta };
	}

	/// <summary>Determina si la intención corresponde a registrar un movimiento.</summary>
	private static bool EsRegistroMovimiento(MessageIntent intent) =>
		intent is MessageIntent.RegistrarGasto or MessageIntent.RegistrarIngreso or MessageIntent.Transferencia;

	private static ContextoFinancieroDto ConstruirContexto(IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
	{
		var hoy = DateTime.Today;
		var inicioSemana = hoy.AddDays(-(int)(hoy.DayOfWeek == DayOfWeek.Sunday ? 6 : hoy.DayOfWeek - DayOfWeek.Monday));
		var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
		var inicioMesAnterior = inicioMesActual.AddMonths(-1);
		var inicioAnio = new DateTime(hoy.Year, 1, 1);
		var inicioSeisMeses = inicioMesActual.AddMonths(-5);

		var mesActual = movimientos.Where(m => m.Fecha >= inicioMesActual).ToList();
		var mesAnterior = movimientos.Where(m => m.Fecha >= inicioMesAnterior && m.Fecha < inicioMesActual).ToList();

		var egresosMesActual = mesActual.Where(m => m.Tipo == TipoMovimiento.Egreso).ToList();
		var egresosMesAnterior = mesAnterior.Where(m => m.Tipo == TipoMovimiento.Egreso).ToList();

		var gastosMes = egresosMesActual.Sum(m => m.Monto);
		var gastosMesAnteriorTotal = egresosMesAnterior.Sum(m => m.Monto);

		var diasTranscurridos = Math.Max(1, hoy.Day);
		var diasDelMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
		var promedioDiario = gastosMes / diasTranscurridos;

		// Promedio mensual de gasto sobre los últimos 6 meses con datos.
		var mesesConGasto = movimientos
			.Where(m => m.Tipo == TipoMovimiento.Egreso && m.Fecha >= inicioSeisMeses)
			.GroupBy(m => new { m.Fecha.Year, m.Fecha.Month })
			.Select(g => g.Sum(m => m.Monto))
			.ToList();
		var promedioMensual = mesesConGasto.Count > 0 ? mesesConGasto.Average() : 0m;

		decimal? variacion = gastosMesAnteriorTotal > 0
			? Math.Round((gastosMes - gastosMesAnteriorTotal) / gastosMesAnteriorTotal * 100, 1)
			: null;

		var gastosPorCategoria = egresosMesActual
			.GroupBy(m => m.Categoria.Nombre)
			.Select(g => new GastoPorCategoriaDto { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
			.OrderByDescending(g => g.Total)
			.ToList();

		var alertas = new List<string>();
		if (variacion is > 20)
		{
			alertas.Add($"Los gastos de este mes aumentaron {variacion:0.#}% respecto al mes anterior.");
		}
		if (gastosPorCategoria.Count > 0 && gastosMes > 0)
		{
			var top = gastosPorCategoria[0];
			var pct = Math.Round(top.Total / gastosMes * 100, 0);
			if (pct >= 40)
			{
				alertas.Add($"La categoría '{top.Categoria}' concentra el {pct}% de los gastos del mes.");
			}
		}
		var ingresosMes = mesActual.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.Monto);
		if (ingresosMes > 0 && gastosMes > ingresosMes)
		{
			alertas.Add("Este mes los gastos superan a los ingresos.");
		}

		var cultura = new CultureInfo("es-AR");

		return new ContextoFinancieroDto
		{
			BalanceTotal = movimientos.Sum(m => m.Tipo == TipoMovimiento.Ingreso ? m.Monto : -m.Monto),
			IngresosMesActual = ingresosMes,
			EgresosMesActual = gastosMes,
			IngresosMesAnterior = mesAnterior.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.Monto),
			EgresosMesAnterior = gastosMesAnteriorTotal,
			GastosHoy = movimientos.Where(m => m.Tipo == TipoMovimiento.Egreso && m.Fecha.Date == hoy).Sum(m => m.Monto),
			GastosSemana = movimientos.Where(m => m.Tipo == TipoMovimiento.Egreso && m.Fecha >= inicioSemana).Sum(m => m.Monto),
			GastosMes = gastosMes,
			GastosAnio = movimientos.Where(m => m.Tipo == TipoMovimiento.Egreso && m.Fecha >= inicioAnio).Sum(m => m.Monto),
			IngresosAnio = movimientos.Where(m => m.Tipo == TipoMovimiento.Ingreso && m.Fecha >= inicioAnio).Sum(m => m.Monto),
			PromedioDiarioGastoMesActual = Math.Round(promedioDiario, 2),
			PromedioMensualGasto = Math.Round(promedioMensual, 2),
			VariacionGastosVsMesAnteriorPorcentaje = variacion,
			ProyeccionGastoFinDeMes = Math.Round(promedioDiario * diasDelMes, 2),
			CantidadMovimientosMesActual = mesActual.Count,
			GastosPorCategoriaMesActual = gastosPorCategoria,
			GastosPorCategoriaMesAnterior = egresosMesAnterior
				.GroupBy(m => m.Categoria.Nombre)
				.Select(g => new GastoPorCategoriaDto { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
				.OrderByDescending(g => g.Total)
				.ToList(),
			GastosUltimosMeses = movimientos
				.Where(m => m.Fecha >= inicioSeisMeses)
				.GroupBy(m => new { m.Fecha.Year, m.Fecha.Month })
				.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
				.Select(g => new TotalMensualDto
				{
					Mes = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy", cultura),
					Ingresos = g.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.Monto),
					Egresos = g.Where(m => m.Tipo == TipoMovimiento.Egreso).Sum(m => m.Monto)
				})
				.ToList(),
			MayorGastoMesActual = egresosMesActual
				.OrderByDescending(m => m.Monto)
				.Select(MapResumen)
				.FirstOrDefault(),
			MovimientosRecientes = movimientos
				.OrderByDescending(m => m.Fecha)
				.Take(15)
				.Select(MapResumen)
				.ToList(),
			Alertas = alertas
		};
	}

	private static MovimientoResumenDto MapResumen(Core.Entities.Movimiento m) => new()
	{
		Descripcion = m.Descripcion,
		Categoria = m.Categoria?.Nombre,
		Tipo = m.Tipo.ToString(),
		Monto = m.Monto,
		Fecha = m.Fecha
	};
}
