using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Backoffice.Services;

public class FinanzasApiClient
{
    private readonly HttpClient _httpClient;

    public FinanzasApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyCollection<CategoriaDto>> GetCategoriasAsync(CancellationToken cancellationToken = default)
    {
        var categorias = await _httpClient.GetFromJsonAsync<List<CategoriaDto>>("api/categoria", cancellationToken);
        return categorias ?? [];
    }

    public async Task<CategoriaDto> CreateCategoriaAsync(CreateCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/categoria", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoriaDto>(cancellationToken))!;
    }

    public async Task<CategoriaDto> UpdateCategoriaAsync(int id, UpdateCategoriaDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/categoria/{id}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoriaDto>(cancellationToken))!;
    }

    public async Task DeleteCategoriaAsync(int id, CancellationToken cancellationToken = default)
    {
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
        var movimientos = await _httpClient.GetFromJsonAsync<List<MovimientoDto>>("api/movimiento", cancellationToken);
        return movimientos ?? [];
    }

    public async Task<MovimientoDto> CreateMovimientoAsync(CreateMovimientoDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/movimiento", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MovimientoDto>(cancellationToken))!;
    }

    public async Task<MovimientoDto> UpdateMovimientoAsync(int id, UpdateMovimientoDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/movimiento/{id}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MovimientoDto>(cancellationToken))!;
    }

    public async Task DeleteMovimientoAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/movimiento/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AnalisisFinancieroDto> GetAnalisisIaAsync(CancellationToken cancellationToken = default)
    {
        var analisis = await _httpClient.GetFromJsonAsync<AnalisisFinancieroDto>("api/ia/analisis", cancellationToken);
        return analisis ?? new AnalisisFinancieroDto();
    }

    public async Task<IReadOnlyCollection<CuentaDto>> GetCuentasAsync(CancellationToken cancellationToken = default)
    {
        var cuentas = await _httpClient.GetFromJsonAsync<List<CuentaDto>>("api/cuenta", cancellationToken);
        return cuentas ?? [];
    }

    public async Task<CuentaDto> CreateCuentaAsync(CreateCuentaDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/cuenta", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CuentaDto>(cancellationToken))!;
    }

    public async Task<CuentaDto> UpdateCuentaAsync(int id, UpdateCuentaDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/cuenta/{id}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CuentaDto>(cancellationToken))!;
    }

    public async Task DeleteCuentaAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/cuenta/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AsistenteRespuestaDto> PreguntarAsistenteAsync(string pregunta, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/asistente/preguntar", new AsistentePreguntaDto { Pregunta = pregunta }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AsistenteRespuestaDto>(cancellationToken))!;
    }

    public async Task<IReadOnlyCollection<SugerenciaDto>> GetSugerenciasAsync(decimal? presupuesto = null, CancellationToken cancellationToken = default)
    {
        var url = presupuesto is > 0
            ? $"api/asistente/sugerencias?presupuesto={presupuesto.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : "api/asistente/sugerencias";
        var sugerencias = await _httpClient.GetFromJsonAsync<List<SugerenciaDto>>(url, cancellationToken);
        return sugerencias ?? [];
    }

    public async Task<IReadOnlyCollection<MensajeLogDto>> GetMensajesProcesadosAsync(int cantidad = 100, CancellationToken cancellationToken = default)
    {
        var mensajes = await _httpClient.GetFromJsonAsync<List<MensajeLogDto>>($"api/mensajes?cantidad={cantidad}", cancellationToken);
        return mensajes ?? [];
    }
}
