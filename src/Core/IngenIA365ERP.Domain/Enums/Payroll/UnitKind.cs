namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>Unidad de la forma «cantidad × unidad»: hora ordinaria (salario/HORAS_MES), día (salario/30), hora con recargo (factor).</summary>
public enum UnitKind
{
    OrdinaryHour = 0,
    Day = 1,
    HourWithSurcharge = 2,
}
