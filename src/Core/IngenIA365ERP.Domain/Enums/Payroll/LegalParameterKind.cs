namespace IngenIA365ERP.Domain.Enums.Payroll;

public enum LegalParameterKind
{
    Amount = 0,
    Percent = 1,
    RangeTable = 2,

    /// <summary>
    /// Feature 010 (D-07): una fecha del año sin año, guardada en <c>Value</c> como <c>MMDD</c>
    /// (0630, 1220, 0131, 0214). Son las fechas límite legales de prima, cesantías e intereses,
    /// y sirven sólo para el aviso del calendario: ningún cálculo las lee como monto.
    /// </summary>
    DateInYear = 3,
}
