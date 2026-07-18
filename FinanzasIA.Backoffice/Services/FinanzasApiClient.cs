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
}
