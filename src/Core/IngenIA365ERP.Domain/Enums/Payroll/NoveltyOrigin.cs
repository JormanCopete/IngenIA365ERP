namespace IngenIA365ERP.Domain.Enums.Payroll;

public enum NoveltyOrigin
{
    Manual = 0,
    Import = 1,
    Recurring = 2,
    Retroactive = 3,
    LoanDeduction = 4,
    CarryOver = 5,

    /// <summary>
    /// Feature 010: la novedad que deja la liquidación de vacaciones en cada período que cubre el
    /// disfrute (<c>PAY_VacationMovements</c>). Sigue la misma regla de anulación y regeneración
    /// que las recurrentes: la anula la reversión o el descarte de la corrida, no una persona.
    /// </summary>
    VacationLeave = 6,
}
