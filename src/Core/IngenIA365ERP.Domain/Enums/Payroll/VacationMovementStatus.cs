namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// <c>Registered → Liquidated</c> al aprobar la corrida <c>Vacation</c> (o la definitiva);
/// <c>Liquidated → Registered</c> al reversarla; <c>Registered → Cancelled</c> con motivo.
/// </summary>
public enum VacationMovementStatus
{
    Registered = 0,
    Liquidated = 1,
    Cancelled = 2,
}
