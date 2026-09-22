using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;

namespace IngenIA365ERP.Domain.Payroll.Withholding;

/// <summary>
/// Códigos de los parámetros legales del cálculo del porcentaje fijo de retención del
/// procedimiento 2 (ET art. 386; feature 010 US7). El divisor del artículo («la prima es
/// el mes trece») es un parámetro, no una constante: aquí sólo vive el código. Lista
/// propia del proceso (research R4).
/// </summary>
public static class WithholdingRateParameterCodes
{
    /// <summary>Divisor de la sumatoria de los doce meses anteriores (ET art. 386).</summary>
    public const string Procedure2Divisor = "RETEFTE_P2_DIVISOR";

    public const string WithholdingTableUvt = LegalParameterCodes.WithholdingTableUvt;
    public const string ExemptIncomePct = LegalParameterCodes.WithholdingExemptIncomePct;
    public const string ExemptIncomeCapUvt = LegalParameterCodes.WithholdingExemptIncomeCapUvt;
    public const string ExemptIncomeAnnualCapUvt = SettlementParameterCodes.WithholdingExemptIncomeAnnualCapUvt;
    public const string DeductionsCapPct = LegalParameterCodes.WithholdingDeductionsCapPct;
    public const string DeductionsCapUvt = LegalParameterCodes.WithholdingDeductionsCapUvt;
    public const string DeductionsAnnualCapUvt = SettlementParameterCodes.WithholdingDeductionsAnnualCapUvt;
    public const string Uvt = LegalParameterCodes.Uvt;

    /// <summary>Sin vigencia de cualquiera de estos al mes del cálculo (junio o diciembre), no se calcula el porcentaje.</summary>
    public static readonly IReadOnlyList<string> Required =
    [
        Procedure2Divisor, WithholdingTableUvt,
        ExemptIncomePct, ExemptIncomeCapUvt, ExemptIncomeAnnualCapUvt,
        DeductionsCapPct, DeductionsCapUvt, DeductionsAnnualCapUvt,
        Uvt,
    ];
}
