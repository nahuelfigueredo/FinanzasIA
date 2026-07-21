using System.Diagnostics;
using System.Globalization;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Orquesta la carga automática de gastos desde imágenes de tickets:
/// OCR → parseo → sugerencia de categoría → creación del movimiento.
/// Si falta el importe, la fecha o el comercio, guarda un ticket pendiente
/// y pide únicamente el dato faltante; la respuesta del usuario lo completa.
/// </summary>
public class TicketProcessor : ITicketProcessor
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>Mapa comercio → categoría sugerida (heurística por palabras clave).</summary>
    private static readonly (string[] Claves, string Categoria)[] CategoriasPorComercio =
    [
        (["super", "market", "carrefour", "coto", "dia", "vea", "disco", "chango"], "Supermercado"),
        (["farmacia", "farmacity"], "Farmacia"),
        (["ypf", "shell", "axion", "puma", "nafta", "combustible"], "Nafta"),
        (["carnic", "polleria", "pescad"], "Carnicería"),
        (["restaurant", "resto", "parrilla", "pizz", "burger", "cafe", "café", "bar "], "Restaurante"),
        (["ferreteria", "ferretería"], "Ferretería"),
        (["kiosco", "kiosko", "drugstore"], "Kiosco"),
        (["indumentaria", "ropa", "zapat"], "Ropa")
    ];

    private readonly ITicketOcrProvider _ocrProvider;
    private readonly ITicketPendienteRepository _ticketRepository;
    private readonly IMovimientoService _movimientoService;
    private readonly ICategoriaService _categoriaService;
    private readonly ILogger<TicketProcessor> _logger;

    public TicketProcessor(
        ITicketOcrProvider ocrProvider,
        ITicketPendienteRepository ticketRepository,
        IMovimientoService movimientoService,
        ICategoriaService categoriaService,
        ILogger<TicketProcessor> logger)
    {
        _ocrProvider = ocrProvider;
        _ticketRepository = ticketRepository;
        _movimientoService = movimientoService;
        _categoriaService = categoriaService;
        _logger = logger;
    }

    public async Task<TicketResultDto> ProcesarImagenAsync(TicketImagenDto imagen, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Procesando imagen de ticket del usuario {UsuarioId} ({Bytes} bytes).", imagen.UsuarioId, imagen.Contenido.Length);

        string texto;
        try
        {
            texto = await _ocrProvider.ExtraerTextoAsync(imagen.Contenido, imagen.MimeType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de OCR al procesar el ticket del usuario {UsuarioId}.", imagen.UsuarioId);
            return new TicketResultDto
            {
                Respuesta = "No pude leer la imagen del ticket. 🙏\n\nProbá con una foto más nítida y con buena luz."
            };
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            return new TicketResultDto
            {
                Respuesta = "No pude reconocer texto en la imagen. 🤔\n\nProbá con una foto más nítida del ticket."
            };
        }

        var datos = TicketParser.Parsear(texto);
        _logger.LogInformation("OCR completado en {Ms} ms. Monto={Monto}, Fecha={Fecha}, Comercio={Comercio}.",
            stopwatch.ElapsedMilliseconds, datos.Monto, datos.Fecha, datos.Comercio);

        var ticket = new TicketPendiente
        {
            UsuarioId = imagen.UsuarioId ?? string.Empty,
            TextoOcr = texto.Length > 4000 ? texto[..4000] : texto,
            Monto = datos.Monto,
            Fecha = datos.Fecha,
            Comercio = datos.Comercio,
            CategoriaSugerida = SugerirCategoria(datos.Comercio, texto)
        };

        var faltante = ObtenerDatoFaltante(ticket);
        if (faltante is null)
        {
            return await RegistrarMovimientoAsync(ticket, cancellationToken);
        }

        // Desactivar cualquier pendiente anterior para no mezclar tickets.
        await DesactivarPendienteAnteriorAsync(ticket.UsuarioId, cancellationToken);

        ticket.DatoSolicitado = faltante;
        await _ticketRepository.AgregarAsync(ticket, cancellationToken);

        return new TicketResultDto
        {
            EsperandoDato = true,
            Respuesta = ConstruirPregunta(ticket, faltante)
        };
    }

    public async Task<bool> TienePendienteAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _ticketRepository.ObtenerActivoAsync(usuarioId, cancellationToken) is not null;
    }

    public async Task<TicketResultDto> CompletarDatoAsync(string usuarioId, string respuestaUsuario, CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.ObtenerActivoAsync(usuarioId, cancellationToken);
        if (ticket is null)
        {
            return new TicketResultDto { Respuesta = "No tenés ningún ticket pendiente de completar." };
        }

        var texto = respuestaUsuario.Trim();

        // El usuario puede cancelar el ticket pendiente.
        if (texto.Equals("cancelar", StringComparison.OrdinalIgnoreCase))
        {
            ticket.Activo = false;
            await _ticketRepository.ActualizarAsync(ticket, cancellationToken);
            return new TicketResultDto { Respuesta = "Listo, descarté el ticket. 👍" };
        }

        switch (ticket.DatoSolicitado)
        {
            case "monto":
                var monto = TicketParser.ParsearDecimal(texto.Replace("$", string.Empty));
                if (monto is not > 0)
                {
                    return new TicketResultDto
                    {
                        EsperandoDato = true,
                        Respuesta = "No entendí el monto. 🤔\n\nEscribí solo el número, por ejemplo: 15000\n(o escribí \"cancelar\" para descartar el ticket)"
                    };
                }
                ticket.Monto = monto;
                break;

            case "fecha":
                if (texto.Equals("hoy", StringComparison.OrdinalIgnoreCase))
                {
                    ticket.Fecha = DateTime.Today;
                }
                else if (DateTime.TryParseExact(texto, ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy"],
                             Cultura, DateTimeStyles.None, out var fecha) && fecha <= DateTime.Now.AddDays(1))
                {
                    ticket.Fecha = fecha;
                }
                else
                {
                    return new TicketResultDto
                    {
                        EsperandoDato = true,
                        Respuesta = "No entendí la fecha. 🤔\n\nEscribila como 21/07/2026 o escribí \"hoy\"\n(o \"cancelar\" para descartar el ticket)"
                    };
                }
                break;

            case "comercio":
                if (texto.Length < 2)
                {
                    return new TicketResultDto
                    {
                        EsperandoDato = true,
                        Respuesta = "Escribí el nombre del comercio, por ejemplo: Carrefour\n(o \"cancelar\" para descartar el ticket)"
                    };
                }
                ticket.Comercio = texto;
                ticket.CategoriaSugerida ??= SugerirCategoria(texto, ticket.TextoOcr);
                break;

            default:
                ticket.Activo = false;
                await _ticketRepository.ActualizarAsync(ticket, cancellationToken);
                return new TicketResultDto { Respuesta = "Hubo un problema con el ticket pendiente. Volvé a enviar la foto. 🙏" };
        }

        // ¿Falta otro dato todavía?
        var faltante = ObtenerDatoFaltante(ticket);
        if (faltante is not null)
        {
            ticket.DatoSolicitado = faltante;
            await _ticketRepository.ActualizarAsync(ticket, cancellationToken);
            return new TicketResultDto
            {
                EsperandoDato = true,
                Respuesta = ConstruirPregunta(ticket, faltante)
            };
        }

        var resultado = await RegistrarMovimientoAsync(ticket, cancellationToken);
        if (resultado.MovimientoCreado)
        {
            ticket.Activo = false;
        }
        await _ticketRepository.ActualizarAsync(ticket, cancellationToken);
        return resultado;
    }

    /// <summary>Devuelve "monto", "fecha" o "comercio" según el primer dato faltante, o null si está completo.</summary>
    private static string? ObtenerDatoFaltante(TicketPendiente ticket)
    {
        if (ticket.Monto is not > 0) return "monto";
        if (ticket.Fecha is null) return "fecha";
        if (string.IsNullOrWhiteSpace(ticket.Comercio)) return "comercio";
        return null;
    }

    private static string ConstruirPregunta(TicketPendiente ticket, string faltante)
    {
        var detectado = ResumenDetectado(ticket);
        var pregunta = faltante switch
        {
            "monto" => "¿Cuál es el importe total del ticket? 💵\nEscribí solo el número, por ejemplo: 15000",
            "fecha" => "¿De qué fecha es el ticket? 📅\nEscribila como 21/07/2026 o escribí \"hoy\"",
            _ => "¿En qué comercio fue la compra? 🏪\nPor ejemplo: Carrefour"
        };

        return $"📷 Recibí tu ticket.\n{detectado}\nMe falta un dato:\n\n{pregunta}";
    }

    private static string ResumenDetectado(TicketPendiente ticket)
    {
        var partes = new List<string>();
        if (ticket.Monto is > 0) partes.Add($"Monto: {ticket.Monto.Value.ToString("C0", Cultura)}");
        if (ticket.Fecha is not null) partes.Add($"Fecha: {ticket.Fecha:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(ticket.Comercio)) partes.Add($"Comercio: {ticket.Comercio}");
        return partes.Count == 0 ? string.Empty : "Detecté:\n" + string.Join("\n", partes) + "\n";
    }

    private async Task<TicketResultDto> RegistrarMovimientoAsync(TicketPendiente ticket, CancellationToken cancellationToken)
    {
        var categoria = await ResolverCategoriaAsync(ticket, cancellationToken);

        var movimiento = await _movimientoService.CreateAsync(new CreateMovimientoDto
        {
            Tipo = TipoMovimiento.Egreso,
            CategoriaId = categoria.Id,
            Descripcion = ticket.Comercio ?? "Ticket",
            Monto = ticket.Monto!.Value,
            Fecha = ticket.Fecha!.Value
        }, ticket.UsuarioId, cancellationToken);

        if (movimiento is null)
        {
            _logger.LogError("No se pudo crear el movimiento del ticket para el usuario {UsuarioId}.", ticket.UsuarioId);
            return new TicketResultDto { Respuesta = "No pude registrar el movimiento. Intentá de nuevo en unos segundos. 🙏" };
        }

        _logger.LogInformation("Movimiento {MovimientoId} creado desde ticket para el usuario {UsuarioId}.", movimiento.Id, ticket.UsuarioId);

        return new TicketResultDto
        {
            MovimientoCreado = true,
            MovimientoId = movimiento.Id,
            Respuesta =
                "✅ Gasto registrado desde tu ticket.\n\n" +
                $"Monto: {ticket.Monto.Value.ToString("C0", Cultura)}\n" +
                $"Comercio: {ticket.Comercio}\n" +
                $"Categoría: {categoria.Nombre}\n" +
                $"Fecha: {ticket.Fecha:dd/MM/yyyy}"
        };
    }

    /// <summary>Busca la categoría sugerida entre las existentes o la crea; nunca falla.</summary>
    private async Task<CategoriaDto> ResolverCategoriaAsync(TicketPendiente ticket, CancellationToken cancellationToken)
    {
        var nombre = ticket.CategoriaSugerida ?? SugerirCategoria(ticket.Comercio, ticket.TextoOcr) ?? "Otros";

        var categorias = await _categoriaService.GetAllAsync(ticket.UsuarioId, cancellationToken);
        var existente = categorias
            .Where(c => c.TipoMovimiento == TipoMovimiento.Egreso)
            .FirstOrDefault(c => c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)
                || c.Nombre.Contains(nombre, StringComparison.OrdinalIgnoreCase)
                || nombre.Contains(c.Nombre, StringComparison.OrdinalIgnoreCase));

        return existente ?? await _categoriaService.CreateAsync(new CreateCategoriaDto
        {
            Nombre = nombre,
            TipoMovimiento = TipoMovimiento.Egreso
        }, ticket.UsuarioId, cancellationToken);
    }

    /// <summary>Sugiere una categoría a partir del nombre del comercio y el texto del ticket.</summary>
    private static string? SugerirCategoria(string? comercio, string textoOcr)
    {
        var texto = $"{comercio} {textoOcr}".ToLowerInvariant();
        foreach (var (claves, categoria) in CategoriasPorComercio)
        {
            if (claves.Any(texto.Contains))
            {
                return categoria;
            }
        }

        return null;
    }

    private async Task DesactivarPendienteAnteriorAsync(string usuarioId, CancellationToken cancellationToken)
    {
        var anterior = await _ticketRepository.ObtenerActivoAsync(usuarioId, cancellationToken);
        if (anterior is not null)
        {
            anterior.Activo = false;
            await _ticketRepository.ActualizarAsync(anterior, cancellationToken);
        }
    }
}
