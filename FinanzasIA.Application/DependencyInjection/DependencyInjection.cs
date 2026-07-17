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
        services.AddScoped<IAnalisisFinancieroService, AnalisisFinancieroService>();

        return services;
    }
}
