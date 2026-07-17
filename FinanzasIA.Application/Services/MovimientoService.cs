using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

public class MovimientoService : IMovimientoService
{
    private readonly IMovimientoRepository _movimientoRepository;
    private readonly ICategoriaRepository _categoriaRepository;

    public MovimientoService(
        IMovimientoRepository movimientoRepository,
        ICategoriaRepository categoriaRepository)
    {
        _movimientoRepository = movimientoRepository;
        _categoriaRepository = categoriaRepository;
    }

    public async Task<IReadOnlyCollection<MovimientoDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var movimientos = await _movimientoRepository.GetAllAsync(usuarioId, cancellationToken);
        return movimientos.Select(MapToDto).ToList();
    }

    public async Task<MovimientoDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var movimiento = await _movimientoRepository.GetByIdAsync(id, cancellationToken);
        return movimiento is null ? null : MapToDto(movimiento);
    }

    public async Task<MovimientoDto?> CreateAsync(CreateMovimientoDto dto, string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var categoriaExists = await _categoriaRepository.ExistsAsync(dto.CategoriaId, cancellationToken);
        if (!categoriaExists)
        {
            return null;
        }

        var movimiento = new Movimiento
        {
            Tipo = dto.Tipo,
            CategoriaId = dto.CategoriaId,
            Descripcion = dto.Descripcion,
            Monto = dto.Monto,
            Fecha = dto.Fecha,
            UsuarioId = usuarioId
        };

        var created = await _movimientoRepository.AddAsync(movimiento, cancellationToken);
        return MapToDto(created);
    }

    public async Task<MovimientoDto?> UpdateAsync(int id, UpdateMovimientoDto dto, CancellationToken cancellationToken = default)
    {
        var movimiento = await _movimientoRepository.GetByIdAsync(id, cancellationToken);
        if (movimiento is null)
        {
            return null;
        }

        var categoriaExists = await _categoriaRepository.ExistsAsync(dto.CategoriaId, cancellationToken);
        if (!categoriaExists)
        {
            return null;
        }

        movimiento.Tipo = dto.Tipo;
        movimiento.CategoriaId = dto.CategoriaId;
        movimiento.Descripcion = dto.Descripcion;
        movimiento.Monto = dto.Monto;
        movimiento.Fecha = dto.Fecha;

        await _movimientoRepository.UpdateAsync(movimiento, cancellationToken);

        return MapToDto(movimiento);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var movimiento = await _movimientoRepository.GetByIdAsync(id, cancellationToken);
        if (movimiento is null)
        {
            return false;
        }

        await _movimientoRepository.DeleteAsync(movimiento, cancellationToken);
        return true;
    }

    private static MovimientoDto MapToDto(Movimiento movimiento)
    {
        return new MovimientoDto
        {
            Id = movimiento.Id,
            Tipo = movimiento.Tipo,
            CategoriaId = movimiento.CategoriaId,
            CategoriaNombre = movimiento.Categoria?.Nombre ?? string.Empty,
            Descripcion = movimiento.Descripcion,
            Monto = movimiento.Monto,
            Fecha = movimiento.Fecha
        };
    }
}
