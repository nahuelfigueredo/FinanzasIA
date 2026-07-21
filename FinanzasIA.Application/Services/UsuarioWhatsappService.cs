using System.Security.Cryptography;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Implementación de la vinculación de números de mensajería con usuarios.
/// Genera códigos de verificación de 6 dígitos y los envía por el canal
/// correspondiente mediante <see cref="ICanalMensajeriaSender"/>.
/// </summary>
public class UsuarioWhatsappService : IUsuarioWhatsappService
{
    private readonly IUsuarioWhatsappRepository _repository;
    private readonly IEnumerable<ICanalMensajeriaSender> _senders;
    private readonly ILogger<UsuarioWhatsappService> _logger;

    public UsuarioWhatsappService(
        IUsuarioWhatsappRepository repository,
        IEnumerable<ICanalMensajeriaSender> senders,
        ILogger<UsuarioWhatsappService> logger)
    {
        _repository = repository;
        _senders = senders;
        _logger = logger;
    }

    public async Task<VinculacionResultDto> VincularAsync(VincularNumeroDto dto, string usuarioId, CancellationToken cancellationToken = default)
    {
        var numero = Normalizar(dto.NumeroTelefono);
        if (numero.Length < 8)
        {
            return new VinculacionResultDto { Exito = false, Mensaje = "El número de teléfono no es válido." };
        }

        if (await _repository.ExisteNumeroAsync(numero, dto.Canal, cancellationToken))
        {
            return new VinculacionResultDto { Exito = false, Mensaje = "Ese número ya está vinculado a una cuenta." };
        }

        var vinculo = new UsuarioWhatsapp
        {
            UsuarioId = usuarioId,
            NumeroTelefono = numero,
            Nombre = dto.Nombre,
            Canal = dto.Canal,
            Verificado = false,
            Activo = true,
            CodigoVerificacion = GenerarCodigo(),
            FechaAlta = DateTime.UtcNow
        };

        await _repository.AgregarAsync(vinculo, cancellationToken);
        _logger.LogInformation("Número {Numero} vinculado (pendiente de verificación) al usuario {UsuarioId}.", numero, usuarioId);

        await EnviarCodigoAsync(vinculo, cancellationToken);

        return new VinculacionResultDto
        {
            Exito = true,
            Mensaje = "Número agregado. Te enviamos un código de verificación por WhatsApp.",
            Vinculo = Map(vinculo)
        };
    }

    public async Task<VinculacionResultDto> VerificarAsync(VerificarNumeroDto dto, string usuarioId, CancellationToken cancellationToken = default)
    {
        var vinculo = await ObtenerVinculoDelUsuarioAsync(dto.Id, usuarioId, cancellationToken);
        if (vinculo is null)
        {
            return new VinculacionResultDto { Exito = false, Mensaje = "No se encontró el número a verificar." };
        }

        if (vinculo.Verificado)
        {
            return new VinculacionResultDto { Exito = true, Mensaje = "El número ya estaba verificado.", Vinculo = Map(vinculo) };
        }

        if (string.IsNullOrEmpty(vinculo.CodigoVerificacion) || vinculo.CodigoVerificacion != dto.Codigo.Trim())
        {
            _logger.LogWarning("Código incorrecto para el número {Numero} del usuario {UsuarioId}.", vinculo.NumeroTelefono, usuarioId);
            return new VinculacionResultDto { Exito = false, Mensaje = "El código ingresado es incorrecto." };
        }

        vinculo.Verificado = true;
        vinculo.CodigoVerificacion = null;
        vinculo.FechaVerificacion = DateTime.UtcNow;
        await _repository.ActualizarAsync(vinculo, cancellationToken);

        _logger.LogInformation("Número {Numero} verificado para el usuario {UsuarioId}.", vinculo.NumeroTelefono, usuarioId);
        return new VinculacionResultDto { Exito = true, Mensaje = "✅ Número verificado correctamente.", Vinculo = Map(vinculo) };
    }

    public async Task<VinculacionResultDto> ReenviarCodigoAsync(int id, string usuarioId, CancellationToken cancellationToken = default)
    {
        var vinculo = await ObtenerVinculoDelUsuarioAsync(id, usuarioId, cancellationToken);
        if (vinculo is null)
        {
            return new VinculacionResultDto { Exito = false, Mensaje = "No se encontró el número." };
        }

        if (vinculo.Verificado)
        {
            return new VinculacionResultDto { Exito = true, Mensaje = "El número ya está verificado.", Vinculo = Map(vinculo) };
        }

        vinculo.CodigoVerificacion = GenerarCodigo();
        await _repository.ActualizarAsync(vinculo, cancellationToken);
        await EnviarCodigoAsync(vinculo, cancellationToken);

        return new VinculacionResultDto { Exito = true, Mensaje = "Te reenviamos el código de verificación.", Vinculo = Map(vinculo) };
    }

