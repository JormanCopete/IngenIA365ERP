using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Files;

/// <summary>
/// Los archivos de desembolsos (feature 013, US3, T0454; api.md §9.3, <c>GET /api/lending/disbursement-files?status=&amp;from=&amp;to=&amp;page=</c>,
/// <c>Lending.DisbursementFiles.Generate</c>): los que llevan al menos una orden de un crédito del alcance de oficina y línea
/// (<see cref="AlcanceDeCartera"/>), con su formato, la cuenta de origen enmascarada, el estado derivado (<c>Generated</c> o <c>Voided</c>) y si
/// alguna de sus órdenes ya tiene resultado de Tesorería. Por fecha de pago y estado; los más recientes primero. (nuevo)
/// </summary>
public sealed record ListDisbursementFilesQuery(string? Status = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<DisbursementFileDto>>>;

public sealed class ListDisbursementFilesQueryValidator : AbstractValidator<ListDisbursementFilesQuery>
{
    public ListDisbursementFilesQueryValidator() =>
        RuleFor(x => x.Status).Must(s => s is null or "Generated" or "Voided").WithMessage("El estado es Generated o Voided.");
}

public sealed class ListDisbursementFilesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListDisbursementFilesQuery, Result<PagedResult<DisbursementFileDto>>>
{
    public async Task<Result<PagedResult<DisbursementFileDto>>> Handle(ListDisbursementFilesQuery request, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var ordenes = db.DisbursementOrders.AsNoTracking().Where(o => creditos.Contains(o.LoanId)).Select(o => o.Id);
        var visibles = db.DisbursementFileLines.AsNoTracking().Where(l => ordenes.Contains(l.OrderId)).Select(l => l.FileId);
        var consulta = db.DisbursementFiles.AsNoTracking().Where(f => !f.IsDeleted && visibles.Contains(f.Id));
        if (request.Status == "Generated") consulta = consulta.Where(f => f.VoidedAt == null);
        if (request.Status == "Voided") consulta = consulta.Where(f => f.VoidedAt != null);
        if (request.From is { } desde) consulta = consulta.Where(f => f.PaymentDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(f => f.PaymentDate <= hasta);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(f => f.GeneratedAt).ThenByDescending(f => f.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<DisbursementFileDto>(await ArchivosDeDesembolsos.DtosAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize,
            total));
    }
}
