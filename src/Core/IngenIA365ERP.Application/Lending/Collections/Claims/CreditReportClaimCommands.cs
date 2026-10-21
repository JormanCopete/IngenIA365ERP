using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Compliance.DataSubjectRequests;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Claims;

// Feature 013, K4 (US12, T0951; api.md §14.4; data-model §26.12; FR-006, FR-137; T69): las escrituras de los reclamos y las suplantaciones sobre el
// reporte a centrales —registrar, prorrogar una vez, responder y retirar—; la lectura es de K1. Los plazos son días hábiles nacionales con los límites
// del titular (PlazosDelTitular, ICalendarioHabil): ningún plazo está en el código. Mientras está en trámite, el archivo de centrales lleva la marca
// «reclamo en trámite» (K5). (nuevo)

/// <summary>Lo común de los reclamos: leer con alcance, los plazos y la proyección. (nuevo)</summary>
public static class ReclamosSobreElReporte
{
    /// <summary>La clase del titular con que se cuentan los plazos de un reclamo sobre el reporte: la de reclamo (Ley 1266).</summary>
    public const DataSubjectRequestKind ClaseDePlazo = DataSubjectRequestKind.Rectification;

    /// <summary>El reclamo del alcance: con crédito, por su oficina y su línea; sin crédito, sólo con todas las oficinas (falla cerrado).</summary>
    public static async Task<Result<CreditReportClaim>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, Guid publicId,
        CancellationToken ct)
    {
        var reclamo = await db.CreditReportClaims.FirstOrDefaultAsync(c => c.PublicId == publicId && !c.IsDeleted, ct);
        if (reclamo is null) return Result.Failure<CreditReportClaim>(ErroresDeCobranza.ReclamoInexistente());
        var dentro = await AsegurarAsync(db, alcance, auditoria, reclamo.LoanId, reclamo.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<CreditReportClaim>(dentro.Error) : Result.Success(reclamo);
    }

    public static async Task<Result> AsegurarAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, int? loanId, Guid recurso,
        CancellationToken ct)
    {
        if (loanId is { } id)
        {
            var credito = await db.Loans.AsNoTracking().Where(l => l.Id == id).Select(l => new { l.BranchId, l.CreditLineId }).FirstAsync(ct);
            return await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeCobranza.ReclamoInexistente(), auditoria, "CreditReportClaim", recurso,
                ct);
        }
        if ((await alcance.ObtenerAsync(ct)).TodasLasOficinas) return Result.Success();
        await auditoria.EmitirFueraDeAlcanceAsync("CreditReportClaim", recurso, ct);
        return Result.Failure(ErroresDeCobranza.ReclamoInexistente());
    }

    public static async Task<CreditReportClaimDto> DtoAsync(IApplicationDbContext db, CreditReportClaim c, CancellationToken ct)
    {
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, [c.PersonId], ct);
        var credito = c.LoanId is { } id
            ? await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == id).Select(l => new CreditoDelReclamoDto(l.PublicId, l.Number)).FirstOrDefaultAsync(ct)
            : null;
        return new CreditReportClaimDto(c.PublicId, personas[c.PersonId], credito, c.Kind, c.Status, c.BureauCode, c.ReceivedOn, c.ExtendedDueDate ?? c.DueDate,
            c.ExtendedDueDate is not null);
    }

    /// <summary>¿Los soportes son de la persona y están disponibles? (adjuntos <c>LendingCreditReportClaimSupport</c> de la persona)</summary>
    public static async Task<bool> SoportesValidosAsync(IApplicationDbContext db, Guid personaPublicId, IReadOnlyCollection<Guid> adjuntos, CancellationToken ct) =>
        adjuntos.Count == 0 || await db.Attachments.AsNoTracking().CountAsync(a => adjuntos.Contains(a.PublicId)
            && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeReclamoSobreElReporte && a.OwnerEntityPublicId == personaPublicId
            && a.Status == EstadoDeAdjunto.Available, ct) == adjuntos.Count;
}

// ------------------------------------------------------------------------------------------------------------- registrar --

