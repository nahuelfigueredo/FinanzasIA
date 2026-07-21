using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Parser financiero por reglas y regex. Interpreta mensajes como:
/// "Gasté 25000 en supermercado", "Compré pan 3500", "Nafta 18000",
/// "Cobré sueldo 900000", "Me pagaron 50000", "Luz 15000", "Netflix 12000".
/// No usa IA: es determinístico y rápido. Cuando no entiende, devuelve
/// EsMovimiento = false para que el pipeline pruebe el siguiente nivel (IA).
/// </summary>
public class MensajeFinancieroParser : IMensajeFinancieroParser
{
    private static readonly Regex MontoRegex = new(
        @"\$?\s*(\d{1,3}(?:\.\d{3})+|\d+)(?:,(\d{1,2}))?",
        RegexOptions.Compiled);

    private static readonly Regex IngresoRegex = new(
        @"\b(cobre|cobro|sueldo|salario|ingreso|ingresaron|recibi|me pagaron|me depositaron|me transfirieron|gane|honorarios|aguinaldo)\b",
        RegexOptions.Compiled);

    private static readonly Regex EgresoRegex = new(
        @"\b(gaste|gasto|compre|compra|pague|pago|abone|transferi|envie|mande|debito|debitaron)\b",
        RegexOptions.Compiled);

    /// <summary>Palabras clave que sugieren una categoría conocida (para "Nafta 18000", "Luz 15000", etc.).</summary>
    private static readonly (string[] Claves, string Categoria)[] CategoriasConocidas =
    [
        (["supermercado", "super", "carrefour", "coto", "dia", "vea", "disco"], "Supermercado"),
        (["nafta", "combustible", "ypf", "shell", "axion"], "Nafta"),
        (["luz", "electricidad", "edesur", "edenor"], "Luz"),
        (["agua", "aysa"], "Agua"),
        (["gas", "metrogas"], "Gas"),
        (["internet", "wifi", "fibertel", "telecentro"], "Internet"),
        (["netflix", "spotify", "disney", "hbo", "prime", "youtube premium"], "Suscripciones"),
        (["celular", "telefono", "personal", "movistar", "claro"], "Teléfono"),
        (["alquiler", "expensas"], "Vivienda"),
        (["farmacia", "remedios", "medicamentos"], "Farmacia"),
        (["pan", "panaderia", "verduleria", "carniceria", "almacen", "kiosco"], "Alimentos"),
        (["taxi", "uber", "cabify", "colectivo", "sube", "tren", "subte"], "Transporte"),
        (["sueldo", "salario", "aguinaldo", "honorarios"], "Sueldo")
    ];

    public ResultadoAnalisis Analizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new ResultadoAnalisis();
        }

        var normalizado = Normalizar(texto);

        var montoMatch = MontoRegex.Match(normalizado);
        var monto = montoMatch.Success ? ParsearMonto(montoMatch) : 0m;
        if (monto <= 0)
        {
            return new ResultadoAnalisis();
        }

        // Tipo: ingreso si hay verbo/término de ingreso; egreso si hay verbo de gasto.
        // Sin verbo (ej. "Nafta 18000"): se asume egreso solo si el mensaje es corto
        // (sustantivo + monto) para evitar falsos positivos en frases largas.
        TipoMovimiento tipo;
        if (IngresoRegex.IsMatch(normalizado))
        {
            tipo = TipoMovimiento.Ingreso;
        }
        else if (EgresoRegex.IsMatch(normalizado))
        {
            tipo = TipoMovimiento.Egreso;
        }
        else if (EsSustantivoMasMonto(normalizado))
        {
            tipo = TipoMovimiento.Egreso;
        }
        else
        {
            return new ResultadoAnalisis();
        }

        var categoria = DetectarCategoria(normalizado, tipo);
        var descripcion = ConstruirDescripcion(texto, montoMatch, categoria, tipo);

        return new ResultadoAnalisis
        {
            EsMovimiento = true,
            Tipo = tipo,
            Monto = monto,
            Categoria = categoria,
            Descripcion = descripcion
        };
    }

    private static decimal ParsearMonto(Match montoMatch)
    {
        var entero = montoMatch.Groups[1].Value.Replace(".", string.Empty);
        var decimales = montoMatch.Groups[2].Success ? montoMatch.Groups[2].Value : "0";
        return decimal.TryParse($"{entero}.{decimales}", NumberStyles.Number, CultureInfo.InvariantCulture, out var monto)
            ? monto
            : 0m;
    }

    /// <summary>Mensajes cortos tipo "Nafta 18000" o "Luz 15000": pocas palabras + un monto.</summary>
    private static bool EsSustantivoMasMonto(string normalizado)
    {
        var sinMonto = MontoRegex.Replace(normalizado, string.Empty).Trim(' ', '$', '.', ',');
        var palabras = sinMonto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palabras.Length is >= 1 and <= 3;
    }

    private static string DetectarCategoria(string normalizado, TipoMovimiento tipo)
    {
        foreach (var (claves, categoria) in CategoriasConocidas)
        {
            if (claves.Any(c => Regex.IsMatch(normalizado, $@"\b{Regex.Escape(c)}\b")))
            {
                return categoria;
            }
        }

        // "en X" / "de X": lo que sigue a la preposición.
        var enMatch = Regex.Match(normalizado, @"\b(?:en|de)\s+([a-zñ]+(?:\s+[a-zñ]+)?)\b");
        if (enMatch.Success)
        {
            return Capitalizar(enMatch.Groups[1].Value.Trim());
        }

        // Sustantivo + monto: usar la(s) palabra(s) restantes como categoría.
        var sinMonto = MontoRegex.Replace(normalizado, string.Empty);
        sinMonto = Regex.Replace(sinMonto, IngresoRegex.ToString() + "|" + EgresoRegex.ToString(), string.Empty);
        sinMonto = Regex.Replace(sinMonto, @"\s+", " ").Trim(' ', '$', '.', ',');
        if (sinMonto.Length >= 2)
        {
            return Capitalizar(sinMonto);
        }

        return tipo == TipoMovimiento.Ingreso ? "Ingresos" : "Otros";
    }

    private static string ConstruirDescripcion(string original, Match montoMatch, string categoria, TipoMovimiento tipo)
    {
        var descripcion = MontoRegex.Replace(original, string.Empty, 1);
        descripcion = Regex.Replace(descripcion,
            @"\b(gasté|gaste|compré|compre|pagué|pague|cobré|cobre|recibí|recibi|transferí|transferi|aboné|abone|me pagaron|me depositaron|por|en|con)\b",
            string.Empty, RegexOptions.IgnoreCase);
        descripcion = Regex.Replace(descripcion, @"\s+", " ").Trim(' ', '.', ',', '$');

        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            return Capitalizar(descripcion);
        }

        return tipo == TipoMovimiento.Ingreso ? $"Ingreso {categoria}" : $"Gasto {categoria}";
    }

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];

    /// <summary>Minúsculas sin acentos, para comparar contra los patrones.</summary>
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
