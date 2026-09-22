namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Si el empleado está en el régimen de transición de la Ley 2381 de 2024. Decide qué tabla
/// del fondo de solidaridad aplica a partir del 2027-04-01. <c>Unknown</c> es lo que traen
/// todas las fichas hasta que alguien lo diga, y desde esa fecha es alerta en la PILA.
/// </summary>
public enum PensionTransitionRegime
{
    Unknown = 0,
    Yes = 1,
    No = 2,
}
