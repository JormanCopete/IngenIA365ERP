using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Holds;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Deceased;

// Feature 014, US14 (T210, T212; contracts/api.md §13; FR-034, FR-065, FR-072, FR-082, FR-083; research §23): registrar el fallecimiento (bloquea las cuentas),
// la vista previa de la entrega (sin clave), registrar la entrega, aprobarla otra persona (órdenes de giro o traslados), anular un registro errado, la lista
// y el detalle. (nuevo)

/// <summary>Una cuenta del caso con su saldo y si se entrega. (nuevo)</summary>
public sealed record DeceasedCaseAccountDto(Guid PublicId, string Number, DepositClass? DepositClass, MemberAccountStatus Status, decimal Balance, bool Deliverable,
    string? Note);

/// <summary>Un beneficiario vigente en la fecha de la defunción. (nuevo)</summary>
public sealed record DeceasedCaseBeneficiaryDto(Guid AccountPublicId, string AccountNumber, MemberAccountPersonDto Person, decimal Percent);

/// <summary>Lo que recibe una persona de una cuenta. (nuevo)</summary>
public sealed record DeliveryPortionDto(Guid AccountPublicId, string AccountNumber, MemberAccountPersonDto Person, decimal Percent, decimal Amount);

/// <summary>Lo que recibe una persona en total. (nuevo)</summary>
public sealed record DeliveryRecipientTotalDto(MemberAccountPersonDto Person, decimal Amount);

/// <summary>El tope de la entrega sin juicio de sucesión que rige y su fuente. (nuevo)</summary>
public sealed record DeliveryCapDto(decimal? Amount, string Source, DateOnly ValidFrom);

/// <summary>La vista previa de la entrega (api.md §13). (nuevo)</summary>
public sealed record DeceasedDeliveryPreviewDto(
    DeliveryRoute Route,
    DateOnly Date,
    IReadOnlyList<DeceasedCaseAccountDto> Accounts,
    IReadOnlyList<DeliveryPortionDto> Portions,
    IReadOnlyList<DeliveryRecipientTotalDto> Recipients,
    decimal Total,
    DeliveryCapDto? CapWithoutSuccession,
    IReadOnlyList<string> SupportsRequired,
    IReadOnlyList<string> Warnings,
    string Sha256);

/// <summary>Un destinatario del plan registrado. (nuevo)</summary>
public sealed record PlannedRecipientDto(MemberAccountPersonDto Person, decimal? Percent, FormaDeEntrega Form, string? TargetAccountNumber);

/// <summary>Una entrega hecha: la operación, la cuenta, a quién, cuánto, el medio y su giro. (nuevo)</summary>
public sealed record DeceasedDeliveryDto(Guid OperationPublicId, string OperationNumber, Guid AccountPublicId, string AccountNumber, MemberAccountPersonDto Recipient,
    decimal Amount, string Form, string? OrderNumber, string? OrderStatus, bool Reversed);

/// <summary>El caso de fallecimiento (api.md §13). (nuevo)</summary>
public sealed record DeceasedHolderCaseDto(
    Guid PublicId,
    string Number,
    MemberAccountPersonDto Person,
    DateOnly DeathDate,
    DateOnly ReportedOn,
    DeceasedCaseStatus Status,
    DeliveryRoute? Route,
    decimal TotalAmount,
    decimal? WithoutProbateCap,
    bool MasterMarksDeceased,
    Guid DeathCertificateAttachmentPublicId,
    IReadOnlyList<Guid> SupportAttachmentPublicIds,
    IReadOnlyList<DeceasedCaseAccountDto> Accounts,
    IReadOnlyList<DeceasedCaseBeneficiaryDto> Beneficiaries,
    IReadOnlyList<PlannedRecipientDto> PlannedRecipients,
    IReadOnlyList<DeceasedDeliveryDto> Deliveries,
    string Reason,
    DateTime RegisteredAt,
    DateTime? DeliveryRegisteredAt,
    DateTime? ApprovedAt,
    DateTime? DeliveredAt,
    string? CancelReason,
    IReadOnlyList<string> Warnings);

/// <summary>Un destinatario pedido: la persona, su porcentaje (fracción; fuera de «beneficiarios») y el medio. (nuevo)</summary>
public sealed record DeliveryRecipientInput(Guid PersonPublicId, decimal? Percent, FormaDeEntrega Form, Guid? TargetAccountPublicId, Guid? BankPublicId,
    byte? AccountType, string? AccountNumber);

