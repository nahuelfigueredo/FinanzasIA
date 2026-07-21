using System.Text.Json;
using FinanzasIA.Api.Options;
using FinanzasIA.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace FinanzasIA.Api.Services;

/// <summary>
/// Implementación de <see cref="ITicketOcrProvider"/> usando OCR.space.
/// Reemplazable por Azure Vision, OpenAI Vision, etc. cambiando solo el registro DI.
/// </summary>
public class OcrSpaceProvider : ITicketOcrProvider
{
    private readonly HttpClient _httpClient;
    private readonly OcrOptions _options;
    private readonly ILogger<OcrSpaceProvider> _logger;

    public OcrSpaceProvider(HttpClient httpClient, IOptions<OcrOptions> options, ILogger<OcrSpaceProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ExtraerTextoAsync(byte[] imagen, string mimeType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Ocr:ApiKey is required to process ticket images.");
        }

        using var content = new MultipartFormDataContent();
        var extension = mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var imageContent = new ByteArrayContent(imagen);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        content.Add(imageContent, "file", $"ticket.{extension}");
        content.Add(new StringContent(_options.Language), "language");
        content.Add(new StringContent("2"), "OCREngine");
        content.Add(new StringContent("true"), "scale");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint) { Content = content };
        request.Headers.Add("apikey", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;

        if (root.TryGetProperty("IsErroredOnProcessing", out var errored) && errored.GetBoolean())
        {
            _logger.LogWarning("OCR.space devolvió error de procesamiento: {Detalle}",
                root.TryGetProperty("ErrorMessage", out var err) ? err.ToString() : "sin detalle");
            return string.Empty;
        }

        if (!root.TryGetProperty("ParsedResults", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var textos = results.EnumerateArray()
            .Select(r => r.TryGetProperty("ParsedText", out var t) ? t.GetString() : null)
            .Where(t => !string.IsNullOrWhiteSpace(t));

        return string.Join("\n", textos).Replace("\r\n", "\n");
    }
}
