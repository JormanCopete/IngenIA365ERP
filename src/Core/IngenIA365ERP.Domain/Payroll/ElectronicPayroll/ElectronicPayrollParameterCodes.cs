namespace IngenIA365ERP.Domain.Payroll.ElectronicPayroll;

/// <summary>
/// Códigos de los parámetros legales de la nómina electrónica (Res. 013/2021, compilada
/// en la Res. 227/2025; feature 010 US6). Hoy uno solo: el plazo de transmisión, que la
/// norma fija en días del mes siguiente y que la política por empresa
/// <c>DianPlazoComputo</c> decide si se cuenta en calendario o en hábiles. Lista propia
/// del proceso (research R4).
/// </summary>
public static class ElectronicPayrollParameterCodes
{
    /// <summary>Días del mes siguiente dentro de los cuales se transmite el documento (Res. 227/2025 art. 1.5.3.4.1.1).</summary>
    public const string TransmissionDeadlineDays = "DIAN_PLAZO_TRANSMISION_DIAS";

    public static readonly IReadOnlyList<string> Required = [TransmissionDeadlineDays];
}
