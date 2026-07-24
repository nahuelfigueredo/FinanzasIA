using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;

namespace FinanzasIA.Application.Tests;

/// <summary>
/// Fake en memoria de IPresupuestoService para las pruebas del pipeline.
/// El gasto acumulado se configura por categoría con <see cref="SetGastoAcumulado"/>.
/// </summary>
public class FakePresupuestoService : IPresupuestoService
{
    private readonly List<PresupuestoDto> _presupuestos = new();
    private readonly Dictionary<int, decimal> _gastosPorCategoria = new();
    private int _nextId;

    public IReadOnlyList<PresupuestoDto> Presupuestos => _presupuestos;

    public void SetGastoAcumulado(int categoriaId, decimal gasto) => _gastosPorCategoria[categoriaId] = gasto;

    public Task<IReadOnlyCollection<PresupuestoDto>> GetAllAsync(string usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<PresupuestoDto>>(_presupuestos.Where(p => p.UsuarioId == usuarioId).ToList());

    public Task<PresupuestoDto?> GetByIdAsync(int id, string usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_presupuestos.FirstOrDefault(p => p.Id == id && p.UsuarioId == usuarioId));

    public Task<PresupuestoDto> CreateAsync(CreatePresupuestoDto dto, string usuarioId, CancellationToken cancellationToken = default)
    {
        var presupuesto = new PresupuestoDto
        {
            Id = ++_nextId,
            UsuarioId = usuarioId,
            CategoriaId = dto.CategoriaId,
            MontoMensual = dto.MontoMensual,
            Mes = dto.Mes,
            Año = dto.Año,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        _presupuestos.Add(presupuesto);
        return Task.FromResult(presupuesto);
    }

    public Task<PresupuestoDto?> UpdateAsync(int id, UpdatePresupuestoDto dto, string usuarioId, CancellationToken cancellationToken = default)
    {
        var presupuesto = _presupuestos.FirstOrDefault(p => p.Id == id && p.UsuarioId == usuarioId);
        if (presupuesto is null)
        {
            return Task.FromResult<PresupuestoDto?>(null);
        }

        presupuesto.CategoriaId = dto.CategoriaId;
        presupuesto.MontoMensual = dto.MontoMensual;
        presupuesto.Mes = dto.Mes;
        presupuesto.Año = dto.Año;
        presupuesto.Activo = dto.Activo;
        return Task.FromResult<PresupuestoDto?>(presupuesto);
    }

    public Task<bool> DeleteAsync(int id, string usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_presupuestos.RemoveAll(p => p.Id == id && p.UsuarioId == usuarioId) > 0);

    public Task<PresupuestoDto> CrearOActualizarAsync(int categoriaId, decimal montoMensual, int mes, int año, string usuarioId, CancellationToken cancellationToken = default)
    {
        var existente = _presupuestos.FirstOrDefault(p =>
            p.UsuarioId == usuarioId && p.CategoriaId == categoriaId && p.Mes == mes && p.Año == año && p.Activo);

        if (existente is not null)
        {
            existente.MontoMensual = montoMensual;
            return Task.FromResult(existente);
        }

        return CreateAsync(new CreatePresupuestoDto
        {
            CategoriaId = categoriaId,
            MontoMensual = montoMensual,
            Mes = mes,
            Año = año
        }, usuarioId, cancellationToken);
    }

    public Task<PresupuestoDto?> GetVigenteAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken = default) =>
        Task.FromResult(_presupuestos.FirstOrDefault(p =>
            p.UsuarioId == usuarioId && p.CategoriaId == categoriaId && p.Mes == mes && p.Año == año && p.Activo));

    public async Task<PresupuestoEstadoDto?> GetEstadoAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken = default)
    {
        var presupuesto = await GetVigenteAsync(usuarioId, categoriaId, mes, año, cancellationToken);
        if (presupuesto is null)
        {
            return null;
        }

        return new PresupuestoEstadoDto
        {
            PresupuestoId = presupuesto.Id,
            CategoriaId = presupuesto.CategoriaId,
            CategoriaNombre = presupuesto.CategoriaNombre,
            MontoMensual = presupuesto.MontoMensual,
            GastoAcumulado = _gastosPorCategoria.GetValueOrDefault(categoriaId),
            Mes = mes,
            Año = año
        };
    }

    public async Task<IReadOnlyCollection<PresupuestoEstadoDto>> GetEstadosDelMesAsync(string usuarioId, int mes, int año, CancellationToken cancellationToken = default)
    {
        var estados = new List<PresupuestoEstadoDto>();
        foreach (var presupuesto in _presupuestos.Where(p => p.UsuarioId == usuarioId && p.Mes == mes && p.Año == año && p.Activo))
        {
            var estado = await GetEstadoAsync(usuarioId, presupuesto.CategoriaId, mes, año, cancellationToken);
            if (estado is not null)
            {
                estados.Add(estado);
            }
        }

        return estados;
    }
}
