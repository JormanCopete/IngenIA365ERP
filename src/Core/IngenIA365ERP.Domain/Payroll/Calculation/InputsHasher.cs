using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// SHA-256 de la entrada normalizada (FR-014): mismo insumo, mismo hash, mismo
/// resultado. Se construye a mano y no serializando objetos, para que ni el orden de
/// las colecciones ni las navegaciones de EF cambien el resultado. Conceptos y
/// parámetros se ordenan por código; las novedades, por identificador.
/// </summary>
public static class InputsHasher
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Compute(CalculationInput input)
    {
        var sb = new StringBuilder(4096);
        sb.Append("v1|");
        Add(sb, "period", input.Period.StartDate, input.Period.EndDate, (int)input.Period.Periodicity);
        Add(sb, "policy", (int)input.Policies.Rounding, input.Policies.ApplyEmployerExemption);

        var e = input.Employee;
        Add(sb, "emp", e.PublicId, (int)e.Class, e.JoinDate, e.TerminationDate, e.WithholdingProcedure, e.WithholdingRatePercent,
            e.Affiliations.Health, e.Affiliations.Pension, e.Affiliations.WorkRiskClass, e.Affiliations.FamilyCompensation);
        foreach (var s in e.SalaryHistory.OrderBy(s => s.EffectiveDate).ThenBy(s => s.MonthlySalary))
            Add(sb, "sal", s.EffectiveDate, s.MonthlySalary);
        foreach (var d in e.TaxDeductions.OrderBy(d => (int)d.Kind).ThenBy(d => d.MonthlyAmount).ThenBy(d => d.Percent))
            Add(sb, "tax", (int)d.Kind, d.MonthlyAmount, d.Percent);

        foreach (var n in input.Novelties.OrderBy(n => n.PublicId))
            Add(sb, "nov", n.PublicId, n.ConceptCode.ToUpperInvariant(), n.Quantity, n.Amount, n.StartDate, n.EndDate, (int)n.Origin);

        foreach (var c in input.Concepts.Where(c => !c.IsDeleted).OrderBy(c => c.Code, StringComparer.Ordinal).ThenBy(c => c.ValidFrom))
            Add(sb, "con", c.Code, c.ValidFrom, c.ValidTo, c.IsActive, (int)c.Nature, (int)c.CalculationKind, c.FixedAmount,
                c.AmountParameterCode, c.ProrateByDays, c.BaseKind is { } bk ? (int)bk : null, c.Percent, c.PercentParameterCode,
                c.UnitKind is { } uk ? (int)uk : null, c.UnitFactor, c.TableParameterCode, c.ComponentConceptCodes,
                c.AffectsSalaryBase, c.AffectsContributionBase, c.AffectsBenefitsBase, c.AffectsWithholdingBase,
                c.IsBenefitRelated, c.MaxQuantity, c.MaxAmount, c.ApplicableClasses, c.IsAutomatic, c.ReducesWorkedDays);

        foreach (var p in input.Parameters.Where(p => !p.IsDeleted).OrderBy(p => p.Code, StringComparer.Ordinal).ThenBy(p => p.ValidFrom))
        {
            Add(sb, "par", p.Code, p.ValidFrom, p.ValidTo, (int)p.Kind, p.Value, p.RangeUnitParameterCode, p.RangeIsMarginal);
            foreach (var r in p.Ranges.Where(r => !r.IsDeleted).OrderBy(r => r.Order).ThenBy(r => r.FromValue))
                Add(sb, "rng", r.FromValue, r.ToValue, r.Rate, r.FixedValue);
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>Hash de la corrida completa: los hashes por empleado, ordenados.</summary>
    public static string Combine(IEnumerable<string> employeeHashes)
    {
        var joined = string.Join("|", employeeHashes.OrderBy(h => h, StringComparer.Ordinal));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("run-v1|" + joined)));
    }

    private static void Add(StringBuilder sb, string tag, params object?[] values)
    {
        sb.Append(tag).Append(':');
        foreach (var v in values)
        {
            sb.Append(v switch
            {
                null => "~",
                DateTime d => d.ToString("yyyy-MM-dd", Inv),
                decimal m => m.ToString("0.############", Inv),
                bool b => b ? "1" : "0",
                Guid g => g.ToString("N"),
                IFormattable f => f.ToString(null, Inv),
                _ => v.ToString(),
            }).Append(',');
        }
        sb.Append(';');
    }
}