    public async Task<bool> DesvincularAsync(int id, string usuarioId, CancellationToken cancellationToken = default)
    {
        var vinculo = await ObtenerVinculoDelUsuarioAsync(id, usuarioId, cancellationToken);
        if (vinculo is null)
        {
            return false;
        }

        await _repository.EliminarAsync(vinculo.Id, cancellationToken);
        _logger.LogInformation("Número {Numero} desvinculado del usuario {UsuarioId}.", vinculo.NumeroTelefono, usuarioId);
        return true;
    }

    public async Task<IReadOnlyCollection<UsuarioWhatsappDto>> ObtenerNumerosAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        var vinculos = await _repository.BuscarPorUsuarioAsync(usuarioId, cancellationToken);
        return vinculos.Select(Map).ToList();
    }

    public async Task<string?> BuscarUsuarioPorNumeroAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default)
    {
        var numero = Normalizar(numeroTelefono);
        var vinculo = await _repository.BuscarPorNumeroAsync(numero, canal, cancellationToken);

        if (vinculo is null)
        {
            _logger.LogInformation("Número {Numero} sin usuario vinculado.", numero);
            return null;
        }

        if (!vinculo.Verificado || !vinculo.Activo)
        {
            _logger.LogInformation("Número {Numero} encontrado pero no habilitado (Verificado={Verificado}, Activo={Activo}).",
                numero, vinculo.Verificado, vinculo.Activo);
            return null;
        }

        vinculo.FechaUltimoUso = DateTime.UtcNow;
        await _repository.ActualizarAsync(vinculo, cancellationToken);

        _logger.LogInformation("Número {Numero} identificado como usuario {UsuarioId}.", numero, vinculo.UsuarioId);
        return vinculo.UsuarioId;
    }

    public async Task<bool> NumeroPendienteDeVerificacionAsync(string numeroTelefono, CanalMensajeria canal = CanalMensajeria.WhatsApp, CancellationToken cancellationToken = default)
    {
        var vinculo = await _repository.BuscarPorNumeroAsync(Normalizar(numeroTelefono), canal, cancellationToken);
        return vinculo is not null && (!vinculo.Verificado || !vinculo.Activo);
    }

    private async Task<UsuarioWhatsapp?> ObtenerVinculoDelUsuarioAsync(int id, string usuarioId, CancellationToken cancellationToken)
    {
        var vinculos = await _repository.BuscarPorUsuarioAsync(usuarioId, cancellationToken);
        var dto = vinculos.FirstOrDefault(x => x.Id == id);
        if (dto is null)
        {
            return null;
        }

        // Recuperar la entidad trackeada por número para poder actualizarla.
        return await _repository.BuscarPorNumeroAsync(dto.NumeroTelefono, dto.Canal, cancellationToken);
    }

    private async Task EnviarCodigoAsync(UsuarioWhatsapp vinculo, CancellationToken cancellationToken)
    {
        var sender = _senders.FirstOrDefault(s => s.Canal == vinculo.Canal);
        if (sender is null)
        {
            _logger.LogWarning("No hay sender registrado para el canal {Canal}. Código para {Numero}: {Codigo}",
                vinculo.Canal, vinculo.NumeroTelefono, vinculo.CodigoVerificacion);
            return;
        }

        try
        {
            await sender.EnviarTextoAsync(vinculo.NumeroTelefono,
                $"Tu código de verificación de FinanzasIA es: {vinculo.CodigoVerificacion}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el código de verificación a {Numero}.", vinculo.NumeroTelefono);
        }
    }

    private static string GenerarCodigo() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static string Normalizar(string telefono) => new(telefono.Where(char.IsDigit).ToArray());

    private static UsuarioWhatsappDto Map(UsuarioWhatsapp v) => new()
    {
        Id = v.Id,
        NumeroTelefono = v.NumeroTelefono,
        Nombre = v.Nombre,
        Canal = v.Canal,
        Verificado = v.Verificado,
        Activo = v.Activo,
        FechaAlta = v.FechaAlta,
        FechaVerificacion = v.FechaVerificacion,
        FechaUltimoUso = v.FechaUltimoUso
    };
}
