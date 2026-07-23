using System.Security.Claims;
using FinanzasIA.Application.DTOs;
using Microsoft.AspNetCore.Components.Authorization;

namespace FinanzasIA.Backoffice.Services;

public class FinanzasApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public FinanzasApiClient(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider)
    {
        _httpClient = httpClient;
        _authenticationStateProvider = authenticationStateProvider;
    }

    // El header se establece aquí (y no en un DelegatingHandler) porque los message handlers
    // de HttpClientFactory viven en un scope de DI propio sin acceso al estado de autenticación del circuito.
    private async Task EnsureUserHeaderAsync()
    {
        try
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
            _httpClient.DefaultRequestHeaders.Remove("X-User-Id");
            if (!string.IsNullOrWhiteSpace(userId))
            {
                _httpClient.DefaultRequestHeaders.Add("X-User-Id", userId);
            }
        }
        catch
        {
            // Sin estado de autenticación disponible (por ejemplo, prerender); continuar sin header.
        }
    }

    public async Task<IReadOnlyCollection<CategoriaDto>> GetCategoriasAsync(CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var categorias = await _httpClient.GetFromJsonAsync<List<CategoriaDto>>("api/categoria", cancellationToken);
        return categorias ?? [];
    }

    public async Task<CategoriaDto> CreateCategoriaAsync(CreateCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("api/categoria", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoriaDto>(cancellationToken))!;
    }

    public async Task<CategoriaDto> UpdateCategoriaAsync(int id, UpdateCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/categoria/{id}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoriaDto>(cancellationToken))!;
    }

    public async Task DeleteCategoriaAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.DeleteAsync($"api/categoria/{id}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
            throw new InvalidOperationException(error?.Message ?? "No se pudo eliminar la categoría.");
        }
    }

    private sealed class ErrorResponse
    {
        public string? Message { get; set; }
    }

    public async Task<IReadOnlyCollection<MovimientoDto>> GetMovimientosAsync(CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var movimientos = await _httpClient.GetFromJsonAsync<List<MovimientoDto>>("api/movimiento", cancellationToken);
        return movimientos ?? [];
    }

    public async Task<MovimientoDto> CreateMovimientoAsync(CreateMovimientoDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("api/movimiento", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MovimientoDto>(cancellationToken))!;
    }

    public async Task<MovimientoDto> UpdateMovimientoAsync(int id, UpdateMovimientoDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/movimiento/{id}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MovimientoDto>(cancellationToken))!;
    }

    public async Task DeleteMovimientoAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.DeleteAsync($"api/movimiento/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AnalisisFinancieroDto> GetAnalisisIaAsync(CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var analisis = await _httpClient.GetFromJsonAsync<AnalisisFinancieroDto>("api/ia/analisis", cancellationToken);
        return analisis ?? new AnalisisFinancieroDto();
    }

    public async Task<IReadOnlyCollection<CuentaDto>> GetCuentasAsync(CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var cuentas = await _httpClient.GetFromJsonAsync<List<CuentaDto>>("api/cuenta", cancellationToken);
        return cuentas ?? [];
    }

    public async Task<CuentaDto> CreateCuentaAsync(CreateCuentaDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("api/cuenta", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CuentaDto>(cancellationToken))!;
    }

    public async Task<CuentaDto> UpdateCuentaAsync(int id, UpdateCuentaDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/cuenta/{id}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CuentaDto>(cancellationToken))!;
    }

    public async Task DeleteCuentaAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.DeleteAsync($"api/cuenta/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AsistenteRespuestaDto> PreguntarAsistenteAsync(string pregunta, List<AsistenteMensajeDto>? historial = null, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("api/asistente/preguntar", new AsistentePreguntaDto
        {
            Pregunta = pregunta,
            Historial = historial ?? []
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AsistenteRespuestaDto>(cancellationToken))!;
    }

    public async Task<IReadOnlyCollection<SugerenciaDto>> GetSugerenciasAsync(decimal? presupuesto = null, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var url = presupuesto is > 0
            ? $"api/asistente/sugerencias?presupuesto={presupuesto.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : "api/asistente/sugerencias";
        var sugerencias = await _httpClient.GetFromJsonAsync<List<SugerenciaDto>>(url, cancellationToken);
        return sugerencias ?? [];
    }

    public async Task<IReadOnlyCollection<MensajeLogDto>> GetMensajesProcesadosAsync(int cantidad = 100, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var mensajes = await _httpClient.GetFromJsonAsync<List<MensajeLogDto>>($"api/mensajes?cantidad={cantidad}", cancellationToken);
        return mensajes ?? [];
    }

    public async Task<IReadOnlyCollection<UsuarioWhatsappDto>> GetNumerosWhatsappAsync(CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var numeros = await _httpClient.GetFromJsonAsync<List<UsuarioWhatsappDto>>("api/usuario-whatsapp", cancellationToken);
        return numeros ?? [];
    }

    public async Task<VinculacionResultDto> VincularNumeroWhatsappAsync(VincularNumeroDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("api/usuario-whatsapp/vincular", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VinculacionResultDto>(cancellationToken))!;
    }

    public async Task EliminarNumeroWhatsappAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureUserHeaderAsync();
        var response = await _httpClient.DeleteAsync($"api/usuario-whatsapp/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
