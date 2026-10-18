using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Withdrawals;

// Feature 014, US10 (T097, T098; api.md §11; data-model §6.1; FR-005, FR-076, FR-124, D16, R15): el trámite del retiro del asociado vive en la Cartera.
// El analista de aportes lo radica con su causa y soportes; se puede desistir antes de aprobarse; el órgano (consejo o junta) lo decide en una sesión con
// acta y quien registra la decisión no es quien lo radicó; al aprobarse se emite RetiroDeAsociadoAprobado a Asociados en el mismo guardado. (nuevo)

/// <summary>
/// <c>POST /api/lending/membership-withdrawals</c> (<c>FileMembershipWithdrawalCommand</c>, <c>Lending.MembershipWithdrawals.File</c>): radica el retiro de
/// un asociado vigente con cuenta de aportes viva. Una solicitud viva por persona (<c>Lending.MembershipWithdrawal.AlreadyInProgress</c>); sin aportes
/// activados, <c>Lending.Contributions.NotActivated</c>. (nuevo)
/// </summary>
public sealed record FileMembershipWithdrawalCommand(
    Guid PersonPublicId,
    MembershipWithdrawalCause Cause,
    string? CauseDetail,
    DateOnly? RequestedOn,
    IReadOnlyList<Guid>? SupportAttachmentPublicIds,
    string? Note) : IRequest<Result<MembershipWithdrawalDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary><c>POST …/{id}/desist</c> (<c>DesistMembershipWithdrawalCommand</c>, <c>Lending.MembershipWithdrawals.File</c>): sólo antes de aprobar. (nuevo)</summary>
public sealed record DesistMembershipWithdrawalCommand(Guid WithdrawalPublicId, string Reason)
    : IRequest<Result<MembershipWithdrawalDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>Lo que decidió el órgano sobre el retiro.</summary>
public enum DecisionSobreRetiro
{
    Approve = 1,
    Reject = 2,
}

/// <summary>
/// <c>POST …/{id}/approve</c> y <c>…/reject</c> (<c>DecideMembershipWithdrawalCommand</c>, <c>Lending.MembershipWithdrawals.Approve</c>): registra lo que
/// decidió el órgano con el punto del acta cerrada (sujeto <c>MembershipWithdrawal</c> y este trámite; si no, <c>Lending.Session.ItemMismatch</c>); quien
/// lo radicó no lo decide (<c>Lending.Segregation.Conflict</c>). Aprobar fija la fecha efectiva y emite <c>RetiroDeAsociadoAprobado</c> en el mismo
/// guardado. (nuevo)
/// </summary>
public sealed record DecideMembershipWithdrawalCommand(Guid WithdrawalPublicId, DecisionSobreRetiro Decision, Guid SessionItemPublicId,
    DateOnly? EffectiveDate, string Reason) : IRequest<Result<MembershipWithdrawalDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class FileMembershipWithdrawalCommandValidator : AbstractValidator<FileMembershipWithdrawalCommand>
{
    public FileMembershipWithdrawalCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.Cause).IsInEnum();
        RuleFor(x => x.CauseDetail).NotEmpty().WithMessage("Con la causa «otra», indique cuál.").When(x => x.Cause == MembershipWithdrawalCause.Other);
        RuleFor(x => x.CauseDetail).MaximumLength(MembershipWithdrawal.LargoDelDetalle);
        RuleFor(x => x.Note).MaximumLength(MembershipWithdrawal.LargoDelDetalle);
        RuleFor(x => x.SupportAttachmentPublicIds).Must(s => s is null || s.Count <= 20).WithMessage("A lo sumo 20 soportes.");
    }
}

public sealed class DesistMembershipWithdrawalCommandValidator : ValidadorConMotivo<DesistMembershipWithdrawalCommand>
{
    public DesistMembershipWithdrawalCommandValidator() => RuleFor(x => x.WithdrawalPublicId).NotEmpty();
}

public sealed class DecideMembershipWithdrawalCommandValidator : ValidadorConMotivo<DecideMembershipWithdrawalCommand>
{
    public DecideMembershipWithdrawalCommandValidator()
    {
        RuleFor(x => x.WithdrawalPublicId).NotEmpty();
        RuleFor(x => x.SessionItemPublicId).NotEmpty();
        RuleFor(x => x.Decision).IsInEnum();
        RuleFor(x => x.EffectiveDate).NotNull().WithMessage("Indique la fecha del retiro.").When(x => x.Decision == DecisionSobreRetiro.Approve);
    }
}

