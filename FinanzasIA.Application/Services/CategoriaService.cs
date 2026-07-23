using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
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

        // Las categorÃ­as del sistema no pueden eliminarse, solo desactivarse.
        if (categoria.EsSistema)
        {
            categoria.Activa = false;
            await _categoriaRepository.UpdateAsync(categoria, cancellationToken);
            return true;
        }

        var movimientos = await _movimientoRepository.GetAllAsync(null, cancellationToken);
        if (movimientos.Any(m => m.CategoriaId == id))
        {
            throw new InvalidOperationException("No se puede eliminar la categorÃ­a porque tiene movimientos asociados. EliminÃ¡ o reasignÃ¡ esos movimientos primero.");
        }

        await _categoriaRepository.DeleteAsync(categoria, cancellationToken);
        return true;
    }

    public async Task<bool> CambiarEstadoAsync(int id, bool activa, CancellationToken cancellationToken = default)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id, cancellationToken);
        if (categoria is null)
        {
            return false;
        }

        categoria.Activa = activa;
        await _categoriaRepository.UpdateAsync(categoria, cancellationToken);
        return true;
    }

    private static readonly (string Nombre, TipoMovimiento Tipo)[] CategoriasPredeterminadas =
    [
        ("Sueldo", TipoMovimiento.Ingreso),
        ("Horas extra", TipoMovimiento.Ingreso),
        ("Ventas", TipoMovimiento.Ingreso),
        ("Inversiones", TipoMovimiento.Ingreso),
        ("Regalos", TipoMovimiento.Ingreso),
        ("Otros ingresos", TipoMovimiento.Ingreso),
        ("Supermercado", TipoMovimiento.Egreso),
        ("Comida", TipoMovimiento.Egreso),
        ("Nafta", TipoMovimiento.Egreso),
        ("Transporte", TipoMovimiento.Egreso),
        ("Servicios", TipoMovimiento.Egreso),
        ("Luz", TipoMovimiento.Egreso),
        ("Agua", TipoMovimiento.Egreso),
        ("Gas", TipoMovimiento.Egreso),
        ("Internet", TipoMovimiento.Egreso),
        ("TelÃ©fono", TipoMovimiento.Egreso),
        ("Salud", TipoMovimiento.Egreso),
        ("Farmacia", TipoMovimiento.Egreso),
        ("EducaciÃ³n", TipoMovimiento.Egreso),
        ("Ropa", TipoMovimiento.Egreso),
        ("Entretenimiento", TipoMovimiento.Egreso),
        ("Mascotas", TipoMovimiento.Egreso),
        ("Impuestos", TipoMovimiento.Egreso),
        ("Alquiler", TipoMovimiento.Egreso),
        ("Tarjeta de crÃ©dito", TipoMovimiento.Egreso),
        ("Otros", TipoMovimiento.Egreso)
    ];

    public async Task CrearCategoriasPredeterminadasAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return;
        }

        // Idempotente: solo si el usuario todavÃ­a no tiene categorÃ­as de sistema.
        var existentes = await _categoriaRepository.GetAllAsync(usuarioId, cancellationToken);
        if (existentes.Any(c => c.EsSistema && c.UsuarioId == usuarioId))
        {
            return;
        }

        foreach (var (nombre, tipo) in CategoriasPredeterminadas)
        {
            await _categoriaRepository.AddAsync(new Categoria
            {
                Nombre = nombre,
                TipoMovimiento = tipo,
                UsuarioId = usuarioId,
                EsSistema = true,
                Activa = true
            }, cancellationToken);
        }
    }

    private static CategoriaDto MapToDto(Categoria categoria)
    {
        return new CategoriaDto
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            TipoMovimiento = categoria.TipoMovimiento,
            EsSistema = categoria.EsSistema,
            Activa = categoria.Activa
        };
    }
}
