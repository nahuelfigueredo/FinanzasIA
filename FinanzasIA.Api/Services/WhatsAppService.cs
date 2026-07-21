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
    }

    // TODO: Método temporal de diagnóstico. Eliminar al terminar las pruebas.
    public async Task<(int StatusCode, string ResponseBody)> TestMetaAuthAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://graph.facebook.com/v25.0/{_options.PhoneNumberId}";

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

    public async Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
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

        var url = $"https://graph.facebook.com/v23.0/{_options.PhoneNumberId}/messages";

        var body = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            text = new
            {
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
            "SendTextMessage -> URL: {Url}, PhoneNumberId: {PhoneNumberId}, AccessToken longitud: {TokenLength}, AccessToken inicio: {TokenPrefix}...",
            url,
            _options.PhoneNumberId,
            _options.AccessToken.Length,
            tokenPrefix);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "SendTextMessage <- StatusCode: {StatusCode}, Body: {Body}",
            (int)response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"WhatsApp API devolviÃ³ {(int)response.StatusCode} ({response.StatusCode}). " +
                $"Respuesta: {responseBody}");
        }
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
            $"https://graph.facebook.com/v23.0/{mediaId}");

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
