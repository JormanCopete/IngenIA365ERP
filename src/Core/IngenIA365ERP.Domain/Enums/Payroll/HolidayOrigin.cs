namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// De dónde salió un festivo. Los tres primeros los produce la semilla con la regla de la Ley 51
/// de 1983 (fijos, trasladados al lunes, los que dependen de Pascua); <c>Decreed</c> y
/// <c>Manual</c> los registra la cooperativa y la semilla nunca los toca ni los borra.
/// </summary>
public enum HolidayOrigin
{
    Ley51Fixed = 1,
    Ley51MovedToMonday = 2,
    Ley51Easter = 3,
    Decreed = 4,
    Manual = 5,
}
