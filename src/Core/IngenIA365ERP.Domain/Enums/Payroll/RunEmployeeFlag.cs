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
}
