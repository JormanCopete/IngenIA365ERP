using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Disbursements.Collaterals;

/// <summary>
/// Libera una garantía (feature 013, US3, T0450; api.md §6.1, <c>POST /api/lending/collaterals/{id}/release</c>, <c>Lending.Guarantees.Release</c>,
/// con clave y motivo; FR-082): sólo una vigente (<c>Lending.Collateral.TransitionInvalid</c>), del crédito cancelado
/// (<c>Lending.Collateral.LoanNotPaidOff</c>) y con el pago que lo canceló ya en firme (<c>Lending.Collateral.NotFinal</c> con
/// <c>finalityUntil</c>). Anota la fecha, quién liberó y el pago que canceló; con A3, emite <c>GarantiaConstituida</c> (<c>Released</c>, todo el
/// valor en negativo). (nuevo)
/// </summary>
public sealed record ReleaseCollateralCommand(Guid CollateralPublicId, string Reason) : IRequest<Result<CollateralDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReleaseCollateralCommandValidator : AbstractValidator<ReleaseCollateralCommand>
{
    public ReleaseCollateralCommandValidator()
    {
        RuleFor(x => x.CollateralPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se libera la garantía.").MaximumLength(300);
    }
}

public sealed class ReleaseCollateralCommandHandler(
    IApplicationDbContext db,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ReleaseCollateralCommand, Result<CollateralDto>>
{
    public async Task<Result<CollateralDto>> Handle(ReleaseCollateralCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leida = await GarantiasDeCartera.LeerAsync(db, alcance, auditoria, request.CollateralPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (g, dueno) = leida.Value;
        if (g.PuedeIrA(CollateralStatus.Released) is { } problema) return Falla(ErroresDeGobierno.De(problema));

        var hoy = reloj.HoyLocal;
        var (sinFirmeza, pago) = await GarantiasDeCartera.FirmezaDeLaCancelacionAsync(db, g.LoanId, hoy, ct);
        if (sinFirmeza is not null) return Falla(sinFirmeza);

        g.Status = CollateralStatus.Released;
        g.ReleasedOn = hoy;
        g.ReleasedByUserId = usuario;
        g.ReleaseOperationId = pago;
        var emitida = await GarantiasDeCartera.EmitirAsync(db, emision, fechaContable, hoy, reloj.UtcNow, g, dueno,
            EmisionDeCartera.MovimientosDeGarantia.Liberada, -g.Value, 0m, ct);
        if (emitida.IsFailure) return Falla(emitida.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success((await GarantiasDeCartera.DtosAsync(db, [g], ct))[0]);
    }

    private static Result<CollateralDto> Falla(Error e) => Result.Failure<CollateralDto>(e);
}
