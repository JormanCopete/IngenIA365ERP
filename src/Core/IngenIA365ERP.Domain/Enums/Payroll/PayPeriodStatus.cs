namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Estado del período de pago. El valor 1 era «liquidado» para el cálculo preliminar
/// retirado; ninguna corrida real lo usó, así que se reinterpreta como «calculado (borrador)».
/// </summary>
public enum PayPeriodStatus
{
    Open = 0,
    Calculated = 1,
    Approved = 2,
    Reversed = 3,
}