public sealed class FileMembershipWithdrawalCommandHandler(
    IApplicationDbContext db,
    RetirosDeAsociado retiros,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    RegistroDeIntervenciones intervenciones,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<FileMembershipWithdrawalCommand, Result<MembershipWithdrawalDto>>
{
    public async Task<Result<MembershipWithdrawalDto>> Handle(FileMembershipWithdrawalCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if ((await ActivacionesDeCaptacion.LeerAsync(db, ct)).Aportes is null) return Falla(ErroresDeCuentas.SinActivar(LineNature.Contribution));
        var fecha = request.RequestedOn ?? hoy;
        if (fecha > hoy) return Falla(ErroresDeRetiro.FechaEfectiva(fecha, hoy));

        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        var afiliacion = await PersonaDeOriginacion.AfiliacionAsync(db, persona, hoy, ct);
        if (!afiliacion.Activo) return Falla(ErroresDeRetiro.NoAsociado("la persona no es asociada vigente"));
        var cuenta = await retiros.CuentaDeAportesAsync(persona.Id, ct);
        if (cuenta is null) return Falla(ErroresDeRetiro.NoAsociado("la persona no tiene una cuenta de aportes viva"));
        var dentro = await alcance.AsegurarAsync(cuenta.BranchId, cuenta.CreditLineId, ErroresDeRetiro.Inexistente(), auditoria, RetirosDeAsociado.Expediente,
            null, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([persona.Id], ModoDeEspera.Esperar, ct);
            var vivo = await db.MembershipWithdrawals.AsNoTracking()
                .Where(w => w.PersonId == persona.Id && !w.IsDeleted && MembershipWithdrawal.Vivos.Contains(w.Status))
                .Select(w => new { w.Number, w.Status }).FirstOrDefaultAsync(ct);
            if (vivo is not null) return Falla(ErroresDeRetiro.EnCurso(vivo.Number, vivo.Status));

            var numero = await numerador.NumeroDeProcesoDeCaptacionesAsync(LendingNumberKind.MembershipWithdrawal, RetirosDeAsociado.Prefijo, ct);
            var retiro = MembershipWithdrawal.Radicar(numero, persona.Id, cuenta.Id, request.Cause,
                request.Cause == MembershipWithdrawalCause.Other || !string.IsNullOrWhiteSpace(request.CauseDetail) ? request.CauseDetail : request.Note,
                request.SupportAttachmentPublicIds ?? [], fecha, usuario);
            var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(RetirosDeAsociado.Expediente, retiro.PublicId),
                AccionesDeSegregacion.Radicar, hoy, ct);
            if (verificada.IsFailure) return Falla(verificada.Error);
            db.MembershipWithdrawals.Add(retiro);
            intervenciones.Registrar(verificada.Value, RetirosDeAsociado.Expediente, retiro.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await retiros.DtoAsync(retiro, conLineas: false, ct));
        }, ct);
    }

    private static Result<MembershipWithdrawalDto> Falla(Error e) => Result.Failure<MembershipWithdrawalDto>(e);
}

