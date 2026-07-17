using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

public interface ICategoriaService
{
    Task<IReadOnlyCollection<CategoriaDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<CategoriaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CategoriaDto> CreateAsync(CreateCategoriaDto dto, string? usuarioId = null, CancellationToken cancellationToken = default);
    Task<CategoriaDto?> UpdateAsync(int id, UpdateCategoriaDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
