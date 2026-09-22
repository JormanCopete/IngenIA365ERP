using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Descuentos de la definitiva ya propuestos desde Cartera y libranzas y validados por la
/// responsable (FR-018a): el motor sólo los vuelve líneas con el valor aplicado, dice cuánto
/// se propuso y por qué bajó, y marca <see cref="SettlementFlags.DeductionOverNet"/> cuando
/// la suma supera el neto antes de descuentos. Los que contabiliza otro módulo (Cartera,
/// D-08) no generan asiento.
/// </summary>
public static class ProposedDeductionsRule
{
    public static void Evaluate(SettlementContext ctx)
    {
        var propuestas = ctx.Input.ProposedDeductions.Where(d => d.AppliedAmount > 0m).ToList();
        if (propuestas.Count == 0) return;

        var devengos = ctx.Lines.Where(l => l.Nature == ConceptNature.Earning).Sum(l => l.Amount);
        var deducciones = ctx.Lines.Where(l => l.Nature == ConceptNature.Deduction).Sum(l => l.Amount);
        var netoAntes = devengos - deducciones;
        var aplicado = 0m;

        foreach (var d in propuestas)
        {
            var def = ctx.Concept(d.ConceptCode);
            if (def is null) continue;
            var exp = new Explanation { Form = "Descuento al retiro" };
            exp.Note("Obligación", d.Description);
            exp.Step("Propuesto (saldo total o cuotas causadas, según la política)", d.ProposedAmount);
            if (d.AppliedAmount < d.ProposedAmount) exp.Step("Aplicado tras la validación de la responsable (bajado con motivo, auditado)", d.AppliedAmount);
            else exp.Step("Aplicado", d.AppliedAmount);
            if (d.AccountedByOtherModule)
                exp.Note("Origen", "Descuento aplicado y contabilizado por Cartera: se muestra para el neto y el comprobante, no genera asiento.");
            exp.Summary = $"{d.Description}: {Fmt.Money(d.AppliedAmount)}";
            ctx.Add(LineFactory.Create(def, d.AppliedAmount, exp, affectsAccounting: !d.AccountedByOtherModule));
            aplicado += d.AppliedAmount;
        }

        if (aplicado > netoAntes)
        {
            ctx.Flags |= SettlementFlags.DeductionOverNet;
            ctx.Warn($"Los descuentos aplicados ({Fmt.Money(aplicado)}) superan el neto antes de descuentos ({Fmt.Money(netoAntes)}): baje alguno con motivo antes de aprobar.");
        }
    }
}
