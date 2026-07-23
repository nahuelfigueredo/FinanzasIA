namespace FinanzasIA.Api.Services;

public interface IWhatsAppService
{
    Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);

    /// <summary>Envía un mensaje de texto y devuelve el status y el body completo de la respuesta de Meta.</summary>
    Task<(int StatusCode, string ResponseBody)> SendTextMessageRawAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);

    /// <summary>Descarga el contenido binario de un media (imagen) recibido por WhatsApp.</summary>
    Task<(byte[] Contenido, string MimeType)> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default);

    /// <summary>Diagnóstico temporal: verifica la autenticación contra Meta Graph API.</summary>
    Task<(int StatusCode, string ResponseBody)> TestMetaAuthAsync(CancellationToken cancellationToken = default);
}
