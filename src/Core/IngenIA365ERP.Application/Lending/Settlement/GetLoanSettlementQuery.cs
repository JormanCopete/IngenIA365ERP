using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Settlement;

/// <summary>Una cuota de la liquidación: número, vencimiento y saldos por concepto. (nuevo)</summary>
public sealed record SettlementInstallmentDto(int Number, DateOnly DueDate, IReadOnlyDictionary<string, decimal> Balances);

/// <summary>Un tramo de la explicación de la liquidación (días, tasas, base y si se topó o suspendió; api.md §8.1). (nuevo)</summary>
public sealed record SettlementExplanationDto(string Concept, int? Installment, DateOnly From, DateOnly To, int Days, decimal Base, decimal? EffectiveAnnual,
    decimal DailyRate, decimal Amount, bool CappedByUsury, bool Suspended);

/// <summary>
/// <c>LoanSettlementDto</c> (api.md §8.1): la liquidación a D con la información hasta R, su huella (<c>sha256:</c> + la de la reconstrucción), lo
/// que cuesta cancelar, los saldos por concepto y por cuota, los saldos con el deudor, la explicación y hasta cuándo vale si se proyectó. (nuevo)
/// </summary>
public sealed record LoanSettlementDto(
    Guid LoanPublicId,
    string Number,
    DateOnly Date,
    DateTime? RecordedAsOf,
    string RulesVersion,
    string Fingerprint,
    decimal ToPayOff,
    IReadOnlyDictionary<string, decimal> ByConcept,
    IReadOnlyDictionary<string, decimal> Credits,
    IReadOnlyList<SettlementInstallmentDto> ByInstallment,
    int DaysPastDue,
    bool Suspended,
    bool UsuryCapped,
    IReadOnlyList<SettlementExplanationDto> Explanation,
    IReadOnlyList<string> ExplanationText,
    DateOnly? ValidUntil,
    IReadOnlyList<string> Warnings);

/// <summary>
/// <c>GET /api/lending/loans/{id}/settlement?date=&amp;recordedAsOf=</c> (feature 013, US4, T0503; api.md §8.1, <c>Lending.Payoffs.Read</c>; FR-068,
/// FR-011, C83): la liquidación sale de <see cref="ReconstruccionDeCredito"/> a (D, R), <b>no del libro</b>. Una D futura se proyecta sin pagos con la
/// última usura conocida y dice hasta cuándo vale (<c>validUntil</c>, D11); <c>NationalValues.Missing</c> sólo si D ≤ hoy. Consultar no escribe y
/// audita <c>Lending.Payoff.Viewed</c> con D, R y la huella. Fuera del alcance de oficina o línea, el mismo 404 que si no existiera
/// (<c>Lending.Access.OutOfScope</c>). (nuevo)
/// </summary>
public sealed record GetLoanSettlementQuery(Guid LoanPublicId, DateOnly? Date = null, DateTime? RecordedAsOf = null) : IRequest<Result<LoanSettlementDto>>;

