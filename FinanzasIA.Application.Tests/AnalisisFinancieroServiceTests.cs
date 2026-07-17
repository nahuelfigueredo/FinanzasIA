using FinanzasIA.Application.Services;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;
using Xunit;

namespace FinanzasIA.Application.Tests;

public class AnalisisFinancieroServiceTests
{
    [Fact]
    public async Task ObtenerAnalisisAsync_CalculaTotalesYBalanceCorrectamente()
    {
        var repo = new FakeMovimientoRepository(
        [
            new Movimiento
            {
                Id = 1,
                Tipo = TipoMovimiento.Ingreso,
                Monto = 2000m,
                Fecha = new DateTime(2026, 5, 1),
                CategoriaId = 1,
                Categoria = new Categoria { Id = 1, Nombre = "Salario", TipoMovimiento = TipoMovimiento.Ingreso }
            },
            new Movimiento
            {
                Id = 2,
                Tipo = TipoMovimiento.Egreso,
                Monto = 600m,
                Fecha = new DateTime(2026, 5, 2),
                CategoriaId = 2,
                Categoria = new Categoria { Id = 2, Nombre = "Alimentación", TipoMovimiento = TipoMovimiento.Egreso }
            }
        ]);

        var service = new AnalisisFinancieroService(repo);
        var result = await service.ObtenerAnalisisAsync();

        Assert.Equal(2000m, result.TotalIngresos);
        Assert.Equal(600m, result.TotalEgresos);
        Assert.Equal(1400m, result.BalanceNeto);
        Assert.Equal(70m, result.TasaAhorroPorcentaje);
        Assert.Equal("Alimentación", result.CategoriaMayorGasto);
    }

    private sealed class FakeMovimientoRepository : IMovimientoRepository
    {
        private readonly IReadOnlyCollection<Movimiento> _movimientos;

        public FakeMovimientoRepository(IReadOnlyCollection<Movimiento> movimientos)
        {
            _movimientos = movimientos;
        }

        public Task<IReadOnlyCollection<Movimiento>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_movimientos);

        public Task<Movimiento?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_movimientos.FirstOrDefault(x => x.Id == id));

        public Task<Movimiento> AddAsync(Movimiento movimiento, CancellationToken cancellationToken = default)
            => Task.FromResult(movimiento);

        public Task UpdateAsync(Movimiento movimiento, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(Movimiento movimiento, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
