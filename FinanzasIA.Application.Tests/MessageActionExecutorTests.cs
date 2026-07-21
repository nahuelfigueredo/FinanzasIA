using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Services;
using FinanzasIA.Core.Enums;
using Xunit;

namespace FinanzasIA.Application.Tests;

public class MessageActionExecutorTests
{
    private readonly FakeMovimientoService _movimientos = new();
    private readonly FakeCategoriaService _categorias = new();
    private readonly FakeCuentaService _cuentas = new();
    private readonly FakeAnalisisFinancieroService _analisis = new();

    private MessageActionExecutor CrearExecutor() =>
        new(_movimientos, _categorias, _cuentas, _analisis);

    [Fact]
    public async Task EjecutarAsync_RegistraGastoYCreaCategoriaSiNoExiste()
    {
        var executor = CrearExecutor();

        var resultado = await executor.EjecutarAsync(new MensajeInterpretadoDto
        {
            Intent = MessageIntent.RegistrarGasto,
            TipoMovimiento = TipoMovimiento.Egreso,
            Monto = 25000m,
            Categoria = "supermercado",
            Descripcion = "Compra supermercado",
            Fecha = DateTime.Today
        }, "user-1");

        Assert.True(resultado.Exito);
        Assert.NotNull(resultado.MovimientoId);
        var creado = Assert.Single(_movimientos.Creados);
        Assert.Equal(25000m, creado.Monto);
        Assert.Equal(TipoMovimiento.Egreso, creado.Tipo);
        // La categoría "supermercado" no existía: se creó automáticamente.
        Assert.Contains(_categorias.Categorias, c => c.Nombre.Equals("Supermercado", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("✅", resultado.Respuesta);
        Assert.Contains("25.000", resultado.Respuesta);
    }

    [Fact]
    public async Task EjecutarAsync_ReutilizaCategoriaExistente()
    {
        _categorias.Categorias.Add(new CategoriaDto { Id = 7, Nombre = "Supermercado", TipoMovimiento = TipoMovimiento.Egreso });
        var executor = CrearExecutor();

        await executor.EjecutarAsync(new MensajeInterpretadoDto
        {
            Intent = MessageIntent.RegistrarGasto,
            TipoMovimiento = TipoMovimiento.Egreso,
            Monto = 10000m,
            Categoria = "supermercado",
            Fecha = DateTime.Today
        }, "user-1");

        Assert.Single(_categorias.Categorias);
        Assert.Equal(7, _movimientos.Creados.Single().CategoriaId);
    }

    [Fact]
    public async Task EjecutarAsync_SinCuentaUsaPredeterminada()
    {
        var executor = CrearExecutor();

        var resultado = await executor.EjecutarAsync(new MensajeInterpretadoDto
        {
            Intent = MessageIntent.RegistrarGasto,
            TipoMovimiento = TipoMovimiento.Egreso,
            Monto = 5000m,
            Fecha = DateTime.Today
        }, "user-1");

        Assert.True(resultado.Exito);
        Assert.Null(_movimientos.Creados.Single().CuentaId);
        Assert.Contains("Predeterminada", resultado.Respuesta);
    }

    [Fact]
    public async Task EjecutarAsync_SinMontoNoRegistraYPideElDato()
    {
        var executor = CrearExecutor();

        var resultado = await executor.EjecutarAsync(new MensajeInterpretadoDto
        {
            Intent = MessageIntent.RegistrarGasto,
            TipoMovimiento = TipoMovimiento.Egreso,
            Monto = null
        }, "user-1");

        Assert.False(resultado.Exito);
        Assert.Empty(_movimientos.Creados);
        Assert.Contains("monto", resultado.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EjecutarAsync_GastosHoySoloSumaEgresosDeHoy()
    {
        _movimientos.Movimientos.AddRange(
        [
            new MovimientoDto { Id = 1, Tipo = TipoMovimiento.Egreso, Monto = 1000m, Fecha = DateTime.Today.AddHours(10), CategoriaNombre = "Comida", Descripcion = "Almuerzo" },
            new MovimientoDto { Id = 2, Tipo = TipoMovimiento.Egreso, Monto = 500m, Fecha = DateTime.Today.AddDays(-1), CategoriaNombre = "Comida", Descripcion = "Ayer" },
            new MovimientoDto { Id = 3, Tipo = TipoMovimiento.Ingreso, Monto = 9999m, Fecha = DateTime.Today, CategoriaNombre = "Sueldo", Descripcion = "Ingreso" }
        ]);
        var executor = CrearExecutor();

        var resultado = await executor.EjecutarAsync(new MensajeInterpretadoDto { Intent = MessageIntent.GastosHoy }, "user-1");

        Assert.True(resultado.Exito);
        Assert.Contains("1.000", resultado.Respuesta);
        Assert.DoesNotContain("9.999", resultado.Respuesta);
    }

    [Fact]
    public async Task EjecutarAsync_IngresosMesRespondeTotal()
    {
        _movimientos.Movimientos.Add(new MovimientoDto
        {
            Id = 1,
            Tipo = TipoMovimiento.Ingreso,
            Monto = 950000m,
            Fecha = DateTime.Today,
            CategoriaNombre = "Sueldo",
            Descripcion = "Sueldo"
        });
        var executor = CrearExecutor();

        var resultado = await executor.EjecutarAsync(new MensajeInterpretadoDto { Intent = MessageIntent.IngresosMes }, "user-1");

        Assert.True(resultado.Exito);
        Assert.Contains("950.000", resultado.Respuesta);
    }

    [Fact]
    public async Task EjecutarAsync_UltimosMovimientosDevuelveMaximoCinco()
    {
        for (var i = 1; i <= 8; i++)
        {
            _movimientos.Movimientos.Add(new MovimientoDto
            {
                Id = i,
                Tipo = TipoMovimiento.Egreso,
                Monto = i * 100m,
                Fecha = DateTime.Today.AddDays(-i),
                CategoriaNombre = "Otros",
                Descripcion = $"Gasto {i}"
            });
        }
        var executor = CrearExecutor();

        var resultado = await executor.EjecutarAsync(new MensajeInterpretadoDto { Intent = MessageIntent.UltimosMovimientos }, "user-1");

        Assert.True(resultado.Exito);
        Assert.Equal(5, resultado.Respuesta.Split('\n').Count(l => l.StartsWith("- ")));
    }

    [Fact]
    public async Task EjecutarAsync_IntentDesconocidoNoEsManejado()
    {
        var executor = CrearExecutor();

        var resultado = await executor.EjecutarAsync(new MensajeInterpretadoDto { Intent = MessageIntent.Desconocido }, "user-1");

        Assert.False(resultado.Manejado);
        Assert.Empty(_movimientos.Creados);
    }
}
