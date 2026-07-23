using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

public class AutomatizacionesService : IAutomatizacionesService
{
    private readonly IConfiguracionAutomatizacionRepository _repository;

    public AutomatizacionesService(IConfiguracionAutomatizacionRepository repository)
    {
        _repository = repository;
    }

    public async Task<AutomatizacionesDto> GetAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));
        }

        var configuracion = await _repository.GetByUsuarioAsync(usuarioId, cancellationToken);
        configuracion ??= await _repository.AddAsync(new ConfiguracionAutomatizacion { UsuarioId = usuarioId }, cancellationToken);
        return MapToDto(configuracion);
    }

    public async Task<AutomatizacionesDto> GuardarAsync(string usuarioId, AutomatizacionesDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));
        }

        var configuracion = await _repository.GetByUsuarioAsync(usuarioId, cancellationToken);

        if (configuracion is null)
        {
            configuracion = new ConfiguracionAutomatizacion { UsuarioId = usuarioId };
            Apply(configuracion, dto);
            configuracion = await _repository.AddAsync(configuracion, cancellationToken);
        }
        else
        {
            Apply(configuracion, dto);
            await _repository.UpdateAsync(configuracion, cancellationToken);
        }

        return MapToDto(configuracion);
    }

    private static void Apply(ConfiguracionAutomatizacion configuracion, AutomatizacionesDto dto)
    {
        configuracion.RegistroAutomaticoWhatsapp = dto.RegistroAutomaticoWhatsapp;
        configuracion.OcrAutomatico = dto.OcrAutomatico;
        configuracion.ClasificacionAutomatica = dto.ClasificacionAutomatica;
        configuracion.RespuestasAutomaticas = dto.RespuestasAutomaticas;
        configuracion.Recordatorios = dto.Recordatorios;
        configuracion.AlertasPresupuesto = dto.AlertasPresupuesto;
        configuracion.ResumenDiario = dto.ResumenDiario;
        configuracion.ResumenSemanal = dto.ResumenSemanal;
    }

    private static AutomatizacionesDto MapToDto(ConfiguracionAutomatizacion configuracion) => new()
    {
        RegistroAutomaticoWhatsapp = configuracion.RegistroAutomaticoWhatsapp,
        OcrAutomatico = configuracion.OcrAutomatico,
        ClasificacionAutomatica = configuracion.ClasificacionAutomatica,
        RespuestasAutomaticas = configuracion.RespuestasAutomaticas,
        Recordatorios = configuracion.Recordatorios,
        AlertasPresupuesto = configuracion.AlertasPresupuesto,
        ResumenDiario = configuracion.ResumenDiario,
        ResumenSemanal = configuracion.ResumenSemanal
    };
}
