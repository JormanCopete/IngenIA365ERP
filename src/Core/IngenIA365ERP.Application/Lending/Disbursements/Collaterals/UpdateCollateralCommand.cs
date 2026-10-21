using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Collaterals;

/// <summary>
/// Edita los datos de una garantía (feature 013, US3, T0450; api.md §6.1, <c>PUT /api/lending/collaterals/{id}</c>, <c>Lending.Guarantees.Manage</c>,
/// con clave): descripción, valor y admisible, vigencia, avalúo, póliza, registro, grado y fondo, sólo mientras el dueño no esté decidido
/// (<c>Lending.Collateral.OwnerLocked</c>) y la garantía siga constituida o vigente. El dueño, la clase y el constituyente no cambian: de
/// <see cref="CollateralRegistrationInput"/> se ignoran. Un cambio de valor es una revaluación y, con A3, emite <c>GarantiaConstituida</c> (<c>Revalued</c>, la
/// diferencia con signo). (nuevo)
/// </summary>
public sealed record UpdateCollateralCommand(Guid CollateralPublicId, CollateralRegistrationInput Collateral) : IRequest<Result<CollateralDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCollateralCommandValidator : AbstractValidator<UpdateCollateralCommand>
{
    public UpdateCollateralCommandValidator()
    {
        RuleFor(x => x.CollateralPublicId).NotEmpty();
        RuleFor(x => x.Collateral).NotNull().SetValidator(new CollateralRegistrationInputValidator());
    }
}

public sealed class UpdateCollateralCommandHandler(
    IApplicationDbContext db,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<UpdateCollateralCommand, Result<CollateralDto>>
{
    public async Task<Result<CollateralDto>> Handle(UpdateCollateralCommand request, CancellationToken ct)
    {
        var leida = await GarantiasDeCartera.LeerAsync(db, alcance, auditoria, request.CollateralPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (g, dueno) = leida.Value;
        if (dueno.Decidido || g.LoanId is not null || g.Status is not (CollateralStatus.Constituted or CollateralStatus.Valid))
            return Falla(ErroresDeGarantias.DuenoDecidido(dueno.Clase));

        var e = request.Collateral;
        var avaluador = await PersonaAsync(e.AppraiserPersonPublicId, ct);
        if (avaluador.IsFailure) return Falla(avaluador.Error);
        var aseguradora = await PersonaAsync(e.PolicyInsurerPersonPublicId, ct);
        if (aseguradora.IsFailure) return Falla(aseguradora.Error);

        var antes = g.Value;
        RegisterCollateralCommandHandler.Aplicar(g, e, avaluador.Value, aseguradora.Value);
        var emitida = await GarantiasDeCartera.EmitirAsync(db, emision, fechaContable, reloj.HoyLocal, reloj.UtcNow, g, dueno,
            EmisionDeCartera.MovimientosDeGarantia.Revaluada, g.Value - antes, g.Value, ct);
        if (emitida.IsFailure) return Falla(emitida.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success((await GarantiasDeCartera.DtosAsync(db, [g], ct))[0]);
    }

    private async Task<Result<int?>> PersonaAsync(Guid? publicId, CancellationToken ct)
    {
        if (publicId is not { } id) return Result.Success<int?>(null);
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == id && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return persona is null ? Result.Failure<int?>(ErroresDeGobierno.PersonaInexistente()) : Result.Success(persona);
    }

    private static Result<CollateralDto> Falla(Error e) => Result.Failure<CollateralDto>(e);
}
