using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Entities;
using FinanzasIA.Core.Enums;
using FinanzasIA.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Implementación de la vinculación de números de mensajería con usuarios.
/// El número queda asociado directamente al usuario autenticado al vincularlo.
/// </summary>
public class UsuarioWhatsappService : IUsuarioWhatsappService
{
	private readonly IUsuarioWhatsappRepository _repository;
	private readonly ILogger<UsuarioWhatsappService> _logger;

	public UsuarioWhatsappService(
		IUsuarioWhatsappRepository repository,
		ILogger<UsuarioWhatsappService> logger)
	{
		_repository = repository;
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
			Activo = true,
			FechaAlta = DateTime.UtcNow
		};

		await _repository.AgregarAsync(vinculo, cancellationToken);
		_logger.LogInformation("Número {Numero} vinculado al usuario {UsuarioId}.", numero, usuarioId);

		return new VinculacionResultDto
		{
			Exito = true,
			Mensaje = "✅ Número vinculado correctamente.",
			Vinculo = Map(vinculo)
		};
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

		if (!vinculo.Activo)
		{
			_logger.LogInformation("Número {Numero} encontrado pero inactivo.", numero);
			return null;
		}

		vinculo.FechaUltimoUso = DateTime.UtcNow;
		await _repository.ActualizarAsync(vinculo, cancellationToken);

		_logger.LogInformation("Número {Numero} identificado como usuario {UsuarioId}.", numero, vinculo.UsuarioId);
		return vinculo.UsuarioId;
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

	private static string Normalizar(string telefono) => new(telefono.Where(char.IsDigit).ToArray());

	private static UsuarioWhatsappDto Map(UsuarioWhatsapp v) => new()
	{
		Id = v.Id,
		NumeroTelefono = v.NumeroTelefono,
		Nombre = v.Nombre,
		Canal = v.Canal,
		Activo = v.Activo,
		FechaAlta = v.FechaAlta,
		FechaUltimoUso = v.FechaUltimoUso
	};
}
