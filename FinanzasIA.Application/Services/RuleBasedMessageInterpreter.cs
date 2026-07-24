using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Intérprete de mensajes basado en reglas. En lugar de if/contains sueltos,
/// usa una tabla declarativa de definiciones de intención (patrones regex +
/// extractores) que se evalúa en orden de prioridad. Agregar una intención
/// nueva = agregar una entrada a la tabla.
/// Implementación intercambiable por <c>OpenAIMessageInterpreter</c> vía DI.
/// </summary>
public partial class RuleBasedMessageInterpreter : IMessageInterpreter
{
    /// <summary>
    /// Definición declarativa de una intención: patrón que la dispara y si
    /// requiere extraer datos de movimiento.
    /// </summary>
    private sealed record IntentDefinition(MessageIntent Intent, Regex Patron, TipoMovimiento? Tipo = null);

    private static readonly Regex MontoRegex = new(
        @"\$?\s*(\d{1,3}(?:\.\d{3})+|\d+)(?:,(\d{1,2}))?",
        RegexOptions.Compiled);

    private static readonly Regex CuentaRegex = new(
        @"\b(?:con|desde|de la cuenta|usando|a)\s+(mercado\s*pago|banco\s+\w+(?:\s+\w+)?|efectivo|uala|ual\u00e1|naranja\s*x|galicia|santander|bbva|brubank|billetera)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly IntentDefinition[] Definiciones =
    [
        // Consultas primero: "cuanto gaste hoy" no debe interpretarse como registro de gasto.
        new(MessageIntent.GastosHoy, new Regex(@"(cuanto\s+gaste\s+hoy|gastos?\s+de\s+hoy)", RegexOptions.Compiled)),
        new(MessageIntent.GastosMes, new Regex(@"(cuanto\s+gaste\s+(?:este\s+)?mes|gastos?\s+del?\s+mes|gastos?\s+de\s+este\s+mes)", RegexOptions.Compiled)),
        new(MessageIntent.IngresosMes, new Regex(@"(cuanto\s+(?:cobre|ingrese|gane)\s+(?:este\s+)?mes|ingresos?\s+del?\s+mes|ingresos?\s+de\s+este\s+mes)", RegexOptions.Compiled)),
        new(MessageIntent.UltimosMovimientos, new Regex(@"\b(ultimos?\s+(?:gastos?|movimientos?)|movimientos?\s+recientes)\b", RegexOptions.Compiled)),
        new(MessageIntent.Transferencia, new Regex(@"\b(transferi|transfiero|transferencia|envie|mande)\b", RegexOptions.Compiled), TipoMovimiento.Egreso),
        new(MessageIntent.RegistrarIngreso, new Regex(@"\b(cobre|ingreso|ingresaron|recibi|me pagaron|me depositaron|gane)\b", RegexOptions.Compiled), TipoMovimiento.Ingreso),
        new(MessageIntent.RegistrarGasto, new Regex(@"\b(gaste|compre|pague|abone|gasto de|compra de)\b", RegexOptions.Compiled), TipoMovimiento.Egreso),
        new(MessageIntent.ConsultaSaldo, new Regex(@"\b(saldo|balance|cuanto tengo|cuanta plata)\b", RegexOptions.Compiled)),
        new(MessageIntent.ResumenMensual, new Regex(@"\b(resumen|analisis|como vengo|como estoy)\b", RegexOptions.Compiled)),
        new(MessageIntent.ConsultaCategoria, new Regex(@"\b(categoria|rubro|en que gasto|donde gasto)\b", RegexOptions.Compiled)),
        // Definir presupuesto antes que la consulta: "presupuesto supermercado 350000" trae monto.
        new(MessageIntent.DefinirPresupuesto, new Regex(@"\b(?:presupuesto|gastar\s+(?:maximo|max|hasta))\b.*\d", RegexOptions.Compiled)),
        new(MessageIntent.ConsultaPresupuesto, new Regex(@"\b(presupuesto)\b", RegexOptions.Compiled)),
        new(MessageIntent.Ayuda, new Regex(@"\b(ayuda|help|comandos|que puedo)\b", RegexOptions.Compiled)),
        new(MessageIntent.Saludo, new Regex(@"\b(hola|buenas|buen dia|buenos dias|buenas tardes|buenas noches)\b", RegexOptions.Compiled))
    ];

    public Task<MensajeInterpretadoDto> InterpretarAsync(string texto, CancellationToken cancellationToken = default)
    {
        var normalizado = Normalizar(texto);

        var definicion = Definiciones.FirstOrDefault(d => d.Patron.IsMatch(normalizado));
        if (definicion is null)
        {
            return Task.FromResult(new MensajeInterpretadoDto { Intent = MessageIntent.Desconocido });
        }

        var resultado = new MensajeInterpretadoDto
        {
            Intent = definicion.Intent,
            TipoMovimiento = definicion.Tipo,
            Fecha = DateTime.Now
        };

        if (definicion.Tipo is not null)
        {
            ExtraerDatosMovimiento(texto, normalizado, resultado);
        }
        else if (definicion.Intent == MessageIntent.DefinirPresupuesto)
        {
            ExtraerDatosPresupuesto(normalizado, resultado);
        }

        return Task.FromResult(resultado);
    }

    /// <summary>
    /// Extrae categoría y monto de frases de presupuesto:
    /// "presupuesto supermercado 350000", "mi presupuesto para comida es 500000",
    /// "quiero gastar maximo 200000 en combustible", "presupuesto ocio 100000".
    /// </summary>
    private static void ExtraerDatosPresupuesto(string normalizado, MensajeInterpretadoDto resultado)
    {
        var montoMatch = MontoRegex.Match(normalizado);
        if (montoMatch.Success)
        {
            var entero = montoMatch.Groups[1].Value.Replace(".", string.Empty);
            var decimales = montoMatch.Groups[2].Success ? montoMatch.Groups[2].Value : "0";
            if (decimal.TryParse($"{entero}.{decimales}", NumberStyles.Number, CultureInfo.InvariantCulture, out var monto) && monto > 0)
            {
                resultado.Monto = monto;
            }
        }

        // "presupuesto [para/de] X 350000" o "mi presupuesto para X es 350000"
        var categoriaMatch = Regex.Match(normalizado, @"presupuesto\s+(?:para\s+|de\s+)?([a-zñ]+(?:\s+[a-zñ]+)?)\s+(?:es\s+)?\$?\d");
        if (!categoriaMatch.Success)
        {
            // "quiero gastar maximo 200000 en X"
            categoriaMatch = Regex.Match(normalizado, @"gastar\s+(?:maximo|max|hasta)\s+\$?[\d.,]+\s+en\s+([a-zñ]+(?:\s+[a-zñ]+)?)");
        }

        if (categoriaMatch.Success)
        {
            resultado.Categoria = categoriaMatch.Groups[1].Value.Trim();
        }
    }

    /// <summary>Extrae monto, cuenta, categoría y descripción del texto original.</summary>
    private static void ExtraerDatosMovimiento(string original, string normalizado, MensajeInterpretadoDto resultado)
    {
        var montoMatch = MontoRegex.Match(normalizado);
        if (montoMatch.Success)
        {
            var entero = montoMatch.Groups[1].Value.Replace(".", string.Empty);
            var decimales = montoMatch.Groups[2].Success ? montoMatch.Groups[2].Value : "0";
            if (decimal.TryParse($"{entero}.{decimales}", NumberStyles.Number, CultureInfo.InvariantCulture, out var monto) && monto > 0)
            {
                resultado.Monto = monto;
            }
        }

        var cuentaMatch = CuentaRegex.Match(normalizado);
        if (cuentaMatch.Success)
        {
            resultado.Cuenta = cuentaMatch.Groups[1].Value.Trim();
        }

        // Categoría: lo que sigue a "en"/"de"/"por" (ej.: "gasté 20000 en carnicería").
        var categoriaMatch = Regex.Match(normalizado, @"\b(?:en|de)\s+(?!la cuenta)([a-zñ]+(?:\s+[a-zñ]+)?)\b");
        if (categoriaMatch.Success)
        {
            var candidata = categoriaMatch.Groups[1].Value.Trim();
            if (!CuentaRegex.IsMatch($"con {candidata}"))
            {
                resultado.Categoria = candidata;
            }
        }

        // Si no hubo "en X", probar con el objeto de "compré X por" / "pagué X".
        if (resultado.Categoria is null)
        {
            var objetoMatch = Regex.Match(normalizado, @"\b(?:compre|pague|abone)\s+([a-zñ]+(?:\s+[a-zñ]+)?)\s*(?:por|\$|\d|$)");
            if (objetoMatch.Success)
            {
                resultado.Categoria = objetoMatch.Groups[1].Value.Trim();
            }
        }

        // Descripción: texto original sin el monto, limpio de verbos comunes.
        var descripcion = montoMatch.Success
            ? MontoRegex.Replace(original, string.Empty, 1)
            : original;
        descripcion = Regex.Replace(descripcion, @"\b(gast\u00e9|gaste|compr\u00e9|compre|pagu\u00e9|pague|cobr\u00e9|cobre|recib\u00ed|recibi|transfer\u00ed|transferi|abon\u00e9|abone|por|en|con|a)\b", string.Empty, RegexOptions.IgnoreCase);
        descripcion = Regex.Replace(descripcion, @"\s+", " ").Trim(' ', '.', ',', '$');

        resultado.Descripcion = string.IsNullOrWhiteSpace(descripcion)
            ? (resultado.TipoMovimiento == TipoMovimiento.Ingreso ? "Ingreso" : "Gasto")
            : Capitalizar(descripcion);
    }

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];

    /// <summary>Pasa a minúsculas y remueve acentos para comparar con los patrones.</summary>
    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
