using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Los tramos desembolsados (feature 013, US3, T0445; api.md §9.1, <c>GET /api/lending/disbursements</c>, <c>Lending.Loans.Read</c>): por crédito,
/// fechas y forma, con su orden y si se reversó; filtra en silencio por el alcance de oficina y línea del crédito (<c>AlcanceDeCartera</c>). (nuevo)
/// </summary>
public sealed record ListLoanDisbursementsQuery(
    Guid? LoanPublicId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    DisbursementForm? Form = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<LoanDisbursementListItemDto>>>;

public sealed class ListLoanDisbursementsQueryValidator : AbstractValidator<ListLoanDisbursementsQuery>
{
    public ListLoanDisbursementsQueryValidator()
    {
        RuleFor(x => x.Form).IsInEnum().When(x => x.Form is not null);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListLoanDisbursementsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListLoanDisbursementsQuery, Result<PagedResult<LoanDisbursementListItemDto>>>
{
    public async Task<Result<PagedResult<LoanDisbursementListItemDto>>> Handle(ListLoanDisbursementsQuery request, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.LoanPublicId is { } credito) creditos = creditos.Where(l => l.PublicId == credito);

        var consulta = from o in db.LoanOperations.AsNoTracking()
                       join l in creditos on o.LoanId equals l.Id
                       where o.Kind == LoanOperationKind.Disbursement
                       select new { o, l };
        if (request.From is { } desde) consulta = consulta.Where(x => x.o.ValueDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(x => x.o.ValueDate <= hasta);
        if (request.Form is { } forma) consulta = consulta.Where(x => x.o.DisbursementForm == forma);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.o.RegisteredAt)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);
        var ids = filas.Select(x => x.o.Id).ToList();
        var ordenes = await db.DisbursementOrders.AsNoTracking().Where(d => ids.Contains(d.DisbursementOperationId))
            .ToDictionaryAsync(d => d.DisbursementOperationId, ct);

        var items = filas.Select(x =>
        {
            var orden = ordenes.GetValueOrDefault(x.o.Id);
            return new LoanDisbursementListItemDto(x.o.PublicId, x.o.Number, x.l.PublicId, x.l.Number, x.o.TrancheNumber ?? 1, x.o.Amount, orden?.NetAmount,
                (x.o.DisbursementForm ?? DisbursementForm.Transfer).ToString(), x.o.ValueDate,
                orden is null ? null : new DisbursementOrderRefDto(orden.PublicId, orden.Number, orden.Status.ToString()),
                x.o.ReversedByOperationId is not null);
        }).ToList();
        return Result.Success(new PagedResult<LoanDisbursementListItemDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}
