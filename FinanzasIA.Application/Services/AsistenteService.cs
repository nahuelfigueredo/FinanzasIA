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

		// Si el usuario pide un gráfico, se responde con datos estructurados reales.
		var grafico = GenerarGraficoSiCorresponde(pregunta.Pregunta.Trim(), movimientos);
		if (grafico is not null)
		{
			return grafico;
		}

		var contexto = ConstruirContexto(movimientos);
		var respuesta = await _iaProvider.GenerarRespuestaAsync(pregunta.Pregunta.Trim(), contexto, pregunta.Historial, cancellationToken);

		return new AsistenteRespuestaDto { Respuesta = respuesta };
	}

	/// <summary>Determina si la intención corresponde a registrar un movimiento.</summary>
	private static bool EsRegistroMovimiento(MessageIntent intent) =>
		intent is MessageIntent.RegistrarGasto or MessageIntent.RegistrarIngreso or MessageIntent.Transferencia;

	#region Gráficos

	private static readonly CultureInfo Cultura = new("es-AR");

	/// <summary>
	/// Detecta si la pregunta pide una visualización y, de ser así, construye la
	/// respuesta estructurada con datos reales del usuario. Devuelve null si no
	/// corresponde un gráfico (el flujo sigue con la IA de texto).
	/// </summary>
	private static AsistenteRespuestaDto? GenerarGraficoSiCorresponde(string pregunta, IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
	{
		var texto = QuitarAcentos(pregunta.ToLowerInvariant());

		var mencionaGrafico = texto.Contains("grafic");
		var mencionaComparar = texto.Contains("compar");
		var mencionaEvolucion = texto.Contains("evolucion") || texto.Contains("evoluciona") || texto.Contains("tendencia");
		var mencionaTop = texto.Contains("top ") || texto.StartsWith("top");
		var mencionaBalance = texto.Contains("balance");
		var mencionaPresupuesto = texto.Contains("presupuesto");
		var mencionaCategoria = texto.Contains("categoria");
		var mencionaIngresos = texto.Contains("ingreso");
		var mencionaGastos = texto.Contains("gasto") || texto.Contains("egreso");

		var pideVisual = mencionaGrafico || mencionaComparar || mencionaEvolucion || mencionaTop
			|| (mencionaBalance && texto.Contains("mensual"))
			|| (mencionaGrafico && mencionaPresupuesto);

		if (!pideVisual)
		{
			return null;
		}

		if (movimientos.Count == 0)
		{
			return SinDatos();
		}

		// Top N categorías
		if (mencionaTop && mencionaCategoria)
		{
			return GraficoTopCategorias(movimientos, texto);
		}

		// Gastos por categoría (torta)
		if (mencionaCategoria)
		{
			return GraficoGastosPorCategoria(movimientos);
		}

		// Presupuesto: egresos mensuales vs promedio
		if (mencionaPresupuesto)
		{
			return GraficoPresupuesto(movimientos);
		}

		// Balance mensual (barras ingresos vs egresos por mes)
		if (mencionaBalance)
		{
			return GraficoBalanceMensual(movimientos);
		}

		// Comparativas / evolución de ingresos (líneas o barras por mes)
		if (mencionaIngresos && (mencionaComparar || mencionaEvolucion || mencionaGrafico))
		{
			return GraficoSerieMensual(movimientos, TipoMovimiento.Ingreso, "Ingresos de los últimos 6 meses", mencionaEvolucion ? TipoGrafico.Lineas : TipoGrafico.Barras);
		}

		// Evolución / comparativa de gastos
		if (mencionaGastos && (mencionaEvolucion || mencionaComparar))
		{
			// "Compará este mes con el anterior" → balance mensual acotado; si es evolución → líneas
			return mencionaEvolucion
				? GraficoSerieMensual(movimientos, TipoMovimiento.Egreso, "Evolución de gastos", TipoGrafico.Lineas)
				: GraficoBalanceMensual(movimientos, meses: 2, titulo: "Este mes vs. el anterior");
		}

		// "Compará este mes con el anterior" sin mencionar gastos/ingresos
		if (mencionaComparar && (texto.Contains("mes") || texto.Contains("anterior")))
		{
			return GraficoBalanceMensual(movimientos, meses: 2, titulo: "Este mes vs. el anterior");
		}

		// Pedido genérico de gráfico → gastos por categoría del mes
		return GraficoGastosPorCategoria(movimientos);
	}

	private static AsistenteRespuestaDto SinDatos() => new()
	{
		Tipo = AsistenteRespuestaTipo.Card,
		Respuesta = "Todavía no hay movimientos suficientes para generar ese gráfico. Registrá algunos ingresos o gastos y volvé a intentarlo. 📊"
	};

	private static AsistenteRespuestaDto GraficoGastosPorCategoria(IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
	{
		var hoy = DateTime.Today;
		var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
		var egresos = movimientos.Where(m => m.Tipo == TipoMovimiento.Egreso && m.Fecha >= inicioMes).ToList();

		if (egresos.Count == 0)
		{
			return SinDatos();
		}

		var porCategoria = egresos
			.GroupBy(m => m.Categoria?.Nombre ?? "Sin categoría")
			.Select(g => new { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
			.OrderByDescending(g => g.Total)
			.ToList();

		var total = porCategoria.Sum(c => c.Total);

		return new AsistenteRespuestaDto
		{
			Tipo = AsistenteRespuestaTipo.Chart,
			Respuesta = $"Acá tenés tus gastos por categoría de {inicioMes.ToString("MMMM yyyy", Cultura)}. El total es {total.ToString("C", Cultura)}.",
			Grafico = new GraficoDto
			{
				Tipo = TipoGrafico.Torta,
				Titulo = "Gastos por categoría",
				Subtitulo = inicioMes.ToString("MMMM yyyy", Cultura),
				Etiquetas = porCategoria.Select(c => c.Categoria).ToList(),
				Valores = porCategoria.Select(c => c.Total).ToList(),
				Total = total,
				Periodo = inicioMes.ToString("MMMM yyyy", Cultura)
			}
		};
	}

	private static AsistenteRespuestaDto GraficoTopCategorias(IReadOnlyCollection<Core.Entities.Movimiento> movimientos, string texto)
	{
		var cantidad = 10;
		var match = System.Text.RegularExpressions.Regex.Match(texto, @"top\s*(\d+)");
		if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > 0)
		{
			cantidad = n;
		}

		var egresos = movimientos.Where(m => m.Tipo == TipoMovimiento.Egreso).ToList();
		if (egresos.Count == 0)
		{
			return SinDatos();
		}

		var top = egresos
			.GroupBy(m => m.Categoria?.Nombre ?? "Sin categoría")
			.Select(g => new { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
			.OrderByDescending(g => g.Total)
			.Take(cantidad)
			.ToList();

		return new AsistenteRespuestaDto
		{
			Tipo = AsistenteRespuestaTipo.Chart,
			Respuesta = $"Estas son tus {top.Count} categorías con mayor gasto histórico.",
			Grafico = new GraficoDto
			{
				Tipo = TipoGrafico.Barras,
				Titulo = $"Top {top.Count} categorías por gasto",
				Subtitulo = "Histórico",
				Etiquetas = top.Select(c => c.Categoria).ToList(),
				Valores = top.Select(c => c.Total).ToList(),
				Total = top.Sum(c => c.Total),
				Periodo = "Histórico"
			}
		};
	}

	private static AsistenteRespuestaDto GraficoBalanceMensual(IReadOnlyCollection<Core.Entities.Movimiento> movimientos, int meses = 6, string? titulo = null)
	{
		var serie = SerieMensual(movimientos, meses);
		if (serie.Count == 0)
		{
			return SinDatos();
		}

		var totalIngresos = serie.Sum(s => s.Ingresos);
		var totalEgresos = serie.Sum(s => s.Egresos);

		return new AsistenteRespuestaDto
		{
			Tipo = AsistenteRespuestaTipo.Chart,
			Respuesta = $"Balance de los últimos {serie.Count} meses: ingresos {totalIngresos.ToString("C", Cultura)} vs egresos {totalEgresos.ToString("C", Cultura)}.",
			Grafico = new GraficoDto
			{
				Tipo = TipoGrafico.Barras,
				Titulo = titulo ?? "Balance mensual",
				Subtitulo = "Ingresos vs egresos",
				Etiquetas = serie.Select(s => s.Mes).ToList(),
				Valores = serie.Select(s => s.Ingresos).ToList(),
				ValoresSecundarios = serie.Select(s => s.Egresos).ToList(),
				EtiquetaSerie = "Ingresos",
				EtiquetaSerieSecundaria = "Egresos",
				Total = totalIngresos - totalEgresos,
				Periodo = $"Últimos {serie.Count} meses"
			}
		};
	}

	private static AsistenteRespuestaDto GraficoPresupuesto(IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
	{
		var serie = SerieMensual(movimientos, 6);
		if (serie.Count == 0 || serie.All(s => s.Egresos == 0))
		{
			return SinDatos();
		}

		var promedio = serie.Where(s => s.Egresos > 0).Average(s => s.Egresos);

		return new AsistenteRespuestaDto
		{
			Tipo = AsistenteRespuestaTipo.Chart,
			Respuesta = $"Gasto mensual de los últimos {serie.Count} meses. Tu promedio es {promedio.ToString("C", Cultura)}: usalo como referencia de presupuesto.",
			Grafico = new GraficoDto
			{
				Tipo = TipoGrafico.Barras,
				Titulo = "Gasto mensual vs presupuesto",
				Subtitulo = $"Promedio: {promedio.ToString("C", Cultura)}",
				Etiquetas = serie.Select(s => s.Mes).ToList(),
				Valores = serie.Select(s => s.Egresos).ToList(),
				EtiquetaSerie = "Egresos",
				Total = serie.Sum(s => s.Egresos),
				Periodo = $"Últimos {serie.Count} meses"
			}
		};
	}

	private static AsistenteRespuestaDto GraficoSerieMensual(IReadOnlyCollection<Core.Entities.Movimiento> movimientos, TipoMovimiento tipo, string titulo, TipoGrafico tipoGrafico)
	{
		var serie = SerieMensual(movimientos, 6);
		var valores = serie.Select(s => tipo == TipoMovimiento.Ingreso ? s.Ingresos : s.Egresos).ToList();

		if (serie.Count == 0 || valores.All(v => v == 0))
		{
			return SinDatos();
		}

		var total = valores.Sum();

		return new AsistenteRespuestaDto
		{
			Tipo = AsistenteRespuestaTipo.Chart,
			Respuesta = $"{titulo}: el total del período es {total.ToString("C", Cultura)}.",
			Grafico = new GraficoDto
			{
				Tipo = tipoGrafico,
				Titulo = titulo,
				Subtitulo = $"Últimos {serie.Count} meses",
				Etiquetas = serie.Select(s => s.Mes).ToList(),
				Valores = valores,
				EtiquetaSerie = tipo == TipoMovimiento.Ingreso ? "Ingresos" : "Egresos",
				Total = total,
				Periodo = $"Últimos {serie.Count} meses"
			}
		};
	}

	private static List<TotalMensualDto> SerieMensual(IReadOnlyCollection<Core.Entities.Movimiento> movimientos, int meses)
	{
		var hoy = DateTime.Today;
		var inicio = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-(meses - 1));

		return movimientos
			.Where(m => m.Fecha >= inicio)
			.GroupBy(m => new { m.Fecha.Year, m.Fecha.Month })
			.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
			.Select(g => new TotalMensualDto
			{
				Mes = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yy", Cultura),
				Ingresos = g.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.Monto),
				Egresos = g.Where(m => m.Tipo == TipoMovimiento.Egreso).Sum(m => m.Monto)
			})
			.ToList();
	}

	private static string QuitarAcentos(string texto)
	{
		var normalized = texto.Normalize(System.Text.NormalizationForm.FormD);
		var sb = new System.Text.StringBuilder(normalized.Length);
		foreach (var c in normalized)
		{
			if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
			{
				sb.Append(c);
			}
		}
		return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
	}

	#endregion

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
