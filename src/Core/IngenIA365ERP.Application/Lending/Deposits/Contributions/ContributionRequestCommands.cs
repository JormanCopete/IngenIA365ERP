using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

// Feature 014, US3 (T086; api.md §7; research E58; FR-042, FR-043, FR-044, FR-048, FR-079, FR-080): la devolución de aportes sin retiro y la cesión entre
// asociados. Una persona las radica y otra las aprueba (segregación radicar × decidir, Lending.Contributions.Manage); al aprobar nace la operación —la
// devolución con su orden de giro a Tesorería, la cesión como traslado entre las dos cuentas de aportes— con sus hechos. (nuevo)

/// <summary>Una solicitud de devolución o de cesión de aportes. (nuevo)</summary>
public sealed record ContributionRequestDto(
    Guid PublicId,
    ContributionRequestKind Kind,
    ContributionRequestStatus Status,
    Guid AccountPublicId,
    string AccountNumber,
    MemberAccountPersonDto Holder,
    MemberAccountPersonDto? Target,
    decimal Amount,
    ContributionRefundCause? Cause,
    Guid? SessionItemPublicId,
    string Reason,
    DateTime RequestedAt,
    DateTime? DecidedAt,
    string? DecisionReason,
    string? OperationNumber,
    string? PayoutOrderNumber);

/// <summary>La forma de una devolución aprobada (api.md §7): por orden de giro a Tesorería o a una cuenta de ahorro del titular (K8).</summary>
public enum FormaDeDevolucionDeAportes
{
    DisbursementOrder = 1,
    TransferToAccount = 2,
}

/// <summary>El beneficiario bancario de la orden de giro (opcional: sin él, Tesorería paga por su medio).</summary>
public sealed record PayeeBankInput(Guid BankPublicId, byte AccountType, string AccountNumber);

// ------------------------------------------------------------------------------------------------------------ pedir --

/// <summary>
/// <c>POST /api/lending/contributions/refunds</c> (<c>RequestContributionRefundCommand</c>): pide devolver aportes sin retiro —el exceso sobre el límite de
/// participación (FR-043) u otra causa decidida—. Nunca perfora el mínimo no reducible (FR-042, <c>Lending.Contributions.MinimumBreached</c>) ni se pide con
/// créditos vivos (FR-044, <c>Lending.Contributions.HasLiveLoans</c>). Queda en espera de aprobación. (nuevo)
/// </summary>
public sealed record RequestContributionRefundCommand(Guid AccountPublicId, decimal Amount, ContributionRefundCause Cause, Guid? SessionItemPublicId,
    string Reason) : IRequest<Result<ContributionRequestDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// <c>POST /api/lending/contributions/assignments</c> (<c>RequestContributionAssignmentCommand</c>): pide ceder aportes a otro asociado con cuenta de aportes
/// viva, sólo si la línea (los estatutos) lo permite (FR-048, <c>Lending.Contributions.AssignmentNotAllowed</c>), sin perforar el mínimo ni con créditos
/// vivos del cedente. Queda en espera de aprobación. (nuevo)
/// </summary>
public sealed record RequestContributionAssignmentCommand(Guid FromAccountPublicId, Guid ToPersonPublicId, decimal Amount, Guid? SessionItemPublicId,
    string Reason) : IRequest<Result<ContributionRequestDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestContributionRefundCommandValidator : ValidadorConMotivo<RequestContributionRefundCommand>
{
    public RequestContributionRefundCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).Must(m => RegistroDeCuentas.Decimales(m) <= 2).WithMessage("El valor lleva a lo sumo dos decimales.");
        RuleFor(x => x.Cause).IsInEnum();
    }
}

public sealed class RequestContributionAssignmentCommandValidator : ValidadorConMotivo<RequestContributionAssignmentCommand>
{
    public RequestContributionAssignmentCommandValidator()
    {
        RuleFor(x => x.FromAccountPublicId).NotEmpty();
        RuleFor(x => x.ToPersonPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).Must(m => RegistroDeCuentas.Decimales(m) <= 2).WithMessage("El valor lleva a lo sumo dos decimales.");
    }
}

