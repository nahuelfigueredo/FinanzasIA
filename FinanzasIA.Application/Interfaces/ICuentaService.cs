using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

public interface ICuentaService
{
    Task<IReadOnlyCollection<CuentaDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<CuentaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CuentaDto> CreateAsync(CreateCuentaDto dto, string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<CuentaDto?> UpdateAsync(int id, UpdateCuentaDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
