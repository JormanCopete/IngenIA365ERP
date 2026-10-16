using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.DebtorEvents;

/// <summary>Una reclamación al seguro (api.md §11.4). (nuevo)</summary>
public sealed record InsuranceClaimDto(
    Guid PublicId,
    Guid LoanPublicId,
    string LoanNumber,
    PersonaRefDto Insurer,
    PersonaRefDto Deceased,
    DateOnly DeathDate,
    InsuranceClaimStatus Status,
    string? ClaimNumber,
    DateOnly? FiledOn,
    decimal? PaidAmount,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string? CloseReason,
    Guid? PaymentOperationPublicId,
    bool Migrated);

/// <summary>Un proceso de insolvencia (api.md §11.4). (nuevo)</summary>
public sealed record InsolvencyProceedingDto(
    Guid PublicId,
    PersonaRefDto Person,
    InsolvencyProceedingStatus Status,
    InsolvencyRegime? Regime,
    string? Authority,
    string? CaseNumber,
    DateOnly StartDate,
    DateOnly? FailedOn,
    DateTime? ClosedAt,
    string? CloseReason,
    Guid? Support,
    IReadOnlyList<string> Loans,
    Guid? NoveltyPublicId,
    bool Migrated);

/// <summary>Las respuestas de reclamaciones y procesos de insolvencia. (nuevo)</summary>
public static class ConsultasDeEventosDelDeudor
{
    public static async Task<IReadOnlyList<InsuranceClaimDto>> ReclamacionesAsync(IApplicationDbContext db, IReadOnlyList<InsuranceClaim> filas,
        CancellationToken ct)
    {
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, filas.SelectMany(c => new[] { c.InsurerPersonId, c.DeceasedPersonId }), ct);
        var ids = filas.Select(c => c.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => (l.PublicId, l.Number), ct);
        var ops = filas.Where(c => c.PaidOperationId is not null).Select(c => c.PaidOperationId!.Value).ToList();
        var operaciones = await db.LoanOperations.AsNoTracking().Where(o => ops.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.PublicId, ct);
        return filas.Select(c => new InsuranceClaimDto(c.PublicId, creditos[c.LoanId].PublicId, creditos[c.LoanId].Number, personas[c.InsurerPersonId],
            personas[c.DeceasedPersonId], c.DeathDate, c.Status, c.ClaimNumber, c.FiledOn, c.PaidAmount, c.CreatedAt, c.ClosedAt, c.CloseReason,
            c.PaidOperationId is { } o ? operaciones.GetValueOrDefault(o) : null, c.MigrationLoadId is not null)).ToList();
    }

    public static async Task<IReadOnlyList<InsolvencyProceedingDto>> ProcesosAsync(IApplicationDbContext db, IReadOnlyList<InsolvencyProceeding> filas,
        CancellationToken ct)
    {
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, filas.Select(p => p.PersonId), ct);
        var deudores = filas.Select(p => p.PersonId).Distinct().ToList();
        var creditos = (await db.Loans.AsNoTracking().Where(l => deudores.Contains(l.DebtorPersonId) && !l.IsDeleted
                                                                && l.Status == LoanStatus.Active)
                .Select(l => new { l.DebtorPersonId, l.Number }).ToListAsync(ct))
            .GroupBy(l => l.DebtorPersonId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Number).Order().ToList());
        var novedades = filas.Where(p => p.AgreementNoveltyId is not null).Select(p => p.AgreementNoveltyId!.Value).ToList();
        var publicas = await db.LoanNovelties.AsNoTracking().Where(n => novedades.Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => n.PublicId, ct);
        return filas.Select(p => new InsolvencyProceedingDto(p.PublicId, personas[p.PersonId], p.Status, p.Regime, p.Authority, p.CaseNumber, p.StartDate,
            p.FailedOn, p.ClosedAt, p.CloseReason, p.SupportAttachmentPublicId, creditos.GetValueOrDefault(p.PersonId) ?? [],
            p.AgreementNoveltyId is { } n ? publicas.GetValueOrDefault(n) : null, p.MigrationLoadId is not null)).ToList();
    }
}

