namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Estado del descuento de Cartera de la nómina ordinaria por empleado y obligación (feature 013, T0155; data-model parte 3 §7.1,
/// §9.3, DE-07): <c>Calculated</c> al calcular → <c>Applied</c> | <c>PartiallyApplied</c> | <c>NotApplied</c> | <c>Rejected</c> al
/// aprobar, según lo que respondió el contrato de obligaciones → <c>Reversed</c> al reversar la nómina. (nuevo)
/// </summary>
public enum PayrollLoanDeductionStatus
{
    Calculated = 1,
    Applied = 2,
    PartiallyApplied = 3,
    NotApplied = 4,
    Rejected = 5,
    Reversed = 6,
}
