using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Analizador de finanzas del usuario. Su única responsabilidad es consultar
/// los datos (movimientos, categorías) y producir un
/// <see cref="AnalisisFinancieroCompletoDto"/> con todas las métricas calculadas.
/// No genera texto ni sugerencias: eso es responsabilidad de <see cref="ISugerenciasService"/>.
/// </summary>
public interface IFinanzasAnalyzer
{
    /// <param name="usuarioId">Id del usuario dueño de los datos.</param>
    /// <param name="presupuestoMensual">Presupuesto mensual configurado (opcional).</param>
    Task<AnalisisFinancieroCompletoDto> AnalizarAsync(string? usuarioId = null, decimal? presupuestoMensual = null, CancellationToken cancellationToken = default);
}
