using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace FinanzasIA.Backoffice.Services;

public class UserIdHeaderHandler : DelegatingHandler
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly string? _apiKey;

    public UserIdHeaderHandler(AuthenticationStateProvider authenticationStateProvider, IConfiguration configuration)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _apiKey = configuration["Api:Key"];
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Remove("X-Api-Key");
            request.Headers.Add("X-Api-Key", _apiKey);
        }

        try
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                request.Headers.Remove("X-User-Id");
                request.Headers.Add("X-User-Id", userId);
            }
        }
        catch
        {
            // Sin estado de autenticación disponible (por ejemplo, prerender); continuar sin header.
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
