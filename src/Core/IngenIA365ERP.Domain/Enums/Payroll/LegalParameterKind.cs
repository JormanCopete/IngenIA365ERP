namespace IngenIA365ERP.Domain.Enums.Payroll;

public enum LegalParameterKind
{
    Amount = 0,
    Percent = 1,
    RangeTable = 2,

    /// <summary>
    /// Feature 010 (D-07): una fecha del año sin año, guardada en <c>Value</c> como <c>MMDD</c>
    /// (630 = 30 de junio, 1220 = 20 de diciembre). Son las fechas límite legales de la prima, la
    /// consignación de cesantías y los intereses; sirven sólo para avisos de calendario y las lee
    /// <c>DateInYear</c>. Se eligió no sumar una columna a <c>PAY_LegalParameters</c>.
    /// </summary>
    DateInYear = 3,
}