/// <summary>Lo común de los casos: la lectura en el alcance, los destinatarios y el DTO. (nuevo)</summary>
public sealed class VistaDeFallecimientos(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, Fallecimientos fallecimientos,
    IDateTimeService reloj)
{
    /// <summary>El caso por su PublicId, con todas las cuentas de la persona en el alcance (falla cerrado, como los embargos); fuera, el 404 del caso.</summary>
    public async Task<Result<DeceasedHolderCase>> LeerAsync(Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.DeceasedHolderCases : db.DeceasedHolderCases.AsNoTracking();
        var caso = await consulta.FirstOrDefaultAsync(c => c.PublicId == publicId && !c.IsDeleted, ct);
        if (caso is null || !await AlcanceDeEmbargos.PersonaEnAlcanceAsync(db, alcance, auditoria, caso.PersonId, ct))
            return Result.Failure<DeceasedHolderCase>(ErroresDeFallecimiento.Inexistente());
        return Result.Success(caso);
    }

    /// <summary>Los destinatarios pedidos, resueltos: la persona existe y la cuenta de destino es de ahorro viva suya y está en el alcance.</summary>
    public async Task<Result<IReadOnlyList<DestinatarioDeEntrega>>> DestinatariosAsync(IReadOnlyList<DeliveryRecipientInput>? pedidos, CancellationToken ct)
    {
        var r = new List<DestinatarioDeEntrega>();
        foreach (var p in pedidos ?? [])
        {
            var persona = await db.People.AsNoTracking().Where(x => x.PublicId == p.PersonPublicId && !x.IsDeleted).Select(x => new { x.Id, x.PublicId })
                .FirstOrDefaultAsync(ct);
            if (persona is null) return Result.Failure<IReadOnlyList<DestinatarioDeEntrega>>(ErroresDeGobierno.PersonaInexistente());
            if (r.Any(x => x.PersonaId == persona.Id))
                return Result.Failure<IReadOnlyList<DestinatarioDeEntrega>>(ErroresDeFallecimiento.Invalido("recipients", "Una persona va una sola vez en la entrega."));
            int? destino = null;
            if (p.Form == FormaDeEntrega.TransferToAccount)
            {
                if (p.TargetAccountPublicId is not { } id)
                    return Result.Failure<IReadOnlyList<DestinatarioDeEntrega>>(ErroresDeFallecimiento.Invalido("targetAccountPublicId",
                        "La entrega por traslado nombra la cuenta de ahorro del destinatario."));
                var cuenta = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, id, seguir: false, ct);
                if (cuenta.IsFailure) return Result.Failure<IReadOnlyList<DestinatarioDeEntrega>>(cuenta.Error);
                var c = cuenta.Value;
                if (c.HolderPersonId != persona.Id || c.Nature != LineNature.Deposit || c.DepositClass is not (DepositClass.Demand or DepositClass.Permanent)
                    || c.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive))
                    return Result.Failure<IReadOnlyList<DestinatarioDeEntrega>>(ErroresDeFallecimiento.Invalido("targetAccountPublicId",
                        $"La cuenta {c.Number} no es una cuenta de ahorro a la vista o permanente viva del destinatario."));
                destino = c.Id;
            }
            r.Add(new DestinatarioDeEntrega(persona.Id, persona.PublicId, p.Percent, p.Form, destino, p.BankPublicId, p.AccountType, p.AccountNumber));
        }
        return Result.Success<IReadOnlyList<DestinatarioDeEntrega>>(r);
    }

    public async Task<DeceasedDeliveryPreviewDto> PreviaDtoAsync(VistaPreviaDeEntrega v, CancellationToken ct)
    {
        var personas = await LectorDeCuentas.PersonasAsync(db, v.Porciones.Select(p => p.PersonaId).Distinct(), ct);
        var numeros = v.Cuentas.ToDictionary(c => c.Cuenta.Id, c => (c.Cuenta.PublicId, c.Cuenta.Number));
        return new DeceasedDeliveryPreviewDto(v.Ruta, v.Fecha, v.Cuentas.Select(Cuenta).ToList(),
            v.Porciones.Select(p => new DeliveryPortionDto(numeros[p.CuentaId].PublicId, numeros[p.CuentaId].Number, personas[p.PersonaId], p.Porcentaje, p.Valor)).ToList(),
            v.Porciones.GroupBy(p => p.PersonaId).Select(g => new DeliveryRecipientTotalDto(personas[g.Key], g.Sum(p => p.Valor))).ToList(),
            v.Total, v.Tope is { } t ? new DeliveryCapDto(t.Valor, t.Fuente, t.ValidFrom) : null, v.SoportesRequeridos, v.Avisos, v.Sha256);
    }

    public static DeceasedCaseAccountDto Cuenta(CuentaDelFallecido c) =>
        new(c.Cuenta.PublicId, c.Cuenta.Number, c.Cuenta.DepositClass, c.Cuenta.Status, c.Saldo, c.Entregable, c.Nota);

    public async Task<DeceasedHolderCaseDto> DtoAsync(DeceasedHolderCase caso, bool conDetalle, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var persona = (await LectorDeCuentas.PersonasAsync(db, [caso.PersonId], ct))[caso.PersonId];
        var enElMaestro = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == caso.PersonId).Select(p => p.IsDeceased).FirstAsync(ct);
        var avisos = new List<string>();
        if (!enElMaestro) avisos.Add("El maestro de personas no marca a la persona como fallecida: actualícelo en Personas.");
        IReadOnlyList<DeceasedCaseAccountDto> cuentas = [];
        IReadOnlyList<DeceasedCaseBeneficiaryDto> beneficiarios = [];
        IReadOnlyList<PlannedRecipientDto> plan = [];
        IReadOnlyList<DeceasedDeliveryDto> entregas = [];
        if (conDetalle)
        {
            var leidas = await fallecimientos.CuentasAsync(caso.PersonId, hoy, seguir: false, ct);
            cuentas = leidas.Select(Cuenta).ToList();
            var porCuenta = await fallecimientos.BeneficiariosAsync(leidas.Select(c => c.Cuenta.Id).ToList(), caso.DeathDate, ct);
            var personas = await LectorDeCuentas.PersonasAsync(db, porCuenta.Values.SelectMany(x => x.Select(b => b.PersonaId)).Distinct(), ct);
            beneficiarios = leidas.SelectMany(c => (porCuenta.GetValueOrDefault(c.Cuenta.Id) ?? [])
                .Select(b => new DeceasedCaseBeneficiaryDto(c.Cuenta.PublicId, c.Cuenta.Number, personas[b.PersonaId], b.Porcentaje))).ToList();
            if (Fallecimientos.Leer(caso.DeliveryPlanJson) is { } p)
            {
                var deLaPlan = await LectorDeCuentas.PersonasAsync(db, p.Destinatarios.Select(d => d.PersonaId).Distinct(), ct);
                var destinos = p.Destinatarios.Where(d => d.CuentaDestinoId is not null).Select(d => d.CuentaDestinoId!.Value).ToList();
                var nums = await db.MemberAccounts.AsNoTracking().Where(a => destinos.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Number, ct);
                plan = p.Destinatarios.Select(d => new PlannedRecipientDto(deLaPlan[d.PersonaId], d.Porcentaje, d.Forma,
                    d.CuentaDestinoId is { } x ? nums.GetValueOrDefault(x) : null)).ToList();
            }
            entregas = await EntregasAsync(caso, ct);
        }
        var soportes = JsonSerializer.Deserialize<List<Guid>>(caso.SupportAttachmentPublicIdsJson) ?? [];
        return new DeceasedHolderCaseDto(caso.PublicId, caso.Number, persona, caso.DeathDate, caso.ReportedOn, caso.Status, caso.Route, caso.TotalAmount,
            caso.WithoutProbateCap, enElMaestro, caso.DeathCertificateAttachmentPublicId, soportes, cuentas, beneficiarios, plan, entregas, caso.Reason,
            caso.RegisteredAt, caso.DeliveryRegisteredAt, caso.ApprovedAt, caso.DeliveredAt, caso.CancelReason, avisos);
    }

    private async Task<IReadOnlyList<DeceasedDeliveryDto>> EntregasAsync(DeceasedHolderCase caso, CancellationToken ct)
    {
        var ops = await (from o in db.MemberAccountOperations.AsNoTracking()
                         join a in db.MemberAccounts.AsNoTracking() on o.MemberAccountId equals a.Id
                         where o.DeceasedHolderCaseId == caso.Id && o.Kind == MemberAccountOperationKind.DeceasedDelivery && !o.IsDeleted
                         orderby o.Id
                         select new { o.Id, o.PublicId, o.Number, o.Amount, o.Payload, o.CounterpartMemberAccountId, o.ReversedByOperationId, Cuenta = a.PublicId, CuentaNumero = a.Number })
            .ToListAsync(ct);
        var ids = ops.Select(o => o.Id).ToList();
        var ordenes = await db.AccountPayoutOrders.AsNoTracking().Where(o => ids.Contains(o.OperationId))
            .ToDictionaryAsync(o => o.OperationId, o => new { o.Number, o.Status, o.BeneficiaryPersonId }, ct);
        var destinos = ops.Where(o => o.CounterpartMemberAccountId is not null).Select(o => o.CounterpartMemberAccountId!.Value).ToList();
        var titulares = await db.MemberAccounts.AsNoTracking().Where(a => destinos.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.HolderPersonId, ct);
        var personasIds = ordenes.Values.Select(o => o.BeneficiaryPersonId).Concat(titulares.Values).Distinct().ToList();
        var personas = await LectorDeCuentas.PersonasAsync(db, personasIds, ct);
        return ops.Select(o =>
        {
            var orden = ordenes.GetValueOrDefault(o.Id);
            var persona = orden is not null ? orden.BeneficiaryPersonId : o.CounterpartMemberAccountId is { } d ? titulares[d] : caso.PersonId;
            return new DeceasedDeliveryDto(o.PublicId, o.Number, o.Cuenta, o.CuentaNumero,
                personas.TryGetValue(persona, out var p) ? p : new MemberAccountPersonDto(Guid.Empty, string.Empty, string.Empty), o.Amount,
                orden is null ? nameof(FormaDeEntrega.TransferToAccount) : nameof(FormaDeEntrega.DisbursementOrder), orden?.Number, orden?.Status.ToString(),
                o.ReversedByOperationId is not null);
        }).ToList();
    }
}

