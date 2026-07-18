using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

public class CategoriaService : ICategoriaService
{
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IMovimientoRepository _movimientoRepository;

    public CategoriaService(ICategoriaRepository categoriaRepository, IMovimientoRepository movimientoRepository)
    {
        _categoriaRepository = categoriaRepository;
        _movimientoRepository = movimientoRepository;
    }

    public async Task<IReadOnlyCollection<CategoriaDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var categorias = await _categoriaRepository.GetAllAsync(usuarioId, cancellationToken);
        return categorias.Select(MapToDto).ToList();
    }

    public async Task<CategoriaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id, cancellationToken);
        return categoria is null ? null : MapToDto(categoria);
    }

    public async Task<CategoriaDto> CreateAsync(CreateCategoriaDto dto, string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var categoria = new Categoria
        {
            Nombre = dto.Nombre,
            TipoMovimiento = dto.TipoMovimiento,
            UsuarioId = usuarioId
        };

        var created = await _categoriaRepository.AddAsync(categoria, cancellationToken);
        return MapToDto(created);
    }

    public async Task<CategoriaDto?> UpdateAsync(int id, UpdateCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id, cancellationToken);
        if (categoria is null)
        {
            return null;
        }

        categoria.Nombre = dto.Nombre;
        categoria.TipoMovimiento = dto.TipoMovimiento;
        await _categoriaRepository.UpdateAsync(categoria, cancellationToken);

        return MapToDto(categoria);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id, cancellationToken);
        if (categoria is null)
        {
            return false;
        }

        var movimientos = await _movimientoRepository.GetAllAsync(null, cancellationToken);
        if (movimientos.Any(m => m.CategoriaId == id))
        {
            throw new InvalidOperationException("No se puede eliminar la categoría porque tiene movimientos asociados. Eliminá o reasigná esos movimientos primero.");
        }

        await _categoriaRepository.DeleteAsync(categoria, cancellationToken);
        return true;
    }

    private static CategoriaDto MapToDto(Categoria categoria)
    {
        return new CategoriaDto
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            TipoMovimiento = categoria.TipoMovimiento
        };
    }
}
