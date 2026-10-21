namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Por qué la cuota de Cartera no se descontó entera en la nómina ordinaria (feature 013, T0163; FR-112, G5). Son los textos de
/// <c>MotivoDeNoDescuento</c> del puerto de Cartera (data-model §9.4) que el motor puede decidir; Nómina los guarda tal cual en
/// <c>PAY_PayrollLoanDeductions.NotDeductedReason</c> y los informa al contrato de obligaciones. El motor de Nómina no depende de
/// Cartera: por eso son textos y no el enum del puerto. (nuevo)
/// </summary>
public static class LoanNonDeductionReasons
{
    /// <summary>No cupo en el tope de deducciones autorizadas (<c>MAX_DEDUCCION_SALARIO_PCT</c>).</summary>
    public const string DeductionCap = "DeductionCap";

    /// <summary>El neto del empleado no alcanzaba.</summary>
    public const string InsufficientNet = "InsufficientNet";
}
