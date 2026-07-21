namespace FinanzasIA.Application.DTOs;

/// <summary>
/// Tipo visual de una sugerencia. Determina el color de la tarjeta en la UI.
/// </summary>
public enum TipoSugerencia
{
    Info = 0,
    Advertencia = 1,
    Exito = 2
}

/// <summary>
/// Sugerencia inteligente generada automáticamente a partir del análisis
/// financiero del usuario. Es agnóstica al motor que la produjo: hoy se
/// genera por reglas de negocio (<c>SugerenciasService</c>) y en el futuro
/// podrá ser generada o enriquecida por OpenAI sin cambiar este contrato.
/// </summary>
public class SugerenciaDto
{
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public TipoSugerencia Tipo { get; set; } = TipoSugerencia.Info;

    /// <summary>Emoji o clase de ícono para mostrar en la tarjeta.</summary>
    public string Icono { get; set; } = "💡";

    /// <summary>Prioridad de la sugerencia: 1 = alta, mayor número = menor prioridad.</summary>
    public int Prioridad { get; set; } = 3;
}
