using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// El saldo inicial de prestaciones como «tramo inicial» del cálculo (research R3,
/// FR-007): lo digitado a la fecha de arranque se suma a los acumulados de valor y días, la
/// ventana proporcional arranca el día siguiente y la explicación lo dice con nombre y
/// fecha («Saldo inicial al 30/11/2026 digitado por … el …»). Cuando el ingreso es
/// anterior al arranque de la nómina y no hay saldo, la liquidación avisa: sin él la prima
/// de diciembre y las cesantías del año saldrían cortas (edge case de la spec).
/// </summary>
public static class OpeningBalanceStep
{
    /// <summary>Lo que el saldo aporta a una ventana: valor, días ya contados y desde dónde sigue la proporción.</summary>
    public sealed record Tramo(decimal Amount, int Days, DateTime WindowStart, string? Text)
    {
        public bool Applies => Text is not null;
    }

    public static Tramo Ninguno(DateTime windowStart) => new(0m, 0, windowStart, null);

    /// <summary>
    /// Aplica el saldo a una ventana [<paramref name="windowStart"/>, corte]: si el saldo está
    /// fechado dentro de ella, devuelve el valor y los días del saldo y mueve el inicio de la
    /// ventana al día siguiente. Los días, si el saldo no los trae, se cuentan desde el inicio de
    /// la ventana hasta la fecha del saldo con el calendario comercial.
    /// </summary>
    public static Tramo Apply(SettlementContext ctx, DateTime windowStart, DateTime windowEnd, Func<OpeningBalanceInput, decimal> amount,
        Func<OpeningBalanceInput, int?> daysAccrued, string label)
    {
        var saldo = ctx.Input.OpeningBalance;
        if (saldo is null) return Ninguno(windowStart);
        var asOf = saldo.AsOfDate.Date;
        if (asOf < windowStart.Date || asOf > windowEnd.Date) return Ninguno(windowStart);

        var valor = amount(saldo);
        var dias = daysAccrued(saldo) ?? CalendarConventions.Days(windowStart, asOf);
        var texto = $"Saldo inicial de {label} al {Fmt.Date(asOf)} digitado por {(string.IsNullOrWhiteSpace(saldo.EnteredBy) ? "(sin registro)" : saldo.EnteredBy)}" +
                    (saldo.EnteredAt is { } en ? $" el {Fmt.Date(en)}" : string.Empty) +
                    $": {Fmt.Money(valor)} por {dias} días.";
        return new Tramo(valor, dias, asOf.AddDays(1), texto);
    }

    /// <summary>La advertencia obligatoria cuando el ingreso es anterior al arranque y no hay saldo (FR-007).</summary>
    public static void WarnIfMissing(SettlementContext ctx)
    {
        if (ctx.Input.OpeningBalance is not null) return;
        if (ctx.Policies.PayrollStartDate is not { } arranque) return;
        if (ctx.Employee.JoinDate.Date >= arranque.Date) return;

        ctx.Flags |= SettlementFlags.OpeningBalanceMissing;
        ctx.Warn($"El empleado ingresó el {Fmt.Date(ctx.Employee.JoinDate)}, antes del arranque de la nómina ({Fmt.Date(arranque)}), " +
                 "y no tiene saldo inicial de prestaciones registrado: la liquidación se calcula desde el historial de salarios y puede no " +
                 "reflejar lo ya pagado o consignado antes del arranque. Registre el saldo en Nómina › Saldos iniciales o confirme que no aplica.");
    }
}
