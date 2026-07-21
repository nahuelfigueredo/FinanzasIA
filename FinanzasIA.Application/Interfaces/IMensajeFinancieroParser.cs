using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Analiza un mensaje de texto libre y determina si describe un movimiento
/// financiero (gasto o ingreso), extrayendo tipo, monto, categoría y descripción.
/// Se usa como segundo intento cuando el intérprete por reglas no detecta
/// una intención (ej.: "Nafta 18000", "Luz 15000").
/// </summary>
public interface IMensajeFinancieroParser
{
    ResultadoAnalisis Analizar(string texto);
}
