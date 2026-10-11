using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

/// <summary>
/// <c>GET /api/lending/loans/{id}/transactions?from=&amp;to=&amp;concept=&amp;page=</c> (feature 013, US4, T0504; api.md §8.1, <c>Lending.Loans.Read</c>): el
/// libro del crédito, <b>sólo lectura</b>, en orden de día valor, con su operación o su corrida, el espejo que lo reversa y el mensaje que lo
/// llevó. (nuevo)
/// </summary>
public sealed record ListLoanTransactionsQuery(Guid LoanPublicId, DateOnly? From = null, DateOnly? To = null, LoanConcept? Concept = null, int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<LoanTransactionDto>>>;

public sealed class ListLoanTransactionsQueryValidator : AbstractValidator<ListLoanTransactionsQuery>
{
    public ListLoanTransactionsQueryValidator()
    {
        RuleFor(x => x.Concept).IsInEnum().When(x => x.Concept is not null);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListLoanTransactionsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListLoanTransactionsQuery, Result<PagedResult<LoanTransactionDto>>>
{
    public async Task<Result<PagedResult<LoanTransactionDto>>> Handle(ListLoanTransactionsQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<PagedResult<LoanTransactionDto>>(leido.Error);
        var consulta = db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == leido.Value.Id);
        if (request.From is { } desde) consulta = consulta.Where(t => t.ValueDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(t => t.ValueDate <= hasta);
        if (request.Concept is { } concepto) consulta = consulta.Where(t => t.Concept == concepto);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderBy(t => t.ValueDate).ThenBy(t => t.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);

        var ids = filas.Select(t => t.Id).ToList();
        var operaciones = filas.Where(t => t.OperationId is not null).Select(t => t.OperationId!.Value).Distinct().ToList();
        var corridas = filas.Where(t => t.ProcessRunId is not null).Select(t => t.ProcessRunId!.Value).Distinct().ToList();
        var originales = filas.Where(t => t.ReversesTransactionId is not null).Select(t => t.ReversesTransactionId!.Value).Distinct().ToList();
        var deOperacion = await db.LoanOperations.AsNoTracking().Where(o => operaciones.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.PublicId, ct);
        var deCorrida = await db.LendingProcessRuns.AsNoTracking().Where(r => corridas.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.PublicId, ct);
        var reversadas = await db.LoanTransactions.AsNoTracking().Where(t => originales.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.PublicId, ct);
        var espejos = await db.LoanTransactions.AsNoTracking().Where(t => t.ReversesTransactionId != null && ids.Contains(t.ReversesTransactionId.Value))
            .ToDictionaryAsync(t => t.ReversesTransactionId!.Value, t => t.PublicId, ct);

        var items = filas.Select(t => new LoanTransactionDto(t.PublicId, t.Kind.ToString(), t.Concept.ToString(), t.InstallmentNumber, t.Amount, t.ValueDate,
            t.AccountingDate, t.RegisteredAt,
            t.OperationId is { } o && deOperacion.TryGetValue(o, out var op) ? op : null,
            t.ProcessRunId is { } r && deCorrida.TryGetValue(r, out var run) ? run : null,
            t.ReversesTransactionId is { } x && reversadas.TryGetValue(x, out var original) ? original : null,
            espejos.TryGetValue(t.Id, out var espejo) ? espejo : null,
            t.MessagePublicId)).ToList();
        return Result.Success(new PagedResult<LoanTransactionDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}
