using System.Globalization;
using FinanzasIA.Application.DTOs;
using FinanzasIA.Application.Interfaces;
using FinanzasIA.Core.Enums;

namespace FinanzasIA.Application.Services;

/// <summary>
/// Ejecuta la acción correspondiente a un mensaje ya interpretado:
/// registra movimientos (resolviendo categoría y cuenta contra la BD) o
/// responde consultas (saldo, resumen, categoría, presupuesto, ayuda).
/// Genera siempre respuestas en lenguaje natural y nunca falla por
/// categoría inexistente: usa "Otros" como fallback.
/// </summary>
public class MessageActionExecutor : IMessageActionExecutor
{
    private const string CategoriaFallback = "Otros";
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    private readonly IMovimientoService _movimientoService;
    private readonly ICategoriaService _categoriaService;
    private readonly ICuentaService _cuentaService;
    private readonly IAnalisisFinancieroService _analisisFinancieroService;

    public MessageActionExecutor(
        IMovimientoService movimientoService,
        ICategoriaService categoriaService,
        ICuentaService cuentaService,
        IAnalisisFinancieroService analisisFinancieroService)
    {
        _movimientoService = movimientoService;
        _categoriaService = categoriaService;
        _cuentaService = cuentaService;
        _analisisFinancieroService = analisisFinancieroService;
    }

    public async Task<MensajeProcesadoResultDto> EjecutarAsync(MensajeInterpretadoDto interpretado, string? usuarioId, CancellationToken cancellationToken = default)
    {
        return interpretado.Intent switch
        {
            MessageIntent.RegistrarGasto or MessageIntent.RegistrarIngreso or MessageIntent.Transferencia
                => await RegistrarMovimientoAsync(interpretado, usuarioId, cancellationToken),
            MessageIntent.ConsultaSaldo => await ResponderSaldoAsync(interpretado, usuarioId, cancellationToken),
            MessageIntent.ResumenMensual => await ResponderResumenAsync(interpretado, usuarioId, cancellationToken),
            MessageIntent.ConsultaCategoria => await ResponderCategoriaAsync(interpretado, usuarioId, cancellationToken),
            MessageIntent.ConsultaPresupuesto => ResponderPresupuesto(interpretado),
            MessageIntent.Ayuda => ResponderAyuda(interpretado),
            MessageIntent.Saludo => ResponderSaludo(interpretado),
            _ => NoManejado(interpretado)
        };
    }

    private async Task<MensajeProcesadoResultDto> RegistrarMovimientoAsync(MensajeInterpretadoDto interpretado, string? usuarioId, CancellationToken cancellationToken)
    {
        if (interpretado.Monto is not > 0)
        {
            return new MensajeProcesadoResultDto
            {
                Intent = interpretado.Intent,
                Manejado = true,
                Exito = false,
                Respuesta = "No pude identificar el monto. 🤔\n\nPodés escribir por ejemplo:\nGasté 12000 en farmacia"
            };
        }

        var tipo = interpretado.TipoMovimiento ?? TipoMovimiento.Egreso;
        var categoria = await ResolverCategoriaAsync(interpretado.Categoria, tipo, usuarioId, cancellationToken);
        var cuenta = await ResolverCuentaAsync(interpretado.Cuenta, usuarioId, cancellationToken);

        var movimiento = await _movimientoService.CreateAsync(new CreateMovimientoDto
        {
            Tipo = tipo,
            CategoriaId = categoria.Id,
            CuentaId = cuenta?.Id,
            Descripcion = interpretado.Descripcion ?? categoria.Nombre,
            Monto = interpretado.Monto.Value,
            Fecha = interpretado.Fecha
        }, usuarioId, cancellationToken);

        if (movimiento is null)
        {
            return new MensajeProcesadoResultDto
            {
                Intent = interpretado.Intent,
                Manejado = true,
                Exito = false,
                Respuesta = "No pude registrar el movimiento. Intentá de nuevo en unos segundos. 🙏"
            };
        }

        var tipoTexto = interpretado.Intent == MessageIntent.Transferencia
            ? "Transferencia"
            : tipo == TipoMovimiento.Ingreso ? "Ingreso" : "Gasto";

        return new MensajeProcesadoResultDto
        {
            Intent = interpretado.Intent,
            Manejado = true,
            Exito = true,
            MovimientoId = movimiento.Id,
            Respuesta =
                "✅ Movimiento registrado correctamente.\n\n" +
                $"Tipo: {tipoTexto}\n" +
                $"Monto: {Moneda(interpretado.Monto.Value)}\n" +
                $"Categoría: {categoria.Nombre}\n" +
                $"Cuenta: {cuenta?.Nombre ?? "Predeterminada"}\n" +
                $"Fecha: {interpretado.Fecha:dd/MM/yyyy}"
        };
    }

    /// <summary>
    /// Busca una categoría existente por coincidencia exacta o parcial.
    /// Si no hay ninguna similar usa (o crea) "Otros". Nunca devuelve error.
    /// </summary>
    private async Task<CategoriaDto> ResolverCategoriaAsync(string? nombre, TipoMovimiento tipo, string? usuarioId, CancellationToken cancellationToken)
    {
        var categorias = await _categoriaService.GetAllAsync(usuarioId, cancellationToken);
        var delTipo = categorias.Where(c => c.TipoMovimiento == tipo).ToList();

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var similar = delTipo.FirstOrDefault(c => c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase))
                ?? delTipo.FirstOrDefault(c =>
                    c.Nombre.Contains(nombre, StringComparison.OrdinalIgnoreCase) ||
                    nombre.Contains(c.Nombre, StringComparison.OrdinalIgnoreCase));

