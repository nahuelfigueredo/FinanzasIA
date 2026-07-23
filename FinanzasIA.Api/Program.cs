using FinanzasIA.Application.DependencyInjection;
using FinanzasIA.Api.Options;
using FinanzasIA.Api.Services;
using FinanzasIA.Infrastructure.DependencyInjection;
using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.HttpOverrides;
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
var whatsAppOptionsBuilder = builder.Services.AddOptions<WhatsAppOptions>()
    .Bind(builder.Configuration.GetSection(WhatsAppOptions.SectionName));

// En Production la app no debe arrancar sin las credenciales de WhatsApp:
// un token faltante haría fallar los envíos silenciosamente.
if (!builder.Environment.IsDevelopment())
{
    whatsAppOptionsBuilder
        .Validate(o => !string.IsNullOrWhiteSpace(o.AccessToken), "Falta la variable de entorno WhatsApp__AccessToken.")
        .Validate(o => !string.IsNullOrWhiteSpace(o.PhoneNumberId), "Falta la variable de entorno WhatsApp__PhoneNumberId.")
        .Validate(o => !string.IsNullOrWhiteSpace(o.VerifyToken), "Falta la variable de entorno WhatsApp__VerifyToken.")
        .ValidateOnStart();
}
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.SectionName));
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();
builder.Services.AddHttpClient<FinanzasIA.Application.Interfaces.ITicketOcrProvider, OcrSpaceProvider>();
builder.Services.AddScoped<IUsuarioWhatsAppResolver, UsuarioWhatsAppResolver>();
builder.Services.AddScoped<FinanzasIA.Application.Interfaces.ICanalMensajeriaSender, WhatsAppSenderAdapter>();
builder.Services.AddSingleton<WhatsAppDiagnosticsStore>();
builder.Services.AddScoped<WhatsAppMessageHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }

            return;
        }

        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Asistente con IA real: si hay ApiKey de OpenAI configurada, se usa OpenAI
// (con fallback automático a reglas); si no, queda el proveedor de reglas.
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
if (!string.IsNullOrWhiteSpace(openAiApiKey))
{
    var openAiModel = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";
    builder.Services.AddHttpClient("openai");
    builder.Services.AddScoped<FinanzasIA.Application.Interfaces.IAsistenteIAProvider>(sp =>
        new FinanzasIA.Application.Services.OpenAIAsistenteProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai"),
            sp.GetRequiredService<FinanzasIA.Application.Services.ReglasAsistenteProvider>(),
            sp.GetRequiredService<ILogger<FinanzasIA.Application.Services.OpenAIAsistenteProvider>>(),
            openAiApiKey,
            openAiModel));
}

var app = builder.Build();

// Swagger habilitado también en Production (temporal, para verificar el deploy en Render).
app.UseSwagger();
app.UseSwaggerUI();

// Nota: no se usa UseHttpsRedirection; en Render el TLS lo termina el proxy.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseCors("FrontendPolicy");

app.UseMiddleware<FinanzasIA.Api.Middleware.ApiKeyMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new
{
    Application = "FinanzasIA API",
    Status = "Running",
    Environment = app.Environment.EnvironmentName
}));

// La migración no debe impedir que la app escuche: si falla, se loguea y la app arranca igual.
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FinanzasDbContext>();
    await dbContext.Database.MigrateAsync();
    app.Logger.LogInformation("Migraciones de base de datos aplicadas correctamente");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Error al aplicar migraciones; la aplicación continúa iniciando");
}

app.Logger.LogInformation("FinanzasIA iniciada correctamente");
app.Logger.LogInformation("Aplicación escuchando en el puerto configurado");

app.Run();