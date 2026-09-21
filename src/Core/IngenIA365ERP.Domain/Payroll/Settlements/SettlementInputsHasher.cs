using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using IngenIA365ERP.Domain.Enums.Payroll;
namespace IngenIA365ERP.Domain.Payroll.Settlements;

/// <summary>
/// SHA-256 de la entrada de una liquidación especial, normalizada a mano como
/// <see cref="Calculation.InputsHasher"/>: mismo insumo, mismo hash, mismo resultado
/// (FR-014 de la 005 aplicada a la 010). Las colecciones se ordenan antes de entrar para
/// que el orden de lectura no cambie el hash.
/// </summary>
public static class SettlementInputsHasher
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Compute(SettlementInput input)
    {
        var sb = new StringBuilder(4096);
        sb.Append("settlement-v2|");
        Add(sb, "kind", (int)input.Kind, input.CutoffDate, input.PeriodStart);
        var pol = input.Policies;
        Add(sb, "policy", (int)pol.Rounding, (int)pol.SemanaLaboral, pol.VacacionesPagoAnticipado, (int)pol.RetefteTopesAnualesModo, pol.PayrollStartDate);

        var e = input.Employee;
        Add(sb, "emp", e.PublicId, (int)e.Class, e.JoinDate, e.TerminationDate, e.ApprenticeStage is { } st ? (int)st : null, e.ApprenticeStageFrom,
            e.TransportAllowanceEntitled, e.WithholdingProcedure, e.WithholdingRatePercent, (int)e.ContractType, e.ContractEndDate,
            e.Affiliations.Health, e.Affiliations.Pension, e.Affiliations.WorkRiskClass, e.Affiliations.FamilyCompensation);
        foreach (var s in e.SalaryHistory.OrderBy(s => s.EffectiveDate).ThenBy(s => s.MonthlySalary))
            Add(sb, "sal", s.EffectiveDate, s.MonthlySalary);
        foreach (var d in e.TaxDeductions.OrderBy(d => (int)d.Kind).ThenBy(d => d.MonthlyAmount).ThenBy(d => d.Percent))
            Add(sb, "tax", (int)d.Kind, d.MonthlyAmount, d.Percent);

        foreach (var a in input.Absences.OrderBy(a => a.From).ThenBy(a => a.To))
            Add(sb, "abs", a.From, a.To, a.IsSuspension);
        if (input.OpeningBalance is { } ob)
            Add(sb, "bal", ob.AsOfDate, ob.PendingVacationDays, ob.AccruedSeverance, ob.AccruedSeveranceInterest, ob.AccruedServiceBonus, ob.ServiceBonusDaysAccrued, ob.SeveranceDaysAccrued);
        foreach (var m in input.VacationMovements.OrderBy(m => m.StartDate).ThenBy(m => (int)m.Kind).ThenBy(m => m.BusinessDays))
            Add(sb, "vac", (int)m.Kind, m.StartDate, m.EndDate, m.BusinessDays, m.CalendarDays, m.IsCancelled);
        if (input.MovementToSettle is { } mv)
            Add(sb, "mov", (int)mv.Kind, mv.StartDate, mv.EndDate, mv.BusinessDays, mv.CalendarDays);
        foreach (var b in input.MonthlyBases.OrderBy(b => b.Year).ThenBy(b => b.Month))
            Add(sb, "mes", b.Year, b.Month, b.VariableBenefitsEarnings, b.VariableVacationEarnings, b.LaborIncome);
        foreach (var p in input.Provisions.OrderBy(p => p.ProvisionConceptCode, StringComparer.Ordinal))
            Add(sb, "prov", p.ProvisionConceptCode.ToUpperInvariant(), p.Accrued);
        if (input.Termination is { } t)
            Add(sb, "term", t.ReasonCode.ToUpperInvariant(), t.GeneratesSeverancePay, t.VoluntaryRetirementBonus);
        foreach (var d in input.ProposedDeductions.OrderBy(d => d.ConceptCode, StringComparer.Ordinal).ThenBy(d => d.Description, StringComparer.Ordinal))
            Add(sb, "ded", d.ConceptCode.ToUpperInvariant(), d.Description, d.ProposedAmount, d.AppliedAmount, d.AccountedByOtherModule);
        foreach (var p in input.ServiceBonusPaidInSettlements.OrderBy(p => p.PaidThrough).ThenBy(p => p.RunPublicId))
            Add(sb, "paid", p.RunPublicId, p.PaidThrough, p.Amount, p.Days, (int)p.PaidBy);
        foreach (var p in input.SeverancePaidInRuns.OrderBy(p => p.PaidThrough).ThenBy(p => p.RunPublicId))
            Add(sb, "sevpaid", p.RunPublicId, p.PaidThrough, p.Amount);
        if (input.PendingSalary is { } ps)
            Add(sb, "pend", ps.PeriodStart, ps.PeriodEnd);
        foreach (var n in input.PendingNovelties.OrderBy(n => n.PublicId))
            Add(sb, "nov", n.PublicId, n.ConceptCode.ToUpperInvariant(), n.Quantity, n.Amount, n.StartDate, n.EndDate, (int)n.Origin);
        if (input.WithholdingYearToDate is { } ytd)
            Add(sb, "ytd", ytd.RentaExentaUsada, ytd.DeduccionesYExentasUsadas);

        foreach (var c in input.Concepts.Where(c => !c.IsDeleted).OrderBy(c => c.Code, StringComparer.Ordinal).ThenBy(c => c.ValidFrom))
            Add(sb, "con", c.Code, c.ValidFrom, c.ValidTo, c.IsActive, (int)c.Nature, (int)c.CalculationKind, c.PercentParameterCode, c.Percent,
                c.TableParameterCode, c.AffectsContributionBase, c.AffectsBenefitsBase, c.AffectsWithholdingBase, c.ApplicableClasses);

        foreach (var p in input.Parameters.Where(p => !p.IsDeleted).OrderBy(p => p.Code, StringComparer.Ordinal).ThenBy(p => p.ValidFrom))
        {
            Add(sb, "par", p.Code, p.ValidFrom, p.ValidTo, (int)p.Kind, p.Value, p.RangeUnitParameterCode, p.RangeIsMarginal);
            foreach (var r in p.Ranges.Where(r => !r.IsDeleted).OrderBy(r => r.Order).ThenBy(r => r.FromValue))
                Add(sb, "rng", r.FromValue, r.ToValue, r.Rate, r.FixedValue);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
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
