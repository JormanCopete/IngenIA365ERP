using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Payments.Queries;

/// <summary>Una línea de la distribución de la pantalla. (nuevo)</summary>
public sealed record PaymentDistributionLineDto(int? Installment, string Concept, decimal Amount);

/// <summary>Un aviso de la simulación con su mensaje en español. (nuevo)</summary>
public sealed record PaymentNoticeDto(string Code, string Message);

/// <summary>Una alternativa del abono extraordinario: su huella, su plan y lo que queda. (nuevo)</summary>
public sealed record PaymentAlternativeDto(string SimulationId, int Installments, decimal InstallmentAmount, IReadOnlyList<CuotaDelPlanDto> Schedule,
    IReadOnlyDictionary<string, decimal> ResultingBalances);

/// <summary><c>PaymentSimulationDto</c> (api.md §10): la distribución, lo que queda y, en un abono sin elección, sus dos planes. (nuevo)</summary>
public sealed record PaymentSimulationDto(
    string SimulationId,
    string Modality,
    decimal Amount,
    DateOnly ValueDate,
    IReadOnlyList<PaymentDistributionLineDto> Distribution,
    decimal Surplus,
    decimal Advance,
    decimal CreditBalance,
    IReadOnlyDictionary<string, decimal> ResultingBalances,
    IReadOnlyDictionary<string, PaymentAlternativeDto> Alternatives,
    IReadOnlyList<PaymentNoticeDto> Notices,
    DateOnly ValidUntil,
    string? PrepaymentChoice,
    PlanResumidoDto? NewPlan);

/// <summary>
/// <c>POST /api/lending/loans/{id}/payment-simulations</c> (feature 013, US5, T0552; api.md §10; <c>Lending.Payments.Simulate</c>, sin clave): simula un
/// pago por pantalla con <see cref="AplicacionDePagos"/>, la <b>misma</b> función que el contrato (FR-072), sin escribir. Sin <c>amount</c>, una
/// cancelación es lo que cuesta cancelar y «lo vencido», lo exigible. (nuevo)
/// </summary>
public sealed record SimulateLoanPaymentQuery(
    Guid LoanPublicId,
    PaymentModality Modality,
    decimal? Amount,
    DateOnly ValueDate,
    PrepaymentChoice? PrepaymentChoice = null,
    int? InstallmentNumber = null) : IRequest<Result<PaymentSimulationDto>>;

/// <summary>
/// La simulación de pantalla (T0552): el crédito en el alcance de oficina y línea (<see cref="CreditoEnAlcance"/>, el 404 del crédito fuera de él);
/// la fecha valor con las reglas de caja (<see cref="ReglasDeFechaValor"/>: <c>Lending.Payment.ValueDateOutOfRange</c> con <c>data.minDate</c>); la
/// distribución con su huella; <c>Lending.Payment.SpecificInstallmentBlocked</c> con <c>data.pendingBefore[]</c> (el mismo código que el contrato,
/// C115); el aviso <c>Lending.Payment.AdvanceDoesNotReduceInterest</c> en un anticipo. (nuevo)
/// </summary>
public sealed class SimulateLoanPaymentQueryHandler(
    IApplicationDbContext db,
    AplicacionDePagos aplicacion,
    ReglasDeFechaValor fechas,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<SimulateLoanPaymentQuery, Result<PaymentSimulationDto>>
{
    public async Task<Result<PaymentSimulationDto>> Handle(SimulateLoanPaymentQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<PaymentSimulationDto>(leido.Error);
        var credito = leido.Value;
        if (credito.Status is LoanStatus.PendingDisbursement or LoanStatus.PaidOff or LoanStatus.Voided or LoanStatus.Novated)
            return Result.Failure<PaymentSimulationDto>(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));
        if (await fechas.ValidarAsync(ClaseDeRecaudador.Receipts, request.ValueDate, request.Amount ?? 0m, ct) is { } fecha)
            return Result.Failure<PaymentSimulationDto>(fecha);

        var d = await aplicacion.DistribuirAsync(credito, request.Modality, request.Amount ?? 0m, request.ValueDate, request.PrepaymentChoice,
            request.InstallmentNumber, usaPreferencia: false, ct);
        if (d.IsFailure) return Result.Failure<PaymentSimulationDto>(d.Error);
        var s = aplicacion.Simulacion(d.Value);
        return Result.Success(Dto(s, reloj.HoyLocal));
    }

    /// <summary>El DTO de pantalla de una simulación del contrato.</summary>
    public static PaymentSimulationDto Dto(SimulacionDePagoDto s, DateOnly validoHasta) => new(
        s.SimulationId,
        s.Modality.ToString(),
        s.Amount,
        s.ValueDate,
        s.Distribution.Select(l => new PaymentDistributionLineDto(l.InstallmentNumber, l.Concept.ToString(), l.Amount)).ToList(),
        s.Surplus,
        s.AdvanceAmount,
        s.CreditBalanceAmount,
        Saldos(s.BalancesAfter),
        s.Alternatives.Where(a => a.Choice is not null).ToDictionary(a => a.Choice!.Value.ToString(), a => new PaymentAlternativeDto(a.SimulationId,
            a.NewPlan?.InstallmentCount ?? 0, a.NewPlan?.InstallmentAmount ?? 0m, a.NewPlan?.Installments ?? [], Saldos(a.BalancesAfter))),
        s.Notices.Select(n => new PaymentNoticeDto(n, Mensaje(n))).ToList(),
        validoHasta,
        s.Choice?.ToString(),
        s.NewPlan);

    private static IReadOnlyDictionary<string, decimal> Saldos(SaldosDeObligacionDto b) => new Dictionary<string, decimal>
    {
        [nameof(LoanConcept.Principal)] = b.Principal,
        [nameof(LoanConcept.CurrentInterest)] = b.CurrentInterest,
        [nameof(LoanConcept.DefaultInterest)] = b.DefaultInterest,
        ["InterestInSuspense"] = b.InterestInSuspense,
        [nameof(LoanConcept.Insurance)] = b.Insurance,
        ["Charges"] = b.Charges,
        [nameof(LoanConcept.Advance)] = b.Advance,
        [nameof(LoanConcept.CreditBalance)] = b.CreditBalance,
        ["PayoffAmount"] = b.PayoffAmount,
    };

    /// <summary>Lo que dice cada aviso (data-model §10.1); el código no se muestra (FR-013).</summary>
    public static string Mensaje(string codigo) => codigo switch
    {
        AplicadorDePagos.AvisoAnticipoNoReduceIntereses => "El anticipo cubre cada cuota en su vencimiento y no reduce intereses; el abono a capital sí.",
        AplicadorDePagos.AvisoPreferenciaAsumida => "El abono quedó como saldo a favor: el deudor no tiene preferencia registrada.",
        AplicadorDePagos.AvisoTratadoComoCancelacion => "El abono alcanzó el capital por vencer y se aplicó como cancelación; lo que sobra vuelve al recaudador.",
        "Lending.Prevalidation.NotPostable" => "Contabilidad no tiene regla para este hecho. Pida a la contadora completar la matriz.",
        "Lending.Prevalidation.NoResponse" => "Contabilidad no respondió a tiempo: la operación sigue y queda marcada para revisión.",
        "Lending.Usury.ProjectedWithLastKnown" => "La usura de algún día todavía no está certificada: se proyectó con la última conocida.",
        _ => codigo,
    };
}
