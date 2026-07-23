using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriaController : ControllerBase
{
    private readonly ICategoriaService _categoriaService;

    public CategoriaController(ICategoriaService categoriaService)
    {
        _categoriaService = categoriaService;
    }

    private string? UserId => HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CategoriaDto>>> GetAll(CancellationToken cancellationToken)
    {
        var categorias = await _categoriaService.GetAllAsync(UserId, cancellationToken);
        return Ok(categorias);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoriaDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var categoria = await _categoriaService.GetByIdAsync(id, cancellationToken);
        return categoria is null ? NotFound() : Ok(categoria);
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaDto>> Create([FromBody] CreateCategoriaDto dto, CancellationToken cancellationToken)
    {
        var categoria = await _categoriaService.CreateAsync(dto, UserId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = categoria.Id }, categoria);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoriaDto>> Update(int id, [FromBody] UpdateCategoriaDto dto, CancellationToken cancellationToken)
    {
        var categoria = await _categoriaService.UpdateAsync(id, dto, cancellationToken);
        return categoria is null ? NotFound() : Ok(categoria);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _categoriaService.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromQuery] bool activa, CancellationToken cancellationToken)
    {
        var ok = await _categoriaService.CambiarEstadoAsync(id, activa, cancellationToken);
        return ok ? NoContent() : NotFound();
    }
}
