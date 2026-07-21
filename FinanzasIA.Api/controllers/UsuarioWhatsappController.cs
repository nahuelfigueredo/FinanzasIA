using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

/// <summary>
/// Endpoints de vinculación de números de WhatsApp con usuarios.
/// El controller solo recibe la solicitud y delega en <see cref="IUsuarioWhatsappService"/>.
/// </summary>
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
        HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var value) == true ? value.ToString() : null;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UsuarioWhatsappDto>>> GetNumeros(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UsuarioId)) return Unauthorized();
        return Ok(await _service.ObtenerNumerosAsync(UsuarioId, cancellationToken));
    }

    [HttpPost("vincular")]
    public async Task<ActionResult<VinculacionResultDto>> Vincular([FromBody] VincularNumeroDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UsuarioId)) return Unauthorized();
        return Ok(await _service.VincularAsync(dto, UsuarioId, cancellationToken));
    }

    [HttpPost("verificar")]
    public async Task<ActionResult<VinculacionResultDto>> Verificar([FromBody] VerificarNumeroDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UsuarioId)) return Unauthorized();
        return Ok(await _service.VerificarAsync(dto, UsuarioId, cancellationToken));
    }

    [HttpPost("{id:int}/reenviar-codigo")]
    public async Task<ActionResult<VinculacionResultDto>> ReenviarCodigo(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UsuarioId)) return Unauthorized();
        return Ok(await _service.ReenviarCodigoAsync(id, UsuarioId, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UsuarioId)) return Unauthorized();
        var ok = await _service.DesvincularAsync(id, UsuarioId, cancellationToken);
        return ok ? NoContent() : NotFound();
    }
}
