using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/asistente")]
public class AsistenteController : ControllerBase
{
    private readonly IAsistenteService _asistenteService;
    private readonly IFinanzasAnalyzer _finanzasAnalyzer;
    private readonly ISugerenciasService _sugerenciasService;
    private readonly ILogger<AsistenteController> _logger;

    public AsistenteController(
        IAsistenteService asistenteService,
        IFinanzasAnalyzer finanzasAnalyzer,
        ISugerenciasService sugerenciasService,
        ILogger<AsistenteController> logger)
    {
        _asistenteService = asistenteService;
        _finanzasAnalyzer = finanzasAnalyzer;
        _sugerenciasService = sugerenciasService;
        _logger = logger;
    }

    [HttpPost("preguntar")]
    public async Task<ActionResult<AsistenteRespuestaDto>> Preguntar([FromBody] AsistentePreguntaDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;
            var respuesta = await _asistenteService.PreguntarAsync(dto.Pregunta, userId, cancellationToken);
            return Ok(respuesta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar la pregunta del asistente financiero.");
            return StatusCode(StatusCodes.Status500InternalServerError, new AsistenteRespuestaDto
            {
                Respuesta = "Ups, no pude procesar tu pregunta en este momento. Intentá de nuevo en unos segundos. 🙏"
            });
        }
    }

    /// <summary>
    /// Analiza automáticamente las finanzas del usuario y devuelve sugerencias
    /// inteligentes generadas por reglas de negocio.
    /// </summary>
    /// <param name="presupuesto">Presupuesto mensual configurado por el usuario (opcional).</param>
    [HttpGet("sugerencias")]
    public async Task<ActionResult<IReadOnlyCollection<SugerenciaDto>>> GetSugerencias([FromQuery] decimal? presupuesto, CancellationToken cancellationToken)
    {
        try
        {
            var userId = HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;
            var analisis = await _finanzasAnalyzer.AnalizarAsync(userId, presupuesto, cancellationToken);
            var sugerencias = await _sugerenciasService.GenerarSugerenciasAsync(analisis, cancellationToken);
            return Ok(sugerencias);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar sugerencias inteligentes.");
            return StatusCode(StatusCodes.Status500InternalServerError, Array.Empty<SugerenciaDto>());
        }
    }
}
