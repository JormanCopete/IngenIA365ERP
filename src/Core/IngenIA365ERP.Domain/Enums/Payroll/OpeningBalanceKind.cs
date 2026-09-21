namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Fila de saldo inicial de prestaciones (R3): la digitación de arranque, o un ajuste con motivo
/// cuando la fila original ya la consumió una liquidación aprobada y no se puede editar.
/// </summary>
public enum OpeningBalanceKind
{
    Opening = 1,
    Adjustment = 2,
}