            if (similar is not null)
            {
                return similar;
            }

            // La categoría mencionada no existe: se crea con ese nombre para
            // que el movimiento quede bien clasificado.
            return await _categoriaService.CreateAsync(new CreateCategoriaDto
            {
                Nombre = Capitalizar(nombre),
                TipoMovimiento = tipo
            }, usuarioId, cancellationToken);
        }

        var otros = delTipo.FirstOrDefault(c => c.Nombre.Equals(CategoriaFallback, StringComparison.OrdinalIgnoreCase));
        return otros ?? await _categoriaService.CreateAsync(new CreateCategoriaDto
        {
            Nombre = CategoriaFallback,
            TipoMovimiento = tipo
        }, usuarioId, cancellationToken);
    }

    /// <summary>
    /// Busca la cuenta mencionada por coincidencia parcial. Si no se especificó
    /// o no existe, devuelve null (cuenta predeterminada = sin cuenta asignada).
    /// </summary>
    private async Task<CuentaDto?> ResolverCuentaAsync(string? nombre, string? usuarioId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return null;
        }

        var cuentas = await _cuentaService.GetAllAsync(usuarioId, cancellationToken);
        return cuentas.FirstOrDefault(c => c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase))
            ?? cuentas.FirstOrDefault(c =>
                c.Nombre.Contains(nombre, StringComparison.OrdinalIgnoreCase) ||
                nombre.Contains(c.Nombre, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<MensajeProcesadoResultDto> ResponderSaldoAsync(MensajeInterpretadoDto interpretado, string? usuarioId, CancellationToken cancellationToken)
    {
        var analisis = await _analisisFinancieroService.ObtenerAnalisisAsync(usuarioId, cancellationToken);
        return Exitoso(interpretado,
            $"Tu balance actual es {Moneda(analisis.BalanceNeto)} 💰\n" +
            $"Ingresos: {Moneda(analisis.TotalIngresos)}\n" +
            $"Egresos: {Moneda(analisis.TotalEgresos)}");
    }

    private async Task<MensajeProcesadoResultDto> ResponderResumenAsync(MensajeInterpretadoDto interpretado, string? usuarioId, CancellationToken cancellationToken)
    {
        var analisis = await _analisisFinancieroService.ObtenerAnalisisAsync(usuarioId, cancellationToken);
        return Exitoso(interpretado,
            "Resumen FinanzasIA 🧾\n" +
            $"Ingresos: {Moneda(analisis.TotalIngresos)}\n" +
            $"Egresos: {Moneda(analisis.TotalEgresos)}\n" +
            $"Balance: {Moneda(analisis.BalanceNeto)}\n" +
            $"Tasa de ahorro: {analisis.TasaAhorroPorcentaje:0.##}%\n" +
            $"Mayor gasto: {analisis.CategoriaMayorGasto}\n" +
            $"Proyección próximo mes: {Moneda(analisis.ProyeccionBalanceProximoMes)}");
    }

    private async Task<MensajeProcesadoResultDto> ResponderCategoriaAsync(MensajeInterpretadoDto interpretado, string? usuarioId, CancellationToken cancellationToken)
    {
        var analisis = await _analisisFinancieroService.ObtenerAnalisisAsync(usuarioId, cancellationToken);
        return Exitoso(interpretado,
            $"Tu categoría con más gastos es \"{analisis.CategoriaMayorGasto}\" 🎯\n" +
            $"Total de egresos: {Moneda(analisis.TotalEgresos)}");
    }

    private static MensajeProcesadoResultDto ResponderPresupuesto(MensajeInterpretadoDto interpretado) =>
        Exitoso(interpretado, "Podés ver y ajustar tu presupuesto mensual en la sección Presupuestos de FinanzasIA. 🎯");

    private static MensajeProcesadoResultDto ResponderAyuda(MensajeInterpretadoDto interpretado) =>
        Exitoso(interpretado,
            "Puedo ayudarte con: 💡\n" +
            "- Gasté 12000 en farmacia\n" +
            "- Compré nafta por 18000 con Mercado Pago\n" +
            "- Cobré sueldo 1500000\n" +
            "- Transferí 30000 a Banco Nación\n" +
            "- saldo / resumen / presupuesto");

    private static MensajeProcesadoResultDto ResponderSaludo(MensajeInterpretadoDto interpretado) =>
        Exitoso(interpretado, "Hola 👋 Soy FinanzasIA. Mandame algo como \"Gasté 12000 en farmacia\" o escribí \"resumen\" para ver tu análisis.");

    private static MensajeProcesadoResultDto NoManejado(MensajeInterpretadoDto interpretado) => new()
    {
        Intent = interpretado.Intent,
        Manejado = false,
        Exito = false,
        Respuesta = "No entendí tu mensaje. Escribí \"ayuda\" o mandame algo como \"Gasté 12000 en farmacia\"."
    };

    private static MensajeProcesadoResultDto Exitoso(MensajeInterpretadoDto interpretado, string respuesta) => new()
    {
        Intent = interpretado.Intent,
        Manejado = true,
        Exito = true,
        Respuesta = respuesta
    };

    private static string Moneda(decimal valor) => valor.ToString("C", Cultura);

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
