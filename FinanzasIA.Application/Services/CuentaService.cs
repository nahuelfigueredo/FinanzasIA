using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

public class CuentaService : ICuentaService
{
    private readonly ICuentaRepository _cuentaRepository;

    public CuentaService(ICuentaRepository cuentaRepository)
    {
        _cuentaRepository = cuentaRepository;
    }

    public async Task<IReadOnlyCollection<CuentaDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var cuentas = await _cuentaRepository.GetAllAsync(usuarioId, cancellationToken);
        return cuentas.Select(MapToDto).ToList();
    }

    public async Task<CuentaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cuenta = await _cuentaRepository.GetByIdAsync(id, cancellationToken);
        return cuenta is null ? null : MapToDto(cuenta);
    }

    public async Task<CuentaDto> CreateAsync(CreateCuentaDto dto, string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var cuenta = new Cuenta
        {
            Nombre = dto.Nombre,
            Tipo = dto.Tipo,
            UsuarioId = usuarioId
        };

        var created = await _cuentaRepository.AddAsync(cuenta, cancellationToken);
        return MapToDto(created);
    }

    public async Task<CuentaDto?> UpdateAsync(int id, UpdateCuentaDto dto, CancellationToken cancellationToken = default)
    {
        var cuenta = await _cuentaRepository.GetByIdAsync(id, cancellationToken);
        if (cuenta is null)
        {
            return null;
        }

        cuenta.Nombre = dto.Nombre;
        cuenta.Tipo = dto.Tipo;
        await _cuentaRepository.UpdateAsync(cuenta, cancellationToken);

        return MapToDto(cuenta);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var cuenta = await _cuentaRepository.GetByIdAsync(id, cancellationToken);
        if (cuenta is null)
        {
            return false;
        }

        await _cuentaRepository.DeleteAsync(cuenta, cancellationToken);
        return true;
    }

    private static CuentaDto MapToDto(Cuenta cuenta)
    {
        return new CuentaDto
        {
            Id = cuenta.Id,
            Nombre = cuenta.Nombre,
            Tipo = cuenta.Tipo
        };
    }
}
