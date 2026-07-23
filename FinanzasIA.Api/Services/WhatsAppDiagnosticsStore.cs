using System.Collections.Concurrent;

namespace FinanzasIA.Api.Services;

/// <summary>
/// Registro en memoria de los últimos eventos de WhatsApp (webhooks reales o
/// simulados) para diagnóstico: JSON recibido, texto/imagen detectados,
/// resultado del procesamiento y respuesta enviada.
/// Es un ring buffer: se conservan los últimos <see cref="MaxEntradas"/> eventos.
/// Se pierde al reiniciar el proceso; es solo una herramienta de prueba.
/// </summary>
public class WhatsAppDiagnosticsStore
{
    private const int MaxEntradas = 100;
    private readonly ConcurrentQueue<WhatsAppDiagnosticEntry> _entradas = new();
    private int _nextId;

    public WhatsAppDiagnosticEntry Registrar(WhatsAppDiagnosticEntry entrada)
    {
        entrada.Id = Interlocked.Increment(ref _nextId);
        entrada.FechaUtc = DateTime.UtcNow;
        _entradas.Enqueue(entrada);

        while (_entradas.Count > MaxEntradas && _entradas.TryDequeue(out _))
        {
        }

        return entrada;
    }

    public IReadOnlyCollection<WhatsAppDiagnosticEntry> GetUltimos(int cantidad = 50) =>
        _entradas.Reverse().Take(Math.Clamp(cantidad, 1, MaxEntradas)).ToList();

    public WhatsAppDiagnosticEntry? GetPorId(int id) =>
        _entradas.FirstOrDefault(e => e.Id == id);
}

/// <summary>Un evento de WhatsApp registrado para diagnóstico.</summary>
public class WhatsAppDiagnosticEntry
{
    public int Id { get; set; }
    public DateTime FechaUtc { get; set; }

    /// <summary>Origen del evento: "webhook", "simulado" o "reproceso".</summary>
    public string Origen { get; set; } = "webhook";

    /// <summary>Número de teléfono del remitente.</summary>
    public string? Telefono { get; set; }

    /// <summary>JSON crudo recibido desde Meta (si aplica).</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Texto del mensaje entrante (si era texto).</summary>
    public string? TextoEntrante { get; set; }

    /// <summary>Id de media de la imagen (si era ticket/foto).</summary>
    public string? ImagenMediaId { get; set; }

    /// <summary>Texto extraído por OCR (si se procesó una imagen).</summary>
    public string? TextoOcr { get; set; }

    /// <summary>Id del usuario resuelto a partir del número.</summary>
    public string? UsuarioId { get; set; }

    /// <summary>Id del movimiento creado, si corresponde.</summary>
    public int? MovimientoId { get; set; }

    /// <summary>Respuesta enviada (o que se habría enviado) al usuario.</summary>
    public string? Respuesta { get; set; }

    /// <summary>Indica si el procesamiento terminó sin excepción.</summary>
    public bool Exito { get; set; }

    /// <summary>Detalle del error si el procesamiento falló.</summary>
    public string? Error { get; set; }
}
