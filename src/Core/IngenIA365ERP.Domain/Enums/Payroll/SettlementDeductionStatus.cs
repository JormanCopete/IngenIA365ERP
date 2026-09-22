namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Ciclo de un descuento de la definitiva: propuesto desde Cartera, ajustado (sólo hacia abajo y
/// con motivo), aplicado al aprobar, revertido al reversar.
/// </summary>
public enum SettlementDeductionStatus
{
    Proposed = 0,
    Adjusted = 1,
    Applied = 2,
    Reverted = 3,
}
