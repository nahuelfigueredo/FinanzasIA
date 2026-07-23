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
			return new AsistenteRespuestaDto
			{
				Respuesta = resultado.Respuesta,
				AccionEjecutada = true,
				Bloques =
				[
					new TextBlockDto { Texto = resultado.Respuesta },
					new ActionBlockDto
					{
						Acciones =
						[
							new ActionItemDto { Etiqueta = "Ver movimientos", Icono = "📄", Tipo = ActionKind.Navegar, Payload = "/movimientos" },
							new ActionItemDto { Etiqueta = "Ver balance", Icono = "📊", Tipo = ActionKind.EnviarMensaje, Payload = "Balance mensual" }
						]
					}
				]
			};
		}

		var movimientos = await _movimientoRepository.GetAllAsync(usuarioId, cancellationToken);

		// Si el usuario pide una visualización, se compone una respuesta de bloques con datos reales.
		var respuestaRica = ComponerRespuestaRica(pregunta.Pregunta.Trim(), movimientos);
		if (respuestaRica is not null)
		{
			return respuestaRica;
		}

		var contexto = ConstruirContexto(movimientos);
		var respuesta = await _iaProvider.GenerarRespuestaAsync(pregunta.Pregunta.Trim(), contexto, pregunta.Historial, cancellationToken);

		return new AsistenteRespuestaDto
		{
			Respuesta = respuesta,
			Bloques = [new TextBlockDto { Texto = respuesta }]
		};
	}

	/// <summary>Determina si la intención corresponde a registrar un movimiento.</summary>
	private static bool EsRegistroMovimiento(MessageIntent intent) =>
		intent is MessageIntent.RegistrarGasto or MessageIntent.RegistrarIngreso or MessageIntent.Transferencia;

	#region Respuestas enriquecidas (bloques)

	private static readonly CultureInfo Cultura = new("es-AR");

	/// <summary>
	/// Detecta si la pregunta pide una visualización y, de ser así, compone una
	/// respuesta enriquecida (bloques) con datos reales del usuario. Devuelve
	/// null si no corresponde (el flujo sigue con la IA de texto).
	/// </summary>
	private static AsistenteRespuestaDto? ComponerRespuestaRica(string pregunta, IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
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
			|| mencionaBalance
			|| (mencionaGrafico && mencionaPresupuesto);

		if (!pideVisual)
		{
			return null;
		}

		if (movimientos.Count == 0)
		{
			return SinDatos();
		}

		List<ResponseBlockDto>? bloques;

		if (mencionaBalance)
		{
			bloques = BloquesBalance(movimientos);
		}
		else if (mencionaTop && mencionaCategoria)
		{
			bloques = BloquesTopCategorias(movimientos, texto);
		}
		else if (mencionaCategoria)
		{
			bloques = BloquesGastosPorCategoria(movimientos);
		}
		else if (mencionaPresupuesto)
		{
			bloques = BloquesPresupuesto(movimientos);
		}
		else if (mencionaIngresos && (mencionaComparar || mencionaEvolucion || mencionaGrafico))
		{
			bloques = BloquesSerieMensual(movimientos, TipoMovimiento.Ingreso, "Evolución de ingresos", ChartKind.Line);
		}
		else if (mencionaGastos && mencionaEvolucion)
		{
			bloques = BloquesSerieMensual(movimientos, TipoMovimiento.Egreso, "Evolución de gastos", ChartKind.Area);
		}
		else if (mencionaComparar)
		{
			bloques = BloquesComparativaMeses(movimientos, texto);
		}
		else
		{
			// Pedido genérico de gráfico → gastos por categoría del mes.
			bloques = BloquesGastosPorCategoria(movimientos);
		}

		if (bloques is null)
		{
			return SinDatos();
		}

		var textoPlano = bloques.OfType<TextBlockDto>().FirstOrDefault()?.Texto ?? string.Empty;
		return new AsistenteRespuestaDto { Respuesta = textoPlano, Bloques = bloques };
	}

	private static AsistenteRespuestaDto SinDatos()
	{
		const string mensaje = "Todavía no hay movimientos suficientes para generar esa visualización.";
		return new AsistenteRespuestaDto
		{
			Respuesta = mensaje,
			Bloques =
			[
				new CardBlockDto
				{
					Titulo = "Sin datos suficientes",
					Valor = "📊",
					Icono = "📭",
					Descripcion = "Registrá algunos ingresos o gastos y volvé a pedirme el gráfico.",
					Color = "#6366f1"
				},
				new ActionBlockDto
				{
					Acciones =
					[
						new ActionItemDto { Etiqueta = "Registrar gasto", Icono = "➖", Tipo = ActionKind.EnviarMensaje, Payload = "Quiero registrar un gasto" },
						new ActionItemDto { Etiqueta = "Ver movimientos", Icono = "📄", Tipo = ActionKind.Navegar, Payload = "/movimientos" }
					]
				}
			]
		};
	}

	private static List<ResponseBlockDto>? BloquesGastosPorCategoria(IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
	{
		var hoy = DateTime.Today;
		var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
		var egresos = movimientos.Where(m => m.Tipo == TipoMovimiento.Egreso && m.Fecha >= inicioMes).ToList();

		if (egresos.Count == 0)
		{
			return null;
		}

		var porCategoria = egresos
			.GroupBy(m => m.Categoria?.Nombre ?? "Sin categoría")
			.Select(g => new { Categoria = g.Key, Total = g.Sum(m => m.Monto) })
			.OrderByDescending(g => g.Total)
			.ToList();

		var total = porCategoria.Sum(c => c.Total);
		var periodo = inicioMes.ToString("MMMM yyyy", Cultura);

		return
		[
			new TextBlockDto { Texto = $"Acá tenés tus gastos por categoría de {periodo}." },
			new ChartBlockDto
			{
				TipoGrafico = ChartKind.Pie,
				Titulo = "Gastos por categoría",
				Subtitulo = periodo,
				Etiquetas = porCategoria.Select(c => c.Categoria).ToList(),
				Valores = porCategoria.Select(c => c.Total).ToList(),
				Total = total,
				Periodo = periodo,
				Descripcion = $"Total del período: {total.ToString("C", Cultura)}"
			},
			new SuggestionBlockDto
			{
				Sugerencias = ["Compará con el mes anterior", "Top 5 categorías", "¿Cómo evolucionaron mis gastos?"]
			}
		];
	}

	private static List<ResponseBlockDto>? BloquesTopCategorias(IReadOnlyCollection<Core.Entities.Movimiento> movimientos, string texto)
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
			return null;
		}

		var top = egresos
			.GroupBy(m => m.Categoria?.Nombre ?? "Sin categoría")
			.Select(g => new { Categoria = g.Key, Total = g.Sum(m => m.Monto), Cantidad = g.Count() })
			.OrderByDescending(g => g.Total)
			.Take(cantidad)
			.ToList();

		var total = top.Sum(c => c.Total);

		return
		[
			new TextBlockDto { Texto = $"Estas son tus {top.Count} categorías con mayor gasto histórico." },
			new ChartBlockDto
			{
				TipoGrafico = ChartKind.Bar,
				Titulo = $"Top {top.Count} categorías por gasto",
				Subtitulo = "Histórico",
				Etiquetas = top.Select(c => c.Categoria).ToList(),
				Valores = top.Select(c => c.Total).ToList(),
				Total = total,
				Periodo = "Histórico"
			},
			new TableBlockDto
			{
				Titulo = "Detalle",
				Columnas = ["Categoría", "Movimientos", "Total"],
				Filas = top.Select(c => new List<string> { c.Categoria, c.Cantidad.ToString(Cultura), c.Total.ToString("C0", Cultura) }).ToList(),
				Totales = ["Total", top.Sum(c => c.Cantidad).ToString(Cultura), total.ToString("C0", Cultura)]
			},
			new SuggestionBlockDto { Sugerencias = ["Gastos por categoría de este mes", "Balance mensual", "¿Cómo puedo ahorrar?"] }
		];
	}

	private static List<ResponseBlockDto>? BloquesBalance(IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
	{
		var serie = SerieMensual(movimientos, 6);
		if (serie.Count == 0)
		{
			return null;
		}

		var totalIngresos = serie.Sum(s => s.Ingresos);
		var totalEgresos = serie.Sum(s => s.Egresos);
		var balance = totalIngresos - totalEgresos;

		return
		[
			new TextBlockDto { Texto = $"Este es tu balance de los últimos {serie.Count} meses." },
			new CardBlockDto
			{
				Titulo = "Balance del período",
				Valor = balance.ToString("C0", Cultura),
				Icono = balance >= 0 ? "💰" : "⚠️",
				Color = balance >= 0 ? "#22c55e" : "#ef4444",
				Variacion = $"Ingresos {totalIngresos.ToString("C0", Cultura)} · Egresos {totalEgresos.ToString("C0", Cultura)}"
			},
			new ChartBlockDto
			{
				TipoGrafico = ChartKind.Column,
				Titulo = "Balance mensual",
				Subtitulo = "Ingresos vs egresos",
				Etiquetas = serie.Select(s => s.Mes).ToList(),
				Valores = serie.Select(s => s.Ingresos).ToList(),
				ValoresSecundarios = serie.Select(s => s.Egresos).ToList(),
				EtiquetaSerie = "Ingresos",
				EtiquetaSerieSecundaria = "Egresos",
				Total = balance,
				Periodo = $"Últimos {serie.Count} meses"
			},
			new TableBlockDto
			{
				Titulo = "Detalle mensual",
				Columnas = ["Mes", "Ingresos", "Egresos", "Balance"],
				Filas = serie.Select(s => new List<string>
				{
					s.Mes,
					s.Ingresos.ToString("C0", Cultura),
					s.Egresos.ToString("C0", Cultura),
					(s.Ingresos - s.Egresos).ToString("C0", Cultura)
				}).ToList(),
				Totales = ["Total", totalIngresos.ToString("C0", Cultura), totalEgresos.ToString("C0", Cultura), balance.ToString("C0", Cultura)]
			},
			new SuggestionBlockDto { Sugerencias = ["Gastos por categoría", "Evolución de ingresos", "Gráfico del presupuesto"] },
			new ActionBlockDto
			{
				Acciones =
				[
					new ActionItemDto { Etiqueta = "Ver reportes", Icono = "📈", Tipo = ActionKind.Navegar, Payload = "/reportes" },
					new ActionItemDto { Etiqueta = "Exportar", Icono = "📤", Tipo = ActionKind.Navegar, Payload = "/reportes/exportar-pdf" }
				]
			}
		];
	}

	private static List<ResponseBlockDto>? BloquesComparativaMeses(IReadOnlyCollection<Core.Entities.Movimiento> movimientos, string texto)
	{
		var meses = 2;
		if (texto.Contains("seis") || texto.Contains("6")) { meses = 6; }
		else if (texto.Contains("tres") || texto.Contains("3") || texto.Contains("trimestre")) { meses = 3; }

		var serie = SerieMensual(movimientos, meses);
		if (serie.Count == 0)
		{
			return null;
		}

		var totalIngresos = serie.Sum(s => s.Ingresos);
		var totalEgresos = serie.Sum(s => s.Egresos);

		return
		[
			new TextBlockDto { Texto = $"Comparativa de los últimos {serie.Count} meses: ingresos vs egresos." },
			new ChartBlockDto
			{
				TipoGrafico = ChartKind.Column,
				Titulo = serie.Count == 2 ? "Este mes vs. el anterior" : $"Comparativa de {serie.Count} meses",
				Subtitulo = "Ingresos vs egresos",
				Etiquetas = serie.Select(s => s.Mes).ToList(),
				Valores = serie.Select(s => s.Ingresos).ToList(),
				ValoresSecundarios = serie.Select(s => s.Egresos).ToList(),
				EtiquetaSerie = "Ingresos",
				EtiquetaSerieSecundaria = "Egresos",
				Total = totalIngresos - totalEgresos,
				Periodo = $"Últimos {serie.Count} meses"
			},
			new SuggestionBlockDto { Sugerencias = ["Balance mensual", "Gastos por categoría", "Top categorías"] }
		];
	}

	private static List<ResponseBlockDto>? BloquesPresupuesto(IReadOnlyCollection<Core.Entities.Movimiento> movimientos)
	{
		var serie = SerieMensual(movimientos, 6);
		if (serie.Count == 0 || serie.All(s => s.Egresos == 0))
		{
			return null;
		}

		var promedio = serie.Where(s => s.Egresos > 0).Average(s => s.Egresos);
		var mesActual = serie[^1].Egresos;
		var variacion = promedio > 0 ? Math.Round((mesActual - promedio) / promedio * 100, 1) : 0m;

		return
		[
			new TextBlockDto { Texto = $"Gasto mensual de los últimos {serie.Count} meses, con tu promedio como referencia de presupuesto." },
			new CardBlockDto
			{
				Titulo = "Promedio mensual de gasto",
				Valor = promedio.ToString("C0", Cultura),
				Icono = "🎯",
				Color = variacion > 0 ? "#ef4444" : "#22c55e",
				Variacion = $"{(variacion > 0 ? "+" : "")}{variacion}% este mes vs promedio"
			},
			new ChartBlockDto
			{
				TipoGrafico = ChartKind.Column,
				Titulo = "Gasto mensual vs presupuesto",
				Subtitulo = $"Promedio: {promedio.ToString("C0", Cultura)}",
				Etiquetas = serie.Select(s => s.Mes).ToList(),
				Valores = serie.Select(s => s.Egresos).ToList(),
				EtiquetaSerie = "Egresos",
				Total = serie.Sum(s => s.Egresos),
				Periodo = $"Últimos {serie.Count} meses"
			},
			new SuggestionBlockDto { Sugerencias = ["¿Cómo puedo ahorrar?", "Balance mensual", "Gastos por categoría"] }
		];
	}

	private static List<ResponseBlockDto>? BloquesSerieMensual(IReadOnlyCollection<Core.Entities.Movimiento> movimientos, TipoMovimiento tipo, string titulo, ChartKind tipoGrafico)
	{
		var serie = SerieMensual(movimientos, 6);
		var valores = serie.Select(s => tipo == TipoMovimiento.Ingreso ? s.Ingresos : s.Egresos).ToList();

		if (serie.Count == 0 || valores.All(v => v == 0))
		{
			return null;
		}

		var total = valores.Sum();

		return
		[
			new TextBlockDto { Texto = $"{titulo} de los últimos {serie.Count} meses." },
			new ChartBlockDto
			{
				TipoGrafico = tipoGrafico,
				Titulo = titulo,
				Subtitulo = $"Últimos {serie.Count} meses",
				Etiquetas = serie.Select(s => s.Mes).ToList(),
				Valores = valores,
				EtiquetaSerie = tipo == TipoMovimiento.Ingreso ? "Ingresos" : "Egresos",
				Total = total,
				Periodo = $"Últimos {serie.Count} meses",
				Descripcion = $"Total del período: {total.ToString("C", Cultura)}"
			},
			new SuggestionBlockDto { Sugerencias = ["Balance mensual", "Compará este mes con el anterior", "Top categorías"] }
		];
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
