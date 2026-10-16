using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Agreements;

// Feature 013, K4 (US12, T0946; api.md §14.7; data-model §26.9; FR-120): el acuerdo de pago sobre lo vencido, sin cambiar las condiciones, con su
// propio calendario y su cumplimiento derivado de los pagos vigentes. (nuevo)

/// <summary>Una cuota del calendario del acuerdo (api.md §14.7 <c>installments[]</c>). (nuevo)</summary>
public sealed record AgreementInstallmentInput(DateOnly DueDate, decimal Amount);

/// <summary>Una cuota del acuerdo como la muestra la lista. (nuevo)</summary>
public sealed record AgreementInstallmentDto(int Number, DateOnly DueDate, decimal Amount);

/// <summary>El cumplimiento derivado de un acuerdo aprobado: lo pagado, el total, las cuotas cubiertas y, si se incumplió, la cuota y el día. (nuevo)</summary>
public sealed record AgreementComplianceDto(decimal Paid, decimal Total, int CoveredInstallments, int? BrokenInstallment, DateOnly? BrokenOn);

/// <summary>Un acuerdo de pago (api.md §14.7): estado, cumplimiento calculado (sólo aprobado), calendario y quién lo propuso y aprobó. (nuevo)</summary>
public sealed record PaymentAgreementDto(
    Guid PublicId,
    string Number,
    CreditoRefDto Loan,
    PersonaRefDto Debtor,
    PaymentAgreementStatus Status,
    PaymentAgreementPerformance? Performance,
    decimal PastDueAmountAtAgreement,
    decimal TotalAmount,
    DateOnly StartDate,
    IReadOnlyList<AgreementInstallmentDto> Installments,
    AgreementComplianceDto? Compliance,
    UsuarioRefDto CreatedBy,
    UsuarioRefDto? ApprovedBy,
    DateTime? ApprovedAt,
    string? RejectionReason,
    string? CancelReason);

/// <summary>Lo común de los acuerdos: leer con alcance, el cumplimiento y la proyección. (nuevo)</summary>
public static class AcuerdosDePago
{
    /// <summary>El prefijo del número de un acuerdo de pago (numerador de Cartera, clase <c>PaymentAgreement</c>).</summary>
    public const string Prefijo = "ACU-";

    /// <summary>El acuerdo con su calendario, del alcance de quien opera (fuera, el mismo 404).</summary>
    public static async Task<Result<(PaymentAgreement Acuerdo, Loan Credito)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance,
        LendingAuditEmitter auditoria, Guid publicId, CancellationToken ct)
    {
        var acuerdo = await db.PaymentAgreements.Include(a => a.Installments).FirstOrDefaultAsync(a => a.PublicId == publicId && !a.IsDeleted, ct);
        if (acuerdo is null) return Result.Failure<(PaymentAgreement, Loan)>(ErroresDeCobranza.AcuerdoInexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == acuerdo.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeCobranza.AcuerdoInexistente(), auditoria, "PaymentAgreement",
            acuerdo.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<(PaymentAgreement, Loan)>(dentro.Error) : Result.Success((acuerdo, credito));
    }

    /// <summary>El cumplimiento a <paramref name="hoy"/> de un acuerdo aprobado (nulo si no lo está), con los pagos vigentes desde su aprobación.</summary>
    public static ResultadoDeAcuerdo? Cumplimiento(PaymentAgreement a, IReadOnlyList<PagoParaCobranza> pagos, DateOnly hoy, TimeSpan desfase) =>
        a.Status == PaymentAgreementStatus.Approved && a.ApprovedAt is { } aprobado && a.Installments.Count > 0
            ? CumplimientoDeAcuerdo.Calcular(a.Calendario(), DateOnly.FromDateTime(CobranzaDeCartera.ALocal(aprobado, desfase)), pagos, hoy)
            : null;

    public static async Task<IReadOnlyList<PaymentAgreementDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<PaymentAgreement> acuerdos, DateOnly hoy,
        TimeSpan desfase, CancellationToken ct)
    {
        var loanIds = acuerdos.Select(a => a.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, acuerdos.Select(a => a.PersonId), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, acuerdos.Select(a => a.CreatedByUserId)
            .Concat(acuerdos.Where(a => a.ApprovedByUserId != null).Select(a => a.ApprovedByUserId!.Value)), ct);
        var pagos = await CobranzaDeCartera.PagosVigentesAsync(db, loanIds, ct);
        return acuerdos.Select(a =>
        {
            var c = creditos[a.LoanId];
            var r = Cumplimiento(a, pagos.GetValueOrDefault(a.LoanId) ?? [], hoy, desfase);
            return new PaymentAgreementDto(a.PublicId, a.Number, new CreditoRefDto(c.PublicId, c.Number), personas[a.PersonId], a.Status, r?.Estado,
                a.PastDueAmountAtAgreement, a.TotalAmount, a.StartDate,
                a.Installments.OrderBy(i => i.Number).Select(i => new AgreementInstallmentDto(i.Number, i.DueDate, i.Amount)).ToList(),
                r is null ? null : new AgreementComplianceDto(r.Pagado, r.Total, r.CuotasCubiertas, r.CuotaIncumplida, r.IncumplidoEl),
                usuarios[a.CreatedByUserId], a.ApprovedByUserId is { } u ? usuarios[u] : null, a.ApprovedAt, a.RejectionReason, a.CancelReason);
        }).ToList();
    }
}

