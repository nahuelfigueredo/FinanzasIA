using FinanzasIA.Api.Controllers;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinanzasIA.Api.Tests;

public class CriticalEndpointsTests
{
    [Fact]
    public async Task MovimientoCreate_RetornaBadRequest_CuandoCategoriaNoExiste()
    {
        var controller = new MovimientoController(new FakeMovimientoService { CreateResult = null });
        var result = await controller.Create(new CreateMovimientoDto(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CategoriaGetById_RetornaNotFound_CuandoNoExiste()
    {
        var controller = new CategoriaController(new FakeCategoriaService());
        var result = await controller.GetById(100, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task AnalisisIa_RetornaOk_ConPayload()
    {
        var controller = new AnalisisIaController(new FakeAnalisisService());
        var result = await controller.GetAnalisis(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<AnalisisFinancieroDto>(ok.Value);
    }

    private sealed class FakeMovimientoService : IMovimientoService
    {
        public MovimientoDto? CreateResult { get; set; }

        public Task<IReadOnlyCollection<MovimientoDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MovimientoDto>>([]);

        public Task<MovimientoDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<MovimientoDto?>(null);

        public Task<MovimientoDto?> CreateAsync(CreateMovimientoDto dto, string? usuarioId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateResult);

        public Task<MovimientoDto?> UpdateAsync(int id, UpdateMovimientoDto dto, CancellationToken cancellationToken = default)
            => Task.FromResult<MovimientoDto?>(null);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeCategoriaService : ICategoriaService
    {
        public Task<IReadOnlyCollection<CategoriaDto>> GetAllAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CategoriaDto>>([]);

        public Task<CategoriaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<CategoriaDto?>(null);

        public Task<CategoriaDto> CreateAsync(CreateCategoriaDto dto, string? usuarioId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new CategoriaDto());

        public Task<CategoriaDto?> UpdateAsync(int id, UpdateCategoriaDto dto, CancellationToken cancellationToken = default)
            => Task.FromResult<CategoriaDto?>(null);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeAnalisisService : IAnalisisFinancieroService
    {
        public Task<AnalisisFinancieroDto> ObtenerAnalisisAsync(string? usuarioId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new AnalisisFinancieroDto { TotalIngresos = 10, TotalEgresos = 5, BalanceNeto = 5 });
    }
}
