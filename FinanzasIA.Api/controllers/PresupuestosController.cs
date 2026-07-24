using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/presupuestos")]
public class PresupuestosController : ControllerBase
{
    private readonly IPresupuestoService _presupuestoService;

    public PresupuestosController(IPresupuestoService presupuestoService)
    {
        _presupuestoService = presupuestoService;
    }

    private string? UserId => HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PresupuestoDto>>> GetAll(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Ok(Array.Empty<PresupuestoDto>());
        }

        var presupuestos = await _presupuestoService.GetAllAsync(UserId, cancellationToken);
        return Ok(presupuestos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PresupuestoDto>> GetById(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return NotFound();
        }

        var presupuesto = await _presupuestoService.GetByIdAsync(id, UserId, cancellationToken);
        return presupuesto is null ? NotFound() : Ok(presupuesto);
    }

    [HttpPost]
    public async Task<ActionResult<PresupuestoDto>> Create([FromBody] CreatePresupuestoDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return BadRequest(new { message = "Falta el encabezado X-User-Id." });
        }

        try
        {
            var presupuesto = await _presupuestoService.CreateAsync(dto, UserId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = presupuesto.Id }, presupuesto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PresupuestoDto>> Update(int id, [FromBody] UpdatePresupuestoDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return NotFound();
        }

        try
        {
            var presupuesto = await _presupuestoService.UpdateAsync(id, dto, UserId, cancellationToken);
            return presupuesto is null ? NotFound() : Ok(presupuesto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return NotFound();
        }

        var deleted = await _presupuestoService.DeleteAsync(id, UserId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Estado de todos los presupuestos activos del mes indicado (o el actual).</summary>
    [HttpGet("estado")]
    public async Task<ActionResult<IReadOnlyCollection<PresupuestoEstadoDto>>> GetEstado(
        [FromQuery] int? mes,
        [FromQuery] int? año,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Ok(Array.Empty<PresupuestoEstadoDto>());
        }

        var hoy = DateTime.Today;
        var estados = await _presupuestoService.GetEstadosDelMesAsync(UserId, mes ?? hoy.Month, año ?? hoy.Year, cancellationToken);
        return Ok(estados);
    }
}
