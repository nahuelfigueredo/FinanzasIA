using FinanzasIA.Application.Services;
using FinanzasIA.Core.Enums;
using Xunit;

namespace FinanzasIA.Application.Tests;

public class MensajeFinancieroParserTests
{
    private readonly MensajeFinancieroParser _parser = new();

    [Theory]
    [InlineData("Gasté 25000 en supermercado", 25000, "Supermercado")]
    [InlineData("Compré pan 3500", 3500, "Alimentos")]
    [InlineData("Nafta 18000", 18000, "Nafta")]
    [InlineData("Netflix 12000", 12000, "Suscripciones")]
    [InlineData("Luz 15000", 15000, "Luz")]
    [InlineData("Agua 8000", 8000, "Agua")]
    [InlineData("Internet 35000", 35000, "Internet")]
    public void Analizar_DetectaGastosConCategoria(string texto, decimal montoEsperado, string categoriaEsperada)
    {
        var resultado = _parser.Analizar(texto);

        Assert.True(resultado.EsMovimiento);
        Assert.Equal(TipoMovimiento.Egreso, resultado.Tipo);
        Assert.Equal(montoEsperado, resultado.Monto);
        Assert.Equal(categoriaEsperada, resultado.Categoria);
    }

    [Theory]
    [InlineData("Cobré sueldo 950000", 950000)]
    [InlineData("Me pagaron 50000", 50000)]
    [InlineData("Ingreso 100000", 100000)]
    public void Analizar_DetectaIngresos(string texto, decimal montoEsperado)
    {
        var resultado = _parser.Analizar(texto);

        Assert.True(resultado.EsMovimiento);
        Assert.Equal(TipoMovimiento.Ingreso, resultado.Tipo);
        Assert.Equal(montoEsperado, resultado.Monto);
    }

    [Theory]
    [InlineData("Gasté 25.000 en supermercado", 25000)]
    [InlineData("Pagué $ 32.500,50 de internet", 32500.50)]
    public void Analizar_ParseaMontosConSeparadores(string texto, decimal montoEsperado)
    {
        var resultado = _parser.Analizar(texto);

        Assert.True(resultado.EsMovimiento);
        Assert.Equal(montoEsperado, resultado.Monto);
    }

    [Theory]
    [InlineData("Hola, ¿cómo estás?")]
    [InlineData("saldo")]
    [InlineData("")]
    [InlineData("   ")]
    public void Analizar_NoDetectaMovimientoEnMensajesSinMonto(string texto)
    {
        var resultado = _parser.Analizar(texto);

        Assert.False(resultado.EsMovimiento);
    }

    [Fact]
    public void Analizar_FrasesLargasSinVerboNoSonMovimiento()
    {
        // Sin verbo financiero y con muchas palabras: no debe asumir egreso.
        var resultado = _parser.Analizar("El otro día vi una promo de la tele a 500000 pero no la compraría nunca jamás");

        Assert.False(resultado.EsMovimiento);
    }

    [Fact]
    public void Analizar_DescripcionNoQuedaVacia()
    {
        var resultado = _parser.Analizar("Nafta 18000");

        Assert.False(string.IsNullOrWhiteSpace(resultado.Descripcion));
    }
}
