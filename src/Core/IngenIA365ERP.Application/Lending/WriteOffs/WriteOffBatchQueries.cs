using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WriteOffs;

/// <summary>
/// Los lotes de castigo (feature 013, K3, US14, T0906; api.md §11.3, <c>GET /api/lending/write-off-batches?status=&amp;page=</c>,
/// <c>Lending.Loans.Read</c>): los que tienen todos sus créditos en las oficinas y líneas del alcance (un lote con un crédito fuera no aparece ni cuenta),
/// los más recientes primero. (nuevo)
/// </summary>
public sealed record ListWriteOffBatchesQuery(WriteOffBatchStatus? Status = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<WriteOffBatchDto>>>;

public sealed class ListWriteOffBatchesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, CastigosDeCartera castigos)
    : IRequestHandler<ListWriteOffBatchesQuery, Result<PagedResult<WriteOffBatchDto>>>
{
    public async Task<Result<PagedResult<WriteOffBatchDto>>> Handle(ListWriteOffBatchesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var dentro = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.WriteOffBatches.AsNoTracking()
            .Where(b => !b.IsDeleted && !db.WriteOffBatchItems.Any(i => i.WriteOffBatchId == b.Id && !i.IsDeleted && !dentro.Contains(i.LoanId)));
        if (request.Status is { } estado) consulta = consulta.Where(b => b.Status == estado);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var lotes = await consulta.OrderByDescending(b => b.PreparedAt).ThenByDescending(b => b.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var ids = lotes.Select(b => b.Id).ToList();
        var items = (await db.WriteOffBatchItems.AsNoTracking().Where(i => ids.Contains(i.WriteOffBatchId) && !i.IsDeleted).ToListAsync(ct))
            .GroupBy(i => i.WriteOffBatchId).ToDictionary(g => g.Key, g => g.ToList());
        var dtos = new List<WriteOffBatchDto>();
        foreach (var lote in lotes) dtos.Add(await castigos.DtoAsync(lote, items.GetValueOrDefault(lote.Id) ?? [], ct));
        return Result.Success(new PagedResult<WriteOffBatchDto>(dtos, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>
/// Un lote de castigo (feature 013, K3, US14, T0906; api.md §11.3, <c>GET /api/lending/write-off-batches/{id}</c>, <c>Lending.Loans.Read</c>): con sus
/// créditos incluidos (saldo, deterioro, mora y cada requisito con su evidencia) y los rechazados con lo que les falta. Fuera del alcance o inexistente,
/// <c>Lending.WriteOffBatch.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetWriteOffBatchQuery(Guid WriteOffBatchPublicId) : IRequest<Result<WriteOffBatchDto>>;

public sealed class GetWriteOffBatchQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, CastigosDeCartera castigos)
    : IRequestHandler<GetWriteOffBatchQuery, Result<WriteOffBatchDto>>
{
    public async Task<Result<WriteOffBatchDto>> Handle(GetWriteOffBatchQuery request, CancellationToken ct)
    {
        var leido = await CastigosDeCartera.LeerAsync(db, alcance, auditoria, request.WriteOffBatchPublicId, seguir: false, ct);
        return leido.IsFailure
            ? Result.Failure<WriteOffBatchDto>(leido.Error)
            : Result.Success(await castigos.DtoAsync(leido.Value.Lote, leido.Value.Items, ct));
    }
}
