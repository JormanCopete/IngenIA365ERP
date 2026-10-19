using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

// Feature 014, US11 (T189; api.md §4 /holds; data-model §5.1; FR-054, FR-057): los bloqueos de una cuenta por una persona —orden interna, garantía de un
// crédito de la Cartera, firma pendiente u otro—, por valor o total, con vigencia. Los de embargo, fallecimiento y listas los pone y levanta su proceso. (nuevo)

/// <summary>Los bloqueos de una cuenta (api.md §4, <c>GET /{id}/holds</c>, <c>Lending.Accounts.Read</c>), los vigentes o también los levantados. (nuevo)</summary>
public sealed record ListAccountHoldsQuery(Guid AccountPublicId, bool IncludeReleased = false) : IRequest<Result<IReadOnlyList<AccountHoldDto>>>;

public sealed class ListAccountHoldsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListAccountHoldsQuery, Result<IReadOnlyList<AccountHoldDto>>>
{
    public async Task<Result<IReadOnlyList<AccountHoldDto>>> Handle(ListAccountHoldsQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<IReadOnlyList<AccountHoldDto>>(leida.Error);
        var consulta = db.AccountHolds.AsNoTracking().Where(h => h.MemberAccountId == leida.Value.Id && !h.IsDeleted);
        if (!request.IncludeReleased) consulta = consulta.Where(h => h.ReleasedAt == null);
        var filas = await consulta.OrderByDescending(h => h.Id).ToListAsync(ct);
        return Result.Success(await DtosAsync(db, filas, ct));
    }

    public static async Task<IReadOnlyList<AccountHoldDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<AccountHold> filas, CancellationToken ct)
    {
        var creditos = filas.Where(h => h.LoanId != null).Select(h => h.LoanId!.Value).Distinct().ToList();
        var numeros = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Number, ct);
        var embargos = filas.Where(h => h.GarnishmentId != null).Select(h => h.GarnishmentId!.Value).Distinct().ToList();
        var publicos = await db.Garnishments.AsNoTracking().Where(g => embargos.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.PublicId, ct);
        return filas.Select(h => new AccountHoldDto(h.PublicId, h.Reason, h.Amount, h.EsTotal, h.ValidFrom, h.ValidTo, h.Notes,
            h.LoanId is { } l ? numeros.GetValueOrDefault(l) : null, h.GarnishmentId is { } g ? publicos.GetValueOrDefault(g) : null, h.ReleasedAt,
            h.ReleaseReason)).ToList();
    }
}

/// <summary>
/// Pone un bloqueo (api.md §4, <c>POST /{id}/holds</c>, <c>Lending.Holds.Manage</c>, con clave): motivo <c>Internal</c>, <c>LoanCollateral</c> (exige un
/// crédito vivo de la Cartera del titular), <c>SignaturePending</c> u <c>Other</c>; por valor (que no supere el disponible,
/// <c>Lending.Holds.InsufficientAvailable</c>) o total (sin valor: la cuenta no admite retiros ni cancelación, FR-039). (nuevo)
/// </summary>
public sealed record PlaceAccountHoldCommand(Guid AccountPublicId, HoldReason Reason, decimal? Amount, DateOnly? ValidTo, Guid? LoanPublicId,
    Guid? SupportAttachmentPublicId, string Notes) : IRequest<Result<AccountHoldDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    /// <summary>Los motivos que pone una persona; los demás los pone su proceso.</summary>
    public static readonly HoldReason[] Manuales = [HoldReason.Internal, HoldReason.LoanCollateral, HoldReason.SignaturePending, HoldReason.Other];
}

public sealed class PlaceAccountHoldCommandValidator : AbstractValidator<PlaceAccountHoldCommand>
{
    public PlaceAccountHoldCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Reason).Must(r => PlaceAccountHoldCommand.Manuales.Contains(r))
            .WithMessage("Por esta vía se pone un bloqueo interno, de garantía, de firma pendiente u otro; el de embargo nace con el embargo.");
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount is not null).WithMessage("Un bloqueo por valor es mayor que cero; sin valor es total.");
        RuleFor(x => x.LoanPublicId).NotEmpty().When(x => x.Reason == HoldReason.LoanCollateral).WithMessage("Elija el crédito que garantiza la cuenta.");
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(AccountHold.LargoDeLasNotas).WithMessage("Diga el motivo del bloqueo.");
    }
}

