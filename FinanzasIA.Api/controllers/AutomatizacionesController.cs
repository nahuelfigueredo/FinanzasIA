using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AutomatizacionesController : ControllerBase
{
    private readonly IAutomatizacionesService _automatizacionesService;

    public AutomatizacionesController(IAutomatizacionesService automatizacionesService)
    {
        _automatizacionesService = automatizacionesService;
    }

    private string? UserId => HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;

    [HttpGet]
    public async Task<ActionResult<AutomatizacionesDto>> Get(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized();
        }

        var configuracion = await _automatizacionesService.GetAsync(UserId, cancellationToken);
        return Ok(configuracion);
    }

    [HttpPut]
    public async Task<ActionResult<AutomatizacionesDto>> Guardar([FromBody] AutomatizacionesDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized();
        }

        var configuracion = await _automatizacionesService.GuardarAsync(UserId, dto, cancellationToken);
        return Ok(configuracion);
    }
}
