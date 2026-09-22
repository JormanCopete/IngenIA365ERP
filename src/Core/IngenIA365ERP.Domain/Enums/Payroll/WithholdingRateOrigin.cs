namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Cómo nació una vigencia del porcentaje fijo del procedimiento 2: digitada, o abierta por la
/// aprobación de un cálculo (<c>PAY_WithholdingRateCalculations</c>, R8). Default 0 en la base:
/// las vigencias anteriores a la feature 010 fueron todas manuales.
/// </summary>
public enum WithholdingRateOrigin
{
    Manual = 0,
    Calculated = 1,
}
