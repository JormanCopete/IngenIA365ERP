using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting.Integration;

/// <summary>
/// <c>ACC_OwnBankAccountLinks</c> (feature 015, US10, T0137; E-04; R-22; FR-102; data-model §17.3): el vínculo <b>único</b> entre una cuenta bancaria propia de
/// Tesorería (por su <see cref="OwnBankAccountPublicId"/>, sin FK: Contabilidad no conoce las tablas <c>TES_</c>) y la cuenta contable bancaria del plan que la
/// representa (<see cref="ChartOfAccountId"/>, que tiene banco: <c>EsBancaria</c>). Único vigente en los dos sentidos
/// (<c>Accounting.OwnBankAccount.AlreadyLinked</c>). Lo escribe sólo <c>IContabilidadParaTesoreria.VincularCuentaBancariaAsync</c>; resuelve el rol <c>Banco</c>
/// y <c>BancoRecaudo</c> de las dos matrices. Un cambio cierra la vigencia la víspera y abre otra. (nuevo)
/// </summary>
public class OwnBankAccountLink : AuditableEntity
{
    public Guid OwnBankAccountPublicId { get; set; }

    public int ChartOfAccountId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }
}
