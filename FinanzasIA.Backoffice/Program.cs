using FinanzasIA.Api.Options;
using FinanzasIA.Api.Services;
using FinanzasIA.Application.DependencyInjection;
using FinanzasIA.Backoffice.Auth;
using FinanzasIA.Backoffice.Components;
using FinanzasIA.Backoffice.Services;
using FinanzasIA.Infrastructure.DependencyInjection;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Cultura fija es-AR para que los montos se muestren siempre como "$ 1.234,56"
// (en el contenedor Linux de Render la cultura por defecto es la invariante y ToString("C") muestra "¤").
var culture = new System.Globalization.CultureInfo("es-AR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

// Render define PORT; si no hay ASPNETCORE_URLS, escuchar en ese puerto (o 8080 por defecto).
if (!builder.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://+:{port}");
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // DIAGNÓSTICO TEMPORAL: muestra la excepción completa del circuito en la consola del navegador
        // y en los logs del servidor. Quitar cuando se estabilice.
        options.DetailedErrors = true;
    });

// --- Servicios de la API integrada (controllers bajo /api, Swagger, WhatsApp, dominio) ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Registro único compartido con FinanzasIA.Api: garantiza que ambos hosts
// expongan exactamente los mismos servicios para los controllers compartidos.
builder.Services.AddFinanzasIAApiServices(builder.Configuration, builder.Environment);
builder.Services.AddScoped<FinanzasIA.Backoffice.Services.ThemeService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var authConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(authConnectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AuthDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<UserIdHeaderHandler>();

// La API vive en la misma aplicación: el cliente se llama a sí mismo con rutas relativas /api/...
// La URL base se resuelve desde el request actual; si no hay HttpContext (p. ej. circuito interactivo),
// se usa la dirección real en la que escucha Kestrel (nunca un puerto hardcodeado).
builder.Services.AddHttpClient<FinanzasApiClient>((sp, httpClient) =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    var request = accessor.HttpContext?.Request;
    if (request is not null)
    {
        httpClient.BaseAddress = new Uri($"{request.Scheme}://{request.Host}/");
        return;
    }

    // Fallback: dirección real de escucha del servidor (self-call por loopback).
    var server = sp.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
    var address = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()
        ?.Addresses.FirstOrDefault();
    var port = address is not null && Uri.TryCreate(address.Replace("+", "localhost").Replace("*", "localhost").Replace("[::]", "localhost"), UriKind.Absolute, out var listenUri)
        ? listenUri.Port
        : 8080;
    httpClient.BaseAddress = new Uri($"http://localhost:{port}/");
})
.AddHttpMessageHandler<UserIdHeaderHandler>();

var app = builder.Build();

// La migración no debe impedir que la app escuche: si falla, se loguea y la app arranca igual.
app.Logger.LogInformation("Iniciando migraciones de base de datos...");
try
{
    using var scope = app.Services.CreateScope();
    var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    authDb.Database.Migrate();
    var finanzasDb = scope.ServiceProvider.GetRequiredService<FinanzasDbContext>();
    finanzasDb.Database.Migrate();
    app.Logger.LogInformation("Migraciones de base de datos aplicadas correctamente");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Error al aplicar migraciones; la aplicación continúa iniciando");
}

// Encabezados del proxy inverso (Render): deben procesarse ANTES que todo el resto del pipeline
// para que Request.Scheme sea https y las cookies de autenticación/antiforgery funcionen.
var forwardedOptions = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
// Render usa un proxy con IP dinámica: limpiar las listas para aceptar los encabezados reenviados.
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Middleware global de diagnóstico: loguea la excepción original completa (tipo, mensaje y stack trace)
// antes de que cualquier otro handler la procese.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "EXCEPCIÓN NO CONTROLADA en {Method} {Path}", context.Request.Method, context.Request.Path);
        throw;
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // DIAGNÓSTICO TEMPORAL: se deshabilita UseExceptionHandler("/Error") porque ocultaba la excepción original
    // ("An exception was thrown attempting to execute the error handler"). Restaurar cuando se resuelva el 500.
    // app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseDeveloperExceptionPage();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Swagger disponible en /swagger (misma aplicación).
app.UseSwagger();
app.UseSwaggerUI();

// Protección por API key solo para las rutas /api (excepto las públicas definidas en el middleware).
app.UseMiddleware<FinanzasIA.Api.Middleware.ApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

app.UseStaticFiles();
app.Logger.LogInformation("Registrando controllers (MapControllers)...");
app.MapControllers();
app.Logger.LogInformation("Controllers registrados correctamente");
app.MapHealthChecks("/health");
app.Logger.LogInformation("Registrando Razor Components (MapRazorComponents)...");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Logger.LogInformation("Razor Components registrados correctamente");

app.Logger.LogInformation("FinanzasIA iniciada correctamente (Backoffice + API integrada)");
app.Logger.LogInformation("Aplicación escuchando en el puerto configurado");

app.Run();
