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
        services.AddScoped<IAsistenteIAProvider, ReglasAsistenteProvider>();
        services.AddScoped<IAsistenteService, AsistenteService>();
        services.AddScoped<IFinanzasAnalyzer, FinanzasAnalyzer>();
        services.AddScoped<ISugerenciasService, SugerenciasService>();

        // Motor genérico de procesamiento de mensajes.
        // Para pasar a OpenAI: reemplazar RuleBasedMessageInterpreter por OpenAIMessageInterpreter.
        services.AddScoped<IMessageInterpreter, RuleBasedMessageInterpreter>();
        services.AddScoped<IMessageActionExecutor, MessageActionExecutor>();
        services.AddScoped<IMessageProcessor, MessageProcessor>();

        return services;
    }
}
