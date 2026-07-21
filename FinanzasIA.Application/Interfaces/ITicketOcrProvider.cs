namespace FinanzasIA.Application.Interfaces;

/// <summary>
/// Abstracción del proveedor de OCR. La implementación concreta
/// (OCR.space, Azure Vision, OpenAI Vision, etc.) se registra por DI en la
/// capa de host; Application no conoce ningún proveedor específico.
/// </summary>
public interface ITicketOcrProvider
{
    /// <summary>Extrae el texto de una imagen. Devuelve string vacío si no reconoce nada.</summary>
    Task<string> ExtraerTextoAsync(byte[] imagen, string mimeType, CancellationToken cancellationToken = default);
}