// ------------------------------------------------------------------------------------------------------------- registrar --

/// <summary>
/// <c>POST /api/lending/deceased-cases</c> (<c>RegisterDeceasedHolderCommand</c>, <c>Lending.DeceasedCases.Register</c>): registra el fallecimiento de un
/// titular con el registro civil de defunción (adjunto <c>LendingDeceasedSupport</c> con dueño la persona). Bloquea todas sus cuentas vivas —de captación y de
/// aportes— con <c>HoldReason.Deceased</c> total, anula los retiros que esperaban aprobación y, desde ese día, la causación se suspende en las líneas que lo
/// dicen (FR-065) y el CDAT no se paga al vencer (FR-072). Ya registrado: <c>AlreadyRegistered</c>. (nuevo)
/// </summary>
public sealed record RegisterDeceasedHolderCommand(Guid PersonPublicId, DateOnly DateOfDeath, Guid DeathCertificateAttachmentPublicId, string Reason)
    : IRequest<Result<DeceasedHolderCaseDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDeceasedHolderCommandValidator : ValidadorConMotivo<RegisterDeceasedHolderCommand>
{
    public RegisterDeceasedHolderCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.DeathCertificateAttachmentPublicId).NotEmpty();
    }
}

public sealed class RegisterDeceasedHolderCommandHandler(
    IApplicationDbContext db,
    VistaDeFallecimientos vista,
    IContextoDeLineasDeCaptacion contexto,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj,
    IngenIA365ERP.Application.Common.Approvals.IMotorDeAprobaciones? aprobaciones = null)
    : IRequestHandler<RegisterDeceasedHolderCommand, Result<DeceasedHolderCaseDto>>
{
    public async Task<Result<DeceasedHolderCaseDto>> Handle(RegisterDeceasedHolderCommand request, CancellationToken ct)
    {
        if (contexto.Entrega < EntregaDeCartera.K10)
            return Falla(Contributions.ErroresDeAportes.EntregaNoDisponible("El registro del fallecimiento", nameof(EntregaDeCartera.K10)));
        var hoy = reloj.HoyLocal;
        if (request.DateOfDeath > hoy) return Falla(ErroresDeFallecimiento.Invalido("dateOfDeath", "La fecha de la defunción no puede ser futura."));
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        if (!await AlcanceDeEmbargos.PersonaEnAlcanceAsync(db, alcance, auditoria, persona.Id, ct)) return Falla(ErroresDeGobierno.PersonaInexistente());
        if (!await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == request.DeathCertificateAttachmentPublicId && !a.IsDeleted
                && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeFallecimiento && a.OwnerEntityPublicId == persona.PublicId, ct))
            return Falla(ErroresDeFallecimiento.SinSoportes(["Registro civil de defunción"]));
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([persona.Id], ModoDeEspera.Esperar, ct);
            if (await db.DeceasedHolderCases.AsNoTracking().Where(c => c.PersonId == persona.Id && !c.IsDeleted && c.Status != DeceasedCaseStatus.Cancelled)
                    .Select(c => c.Number).FirstOrDefaultAsync(ct) is { } otro)
                return Falla(ErroresDeFallecimiento.YaRegistrado(otro));
            var cuentas = await db.MemberAccounts.Where(a => a.HolderPersonId == persona.Id && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
                .OrderBy(a => a.Id).ToListAsync(ct);
            await cerrojo.BloquearCuentasAsync(cuentas.Select(c => c.Id).ToList(), ModoDeEspera.Esperar, ct);
            var caso = new DeceasedHolderCase
            {
                Number = await numerador.NumeroDeProcesoDeCaptacionesAsync(LendingNumberKind.DeceasedHolderCase, Fallecimientos.Prefijo, ct),
                PersonId = persona.Id,
                DeathDate = request.DateOfDeath,
                ReportedOn = hoy,
                DeathCertificateAttachmentPublicId = request.DeathCertificateAttachmentPublicId,
                Reason = DeceasedHolderCase.Recortar(request.Reason),
                RegisteredByUserId = usuario,
                RegisteredAt = reloj.UtcNow,
            };
            db.DeceasedHolderCases.Add(caso);
            await db.SaveChangesAsync(ct);
            foreach (var cuenta in cuentas)
            {
                var bloqueo = AccountHold.Poner(cuenta.Id, HoldReason.Deceased, null, hoy, null, $"Fallecimiento del titular (caso {caso.Number})",
                    request.DeathCertificateAttachmentPublicId, usuario);
                bloqueo.DeceasedHolderCaseId = caso.Id;
                db.AccountHolds.Add(bloqueo);
                // El retiro que esperaba un aprobador ya no vale: el titular falleció.
                if (aprobaciones is not null)
                    await aprobaciones.InvalidarAsync(Domain.Approvals.ApprovalSourceTypes.MemberAccountOperation, cuenta.PublicId,
                        Domain.Approvals.ApprovalSubjects.AccountWithdrawal, ct);
            }
            await db.SaveChangesAsync(ct);
            return Result.Success(await vista.DtoAsync(caso, conDetalle: true, ct));
        }, ct);
    }

    private static Result<DeceasedHolderCaseDto> Falla(Error e) => Result.Failure<DeceasedHolderCaseDto>(e);
}

