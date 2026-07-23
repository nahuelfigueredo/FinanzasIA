using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.Tests;

/// <summary>
/// Fakes en memoria de los servicios de dominio, compartidos por los tests
/// del executor y del pipeline completo de mensajes.
/// </summary>
internal sealed class FakeMovimientoService : IMovimientoService
{
    public List<MovimientoDto> Movimientos { get; } = [];
    public List<CreateMovimientoDto> Creados { get; } = [];
    public bool FallarAlCrear { get; set; }

    public Task<IReadOnlyCollection<MovimientoDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<MovimientoDto>>(Movimientos);

    public Task<MovimientoDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Movimientos.FirstOrDefault(m => m.Id == id));

    public Task<MovimientoDto?> CreateAsync(CreateMovimientoDto dto, string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        if (FallarAlCrear)
        {
            throw new InvalidOperationException("Fallo simulado al crear el movimiento.");
        }

        Creados.Add(dto);
        var movimiento = new MovimientoDto
        {
            Id = Creados.Count,
            Tipo = dto.Tipo,
            CategoriaId = dto.CategoriaId,
            CuentaId = dto.CuentaId,
            Descripcion = dto.Descripcion,
            Monto = dto.Monto,
            Fecha = dto.Fecha
        };
        Movimientos.Add(movimiento);
        return Task.FromResult<MovimientoDto?>(movimiento);
    }

    public Task<MovimientoDto?> UpdateAsync(int id, UpdateMovimientoDto dto, CancellationToken cancellationToken = default)
        => Task.FromResult<MovimientoDto?>(null);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

internal sealed class FakeCategoriaService : ICategoriaService
{
    public List<CategoriaDto> Categorias { get; } = [];

    public Task<IReadOnlyCollection<CategoriaDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<CategoriaDto>>(Categorias);

    public Task<CategoriaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Categorias.FirstOrDefault(c => c.Id == id));

    public Task<CategoriaDto> CreateAsync(CreateCategoriaDto dto, string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var categoria = new CategoriaDto
        {
            Id = Categorias.Count + 1,
            Nombre = dto.Nombre,
            TipoMovimiento = dto.TipoMovimiento
        };
        Categorias.Add(categoria);
        return Task.FromResult(categoria);
    }

    public Task<CategoriaDto?> UpdateAsync(int id, UpdateCategoriaDto dto, CancellationToken cancellationToken = default)
        => Task.FromResult<CategoriaDto?>(null);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

internal sealed class FakeCuentaService : ICuentaService
{
    public List<CuentaDto> Cuentas { get; } = [];

    public Task<IReadOnlyCollection<CuentaDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<CuentaDto>>(Cuentas);

    public Task<CuentaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Cuentas.FirstOrDefault(c => c.Id == id));

    public Task<CuentaDto> CreateAsync(CreateCuentaDto dto, string? usuarioId = null, CancellationToken cancellationToken = default)
    {
        var cuenta = new CuentaDto { Id = Cuentas.Count + 1, Nombre = dto.Nombre, Tipo = dto.Tipo };
        Cuentas.Add(cuenta);
        return Task.FromResult(cuenta);
    }

    public Task<CuentaDto?> UpdateAsync(int id, UpdateCuentaDto dto, CancellationToken cancellationToken = default)
        => Task.FromResult<CuentaDto?>(null);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

internal sealed class FakeAnalisisFinancieroService : IAnalisisFinancieroService
{
    public AnalisisFinancieroDto Analisis { get; set; } = new();

    public Task<AnalisisFinancieroDto> ObtenerAnalisisAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Analisis);
}

internal sealed class FakeAsistenteIAProvider : IAsistenteIAProvider
{
    public int Invocaciones { get; private set; }
    public string RespuestaJson { get; set; } = "{}";

    public Task<string> GenerarRespuestaAsync(string pregunta, ContextoFinancieroDto contexto, IReadOnlyCollection<AsistenteMensajeDto>? historial = null, CancellationToken cancellationToken = default)
    {
        Invocaciones++;
        return Task.FromResult(RespuestaJson);
    }
}
