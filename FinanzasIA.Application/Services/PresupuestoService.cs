using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Presupuestos mensuales por categoría: CRUD, presupuesto vigente y cálculo
/// de estado (gasto acumulado, porcentaje utilizado, saldo restante, excedido).
/// </summary>
public class PresupuestoService : IPresupuestoService
{
    private readonly IPresupuestoRepository _presupuestoRepository;
    private readonly IMovimientoRepository _movimientoRepository;

    public PresupuestoService(IPresupuestoRepository presupuestoRepository, IMovimientoRepository movimientoRepository)
    {
        _presupuestoRepository = presupuestoRepository;
        _movimientoRepository = movimientoRepository;
    }

    public async Task<IReadOnlyCollection<PresupuestoDto>> GetAllAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        var presupuestos = await _presupuestoRepository.GetAllAsync(usuarioId, cancellationToken);
        return presupuestos.Select(MapToDto).ToList();
    }

    public async Task<PresupuestoDto?> GetByIdAsync(int id, string usuarioId, CancellationToken cancellationToken = default)
    {
        var presupuesto = await _presupuestoRepository.GetByIdAsync(id, cancellationToken);
        return presupuesto is null || presupuesto.UsuarioId != usuarioId ? null : MapToDto(presupuesto);
    }

    public async Task<PresupuestoDto> CreateAsync(CreatePresupuestoDto dto, string usuarioId, CancellationToken cancellationToken = default)
    {
        Validar(dto.MontoMensual, dto.Mes, dto.Año);

        var presupuesto = new Presupuesto
        {
            UsuarioId = usuarioId,
            CategoriaId = dto.CategoriaId,
            MontoMensual = dto.MontoMensual,
            Mes = dto.Mes,
            Año = dto.Año,
            Activo = true
        };

        var creado = await _presupuestoRepository.AddAsync(presupuesto, cancellationToken);
        var conCategoria = await _presupuestoRepository.GetByIdAsync(creado.Id, cancellationToken);
        return MapToDto(conCategoria ?? creado);
    }

    public async Task<PresupuestoDto?> UpdateAsync(int id, UpdatePresupuestoDto dto, string usuarioId, CancellationToken cancellationToken = default)
    {
        Validar(dto.MontoMensual, dto.Mes, dto.Año);

        var presupuesto = await _presupuestoRepository.GetByIdAsync(id, cancellationToken);
        if (presupuesto is null || presupuesto.UsuarioId != usuarioId)
        {
            return null;
        }

        presupuesto.CategoriaId = dto.CategoriaId;
        presupuesto.MontoMensual = dto.MontoMensual;
        presupuesto.Mes = dto.Mes;
        presupuesto.Año = dto.Año;
        presupuesto.Activo = dto.Activo;
        await _presupuestoRepository.UpdateAsync(presupuesto, cancellationToken);

        var actualizado = await _presupuestoRepository.GetByIdAsync(id, cancellationToken);
        return MapToDto(actualizado ?? presupuesto);
    }

    public async Task<bool> DeleteAsync(int id, string usuarioId, CancellationToken cancellationToken = default)
    {
        var presupuesto = await _presupuestoRepository.GetByIdAsync(id, cancellationToken);
        if (presupuesto is null || presupuesto.UsuarioId != usuarioId)
        {
            return false;
        }

        await _presupuestoRepository.DeleteAsync(presupuesto, cancellationToken);
        return true;
    }

    public async Task<PresupuestoDto> CrearOActualizarAsync(int categoriaId, decimal montoMensual, int mes, int año, string usuarioId, CancellationToken cancellationToken = default)
    {
        Validar(montoMensual, mes, año);

        var existente = await _presupuestoRepository.GetVigenteAsync(usuarioId, categoriaId, mes, año, cancellationToken);
        if (existente is not null)
        {
            existente.MontoMensual = montoMensual;
            await _presupuestoRepository.UpdateAsync(existente, cancellationToken);
            return MapToDto(existente);
        }

        return await CreateAsync(new CreatePresupuestoDto
        {
            CategoriaId = categoriaId,
            MontoMensual = montoMensual,
            Mes = mes,
            Año = año
        }, usuarioId, cancellationToken);
    }

    public async Task<PresupuestoDto?> GetVigenteAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken = default)
    {
        var presupuesto = await _presupuestoRepository.GetVigenteAsync(usuarioId, categoriaId, mes, año, cancellationToken);
        return presupuesto is null ? null : MapToDto(presupuesto);
    }

    public async Task<PresupuestoEstadoDto?> GetEstadoAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken = default)
    {
        var presupuesto = await _presupuestoRepository.GetVigenteAsync(usuarioId, categoriaId, mes, año, cancellationToken);
        if (presupuesto is null)
        {
            return null;
        }

        var gastoAcumulado = await CalcularGastoAcumuladoAsync(usuarioId, categoriaId, mes, año, cancellationToken);
        return MapToEstado(presupuesto, gastoAcumulado);
    }

    public async Task<IReadOnlyCollection<PresupuestoEstadoDto>> GetEstadosDelMesAsync(string usuarioId, int mes, int año, CancellationToken cancellationToken = default)
    {
        var presupuestos = await _presupuestoRepository.GetAllAsync(usuarioId, cancellationToken);
        var delMes = presupuestos.Where(p => p.Activo && p.Mes == mes && p.Año == año).ToList();
        if (delMes.Count == 0)
        {
            return Array.Empty<PresupuestoEstadoDto>();
        }

        var movimientos = await _movimientoRepository.GetAllAsync(usuarioId, cancellationToken);
        var gastosPorCategoria = movimientos
            .Where(m => m.Tipo == TipoMovimiento.Egreso && m.Fecha.Month == mes && m.Fecha.Year == año)
            .GroupBy(m => m.CategoriaId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Monto));

        return delMes
            .Select(p => MapToEstado(p, gastosPorCategoria.GetValueOrDefault(p.CategoriaId)))
            .OrderByDescending(e => e.PorcentajeUtilizado)
            .ToList();
    }

    private async Task<decimal> CalcularGastoAcumuladoAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken)
    {
        var movimientos = await _movimientoRepository.GetAllAsync(usuarioId, cancellationToken);
        return movimientos
            .Where(m => m.Tipo == TipoMovimiento.Egreso &&
                        m.CategoriaId == categoriaId &&
                        m.Fecha.Month == mes && m.Fecha.Year == año)
            .Sum(m => m.Monto);
    }

    private static void Validar(decimal montoMensual, int mes, int año)
    {
        if (montoMensual <= 0)
        {
            throw new ArgumentException("El monto mensual del presupuesto debe ser mayor a cero.");
        }

        if (mes is < 1 or > 12)
        {
            throw new ArgumentException("El mes debe estar entre 1 y 12.");
        }

        if (año < 2000)
        {
            throw new ArgumentException("El año no es válido.");
        }
    }

    private static PresupuestoDto MapToDto(Presupuesto presupuesto) => new()
    {
        Id = presupuesto.Id,
        UsuarioId = presupuesto.UsuarioId,
        CategoriaId = presupuesto.CategoriaId,
        CategoriaNombre = presupuesto.Categoria?.Nombre ?? string.Empty,
        MontoMensual = presupuesto.MontoMensual,
        Mes = presupuesto.Mes,
        Año = presupuesto.Año,
        Activo = presupuesto.Activo,
        FechaCreacion = presupuesto.FechaCreacion
    };

    private static PresupuestoEstadoDto MapToEstado(Presupuesto presupuesto, decimal gastoAcumulado) => new()
    {
        PresupuestoId = presupuesto.Id,
        CategoriaId = presupuesto.CategoriaId,
        CategoriaNombre = presupuesto.Categoria?.Nombre ?? string.Empty,
        MontoMensual = presupuesto.MontoMensual,
        GastoAcumulado = gastoAcumulado,
        Mes = presupuesto.Mes,
        Año = presupuesto.Año
    };
}