// ---------------------------------------------------------------------------------------------------------------- proponer --

/// <summary>
/// Propone un acuerdo de pago (feature 013, K4, T0946; api.md §14.7, <c>POST /api/lending/payment-agreements</c>, <c>Lending.PaymentAgreements.Create</c>,
/// con clave y motivo; FR-120): sobre un crédito vigente del alcance y <b>sólo sobre lo vencido</b> a hoy —si el calendario suma más de lo vencido, o
/// no hay nada vencido, cambia las condiciones y es una novedad (<c>Lending.PaymentAgreement.ChangesTerms</c>, FR-090)—; el calendario empieza hoy o
/// después, crece y cada cuota es positiva (<c>.ScheduleInvalid</c>); un crédito tiene a lo sumo un acuerdo propuesto o vigente al día
/// (<c>.AlreadyOpen</c>). Nace <c>Proposed</c> con su número (<see cref="NumeradorDeCartera"/>) y lo vencido sellado. (nuevo)
/// </summary>
public sealed record CreatePaymentAgreementCommand(Guid LoanPublicId, IReadOnlyList<AgreementInstallmentInput> Installments, string Reason)
    : IRequest<Result<PaymentAgreementDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CreatePaymentAgreementCommandValidator : ValidadorConMotivo<CreatePaymentAgreementCommand>
{
    public CreatePaymentAgreementCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.Installments).NotEmpty().WithMessage("El acuerdo lleva al menos una cuota.");
        RuleForEach(x => x.Installments).ChildRules(c => c.RuleFor(x => x.Amount).GreaterThan(0m).PrecisionScale(18, 2, true)
            .WithMessage("Cada cuota del acuerdo es mayor que cero, con hasta dos decimales."));
    }
}

