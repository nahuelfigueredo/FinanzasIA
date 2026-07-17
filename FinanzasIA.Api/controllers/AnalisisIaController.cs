using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/ia")]
public class AnalisisIaController : ControllerBase
{
    private readonly IAnalisisFinancieroService _analisisFinancieroService;

    public AnalisisIaController(IAnalisisFinancieroService analisisFinancieroService)
    {
        _analisisFinancieroService = analisisFinancieroService;
    }

    [HttpGet("analisis")]
    public async Task<ActionResult<AnalisisFinancieroDto>> GetAnalisis(CancellationToken cancellationToken)
    {
        var userId = HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;
        var analisis = await _analisisFinancieroService.ObtenerAnalisisAsync(userId, cancellationToken);
        return Ok(analisis);
    }
}