/// <summary>Lo común de las solicitudes de aportes: la cuenta en alcance y operable, las reglas del débito y el DTO. (nuevo)</summary>
public sealed class SolicitudesDeAportes(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, ReglasDeAportes reglas)
{
    /// <summary>El tipo de expediente de la segregación (radicar × decidir).</summary>
    public const string Expediente = SegregacionDeCaptaciones.SolicitudDeAportes;

    /// <summary>La cuenta de aportes en alcance, activa o inactiva, con aportes activados.</summary>
    public async Task<Result<MemberAccount>> CuentaAsync(Guid publicId, bool seguir, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, publicId, seguir, ct);
        if (leida.IsFailure) return leida;
        var cuenta = leida.Value;
        if (cuenta.Nature != LineNature.Contribution)
            return Result.Failure<MemberAccount>(ErroresDeCuentas.De(ProblemaDeCartera.Con(MemberAccount.CodigoNaturalezaDistinta,
                "La devolución y la cesión son de una cuenta de aportes.", "accountPublicId", publicId)));
        if ((await ActivacionesDeCaptacion.LeerAsync(db, ct)).Aportes is null) return Result.Failure<MemberAccount>(ErroresDeCuentas.SinActivar(LineNature.Contribution));
        if (cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive))
            return Result.Failure<MemberAccount>(ErroresDeAportes.NoOperable(cuenta.Number, cuenta.Status));
        return Result.Success(cuenta);
    }

    /// <summary>
    /// US16 (T162): la cuenta de aportes que <b>recibe</b> una cesión también tiene que estar en el alcance de quien pide o aprueba (la operación escribe en
    /// ella). Fuera responde lo mismo que si el asociado no tuviera cuenta de aportes, con <c>Lending.Access.OutOfScope</c>.
    /// </summary>
    public Task<Result> DestinoEnAlcanceAsync(MemberAccount destino, CancellationToken ct) =>
        alcance.AsegurarAsync(destino.BranchId, destino.CreditLineId, ErroresDeAportes.SinCuentaDeAportes(), auditoria, nameof(MemberAccount), destino.PublicId, ct);

    /// <summary>
    /// Las reglas del débito de aportes a <paramref name="fecha"/>: sin créditos vivos del titular (FR-044) y sin perforar el mínimo no reducible (FR-042).
    /// Devuelve las porciones por clase de aporte.
    /// </summary>
    public async Task<Result<IReadOnlyList<(MemberAccountConcept Concepto, decimal Monto)>>> DebitoAsync(MemberAccount cuenta, decimal monto, DateOnly fecha,
        CancellationToken ct)
    {
        var creditos = await reglas.CreditosVivosAsync(cuenta.HolderPersonId, ct);
        if (creditos.Count > 0)
            return Result.Failure<IReadOnlyList<(MemberAccountConcept, decimal)>>(ErroresDeAportes.ConCreditosVivos(creditos));
        var version = await reglas.VersionAsync(cuenta, fecha, ct);
        var minimo = version is null ? 0m : await reglas.MinimoAsync(version, cuenta.HolderPersonId, fecha, ct);
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var porciones = DebitoDeAportes.Repartir(saldos, monto, minimo);
        return porciones is null
            ? Result.Failure<IReadOnlyList<(MemberAccountConcept, decimal)>>(ErroresDeAportes.Minimo(minimo,
                LectorDeCuentas.SaldoDelTitular(LineNature.Contribution, saldos), monto))
            : Result.Success(porciones);
    }

    public async Task<ContributionRequestDto> DtoAsync(ContributionRequest s, CancellationToken ct)
    {
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == s.MemberAccountId, ct);
        var personas = await LectorDeCuentas.PersonasAsync(db, s.TargetPersonId is { } t ? [cuenta.HolderPersonId, t] : [cuenta.HolderPersonId], ct);
        var operacion = s.OperationId is { } op
            ? await db.MemberAccountOperations.AsNoTracking().Where(o => o.Id == op).Select(o => o.Number).FirstOrDefaultAsync(ct)
            : null;
        var orden = s.PayoutOrderId is { } o2
            ? await db.AccountPayoutOrders.AsNoTracking().Where(o => o.Id == o2).Select(o => o.Number).FirstOrDefaultAsync(ct)
            : null;
        return new ContributionRequestDto(s.PublicId, s.Kind, s.Status, cuenta.PublicId, cuenta.Number, personas[cuenta.HolderPersonId],
            s.TargetPersonId is { } tp ? personas[tp] : null, s.Amount, s.RefundCause, s.SessionItemPublicId, s.Reason, s.RequestedAt, s.DecidedAt,
            s.DecisionReason, operacion, orden);
    }
}

