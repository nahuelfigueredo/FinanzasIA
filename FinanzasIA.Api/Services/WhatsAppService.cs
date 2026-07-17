using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FinanzasIA.Api.Options;
using Microsoft.Extensions.Options;

namespace FinanzasIA.Api.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;

    public WhatsAppService(HttpClient httpClient, IOptions<WhatsAppOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            throw new InvalidOperationException("WhatsApp AccessToken and PhoneNumberId are required to send messages.");
        }

        if (string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            throw new ArgumentException("Destination phone number is required.", nameof(toPhoneNumber));
        }

        var url = $"https://graph.facebook.com/v23.0/{_options.PhoneNumberId}/messages";
        var body = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            text = new
            {
                body = message
            }
        };

        var json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
