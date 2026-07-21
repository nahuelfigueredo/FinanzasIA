namespace FinanzasIA.Api.Options;

/// <summary>Configuración del proveedor OCR (OCR.space).</summary>
public class OcrOptions
{
    public const string SectionName = "Ocr";

    /// <summary>API key de OCR.space. "helloworld" es la clave pública de prueba.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Endpoint del servicio OCR.</summary>
    public string Endpoint { get; set; } = "https://api.ocr.space/parse/image";

    /// <summary>Idioma para el reconocimiento (spa = español).</summary>
    public string Language { get; set; } = "spa";
}
