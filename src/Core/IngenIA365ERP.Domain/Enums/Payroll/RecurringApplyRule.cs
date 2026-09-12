namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// En qué períodos del mes se genera una novedad recurrente. Un préstamo en una nómina
/// quincenal suele cobrarse entero en la segunda quincena; un auxilio, en la primera. En un
/// plan mensual las tres reglas dan lo mismo.
/// </summary>
public enum RecurringApplyRule
{
    EveryPeriod = 0,
    FirstOfMonth = 1,
    LastOfMonth = 2,
}
