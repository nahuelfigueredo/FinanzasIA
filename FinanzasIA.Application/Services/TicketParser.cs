using System.Globalization;
using System.Text.RegularExpressions;
using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Extrae importe, fecha y comercio del texto OCR de un ticket usando
/// heurísticas y expresiones regulares. Solo devuelve un dato cuando la
/// confianza es razonable; en caso contrario lo deja en null para que el
/// flujo lo solicite al usuario.
/// </summary>
public static class TicketParser
{
    private static readonly Regex MontoConEtiqueta = new(
        @"(?:total|importe|monto|a\s*pagar|total\s*a\s*pagar)\s*:?\s*\$?\s*([\d.,]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MontoGenerico = new(
        @"\$\s*([\d.,]+)",
        RegexOptions.Compiled);

    private static readonly Regex FechaRegex = new(
        @"\b(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{2,4})\b",
        RegexOptions.Compiled);

    private static readonly string[] PalabrasIgnoradasEncabezado =
    [
        "ticket", "factura", "comprobante", "cuit", "iva", "consumidor", "final",
        "fecha", "hora", "caja", "punto", "venta", "original", "duplicado"
    ];

    /// <summary>Parsea el texto OCR y devuelve los datos detectados con confianza.</summary>
    public static TicketDatosDto Parsear(string textoOcr)
    {
        return new TicketDatosDto
        {
            TextoCompleto = textoOcr,
            Monto = DetectarMonto(textoOcr),
            Fecha = DetectarFecha(textoOcr),
            Comercio = DetectarComercio(textoOcr)
        };
    }

    /// <summary>
    /// Detecta el importe. Prioriza líneas con "TOTAL"/"IMPORTE"; como segunda
    /// opción usa el mayor valor con símbolo "$". Sin etiqueta ni símbolo no
    /// hay confianza suficiente y devuelve null.
    /// </summary>
    private static decimal? DetectarMonto(string texto)
    {
        var etiquetados = MontoConEtiqueta.Matches(texto)
            .Select(m => ParsearDecimal(m.Groups[1].Value))
            .Where(v => v is > 0)
            .Select(v => v!.Value)
            .ToList();

        if (etiquetados.Count > 0)
        {
            return etiquetados.Max();
        }

        var conSimbolo = MontoGenerico.Matches(texto)
            .Select(m => ParsearDecimal(m.Groups[1].Value))
            .Where(v => v is > 0)
            .Select(v => v!.Value)
            .ToList();

        return conSimbolo.Count > 0 ? conSimbolo.Max() : null;
    }

    /// <summary>Detecta la primera fecha válida en formato dd/mm/aaaa (o variantes).</summary>
    private static DateTime? DetectarFecha(string texto)
    {
        foreach (Match m in FechaRegex.Matches(texto))
        {
            if (!int.TryParse(m.Groups[1].Value, out var dia) ||
                !int.TryParse(m.Groups[2].Value, out var mes) ||
                !int.TryParse(m.Groups[3].Value, out var anio))
            {
                continue;
            }

            if (anio < 100)
            {
                anio += 2000;
            }

            if (dia is < 1 or > 31 || mes is < 1 or > 12 || anio < 2000 || anio > DateTime.Now.Year + 1)
            {
                continue;
            }

            try
            {
                var fecha = new DateTime(anio, mes, dia);
                // Un ticket no puede ser de un futuro lejano.
                if (fecha <= DateTime.Now.AddDays(1))
                {
                    return fecha;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Combinación inválida (ej. 31/02): probar la siguiente.
            }
        }

        return null;
    }

    /// <summary>
    /// Toma como comercio la primera línea "significativa" del ticket
    /// (los tickets suelen empezar con el nombre del local).
    /// </summary>
    private static string? DetectarComercio(string texto)
    {
        var lineas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var linea in lineas.Take(5))
        {
            var limpia = Regex.Replace(linea, @"[^\p{L}\p{N} .&'-]", string.Empty).Trim();
            if (limpia.Length < 3 || limpia.Length > 60)
            {
                continue;
            }

            // Descartar líneas que son solo números o encabezados típicos.
            if (!limpia.Any(char.IsLetter))
            {
                continue;
            }

            var lower = limpia.ToLowerInvariant();
            if (PalabrasIgnoradasEncabezado.Any(p => lower.StartsWith(p)))
            {
                continue;
            }

            return limpia;
        }

        return null;
    }

    /// <summary>Convierte textos como "15.000,50", "15,000.50" o "15000" a decimal.</summary>
    public static decimal? ParsearDecimal(string valor)
    {
        valor = valor.Trim().TrimEnd('.', ',');
        if (valor.Length == 0)
        {
            return null;
        }

        // Determinar el separador decimal: el último '.' o ',' con 1-2 dígitos a la derecha.
        var ultimoSeparador = Math.Max(valor.LastIndexOf('.'), valor.LastIndexOf(','));
        string normalizado;
        if (ultimoSeparador >= 0 && valor.Length - ultimoSeparador - 1 is 1 or 2)
        {
            var entero = new string(valor[..ultimoSeparador].Where(char.IsDigit).ToArray());
            var decimales = valor[(ultimoSeparador + 1)..];
            normalizado = $"{entero}.{decimales}";
        }
        else
        {
            normalizado = new string(valor.Where(char.IsDigit).ToArray());
        }

        return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var resultado) && resultado > 0
            ? resultado
            : null;
    }
}
