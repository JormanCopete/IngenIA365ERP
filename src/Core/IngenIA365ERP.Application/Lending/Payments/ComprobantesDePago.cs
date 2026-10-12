using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// Lo que el contrato, la simulación de pantalla y el reverso muestran de un pago (feature 013, US5, bloque 9B; interfaces §3.4–§3.9): saldos,
/// plan resumido, distribución y la simulación con su huella. Los enums del puerto valen lo mismo que los del motor (DT §2.7,
/// <c>LosEnumsDelContratoNumeranComoElDominio</c>), así que se traducen por su número. Puro. (nuevo)
/// </summary>
public static class ComprobantesDePago
{
    private static readonly LoanConcept[] Cobrables =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense,
        LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge,
    ];

    public static ModalidadDePago Modalidad(PaymentModality m) => (ModalidadDePago)(int)m;

    public static PaymentModality Modalidad(ModalidadDePago m) => (PaymentModality)(int)m;

    public static EleccionDeAbono? Eleccion(PrepaymentChoice? e) => e is null ? null : (EleccionDeAbono)(int)e.Value;

    public static PrepaymentChoice? Eleccion(EleccionDeAbono? e) => e is null ? null : (PrepaymentChoice)(int)e.Value;

    public static ConceptoDeObligacion Concepto(LoanConcept c) => (ConceptoDeObligacion)(int)c;

    public static LoanConcept Concepto(ConceptoDeObligacion c) => (LoanConcept)(int)c;

    public static EstadoDeObligacion Estado(LoanStatus s) => (EstadoDeObligacion)(int)s;

    /// <summary>Lo exigible a la fecha del estado: todo lo cobrable de las cuotas que vencen hasta ese día (y lo que no es de una cuota).</summary>
    public static decimal Exigible(EstadoDelCredito e, bool incluyeElDia = true)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Plan is null) return 0m;
        var vencidas = e.Plan.Cuotas.Where(c => incluyeElDia ? c.Vencimiento <= e.Fecha : c.Vencimiento < e.Fecha).Select(c => c.Numero).ToHashSet();
        return e.Saldos.Where(s => Cobrables.Contains(s.Concepto) && s.Valor > 0m && (s.Cuota is null ? s.Concepto != LoanConcept.Principal : vencidas.Contains(s.Cuota.Value)))
            .Sum(s => s.Valor);
    }

    /// <summary>La cuota del ciclo: lo exigible más la próxima cuota por vencer (capital pendiente, interés y seguro del plan).</summary>
    public static decimal CuotaDelCiclo(EstadoDelCredito e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var exigible = Exigible(e);
        var proxima = e.Plan?.Cuotas.FirstOrDefault(c => c.Vencimiento > e.Fecha);
        if (proxima is null) return exigible;
        return exigible + e.Saldo(LoanConcept.Principal, proxima.Numero) + proxima.Interes + proxima.Seguro;
    }

    /// <summary>Los saldos de la obligación a la fecha del estado.</summary>
    public static SaldosDeObligacionDto Saldos(EstadoDelCredito e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new SaldosDeObligacionDto(
            e.Saldo(LoanConcept.Principal), e.Saldo(LoanConcept.CurrentInterest), e.Saldo(LoanConcept.DefaultInterest),
            e.Saldo(LoanConcept.CurrentInterestInSuspense) + e.Saldo(LoanConcept.DefaultInterestInSuspense),
            e.Saldo(LoanConcept.Insurance), e.Saldo(LoanConcept.CollectionFee) + e.Saldo(LoanConcept.OtherCharge),
            e.Anticipo, e.SaldoAFavor, Exigible(e, incluyeElDia: false), Exigible(e), Math.Max(0m, e.ValorParaCancelar), e.AlturaDeMora.Dias);
    }

    /// <summary>El plan resumido; nulo si no hay plan.</summary>
    public static PlanResumidoDto? Plan(PlanDePagos? plan, DateOnly? desde = null)
    {
        if (plan is null) return null;
        var cuotas = plan.Cuotas.Select(c => new CuotaDelPlanDto(c.Numero, c.Vencimiento, c.Capital, c.Interes, c.Seguro, 0m, c.Total)).ToList();
        var proxima = plan.Cuotas.FirstOrDefault(c => desde is null || c.Vencimiento > desde.Value);
        return new PlanResumidoDto(plan.Cuotas.Count, plan.CuotaNivelada ?? proxima?.Total ?? 0m, proxima?.Vencimiento, plan.Cuotas.LastOrDefault()?.Vencimiento,
            cuotas);
    }

    /// <summary>El resumen del plan en un hecho (<c>LoanPlanSummaryV1</c>).</summary>
    public static LoanPlanSummaryV1? PlanDelHecho(PlanDePagos? plan, int version, string periodicidad)
    {
        if (plan is null || plan.Cuotas.Count == 0) return null;
        return new LoanPlanSummaryV1
        {
            ScheduleVersion = version,
            AmortizationSystem = plan.Entrada.Sistema.ToString(),
            Periodicity = periodicidad,
            InstallmentCount = plan.Cuotas.Count,
            InstallmentAmount = plan.CuotaNivelada ?? plan.Cuotas[0].Total,
            FirstDueDate = plan.Cuotas[0].Vencimiento,
            FinalDueDate = plan.Cuotas[^1].Vencimiento,
            AnnualEffectiveRate = plan.Entrada.TasaEfectivaAnual,
            DayCountConvention = plan.Entrada.Convencion.ToString(),
            RulesVersion = plan.RulesVersion,
        };
    }

    /// <summary>La distribución por cuota y concepto (lo cubierto y el anticipo del mismo crédito que usó una cancelación).</summary>
    public static IReadOnlyList<LineaDeDistribucionDto> Lineas(DistribucionDePago d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return [.. d.Lineas.Select(l => new LineaDeDistribucionDto(l.Cuota, Concepto(l.Concepto), l.Monto))];
    }

    /// <summary>La simulación de un pago (o una alternativa del abono) con su huella (T32).</summary>
    public static SimulacionDePagoDto Simulacion(Guid obligacion, DistribucionDePago d, string simulationId, EstadoDelCredito despues,
        IReadOnlyList<SimulacionDePagoDto> alternativas, IReadOnlyList<string> avisos) =>
        new(simulationId, obligacion, Modalidad(d.Modalidad), d.Valor, d.FechaValor, Eleccion(d.Eleccion), d.Aplicado, d.Anticipo, d.SaldoAFavor,
            d.Excedente, Lineas(d), Saldos(despues), Plan(d.PlanNuevo, d.FechaValor), alternativas, avisos);

    /// <summary>¿Dos distribuciones cubren lo mismo? (para saber qué pagos posteriores se redistribuyeron.)</summary>
    public static bool MismaDistribucion(DistribucionDePago a, DistribucionDePago b) =>
        Clave(a) == Clave(b);

    private static string Clave(DistribucionDePago d) =>
        string.Join("|", d.Lineas.OrderBy(l => l.Cuota ?? 0).ThenBy(l => (int)l.Concepto).Select(l => $"{l.Cuota}:{(int)l.Concepto}:{l.Monto}"))
        + $"|A{d.Anticipo}:{d.CuotaDelAnticipo}|S{d.SaldoAFavor}|E{d.Excedente}|U" + string.Join(",", d.AnticipoUsado.Select(u => $"{u.Cuota}:{u.Monto}"));

    /// <summary>Lo que cambió en la distribución de un pago (después − antes) por concepto y cuota, para <c>reallocations</c> (suma cero).</summary>
    public static IReadOnlyList<ConceptAmountV1> Reasignacion(DistribucionDePago antes, DistribucionDePago despues)
    {
        static IEnumerable<(int? Cuota, LoanConcept Concepto, decimal Monto)> Partes(DistribucionDePago d) =>
            d.Lineas.Select(l => (l.Cuota, l.Concepto, l.Monto))
                .Concat(d.Anticipo != 0m ? [(d.CuotaDelAnticipo, LoanConcept.Advance, d.Anticipo)] : [])
                .Concat(d.SaldoAFavor != 0m ? [((int?)null, LoanConcept.CreditBalance, d.SaldoAFavor)] : [])
                .Concat(d.AnticipoUsado.Select(u => (u.Cuota, LoanConcept.Advance, -u.Monto)));
        return Partes(despues).Concat(Partes(antes).Select(p => (p.Cuota, p.Concepto, Monto: -p.Monto)))
            .GroupBy(p => (p.Cuota, p.Concepto))
            .Select(g => new ConceptAmountV1 { Concept = g.Key.Concepto.ToString(), InstallmentNumber = g.Key.Cuota, Amount = g.Sum(p => p.Monto) })
            .Where(m => m.Amount != 0m)
            .OrderBy(m => m.InstallmentNumber ?? 0).ThenBy(m => m.Concept, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>La huella de simulación de la distribución (atajo de <see cref="MotorDeCartera.HuellaDeSimulacion"/>).</summary>
    public static string Huella(Guid credito, string rulesVersion, DistribucionDePago d) => MotorDeCartera.HuellaDeSimulacion(credito, rulesVersion, d);
}
