namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>Bloqueos de aprobación por empleado (FR-019). Se combinan.</summary>
[Flags]
public enum RunEmployeeFlag
{
    None = 0,
    NegativeNet = 1,
    DeductionsOverMax = 2,
    MissingAffiliation = 4,
    ConceptWithoutAccounts = 8,
    WithholdingRateMissing = 16,

    /// <summary>
    /// Feature 010: el empleado ingresó antes del arranque de la nómina en la cooperativa y no
    /// tiene saldo inicial de prestaciones digitado; la liquidación sale corta hasta que lo haya.
    /// </summary>
    OpeningBalanceMissing = 32,

    /// <summary>Feature 010: los descuentos propuestos de la definitiva superan el neto; hay que bajar alguno.</summary>
    DeductionOverNet = 64,
}
