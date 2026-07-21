using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CuentaController : ControllerBase
{
    private readonly ICuentaService _cuentaService;

    public CuentaController(ICuentaService cuentaService)
    {
        _cuentaService = cuentaService;
    }

    private string? UserId => HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CuentaDto>>> GetAll(CancellationToken cancellationToken)
    {
        var cuentas = await _cuentaService.GetAllAsync(UserId, cancellationToken);
        return Ok(cuentas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CuentaDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var cuenta = await _cuentaService.GetByIdAsync(id, cancellationToken);
        return cuenta is null ? NotFound() : Ok(cuenta);
    }

    [HttpPost]
    public async Task<ActionResult<CuentaDto>> Create([FromBody] CreateCuentaDto dto, CancellationToken cancellationToken)
    {
        var cuenta = await _cuentaService.CreateAsync(dto, UserId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = cuenta.Id }, cuenta);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CuentaDto>> Update(int id, [FromBody] UpdateCuentaDto dto, CancellationToken cancellationToken)
    {
        var cuenta = await _cuentaService.UpdateAsync(id, dto, cancellationToken);
        return cuenta is null ? NotFound() : Ok(cuenta);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _cuentaService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
