using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

/// <summary>
/// <c>GET /api/lending/loans/{id}/operations?kind=&amp;page=</c> (feature 013, US4, T0504; api.md §8.1, <c>Lending.Loans.Read</c>): las operaciones del
/// crédito en orden de registro, con quién las hizo, el recaudador y los reversos que las enlazan. (nuevo)
/// </summary>
public sealed record ListLoanOperationsQuery(Guid LoanPublicId, LoanOperationKind? Kind = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<LoanOperationDto>>>;

public sealed class ListLoanOperationsQueryValidator : AbstractValidator<ListLoanOperationsQuery>
{
    public ListLoanOperationsQueryValidator() => RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
}

public sealed class ListLoanOperationsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListLoanOperationsQuery, Result<PagedResult<LoanOperationDto>>>
{
    public async Task<Result<PagedResult<LoanOperationDto>>> Handle(ListLoanOperationsQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<PagedResult<LoanOperationDto>>(leido.Error);
        var consulta = db.LoanOperations.AsNoTracking().Where(o => o.LoanId == leido.Value.Id);
        if (request.Kind is { } clase) consulta = consulta.Where(o => o.Kind == clase);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(o => o.Sequence)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var items = await OperacionesDelCredito.DtosAsync(db, filas, ct);
        return Result.Success(new PagedResult<LoanOperationDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>El DTO de las operaciones (lo comparten la lista y el comprobante). (nuevo)</summary>
public static class OperacionesDelCredito
{
    public static async Task<IReadOnlyList<LoanOperationDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<LoanOperation> operaciones, CancellationToken ct)
    {
        var enlazadas = operaciones.SelectMany(o => new[] { o.ReversedByOperationId, o.ReversesOperationId }).Where(x => x is not null).Select(x => x!.Value)
            .Distinct().ToList();
        var publicas = await db.LoanOperations.AsNoTracking().Where(o => enlazadas.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.PublicId, ct);
        var pagadores = operaciones.Where(o => o.PayerPersonId is not null).Select(o => o.PayerPersonId!.Value).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => pagadores.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var usuarios = operaciones.Where(o => o.ActorUserId is not null).Select(o => o.ActorUserId!.Value).Distinct().ToList();
        var nombres = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarios.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        return operaciones.Select(o => new LoanOperationDto(
            o.PublicId, o.Number, o.Kind.ToString(), o.ValueDate, o.AccountingDate, o.RegisteredAt, o.Amount,
            o.CollectorCode is null && o.CollectorClass is null ? null : new OperationCollectorDto(o.CollectorCode, o.CollectorClass),
            o.PaymentMeansClass,
            o.PayerPersonId is { } p && personas.TryGetValue(p, out var pagador) ? pagador : null,
            o.ReceiptNumber,
            o.ReversedByOperationId is { } r && publicas.TryGetValue(r, out var por) ? por : null,
            o.ReversesOperationId is { } s && publicas.TryGetValue(s, out var de) ? de : null,
            o.ActorKind == ActorKind.Person && o.ActorUserId is { } u && nombres.TryGetValue(u, out var nombre) ? nombre ?? "Persona" : "Proceso de integración"))
            .ToList();
    }
}
