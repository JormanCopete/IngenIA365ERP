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
/// Cambia el estado de una garantía (feature 013, US3, T0450; api.md §6.1, <c>POST /api/lending/collaterals/{id}/status</c>,
/// <c>Lending.Guarantees.Manage</c>, con clave y motivo): <c>Constituted → Valid</c> (perfeccionada: sus documentos y el registro) <c>→ Extinguished
/// | Executed</c>, y de constituida a extinguida; una transición que no cabe responde <c>Lending.Collateral.TransitionInvalid</c> con
/// <c>from</c>/<c>to</c>. Liberar no va por aquí: exige la firmeza de la cancelación (<see cref="ReleaseCollateralCommand"/>). Extinguir anota la
/// fecha y el motivo; ejecutar, la fecha. Con A3, extinguir o ejecutar emite <c>GarantiaConstituida</c> por todo el valor, en negativo. El soporte
/// del cambio, si viene, queda en la auditoría del comando. (nuevo)
/// </summary>
public sealed record ChangeCollateralStatusCommand(Guid CollateralPublicId, CollateralStatus Status, string Reason, Guid? SupportAttachmentPublicId = null)
    : IRequest<Result<CollateralDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ChangeCollateralStatusCommandValidator : AbstractValidator<ChangeCollateralStatusCommand>
{
    public ChangeCollateralStatusCommandValidator()
    {
        RuleFor(x => x.CollateralPublicId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum().NotEqual(CollateralStatus.Released)
            .WithMessage("La liberación va por «Liberar»: exige el crédito cancelado y en firme.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué cambia el estado de la garantía.").MaximumLength(300);
    }
}

public sealed class ChangeCollateralStatusCommandHandler(
    IApplicationDbContext db,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ChangeCollateralStatusCommand, Result<CollateralDto>>
{
    public async Task<Result<CollateralDto>> Handle(ChangeCollateralStatusCommand request, CancellationToken ct)
    {
        var leida = await GarantiasDeCartera.LeerAsync(db, alcance, auditoria, request.CollateralPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (g, dueno) = leida.Value;
        if (g.PuedeIrA(request.Status) is { } problema) return Falla(ErroresDeGobierno.De(problema));

        var hoy = reloj.HoyLocal;
        g.Status = request.Status;
        string? movimiento = null;
        switch (request.Status)
        {
            case CollateralStatus.Extinguished:
                g.ExtinguishedOn = hoy;
                g.ExtinguishReason = request.Reason.Trim();
                movimiento = EmisionDeCartera.MovimientosDeGarantia.Extinguida;
                break;
            case CollateralStatus.Executed:
                g.ExecutedOn = hoy;
                movimiento = EmisionDeCartera.MovimientosDeGarantia.Ejecutada;
                break;
        }
        if (movimiento is not null)
        {
            var emitida = await GarantiasDeCartera.EmitirAsync(db, emision, fechaContable, hoy, reloj.UtcNow, g, dueno, movimiento, -g.Value, 0m, ct);
            if (emitida.IsFailure) return Falla(emitida.Error);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success((await GarantiasDeCartera.DtosAsync(db, [g], ct))[0]);
    }

    private static Result<CollateralDto> Falla(Error e) => Result.Failure<CollateralDto>(e);
}