/// <summary>
/// Registra un reclamo o una suplantación sobre el reporte a centrales (feature 013, K4, T0951; api.md §14.4, <c>POST /api/lending/credit-report-claims</c>,
/// <c>Lending.CreditReportClaims.Manage</c>, con clave; FR-006, T69): de una persona, con o sin crédito (del alcance; sin crédito sólo con todas las
/// oficinas), la fecha de recibo, el medio, la descripción y sus soportes (<c>LendingCreditReportClaimSupport</c> de la persona). Vence en los días hábiles
/// nacionales del reclamo contados desde el recibo (<see cref="PlazosDelTitular"/>, <see cref="ICalendarioHabil"/>); si entró como solicitud del titular,
/// la enlaza. Nace <c>InProcess</c>: desde ahí lleva la marca «reclamo en trámite». (nuevo)
/// </summary>
public sealed record RegisterCreditReportClaimCommand(
    Guid PersonPublicId,
    CreditReportClaimKind Kind,
    DateOnly ReceivedAt,
    string Channel,
    string Description,
    Guid? LoanPublicId = null,
    IReadOnlyList<Guid>? AttachmentPublicIds = null,
    string? BureauCode = null,
    Guid? DataSubjectRequestPublicId = null) : IRequest<Result<CreditReportClaimDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterCreditReportClaimCommandValidator : AbstractValidator<RegisterCreditReportClaimCommand>
{
    public RegisterCreditReportClaimCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Channel).NotEmpty().MaximumLength(30).WithMessage("Indique el medio por el que llegó el reclamo.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000).WithMessage("Describa el reclamo (hasta 2.000 caracteres).");
        RuleFor(x => x.BureauCode).MaximumLength(20);
    }
}

public sealed class RegisterCreditReportClaimCommandHandler(
    IApplicationDbContext db,
    IValoresNacionales valores,
    ICalendarioHabil calendario,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterCreditReportClaimCommand, Result<CreditReportClaimDto>>
{
    public async Task<Result<CreditReportClaimDto>> Handle(RegisterCreditReportClaimCommand request, CancellationToken ct)
    {
        if (request.ReceivedAt > reloj.HoyLocal)
            return Falla(new ErrorConDatos("Validation.CreditReportClaim.ReceivedAt", "El reclamo no se puede recibir en una fecha futura.", new { request.ReceivedAt }));
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        int? loanId = null;
        if (request.LoanPublicId is { } loan)
        {
            var leido = await CobranzaDeCartera.CreditoAsync(db, alcance, auditoria, loan, ct);
            if (leido.IsFailure) return Falla(leido.Error);
            if (leido.Value.DebtorPersonId != persona
                && !await db.LoanParties.AsNoTracking().AnyAsync(p => p.LoanId == leido.Value.Id && p.PersonId == persona && p.Role == CreditPartyRole.CoDebtor, ct))
                return Falla(ErroresDeMensajesAlDeudor.DestinatarioNoObligado());
            loanId = leido.Value.Id;
        }
        else
        {
            var dentro = await ReclamosSobreElReporte.AsegurarAsync(db, alcance, auditoria, null, request.PersonPublicId, ct);
            if (dentro.IsFailure) return Falla(dentro.Error);
        }
        var adjuntos = request.AttachmentPublicIds?.Distinct().ToList() ?? [];
        if (!await ReclamosSobreElReporte.SoportesValidosAsync(db, request.PersonPublicId, adjuntos, ct)) return Falla(ErroresDeCobranza.SoporteDeReclamoInvalido());
        int? solicitud = null;
        if (request.DataSubjectRequestPublicId is { } s)
        {
            solicitud = await db.DataSubjectRequests.AsNoTracking().Where(r => r.PublicId == s && r.PersonId == persona).Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
            if (solicitud is null) return Falla(new Error("Compliance.DataSubjectRequest.NotFound", "La solicitud del titular no existe o es de otra persona."));
        }
        var plazos = await LecturaDePlazos.LeerAsync(valores, calendario, request.ReceivedAt, ct);
        if (plazos.IsFailure) return Falla(plazos.Error);
        var reclamo = new CreditReportClaim
        {
            PersonId = persona.Value,
            LoanId = loanId,
            Kind = request.Kind,
            Status = CreditReportClaimStatus.InProcess,
            BureauCode = string.IsNullOrWhiteSpace(request.BureauCode) ? null : request.BureauCode.Trim().ToUpperInvariant(),
            ReceivedOn = request.ReceivedAt,
            Channel = request.Channel.Trim(),
            Description = request.Description.Trim(),
            DueDate = PlazosDelTitular.Vencimiento(ReclamosSobreElReporte.ClaseDePlazo, request.ReceivedAt, plazos.Value.Limites, plazos.Value.Calendario),
            DataSubjectRequestId = solicitud,
        };
        db.CreditReportClaims.Add(reclamo);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ReclamosSobreElReporte.DtoAsync(db, reclamo, ct));
    }

    private static Result<CreditReportClaimDto> Falla(Error e) => Result.Failure<CreditReportClaimDto>(e);
}

