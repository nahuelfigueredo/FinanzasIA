namespace FinanzasIA.Api.Services;

public interface IWhatsAppService
{
    Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);
}
