using FinanzasIA.Application.Interfaces;
using FinanzasIA.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanzasIA.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IMovimientoService, MovimientoService>();
        services.AddScoped<ICuentaService, CuentaService>();
        services.AddScoped<IAnalisisFinancieroService, AnalisisFinancieroService>();
        services.AddScoped<IPresupuestoService, PresupuestoService>();
        // Proveedor de reglas siempre disponible (fallback del proveedor OpenAI).
        services.AddScoped<ReglasAsistenteProvider>();
        // Registro por defecto: si la capa host configura OpenAI, lo reemplaza.
        services.AddScoped<IAsistenteIAProvider>(sp => sp.GetRequiredService<ReglasAsistenteProvider>());
        services.AddScoped<IAsistenteService, AsistenteService>();
        services.AddScoped<IFinanzasAnalyzer, FinanzasAnalyzer>();
        services.AddScoped<ISugerenciasService, SugerenciasService>();
        services.AddScoped<IAutomatizacionesService, AutomatizacionesService>();

        // Motor genérico de procesamiento de mensajes.
        // Para pasar a OpenAI: reemplazar RuleBasedMessageInterpreter por OpenAIMessageInterpreter.
        services.AddScoped<IMessageInterpreter, RuleBasedMessageInterpreter>();
        services.AddScoped<IMensajeFinancieroParser, MensajeFinancieroParser>();
        services.AddScoped<IMessageActionExecutor, MessageActionExecutor>();
        services.AddScoped<IMessageProcessor, MessageProcessor>();

        // Vinculación de números de mensajería (WhatsApp hoy, otros canales a futuro).
        services.AddScoped<IUsuarioWhatsappService, UsuarioWhatsappService>();

        // Carga automática de gastos desde tickets (OCR).
        // El ITicketOcrProvider concreto se registra en la capa de host (Api).
        services.AddScoped<ITicketProcessor, TicketProcessor>();

        return services;
    }
}
