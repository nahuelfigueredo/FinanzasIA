using FinanzasIA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanzasIA.Api.Services;

public interface IUsuarioWhatsAppResolver
{
    Task<string?> ResolverUsuarioIdAsync(string telefono, CancellationToken cancellationToken = default);
}

public class UsuarioWhatsAppResolver : IUsuarioWhatsAppResolver
{
    private readonly FinanzasDbContext _context;
    private readonly ILogger<UsuarioWhatsAppResolver> _logger;

    public UsuarioWhatsAppResolver(FinanzasDbContext context, ILogger<UsuarioWhatsAppResolver> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string?> ResolverUsuarioIdAsync(string telefono, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizado = new string(telefono.Where(char.IsDigit).ToArray());
            return await _context.Database
                .SqlQuery<string?>($"SELECT Id AS [Value] FROM AspNetUsers WHERE TelefonoWhatsApp = {normalizado}")
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // La tabla AspNetUsers puede no existir todavía si el Backoffice nunca corrió.
            _logger.LogWarning(ex, "No se pudo resolver el usuario de WhatsApp para {Telefono}.", telefono);
            return null;
        }
    }
}
