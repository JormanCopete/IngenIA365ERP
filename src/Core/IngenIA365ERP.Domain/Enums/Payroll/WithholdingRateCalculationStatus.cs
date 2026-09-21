namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// <c>Calculated → Approved</c> abre la vigencia del porcentaje; <c>→ Superseded</c> por recálculo
/// (versión nueva); <c>→ Rejected</c> con motivo. Un cálculo aprobado no se edita ni se borra.
/// </summary>
public enum WithholdingRateCalculationStatus
{
    Calculated = 0,
    Approved = 1,
    Superseded = 2,
    Rejected = 3,
}
