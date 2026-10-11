using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;

/// <summary>
/// Los pagarés (feature 013, US3, T0451; api.md §6.2, <c>GET /api/lending/promissory-notes?loanPublicId=&amp;applicationPublicId=&amp;status=&amp;page=</c>,
/// <c>Lending.Guarantees.Read</c>): los de solicitudes y créditos del alcance de oficina y línea (<see cref="AlcanceDeCartera"/>; los demás no
/// aparecen), con «perfeccionado» según la guardia (en producción, un simulado nunca). Los más recientes primero. (nuevo)
/// </summary>
public sealed record ListPromissoryNotesQuery(
    Guid? LoanPublicId = null,
    Guid? ApplicationPublicId = null,
    PromissoryNoteStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<PromissoryNoteDto>>>;

public sealed class ListPromissoryNotesQueryValidator : AbstractValidator<ListPromissoryNotesQuery>;

public sealed class ListPromissoryNotesQueryHandler(IApplicationDbContext db, GuardiaDeCanalesExternos guardia, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListPromissoryNotesQuery, Result<PagedResult<PromissoryNoteDto>>>
{
    public async Task<Result<PagedResult<PromissoryNoteDto>>> Handle(ListPromissoryNotesQuery request, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var solicitudes = db.CreditApplications.AsNoTracking().PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId).Select(a => a.Id);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.PromissoryNotes.AsNoTracking().Where(p => !p.IsDeleted
            && (p.LoanId != null ? creditos.Contains(p.LoanId.Value) : p.ApplicationId != null && solicitudes.Contains(p.ApplicationId.Value)));
        if (request.LoanPublicId is { } credito) consulta = consulta.Where(p => db.Loans.Any(l => l.Id == p.LoanId && l.PublicId == credito));
        if (request.ApplicationPublicId is { } solicitud)
            consulta = consulta.Where(p => db.CreditApplications.Any(a => a.Id == p.ApplicationId && a.PublicId == solicitud));
        if (request.Status is { } estado) consulta = consulta.Where(p => p.Status == estado);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(p => p.IssuedOn).ThenByDescending(p => p.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<PromissoryNoteDto>(await DepositoDelPagare.DtosAsync(db, guardia, filas, ct), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}
