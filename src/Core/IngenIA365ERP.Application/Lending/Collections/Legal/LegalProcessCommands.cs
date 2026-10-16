using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Collections.Fees;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Legal;

// Feature 013, K4 (US12, T0947; api.md §14.8; data-model §26.10; FR-124, FR-098): el cobro jurídico de un crédito en la etapa jurídica, entregado a
// un abogado que es una persona del maestro, con sus actuaciones y documentos; lo que se cobra al deudor sigue la regla del gasto de cobranza y la
// insolvencia lo suspende. (nuevo)

/// <summary>Una actuación como la muestra la lista. (nuevo)</summary>
public sealed record LegalProcessActionDto(Guid PublicId, DateOnly Date, LegalActionKind Kind, string Description, decimal? Amount, bool ChargeableToDebtor,
    bool Charged, Guid? AttachmentPublicId, UsuarioRefDto RecordedBy);

/// <summary>Un proceso de cobro jurídico (api.md §14.8). Un proceso es de un crédito: <see cref="Loans"/> lleva uno (data-model §26.10). (nuevo)</summary>
public sealed record LegalProcessDto(
    Guid PublicId,
    string Number,
    IReadOnlyList<CreditoRefDto> Loans,
    PersonaRefDto Debtor,
    PersonaRefDto Lawyer,
    string? Court,
    string? FileNumber,
    string? ProcessType,
    LegalProcessStatus Status,
    DateOnly StartDate,
    Guid? SuspendedByInsolvencyPublicId,
    LegalProcessActionDto? LastAction,
    DateTime? ClosedAt,
    string? CloseReason);

/// <summary>Lo común del cobro jurídico: leer con alcance, suspender por insolvencia y proyectar. (nuevo)</summary>
public static class CobroJuridicoDeCartera
{
    /// <summary>El prefijo del número de un proceso de cobro jurídico (numerador de Cartera, clase <c>LegalProcess</c>).</summary>
    public const string Prefijo = "JUR-";

    public static async Task<Result<(LegalProcess Proceso, Loan Credito)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance,
        LendingAuditEmitter auditoria, Guid publicId, CancellationToken ct)
    {
        var proceso = await db.LegalProcesses.FirstOrDefaultAsync(p => p.PublicId == publicId && !p.IsDeleted, ct);
        if (proceso is null) return Result.Failure<(LegalProcess, Loan)>(ErroresDeCobranza.ProcesoInexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == proceso.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeCobranza.ProcesoInexistente(), auditoria, "LegalProcess",
            proceso.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<(LegalProcess, Loan)>(dentro.Error) : Result.Success((proceso, credito));
    }

    /// <summary>
    /// FR-098 (T0947): una insolvencia que se abre suspende los procesos de cobro jurídico abiertos del deudor, dejando dicha la insolvencia. Lo llama
    /// <c>OpenInsolvencyProceedingCommand</c> en su transacción, después de guardar la insolvencia (necesita su Id). Sin guardar. Devuelve cuántos.
    /// </summary>
    public static async Task<int> SuspenderPorInsolvenciaAsync(IApplicationDbContext db, int personId, int insolvenciaId, CancellationToken ct)
    {
        var abiertos = await db.LegalProcesses.Where(p => p.PersonId == personId && !p.IsDeleted
            && p.Status != LegalProcessStatus.Concluded && p.Status != LegalProcessStatus.Withdrawn && p.Status != LegalProcessStatus.Suspended).ToListAsync(ct);
        foreach (var p in abiertos) p.Suspender(insolvenciaId);
        return abiertos.Count;
    }

    public static async Task<IReadOnlyList<LegalProcessDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<LegalProcess> procesos, CancellationToken ct)
    {
        var loanIds = procesos.Select(p => p.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, procesos.Select(p => p.PersonId).Concat(procesos.Select(p => p.LawyerPersonId)), ct);
        var ids = procesos.Select(p => p.Id).ToList();
        var ultimas = (await db.LegalProcessActions.AsNoTracking().Where(a => ids.Contains(a.LegalProcessId) && !a.IsDeleted).ToListAsync(ct))
            .GroupBy(a => a.LegalProcessId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.ActionDate).ThenBy(a => a.Id).Last());
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, ultimas.Values.Select(a => a.RecordedByUserId), ct);
        var insolvencias = procesos.Where(p => p.SuspendedByInsolvencyId != null).Select(p => p.SuspendedByInsolvencyId!.Value).ToList();
        var deInsolvencia = await db.InsolvencyProceedings.AsNoTracking().IgnoreQueryFilters().Where(i => insolvencias.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.PublicId, ct);
        return procesos.Select(p =>
        {
            var c = creditos[p.LoanId];
            var ultima = ultimas.GetValueOrDefault(p.Id);
            return new LegalProcessDto(p.PublicId, p.Number, [new CreditoRefDto(c.PublicId, c.Number)], personas[p.PersonId], personas[p.LawyerPersonId],
                p.Authority, p.CaseNumber, p.ProcessType, p.Status, p.AssignedOn,
                p.SuspendedByInsolvencyId is { } i && deInsolvencia.TryGetValue(i, out var ins) ? ins : null,
                ultima is null ? null : Actuacion(ultima, usuarios), p.ClosedAt, p.CloseReason);
        }).ToList();
    }

    public static LegalProcessActionDto Actuacion(LegalProcessAction a, IReadOnlyDictionary<int, UsuarioRefDto> usuarios) => new(a.PublicId, a.ActionDate, a.Kind,
        a.Description, a.Amount, a.ChargeableToDebtor, a.ChargeOperationId is not null, a.AttachmentPublicId, usuarios[a.RecordedByUserId]);
}