// ------------------------------------------------------------------------------------------- prorrogar, responder, retirar --

/// <summary>
/// Prorroga un reclamo en trámite (feature 013, K4, T0951; api.md §14.4, <c>POST /api/lending/credit-report-claims/{id}/extend</c>,
/// <c>Lending.CreditReportClaims.Manage</c>, con clave y motivo): una sola vez (<c>Lending.CreditReportClaim.AlreadyExtended</c>), los días hábiles de la
/// prórroga sobre el vencimiento, y hoy como día en que se le informa al titular. Fuera de trámite, <c>.NotInProcess</c>. (nuevo)
/// </summary>
public sealed record ExtendCreditReportClaimCommand(Guid ClaimPublicId, string Reason) : IRequest<Result<CreditReportClaimDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ExtendCreditReportClaimCommandValidator : ValidadorConMotivo<ExtendCreditReportClaimCommand>
{
    public ExtendCreditReportClaimCommandValidator() => RuleFor(x => x.ClaimPublicId).NotEmpty();
}

public sealed class ExtendCreditReportClaimCommandHandler(
    IApplicationDbContext db,
    IValoresNacionales valores,
    ICalendarioHabil calendario,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ExtendCreditReportClaimCommand, Result<CreditReportClaimDto>>
{
    public async Task<Result<CreditReportClaimDto>> Handle(ExtendCreditReportClaimCommand request, CancellationToken ct)
    {
        var leido = await ReclamosSobreElReporte.LeerAsync(db, alcance, auditoria, request.ClaimPublicId, ct);
        if (leido.IsFailure) return Result.Failure<CreditReportClaimDto>(leido.Error);
        var reclamo = leido.Value;
        if (reclamo.Status != CreditReportClaimStatus.InProcess)
            return Result.Failure<CreditReportClaimDto>(ErroresDeCobranza.ReclamoNoEnTramite(reclamo.Status.ToString()));
        if (reclamo.ExtendedDueDate is { } ya) return Result.Failure<CreditReportClaimDto>(ErroresDeCobranza.ReclamoYaProrrogado(ya));
        var plazos = await LecturaDePlazos.LeerAsync(valores, calendario, reclamo.ReceivedOn, ct);
        if (plazos.IsFailure) return Result.Failure<CreditReportClaimDto>(plazos.Error);
        reclamo.ExtendedDueDate = PlazosDelTitular.Prorroga(ReclamosSobreElReporte.ClaseDePlazo, reclamo.DueDate, yaProrrogada: false, plazos.Value.Limites,
            plazos.Value.Calendario);
        reclamo.ExtensionNotifiedOn = reloj.HoyLocal;
        await db.SaveChangesAsync(ct);
        return Result.Success(await ReclamosSobreElReporte.DtoAsync(db, reclamo, ct));
    }
}

/// <summary>
/// Responde un reclamo en trámite (feature 013, K4, T0951; api.md §14.4, <c>POST /api/lending/credit-report-claims/{id}/answer</c>,
/// <c>Lending.CreditReportClaims.Manage</c>, con clave): el resumen de la respuesta, si se le da la razón al titular y el soporte de la respuesta
/// (<c>LendingCreditReportClaimSupport</c> de la persona); pasa a <c>Answered</c> hoy. Fuera de trámite, <c>.NotInProcess</c>. (nuevo)
/// </summary>
public sealed record AnswerCreditReportClaimCommand(Guid ClaimPublicId, string Answer, bool UpholdsClaim, Guid? AttachmentPublicId = null)
    : IRequest<Result<CreditReportClaimDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AnswerCreditReportClaimCommandValidator : AbstractValidator<AnswerCreditReportClaimCommand>
{
    public AnswerCreditReportClaimCommandValidator()
    {
        RuleFor(x => x.ClaimPublicId).NotEmpty();
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(2000).WithMessage("Resuma la respuesta al titular (hasta 2.000 caracteres).");
    }
}

public sealed class AnswerCreditReportClaimCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<AnswerCreditReportClaimCommand, Result<CreditReportClaimDto>>
{
    public async Task<Result<CreditReportClaimDto>> Handle(AnswerCreditReportClaimCommand request, CancellationToken ct)
    {
        var leido = await ReclamosSobreElReporte.LeerAsync(db, alcance, auditoria, request.ClaimPublicId, ct);
        if (leido.IsFailure) return Result.Failure<CreditReportClaimDto>(leido.Error);
        var reclamo = leido.Value;
        if (reclamo.Status != CreditReportClaimStatus.InProcess)
            return Result.Failure<CreditReportClaimDto>(ErroresDeCobranza.ReclamoNoEnTramite(reclamo.Status.ToString()));
        if (request.AttachmentPublicId is { } adjunto)
        {
            var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == reclamo.PersonId).Select(p => p.PublicId).FirstAsync(ct);
            if (!await ReclamosSobreElReporte.SoportesValidosAsync(db, persona, [adjunto], ct))
                return Result.Failure<CreditReportClaimDto>(ErroresDeCobranza.SoporteDeReclamoInvalido());
        }
        reclamo.Status = CreditReportClaimStatus.Answered;
        reclamo.AnsweredOn = reloj.HoyLocal;
        reclamo.AnswerSummary = request.Answer.Trim();
        reclamo.AnswerUpholdsClaim = request.UpholdsClaim;
        reclamo.AnswerAttachmentPublicId = request.AttachmentPublicId;
        await db.SaveChangesAsync(ct);
        return Result.Success(await ReclamosSobreElReporte.DtoAsync(db, reclamo, ct));
    }
}

