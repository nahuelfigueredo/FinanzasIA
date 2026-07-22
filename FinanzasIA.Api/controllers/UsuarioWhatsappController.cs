using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/usuario-whatsapp")]
public class UsuarioWhatsappController : ControllerBase
{
    private readonly IUsuarioWhatsappService _service;

    public UsuarioWhatsappController(IUsuarioWhatsappService service)
    {
        _service = service;
    }

    private string? UsuarioId =>
        Request.Headers.TryGetValue("X-User-Id", out var values) ? values.ToString() : null;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UsuarioWhatsappDto>>> GetNumeros(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UsuarioId)) return Unauthorized();
        return Ok(await _service.ObtenerNumerosAsync(UsuarioId, cancellationToken));
    }

    [HttpPost("vincular")]
    public async Task<ActionResult<VinculacionResultDto>> Vincular(
    [FromBody] VincularNumeroDto dto,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UsuarioId)) return Unauthorized();
        return Ok(await _service.VincularAsync(dto, UsuarioId, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(
        int id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UsuarioId)) return Unauthorized();
        var ok = await _service.DesvincularAsync(id, UsuarioId, cancellationToken);
        return ok ? NoContent() : NotFound();
    }
}