// ---------------------------------------------------------------------------------------------------------------- abrir --

/// <summary>
/// Entrega créditos al cobro jurídico (feature 013, K4, T0947; api.md §14.8, <c>POST /api/lending/legal-processes</c>, <c>Lending.LegalProcesses.Manage</c>,
/// con clave; FR-124): cada crédito, vivo, del alcance y en una etapa <b>jurídica</b> (<c>Lending.LegalProcess.LoanNotInLegalStage</c>) y sin otro
/// proceso abierto (<c>.AlreadyOpen</c>), recibe su proceso —un proceso por crédito, data-model §26.10— con el abogado, que es una persona del maestro
/// distinta del demandado (<c>Core.Person.NotFound</c>), el despacho, el radicado y la fecha. Si el deudor está en una insolvencia en negociación, el
/// proceso nace suspendido por ella (FR-098). (nuevo)
/// </summary>
public sealed record OpenLegalProcessCommand(
    IReadOnlyList<Guid> LoanPublicIds,
    Guid LawyerPersonPublicId,
    string? Court,
    string? FileNumber,
    DateOnly StartDate,
    string? ProcessType = null,
    string? FeeAgreementJson = null) : IRequest<Result<IReadOnlyList<LegalProcessDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class OpenLegalProcessCommandValidator : AbstractValidator<OpenLegalProcessCommand>
{
    public OpenLegalProcessCommandValidator()
    {
        RuleFor(x => x.LoanPublicIds).NotEmpty().WithMessage("Indique los créditos que se entregan al abogado.");
        RuleFor(x => x.LawyerPersonPublicId).NotEmpty().WithMessage("Indique el abogado.");
        RuleFor(x => x.Court).MaximumLength(150);
        RuleFor(x => x.FileNumber).MaximumLength(60);
        RuleFor(x => x.ProcessType).MaximumLength(60);
    }
}

