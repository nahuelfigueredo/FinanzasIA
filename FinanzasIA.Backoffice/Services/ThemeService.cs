using Microsoft.JSInterop;

namespace FinanzasIA.Backoffice.Services;

/// <summary>
/// Servicio global de temas de la aplicación (claro/oscuro).
/// Administra el tema actual, notifica cambios instantáneos a todos los
/// componentes suscritos y persiste la preferencia en localStorage.
/// </summary>
public sealed class ThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Tema actual: "dark" o "light".</summary>
    public string CurrentTheme { get; private set; } = "light";

    public bool IsDark => CurrentTheme == "dark";

    /// <summary>Se dispara cuando cambia el tema, para que los componentes se re-rendericen.</summary>
    public event Action? OnChange;

    /// <summary>Lee el tema persistido (o preferencia del sistema) y lo aplica.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            var isDark = await _jsRuntime.InvokeAsync<bool>("finanzasTheme.initialize");
            CurrentTheme = isDark ? "dark" : "light";
            _initialized = true;
            OnChange?.Invoke();
        }
        catch
        {
            // prerender: el JS todavía no está disponible; se reintenta después
        }
    }

    /// <summary>Aplica el tema indicado ("dark" o "light") de forma instantánea y lo persiste.</summary>
    public async Task SetThemeAsync(string theme)
    {
        theme = theme == "dark" ? "dark" : "light";
        try
        {
            await _jsRuntime.InvokeAsync<bool>("finanzasTheme.set", theme);
            CurrentTheme = theme;
            OnChange?.Invoke();
        }
        catch
        {
            // sin JS disponible no se puede aplicar; se ignora
        }
    }

    /// <summary>Alterna entre claro y oscuro.</summary>
    public Task ToggleAsync() => SetThemeAsync(IsDark ? "light" : "dark");
}
