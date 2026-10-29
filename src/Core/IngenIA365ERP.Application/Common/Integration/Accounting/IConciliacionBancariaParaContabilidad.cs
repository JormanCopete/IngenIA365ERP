namespace IngenIA365ERP.Application.Common.Integration.Accounting;

/// <summary>
/// Ofrece Tesorería → usa Contabilidad (feature 015, US25, T0278a; E-04; TR41; FR-167, FR-216; contracts/interfaces.md §3): la conciliación bancaria es de Tesorería, y
/// cuando Contabilidad reabre un período las conciliaciones cerradas de ese mes quedan <c>Outdated</c> por aquí, con su evento de auditoría, en la unidad de trabajo de
/// quien llama y sin guardar. Sin Tesorería activada (sin conciliaciones) devuelve cero. Antes <c>ReopenPeriodCommand</c> marcaba <c>ACC_BankReconciliations</c>, que
/// quedan sin uso (<c>NadieEscribeLaConciliacionBancariaDeContabilidad</c>). (nuevo, DT §2.7)
/// </summary>
public interface IConciliacionBancariaParaContabilidad
{
    /// <summary>Marca desactualizadas las conciliaciones cerradas del mes y devuelve cuántas.</summary>
    Task<int> MarcarDesactualizadasAsync(int anio, int mes, string motivo, CancellationToken ct);
}