// --------------------------------------------------------------------------------------------------------- vista previa --

/// <summary>
/// <c>POST /{id}/delivery/preview</c> (<c>PreviewDeceasedDeliveryQuery</c>, sin clave, <c>Lending.DeceasedCases.Register</c>): las cuentas, el reparto por
/// cuenta y persona, el tope (sin juicio de sucesión), los soportes que pide y la huella. Sin escribir. (nuevo)
/// </summary>
public sealed record PreviewDeceasedDeliveryQuery(Guid CasePublicId, DeliveryRoute Mode, IReadOnlyList<DeliveryRecipientInput>? Recipients)
    : IRequest<Result<DeceasedDeliveryPreviewDto>>;

public sealed class PreviewDeceasedDeliveryQueryHandler(VistaDeFallecimientos vista, Fallecimientos fallecimientos, IDateTimeService reloj)
    : IRequestHandler<PreviewDeceasedDeliveryQuery, Result<DeceasedDeliveryPreviewDto>>
{
    public async Task<Result<DeceasedDeliveryPreviewDto>> Handle(PreviewDeceasedDeliveryQuery request, CancellationToken ct)
    {
        var leido = await vista.LeerAsync(request.CasePublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<DeceasedDeliveryPreviewDto>(leido.Error);
        var destinatarios = await vista.DestinatariosAsync(request.Recipients, ct);
        if (destinatarios.IsFailure) return Result.Failure<DeceasedDeliveryPreviewDto>(destinatarios.Error);
        var previa = await fallecimientos.VistaPreviaAsync(leido.Value, request.Mode, destinatarios.Value, reloj.HoyLocal, ct);
        return previa.IsFailure ? Result.Failure<DeceasedDeliveryPreviewDto>(previa.Error) : Result.Success(await vista.PreviaDtoAsync(previa.Value, ct));
    }
}

// ------------------------------------------------------------------------------------------------------ registrar entrega --

/// <summary>
/// <c>POST /{id}/delivery</c> (<c>RegisterDeceasedDeliveryCommand</c>, <c>Lending.DeceasedCases.Register</c>): registra la entrega —la ruta, los destinatarios,
/// los soportes y la huella de la vista previa que se vio (<c>Lending.Savings.PreviewChanged</c> si cambió)— y la deja esperando que otra persona la
/// apruebe (<c>DeliveryPending</c>). Sin juicio de sucesión exige los soportes del parámetro (al menos uno); por sucesión, el suyo. Se puede volver a
/// registrar mientras no esté aprobada. (nuevo)
/// </summary>
public sealed record RegisterDeceasedDeliveryCommand(
    Guid CasePublicId,
    DeliveryRoute Mode,
    IReadOnlyList<DeliveryRecipientInput>? Recipients,
    IReadOnlyList<Guid>? SupportAttachmentPublicIds,
    string ExpectedPreviewSha256,
    string Reason) : IRequest<Result<DeceasedHolderCaseDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDeceasedDeliveryCommandValidator : ValidadorConMotivo<RegisterDeceasedDeliveryCommand>
{
    public RegisterDeceasedDeliveryCommandValidator()
    {
        RuleFor(x => x.CasePublicId).NotEmpty();
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.ExpectedPreviewSha256).NotEmpty().Length(64);
    }
}