/// <summary>
/// Retira un reclamo en trámite a pedido del titular (feature 013, K4, T0951; api.md §14.4, <c>POST /api/lending/credit-report-claims/{id}/withdraw</c>,
/// <c>Lending.CreditReportClaims.Manage</c>, con clave y motivo): pasa a <c>Withdrawn</c> hoy. Fuera de trámite, <c>.NotInProcess</c>. (nuevo)
/// </summary>
public sealed record WithdrawCreditReportClaimCommand(Guid ClaimPublicId, string Reason) : IRequest<Result<CreditReportClaimDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class WithdrawCreditReportClaimCommandValidator : ValidadorConMotivo<WithdrawCreditReportClaimCommand>
{
    public WithdrawCreditReportClaimCommandValidator() => RuleFor(x => x.ClaimPublicId).NotEmpty();
}

public sealed class WithdrawCreditReportClaimCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<WithdrawCreditReportClaimCommand, Result<CreditReportClaimDto>>
{
    public async Task<Result<CreditReportClaimDto>> Handle(WithdrawCreditReportClaimCommand request, CancellationToken ct)
    {
        var leido = await ReclamosSobreElReporte.LeerAsync(db, alcance, auditoria, request.ClaimPublicId, ct);
        if (leido.IsFailure) return Result.Failure<CreditReportClaimDto>(leido.Error);
        var reclamo = leido.Value;
        if (reclamo.Status != CreditReportClaimStatus.InProcess)
            return Result.Failure<CreditReportClaimDto>(ErroresDeCobranza.ReclamoNoEnTramite(reclamo.Status.ToString()));
        reclamo.Status = CreditReportClaimStatus.Withdrawn;
        reclamo.WithdrawnOn = reloj.HoyLocal;
        await db.SaveChangesAsync(ct);
        return Result.Success(await ReclamosSobreElReporte.DtoAsync(db, reclamo, ct));
    }
}

/// <summary>
/// La aceptación tácita de los reclamos vencidos sin respuesta (feature 013, K4, T0943; data-model §26.12: «TacitlyAccepted lo escribe el ciclo diario
/// (paso 7) con alerta»): los reclamos en trámite cuyo vencimiento (el prorrogado si lo hay) pasó antes de <paramref name="dia"/> pasan a
/// <c>TacitlyAccepted</c> ese día. Sin guardar. Devuelve los aceptados. (nuevo)
/// </summary>
public static class AceptacionTacita
{
    public static async Task<IReadOnlyList<CreditReportClaim>> AplicarAsync(IApplicationDbContext db, DateOnly dia, CancellationToken ct)
    {
        var vencidos = await db.CreditReportClaims.Where(c => !c.IsDeleted && c.Status == CreditReportClaimStatus.InProcess
            && (c.ExtendedDueDate ?? c.DueDate) < dia).ToListAsync(ct);
        foreach (var c in vencidos)
        {
            c.Status = CreditReportClaimStatus.TacitlyAccepted;
            c.TacitAcceptanceOn = dia;
        }
        return vencidos;
    }
}
