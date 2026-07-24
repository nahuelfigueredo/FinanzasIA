using FinanzasIA.Application.DTOs;

namespace FinanzasIA.Application.Interfaces;

public interface IPresupuestoService
{
    Task<IReadOnlyCollection<PresupuestoDto>> GetAllAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<PresupuestoDto?> GetByIdAsync(int id, string usuarioId, CancellationToken cancellationToken = default);
    Task<PresupuestoDto> CreateAsync(CreatePresupuestoDto dto, string usuarioId, CancellationToken cancellationToken = default);
    Task<PresupuestoDto?> UpdateAsync(int id, UpdatePresupuestoDto dto, string usuarioId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Crea el presupuesto del mes indicado o actualiza su monto si ya existe.</summary>
    Task<PresupuestoDto> CrearOActualizarAsync(int categoriaId, decimal montoMensual, int mes, int año, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene el presupuesto vigente de una categoría para el mes/año dados (null si no hay).</summary>
    Task<PresupuestoDto?> GetVigenteAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula el estado del presupuesto vigente de una categoría:
    /// gasto acumulado del mes, porcentaje utilizado, saldo restante y si fue superado.
    /// Devuelve null si no hay presupuesto activo para esa categoría/mes.
    /// </summary>
    Task<PresupuestoEstadoDto?> GetEstadoAsync(string usuarioId, int categoriaId, int mes, int año, CancellationToken cancellationToken = default);

    /// <summary>Estado de todos los presupuestos activos del usuario para el mes/año dados.</summary>
    Task<IReadOnlyCollection<PresupuestoEstadoDto>> GetEstadosDelMesAsync(string usuarioId, int mes, int año, CancellationToken cancellationToken = default);
}