public sealed class RegisterDeceasedDeliveryCommandHandler(
    IApplicationDbContext db,
    VistaDeFallecimientos vista,
    Fallecimientos fallecimientos,
    RegistroDeIntervenciones intervenciones,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RegisterDeceasedDeliveryCommand, Result<DeceasedHolderCaseDto>>
{
    public async Task<Result<DeceasedHolderCaseDto>> Handle(RegisterDeceasedDeliveryCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leido = await vista.LeerAsync(request.CasePublicId, seguir: true, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var caso = leido.Value;
        if (caso.Status is not (DeceasedCaseStatus.Registered or DeceasedCaseStatus.DeliveryPending))
            return Falla(ErroresDeFallecimiento.Estado(caso.Status, "la entrega sólo se registra antes de aprobarla"));
        var destinatarios = await vista.DestinatariosAsync(request.Recipients, ct);
        if (destinatarios.IsFailure) return Falla(destinatarios.Error);
        var previa = await fallecimientos.VistaPreviaAsync(caso, request.Mode, destinatarios.Value, hoy, ct);
        if (previa.IsFailure) return Falla(previa.Error);
        if (!string.Equals(previa.Value.Sha256, request.ExpectedPreviewSha256, StringComparison.OrdinalIgnoreCase))
            return Falla(new ErrorConDatos(Assembly.ErroresDeAsamblea.CodigoVistaPreviaCambio,
                "La vista previa de la entrega cambió desde que la vio: revísela y vuelva a registrarla.", new { preview = await vista.PreviaDtoAsync(previa.Value, ct) }));

        // Los soportes: con dueño la persona fallecida.
        var soportes = (request.SupportAttachmentPublicIds ?? []).Distinct().ToList();
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == caso.PersonId).Select(p => p.PublicId).FirstAsync(ct);
        var existentes = soportes.Count == 0 ? 0 : await db.Attachments.AsNoTracking().CountAsync(a => soportes.Contains(a.PublicId) && !a.IsDeleted
            && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeFallecimiento && a.OwnerEntityPublicId == persona, ct);
        if (existentes != soportes.Count) return Falla(ErroresDeFallecimiento.SinSoportes(previa.Value.SoportesRequeridos));
        if (request.Mode != DeliveryRoute.Beneficiaries && soportes.Count < Math.Max(1, previa.Value.SoportesRequeridos.Count))
            return Falla(ErroresDeFallecimiento.SinSoportes(previa.Value.SoportesRequeridos));

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SegregacionDeCaptaciones.EntregaPorFallecimiento, caso.PublicId),
            AccionesDeSegregacion.Radicar, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var plan = new PlanDeEntrega(request.Mode, destinatarios.Value);
        var beneficiarios = JsonSerializer.Serialize(previa.Value.Beneficiarios.ToDictionary(k => k.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
            k => k.Value.Select(b => new { personId = b.PersonaId, share = b.Porcentaje }).ToList()));
        caso.RegistrarEntrega(request.Mode, Fallecimientos.Serializar(plan), beneficiarios, JsonSerializer.Serialize(soportes), previa.Value.Total,
            previa.Value.Tope?.Valor, previa.Value.Sha256, usuario, reloj.UtcNow);
        if (!string.IsNullOrWhiteSpace(request.Reason)) caso.Reason = DeceasedHolderCase.Recortar(request.Reason);
        intervenciones.Registrar(verificada.Value, SegregacionDeCaptaciones.EntregaPorFallecimiento, caso.PublicId);
        await db.SaveChangesAsync(ct);
        return Result.Success(await vista.DtoAsync(caso, conDetalle: true, ct));
    }

    private static Result<DeceasedHolderCaseDto> Falla(Error e) => Result.Failure<DeceasedHolderCaseDto>(e);
}

