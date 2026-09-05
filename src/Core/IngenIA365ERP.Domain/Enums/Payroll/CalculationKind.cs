namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>Las cinco formas de cálculo predefinidas (spec FR-009). No hay lenguaje de fórmulas libre.</summary>
public enum CalculationKind
{
    FixedAmount = 0,
    PercentOfBase = 1,
    QuantityTimesUnit = 2,
    RangeTable = 3,
    CompositeOfConcepts = 4,
}
