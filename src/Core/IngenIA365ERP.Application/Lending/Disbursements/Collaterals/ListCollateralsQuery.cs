using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Collaterals;

/// <summary>
/// Las garantías (feature 013, US3, T0450; api.md §6.1, <c>GET /api/lending/collaterals?loanPublicId=&amp;applicationPublicId=&amp;personPublicId=
/// &amp;kind=&amp;status=&amp;expiringWithinDays=&amp;page=</c>, <c>Lending.Guarantees.Read</c>): las de solicitudes y créditos del alcance de
/// oficina y línea (<see cref="AlcanceDeCartera"/>; las demás no aparecen), por dueño, constituyente, clase, estado o las que vencen —la garantía o
/// su póliza— dentro de los días pedidos. Las que vencen primero, primero. (nuevo)
/// </summary>
public sealed record ListCollateralsQuery(
    Guid? LoanPublicId = null,
    Guid? ApplicationPublicId = null,
    Guid? PersonPublicId = null,
    CollateralKind? Kind = null,
    CollateralStatus? Status = null,
    int? ExpiringWithinDays = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<CollateralDto>>>;

public sealed class ListCollateralsQueryValidator : AbstractValidator<ListCollateralsQuery>
{
    public ListCollateralsQueryValidator() =>
        RuleFor(x => x.ExpiringWithinDays).InclusiveBetween(0, 3650).When(x => x.ExpiringWithinDays is not null);
}

public sealed class ListCollateralsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListCollateralsQuery, Result<PagedResult<CollateralDto>>>
{
    public async Task<Result<PagedResult<CollateralDto>>> Handle(ListCollateralsQuery request, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var solicitudes = db.CreditApplications.AsNoTracking().PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId).Select(a => a.Id);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.Collaterals.AsNoTracking().Where(c => !c.IsDeleted
            && (c.LoanId != null ? creditos.Contains(c.LoanId.Value) : c.ApplicationId != null && solicitudes.Contains(c.ApplicationId.Value)));

        if (request.LoanPublicId is { } credito) consulta = consulta.Where(c => db.Loans.Any(l => l.Id == c.LoanId && l.PublicId == credito));
        if (request.ApplicationPublicId is { } solicitud)
            consulta = consulta.Where(c => db.CreditApplications.Any(a => a.Id == c.ApplicationId && a.PublicId == solicitud));
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(c => db.People.Any(p => p.Id == c.OwnerPersonId && p.PublicId == persona));
        if (request.Kind is { } clase) consulta = consulta.Where(c => c.Kind == clase);
        if (request.Status is { } estado) consulta = consulta.Where(c => c.Status == estado);
        if (request.ExpiringWithinDays is { } dias)
        {
            var hasta = reloj.HoyLocal.AddDays(dias);
            consulta = consulta.Where(c => (c.ValidUntil != null && c.ValidUntil <= hasta) || (c.PolicyExpiresOn != null && c.PolicyExpiresOn <= hasta));
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderBy(c => c.ValidUntil == null).ThenBy(c => c.ValidUntil).ThenByDescending(c => c.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<CollateralDto>(await GarantiasDeCartera.DtosAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }
}
