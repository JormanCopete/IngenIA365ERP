namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Tipo de corrida (feature 010, R2). Las liquidaciones especiales —prima, cesantías e
/// intereses, vacaciones y definitiva— viven en las mismas tablas de corrida que la nómina
/// ordinaria para heredar sin reescribir la relación de pago, la marca de pagado, los
/// comprobantes, la exportación y la reversión. El período sólo existe en la ordinaria:
/// <c>Kind = Ordinary ⇔ PayPeriodId != null</c>. El valor 0 va como default <b>en la base</b>
/// para que toda corrida anterior a esta feature quede <c>Ordinary</c> sin migración de datos.
/// </summary>
public enum PayrollRunKind
{
    Ordinary = 0,
    ServiceBonus = 1,
    Severance = 2,
    Vacation = 3,
    Settlement = 4,
}
