using System.Globalization;
using System.Text;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Proveedor de IA por defecto basado en reglas locales.
/// Detecta la intención de la pregunta por palabras clave y responde en
/// lenguaje natural usando el contexto financiero del usuario.
/// Cuando se integre OpenAI, este proveedor puede quedar como fallback.
/// </summary>
public class ReglasAsistenteProvider : IAsistenteIAProvider
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    public Task<string> GenerarRespuestaAsync(string pregunta, ContextoFinancieroDto contexto, IReadOnlyCollection<AsistenteMensajeDto>? historial = null, CancellationToken cancellationToken = default)
    {
        var texto = Normalizar(pregunta);

        string respuesta;
        if (Contiene(texto, "compar", "anterior", "pasado"))
        {
            respuesta = ResponderComparativa(contexto);
        }
        else if (Contiene(texto, "categoria", "rubro", "donde gasto", "en que gasto"))
        {
            respuesta = ResponderCategoriaTop(contexto);
        }
        else if (Contiene(texto, "ahorr", "consejo", "recomend"))
        {
            respuesta = ResponderAhorro(contexto);
        }
        else if (Contiene(texto, "resumen", "como estoy", "estado", "balance"))
        {
            respuesta = ResponderResumen(contexto);
        }
        else if (Contiene(texto, "gaste", "gasto", "egreso", "gastos"))
        {
            respuesta = ResponderGastoMensual(contexto);
        }
        else if (Contiene(texto, "ingreso", "gane", "cobre"))
        {
            respuesta = ResponderIngresos(contexto);
        }
        else
        {
            respuesta = "Puedo ayudarte con tus finanzas: preguntame cuánto gastaste este mes, cuál es tu categoría con más gastos, cómo viene tu balance, una comparativa con el mes anterior o consejos para ahorrar. 💡";
        }

        return Task.FromResult(respuesta);
    }

    private static string ResponderGastoMensual(ContextoFinancieroDto c)
    {
        if (c.CantidadMovimientosMesActual == 0)
        {
            return "Todavía no registraste movimientos este mes, así que no tengo gastos para mostrarte. 📭";
        }

        var sb = new StringBuilder();
        sb.Append($"Este mes gastaste {Moneda(c.EgresosMesActual)} en total. ");
        if (c.MayorGastoMesActual is not null)
        {
            sb.Append($"Tu gasto más grande fue \"{c.MayorGastoMesActual.Descripcion}\" por {Moneda(c.MayorGastoMesActual.Monto)} el {c.MayorGastoMesActual.Fecha:dd/MM}. ");
        }
        sb.Append($"Frente a ingresos de {Moneda(c.IngresosMesActual)}, tu balance del mes es {Moneda(c.IngresosMesActual - c.EgresosMesActual)}.");
        return sb.ToString();
    }

    private static string ResponderIngresos(ContextoFinancieroDto c)
    {
        return c.IngresosMesActual <= 0
            ? "Este mes todavía no registraste ingresos. 📭"
            : $"Este mes registraste ingresos por {Moneda(c.IngresosMesActual)}. Con egresos de {Moneda(c.EgresosMesActual)}, tu balance mensual es {Moneda(c.IngresosMesActual - c.EgresosMesActual)}.";
    }

    private static string ResponderCategoriaTop(ContextoFinancieroDto c)
    {
        var top = c.GastosPorCategoriaMesActual.FirstOrDefault();
        if (top is null)
        {
            return "Este mes no tenés gastos registrados, así que no hay categorías para analizar. 📭";
        }

        var porcentaje = c.EgresosMesActual > 0 ? top.Total / c.EgresosMesActual * 100 : 0;
        var sb = new StringBuilder();
        sb.Append($"Tu categoría con más gastos este mes es \"{top.Categoria}\" con {Moneda(top.Total)} ({porcentaje:0}% de tus egresos). ");

        var segunda = c.GastosPorCategoriaMesActual.Skip(1).FirstOrDefault();
        if (segunda is not null)
        {
            sb.Append($"Le sigue \"{segunda.Categoria}\" con {Moneda(segunda.Total)}.");
        }

        return sb.ToString();
    }

    private static string ResponderComparativa(ContextoFinancieroDto c)
    {
        if (c.EgresosMesAnterior <= 0 && c.EgresosMesActual <= 0)
        {
            return "No tengo gastos registrados ni este mes ni el anterior para comparar. 📭";
        }

        var diferencia = c.EgresosMesActual - c.EgresosMesAnterior;
        var sb = new StringBuilder();
        sb.Append($"Este mes gastaste {Moneda(c.EgresosMesActual)} y el mes anterior {Moneda(c.EgresosMesAnterior)}. ");

        if (c.EgresosMesAnterior > 0)
        {
            var variacion = diferencia / c.EgresosMesAnterior * 100;
            sb.Append(diferencia > 0
                ? $"Estás gastando {Moneda(diferencia)} más ({Math.Abs(variacion):0}% de aumento). 📈 "
                : diferencia < 0
                    ? $"Estás gastando {Moneda(Math.Abs(diferencia))} menos ({Math.Abs(variacion):0}% de baja). ¡Buen trabajo! 📉 "
                    : "Estás gastando exactamente lo mismo. ");
        }

        sb.Append($"En ingresos: {Moneda(c.IngresosMesActual)} este mes vs {Moneda(c.IngresosMesAnterior)} el anterior.");
        return sb.ToString();
    }

    private static string ResponderAhorro(ContextoFinancieroDto c)
    {
        var sb = new StringBuilder();
        var top = c.GastosPorCategoriaMesActual.FirstOrDefault();

        if (c.IngresosMesActual > 0)
        {
            var tasa = (c.IngresosMesActual - c.EgresosMesActual) / c.IngresosMesActual * 100;
            sb.Append(tasa >= 20
                ? $"Tu tasa de ahorro este mes es del {tasa:0}%, ¡muy buena! 💪 "
                : tasa > 0
                    ? $"Tu tasa de ahorro este mes es del {tasa:0}%. Un objetivo saludable es llegar al 20%. "
                    : "Este mes estás gastando más de lo que ingresás, prioridad número uno: frenar los gastos variables. ⚠️ ");
        }

        if (top is not null)
        {
            sb.Append($"Tu mayor rubro de gasto es \"{top.Categoria}\" ({Moneda(top.Total)}); revisalo para encontrar recortes. ");
        }

        sb.Append("Consejos: fijate un presupuesto mensual, automatizá un ahorro apenas cobrás y registrá todos tus movimientos para tener visibilidad. 💡");
        return sb.ToString();
    }

    private static string ResponderResumen(ContextoFinancieroDto c)
    {
        if (c.CantidadMovimientosMesActual == 0)
        {
            return "Este mes todavía no tenés movimientos registrados. Cargá tus ingresos y gastos para que pueda darte un resumen. 📭";
        }

        var balanceMes = c.IngresosMesActual - c.EgresosMesActual;
        var sb = new StringBuilder();
        sb.Append($"Resumen del mes 🧾: ingresos {Moneda(c.IngresosMesActual)}, egresos {Moneda(c.EgresosMesActual)}, balance {Moneda(balanceMes)} ({c.CantidadMovimientosMesActual} movimientos). ");

        var top = c.GastosPorCategoriaMesActual.FirstOrDefault();
        if (top is not null)
        {
            sb.Append($"La categoría con más gasto fue \"{top.Categoria}\" con {Moneda(top.Total)}. ");
        }

        sb.Append($"Tu balance histórico total es {Moneda(c.BalanceTotal)}.");
        return sb.ToString();
    }

    private static string Moneda(decimal valor) => valor.ToString("C", Cultura);

    private static bool Contiene(string texto, params string[] claves) => claves.Any(texto.Contains);

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