public sealed class GetLoanSettlementQueryHandler(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetLoanSettlementQuery, Result<LoanSettlementDto>>
{
    public async Task<Result<LoanSettlementDto>> Handle(GetLoanSettlementQuery request, CancellationToken ct)
    {
        var leido = await LecturaDelCredito.ReconstruirAsync(db, cargador, alcance, auditoria, reloj, request.LoanPublicId, request.Date, request.RecordedAsOf, ct);
        if (leido.IsFailure) return Result.Failure<LoanSettlementDto>(leido.Error);
        var (credito, estado, d) = leido.Value;
        await auditoria.EmitirLiquidacionVistaAsync(credito.PublicId, d, request.RecordedAsOf, estado.Huella, ct);
        return Result.Success(Dto(credito, estado, d, request.RecordedAsOf));
    }

    public static LoanSettlementDto Dto(Loan credito, EstadoDelCredito estado, DateOnly d, DateTime? r)
    {
        string[] aFavor = [nameof(LoanConcept.Advance), nameof(LoanConcept.CreditBalance)];
        var porConcepto = new Dictionary<string, decimal>();
        foreach (var c in new[] { LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense,
                     LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge })
            porConcepto[c.ToString()] = estado.Saldo(c);
        var conDeudor = aFavor.ToDictionary(c => c, c => estado.Saldo(Enum.Parse<LoanConcept>(c)));
        var porCuota = estado.Cuotas
            .Select(q => new SettlementInstallmentDto(q.Numero, q.Vencimiento, estado.Saldos.Where(s => s.Cuota == q.Numero && s.Valor != 0m)
                .ToDictionary(s => s.Concepto.ToString(), s => s.Valor)))
            .Where(q => q.Balances.Count > 0)
            .ToList();
        var explicacion = estado.Explicacion.Tramos.Select(t => new SettlementExplanationDto(t.Concepto, t.Cuota, t.Desde, t.Hasta, t.Dias, t.Base, t.TasaEA,
            t.TasaDiaria, t.Valor, t.Topada, t.Suspendido)).ToList();
        var avisos = new List<string>();
        if (estado.ValidaHasta is { } v)
            avisos.Add($"La liquidación usa la última usura conocida desde el {v.AddDays(1):yyyy-MM-dd}: vale hasta el {v:yyyy-MM-dd}.");
        if (estado.UsuraTopada) avisos.Add("La tasa del día está topada por la usura certificada.");
        if (estado.Suspendida) avisos.Add("La causación está suspendida: los intereses van a suspenso.");
        return new LoanSettlementDto(credito.PublicId, credito.Number, d, r, estado.RulesVersion, "sha256:" + estado.Huella, estado.ValorParaCancelar,
            porConcepto, conDeudor, porCuota, estado.AlturaDeMora.Dias, estado.Suspendida, estado.UsuraTopada, explicacion, estado.Explicacion.Lineas,
            estado.ValidaHasta, avisos);
    }
}

/// <summary>
/// Lo que comparten la liquidación y el estado de cuenta (feature 013, US4, T0503): el crédito en el alcance (404 genérico con
/// <c>Lending.Access.OutOfScope</c> si no) y su reconstrucción a (D, R); <c>NationalValues.Missing</c> si un día hasta D ≤ hoy no tiene usura. (nuevo)
/// </summary>
public static class LecturaDelCredito
{
    public const string CodigoInexistente = "Lending.Loan.NotFound";

    public static Error Inexistente() => new(CodigoInexistente, "El crédito no existe.");

    public static async Task<Result<(Loan Credito, EstadoDelCredito Estado, DateOnly Fecha)>> ReconstruirAsync(IApplicationDbContext db,
        CargadorDeReconstruccion cargador, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj, Guid loanPublicId,
        DateOnly? fecha, DateTime? informacionHasta, CancellationToken ct)
    {
        var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == loanPublicId && !l.IsDeleted, ct);
        if (credito is null) return Result.Failure<(Loan, EstadoDelCredito, DateOnly)>(Inexistente());
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, Inexistente(), auditoria, "Loan", credito.PublicId, ct);
        if (dentro.IsFailure) return Result.Failure<(Loan, EstadoDelCredito, DateOnly)>(dentro.Error);

        var hoy = reloj.HoyLocal;
        var d = fecha ?? hoy;
        var r = informacionHasta is { } instante ? DateTime.SpecifyKind(instante.Kind == DateTimeKind.Local ? instante.ToUniversalTime() : instante, DateTimeKind.Utc) : (DateTime?)null;
        var cargado = await cargador.CargarAsync(credito.Id, r, d, ct);
        if (cargado is null) return Result.Failure<(Loan, EstadoDelCredito, DateOnly)>(Inexistente());
        if (!cargado.Listo) return Result.Failure<(Loan, EstadoDelCredito, DateOnly)>(cargado.Error!);
        EstadoDelCredito estado;
        try
        {
            estado = MotorDeCartera.Reconstruir(cargado.Entrada!, d);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<(Loan, EstadoDelCredito, DateOnly)>(ErroresDeGobierno.De(ex.Problema));
        }
        if (d <= hoy && cargado.Entrada!.Condiciones.UsuraRequerida && estado.PrimerDiaSinUsura is { } sinUsura && sinUsura <= d)
            return Result.Failure<(Loan, EstadoDelCredito, DateOnly)>(ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Usura,
                cargado.Series?.ClaveDeUsura ?? cargado.Pactadas.ModalityCode, sinUsura));
        return Result.Success((credito, estado, d));
    }
}
