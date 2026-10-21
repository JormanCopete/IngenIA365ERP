using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Disbursements.Collaterals;

/// <summary>
/// Una garantía con sus documentos (feature 013, US3, T0450; api.md §6.1, <c>GET /api/lending/collaterals/{id}</c>, <c>Lending.Guarantees.Read</c>):
/// la de un dueño fuera del alcance de oficina y línea (<see cref="AlcanceDeCartera"/>) responde el mismo 404 que la que no existe
/// (<c>Lending.Collateral.NotFound</c>). (nuevo)
/// </summary>
public sealed record GetCollateralQuery(Guid CollateralPublicId) : IRequest<Result<CollateralDto>>;

public sealed class GetCollateralQueryValidator : AbstractValidator<GetCollateralQuery>
{
    public GetCollateralQueryValidator() => RuleFor(x => x.CollateralPublicId).NotEmpty();
}

public sealed class GetCollateralQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetCollateralQuery, Result<CollateralDto>>
{
    public async Task<Result<CollateralDto>> Handle(GetCollateralQuery request, CancellationToken ct)
    {
        var leida = await GarantiasDeCartera.LeerAsync(db, alcance, auditoria, request.CollateralPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<CollateralDto>(leida.Error);
        return Result.Success((await GarantiasDeCartera.DtosAsync(db, [leida.Value.Garantia], ct))[0]);
    }
}
