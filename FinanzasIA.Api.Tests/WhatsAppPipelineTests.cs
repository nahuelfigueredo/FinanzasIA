using System.Diagnostics;
using System.Text.Json;
using FinanzasIA.Api.Controllers;
using FinanzasIA.Api.Options;
using FinanzasIA.Api.Services;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace FinanzasIA.Api.Tests;

/// <summary>
/// Suite de pruebas del pipeline completo de WhatsApp e IA:
/// payload de Meta -> webhook -> WhatsAppMessageHandler -> motor de mensajes -> respuesta.
/// Todas las dependencias externas (Meta, WhatsApp, IA, base de datos) están mockeadas,
/// por lo que la suite se ejecuta sin depender de servicios externos ni de un teléfono real.
/// </summary>
public class WhatsAppPipelineTests
{
    private readonly ITestOutputHelper _output;

    public WhatsAppPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Casos de intención (payload -> intent esperado -> movimiento esperado) ----------

    [Theory]
    [InlineData("gasto.json", "RegistrarGasto", true)]
    [InlineData("ingreso.json", "RegistrarIngreso", true)]
    [InlineData("transferencia.json", "Transferencia", false)]
    [InlineData("consulta-saldo.json", "ConsultaSaldo", false)]
    [InlineData("sin-intencion.json", "Desconocido", false)]
    public async Task Pipeline_DetectaIntencion_YCreaMovimientoEsperado(string payloadFile, string intentEsperado, bool creaMovimiento)
    {
        var ctx = TestContext.Crear();
        var payload = CargarPayload(payloadFile);
        var reloj = Stopwatch.StartNew();

        var result = await ctx.Controller.SimularWebhook(payload, send: false, ctx.Environment, CancellationToken.None);
        reloj.Stop();

        var entrada = Assert.Single(ctx.Diagnostics.GetUltimos());
        try
        {
            Assert.Equal(intentEsperado, entrada.Intent);
            Assert.Equal(creaMovimiento, entrada.MovimientoId is not null);
            Assert.False(string.IsNullOrWhiteSpace(entrada.Respuesta)); // la respuesta de IA nunca es nula
            Assert.IsType<OkObjectResult>(result);

            ImprimirResumen(payloadFile, entrada.Intent, entrada.MovimientoId, reloj.ElapsedMilliseconds, ok: true);
        }
        catch
        {
            ImprimirResumen(payloadFile, entrada.Intent, entrada.MovimientoId, reloj.ElapsedMilliseconds, ok: false);
            throw;
        }
    }

    // ---------- Envío por WhatsApp controlado por enviarRespuesta ----------

    [Fact]
    public async Task Pipeline_ConSendFalse_NuncaEnviaWhatsApp()
    {
        var ctx = TestContext.Crear();
        var reloj = Stopwatch.StartNew();

        await ctx.Controller.SimularWebhook(CargarPayload("gasto.json"), send: false, ctx.Environment, CancellationToken.None);
        reloj.Stop();

        Assert.Equal(0, ctx.WhatsApp.EnviosRealizados);

        var entrada = Assert.Single(ctx.Diagnostics.GetUltimos());
        ImprimirResumen("gasto.json (send=false)", entrada.Intent, entrada.MovimientoId, reloj.ElapsedMilliseconds, ok: true);
    }

    [Fact]
    public async Task Pipeline_ConSendTrue_EnviaWhatsAppExactamenteUnaVez()
    {
        var ctx = TestContext.Crear();
        var reloj = Stopwatch.StartNew();

        await ctx.Controller.SimularWebhook(CargarPayload("gasto.json"), send: true, ctx.Environment, CancellationToken.None);
        reloj.Stop();

        Assert.Equal(1, ctx.WhatsApp.EnviosRealizados);
        Assert.Equal("5492215916893", ctx.WhatsApp.UltimoDestinatario);

        var entrada = Assert.Single(ctx.Diagnostics.GetUltimos());
        ImprimirResumen("gasto.json (send=true)", entrada.Intent, entrada.MovimientoId, reloj.ElapsedMilliseconds, ok: true);
    }