public sealed class RequestContributionRefundCommandHandler(
    IApplicationDbContext db,
    SolicitudesDeAportes solicitudes,
    ReglasDeAportes reglas,
    RegistroDeIntervenciones intervenciones,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RequestContributionRefundCommand, Result<ContributionRequestDto>>
{
    public async Task<Result<ContributionRequestDto>> Handle(RequestContributionRefundCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await solicitudes.CuentaAsync(request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        var debito = await solicitudes.DebitoAsync(cuenta, request.Amount, hoy, ct);
        if (debito.IsFailure) return Falla(debito.Error);

        // FR-043: el exceso sobre el límite de participación es lo que se devuelve por esa causa.
        if (request.Cause == ContributionRefundCause.ParticipationExcess)
        {
            var version = await reglas.VersionAsync(cuenta, hoy, ct);
            var limite = version is null ? null : await reglas.LimiteAsync(version, cuenta.HolderPersonId, hoy, ct);
            var actual = (await reglas.AportesDeAsync(cuenta.HolderPersonId, ct)).Values.Sum();
            var capital = await reglas.CapitalSocialAsync(ct);
            var exceso = limite is null ? 0m : Math.Max(0m, decimal.Round((actual - limite.Fraccion * capital) / (1m - limite.Fraccion), 2, MidpointRounding.ToZero));
            if (request.Amount > exceso)
                return Falla(new ErrorConDatos(ErroresDeAportes.CodigoExcesoInexistente,
                    $"El exceso sobre el límite de participación es {exceso:N2}: no se devuelve más por esa causa.", new { excess = exceso, requested = request.Amount }));
        }

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var solicitud = new ContributionRequest
        {
            Kind = ContributionRequestKind.Refund,
            MemberAccountId = cuenta.Id,
            Amount = request.Amount,
            RefundCause = request.Cause,
            SessionItemPublicId = request.SessionItemPublicId,
            Reason = ContributionRequest.Recortar(request.Reason),
            RequestedByUserId = usuario,
            RequestedAt = reloj.UtcNow,
        };
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SolicitudesDeAportes.Expediente, solicitud.PublicId),
            AccionesDeSegregacion.Radicar, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        db.ContributionRequests.Add(solicitud);
        intervenciones.Registrar(verificada.Value, SolicitudesDeAportes.Expediente, solicitud.PublicId);
        await db.SaveChangesAsync(ct);
        return Result.Success(await solicitudes.DtoAsync(solicitud, ct));
    }

    private static Result<ContributionRequestDto> Falla(Error e) => Result.Failure<ContributionRequestDto>(e);
}

public sealed class RequestContributionAssignmentCommandHandler(
    IApplicationDbContext db,
    SolicitudesDeAportes solicitudes,
    ReglasDeAportes reglas,
    RegistroDeIntervenciones intervenciones,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RequestContributionAssignmentCommand, Result<ContributionRequestDto>>
{
    public async Task<Result<ContributionRequestDto>> Handle(RequestContributionAssignmentCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await solicitudes.CuentaAsync(request.FromAccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        var version = await reglas.VersionAsync(cuenta, hoy, ct);
        if (version is not { AllowsTransferBetweenMembers: true }) return Falla(ErroresDeAportes.CesionNoAdmitida());
        var destino = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.ToPersonPublicId && !p.IsDeleted, ct);
        if (destino is null || destino.Id == cuenta.HolderPersonId)
            return Falla(new ErrorConDatos(ErroresDeAportes.CodigoSinCuentaDeAportes, "Elija otro asociado: los aportes se ceden a una persona distinta del titular.",
                new { personPublicId = request.ToPersonPublicId }));
        var cuentaDestino = await db.MemberAccounts.AsNoTracking().Where(a => a.HolderPersonId == destino.Id && a.Nature == LineNature.Contribution
            && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive) && !a.IsDeleted).OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        if (cuentaDestino is null) return Falla(ErroresDeAportes.SinCuentaDeAportes());
        if (await solicitudes.DestinoEnAlcanceAsync(cuentaDestino, ct) is { IsFailure: true } fuera) return Falla(fuera.Error);
        var debito = await solicitudes.DebitoAsync(cuenta, request.Amount, hoy, ct);
        if (debito.IsFailure) return Falla(debito.Error);

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var solicitud = new ContributionRequest
        {
            Kind = ContributionRequestKind.Assignment,
            MemberAccountId = cuenta.Id,
            TargetPersonId = destino.Id,
            Amount = request.Amount,
            SessionItemPublicId = request.SessionItemPublicId,
            Reason = ContributionRequest.Recortar(request.Reason),
            RequestedByUserId = usuario,
            RequestedAt = reloj.UtcNow,
        };
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SolicitudesDeAportes.Expediente, solicitud.PublicId),
            AccionesDeSegregacion.Radicar, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        db.ContributionRequests.Add(solicitud);
        intervenciones.Registrar(verificada.Value, SolicitudesDeAportes.Expediente, solicitud.PublicId);
        await db.SaveChangesAsync(ct);
        return Result.Success(await solicitudes.DtoAsync(solicitud, ct));
    }

    private static Result<ContributionRequestDto> Falla(Error e) => Result.Failure<ContributionRequestDto>(e);
}

