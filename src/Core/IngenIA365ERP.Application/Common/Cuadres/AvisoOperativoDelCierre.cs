using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;

namespace IngenIA365ERP.Application.Common.Cuadres;

/// <summary>
/// Feature 015, T1 (fase 35, T0422; FR-132; D29; E-04): lo común del aviso operativo del cierre del mes contable —conciliaciones de Tesorería abiertas y sesiones de caja de
/// Recaudo sin cerrar—. Por defecto sólo avisa (Contabilidad lo deja reconocer); bloquea si la cooperativa pone <c>Contabilidad.CierreExigeConciliaciones</c> (que vive en el
/// módulo <c>TES</c>, DT §2.7) a la fecha de fin del mes. Lo leen los dos módulos por el único lector de parámetros. (nuevo)
/// </summary>
public static class AvisoOperativoDelCierre
{
    public const string CodigoConciliacionesAbiertas = "Accounting.Period.ReconciliationsOpen";
    public const string CodigoSesionesSinCerrar = "Accounting.Period.CashSessionsOpen";

    /// <summary>Si el aviso bloquea el cierre del mes: <c>Contabilidad.CierreExigeConciliaciones</c> vigente al último día del mes; sin lector o sin leer, no bloquea.</summary>
    public static async Task<bool> BloqueaAsync(ILectorDeParametros? lector, int anio, int mes, CancellationToken ct)
    {
        if (lector is null) return false;
        var fin = new DateOnly(anio, mes, 1).AddMonths(1).AddDays(-1);
        var leido = await lector.LeerAsync(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, ParametrosDeRecaudoYTesoreria.ContabilidadCierreExigeConciliaciones, fin, ct: ct);
        return leido.IsSuccess && leido.Value.Valor is true;
    }
}
