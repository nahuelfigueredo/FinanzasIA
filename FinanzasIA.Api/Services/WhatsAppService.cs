using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FinanzasIA.Api.Options;
using Microsoft.Extensions.Options;

namespace FinanzasIA.Api.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Defensivo: tokens pegados en variables de entorno suelen traer espacios o saltos
        // de línea al final, lo que provoca 401 (code 190, OAuthException) en Meta.
        _options.AccessToken = _options.AccessToken?.Trim() ?? string.Empty;
        _options.VerifyToken = _options.VerifyToken?.Trim() ?? string.Empty;
        _options.PhoneNumberId = _options.PhoneNumberId?.Trim() ?? string.Empty;
        _options.GraphApiVersion = string.IsNullOrWhiteSpace(_options.GraphApiVersion) ? "v23.0" : _options.GraphApiVersion.Trim();
    }

    // TODO: Método temporal de diagnóstico. Eliminar al terminar las pruebas.
    public async Task<(int StatusCode, string ResponseBody)> TestMetaAuthAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://graph.facebook.com/{_options.GraphApiVersion}/{_options.PhoneNumberId}";

        var tokenPrefix = _options.AccessToken.Length >= 10
            ? _options.AccessToken[..10]
            : _options.AccessToken;

        _logger.LogInformation(
            "TestMetaAuth -> PhoneNumberId: {PhoneNumberId}, AccessToken longitud: {TokenLength}, AccessToken inicio: {TokenPrefix}..., URL: {Url}",
            _options.PhoneNumberId,
            _options.AccessToken.Length,
            tokenPrefix,
            url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "TestMetaAuth <- StatusCode: {StatusCode}, Body: {Body}",
            (int)response.StatusCode,
            responseBody);

        return ((int)response.StatusCode, responseBody);
    }

    /// <summary>
    /// Inspecciona el AccessToken con el endpoint debug_token de Graph API
    /// (el token se inspecciona a sí mismo) y consulta el Phone Number ID.
    /// Devuelve validez, vencimiento, permisos y el diagnóstico del error 190.
    /// </summary>
    public async Task<object> InspectTokenAsync(CancellationToken cancellationToken = default)
    {
        // 1) debug_token: el propio token puede inspeccionarse a sí mismo.
        var debugUrl = $"https://graph.facebook.com/{_options.GraphApiVersion}/debug_token?input_token={Uri.EscapeDataString(_options.AccessToken)}";
        using var debugRequest = new HttpRequestMessage(HttpMethod.Get, debugUrl);
        debugRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var debugResponse = await _httpClient.SendAsync(debugRequest, cancellationToken);
        var debugBody = await debugResponse.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("InspectToken debug_token <- {Status}: {Body}", (int)debugResponse.StatusCode, debugBody);

        bool? esValido = null;
        string? tipoToken = null;
        string? appId = null;
        DateTime? expiraUtc = null;
        List<string> permisos = [];
        string? errorDebug = null;

        try
        {
            using var doc = JsonDocument.Parse(debugBody);
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("is_valid", out var v)) esValido = v.GetBoolean();
                if (data.TryGetProperty("type", out var t)) tipoToken = t.GetString();
                if (data.TryGetProperty("app_id", out var a)) appId = a.GetString();
                if (data.TryGetProperty("expires_at", out var e) && e.TryGetInt64(out var unix) && unix > 0)
                {
                    expiraUtc = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                }
                if (data.TryGetProperty("scopes", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
                {
                    permisos = scopes.EnumerateArray().Select(s => s.GetString() ?? "").ToList();
                }
                if (data.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                {
                    errorDebug = msg.GetString();
                }
            }
            else if (doc.RootElement.TryGetProperty("error", out var topError) &&
                     topError.TryGetProperty("message", out var topMsg))
            {
                errorDebug = topMsg.GetString();
            }
        }
        catch (JsonException)
        {
            errorDebug = "Respuesta de debug_token no es JSON válido.";
        }

        // 2) Consultar el Phone Number ID con el mismo token.
        var (phoneStatus, phoneBody) = await TestMetaAuthAsync(cancellationToken);

        // 3) Diagnóstico legible del motivo del 190.
        string diagnostico;
        if (esValido == false)
        {
            diagnostico = errorDebug is not null
                ? $"El token NO es válido: {errorDebug}. Generá uno nuevo (permanente, de System User)."
                : "El token NO es válido (revocado, vencido o mal copiado). Generá uno nuevo.";
        }
        else if (expiraUtc is not null && expiraUtc <= DateTime.UtcNow)
        {
            diagnostico = $"El token venció el {expiraUtc:yyyy-MM-dd HH:mm} UTC. Los tokens temporales de API Setup duran 24 hs; generá uno permanente con un System User.";
        }
        else if (esValido == true && phoneStatus == 401)
        {
            diagnostico = "El token es válido pero NO tiene acceso a este Phone Number ID: pertenece a otra app/WABA, o al System User no se le asignó el asset de la cuenta de WhatsApp.";
        }
        else if (esValido == true && !permisos.Contains("whatsapp_business_messaging"))
        {
            diagnostico = "El token es válido pero le falta el permiso whatsapp_business_messaging.";
        }
        else if (esValido == true && phoneStatus == 200)
        {
            diagnostico = "Token válido y con acceso al Phone Number ID. La autenticación está OK.";
        }
        else
        {
            diagnostico = "No se pudo determinar el estado del token; revisá los cuerpos de respuesta incluidos.";
        }

        return new
        {
            tokenValido = esValido,
            tipoToken,
            appId,
            expiraUtc,
            expiraEn = expiraUtc is null ? "nunca (token permanente)" : (expiraUtc <= DateTime.UtcNow ? "VENCIDO" : (expiraUtc.Value - DateTime.UtcNow).ToString(@"d\d\ h\h\ m\m")),
            permisos,
            tienePermisoMessaging = permisos.Contains("whatsapp_business_messaging"),
            tienePermisoManagement = permisos.Contains("whatsapp_business_management"),
            phoneNumberId = _options.PhoneNumberId,
            consultaPhoneNumberStatus = phoneStatus,
            consultaPhoneNumberBody = phoneBody,
            debugTokenBody = debugBody,
            diagnostico
        };
    }

    public async Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        await SendTextMessageRawAsync(toPhoneNumber, message, cancellationToken);
    }

    /// <summary>
    /// Envía un mensaje de texto y devuelve el status y el body completo de Meta.
    /// Lanza excepción con el contenido devuelto por Meta si la respuesta no es exitosa.
    /// </summary>
    public async Task<(int StatusCode, string ResponseBody)> SendTextMessageRawAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) ||
            string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            throw new InvalidOperationException(
                "WhatsApp AccessToken and PhoneNumberId are required to send messages.");
        }

        if (string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            throw new ArgumentException(
                "Destination phone number is required.",
                nameof(toPhoneNumber));
        }

        var url = _options.MessagesEndpoint;

        // Cuerpo según la especificación de WhatsApp Cloud API:
        // https://developers.facebook.com/docs/whatsapp/cloud-api/reference/messages
        var body = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhoneNumber,
            type = "text",
            text = new
            {
                preview_url = false,
                body = message
            }
        };

        var json = JsonSerializer.Serialize(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        var tokenPrefix = _options.AccessToken.Length >= 10
            ? _options.AccessToken[..10]
            : _options.AccessToken;

        _logger.LogInformation(
            "SendTextMessage -> URL: {Url}, PhoneNumberId: {PhoneNumberId}, To: {To}, AccessToken longitud: {TokenLength}, AccessToken inicio: {TokenPrefix}...",
            url,
            _options.PhoneNumberId,
            toPhoneNumber,
            _options.AccessToken.Length,
            tokenPrefix);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "SendTextMessage <- StatusCode: {StatusCode}, Body: {Body}",
                (int)response.StatusCode,
                responseBody);
        }
        else
        {
            _logger.LogError(
                "SendTextMessage <- Meta devolvió error. StatusCode: {StatusCode}, Body: {Body}",
                (int)response.StatusCode,
                responseBody);

            throw new HttpRequestException(
                $"WhatsApp API devolvió {(int)response.StatusCode} ({response.StatusCode}). Respuesta: {responseBody}");
        }

        return ((int)response.StatusCode, responseBody);
    }

    public async Task<(byte[] Contenido, string MimeType)> DownloadMediaAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new InvalidOperationException(
                "WhatsApp AccessToken is required to download media.");
        }

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            throw new ArgumentException(
                "Media id is required.",
                nameof(mediaId));
        }

        // 1) Obtener la URL temporal del archivo
        using var metadataRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://graph.facebook.com/{_options.GraphApiVersion}/{mediaId}");

        metadataRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var metadataResponse =
            await _httpClient.SendAsync(metadataRequest, cancellationToken);

        var metadataBody =
            await metadataResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!metadataResponse.IsSuccessStatusCode)
        {
            throw new Exception(
                $"WhatsApp API devolviÃ³ {(int)metadataResponse.StatusCode} ({metadataResponse.StatusCode}). " +
                $"Respuesta: {metadataBody}");
        }

        using var metadata = JsonDocument.Parse(metadataBody);

        var url = metadata.RootElement.GetProperty("url").GetString()
            ?? throw new InvalidOperationException(
                "WhatsApp media metadata did not include a URL.");

        var mimeType =
            metadata.RootElement.TryGetProperty("mime_type", out var mimeNode)
                ? mimeNode.GetString() ?? "image/jpeg"
                : "image/jpeg";

        // 2) Descargar el archivo
        using var mediaRequest = new HttpRequestMessage(HttpMethod.Get, url);

        mediaRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var mediaResponse =
            await _httpClient.SendAsync(mediaRequest, cancellationToken);

        var mediaBody =
            await mediaResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!mediaResponse.IsSuccessStatusCode)
        {
            throw new Exception(
                $"WhatsApp API devolviÃ³ {(int)mediaResponse.StatusCode} ({mediaResponse.StatusCode}). " +
                $"Respuesta: {mediaBody}");
        }

        var contenido =
            await mediaResponse.Content.ReadAsByteArrayAsync(cancellationToken);

        return (contenido, mimeType);
    }
}