public sealed class CreatePaymentAgreementCommandHandler(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CreatePaymentAgreementCommand, Result<PaymentAgreementDto>>
{
    public async Task<Result<PaymentAgreementDto>> Handle(CreatePaymentAgreementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await CobranzaDeCartera.CreditoAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var credito = leido.Value;
        if (credito.Status != LoanStatus.Active) return Falla(Novelties.ErroresDeNovedades.CreditoNoActivo(credito.Number, credito.Status.ToString()));
        var hoy = reloj.HoyLocal;
        var cuotas = request.Installments.OrderBy(i => i.DueDate).Select(i => (i.DueDate, i.Amount)).ToList();
        if (PaymentAgreement.ValidarCalendario(hoy, cuotas) is { } calendario) return Falla(ErroresDeGobierno.De(calendario));

        var cargado = await cargador.CargarAsync(credito.Id, null, hoy, ct);
        if (cargado is not { Listo: true }) return Falla(cargado?.Error ?? Settlement.LecturaDelCredito.Inexistente());
        decimal vencido;
        try
        {
            vencido = CobranzaDeCartera.ValorVencido(MotorDeCartera.Reconstruir(cargado.Entrada!, hoy));
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }
        var total = cuotas.Sum(c => c.Amount);
        if (vencido <= 0m || total > vencido) return Falla(ErroresDeCobranza.CambiaCondiciones(total, vencido));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);
            var abiertos = await db.PaymentAgreements.Include(a => a.Installments).AsNoTracking()
                .Where(a => a.LoanId == credito.Id && !a.IsDeleted && (a.Status == PaymentAgreementStatus.Proposed || a.Status == PaymentAgreementStatus.Approved))
                .ToListAsync(ct);
            var pagos = (await CobranzaDeCartera.PagosVigentesAsync(db, [credito.Id], ct))[credito.Id];
            var abierto = abiertos.FirstOrDefault(a => a.Status == PaymentAgreementStatus.Proposed
                || AcuerdosDePago.Cumplimiento(a, pagos, hoy, reloj.AhoraLocal.Offset)?.Estado == PaymentAgreementPerformance.Current);
            if (abierto is not null) return Falla(ErroresDeCobranza.AcuerdoAbierto(abierto.Number));

            var numero = await numerador.AsignarAsync(LendingNumberKind.PaymentAgreement, string.Empty, AcuerdosDePago.Prefijo, ct);
            var acuerdo = PaymentAgreement.Proponer(numero, credito.Id, credito.DebtorPersonId, vencido, hoy, cuotas, usuario);
            db.PaymentAgreements.Add(acuerdo);
            await db.SaveChangesAsync(ct);
            return Result.Success((await AcuerdosDePago.DtosAsync(db, [acuerdo], hoy, reloj.AhoraLocal.Offset, ct))[0]);
        }, ct);
    }

    private static Result<PaymentAgreementDto> Falla(Error e) => Result.Failure<PaymentAgreementDto>(e);
}

// --------------------------------------------------------------------------------------------- aprobar, rechazar, anular --

/// <summary>
/// Aprueba un acuerdo de pago (feature 013, K4, T0946; api.md §14.7, <c>POST /api/lending/payment-agreements/{id}/approve</c>,
/// <c>Lending.PaymentAgreements.Approve</c>, con clave): sólo uno propuesto (<c>Lending.PaymentAgreement.NotProposed</c>) y por otra persona que quien lo
/// propuso (<c>Lending.Segregation.Conflict</c>, comparando <c>SEC_Users.Id</c>). Desde la aprobación, su cumplimiento se deriva de los pagos y, mientras
/// esté al día, el crédito no avanza de etapa. (nuevo)
/// </summary>
public sealed record ApprovePaymentAgreementCommand(Guid AgreementPublicId, string? Reason = null) : IRequest<Result<PaymentAgreementDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApprovePaymentAgreementCommandValidator : AbstractValidator<ApprovePaymentAgreementCommand>
{
    public ApprovePaymentAgreementCommandValidator() => RuleFor(x => x.AgreementPublicId).NotEmpty();
}

public sealed class ApprovePaymentAgreementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApprovePaymentAgreementCommand, Result<PaymentAgreementDto>>
{
    public async Task<Result<PaymentAgreementDto>> Handle(ApprovePaymentAgreementCommand request, CancellationToken ct) =>
        await TransicionDeAcuerdo.AplicarAsync(db, alcance, auditoria, actorActual, reloj, request.AgreementPublicId,
            (a, usuario) => a.Aprobar(usuario, reloj.UtcNow), ct);
}

/// <summary>
/// Rechaza un acuerdo de pago propuesto (feature 013, K4, T0946; <c>Lending.PaymentAgreements.Approve</c>, con clave y motivo;
/// <c>Lending.PaymentAgreement.NotProposed</c> si no está propuesto). api.md no trae la ruta (el estado <c>Rejected</c> sí): la propone T0956 en
/// <c>/{id}/reject</c>. (nuevo)
/// </summary>
public sealed record RejectPaymentAgreementCommand(Guid AgreementPublicId, string Reason) : IRequest<Result<PaymentAgreementDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RejectPaymentAgreementCommandValidator : ValidadorConMotivo<RejectPaymentAgreementCommand>
{
    public RejectPaymentAgreementCommandValidator() => RuleFor(x => x.AgreementPublicId).NotEmpty();
}

public sealed class RejectPaymentAgreementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RejectPaymentAgreementCommand, Result<PaymentAgreementDto>>
{
    public async Task<Result<PaymentAgreementDto>> Handle(RejectPaymentAgreementCommand request, CancellationToken ct) =>
        await TransicionDeAcuerdo.AplicarAsync(db, alcance, auditoria, actorActual, reloj, request.AgreementPublicId, (a, _) => a.Rechazar(request.Reason), ct);
}

