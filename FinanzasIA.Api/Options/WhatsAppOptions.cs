namespace FinanzasIA.Api.Options;

public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string VerifyToken { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Versión de la Graph API de Meta a utilizar.</summary>
    public string GraphApiVersion { get; set; } = "v23.0";

    /// <summary>Endpoint que se usará para enviar mensajes.</summary>
    public string MessagesEndpoint =>
        $"https://graph.facebook.com/{GraphApiVersion}/{PhoneNumberId}/messages";
}