public sealed class OpenLegalProcessCommandHandler(
    IApplicationDbContext db,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<OpenLegalProcessCommand, Result<IReadOnlyList<LegalProcessDto>>>
{
    public async Task<Result<IReadOnlyList<LegalProcessDto>>> Handle(OpenLegalProcessCommand request, CancellationToken ct)
    {
        var abogado = await db.People.AsNoTracking().Where(p => p.PublicId == request.LawyerPersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (abogado is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        var creditos = new List<Loan>();
        foreach (var publico in request.LoanPublicIds.Distinct())
        {
            var leido = await CobranzaDeCartera.CreditoAsync(db, alcance, auditoria, publico, ct);
            if (leido.IsFailure) return Falla(leido.Error);
            creditos.Add(leido.Value);
        }
        if (creditos.Any(c => c.DebtorPersonId == abogado))
            return Falla(ErroresDeCobranza.AbogadoEsElDemandado());

        var etapas = await CobranzaDeCartera.EtapaVigenteAsync(db, creditos.Select(c => c.Id).ToList(), ct);
        var versiones = etapas.Values.Select(e => e.CollectionStageId).Distinct().ToList();
        var juridicas = (await db.CollectionStages.AsNoTracking().IgnoreQueryFilters().Where(s => versiones.Contains(s.Id) && s.IsLegal).Select(s => s.Id)
            .ToListAsync(ct)).ToHashSet();
        foreach (var c in creditos)
        {
            if (c.Status is not (LoanStatus.Active or LoanStatus.WrittenOff))
                return Falla(Novelties.ErroresDeNovedades.CreditoNoActivo(c.Number, c.Status.ToString()));
            var etapa = etapas.GetValueOrDefault(c.Id);
            if (etapa is null || !juridicas.Contains(etapa.CollectionStageId)) return Falla(ErroresDeCobranza.FueraDeEtapaJuridica(c.Number, etapa?.StageCode));
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCreditosAsync(creditos.Select(c => c.Id).ToList(), ModoDeEspera.Esperar, ct);
            var ids = creditos.Select(c => c.Id).ToList();
            var abierto = await db.LegalProcesses.AsNoTracking().Where(p => ids.Contains(p.LoanId) && !p.IsDeleted
                && p.Status != LegalProcessStatus.Concluded && p.Status != LegalProcessStatus.Withdrawn).Select(p => new { p.LoanId, p.Number })
                .FirstOrDefaultAsync(ct);
            if (abierto is not null) return Falla(ErroresDeCobranza.ProcesoAbierto(creditos.First(c => c.Id == abierto.LoanId).Number, abierto.Number));

            var nuevos = new List<LegalProcess>();
            foreach (var c in creditos.OrderBy(c => c.Number, StringComparer.Ordinal))
            {
                var numero = await numerador.AsignarAsync(LendingNumberKind.LegalProcess, string.Empty, CobroJuridicoDeCartera.Prefijo, ct);
                var p = LegalProcess.Abrir(numero, c.Id, c.DebtorPersonId, abogado.Value, request.StartDate, request.Court, request.FileNumber,
                    request.ProcessType, request.FeeAgreementJson);
                db.LegalProcesses.Add(p);
                nuevos.Add(p);
            }
            await db.SaveChangesAsync(ct);
            foreach (var p in nuevos)
                if (await CobranzaDeCartera.InsolvenciaAsync(db, p.PersonId, reloj.HoyLocal, ct) is { } insolvencia) p.Suspender(insolvencia.Id);
            await db.SaveChangesAsync(ct);
            return Result.Success(await CobroJuridicoDeCartera.DtosAsync(db, nuevos, ct));
        }, ct);
    }

    private static Result<IReadOnlyList<LegalProcessDto>> Falla(Error e) => Result.Failure<IReadOnlyList<LegalProcessDto>>(e);
}

// ------------------------------------------------------------------------------------------------------------- actuación --

/// <summary>
/// Registra una actuación del proceso (feature 013, K4, T0947; api.md §14.8, <c>POST /api/lending/legal-processes/{id}/actions</c>,
/// <c>Lending.LegalProcesses.Manage</c>, con clave; FR-124): fecha, clase, descripción y, si es un honorario o un gasto, su valor y si se cobra al
/// deudor; los soportes son documentos <c>LendingLegalProcessDocument</c> del proceso, disponibles (<c>Lending.LegalProcess.AttachmentInvalid</c>; la
/// actuación cita el primero, data-model §26.10). Radicar mueve un proceso asignado a <c>Filed</c>; la primera actuación de un radicado lo pone
/// <c>InProgress</c>. Un honorario o gasto cobrable al deudor <b>sigue la regla del gasto de cobranza</b> (FR-121, T77): aquí sólo se informa al deudor
/// (<c>CollectionFeeNotice</c>); la operación <c>Charge</c> la escribe <c>CauseCollectionFeesCommand</c> cuando el aviso salió. Un proceso cerrado no
/// recibe actuaciones (<c>.InvalidStatus</c>). (nuevo)
/// </summary>
public sealed record RecordLegalProcessActionCommand(
    Guid LegalProcessPublicId,
    DateOnly Date,
    LegalActionKind Kind,
    string Description,
    decimal? Amount = null,
    bool ChargeableToDebtor = false,
    IReadOnlyList<Guid>? AttachmentPublicIds = null) : IRequest<Result<LegalProcessDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RecordLegalProcessActionCommandValidator : AbstractValidator<RecordLegalProcessActionCommand>
{
    public RecordLegalProcessActionCommandValidator()
    {
        RuleFor(x => x.LegalProcessPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000).WithMessage("Describa la actuación (hasta 2.000 caracteres).");
        RuleFor(x => x.Amount).GreaterThan(0m).PrecisionScale(18, 2, true).When(x => x.Amount is not null)
            .WithMessage("El valor de la actuación es mayor que cero, con hasta dos decimales.");
        RuleFor(x => x.ChargeableToDebtor).Equal(false).When(x => x.Amount is null || x.Kind is not (LegalActionKind.Fee or LegalActionKind.Expense))
            .WithMessage("Sólo un honorario o un gasto con valor se cobra al deudor.");
    }
}

public sealed class RecordLegalProcessActionCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RecordLegalProcessActionCommand, Result<LegalProcessDto>>
{
    public async Task<Result<LegalProcessDto>> Handle(RecordLegalProcessActionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await CobroJuridicoDeCartera.LeerAsync(db, alcance, auditoria, request.LegalProcessPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var (proceso, credito) = leido.Value;
        if (!proceso.Abierto)
            return Falla(ErroresDeGobierno.De(ProblemaDeCartera.Con(LegalProcess.CodigoEstadoInvalido, "El proceso de cobro jurídico ya terminó.", "status",
                proceso.Status.ToString())));
        var adjuntos = request.AttachmentPublicIds?.Distinct().ToList() ?? [];
        if (adjuntos.Count > 0)
        {
            var validos = await db.Attachments.AsNoTracking().CountAsync(a => adjuntos.Contains(a.PublicId)
                && a.OwnerEntityType == AdjuntosDeModulo.DocumentoDeProcesoJuridico && a.OwnerEntityPublicId == proceso.PublicId
                && a.Status == EstadoDeAdjunto.Available, ct);
            if (validos != adjuntos.Count) return Falla(ErroresDeCobranza.SoporteJuridicoInvalido());
        }

        var actuacion = LegalProcessAction.Registrar(proceso.Id, request.Date, request.Kind, request.Description, usuario, request.Amount,
            request.ChargeableToDebtor, adjuntos.Count > 0 ? adjuntos[0] : null);
        try
        {
            if (request.Kind == LegalActionKind.Filing && proceso.Status == LegalProcessStatus.Assigned) proceso.Avanzar(LegalProcessStatus.Filed);
            else if (proceso.Status == LegalProcessStatus.Filed && request.Kind is not (LegalActionKind.Fee or LegalActionKind.Expense or LegalActionKind.Other))
                proceso.Avanzar(LegalProcessStatus.InProgress);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }
        db.LegalProcessActions.Add(actuacion);

        if (actuacion.ChargeableToDebtor)
        {
            var etapa = (await CobranzaDeCartera.EtapaVigenteAsync(db, [credito.Id], ct)).GetValueOrDefault(credito.Id)?.StageCode;
            var aviso = await GastosDeCobranza.InformarAsync(cola, credito, new GastoPropuesto(actuacion.Amount!.Value, etapa ?? string.Empty,
                GastosDeCobranza.ClaveDeActuacion(credito.PublicId, actuacion.PublicId), actuacion.PublicId), reloj, ActorKind.Person, usuario, ct);
            if (aviso.IsFailure) return Falla(aviso.Error);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success((await CobroJuridicoDeCartera.DtosAsync(db, [proceso], ct))[0]);
    }

    private static Result<LegalProcessDto> Falla(Error e) => Result.Failure<LegalProcessDto>(e);
}

// ---------------------------------------------------------------------------------------------------------------- cerrar --

/// <summary>
/// Cierra un proceso de cobro jurídico con su resultado (feature 013, K4, T0947; api.md §14.8, <c>POST /api/lending/legal-processes/{id}/close</c>,
/// <c>Lending.LegalProcesses.Manage</c>, con clave y motivo): <c>Concluded</c> o <c>Withdrawn</c>; uno ya cerrado, <c>Lending.LegalProcess.InvalidStatus</c>.
/// (nuevo)
/// </summary>
public sealed record CloseLegalProcessCommand(Guid LegalProcessPublicId, LegalProcessStatus Outcome, string Reason)
    : IRequest<Result<LegalProcessDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseLegalProcessCommandValidator : ValidadorConMotivo<CloseLegalProcessCommand>
{
    public CloseLegalProcessCommandValidator()
    {
        RuleFor(x => x.LegalProcessPublicId).NotEmpty();
        RuleFor(x => x.Outcome).Must(o => o is LegalProcessStatus.Concluded or LegalProcessStatus.Withdrawn)
            .WithMessage("Un proceso se cierra concluido o retirado.");
    }
}

public sealed class CloseLegalProcessCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<CloseLegalProcessCommand, Result<LegalProcessDto>>
{
    public async Task<Result<LegalProcessDto>> Handle(CloseLegalProcessCommand request, CancellationToken ct)
    {
        var leido = await CobroJuridicoDeCartera.LeerAsync(db, alcance, auditoria, request.LegalProcessPublicId, ct);
        if (leido.IsFailure) return Result.Failure<LegalProcessDto>(leido.Error);
        var (proceso, _) = leido.Value;
        try
        {
            proceso.Cerrar(request.Outcome, request.Reason, reloj.UtcNow);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<LegalProcessDto>(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success((await CobroJuridicoDeCartera.DtosAsync(db, [proceso], ct))[0]);
    }
}

// ---------------------------------------------------------------------------------------------------------------- lista --

/// <summary>
/// Los procesos de cobro jurídico (feature 013, K4, T0947; api.md §14.8, <c>GET /api/lending/legal-processes?status=&amp;lawyer=&amp;page=</c>,
/// <c>Lending.Collections.Read</c>): con su crédito, el abogado, el despacho, el radicado, el estado y la última actuación; filtrados en silencio por el
/// alcance; el más reciente primero. (nuevo)
/// </summary>
public sealed record ListLegalProcessesQuery(LegalProcessStatus? Status = null, Guid? Lawyer = null, Guid? LoanPublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<LegalProcessDto>>>;

public sealed class ListLegalProcessesQueryValidator : AbstractValidator<ListLegalProcessesQuery>
{
    public ListLegalProcessesQueryValidator() => RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
}

public sealed class ListLegalProcessesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListLegalProcessesQuery, Result<PagedResult<LegalProcessDto>>>
{
    public async Task<Result<PagedResult<LegalProcessDto>>> Handle(ListLegalProcessesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.LegalProcesses.AsNoTracking().Where(p => !p.IsDeleted && creditos.Contains(p.LoanId));
        if (request.Status is { } estado) consulta = consulta.Where(p => p.Status == estado);
        if (request.Lawyer is { } abogado)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == abogado).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(p => p.LawyerPersonId == id);
        }
        if (request.LoanPublicId is { } loan)
        {
            var id = await db.Loans.AsNoTracking().Where(l => l.PublicId == loan).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(p => p.LoanId == id);
        }
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(p => p.AssignedOn).ThenByDescending(p => p.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<LegalProcessDto>(await CobroJuridicoDeCartera.DtosAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }
}