    // ---------- Payloads que no deben ejecutar el pipeline de IA ----------

    [Fact]
    public async Task Pipeline_ConSoloStatuses_NoProcesaNiEnvia()
    {
        var ctx = TestContext.Crear();
        var reloj = Stopwatch.StartNew();

        var result = await ctx.Controller.SimularWebhook(CargarPayload("solo-statuses.json"), send: true, ctx.Environment, CancellationToken.None);
        reloj.Stop();

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(ctx.Diagnostics.GetUltimos().Where(e => e.Origen == "simulado"));
        Assert.Equal(0, ctx.WhatsApp.EnviosRealizados);
        Assert.Equal(0, ctx.Processor.Invocaciones);

        ImprimirResumen("solo-statuses.json", "(ninguna)", null, reloj.ElapsedMilliseconds, ok: true);
    }

    [Fact]
    public async Task Pipeline_ConPayloadInvalido_NoProcesaNiFalla()
    {
        var ctx = TestContext.Crear();
        var reloj = Stopwatch.StartNew();

        var result = await ctx.Controller.SimularWebhook(CargarPayload("invalido.json"), send: true, ctx.Environment, CancellationToken.None);
        reloj.Stop();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, ctx.WhatsApp.EnviosRealizados);
        Assert.Equal(0, ctx.Processor.Invocaciones);