/// <summary>
/// Las reclamaciones al seguro (feature 013, K3, T0885; api.md §11.4, <c>GET /api/lending/insurance-claims?status=&amp;loanPublicId=&amp;page=</c>,
/// <c>Lending.Loans.Read</c>): las de los créditos del alcance de oficina y línea, las más recientes primero; migradas y registradas por pantalla. (nuevo)
/// </summary>
public sealed record ListInsuranceClaimsQuery(InsuranceClaimStatus? Status = null, Guid? LoanPublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<InsuranceClaimDto>>>;

public sealed class ListInsuranceClaimsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListInsuranceClaimsQuery, Result<PagedResult<InsuranceClaimDto>>>
{
    public async Task<Result<PagedResult<InsuranceClaimDto>>> Handle(ListInsuranceClaimsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.LoanPublicId is { } credito) creditos = creditos.Where(l => l.PublicId == credito);
        var consulta = from c in db.InsuranceClaims.AsNoTracking()
                       join l in creditos on c.LoanId equals l.Id
                       where !c.IsDeleted
                       select c;
        if (request.Status is { } estado) consulta = consulta.Where(c => c.Status == estado);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(c => c.DeathDate).ThenByDescending(c => c.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<InsuranceClaimDto>(await ConsultasDeEventosDelDeudor.ReclamacionesAsync(db, filas, ct), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}

/// <summary>
/// Niega o desiste de una reclamación al seguro (feature 013, K3, T0885; api.md §11.4, <c>POST /api/lending/insurance-claims/{id}/status</c>,
/// <c>Lending.DebtorEvents.Manage</c>, con clave y motivo): sólo desde <c>InProcess</c> (<c>Lending.InsuranceClaim.NotInProcess</c>) y sólo a
/// <c>Denied</c> o <c>Withdrawn</c>: <c>Paid</c> lo pone el contrato cuando la aseguradora paga. Cerrada, la suspensión del cobro se levanta. (nuevo)
/// </summary>
public sealed record ChangeInsuranceClaimStatusCommand(Guid ClaimPublicId, InsuranceClaimStatus Status, string Reason, Guid? SupportAttachmentPublicId = null)
    : IRequest<Result<InsuranceClaimDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ChangeInsuranceClaimStatusCommandValidator : ValidadorConMotivo<ChangeInsuranceClaimStatusCommand>
{
    public ChangeInsuranceClaimStatusCommandValidator()
    {
        RuleFor(x => x.ClaimPublicId).NotEmpty();
        RuleFor(x => x.Status).Must(s => s is InsuranceClaimStatus.Denied or InsuranceClaimStatus.Withdrawn)
            .WithMessage("Una reclamación se niega o se desiste; el pago de la aseguradora la cierra solo.");
    }
}

public sealed class ChangeInsuranceClaimStatusCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ChangeInsuranceClaimStatusCommand, Result<InsuranceClaimDto>>
{
    public async Task<Result<InsuranceClaimDto>> Handle(ChangeInsuranceClaimStatusCommand request, CancellationToken ct)
    {
        var reclamacion = await db.InsuranceClaims.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.ClaimPublicId && !c.IsDeleted, ct);
        if (reclamacion is null) return Falla(ErroresDeEventosDelDeudor.ReclamacionInexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == reclamacion.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeEventosDelDeudor.ReclamacionInexistente(), auditoria,
            "InsuranceClaim", reclamacion.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);
            var viva = await db.InsuranceClaims.FirstAsync(c => c.Id == reclamacion.Id, ct);
            if (viva.Status != InsuranceClaimStatus.InProcess) return Falla(ErroresDeEventosDelDeudor.ReclamacionNoEnTramite(viva.Status));
            viva.Status = request.Status;
            viva.ClosedAt = reloj.UtcNow;
            viva.CloseReason = request.Reason.Trim().Length <= 500 ? request.Reason.Trim() : request.Reason.Trim()[..500];
            await db.SaveChangesAsync(ct);
            return Result.Success((await ConsultasDeEventosDelDeudor.ReclamacionesAsync(db, [viva], ct))[0]);
        }, ct);
    }

    private static Result<InsuranceClaimDto> Falla(Error e) => Result.Failure<InsuranceClaimDto>(e);
}

