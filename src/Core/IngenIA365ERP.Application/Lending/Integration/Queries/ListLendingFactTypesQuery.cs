using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Queries;

/// <summary>Un tipo de hecho de Cartera (<c>LND_FactTypes</c>): su código —el <c>Type</c> del mensaje—, su corto, su nombre y su modo por defecto.</summary>
public sealed record LendingFactTypeDto(
    Guid PublicId,
    string Code,
    string ShortCode,
    string Name,
    bool IsInformational,
    string? VoucherTypeCode,
    string DefaultMode,
    string? DefaultBatchTrigger);

/// <summary>
/// <c>GET /api/lending/fact-types</c> (feature 013, US7; contracts/api.md §3.4, <c>Lending.Parameters.View</c>): el catálogo sembrado de tipos de
/// hecho, que es el ámbito <c>LendingFactType</c> de los parámetros de contabilidad y el filtro por tipo del envío posterior de la bandeja
/// (<c>send-not-applicable</c> pide sus <c>PublicId</c>). Catálogo de la cooperativa, sin oficina ni deudores. (nuevo)
/// </summary>
public sealed record ListLendingFactTypesQuery : IRequest<Result<IReadOnlyList<LendingFactTypeDto>>>;

public sealed class ListLendingFactTypesQueryValidator : AbstractValidator<ListLendingFactTypesQuery>;

public sealed class ListLendingFactTypesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListLendingFactTypesQuery, Result<IReadOnlyList<LendingFactTypeDto>>>
{
    public async Task<Result<IReadOnlyList<LendingFactTypeDto>>> Handle(ListLendingFactTypesQuery request, CancellationToken ct)
    {
        var tipos = await db.LendingFactTypes.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.Code)
            .Select(t => new LendingFactTypeDto(t.PublicId, t.Code, t.ShortCode, t.Name, t.IsInformational, t.VoucherTypeCode, t.DefaultMode,
                t.DefaultBatchTrigger))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<LendingFactTypeDto>>(tipos);
    }
}
