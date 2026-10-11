using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Settlement;

/// <summary>El estado de una cuota en el estado de cuenta (FR-069). (nuevo)</summary>
public static class EstadosDeCuota
{
    public const string Pagada = "Paid";
    public const string Vencida = "PastDue";
    public const string Cubierta = "Covered";
    public const string PorVencer = "Upcoming";
}

/// <summary>Una cuota del estado de cuenta: plan, fecha exigible, estado y lo que debe por concepto. (nuevo)</summary>
public sealed record StatementInstallmentDto(int Number, DateOnly DueDate, DateOnly ExigibleDate, string Status, decimal Principal, decimal Interest,
    decimal Insurance, decimal Total, IReadOnlyDictionary<string, decimal> Owed);

/// <summary>Una versión del plan del crédito. (nuevo)</summary>
public sealed record StatementScheduleVersionDto(int Version, DateOnly ValidFrom, string Cause, string RulesVersion, int InstallmentCount, int FirstRegeneratedInstallment);

/// <summary>Una cuota no aplicada por la pagaduría o la nómina (<c>LND_InstallmentNonDeductions</c>). (nuevo)</summary>
public sealed record StatementNonDeductionDto(int Installment, DateOnly DueDate, string Origin, string Reason, string? ReasonText, decimal ExpectedAmount,
    decimal NotDeductedAmount);

/// <summary>Las marcas del crédito a la fecha. (nuevo)</summary>
public sealed record StatementMarksDto(bool Suspended, bool UsuryCapped, bool Restructured, int RestructuringCount);

/// <summary>
/// El estado de cuenta a D con la información hasta R (FR-069; api.md §8.1): cuotas pagadas, vencidas, cubiertas por anticipo y por vencer; días de
/// mora; saldos por concepto; lo que cuesta cancelar; versiones del plan; marcas; los no aplicados. La calificación y la etapa de cobranza quedan
/// vacías hasta US9 y K4. (nuevo)
/// </summary>
public sealed record LoanStatementDto(
    Guid LoanPublicId,
    string Number,
    string Status,
    DateOnly Date,
    DateTime? RecordedAsOf,
    string Fingerprint,
    int DaysPastDue,
    int? OldestUnpaidInstallment,
    int InstallmentsPastDue,
    decimal ToPayOff,
    IReadOnlyDictionary<string, decimal> Balances,
    IReadOnlyList<StatementInstallmentDto> Installments,
    IReadOnlyList<StatementScheduleVersionDto> ScheduleVersions,
    StatementMarksDto Marks,
    IReadOnlyList<StatementNonDeductionDto> NotApplied,
    string? InformativeRating,
    string? OfficialRating,
    string? CollectionStage);

/// <summary>
/// <c>GET /api/lending/loans/{id}/statement?date=&amp;recordedAsOf=</c> (feature 013, US4, T0503; api.md §8.1, <c>Lending.Loans.Read</c>; FR-069): de
/// <see cref="Domain.Lending.Replay.ReconstruccionDeCredito"/> a (D, R), como la liquidación; consultar no escribe y, porque lleva lo que cuesta
/// cancelar, audita <c>Lending.Payoff.Viewed</c> con D, R y la huella (la lista cerrada de lecturas sensibles no tiene otro evento). Fuera del
/// alcance, el mismo 404 que si no existiera. (nuevo)
/// </summary>
public sealed record GetLoanStatementQuery(Guid LoanPublicId, DateOnly? Date = null, DateTime? RecordedAsOf = null) : IRequest<Result<LoanStatementDto>>;

public sealed class GetLoanStatementQueryHandler(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetLoanStatementQuery, Result<LoanStatementDto>>
{
    public async Task<Result<LoanStatementDto>> Handle(GetLoanStatementQuery request, CancellationToken ct)
    {
        var leido = await LecturaDelCredito.ReconstruirAsync(db, cargador, alcance, auditoria, reloj, request.LoanPublicId, request.Date, request.RecordedAsOf, ct);
        if (leido.IsFailure) return Result.Failure<LoanStatementDto>(leido.Error);
        var (credito, estado, d) = leido.Value;
        var r = request.RecordedAsOf ?? DateTime.MaxValue;

        var plan = estado.Plan?.Cuotas ?? [];
        var cuotas = estado.Cuotas.Select(q =>
        {
            var delPlan = plan.First(c => c.Numero == q.Numero);
            var debe = estado.Saldos.Where(s => s.Cuota == q.Numero && s.Valor != 0m).ToDictionary(s => s.Concepto.ToString(), s => s.Valor);
            var saldo = debe.Values.Sum();
            var estadoDeCuota = saldo <= 0m && q.Vencimiento <= d ? EstadosDeCuota.Pagada : q.Vencida ? EstadosDeCuota.Vencida : EstadosDeCuota.PorVencer;
            return new StatementInstallmentDto(q.Numero, q.Vencimiento, q.FechaExigible, estadoDeCuota, delPlan.Capital, delPlan.Interes, delPlan.Seguro,
                delPlan.Total, debe);
        }).ToList();

        var versiones = (await (from v in db.RepaymentScheduleVersions.AsNoTracking()
                                join t in db.LoanTermsVersions.AsNoTracking() on v.TermsVersionId equals t.Id
                                where v.LoanId == credito.Id
                                orderby v.VersionNumber
                                select new { v.VersionNumber, v.EffectiveValueDate, t.Cause, v.RulesVersion, v.InstallmentCount, v.FirstRegeneratedInstallment, v.RegisteredAt })
                .ToListAsync(ct))
            .Where(v => v.RegisteredAt <= r)
            .Select(v => new StatementScheduleVersionDto(v.VersionNumber, v.EffectiveValueDate, v.Cause.ToString(), v.RulesVersion, v.InstallmentCount,
                v.FirstRegeneratedInstallment))
            .ToList();
        var condiciones = (await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == credito.Id).ToListAsync(ct))
            .Where(t => t.RegisteredAt <= r).OrderBy(t => t.VersionNumber).LastOrDefault();
        var noAplicados = await db.InstallmentNonDeductions.AsNoTracking().Where(n => n.LoanId == credito.Id && !n.IsDeleted)
            .OrderBy(n => n.DueDate).ThenBy(n => n.InstallmentNumber)
            .Select(n => new StatementNonDeductionDto(n.InstallmentNumber, n.DueDate, n.Origin.ToString(), n.Reason.ToString(), n.ReasonText, n.ExpectedAmount,
                n.NotDeductedAmount))
            .ToListAsync(ct);

        await auditoria.EmitirLiquidacionVistaAsync(credito.PublicId, d, request.RecordedAsOf, estado.Huella, ct);
        return Result.Success(new LoanStatementDto(credito.PublicId, credito.Number, credito.Status.ToString(), d, request.RecordedAsOf, "sha256:" + estado.Huella,
            estado.AlturaDeMora.Dias, estado.AlturaDeMora.CuotaMasAntigua, estado.AlturaDeMora.CuotasVencidas, estado.ValorParaCancelar,
            estado.SaldosPorConcepto.ToDictionary(s => s.Key.ToString(), s => s.Value), cuotas, versiones,
            new StatementMarksDto(estado.Suspendida, estado.UsuraTopada, (condiciones?.RestructuringCount ?? 0) > 0, condiciones?.RestructuringCount ?? 0),
            noAplicados, null, null, null));
    }
}