// ---------------------------------------------------------------------------------------------------------- aprobar --

/// <summary>
/// <c>POST /api/lending/contributions/refunds/{refundId}/approve</c> (<c>ApproveContributionRefundCommand</c>): otra persona aprueba la devolución (segregación,
/// <c>Lending.Segregation.Conflict</c>), se revalidan el mínimo y los créditos vivos, y el capital social no baja del mínimo irreducible de la entidad
/// (FR-080, <c>Lending.Contributions.EntityMinimumBreached</c>: la solicitud sigue en espera). Nace la operación <c>ContributionRefund</c> que debita los
/// aportes, la orden de giro a Tesorería y los hechos <c>DevolucionOrdenada</c> y <c>OrdenDeGiroEmitida</c> v2, en una transacción. La devolución a una
/// cuenta de ahorro llega con K8. (nuevo)
/// </summary>
public sealed record ApproveContributionRefundCommand(Guid RefundPublicId, FormaDeDevolucionDeAportes Form, PayeeBankInput? Payee, Guid? TargetAccountPublicId,
    string Reason) : IRequest<Result<ContributionRequestDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// <c>POST /api/lending/contributions/assignments/{assignmentId}/approve</c> (<c>ApproveContributionAssignmentCommand</c>): otra persona aprueba la cesión; se
/// revalidan el mínimo, los créditos vivos y el límite de participación del que recibe (FR-043), y nace el traslado entre las dos cuentas de aportes con
/// <c>TrasladoEntreCuentasAplicado</c>. (nuevo)
/// </summary>
public sealed record ApproveContributionAssignmentCommand(Guid AssignmentPublicId, string Reason) : IRequest<Result<ContributionRequestDto>>,
    IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary><c>POST …/refunds/{id}/reject</c> y <c>…/assignments/{id}/reject</c>: otra persona niega la solicitud con motivo. (nuevo)</summary>
public sealed record RejectContributionRequestCommand(Guid RequestPublicId, string Reason) : IRequest<Result<ContributionRequestDto>>, IOperacionIdempotente,
    IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveContributionRefundCommandValidator : ValidadorConMotivo<ApproveContributionRefundCommand>
{
    public ApproveContributionRefundCommandValidator()
    {
        RuleFor(x => x.RefundPublicId).NotEmpty();
        RuleFor(x => x.Form).IsInEnum();
        RuleFor(x => x.Payee!.AccountNumber).NotEmpty().MaximumLength(25).When(x => x.Payee is not null);
    }
}

