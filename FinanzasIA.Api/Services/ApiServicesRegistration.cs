using FinanzasIA.Api.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanzasIA.Api.Services;

/// <summary>
/// Registro único de todos los servicios de la API compartida (controllers
/// bajo /api). Lo consumen los dos hosts: FinanzasIA.Api (standalone) y
/// FinanzasIA.Backoffice (API integrada). Cualquier servicio nuevo usado por
/// los controllers compartidos debe registrarse acá, y solo acá, para que
/// ambos hosts expongan exactamente el mismo contenedor.
/// </summary>
public static class ApiServicesRegistration
{
    public static IServiceCollection AddFinanzasIAApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Opciones de WhatsApp con fail-fast en entornos no-Development:
        // la app no debe arrancar sin credenciales (los envíos fallarían en silencio).
        var whatsAppOptionsBuilder = services.AddOptions<WhatsAppOptions>()
            .Bind(configuration.GetSection(WhatsAppOptions.SectionName));

        if (!environment.IsDevelopment())
        {
            whatsAppOptionsBuilder
                .Validate(o => !string.IsNullOrWhiteSpace(o.AccessToken), "Falta la variable de entorno WhatsApp__AccessToken.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.PhoneNumberId), "Falta la variable de entorno WhatsApp__PhoneNumberId.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.VerifyToken), "Falta la variable de entorno WhatsApp__VerifyToken.")
                .ValidateOnStart();
        }

        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));

        // Servicios de la integración WhatsApp / OCR.
        services.AddHttpClient<IWhatsAppService, WhatsAppService>();
        services.AddHttpClient<FinanzasIA.Application.Interfaces.ITicketOcrProvider, OcrSpaceProvider>();
        services.AddScoped<IUsuarioWhatsAppResolver, UsuarioWhatsAppResolver>();
        services.AddScoped<FinanzasIA.Application.Interfaces.ICanalMensajeriaSender, WhatsAppSenderAdapter>();
        services.AddSingleton<WhatsAppDiagnosticsStore>();
        services.AddScoped<WhatsAppMessageHandler>();

        return services;
    }
}
