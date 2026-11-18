using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

/// <summary>Lo que un pago abona a un rubro (capital, interés corriente, interés de mora, seguro…), con su nombre para mostrar e imprimir. (nuevo)</summary>
public sealed record RubroDelPagoDto(string Concept, string Name, decimal Amount);

/// <summary>Lo que un pago a una cuenta abona a una cuota de un período (aportes, ahorro permanente o contractual). (nuevo)</summary>
public sealed record PeriodoDelPagoDto(string Period, decimal Amount);

/// <summary>
/// 2026-11-17 (pedido del dueño): el desglose de un ítem de la caja tal como lo simuló su módulo, para que el cajero vea —y el recibo imprima— qué paga
/// de cada rubro y cómo queda el producto. En un crédito: los rubros (el interés corriente y el de mora aparte), las cuotas que toca, lo que queda como
/// anticipo o saldo a favor y los saldos después (para cancelar, vencido, días de mora, próxima cuota). En una cuenta: los períodos que cubre y el saldo
/// después. Viaja en <c>ItemsJson</c> con la simulación: la confirmación sólo pasa si la distribución no cambió (<c>Receipts.Receipt.DistributionChanged</c>),
/// así que lo simulado es lo aplicado. (nuevo)
/// </summary>
public sealed record DesgloseDelPagoDto(
    IReadOnlyList<RubroDelPagoDto> Concepts,
    IReadOnlyList<int> Installments,
    decimal Advance,
    decimal CreditBalance,
    decimal? PayoffAfter,
    decimal? PastDueAfter,
    int? DaysPastDueAfter,
    DateOnly? NextDueDate,
    decimal? NextInstallment,
    bool PaidOff,
    IReadOnlyList<PeriodoDelPagoDto> Periods,
    decimal? AccountBalanceAfter);

/// <summary>Arma el <see cref="DesgloseDelPagoDto"/> desde la simulación de cada módulo. (nuevo)</summary>
public static class DesgloseDelPago
{
    /// <summary>El orden en que se muestran los rubros: el de la prelación habitual de un pago.</summary>
    private static readonly ConceptoDeObligacion[] Orden =
    [
        ConceptoDeObligacion.CollectionFee, ConceptoDeObligacion.OtherCharge, ConceptoDeObligacion.Insurance, ConceptoDeObligacion.Administration,
        ConceptoDeObligacion.DefaultInterest, ConceptoDeObligacion.DefaultInterestInSuspense, ConceptoDeObligacion.CurrentInterest,
        ConceptoDeObligacion.CurrentInterestInSuspense, ConceptoDeObligacion.Principal,
    ];

    public static string Nombre(ConceptoDeObligacion concepto) => concepto switch
    {
        ConceptoDeObligacion.Principal => "Capital",
        ConceptoDeObligacion.CurrentInterest => "Interés corriente",
        ConceptoDeObligacion.CurrentInterestInSuspense => "Interés corriente en suspenso",
        ConceptoDeObligacion.DefaultInterest => "Interés de mora",
        ConceptoDeObligacion.DefaultInterestInSuspense => "Interés de mora en suspenso",
        ConceptoDeObligacion.Insurance => "Seguro",
        ConceptoDeObligacion.Administration => "Administración",
        ConceptoDeObligacion.CollectionFee => "Gastos de cobranza",
        ConceptoDeObligacion.OtherCharge => "Otros cargos",
        ConceptoDeObligacion.Advance => "Anticipo",
        ConceptoDeObligacion.CreditBalance => "Saldo a favor",
        ConceptoDeObligacion.RecognizedInterest or ConceptoDeObligacion.RecognizedInterestDirect => "Interés corriente de la cuota",
        ConceptoDeObligacion.RecognizedInsurance or ConceptoDeObligacion.RecognizedInsuranceDirect => "Seguro de la cuota",
        ConceptoDeObligacion.RecognizedAdministration or ConceptoDeObligacion.RecognizedAdministrationDirect => "Administración de la cuota",
        _ => concepto.ToString(),
    };

    /// <summary>El desglose de un pago a un crédito simulado por el contrato de obligaciones.</summary>
    public static DesgloseDelPagoDto DeCredito(SimulacionDePagoDto s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var rubros = s.Distribution
            .Where(d => d.Concept is not (ConceptoDeObligacion.Advance or ConceptoDeObligacion.CreditBalance) && d.Amount != 0m)
            .GroupBy(d => d.Concept)
            .OrderBy(g => Array.IndexOf(Orden, g.Key) is var i && i < 0 ? int.MaxValue : i)
            .Select(g => new RubroDelPagoDto(g.Key.ToString(), Nombre(g.Key), g.Sum(x => x.Amount)))
            .ToList();
        var cuotas = s.Distribution.Where(d => d.InstallmentNumber is not null && d.Amount != 0m).Select(d => d.InstallmentNumber!.Value).Distinct().Order().ToList();
        var despues = s.BalancesAfter;
        var plan = s.NewPlan;
        return new DesgloseDelPagoDto(rubros, cuotas, s.AdvanceAmount, s.CreditBalanceAmount, despues.PayoffAmount, despues.PastDue, despues.DaysPastDue,
            plan?.NextDueDate, plan?.InstallmentAmount, despues.PayoffAmount <= 0m, [], null);
    }

    /// <summary>El desglose de una consignación a una cuenta simulada por el contrato de cuentas.</summary>
    public static DesgloseDelPagoDto DeCuenta(SimulacionDeCuentaDto s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var periodos = s.InstallmentsCovered.Where(c => c.Amount != 0m).GroupBy(c => c.Period).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PeriodoDelPagoDto(g.Key, g.Sum(x => x.Amount))).ToList();
        return new DesgloseDelPagoDto([], [], 0m, 0m, null, null, null, null, null, false, periodos, s.AvailableAfter.Balance);
    }
}
