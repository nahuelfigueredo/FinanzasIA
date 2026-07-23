using Microsoft.AspNetCore.Mvc;

namespace FinanzasIA.Api.Controllers;

/// <summary>
/// Diagnóstico del contenedor de dependencias en tiempo de ejecución.
/// Permite verificar desde el entorno desplegado qué servicios pueden
/// resolverse realmente y desde qué ensamblado/Program.cs se está corriendo.
/// </summary>
[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public DiagnosticsController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [HttpGet("services")]
    public IActionResult Services()
    {
        var resultados = new List<object>();

        void Probar(Type tipo)
        {
            string estado;
            string? error = null;
            try
            {
                var instancia = _serviceProvider.GetService(tipo);
                estado = instancia is not null ? "resuelto" : "NO REGISTRADO";
            }
            catch (Exception ex)
            {
                estado = "ERROR AL RESOLVER";
                error = ex.Message;
            }

            resultados.Add(new { servicio = tipo.FullName, estado, error });
        }

        Probar(typeof(FinanzasIA.Api.Services.WhatsAppMessageHandler));
        Probar(typeof(FinanzasIA.Api.Services.WhatsAppDiagnosticsStore));
        Probar(typeof(FinanzasIA.Api.Services.IWhatsAppService));
        Probar(typeof(FinanzasIA.Application.Interfaces.IMessageProcessor));
        Probar(typeof(FinanzasIA.Application.Interfaces.IUsuarioWhatsappService));
        Probar(typeof(FinanzasIA.Application.Interfaces.ITicketProcessor));
        Probar(typeof(FinanzasIA.Application.Interfaces.ITicketOcrProvider));

        var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();

        return Ok(new
        {
            // Identifica qué aplicación (qué Program.cs) está corriendo realmente.
            entryAssembly = entryAssembly?.GetName().Name,
            entryAssemblyVersion = entryAssembly?.GetName().Version?.ToString(),
            controllerAssembly = typeof(DiagnosticsController).Assembly.GetName().Name,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            servicios = resultados
        });
    }
}