/// <summary>
/// Anula un acuerdo de pago propuesto o aprobado (feature 013, K4, T0946; api.md §14.7, <c>POST /api/lending/payment-agreements/{id}/cancel</c>,
/// <c>Lending.PaymentAgreements.Create</c>, con clave y motivo; <c>Lending.PaymentAgreement.InvalidStatus</c> si ya terminó). (nuevo)
/// </summary>
public sealed record CancelPaymentAgreementCommand(Guid AgreementPublicId, string Reason) : IRequest<Result<PaymentAgreementDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CancelPaymentAgreementCommandValidator : ValidadorConMotivo<CancelPaymentAgreementCommand>
{
    public CancelPaymentAgreementCommandValidator() => RuleFor(x => x.AgreementPublicId).NotEmpty();
}

public sealed class CancelPaymentAgreementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CancelPaymentAgreementCommand, Result<PaymentAgreementDto>>
{
    public async Task<Result<PaymentAgreementDto>> Handle(CancelPaymentAgreementCommand request, CancellationToken ct) =>
        await TransicionDeAcuerdo.AplicarAsync(db, alcance, auditoria, actorActual, reloj, request.AgreementPublicId,
            (a, _) => a.Anular(request.Reason, reloj.UtcNow), ct);
}

/// <summary>Una transición del acuerdo: leer con alcance, aplicar la del dominio (sus códigos), guardar y proyectar. (nuevo)</summary>
internal static class TransicionDeAcuerdo
{
    public static async Task<Result<PaymentAgreementDto>> AplicarAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        IActorActual actorActual, IDateTimeService reloj, Guid publicId, Action<PaymentAgreement, int> transicion, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymentAgreementDto>(ErroresDeGobierno.SinUsuario());
        var leido = await AcuerdosDePago.LeerAsync(db, alcance, auditoria, publicId, ct);
        if (leido.IsFailure) return Result.Failure<PaymentAgreementDto>(leido.Error);
        var (acuerdo, _) = leido.Value;
        try
        {
            transicion(acuerdo, usuario);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<PaymentAgreementDto>(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success((await AcuerdosDePago.DtosAsync(db, [acuerdo], reloj.HoyLocal, reloj.AhoraLocal.Offset, ct))[0]);
    }
}

// ---------------------------------------------------------------------------------------------------------------- lista --

/// <summary>
/// Los acuerdos de pago (feature 013, K4, T0946; api.md §14.7, <c>GET /api/lending/payment-agreements?status=&amp;loanPublicId=&amp;page=</c>,
/// <c>Lending.Collections.Read</c>): con su calendario y, los aprobados, su cumplimiento <b>calculado</b> a hoy (<see cref="CumplimientoDeAcuerdo"/>).
/// Filtrados en silencio por el alcance; el más reciente primero. (nuevo)
/// </summary>
public sealed record ListPaymentAgreementsQuery(PaymentAgreementStatus? Status = null, Guid? LoanPublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<PaymentAgreementDto>>>;

public sealed class ListPaymentAgreementsQueryValidator : AbstractValidator<ListPaymentAgreementsQuery>
{
    public ListPaymentAgreementsQueryValidator() => RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
}

public sealed class ListPaymentAgreementsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListPaymentAgreementsQuery, Result<PagedResult<PaymentAgreementDto>>>
{
    public async Task<Result<PagedResult<PaymentAgreementDto>>> Handle(ListPaymentAgreementsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.PaymentAgreements.AsNoTracking().Include(a => a.Installments).Where(a => !a.IsDeleted && creditos.Contains(a.LoanId));
        if (request.Status is { } estado) consulta = consulta.Where(a => a.Status == estado);
        if (request.LoanPublicId is { } loan)
        {
            var id = await db.Loans.AsNoTracking().Where(l => l.PublicId == loan).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(a => a.LoanId == id);
        }
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(a => a.Id).Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<PaymentAgreementDto>(await AcuerdosDePago.DtosAsync(db, filas, reloj.HoyLocal, reloj.AhoraLocal.Offset, ct),
            pagina.SafePage, pagina.SafePageSize, total));
    }
}
