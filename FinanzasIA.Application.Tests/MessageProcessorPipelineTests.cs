using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Application.Services;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinanzasIA.Application.Tests;

/// <summary>
/// Tests del pipeline completo de mensajes (integración a nivel Application):
/// MessageProcessor real + RuleBasedMessageInterpreter real +
/// MensajeFinancieroParser real + MessageActionExecutor real, con fakes solo
/// en los bordes (servicios de dominio, IA e historial). Simula los mensajes
/// que llegan por WhatsApp y verifica el movimiento creado y la respuesta.
/// </summary>
public class MessageProcessorPipelineTests
{
    private readonly FakeMovimientoService _movimientos = new();
    private readonly FakeCategoriaService _categorias = new();
    private readonly FakeCuentaService _cuentas = new();
    private readonly FakeAnalisisFinancieroService _analisis = new();
    private readonly FakeAsistenteIAProvider _ia = new();
    private readonly FakeMensajeProcesadoRepository _historial = new();
    private readonly FakePresupuestoService _presupuestos = new();

    private MessageProcessor CrearProcessor()
    {
        var executor = new MessageActionExecutor(_movimientos, _categorias, _cuentas, _analisis, _presupuestos);
        return new MessageProcessor(
            new RuleBasedMessageInterpreter(),
            new MensajeFinancieroParser(),
            _ia,
            executor,
            _historial,
            NullLogger<MessageProcessor>.Instance);
    }

    private Task<MensajeProcesadoResultDto> ProcesarAsync(string texto) =>
        CrearProcessor().ProcesarAsync(new MensajeEntranteDto
        {
            Texto = texto,
            Origen = MessageOrigen.WhatsApp,
            UsuarioId = "user-1"
        });

    [Theory]
    [InlineData("Gasté 25000 en supermercado", 25000)]
    [InlineData("Compré pan 3500", 3500)]
    [InlineData("Nafta 18000", 18000)]
    [InlineData("Netflix 12000", 12000)]
    [InlineData("Luz 15000", 15000)]
    [InlineData("Agua 8000", 8000)]
    [InlineData("Internet 35000", 35000)]
    public async Task Procesar_GastosObligatorios_CreanMovimientoEgreso(string texto, decimal monto)
    {
        var resultado = await ProcesarAsync(texto);

        Assert.True(resultado.Exito, $"Falló: {texto} → {resultado.Respuesta}");
        Assert.NotNull(resultado.MovimientoId);
        var creado = Assert.Single(_movimientos.Creados);
        Assert.Equal(TipoMovimiento.Egreso, creado.Tipo);
        Assert.Equal(monto, creado.Monto);
        Assert.Null(creado.CuentaId); // cuenta por defecto
        Assert.NotEmpty(_categorias.Categorias); // categoría asignada/creada
        Assert.Contains("✅", resultado.Respuesta);
    }

    [Theory]
    [InlineData("Cobré sueldo 950000", 950000)]
    [InlineData("Me pagaron 50000", 50000)]
    [InlineData("Ingreso 100000", 100000)]
    public async Task Procesar_IngresosObligatorios_CreanMovimientoIngreso(string texto, decimal monto)
    {
        var resultado = await ProcesarAsync(texto);

        Assert.True(resultado.Exito, $"Falló: {texto} → {resultado.Respuesta}");
        var creado = Assert.Single(_movimientos.Creados);
        Assert.Equal(TipoMovimiento.Ingreso, creado.Tipo);
        Assert.Equal(monto, creado.Monto);
    }

    [Theory]
    [InlineData("Saldo")]
    [InlineData("Resumen")]
    [InlineData("¿Cuánto gasté hoy?")]
    [InlineData("¿Cuánto gasté este mes?")]
    [InlineData("¿Cuánto ingresé este mes?")]
    [InlineData("Últimos gastos")]
    public async Task Procesar_ConsultasObligatorias_RespondenSinCrearMovimientos(string texto)
    {
        var resultado = await ProcesarAsync(texto);

        Assert.True(resultado.Exito, $"Falló: {texto} → {resultado.Respuesta}");
        Assert.Empty(_movimientos.Creados);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Respuesta));
    }

    [Theory]
    [InlineData("Gasté 25000 en supermercado")]
    [InlineData("Nafta 18000")]
    [InlineData("Saldo")]
    public async Task Procesar_NoInvocaIaCuandoLasReglasOElParserResuelven(string texto)
    {
        await ProcesarAsync(texto);

        Assert.Equal(0, _ia.Invocaciones);
    }

    [Fact]
    public async Task Procesar_InvocaIaSoloCuandoReglasYParserFallan()
    {
        _ia.RespuestaJson = """{"tipo":"Gasto","monto":25000,"categoria":"Supermercado","descripcion":"Compra Carrefour"}""";

        var resultado = await ProcesarAsync("ayer fui al chino de la esquina y se me fueron como veinticinco lucas 25000");

        Assert.Equal(1, _ia.Invocaciones);
        Assert.True(resultado.Exito);
        var creado = Assert.Single(_movimientos.Creados);
        Assert.Equal(25000m, creado.Monto);
        Assert.Equal(TipoMovimiento.Egreso, creado.Tipo);
    }

    [Fact]
    public async Task Procesar_IaSinMovimientoValidoNoCreaNada()
    {
        _ia.RespuestaJson = "{}";

        var resultado = await ProcesarAsync("bla bla texto sin sentido");

        Assert.Equal(1, _ia.Invocaciones);
        Assert.False(resultado.Exito);
        Assert.Empty(_movimientos.Creados);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Respuesta)); // respuesta amigable
    }

    [Fact]
    public async Task Procesar_ErrorEnCreacion_RespondeAmigableYGuardaHistorial()
    {
        _movimientos.FallarAlCrear = true;

        var resultado = await ProcesarAsync("Gasté 25000 en supermercado");

        Assert.False(resultado.Exito);
        Assert.Contains("salió mal", resultado.Respuesta);
        // El mensaje recibido nunca se pierde: quedó en el historial.
        var registro = Assert.Single(_historial.Mensajes);
        Assert.Contains("25000", registro.Texto);
    }

    [Fact]
    public async Task Procesar_SiempreGuardaHistorialDelMensaje()
    {
        await ProcesarAsync("Gasté 25000 en supermercado");

        var registro = Assert.Single(_historial.Mensajes);
        Assert.Equal((int)MessageOrigen.WhatsApp, registro.Origen);
        Assert.True(registro.Exito);
        Assert.NotNull(registro.MovimientoId);
    }

    private sealed class FakeMensajeProcesadoRepository : IMensajeProcesadoRepository
    {
        public List<MensajeProcesado> Mensajes { get; } = [];

        public Task<IReadOnlyCollection<MensajeProcesado>> GetUltimosAsync(string? usuarioId = null, int cantidad = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MensajeProcesado>>(Mensajes);

        public Task<MensajeProcesado> AddAsync(MensajeProcesado mensaje, CancellationToken cancellationToken = default)
        {
            Mensajes.Add(mensaje);
            return Task.FromResult(mensaje);
        }
    }
}
