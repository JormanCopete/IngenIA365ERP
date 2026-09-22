namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Movimientos de vacaciones (R6). La causación no es un movimiento: se deriva de los días
/// trabajados y el parámetro de días por año. Lo que sí se registra es lo que consume saldo:
/// el disfrute, la compensación en dinero, un ajuste con signo y lo pagado al retiro.
/// </summary>
public enum VacationMovementKind
{
    Enjoyment = 1,
    Compensation = 2,
    Adjustment = 3,
    SettlementPayout = 4,
}
