namespace FinanzasIA.Api.Services;

public interface IWhatsAppService
{
    Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);

    /// <summary>Descarga el contenido binario de un media (imagen) recibido por WhatsApp.</summary>
    Task<(byte[] Contenido, string MimeType)> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default);
}
