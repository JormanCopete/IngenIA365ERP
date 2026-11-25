using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// 2026-11-24 (pedido del dueño; feature 025, fase 5, HU5; FR-016): <b>un solo lector</b> de <c>Causacion.Periodo</c> para una línea a una fecha: el de la
/// línea con caída al general; ilegible o «CicloDeLaCuota» = el ciclo de la cuota, lo de siempre. Lo usan el reconocimiento al inicio del ciclo (ciclo diario y
/// proceso de causación), la caja y la aplicación de pagos, que lo sella en la decisión del pago para que la reconstrucción lo repita. (nuevo)
/// </summary>
public static class PeriodoDeLaCuota
{
    /// <summary>¿La línea causa por período calendario a <paramref name="fecha"/>?</summary>
    public static async Task<bool> CalendarioAsync(ReglasDeParametrosDeCartera parametros, DateOnly fecha, int lineaId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CausacionPeriodo, fecha, null, ParameterScopeKind.CreditLine, lineaId, ct);
        return leido.IsSuccess && string.Equals(leido.Value.Texto?.Trim(), ParametrosDeCartera.PeriodoCalendario, StringComparison.Ordinal);
    }
}
