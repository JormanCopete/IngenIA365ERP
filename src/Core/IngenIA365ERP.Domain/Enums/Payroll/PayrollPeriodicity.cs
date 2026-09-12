namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Días del período: la periodicidad del plan decide la base de proporción (FR-008 de la
/// feature 005; decadal y semanal desde la 006). El valor numérico ES la base: el motor no
/// conoce otra cosa que <c>DaysInPeriod</c>.
/// </summary>
public enum PayrollPeriodicity
{
    Monthly = 30,
    Biweekly = 15,
    /// <summary>Tres décadas fijas por mes: 1–10, 11–20 y 21–fin de mes.</summary>
    TenDay = 10,
    /// <summary>Semanas de siete días; la persona dice a qué semana (1–5) y mes se imputa cada una.</summary>
    Weekly = 7,
}
