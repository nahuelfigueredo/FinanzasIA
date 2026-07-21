using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Services;
using FinanzasIA.Core.Enums;
using Xunit;

namespace FinanzasIA.Application.Tests;

public class RuleBasedMessageInterpreterTests
{
    private readonly RuleBasedMessageInterpreter _interpreter = new();

    [Theory]
    [InlineData("Gasté 25000 en supermercado", MessageIntent.RegistrarGasto)]
    [InlineData("Compré pan 3500", MessageIntent.RegistrarGasto)]
    [InlineData("Pagué internet 32000", MessageIntent.RegistrarGasto)]
    [InlineData("Cobré sueldo 950000", MessageIntent.RegistrarIngreso)]
    [InlineData("Me pagaron 50000", MessageIntent.RegistrarIngreso)]
    [InlineData("Transferí 25000", MessageIntent.Transferencia)]
    public async Task InterpretarAsync_DetectaIntentsDeMovimiento(string texto, MessageIntent esperado)
    {
        var resultado = await _interpreter.InterpretarAsync(texto);

        Assert.Equal(esperado, resultado.Intent);
        Assert.NotNull(resultado.Monto);
    }

    [Theory]
    [InlineData("Saldo", MessageIntent.ConsultaSaldo)]
    [InlineData("¿Cuánto tengo?", MessageIntent.ConsultaSaldo)]
    [InlineData("Resumen", MessageIntent.ResumenMensual)]
    [InlineData("¿Cuánto gasté hoy?", MessageIntent.GastosHoy)]
    [InlineData("¿Cuánto gasté este mes?", MessageIntent.GastosMes)]
    [InlineData("Gastos del mes", MessageIntent.GastosMes)]
    [InlineData("¿Cuánto ingresé este mes?", MessageIntent.IngresosMes)]
    [InlineData("Ingresos del mes", MessageIntent.IngresosMes)]
    [InlineData("Últimos gastos", MessageIntent.UltimosMovimientos)]
    [InlineData("Últimos movimientos", MessageIntent.UltimosMovimientos)]
    [InlineData("ayuda", MessageIntent.Ayuda)]
    [InlineData("Hola", MessageIntent.Saludo)]
    public async Task InterpretarAsync_DetectaIntentsDeConsulta(string texto, MessageIntent esperado)
    {
        var resultado = await _interpreter.InterpretarAsync(texto);

        Assert.Equal(esperado, resultado.Intent);
    }

    [Fact]
    public async Task InterpretarAsync_LasConsultasNoRegistranMovimientos()
    {
        // "¿Cuánto gasté hoy?" contiene "gaste" pero es una consulta.
        var resultado = await _interpreter.InterpretarAsync("¿Cuánto gasté hoy?");

        Assert.Equal(MessageIntent.GastosHoy, resultado.Intent);
        Assert.Null(resultado.TipoMovimiento);
    }

    [Fact]
    public async Task InterpretarAsync_ExtraeMontoCategoriaYTipo()
    {
        var resultado = await _interpreter.InterpretarAsync("Gasté 25000 en supermercado");

        Assert.Equal(MessageIntent.RegistrarGasto, resultado.Intent);
        Assert.Equal(TipoMovimiento.Egreso, resultado.TipoMovimiento);
        Assert.Equal(25000m, resultado.Monto);
        Assert.Equal("supermercado", resultado.Categoria);
    }

    [Theory]
    [InlineData("Nafta 18000")]
    [InlineData("Luz 15000")]
    [InlineData("cualquier cosa sin sentido")]
    public async Task InterpretarAsync_DevuelveDesconocidoCuandoNoHayVerbo(string texto)
    {
        // Estos mensajes los resuelve el MensajeFinancieroParser (nivel 2).
        var resultado = await _interpreter.InterpretarAsync(texto);

        Assert.Equal(MessageIntent.Desconocido, resultado.Intent);
    }
}
