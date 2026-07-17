using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using FinanzasIA.Api.DTOs;
using FinanzasIA.Api.Options;
using FinanzasIA.Api.Services;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly WhatsAppOptions _options;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IAnalisisFinancieroService _analisisFinancieroService;
    private readonly ICategoriaService _categoriaService;
    private readonly IMovimientoService _movimientoService;
    private readonly IUsuarioWhatsAppResolver _usuarioResolver;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IOptions<WhatsAppOptions> options,
        IWhatsAppService whatsAppService,
        IAnalisisFinancieroService analisisFinancieroService,
        ICategoriaService categoriaService,
        IMovimientoService movimientoService,
        IUsuarioWhatsAppResolver usuarioResolver,
        ILogger<WhatsAppWebhookController> logger)
    {
        _options = options.Value;
        _whatsAppService = whatsAppService;
        _analisisFinancieroService = analisisFinancieroService;
        _categoriaService = categoriaService;
        _movimientoService = movimientoService;
        _usuarioResolver = usuarioResolver;
        _logger = logger;
    }

    [HttpGet("webhook")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" &&
            !string.IsNullOrWhiteSpace(_options.VerifyToken) &&
            string.Equals(verifyToken, _options.VerifyToken, StringComparison.Ordinal))
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        return Unauthorized();
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook([FromBody] JsonDocument payload, CancellationToken cancellationToken)
    {
        var root = payload.RootElement;
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return Ok();
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value))
                {
                    continue;
                }

                if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var message in messages.EnumerateArray())
                {
                    if (!message.TryGetProperty("from", out var fromNode))
                    {
                        continue;
                    }

                    var from = fromNode.GetString();
                    var incomingText = ExtractIncomingText(message);
                    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(incomingText))
                    {
                        continue;
                    }

                    var usuarioId = await _usuarioResolver.ResolverUsuarioIdAsync(from, cancellationToken);
                    var response = await BuildResponseAsync(incomingText, usuarioId, cancellationToken);
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(_options.AccessToken) && !string.IsNullOrWhiteSpace(_options.PhoneNumberId))
                    {
                        await _whatsAppService.SendTextMessageAsync(from, response, cancellationToken);
                        _logger.LogInformation("WhatsApp reply sent to {Phone}.", from);
                    }
                    else
                    {
                        _logger.LogInformation("WhatsApp response not sent because credentials are missing. Response: {Response}", response);
                    }
                }
            }
        }

        return Ok();
    }

    [HttpPost("send-test")]
    public async Task<IActionResult> SendTestMessage([FromBody] SendWhatsAppMessageDto dto, CancellationToken cancellationToken)
    {
        await _whatsAppService.SendTextMessageAsync(dto.To, dto.Message, cancellationToken);
        return Ok(new { message = "Mensaje enviado a WhatsApp." });
    }

    private async Task<string> BuildResponseAsync(string incomingText, string? usuarioId, CancellationToken cancellationToken)
    {
        var normalizedText = incomingText.Trim().ToLowerInvariant();

        if (TryParseMovimiento(incomingText, out var parsedMovimiento))
        {
            var categoria = await GetOrCreateWhatsAppCategoriaAsync(parsedMovimiento.Tipo, usuarioId, cancellationToken);
            var movimiento = await _movimientoService.CreateAsync(new CreateMovimientoDto
            {
                Tipo = parsedMovimiento.Tipo,
                CategoriaId = categoria.Id,
                Descripcion = parsedMovimiento.Descripcion,
                Monto = parsedMovimiento.Monto,
                Fecha = DateTime.Today
            }, usuarioId, cancellationToken);

            if (movimiento is null)
            {
                return "No pude registrar el movimiento. Revisá las categorías en Finanzas IA.";
            }

            return parsedMovimiento.Tipo == TipoMovimiento.Ingreso
                ? $"Listo ✅ Registré el ingreso: {parsedMovimiento.Descripcion} por {parsedMovimiento.Monto:N2}."
                : $"Listo ✅ Registré la compra: {parsedMovimiento.Descripcion} por {parsedMovimiento.Monto:N2}.";
        }

        if (normalizedText.Contains("resumen") || normalizedText.Contains("analisis") || normalizedText.Contains("análisis"))
        {
            var analisis = await _analisisFinancieroService.ObtenerAnalisisAsync(usuarioId, cancellationToken);
            return
                $"Resumen FinanzasIA:\n" +
                $"Ingresos: {analisis.TotalIngresos:N2}\n" +
                $"Egresos: {analisis.TotalEgresos:N2}\n" +
                $"Balance: {analisis.BalanceNeto:N2}\n" +
                $"Tasa ahorro: {analisis.TasaAhorroPorcentaje:N2}\n" +
                $"Mayor gasto: {analisis.CategoriaMayorGasto}\n" +
                $"Proyección mes próximo: {analisis.ProyeccionBalanceProximoMes:N2}";
        }

        if (normalizedText.Contains("ayuda"))
        {
            return "Comandos disponibles:\n- resumen\n- analisis\n- ayuda\n- compré café 2500\n- gasté 12000 en supermercado\n- cobré sueldo 350000";
        }

        if (normalizedText.Contains("hola") || normalizedText.Contains("buenas"))
        {
            return "Hola 👋 Soy FinanzasIA. Escribí 'resumen' para recibir tu análisis financiero o mandame algo como 'compré café 2500'.";
        }

        return "No entendí tu mensaje. Escribí 'ayuda' o mandame algo como 'compré café 2500'.";
    }

    private async Task<CategoriaDto> GetOrCreateWhatsAppCategoriaAsync(TipoMovimiento tipo, string? usuarioId, CancellationToken cancellationToken)
    {
        var nombre = tipo == TipoMovimiento.Ingreso ? "Ingresos WhatsApp" : "Compras WhatsApp";
        var categorias = await _categoriaService.GetAllAsync(usuarioId, cancellationToken);
        var existing = categorias.FirstOrDefault(c => c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        return await _categoriaService.CreateAsync(new CreateCategoriaDto
        {
            Nombre = nombre,
            TipoMovimiento = tipo
        }, usuarioId, cancellationToken);
    }

    private static bool TryParseMovimiento(string text, out ParsedMovimiento parsedMovimiento)
    {
        parsedMovimiento = default;
        var normalizedText = text.Trim().ToLowerInvariant();
        var isIngreso = normalizedText.Contains("cobre") ||
            normalizedText.Contains("cobré") ||
            normalizedText.Contains("ingreso") ||
            normalizedText.Contains("me pagaron");

        var isEgreso = normalizedText.Contains("compre") ||
            normalizedText.Contains("compré") ||
            normalizedText.Contains("gaste") ||
            normalizedText.Contains("gasté") ||
            normalizedText.Contains("pague") ||
            normalizedText.Contains("pagué") ||
            normalizedText.Contains("compra");

        if (!isIngreso && !isEgreso)
        {
            return false;
        }

        var amountMatch = Regex.Match(text, @"(?<!\d)(\$?\s*\d+(?:[\.,]\d{1,2})?)(?!\d)");
        if (!amountMatch.Success)
        {
            return false;
        }

        var amountText = amountMatch.Groups[1].Value
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(',', '.');

        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            return false;
        }

        var description = text.Remove(amountMatch.Index, amountMatch.Length).Trim();
        description = Regex.Replace(description, @"\b(compre|compré|gaste|gasté|pague|pagué|compra|cobre|cobré|ingreso|me pagaron|en)\b", string.Empty, RegexOptions.IgnoreCase).Trim();
        description = Regex.Replace(description, @"\s+", " ");
        if (string.IsNullOrWhiteSpace(description))
        {
            description = isIngreso ? "Ingreso por WhatsApp" : "Compra por WhatsApp";
        }

        parsedMovimiento = new ParsedMovimiento(
            isIngreso ? TipoMovimiento.Ingreso : TipoMovimiento.Egreso,
            description,
            amount);
        return true;
    }

    private readonly record struct ParsedMovimiento(TipoMovimiento Tipo, string Descripcion, decimal Monto);

    private static string? ExtractIncomingText(JsonElement message)
    {
        if (message.TryGetProperty("text", out var textNode) &&
            textNode.TryGetProperty("body", out var bodyNode))
        {
            return bodyNode.GetString();
        }

        if (message.TryGetProperty("interactive", out var interactiveNode))
        {
            if (interactiveNode.TryGetProperty("button_reply", out var buttonReply) &&
                buttonReply.TryGetProperty("title", out var buttonTitle))
            {
                return buttonTitle.GetString();
            }

            if (interactiveNode.TryGetProperty("list_reply", out var listReply) &&
                listReply.TryGetProperty("title", out var listTitle))
            {
                return listTitle.GetString();
            }
        }

        return null;
    }
}
