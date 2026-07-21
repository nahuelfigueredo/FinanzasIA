namespace FinanzasIA.Api.Middleware;

public class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly string? _apiKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _apiKey = configuration["Api:Key"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Solo protege rutas /api; el resto (Blazor, Swagger, health) es de acceso libre.
        // Sin API key configurada (desarrollo local) o rutas públicas: no se exige.
        // El webhook de WhatsApp (GET/POST /api/whatsapp/webhook) queda excluido para que
        // Meta pueda verificar el webhook y enviar eventos sin autenticación.
        if (string.IsNullOrWhiteSpace(_apiKey) ||
            !context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Path.StartsWithSegments("/api/whatsapp/webhook"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            !string.Equals(providedKey, _apiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("API key inválida o ausente.");
            return;
        }

        await _next(context);
    }
}
