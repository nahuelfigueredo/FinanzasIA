using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Services;
using FinanzasIA.Core.Enums;
using Xunit;

namespace FinanzasIA.Application.Tests;

/// <summary>
/// Escenarios de negocio del módulo de Presupuestos Inteligentes:
/// presupuesto no alcanzado, 80% alcanzado, excedido, sin presupuesto
/// y creación/actualización vía WhatsApp.
/// </summary>
public class PresupuestoScenarioTests
{
    private readonly FakeMovimientoService _movimientos = new();
    private readonly FakeCategoriaService _categorias = new();
    private readonly FakeCuentaService _cuentas = new();
    private readonly FakeAnalisisFinancieroService _analisis = new();
    private readonly FakePresupuestoService _presupuestos = new();

    private const string Usuario = "user-1";

    public PresupuestoScenarioTests()
    {
        _categorias.Categorias.Add(new CategoriaDto { Id = 1, Nombre = "Supermercado", TipoMovimiento = TipoMovimiento.Egreso });
    }

    private MessageActionExecutor CrearExecutor() =>
        new(_movimientos, _categorias, _cuentas, _analisis, _presupuestos);

    private Task<MensajeProcesadoResultDto> RegistrarGastoAsync(decimal monto) =>
        CrearExecutor().EjecutarAsync(new MensajeInterpretadoDto
        {
            Intent = MessageIntent.RegistrarGasto,
            TipoMovimiento = TipoMovimiento.Egreso,
            Monto = monto,
            Categoria = "supermercado",
            Fecha = DateTime.Today
        }, Usuario);

    private async Task CrearPresupuestoAsync(decimal montoMensual)
    {
        var hoy = DateTime.Today;
        await _presupuestos.CrearOActualizarAsync(1, montoMensual, hoy.Month, hoy.Year, Usuario);
    }

    [Fact]
    public async Task GastoConPresupuestoNoAlcanzado_NoMuestraAdvertencias()
    {
        await CrearPresupuestoAsync(100000m);
        _presupuestos.SetGastoAcumulado(1, 30000m);

        var resultado = await RegistrarGastoAsync(10000m);

        Assert.True(resultado.Exito);
        Assert.Contains("Presupuesto de", resultado.Respuesta);
        Assert.DoesNotContain("80%", resultado.Respuesta);
        Assert.DoesNotContain("Superaste", resultado.Respuesta);
    }

    [Fact]
    public async Task GastoConOchentaPorCientoAlcanzado_MuestraAdvertencia()
    {
        await CrearPresupuestoAsync(100000m);
        _presupuestos.SetGastoAcumulado(1, 85000m);

        var resultado = await RegistrarGastoAsync(5000m);

        Assert.True(resultado.Exito);
        Assert.Contains("80%", resultado.Respuesta);
        Assert.DoesNotContain("Superaste", resultado.Respuesta);
    }

    [Fact]
    public async Task GastoConPresupuestoExcedido_MuestraAlertaDeExceso()
    {
        await CrearPresupuestoAsync(100000m);
        _presupuestos.SetGastoAcumulado(1, 120000m);

        var resultado = await RegistrarGastoAsync(5000m);

        Assert.True(resultado.Exito);
        Assert.Contains("Superaste", resultado.Respuesta);
    }

    [Fact]
    public async Task GastoSinPresupuesto_NoAgregaInformacionDePresupuesto()
    {
        var resultado = await RegistrarGastoAsync(10000m);

        Assert.True(resultado.Exito);
        Assert.DoesNotContain("Presupuesto de", resultado.Respuesta);
        Assert.DoesNotContain("80%", resultado.Respuesta);
    }

    [Fact]
    public async Task DefinirPresupuestoPorWhatsApp_CreaPresupuestoNuevo()
    {
        var resultado = await CrearExecutor().EjecutarAsync(new MensajeInterpretadoDto
        {
            Intent = MessageIntent.DefinirPresupuesto,
            Monto = 350000m,
            Categoria = "supermercado",
            Fecha = DateTime.Today
        }, Usuario);

        Assert.True(resultado.Exito);
        Assert.Contains("Presupuesto creado", resultado.Respuesta);
        var presupuesto = Assert.Single(_presupuestos.Presupuestos);
        Assert.Equal(350000m, presupuesto.MontoMensual);
        Assert.Equal(1, presupuesto.CategoriaId);
    }

    [Fact]
    public async Task DefinirPresupuestoPorWhatsApp_ActualizaPresupuestoExistente()
    {
        await CrearPresupuestoAsync(200000m);

        var resultado = await CrearExecutor().EjecutarAsync(new MensajeInterpretadoDto
        {
            Intent = MessageIntent.DefinirPresupuesto,
            Monto = 500000m,
            Categoria = "supermercado",
            Fecha = DateTime.Today
        }, Usuario);

        Assert.True(resultado.Exito);
        Assert.Contains("Presupuesto actualizado", resultado.Respuesta);
        var presupuesto = Assert.Single(_presupuestos.Presupuestos);
        Assert.Equal(500000m, presupuesto.MontoMensual);
    }

    [Fact]
    public async Task DefinirPresupuestoSinMonto_PideElMonto()
    {
        var resultado = await CrearExecutor().EjecutarAsync(new MensajeInterpretadoDto
        {
            Intent = MessageIntent.DefinirPresupuesto,
            Categoria = "supermercado",
            Fecha = DateTime.Today
        }, Usuario);

        Assert.False(resultado.Exito);
        Assert.Contains("monto", resultado.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_presupuestos.Presupuestos);
    }
}
