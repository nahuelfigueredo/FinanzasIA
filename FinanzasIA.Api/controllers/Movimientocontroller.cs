using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MovimientoController : ControllerBase
{
    private readonly IMovimientoService _movimientoService;

    public MovimientoController(IMovimientoService movimientoService)
    {
        _movimientoService = movimientoService;
    }

    private string? UserId => HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<MovimientoDto>>> GetAll(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized("Falta el usuario autenticado.");
        }

        var movimientos = await _movimientoService.GetAllAsync(UserId, cancellationToken);
        return Ok(movimientos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MovimientoDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var movimiento = await _movimientoService.GetByIdAsync(id, cancellationToken);
        return movimiento is null || movimiento.UsuarioId != UserId ? NotFound() : Ok(movimiento);
    }

    [HttpPost]
    public async Task<ActionResult<MovimientoDto>> Create([FromBody] CreateMovimientoDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized("Falta el usuario autenticado.");
        }

        var movimiento = await _movimientoService.CreateAsync(dto, UserId, cancellationToken);
        if (movimiento is null)
        {
            return BadRequest("La categoría indicada no existe.");
        }

        return CreatedAtAction(nameof(GetById), new { id = movimiento.Id }, movimiento);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MovimientoDto>> Update(int id, [FromBody] UpdateMovimientoDto dto, CancellationToken cancellationToken)
    {
        var existing = await _movimientoService.GetByIdAsync(id, cancellationToken);
        if (existing is null || existing.UsuarioId != UserId)
        {
            return NotFound();
        }

        var movimiento = await _movimientoService.UpdateAsync(id, dto, cancellationToken);
        if (movimiento is null)
        {
            return BadRequest("La categoría indicada no existe.");
        }

        return Ok(movimiento);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _movimientoService.GetByIdAsync(id, cancellationToken);
        if (existing is null || existing.UsuarioId != UserId)
        {
            return NotFound();
        }

        var deleted = await _movimientoService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}