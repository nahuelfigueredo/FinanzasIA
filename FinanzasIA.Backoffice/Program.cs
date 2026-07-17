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
    .AddInteractiveServerComponents();

// --- Servicios de la API integrada (controllers bajo /api, Swagger, WhatsApp, dominio) ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection(WhatsAppOptions.SectionName));
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IUsuarioWhatsAppResolver, UsuarioWhatsAppResolver>();
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

// La API vive en la misma aplicación: el cliente llama a la propia instancia (self-call) con rutas relativas /api/...
var selfPort = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var selfBaseUrl = builder.Environment.IsDevelopment()
    ? null
    : $"http://localhost:{selfPort}/";

builder.Services.AddHttpClient<FinanzasApiClient>((sp, httpClient) =>
{
    if (selfBaseUrl is not null)
    {
        httpClient.BaseAddress = new Uri(selfBaseUrl);
        return;
    }

    // En desarrollo, usar la URL en la que corre esta misma app.
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    var request = accessor.HttpContext?.Request;
    var baseUrl = request is not null
        ? $"{request.Scheme}://{request.Host}/"
        : "http://localhost:5000/";
    httpClient.BaseAddress = new Uri(baseUrl);
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

// Middleware global de diagnóstico: loguea la excepción original completa (tipo, mensaje y stack trace)
// antes de que cualquier otro handler la procese. Debe ir PRIMERO en el pipeline.
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
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});
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