public sealed class DesistMembershipWithdrawalCommandHandler(IApplicationDbContext db, RetirosDeAsociado retiros, IAlcanceDeCartera alcance,
    IDateTimeService reloj)
    : IRequestHandler<DesistMembershipWithdrawalCommand, Result<MembershipWithdrawalDto>>
{
    public async Task<Result<MembershipWithdrawalDto>> Handle(DesistMembershipWithdrawalCommand request, CancellationToken ct)
    {
        var leido = await retiros.EnAlcanceAsync(alcance, request.WithdrawalPublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<MembershipWithdrawalDto>(leido.Error);
        var retiro = leido.Value;
        if (retiro.Status != MembershipWithdrawalStatus.Filed) return Result.Failure<MembershipWithdrawalDto>(ErroresDeRetiro.NoRadicado(retiro.Status));
        retiro.Desistir(request.Reason, reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success(await retiros.DtoAsync(retiro, conLineas: false, ct));
    }
}

public sealed class DecideMembershipWithdrawalCommandHandler(
    IApplicationDbContext db,
    RetirosDeAsociado retiros,
    IAlcanceDeCartera alcance,
    PuntoDeActaCerrado puntos,
    RegistroDeIntervenciones intervenciones,
    EmisionDeCartera emision,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<DecideMembershipWithdrawalCommand, Result<MembershipWithdrawalDto>>
{
    public async Task<Result<MembershipWithdrawalDto>> Handle(DecideMembershipWithdrawalCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if ((await ActivacionesDeCaptacion.LeerAsync(db, ct)).Aportes is null)
            return Falla(ErroresDeCuentas.SinActivar(LineNature.Contribution));
        var leido = await retiros.EnAlcanceAsync(alcance, request.WithdrawalPublicId, seguir: false, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        if (leido.Value.Status != MembershipWithdrawalStatus.Filed) return Falla(ErroresDeRetiro.NoRadicado(leido.Value.Status));

        var punto = request.Decision == DecisionSobreRetiro.Approve
            ? await puntos.ValidarAsync(request.SessionItemPublicId, LendingDecisionSubjects.MembershipWithdrawal, request.WithdrawalPublicId, ct)
            : await RechazoAsync(request.SessionItemPublicId, request.WithdrawalPublicId, ct);
        if (punto.IsFailure) return Falla(punto.Error);
        if (request.Decision == DecisionSobreRetiro.Approve && request.EffectiveDate is { } efectiva && efectiva < leido.Value.FiledOn)
            return Falla(ErroresDeRetiro.FechaEfectiva(efectiva, leido.Value.FiledOn));

        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(RetirosDeAsociado.Expediente, request.WithdrawalPublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var retiro = await db.MembershipWithdrawals.FirstAsync(w => w.PublicId == request.WithdrawalPublicId, ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([retiro.PersonId], ModoDeEspera.Esperar, ct);
            if (retiro.Status != MembershipWithdrawalStatus.Filed) return Falla(ErroresDeRetiro.NoRadicado(retiro.Status));
            var ahora = reloj.UtcNow;
            if (request.Decision == DecisionSobreRetiro.Reject)
            {
                retiro.Rechazar(punto.Value.ItemId, usuario, request.Reason, ahora);
            }
            else
            {
                var cuenta = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().FirstAsync(a => a.Id == retiro.ContributionAccountId, ct);
                var sucursal = await emision.SucursalContableAsync(cuenta.BranchId, ct);
                if (sucursal.IsFailure) return Falla(sucursal.Error);
                var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == retiro.PersonId).Select(p => p.PublicId).FirstAsync(ct);
                var sesion = await (from i in db.SessionItems.AsNoTracking()
                                    join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                                    join b in db.GoverningBodies.AsNoTracking() on s.GoverningBodyId equals b.Id
                                    where i.Id == punto.Value.ItemId
                                    select new { s.Number, s.HeldOn, Organo = b.Name }).FirstAsync(ct);
                var efectiva = request.EffectiveDate ?? hoy;
                retiro.EffectiveDate = efectiva;
                var mensaje = await emision.RetiroAprobadoAsync(new RetiroAprobadoAEmitir(retiro, persona, sucursal.Value,
                    new MembershipWithdrawalApprovalV1
                    {
                        SessionItemPublicId = request.SessionItemPublicId, SessionNumber = sesion.Number, GoverningBody = sesion.Organo, DecidedOn = sesion.HeldOn,
                    },
                    new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name }, hoy), ct);
                retiro.Aprobar(punto.Value.ItemId, efectiva, usuario, request.Reason, ahora, mensaje);
            }
            intervenciones.Registrar(verificada.Value, RetirosDeAsociado.Expediente, retiro.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await retiros.DtoAsync(retiro, conLineas: false, ct));
        }, ct);
    }

    /// <summary>El punto que niega: de una sesión cerrada, decisión sobre este trámite y con un resultado que no aprueba.</summary>
    private async Task<Result<PuntoCerrado>> RechazoAsync(Guid item, Guid retiro, CancellationToken ct)
    {
        var p = await (from i in db.SessionItems.AsNoTracking()
                       join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                       where i.PublicId == item && !i.IsDeleted
                       select new { i.Id, i.PublicId, i.Kind, i.Outcome, i.Conditions, i.DecisionId, i.SubjectType, i.SubjectPublicId, s.Status, Sesion = s.PublicId, s.Number })
            .FirstOrDefaultAsync(ct);
        var corresponde = p is not null && p.Status == SessionStatus.Closed && p.Kind == SessionItemKind.Decision
                          && p.Outcome is CreditDecisionOutcome.Deny or CreditDecisionOutcome.Return
                          && p.SubjectType == LendingDecisionSubjects.MembershipWithdrawal && p.SubjectPublicId == retiro;
        return corresponde
            ? Result.Success(new PuntoCerrado(p!.Id, p.PublicId, p.Outcome!.Value, p.Conditions, p.DecisionId, p.Sesion, p.Number))
            : Result.Failure<PuntoCerrado>(ErroresDeGobierno.PuntoNoCorresponde(LendingDecisionSubjects.MembershipWithdrawal, p?.SubjectType, p?.Outcome?.ToString()));
    }

    private static Result<MembershipWithdrawalDto> Falla(Error e) => Result.Failure<MembershipWithdrawalDto>(e);
}

/// <summary>
/// El asunto de acta <c>MembershipWithdrawal</c> (feature 014, US10, T097; FR-005, FR-076): el retiro radicado que decide el órgano competente (consejo o
/// junta, el de la sesión). Pendiente mientras esté radicado; quien lo radicó no vota su punto; la huella cubre el trámite (persona, causa, soportes). La
/// decisión la registra después la ruta <c>/approve</c> o <c>/reject</c> con el punto cerrado (fija la fecha efectiva y emite el aviso a Asociados); el
/// cierre del acta no la aplica. (nuevo)
/// </summary>
public sealed class AsuntoDeRetiroDeAsociado(IApplicationDbContext db) : IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.MembershipWithdrawal;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var retiro = await db.MembershipWithdrawals.AsNoTracking().FirstOrDefaultAsync(w => w.PublicId == subjectPublicId && !w.IsDeleted, ct);
        if (retiro is null) return Result.Failure<AsuntoDeActa>(ErroresDeRetiro.Inexistente());
        var personas = await LectorDeCuentas.PersonasAsync(db, [retiro.PersonId], ct);
        var persona = personas[retiro.PersonId];
        var huella = new HuellaDeCartera()
            .Identificador("withdrawal", retiro.PublicId)
            .Texto("number", retiro.Number)
            .Texto("cause", retiro.Cause.ToString())
            .Texto("causeDetail", retiro.CauseDetail)
            .Fecha("filedOn", retiro.FiledOn)
            .Texto("supports", retiro.SupportAttachmentPublicIdsJson)
            .Calcular();
        var titulo = string.Create(CultureInfo.InvariantCulture,
            $"Retiro del asociado {persona.FullName} ({persona.DocumentNumber}), radicado {retiro.Number} del {retiro.FiledOn:yyyy-MM-dd}");
        var resumen = $"Causa: {Causa(retiro.Cause)}" + (string.IsNullOrWhiteSpace(retiro.CauseDetail) ? "." : $" ({retiro.CauseDetail}).");
        var personasDelAsunto = new List<PersonaDelAsunto>();
        if (await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == retiro.FiledByUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct)
            is { } radico)
            personasDelAsunto.Add(new PersonaDelAsunto(radico, MotivosDeExclusion.Radicador));
        return Result.Success(new AsuntoDeActa(titulo, resumen, retiro.Status == MembershipWithdrawalStatus.Filed, retiro.Status.ToString(),
            new ExpedienteDeCartera(RetirosDeAsociado.Expediente, retiro.PublicId), huella)
        {
            Personas = personasDelAsunto,
        });
    }

    public Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct) => Task.FromResult(Result.Success());

    /// <summary>El nombre de la causa en el acta y los documentos.</summary>
    public static string Causa(MembershipWithdrawalCause causa) => causa switch
    {
        MembershipWithdrawalCause.Voluntary => "retiro voluntario",
        MembershipWithdrawalCause.Exclusion => "exclusión",
        MembershipWithdrawalCause.Death => "fallecimiento",
        _ => "otra",
    };
}