public sealed class ApproveContributionAssignmentCommandValidator : ValidadorConMotivo<ApproveContributionAssignmentCommand>
{
    public ApproveContributionAssignmentCommandValidator() => RuleFor(x => x.AssignmentPublicId).NotEmpty();
}

public sealed class RejectContributionRequestCommandValidator : ValidadorConMotivo<RejectContributionRequestCommand>
{
    public RejectContributionRequestCommandValidator() => RuleFor(x => x.RequestPublicId).NotEmpty();
}

public sealed class ApproveContributionRefundCommandHandler(
    IApplicationDbContext db,
    SolicitudesDeAportes solicitudes,
    ReglasDeAportes reglas,
    RegistroDeIntervenciones intervenciones,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApproveContributionRefundCommand, Result<ContributionRequestDto>>
{
    /// <summary>El prefijo del comprobante de una devolución de aportes.</summary>
    public const string Prefijo = "DVC-";

    public async Task<Result<ContributionRequestDto>> Handle(ApproveContributionRefundCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if (request.Form == FormaDeDevolucionDeAportes.TransferToAccount)
            return Falla(ErroresDeAportes.EntregaNoDisponible("La devolución a una cuenta de ahorro", nameof(EntregaDeCartera.K8)));
        var solicitud = await db.ContributionRequests.FirstOrDefaultAsync(s => s.PublicId == request.RefundPublicId
            && s.Kind == ContributionRequestKind.Refund && !s.IsDeleted, ct);
        if (solicitud is null) return Falla(ErroresDeAportes.SolicitudInexistente());
        var cuentaPublica = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == solicitud.MemberAccountId).Select(a => a.PublicId).FirstAsync(ct);
        var leida = await solicitudes.CuentaAsync(cuentaPublica, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        if (solicitud.Status != ContributionRequestStatus.PendingApproval) return Falla(ErroresDeAportes.SolicitudDecidida(solicitud.Status.ToString()));
        var cuenta = leida.Value;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SolicitudesDeAportes.Expediente, solicitud.PublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var debito = await solicitudes.DebitoAsync(cuenta, solicitud.Amount, hoy, ct);
        if (debito.IsFailure) return Falla(debito.Error);
        if (await reglas.MinimoDeLaEntidadAsync(hoy, ct) is { } minimoDeLaEntidad and > 0m)
        {
            var capital = await reglas.CapitalSocialAsync(ct);
            if (capital - solicitud.Amount < minimoDeLaEntidad) return Falla(ErroresDeAportes.MinimoDeLaEntidad(minimoDeLaEntidad, capital));
        }
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == cuenta.HolderPersonId, ct);
        var banco = request.Payee is { } p
            ? await db.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == p.BankPublicId && !b.IsDeleted, ct)
            : null;
        if (request.Payee is not null && banco is null)
            return Falla(new ErrorConDatos("Generic.NotFound", "El banco del beneficiario no existe.", new { field = "payee.bankPublicId" }));
        var version = await reglas.VersionAsync(cuenta, hoy, ct);
        var actor = await actorActual.ObtenerAsync(ct);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cuenta.HolderPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);

            var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
            if (hecho.IsFailure) return Falla(hecho.Error);
            var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
            var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.ContributionRefund, Prefijo, ct);
            var ahora = reloj.UtcNow;
            var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
            {
                MemberAccountId = cuenta.Id,
                Kind = MemberAccountOperationKind.ContributionRefund,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = hoy,
                AccountingDate = hecho.Value.FechaContable,
                RegisteredAt = sello.RegisteredAt,
                Amount = solicitud.Amount,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                Reason = solicitud.Reason,
                Payload = $"{{\"requestPublicId\":\"{solicitud.PublicId:N}\"}}",
                InputsSha256 = new HuellaDeCartera().Identificador("request", solicitud.PublicId).Decimal("amount", solicitud.Amount, 2).Calcular(),
                RulesVersion = VersionesDeReglas.Vigente,
            });
            await db.SaveChangesAsync(ct);

            var orden = AccountPayoutOrder.Emitir(cuenta.Id, operacion.Id, PayoutPurpose.ContributionRefund, titular.Id, titular.IdType ?? string.Empty,
                titular.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(titular), banco?.Id, request.Payee?.AccountType, request.Payee?.AccountNumber,
                solicitud.Amount, hoy.AddDays(Math.Max(1, version?.RefundTermDays ?? 0)), ahora, actor.UserId);
            await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
            db.AccountPayoutOrders.Add(orden);
            await db.SaveChangesAsync(ct);

            var valores = debito.Value.Select(d => new DepositConceptAmountV1
            {
                Concept = d.Concepto.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(d.Concepto)?.ToString(), Amount = -d.Monto,
                ReasonCode = "ContributionRefund",
            }).ToList();
            var beneficiario = new PayeeV1
            {
                PersonPublicId = titular.PublicId, TaxIdType = titular.IdType ?? string.Empty, TaxId = titular.TaxId ?? string.Empty,
                Name = ReferenciasDeGobierno.Nombre(titular), BankCode = banco?.TransferCode, BankAccountNumber = request.Payee?.AccountNumber,
                BankAccountType = request.Payee?.AccountType.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            var unidad = await emision.DevolucionOrdenadaAsync(new DevolucionAEmitir(hecho.Value, operacion, valores, "ContributionRefund", orden, beneficiario,
                titular.PublicId, "ContributionRefund", cuenta.Number), ct);
            if (unidad.IsFailure) throw new InvalidOperationException($"{unidad.Error.Code}: {unidad.Error.Message}");
            var mensaje = unidad.Value.De(DevolucionOrdenadaV1.Type);
            registro.RegistrarTransacciones(debito.Value.Select(d => new MemberAccountTransaction
            {
                MemberAccountId = cuenta.Id,
                Operation = operacion,
                OperationId = operacion.Id,
                Kind = MemberAccountTransactionKind.Refund,
                Concept = d.Concepto,
                Amount = -d.Monto,
                ValueDate = hoy,
                AccountingDate = hecho.Value.FechaContable,
                RegisteredAt = operacion.RegisteredAt,
                MessagePublicId = mensaje,
            }).ToList());
            solicitud.Aprobar(actor.UserId ?? 0, ahora, request.Reason, operacion.Id, orden.Id);
            intervenciones.Registrar(verificada.Value, SolicitudesDeAportes.Expediente, solicitud.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await solicitudes.DtoAsync(solicitud, ct));
        }, ct);
    }

    private static Result<ContributionRequestDto> Falla(Error e) => Result.Failure<ContributionRequestDto>(e);
}

