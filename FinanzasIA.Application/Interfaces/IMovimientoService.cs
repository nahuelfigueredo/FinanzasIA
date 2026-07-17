using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

public interface IMovimientoService
{
    Task<IReadOnlyCollection<MovimientoDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<MovimientoDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<MovimientoDto?> CreateAsync(CreateMovimientoDto dto, string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<MovimientoDto?> UpdateAsync(int id, UpdateMovimientoDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