// ------------------------------------------------------------------------------------------------------ aprobar entrega --

/// <summary>
/// <c>POST /{id}/delivery/approve</c> (<c>ApproveDeceasedDeliveryCommand</c>, <c>Lending.DeceasedCases.Approve</c>): <b>otra persona</b> que quien registró la
/// entrega (<c>Lending.Segregation.Conflict</c>, FR-083) la aprueba y se entrega en el acto con el saldo del día (ver <see cref="Fallecimientos"/>): una
/// operación por destinatario y cuenta, con su orden de giro (US14-2: dos beneficiarios, dos órdenes) o su traslado; la cuenta se cancela cuando se pagan
/// todas. (nuevo)
/// </summary>
public sealed record ApproveDeceasedDeliveryCommand(Guid CasePublicId, string Reason) : IRequest<Result<DeceasedHolderCaseDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveDeceasedDeliveryCommandValidator : ValidadorConMotivo<ApproveDeceasedDeliveryCommand>
{
    public ApproveDeceasedDeliveryCommandValidator() => RuleFor(x => x.CasePublicId).NotEmpty();
}

public sealed class ApproveDeceasedDeliveryCommandHandler(
    IApplicationDbContext db,
    VistaDeFallecimientos vista,
    Fallecimientos fallecimientos,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IDateTimeService reloj,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null) : IRequestHandler<ApproveDeceasedDeliveryCommand, Result<DeceasedHolderCaseDto>>
{
    public async Task<Result<DeceasedHolderCaseDto>> Handle(ApproveDeceasedDeliveryCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leido = await vista.LeerAsync(request.CasePublicId, seguir: false, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        if (leido.Value.Status != DeceasedCaseStatus.DeliveryPending)
            return Falla(ErroresDeFallecimiento.Estado(leido.Value.Status, "sólo se aprueba una entrega registrada"));
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SegregacionDeCaptaciones.EntregaPorFallecimiento, leido.Value.PublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        // D23 (pendiente de la fase 17, research E206): la pantalla no aprueba si la liquidación y la entrega de las cuentas no se contabilizarían.
        if (validacionPrevia is not null)
        {
            var planPrevio = Fallecimientos.Leer(leido.Value.DeliveryPlanJson)!;
            var entregables = (await fallecimientos.CuentasAsync(leido.Value.PersonId, hoy, seguir: false, ct)).Where(c => c.Entregable)
                .Select(c => c.Cuenta.Id).ToList();
            var destinosPrevios = planPrevio.Destinatarios.Where(d => d.Forma == FormaDeEntrega.TransferToAccount && d.CuentaDestinoId is not null)
                .Select(d => d.CuentaDestinoId!.Value).Distinct().ToList();
            var hayGiros = planPrevio.Ruta == DeliveryRoute.Beneficiaries || planPrevio.Destinatarios.Any(d => d.Forma == FormaDeEntrega.DisbursementOrder);
            if (await ValidacionPreviaDelCicloDeVida.EntregaPorFallecimientoAsync(validacionPrevia, db, entregables, destinosPrevios, hayGiros, hoy,
                    leido.Value.Number, ct) is { } bloqueo)
                return Falla(bloqueo);
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var caso = await db.DeceasedHolderCases.FirstAsync(c => c.Id == leido.Value.Id, ct);
            if (caso.Status != DeceasedCaseStatus.DeliveryPending)
                return Falla(ErroresDeFallecimiento.Estado(caso.Status, "sólo se aprueba una entrega registrada"));
            var plan = Fallecimientos.Leer(caso.DeliveryPlanJson)!;
            var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => a.HolderPersonId == caso.PersonId && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
                .Select(a => a.Id).ToListAsync(ct);
            var destinos = plan.Destinatarios.Where(d => d.CuentaDestinoId is not null).Select(d => d.CuentaDestinoId!.Value);
            var personas = plan.Destinatarios.Select(d => d.PersonaId).Append(caso.PersonId).Distinct().Order().ToList();
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(personas, ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(cuentas.Concat(destinos).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            var entregado = await fallecimientos.EntregarAsync(caso, plan, hoy, request.Reason, ct);
            if (entregado.IsFailure) return Falla(entregado.Error);
            intervenciones.Registrar(verificada.Value, SegregacionDeCaptaciones.EntregaPorFallecimiento, caso.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await vista.DtoAsync(caso, conDetalle: true, ct));
        }, ct);
    }

    private static Result<DeceasedHolderCaseDto> Falla(Error e) => Result.Failure<DeceasedHolderCaseDto>(e);
}

// ----------------------------------------------------------------------------------------------------------------- anular --

/// <summary>
/// <c>POST /{id}/void</c> (<c>VoidDeceasedHolderCaseCommand</c>, <c>Lending.DeceasedCases.Approve</c>): anula un registro errado (antes de aprobar la entrega)
/// con motivo: levanta los bloqueos del fallecimiento y la causación vuelve a correr como antes. (nuevo)
/// </summary>
public sealed record VoidDeceasedHolderCaseCommand(Guid CasePublicId, string Reason) : IRequest<Result<DeceasedHolderCaseDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidDeceasedHolderCaseCommandValidator : ValidadorConMotivo<VoidDeceasedHolderCaseCommand>
{
    public VoidDeceasedHolderCaseCommandValidator() => RuleFor(x => x.CasePublicId).NotEmpty();
}

public sealed class VoidDeceasedHolderCaseCommandHandler(
    IApplicationDbContext db,
    VistaDeFallecimientos vista,
    Fallecimientos fallecimientos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<VoidDeceasedHolderCaseCommand, Result<DeceasedHolderCaseDto>>
{
    public async Task<Result<DeceasedHolderCaseDto>> Handle(VoidDeceasedHolderCaseCommand request, CancellationToken ct)
    {
        var leido = await vista.LeerAsync(request.CasePublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<DeceasedHolderCaseDto>(leido.Error);
        var caso = leido.Value;
        if (caso.Status is not (DeceasedCaseStatus.Registered or DeceasedCaseStatus.DeliveryPending))
            return Result.Failure<DeceasedHolderCaseDto>(ErroresDeFallecimiento.Estado(caso.Status, "sólo se anula antes de aprobar la entrega"));
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DeceasedHolderCaseDto>(ErroresDeGobierno.SinUsuario());
        var ahora = reloj.UtcNow;
        foreach (var cuenta in await db.AccountHolds.AsNoTracking().Where(h => h.DeceasedHolderCaseId == caso.Id && h.ReleasedAt == null && !h.IsDeleted)
                     .Select(h => h.MemberAccountId).Distinct().ToListAsync(ct))
            await fallecimientos.LevantarBloqueosAsync(cuenta, usuario, ahora, $"Caso {caso.Number} anulado: {DeceasedHolderCase.Recortar(request.Reason)}", ct);
        caso.Anular(usuario, ahora, request.Reason);
        await db.SaveChangesAsync(ct);
        return Result.Success(await vista.DtoAsync(caso, conDetalle: true, ct));
    }
}

// --------------------------------------------------------------------------------------------------------- consultas --

/// <summary>Los casos de fallecimiento (api.md §13, <c>GET /</c>): de las personas en el alcance, por estado. Lectura sensible auditada. (nuevo)</summary>
public sealed record ListDeceasedHolderCasesQuery(DeceasedCaseStatus? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<DeceasedHolderCaseDto>>>;

public sealed class ListDeceasedHolderCasesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
    VistaDeFallecimientos vista) : IRequestHandler<ListDeceasedHolderCasesQuery, Result<PagedResult<DeceasedHolderCaseDto>>>
{
    public async Task<Result<PagedResult<DeceasedHolderCaseDto>>> Handle(ListDeceasedHolderCasesQuery request, CancellationToken ct)
    {
        var consulta = db.DeceasedHolderCases.AsNoTracking().Where(c => !c.IsDeleted);
        if (request.Status is { } s) consulta = consulta.Where(c => c.Status == s);
        var todos = await consulta.OrderByDescending(c => c.ReportedOn).ThenByDescending(c => c.Id).Take(2_000).ToListAsync(ct);
        var visibles = new List<DeceasedHolderCase>();
        foreach (var c in todos)
            if (await AlcanceDeEmbargos.PersonaEnAlcanceAsync(db, alcance, auditoria, c.PersonId, ct)) visibles.Add(c);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var items = new List<DeceasedHolderCaseDto>();
        foreach (var c in visibles.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize))
            items.Add(await vista.DtoAsync(c, conDetalle: false, ct));
        foreach (var persona in items.Select(i => i.Person.PersonPublicId).Distinct())
            await auditoria.EmitirSaldosDePersonaVistosAsync(persona, nameof(ListDeceasedHolderCasesQuery), new { status = request.Status?.ToString() }, ct);
        return Result.Success(new PagedResult<DeceasedHolderCaseDto>(items, pagina.SafePage, pagina.SafePageSize, visibles.Count));
    }
}

/// <summary>
/// Un caso con sus cuentas, los beneficiarios vigentes en la fecha de la defunción, el plan y las entregas (api.md §13, <c>GET /{id}</c>). Lectura sensible
/// auditada. (nuevo)
/// </summary>
public sealed record GetDeceasedHolderCaseQuery(Guid CasePublicId) : IRequest<Result<DeceasedHolderCaseDto>>;

public sealed class GetDeceasedHolderCaseQueryHandler(VistaDeFallecimientos vista, LendingAuditEmitter auditoria)
    : IRequestHandler<GetDeceasedHolderCaseQuery, Result<DeceasedHolderCaseDto>>
{
    public async Task<Result<DeceasedHolderCaseDto>> Handle(GetDeceasedHolderCaseQuery request, CancellationToken ct)
    {
        var leido = await vista.LeerAsync(request.CasePublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<DeceasedHolderCaseDto>(leido.Error);
        var dto = await vista.DtoAsync(leido.Value, conDetalle: true, ct);
        await auditoria.EmitirSaldosDePersonaVistosAsync(dto.Person.PersonPublicId, nameof(GetDeceasedHolderCaseQuery), new { @case = dto.Number }, ct);
        return Result.Success(dto);
    }
}
