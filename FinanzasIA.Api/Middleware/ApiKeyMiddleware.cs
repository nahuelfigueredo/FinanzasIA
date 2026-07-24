namespace FinanzasIA.Api.Middleware;

public class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly string? _apiKey;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _apiKey = configuration["Api:Key"];
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var tieneHeader = context.Request.Headers.ContainsKey(HeaderName);

        // Solo protege rutas /api; el resto (Blazor, Swagger, health) es de acceso libre.
        // Sin API key configurada (desarrollo local) o rutas públicas: no se exige.
        // El webhook de WhatsApp (GET/POST /api/whatsapp/webhook) queda excluido para que
        // Meta pueda verificar el webhook y enviar eventos sin autenticación.
        if (string.IsNullOrWhiteSpace(_apiKey) ||
            !path.StartsWithSegments("/api") ||
            path.StartsWithSegments("/api/whatsapp/webhook"))
        {
            if (path.StartsWithSegments("/api"))
            {
                _logger.LogInformation(
                    "APIKEY: {Path} -> EXCEPTUADA de validación (headerPresente: {TieneHeader})",
                    path,
                    tieneHeader);
            }
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            !string.Equals(providedKey, _apiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "APIKEY: {Path} -> RECHAZADA con 401 (headerPresente: {TieneHeader})",
                path,
                tieneHeader);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("API key inválida o ausente.");
            return;
        }

        _logger.LogInformation(
            "APIKEY: {Path} -> AUTORIZADA (headerPresente: {TieneHeader})",
            path,
            tieneHeader);
        await _next(context);
    }
}