        ImprimirResumen("invalido.json", "(ninguna)", null, reloj.ElapsedMilliseconds, ok: true);
    }

    // ---------- Infraestructura de la suite ----------

    private static JsonDocument CargarPayload(string nombreArchivo)
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Payloads", nombreArchivo);
        return JsonDocument.Parse(File.ReadAllText(ruta));
    }

    private void ImprimirResumen(string caso, string? intent, int? movimientoId, long duracionMs, bool ok)
    {
        _output.WriteLine("---------- RESUMEN ----------");
        _output.WriteLine($"Caso:                {caso}");
        _output.WriteLine($"Intención detectada: {intent ?? "(ninguna)"}");
        _output.WriteLine($"Movimiento creado:   {(movimientoId is not null ? $"sí (Id {movimientoId})" : "no")}");
        _output.WriteLine($"Tiempo:              {duracionMs} ms");
        _output.WriteLine($"Resultado:           {(ok ? "OK" : "ERROR")}");
        _output.WriteLine("-----------------------------");
    }

    /// <summary>Arma el controller real con todas las dependencias externas mockeadas.</summary>
    private sealed class TestContext
    {
        public required WhatsAppWebhookController Controller { get; init; }
        public required FakeWhatsAppService WhatsApp { get; init; }
        public required FakeMessageProcessor Processor { get; init; }
        public required WhatsAppDiagnosticsStore Diagnostics { get; init; }
        public required IWebHostEnvironment Environment { get; init; }

        public static TestContext Crear()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new WhatsAppOptions
            {
                AccessToken = "token-test",
                PhoneNumberId = "12345",
                VerifyToken = "verify-test"
            });

            var whatsApp = new FakeWhatsAppService();
            var processor = new FakeMessageProcessor();
            var diagnostics = new WhatsAppDiagnosticsStore();

            var handler = new WhatsAppMessageHandler(
                options,
                whatsApp,
                processor,
                new FakeUsuarioWhatsappService(),
                new FakeTicketProcessor(),
                diagnostics,
                NullLogger<WhatsAppMessageHandler>.Instance);

            var controller = new WhatsAppWebhookController(
                options,
                whatsApp,
                handler,
                NullLogger<WhatsAppWebhookController>.Instance);

            return new TestContext
            {
                Controller = controller,
                WhatsApp = whatsApp,
                Processor = processor,
                Diagnostics = diagnostics,
                Environment = new FakeWebHostEnvironment()
            };
        }
    }

    /// <summary>Mock de WhatsApp: cuenta los envíos en lugar de llamar a Meta.</summary>
    private sealed class FakeWhatsAppService : IWhatsAppService
    {
        public int EnviosRealizados { get; private set; }
        public string? UltimoDestinatario { get; private set; }

        public Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
        {
            EnviosRealizados++;
            UltimoDestinatario = toPhoneNumber;
            return Task.CompletedTask;
        }

        public Task<(int StatusCode, string ResponseBody)> SendTextMessageRawAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
            => Task.FromResult((200, "{}"));

        public Task<(byte[] Contenido, string MimeType)> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default)
            => Task.FromResult((Array.Empty<byte>(), "image/jpeg"));

        public Task<(int StatusCode, string ResponseBody)> TestMetaAuthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((200, "{}"));

        public Task<object> InspectTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<object>(new { });
    }

    /// <summary>
    /// Mock del motor de mensajes (IA): interpreta por palabras clave de forma determinística,
    /// sin base de datos ni OpenAI, imitando el contrato real del pipeline.
    /// </summary>
    private sealed class FakeMessageProcessor : IMessageProcessor
    {
        private int _nextMovimientoId;
        public int Invocaciones { get; private set; }

        public Task<MensajeProcesadoResultDto> ProcesarAsync(MensajeEntranteDto mensaje, CancellationToken cancellationToken = default)
        {
            Invocaciones++;
            var texto = mensaje.Texto.ToLowerInvariant();

            var resultado = texto switch
            {
                _ when texto.Contains("gast") => new MensajeProcesadoResultDto
                {
                    Intent = MessageIntent.RegistrarGasto,
                    Respuesta = "Perfecto. Registré el gasto.",
                    Exito = true,
                    Manejado = true,
                    MovimientoId = ++_nextMovimientoId
                },
                _ when texto.Contains("cobr") || texto.Contains("sueldo") => new MensajeProcesadoResultDto
                {
                    Intent = MessageIntent.RegistrarIngreso,
                    Respuesta = "Perfecto. Registré el ingreso.",
                    Exito = true,
                    Manejado = true,
                    MovimientoId = ++_nextMovimientoId
                },
                _ when texto.Contains("transfer") => new MensajeProcesadoResultDto
                {
                    Intent = MessageIntent.Transferencia,
                    Respuesta = "Transferencia registrada entre tus cuentas.",
                    Exito = true,
                    Manejado = true
                },
                _ when texto.Contains("saldo") => new MensajeProcesadoResultDto
                {
                    Intent = MessageIntent.ConsultaSaldo,
                    Respuesta = "Tu saldo actual es $100.000.",
                    Exito = true,
                    Manejado = true
                },
                _ => new MensajeProcesadoResultDto
                {
                    Intent = MessageIntent.Desconocido,
                    Respuesta = "No entendí tu mensaje. Probá con 'Gasté 5000 en comida'.",
                    Exito = false,
                    Manejado = false
                }
            };

            return Task.FromResult(resultado);
        }
    }

    /// <summary>Mock de vinculación: todo número está vinculado al usuario de prueba.</summary>
    private sealed class FakeUsuarioWhatsappService : IUsuarioWhatsappService
    {
        public Task<string?> BuscarUsuarioPorNumeroAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("user-test");

        public Task<VinculacionResultDto> VincularAsync(VincularNumeroDto dto, string usuarioId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No usado en estas pruebas.");

        public Task<bool> DesvincularAsync(int id, string usuarioId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No usado en estas pruebas.");

        public Task<IReadOnlyCollection<UsuarioWhatsappDto>> ObtenerNumerosAsync(string usuarioId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No usado en estas pruebas.");
    }

    /// <summary>Mock de tickets: nunca hay pendientes (los casos de imagen no aplican aquí).</summary>
    private sealed class FakeTicketProcessor : ITicketProcessor
    {
        public Task<bool> TienePendienteAsync(string usuarioId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<TicketResultDto> ProcesarImagenAsync(TicketImagenDto imagen, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No usado en estas pruebas.");

        public Task<TicketResultDto> CompletarDatoAsync(string usuarioId, string respuestaUsuario, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No usado en estas pruebas.");
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FinanzasIA.Api.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