/// <summary>
/// Los procesos de insolvencia (feature 013, K3, T0885; api.md §11.4, <c>GET /api/lending/insolvency-proceedings?status=&amp;personPublicId=&amp;page=</c>,
/// <c>Lending.Loans.Read</c>): los más recientes primero. El proceso es de la persona, no de una oficina. (nuevo)
/// </summary>
public sealed record ListInsolvencyProceedingsQuery(InsolvencyProceedingStatus? Status = null, Guid? PersonPublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<InsolvencyProceedingDto>>>;

public sealed class ListInsolvencyProceedingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListInsolvencyProceedingsQuery, Result<PagedResult<InsolvencyProceedingDto>>>
{
    public async Task<Result<PagedResult<InsolvencyProceedingDto>>> Handle(ListInsolvencyProceedingsQuery request, CancellationToken ct)
    {
        var consulta = db.InsolvencyProceedings.AsNoTracking().Where(p => !p.IsDeleted);
        if (request.Status is { } estado) consulta = consulta.Where(p => p.Status == estado);
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(p => db.People.Any(x => x.Id == p.PersonId && x.PublicId == persona));
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(p => p.StartDate).ThenByDescending(p => p.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<InsolvencyProceedingDto>(await ConsultasDeEventosDelDeudor.ProcesosAsync(db, filas, ct), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}

/// <summary>
/// Abre un proceso de insolvencia (feature 013, K3, T0885; api.md §11.4, <c>POST /api/lending/insolvency-proceedings</c>, <c>Lending.Insolvency.Manage</c>,
/// con clave; FR-098, T102): en <c>Negotiating</c> desde <c>startDate</c>, con régimen y autoridad (C64); uno en negociación por persona
/// (<c>Lending.Insolvency.AlreadyOpen</c>). Suspende causación (la reconstrucción lee el proceso), cobro, mensajes y jurídico de todos los créditos del
/// deudor y mantiene la calificación; bloquea crédito nuevo con un bloqueo <c>Insolvency</c>; los mensajes de cobro en cola se cancelan en la misma
/// transacción. (nuevo)
/// </summary>
public sealed record OpenInsolvencyProceedingCommand(
    Guid PersonPublicId,
    DateOnly StartDate,
    InsolvencyRegime Regime,
    string Authority,
    string? FileNumber,
    Guid? SupportAttachmentPublicId) : IRequest<Result<InsolvencyProceedingDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class OpenInsolvencyProceedingCommandValidator : AbstractValidator<OpenInsolvencyProceedingCommand>
{
    public OpenInsolvencyProceedingCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.Regime).IsInEnum().WithMessage("Diga el régimen: reorganización empresarial o negociación de deudas de persona natural.");
        RuleFor(x => x.Authority).NotEmpty().WithMessage("Diga ante quién se tramita: juzgado, Superintendencia o centro de conciliación.").MaximumLength(200);
        RuleFor(x => x.FileNumber).MaximumLength(60);
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el auto de admisión o el soporte del proceso.");
    }
}

public sealed class OpenInsolvencyProceedingCommandHandler(
    IApplicationDbContext db,
    BloqueosDelDeudor bloqueos,
    ColaDeMensajesAlDeudor cola,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<OpenInsolvencyProceedingCommand, Result<InsolvencyProceedingDto>>
{
    /// <summary>El disparador del bloqueo de un proceso de insolvencia.</summary>
    public static string Disparador(Guid publicId) => $"Insolvency:{publicId:N}";

    public async Task<Result<InsolvencyProceedingDto>> Handle(OpenInsolvencyProceedingCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        if (request.StartDate > reloj.HoyLocal)
            return Falla(ErroresDeEventosDelDeudor.Invalido("startDate", "La negociación no puede empezar en una fecha futura."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([persona.Id], ModoDeEspera.Esperar, ct);
            var abierto = await db.InsolvencyProceedings.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonId == persona.Id && p.Status == InsolvencyProceedingStatus.Negotiating && !p.IsDeleted, ct);
            if (abierto is not null) return Falla(ErroresDeEventosDelDeudor.InsolvenciaAbierta(abierto.PublicId, abierto.StartDate));

            var proceso = new InsolvencyProceeding
            {
                PersonId = persona.Id,
                Regime = request.Regime,
                Authority = request.Authority.Trim(),
                CaseNumber = string.IsNullOrWhiteSpace(request.FileNumber) ? null : request.FileNumber.Trim(),
                StartDate = request.StartDate,
                SupportAttachmentPublicId = request.SupportAttachmentPublicId,
                Status = InsolvencyProceedingStatus.Negotiating,
            };
            db.InsolvencyProceedings.Add(proceso);
            if (await bloqueos.VigenteAsync(persona.Id, DebtorBlockReason.Insolvency, Disparador(proceso.PublicId), ct) is null)
                db.DebtorBlocks.Add(DebtorBlock.Nuevo(persona.Id, DebtorBlockReason.Insolvency, Disparador(proceso.PublicId), request.StartDate,
                    $"Proceso de insolvencia ante {proceso.Authority}", usuario, soporte: request.SupportAttachmentPublicId));
            var creditos = await db.Loans.AsNoTracking().Where(l => l.DebtorPersonId == persona.Id && !l.IsDeleted).Select(l => l.Id).ToListAsync(ct);
            await cola.CancelarCobrosDelDeudorAsync(persona.Id, creditos, "Proceso de insolvencia en negociación", actor.Kind, usuario, ct);
            await db.SaveChangesAsync(ct);
            // K4 (T0947; FR-098, FR-124): la insolvencia suspende el cobro jurídico abierto del deudor, dejándose dicha.
            if (await Collections.Legal.CobroJuridicoDeCartera.SuspenderPorInsolvenciaAsync(db, persona.Id, proceso.Id, ct) > 0) await db.SaveChangesAsync(ct);
            return Result.Success((await ConsultasDeEventosDelDeudor.ProcesosAsync(db, [proceso], ct))[0]);
        }, ct);
    }

    private static Result<InsolvencyProceedingDto> Falla(Error e) => Result.Failure<InsolvencyProceedingDto>(e);
}

/// <summary>
/// Registra el fracaso de la negociación (feature 013, K3, T0885; api.md §11.4, <c>POST /api/lending/insolvency-proceedings/{id}/failure</c>,
/// <c>Lending.Insolvency.Manage</c>, con clave y motivo; FR-098): <c>Negotiating</c> → <c>Failed</c> desde <c>date</c>; desde otro estado,
/// <c>Lending.Insolvency.NotNegotiating</c>. Desde esa fecha la causación se reanuda y rige la calificación que fija la norma. Lo migrado sin régimen o
/// autoridad se completa aquí (C64). El bloqueo <c>Insolvency</c> de crédito nuevo <b>se mantiene</b> (decisión del dueño del 2026-10-15, bloque D): lo
/// levanta a mano una persona autorizada, con motivo, por el flujo de bloqueos; sólo el acuerdo aplicado como novedad lo levanta solo
/// (<see cref="CierreDeInsolvencia"/>). (nuevo)
/// </summary>
public sealed record RecordInsolvencyFailureCommand(
    Guid ProceedingPublicId,
    DateOnly Date,
    string Reason,
    Guid? SupportAttachmentPublicId,
    InsolvencyRegime? Regime = null,
    string? Authority = null) : IRequest<Result<InsolvencyProceedingDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RecordInsolvencyFailureCommandValidator : ValidadorConMotivo<RecordInsolvencyFailureCommand>
{
    public RecordInsolvencyFailureCommandValidator()
    {
        RuleFor(x => x.ProceedingPublicId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Regime).IsInEnum().When(x => x.Regime is not null);
        RuleFor(x => x.Authority).MaximumLength(200);
    }
}

public sealed class RecordInsolvencyFailureCommandHandler(IApplicationDbContext db, ICerrojoDeCartera cerrojo, IDateTimeService reloj)
    : IRequestHandler<RecordInsolvencyFailureCommand, Result<InsolvencyProceedingDto>>
{
    public async Task<Result<InsolvencyProceedingDto>> Handle(RecordInsolvencyFailureCommand request, CancellationToken ct)
    {
        var leido = await db.InsolvencyProceedings.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.ProceedingPublicId && !p.IsDeleted, ct);
        if (leido is null) return Falla(ErroresDeEventosDelDeudor.InsolvenciaInexistente());
        if (request.Date > reloj.HoyLocal) return Falla(ErroresDeEventosDelDeudor.Invalido("date", "La fecha del fracaso no puede ser futura."));
        if (request.Date < leido.StartDate)
            return Falla(ErroresDeEventosDelDeudor.Invalido("date", "La negociación no fracasa antes de empezar."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([leido.PersonId], ModoDeEspera.Esperar, ct);
            var proceso = await db.InsolvencyProceedings.FirstAsync(p => p.Id == leido.Id, ct);
            if (proceso.Status != InsolvencyProceedingStatus.Negotiating) return Falla(ErroresDeEventosDelDeudor.InsolvenciaNoNegociando(proceso.Status));
            // C64: lo migrado sin régimen o autoridad se completa al tramitarlo.
            proceso.Regime ??= request.Regime;
            if (string.IsNullOrWhiteSpace(proceso.Authority) && !string.IsNullOrWhiteSpace(request.Authority)) proceso.Authority = request.Authority.Trim();
            if (proceso.Regime is null)
                return Falla(ErroresDeEventosDelDeudor.Invalido("regime", "El proceso migrado no tiene régimen: dígalo para registrar el fracaso."));
            if (string.IsNullOrWhiteSpace(proceso.Authority))
                return Falla(ErroresDeEventosDelDeudor.Invalido("authority", "El proceso migrado no tiene autoridad: dígala para registrar el fracaso."));
            proceso.SupportAttachmentPublicId ??= request.SupportAttachmentPublicId;
            try
            {
                proceso.RegistrarFracaso(request.Date, reloj.UtcNow, request.Reason);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            // Decisión del dueño (2026-10-15): el fracaso no levanta el bloqueo Insolvency; lo levanta una persona autorizada, con motivo, por el flujo
            // de bloqueos.
            await db.SaveChangesAsync(ct);
            return Result.Success((await ConsultasDeEventosDelDeudor.ProcesosAsync(db, [proceso], ct))[0]);
        }, ct);
    }

    private static Result<InsolvencyProceedingDto> Falla(Error e) => Result.Failure<InsolvencyProceedingDto>(e);
}

/// <summary>
/// Lo que pasa con los bloqueos al terminar una negociación de insolvencia (feature 013, K3, bloque B2; FR-098): por el acuerdo aplicado como novedad
/// (<c>ApplyLoanNoveltyCommand</c>) el bloqueo
/// <c>Insolvency</c> de crédito nuevo de la persona se levanta —el abierto por pantalla y el migrado—, sin guardar. Hay a lo sumo una negociación
/// abierta por persona (<c>Lending.Insolvency.AlreadyOpen</c>). El fracaso no lo levanta (decisión del dueño del 2026-10-15): lo levanta una persona
/// autorizada por el flujo de bloqueos. (nuevo)
/// </summary>
public static class CierreDeInsolvencia
{
    public static async Task<int> LevantarBloqueosAsync(IApplicationDbContext db, int personaId, DateOnly fecha, DateTime instante, int? usuarioId, string motivo,
        CancellationToken ct)
    {
        var vigentes = await db.DebtorBlocks
            .Where(b => b.PersonId == personaId && b.Reason == DebtorBlockReason.Insolvency && b.LiftedAt == null && !b.IsDeleted).ToListAsync(ct);
        var texto = motivo.Length <= 500 ? motivo : motivo[..500];
        foreach (var bloqueo in vigentes) bloqueo.Levantar(fecha, instante, usuarioId, texto);
        return vigentes.Count;
    }
}

/// <summary>
/// Las reclamaciones al seguro de vida deudores en trámite (feature 013, K3, bloque B2; FR-097, T102): mientras una está <c>InProcess</c> no hay cobranza,
/// mensajes de cobro ni <b>reporte negativo</b>. La cola de mensajes la consulta aquí y la proyección a centrales de K5 (T1003, exclusión
/// <c>DeceasedClaimInProcess</c>) debe consultarla aquí también: la reclamación cuenta por el crédito o por el deudor fallecido, migrada o registrada
/// por pantalla. (nuevo)
/// </summary>
public static class ReclamacionesEnTramite
{
    /// <summary>¿Hay una reclamación en trámite por el deudor fallecido <paramref name="deudorId"/> o por el crédito <paramref name="loanId"/>?</summary>
    public static Task<bool> HayAsync(IApplicationDbContext db, int deudorId, int? loanId, CancellationToken ct) =>
        db.InsuranceClaims.AsNoTracking().AnyAsync(c => !c.IsDeleted && c.Status == InsuranceClaimStatus.InProcess
                                                        && (c.DeceasedPersonId == deudorId || (loanId != null && c.LoanId == loanId)), ct);

    /// <summary>De los créditos de <paramref name="loanIds"/>, los que tienen reclamación en trámite (por el crédito o por su deudor fallecido).</summary>
    public static async Task<IReadOnlySet<int>> CreditosAsync(IApplicationDbContext db, IReadOnlyCollection<int> loanIds, CancellationToken ct)
    {
        var ids = loanIds.Distinct().ToList();
        if (ids.Count == 0) return new HashSet<int>();
        var deudores = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.Id)).Select(l => new { l.Id, l.DebtorPersonId }).ToListAsync(ct);
        var personas = deudores.Select(d => d.DebtorPersonId).Distinct().ToList();
        var reclamaciones = await db.InsuranceClaims.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == InsuranceClaimStatus.InProcess && (ids.Contains(c.LoanId) || personas.Contains(c.DeceasedPersonId)))
            .Select(c => new { c.LoanId, c.DeceasedPersonId }).ToListAsync(ct);
        var porCredito = reclamaciones.Select(r => r.LoanId).ToHashSet();
        var porPersona = reclamaciones.Select(r => r.DeceasedPersonId).ToHashSet();
        return deudores.Where(d => porCredito.Contains(d.Id) || porPersona.Contains(d.DebtorPersonId)).Select(d => d.Id).ToHashSet();
    }
}
