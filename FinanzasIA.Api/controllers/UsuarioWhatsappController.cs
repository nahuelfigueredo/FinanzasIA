using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

[ApiController]
[Route("api/usuario-whatsapp")]
public class UsuarioWhatsappController : ControllerBase
{
    private readonly IUsuarioWhatsappService _service;

    // SOLO PARA PRUEBAS
    private const string UsuarioId = "141b28d8-ddd3-4435-9894-2f430d76a8d9";

    public UsuarioWhatsappController(IUsuarioWhatsappService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UsuarioWhatsappDto>>> GetNumeros(
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ObtenerNumerosAsync(UsuarioId, cancellationToken));
    }

    [HttpPost("vincular")]
    public async Task<ActionResult<VinculacionResultDto>> Vincular(
        [FromBody] VincularNumeroDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.VincularAsync(UsuarioId: UsuarioId, dto: dto, cancellationToken: cancellationToken));
    }

    [HttpPost("verificar")]
    public async Task<ActionResult<VinculacionResultDto>> Verificar(
        [FromBody] VerificarNumeroDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.VerificarAsync(dto, UsuarioId, cancellationToken));
    }

    [HttpPost("{id:int}/reenviar-codigo")]
    public async Task<ActionResult<VinculacionResultDto>> ReenviarCodigo(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ReenviarCodigoAsync(id, UsuarioId, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(
        int id,
        CancellationToken cancellationToken)
    {
        var ok = await _service.DesvincularAsync(id, UsuarioId, cancellationToken);
        return ok ? NoContent() : NotFound();
    }
}