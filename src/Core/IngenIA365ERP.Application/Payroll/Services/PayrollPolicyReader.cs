using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Las decisiones de la cooperativa que el motor y la aprobación consultan (D-09):
/// <c>Payroll.Rounding</c>, <c>Payroll.VariationThresholdPercent</c>,
/// <c>Payroll.AllowSameUserApproval</c>, <c>Payroll.ApplyEmployerExemption</c>. Viven en
/// <c>COR_SystemSettings</c> con prefijo <c>PAY</c>; las siembra <c>SystemParametersSeeder</c>
/// y se editan en Parámetros del sistema. Si una falta, rige el valor por defecto de la
/// semilla, nunca un error: son políticas, no valores legales.
/// </summary>
public sealed class PayrollPolicyReader(IApplicationDbContext db)
{
    public const string RoundingKey = "Payroll.Rounding";
    public const string VariationThresholdKey = "Payroll.VariationThresholdPercent";
    public const string AllowSameUserApprovalKey = "Payroll.AllowSameUserApproval";
    public const string ApplyEmployerExemptionKey = "Payroll.ApplyEmployerExemption";

    /// <summary>Umbral por defecto del comparativo (D-09); es politica de la cooperativa, no un valor legal.</summary>
    private const decimal DefaultVariationThresholdPercent = 10;

    public sealed record PayrollPolicies(
        PayrollRounding Rounding,
        decimal VariationThresholdPercent,
        bool AllowSameUserApproval,
        bool ApplyEmployerExemption)
    {
        public CalculationPolicies ForCalculation() => new() { Rounding = Rounding, ApplyEmployerExemption = ApplyEmployerExemption };
    }

    public async Task<PayrollPolicies> ReadAsync(CancellationToken ct)
    {
        var filas = await db.SystemSettings.AsNoTracking()
            .Where(s => s.ModulePrefix == "PAY")
            .Select(s => new { s.SettingKey, s.SettingValue })
            .ToListAsync(ct);
        var mapa = filas.ToDictionary(f => f.SettingKey, f => f.SettingValue, StringComparer.OrdinalIgnoreCase);

        var redondeo = mapa.TryGetValue(RoundingKey, out var r) && Enum.TryParse<PayrollRounding>(r, true, out var pr)
            ? pr : PayrollRounding.Peso;
        var umbral = mapa.TryGetValue(VariationThresholdKey, out var u)
                     && decimal.TryParse(u, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d : DefaultVariationThresholdPercent;
        var mismoUsuario = mapa.TryGetValue(AllowSameUserApprovalKey, out var a) && bool.TryParse(a, out var ba) && ba;
        var exoneracion = mapa.TryGetValue(ApplyEmployerExemptionKey, out var e) && bool.TryParse(e, out var be) && be;

        return new PayrollPolicies(redondeo, umbral, mismoUsuario, exoneracion);
    }
}
