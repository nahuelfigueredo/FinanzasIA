namespace FinanzasIA.Api.Options;

public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string VerifyToken { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
}
