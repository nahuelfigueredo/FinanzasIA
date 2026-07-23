using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Proveedor de IA basado en la API de chat de OpenAI. Serializa el contexto
/// financiero del usuario como JSON, lo envía junto con un system prompt de
/// asesor financiero y el historial de la conversación, y devuelve la
/// respuesta en markdown. Si OpenAI falla, delega en el proveedor de reglas.
/// </summary>
public class OpenAIAsistenteProvider : IAsistenteIAProvider
{
    private readonly HttpClient _httpClient;
    private readonly ReglasAsistenteProvider _fallback;
    private readonly ILogger<OpenAIAsistenteProvider> _logger;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string SystemPrompt = """
        Sos el asesor financiero personal de FinanzasIA, una app de finanzas personales argentina.
        Respondés en español rioplatense (voseo), con tono cercano, profesional y claro.

        Recibirás el contexto financiero REAL del usuario en formato JSON. Usalo siempre para responder
        con números concretos. Nunca inventes datos que no estén en el contexto. Los montos son en pesos
        argentinos: formatealos como $850.000 (separador de miles con punto, sin decimales salvo que aporten).

        FORMATO OBLIGATORIO de tus respuestas (markdown):
        - Nada de párrafos enormes: usá secciones cortas.
        - Usá títulos con emojis discretos, por ejemplo: "📊 Estado actual", "✅ Ingresos", "⚠ Gastos", "💡 Recomendación", "📈 Tendencia".
        - Usá listas con viñetas y **negritas** para los números importantes.
        - Podés usar separadores (---) entre secciones.
        - Cerrá con una recomendación accionable cuando corresponda.
        - Máximo ~200 palabras salvo que el usuario pida detalle.

        Si el usuario pregunta si puede afrontar una compra, analizá balance, ingresos, gastos fijos,
        promedios y proyección de fin de mes, y respondé con una recomendación fundamentada.
        Si falta información (por ejemplo presupuesto no configurado), decilo brevemente.
        """;

    public OpenAIAsistenteProvider(
        HttpClient httpClient,
        ReglasAsistenteProvider fallback,
        ILogger<OpenAIAsistenteProvider> logger,
        string apiKey,
        string model)
    {
        _httpClient = httpClient;
        _fallback = fallback;
        _logger = logger;
        _model = model;
        _httpClient.BaseAddress ??= new Uri("https://api.openai.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> GenerarRespuestaAsync(
        string pregunta,
        ContextoFinancieroDto contexto,
        IReadOnlyCollection<AsistenteMensajeDto>? historial = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<object>
            {
                new { role = "system", content = SystemPrompt },
                new { role = "system", content = "Contexto financiero del usuario (JSON): " + JsonSerializer.Serialize(contexto, JsonOptions) }
            };

            if (historial is not null)
            {
                // Solo los últimos 12 mensajes para controlar el tamaño del prompt.
                foreach (var mensaje in historial.TakeLast(12))
                {
                    messages.Add(new
                    {
                        role = mensaje.EsUsuario ? "user" : "assistant",
                        content = mensaje.Texto
                    });
                }
            }

            messages.Add(new { role = "user", content = pregunta });

            var request = new
            {
                model = _model,
                messages,
                temperature = 0.4,
                max_tokens = 700
            };

            using var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenAI devolvió {Status}: {Body}. Se usa el proveedor de reglas.", response.StatusCode, body);
                return await _fallback.GenerarRespuestaAsync(pregunta, contexto, historial, cancellationToken);
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var content = json.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content)
                ? await _fallback.GenerarRespuestaAsync(pregunta, contexto, historial, cancellationToken)
                : content.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error llamando a OpenAI. Se usa el proveedor de reglas.");
            return await _fallback.GenerarRespuestaAsync(pregunta, contexto, historial, cancellationToken);
        }
    }
}