public sealed class PlaceAccountHoldCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReglasDeMovimientos movimientos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<PlaceAccountHoldCommand, Result<AccountHoldDto>>
{
    public async Task<Result<AccountHoldDto>> Handle(PlaceAccountHoldCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        if (cuenta.Status == MemberAccountStatus.Closed) return Falla(new Error(MemberAccount.CodigoCerrada, $"La cuenta {cuenta.Number} está cancelada."));
        if (request.ValidTo is { } hasta && hasta < hoy) return Falla(ErroresDeEmbargos.BloqueoInvalido("El bloqueo no vence antes de hoy.", "validTo"));
        int? credito = null;
        if (request.Reason == HoldReason.LoanCollateral)
        {
            var loan = await db.Loans.AsNoTracking().Where(l => l.PublicId == request.LoanPublicId && !l.IsDeleted)
                .Select(l => new { l.Id, l.Status, l.Number }).FirstOrDefaultAsync(ct);
            if (loan is null || loan.Status is not (LoanStatus.Active or LoanStatus.PendingDisbursement))
                return Falla(ErroresDeEmbargos.BloqueoInvalido("El crédito no existe o no está vivo: una cuenta garantiza un crédito vigente de la Cartera.",
                    "loanPublicId"));
            credito = loan.Id;
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            if (request.Amount is { } valor)
            {
                var c = await movimientos.LeerAsync(cuenta, hoy, ct);
                if (cuenta.Nature == LineNature.Deposit && valor > c.Disponible.Disponible)
                    return Falla(ErroresDeEmbargos.DisponibleInsuficiente(c.Disponible.Disponible, valor));
            }
            var actor = await actorActual.ObtenerAsync(ct);
            var bloqueo = AccountHold.Poner(cuenta.Id, request.Reason, request.Amount, hoy, request.ValidTo, request.Notes, request.SupportAttachmentPublicId,
                actor.UserId, loanId: credito);
            db.AccountHolds.Add(bloqueo);
            await db.SaveChangesAsync(ct);
            return Result.Success((await ListAccountHoldsQueryHandler.DtosAsync(db, [bloqueo], ct))[0]);
        }, ct);
    }

    private static Result<AccountHoldDto> Falla(Error e) => Result.Failure<AccountHoldDto>(e);
}

/// <summary>
/// Levanta un bloqueo (api.md §4, <c>POST /{id}/holds/{holdId}/release</c>, <c>Lending.Holds.Manage</c>, con clave, con motivo): sólo los que pone una
/// persona (<c>Lending.Holds.OwnedByProcess</c> para los demás); el de garantía, sólo con el crédito pagado o sin la garantía
/// (<c>Lending.Holds.LoanStillActive</c>). (nuevo)
/// </summary>
public sealed record ReleaseAccountHoldCommand(Guid AccountPublicId, Guid HoldPublicId, string Reason)
    : IRequest<Result<AccountHoldDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReleaseAccountHoldCommandValidator : ValidadorConMotivo<ReleaseAccountHoldCommand>
{
    public ReleaseAccountHoldCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.HoldPublicId).NotEmpty();
    }
}

public sealed class ReleaseAccountHoldCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ReleaseAccountHoldCommand, Result<AccountHoldDto>>
{
    public async Task<Result<AccountHoldDto>> Handle(ReleaseAccountHoldCommand request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        var vista = await db.AccountHolds.AsNoTracking()
            .FirstOrDefaultAsync(h => h.PublicId == request.HoldPublicId && h.MemberAccountId == cuenta.Id && h.ReleasedAt == null && !h.IsDeleted, ct);
        if (vista is null) return Falla(ErroresDeEmbargos.BloqueoNoEncontrado());
        if (!PlaceAccountHoldCommand.Manuales.Contains(vista.Reason)) return Falla(ErroresDeEmbargos.BloqueoDeProceso(vista.Reason.ToString()));
        if (vista.Reason == HoldReason.LoanCollateral && vista.LoanId is { } credito
            && await db.Loans.AsNoTracking().Where(l => l.Id == credito).Select(l => new { l.Status, l.Number }).FirstOrDefaultAsync(ct) is
                { Status: LoanStatus.Active or LoanStatus.PendingDisbursement or LoanStatus.WrittenOff } vivo)
            return Falla(ErroresDeEmbargos.GarantiaVigente(vivo.Number));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            var bloqueo = await db.AccountHolds.FirstAsync(h => h.Id == vista.Id, ct);
            if (bloqueo.ReleasedAt is not null) return Falla(ErroresDeEmbargos.BloqueoNoEncontrado());
            bloqueo.Levantar(reloj.UtcNow, (await actorActual.ObtenerAsync(ct)).UserId, request.Reason);
            await db.SaveChangesAsync(ct);
            return Result.Success((await ListAccountHoldsQueryHandler.DtosAsync(db, [bloqueo], ct))[0]);
        }, ct);
    }

    private static Result<AccountHoldDto> Falla(Error e) => Result.Failure<AccountHoldDto>(e);
}