public sealed class ApproveContributionAssignmentCommandHandler(
    IApplicationDbContext db,
    SolicitudesDeAportes solicitudes,
    ReglasDeAportes reglas,
    RegistroDeIntervenciones intervenciones,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApproveContributionAssignmentCommand, Result<ContributionRequestDto>>
{
    /// <summary>El prefijo del comprobante de una cesión de aportes (un traslado entre cuentas).</summary>
    public const string Prefijo = "TRC-";

    public async Task<Result<ContributionRequestDto>> Handle(ApproveContributionAssignmentCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var solicitud = await db.ContributionRequests.FirstOrDefaultAsync(s => s.PublicId == request.AssignmentPublicId
            && s.Kind == ContributionRequestKind.Assignment && !s.IsDeleted, ct);
        if (solicitud is null) return Falla(ErroresDeAportes.SolicitudInexistente());
        var cuentaPublica = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == solicitud.MemberAccountId).Select(a => a.PublicId).FirstAsync(ct);
        var leida = await solicitudes.CuentaAsync(cuentaPublica, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        if (solicitud.Status != ContributionRequestStatus.PendingApproval) return Falla(ErroresDeAportes.SolicitudDecidida(solicitud.Status.ToString()));
        var origen = leida.Value;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SolicitudesDeAportes.Expediente, solicitud.PublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var version = await reglas.VersionAsync(origen, hoy, ct);
        if (version is not { AllowsTransferBetweenMembers: true }) return Falla(ErroresDeAportes.CesionNoAdmitida());
        var destino = await db.MemberAccounts.Where(a => a.HolderPersonId == solicitud.TargetPersonId && a.Nature == LineNature.Contribution
                && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive) && !a.IsDeleted)
            .OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        if (destino is null) return Falla(ErroresDeAportes.SinCuentaDeAportes());
        if (await solicitudes.DestinoEnAlcanceAsync(destino, ct) is { IsFailure: true } fuera) return Falla(fuera.Error);
        var debito = await solicitudes.DebitoAsync(origen, solicitud.Amount, hoy, ct);
        if (debito.IsFailure) return Falla(debito.Error);
        var versionDestino = await reglas.VersionAsync(destino, hoy, ct);
        if (versionDestino is { ExcessAction: ExcessContributionAction.Reject }
            && await reglas.LimiteAsync(versionDestino, destino.HolderPersonId, hoy, ct) is { } limite)
        {
            var actual = (await reglas.AportesDeAsync(destino.HolderPersonId, ct)).Values.Sum();
            var capital = await reglas.CapitalSocialAsync(ct);
            if (capital > 0m && (actual + solicitud.Amount) / capital > limite.Fraccion)
                return Falla(ErroresDeAportes.LimiteDeParticipacion(limite.Fraccion, actual, solicitud.Amount, limite.Fuente));
        }
        var actor = await actorActual.ObtenerAsync(ct);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(new[] { origen.HolderPersonId, destino.HolderPersonId }.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(new[] { origen.Id, destino.Id }.Order().ToList(), ModoDeEspera.Esperar, ct);

            var hechoOrigen = await emision.HechoDeCuentaAsync(origen, hoy, ct);
            if (hechoOrigen.IsFailure) return Falla(hechoOrigen.Error);
            var hechoDestino = await emision.HechoDeCuentaAsync(destino, hoy, ct);
            if (hechoDestino.IsFailure) return Falla(hechoDestino.Error);
            var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == origen.Id, ct);
            var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Transfer, Prefijo, ct);
            var ahora = reloj.UtcNow;
            var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
            {
                MemberAccountId = origen.Id,
                CounterpartMemberAccountId = destino.Id,
                Kind = MemberAccountOperationKind.Transfer,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = hoy,
                AccountingDate = hechoOrigen.Value.FechaContable,
                RegisteredAt = sello.RegisteredAt,
                Amount = solicitud.Amount,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                Reason = solicitud.Reason,
                Payload = $"{{\"requestPublicId\":\"{solicitud.PublicId:N}\"}}",
                InputsSha256 = new HuellaDeCartera().Identificador("request", solicitud.PublicId).Decimal("amount", solicitud.Amount, 2).Calcular(),
                RulesVersion = VersionesDeReglas.Vigente,
            });
            var deOrigen = debito.Value.Select(d => new DepositConceptAmountV1
            {
                Concept = d.Concepto.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(d.Concepto)?.ToString(), Amount = -d.Monto,
            }).ToList();
            var aDestino = debito.Value.Select(d => new DepositConceptAmountV1
            {
                Concept = d.Concepto.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(d.Concepto)?.ToString(), Amount = d.Monto,
            }).ToList();
            var unidad = await emision.TrasladoEntreCuentasAsync(new TrasladoAEmitir(hechoOrigen.Value, hechoDestino.Value, operacion, deOrigen, aDestino), ct);
            if (unidad.IsFailure) throw new InvalidOperationException($"{unidad.Error.Code}: {unidad.Error.Message}");
            var mensaje = unidad.Value.De(TrasladoEntreCuentasAplicadoV1.Type);
            var filas = debito.Value.SelectMany(d => new[]
            {
                Fila(origen.Id, operacion, MemberAccountTransactionKind.TransferOut, d.Concepto, -d.Monto, hoy, hechoOrigen.Value.FechaContable, mensaje),
                Fila(destino.Id, operacion, MemberAccountTransactionKind.TransferIn, d.Concepto, d.Monto, hoy, hechoOrigen.Value.FechaContable, mensaje),
            }).ToList();
            registro.RegistrarTransacciones(filas);
            await db.SaveChangesAsync(ct);
            solicitud.TargetMemberAccountId = destino.Id;
            solicitud.Aprobar(actor.UserId ?? 0, ahora, request.Reason, operacion.Id, null);
            intervenciones.Registrar(verificada.Value, SolicitudesDeAportes.Expediente, solicitud.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await solicitudes.DtoAsync(solicitud, ct));
        }, ct);
    }

    private static MemberAccountTransaction Fila(int cuenta, MemberAccountOperation operacion, MemberAccountTransactionKind clase, MemberAccountConcept concepto,
        decimal monto, DateOnly fecha, DateOnly contable, Guid? mensaje) => new()
    {
        MemberAccountId = cuenta,
        Operation = operacion,
        Kind = clase,
        Concept = concepto,
        Amount = monto,
        ValueDate = fecha,
        AccountingDate = contable,
        RegisteredAt = operacion.RegisteredAt,
        MessagePublicId = mensaje,
    };

    private static Result<ContributionRequestDto> Falla(Error e) => Result.Failure<ContributionRequestDto>(e);
}

