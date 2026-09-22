namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Estado de una terminación de contrato (R7). Una sola viva por ficha (<c>Registered</c> o
/// <c>Settled</c>, cada una con su índice único filtrado); reversar la definitiva aprobada la
/// deja <c>Reinstated</c> y volver a terminar es otra fila.
/// </summary>
public enum TerminationStatus
{
    Registered = 0,
    Settled = 1,
    Reinstated = 2,
    Cancelled = 3,
}