public sealed class RejectContributionRequestCommandHandler(
    IApplicationDbContext db,
    SolicitudesDeAportes solicitudes,
    RegistroDeIntervenciones intervenciones,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RejectContributionRequestCommand, Result<ContributionRequestDto>>
{
    public async Task<Result<ContributionRequestDto>> Handle(RejectContributionRequestCommand request, CancellationToken ct)
    {
        var solicitud = await db.ContributionRequests.FirstOrDefaultAsync(s => s.PublicId == request.RequestPublicId && !s.IsDeleted, ct);
        if (solicitud is null) return Result.Failure<ContributionRequestDto>(ErroresDeAportes.SolicitudInexistente());
        var cuentaPublica = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == solicitud.MemberAccountId).Select(a => a.PublicId).FirstAsync(ct);
        var leida = await solicitudes.CuentaAsync(cuentaPublica, seguir: false, ct);
        if (leida.IsFailure && leida.Error.Code == ErroresDeCuentas.CodigoCuentaInexistente) return Result.Failure<ContributionRequestDto>(leida.Error);
        if (solicitud.Status != ContributionRequestStatus.PendingApproval)
            return Result.Failure<ContributionRequestDto>(ErroresDeAportes.SolicitudDecidida(solicitud.Status.ToString()));
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SolicitudesDeAportes.Expediente, solicitud.PublicId),
            AccionesDeSegregacion.Decidir, reloj.HoyLocal, ct);
        if (verificada.IsFailure) return Result.Failure<ContributionRequestDto>(verificada.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        solicitud.Negar(actor.UserId ?? 0, reloj.UtcNow, request.Reason);
        intervenciones.Registrar(verificada.Value, SolicitudesDeAportes.Expediente, solicitud.PublicId);
        await db.SaveChangesAsync(ct);
        return Result.Success(await solicitudes.DtoAsync(solicitud, ct));
    }
}

// ------------------------------------------------------------------------------------------------------------ listar --

/// <summary>
/// <c>GET /api/lending/contributions/refunds</c> y <c>/assignments</c> (<c>ListContributionRefundsQuery</c>): las solicitudes de una clase, por estado, de las
/// cuentas en alcance, las más recientes primero. (nuevo)
/// </summary>
public sealed record ListContributionRequestsQuery(ContributionRequestKind Kind, ContributionRequestStatus? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<ContributionRequestDto>>>;

public sealed class ListContributionRequestsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, SolicitudesDeAportes solicitudes)
    : IRequestHandler<ListContributionRequestsQuery, Result<PagedResult<ContributionRequestDto>>>
{
    public async Task<Result<PagedResult<ContributionRequestDto>>> Handle(ListContributionRequestsQuery request, CancellationToken ct)
    {
        var vigente = await alcance.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking().PorOficinaYLinea(vigente, a => a.BranchId, a => a.CreditLineId).Select(a => a.Id);
        var consulta = db.ContributionRequests.AsNoTracking()
            .Where(s => s.Kind == request.Kind && !s.IsDeleted && (request.Status == null || s.Status == request.Status) && cuentas.Contains(s.MemberAccountId));
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(s => s.Id).Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var items = new List<ContributionRequestDto>();
        foreach (var s in filas) items.Add(await solicitudes.DtoAsync(s, ct));
        return Result.Success(new PagedResult<ContributionRequestDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}
