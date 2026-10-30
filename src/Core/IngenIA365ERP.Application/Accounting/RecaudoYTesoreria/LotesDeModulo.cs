using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

/// <summary>Un lote de integración por el que pasaron hechos del módulo, con lo que dejó en Contabilidad. (nuevo)</summary>
public sealed record LoteDeModuloDto(Guid BatchPublicId, long Number, string Trigger, string Status, DateTime CreatedAt, DateOnly? DateFrom, DateOnly? DateTo,
    int Facts, int Vouchers, int WithoutVoucher, decimal TotalDebit, decimal TotalCredit);

/// <summary>
/// Los lotes de un módulo (feature 015, US8, T0265, T0266; contabilidad.md §7): los <c>COR_IntegrationBatches</c> que nombran los recibos de
/// <c>ACC_ReceiptsPostings</c> o <c>ACC_TreasuryPostings</c>, del más reciente al más antiguo, con los hechos, los comprobantes y sus totales. Sólo lectura. (nuevo)
/// </summary>
public sealed record ListModuleBatchesQuery(string Module, DateOnly? From = null, DateOnly? To = null) : IRequest<Result<IReadOnlyList<LoteDeModuloDto>>>;

public sealed class ListModuleBatchesQueryValidator : AbstractValidator<ListModuleBatchesQuery>
{
    public ListModuleBatchesQueryValidator() => RuleFor(x => x.Module).Must(m => MatricesDeRecaudoYTesoreria.De(m) is not null).WithMessage("El módulo es REC o TES.");
}

public sealed class ListModuleBatchesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListModuleBatchesQuery, Result<IReadOnlyList<LoteDeModuloDto>>>
{
    public async Task<Result<IReadOnlyList<LoteDeModuloDto>>> Handle(ListModuleBatchesQuery request, CancellationToken ct)
    {
        var modulo = MatricesDeRecaudoYTesoreria.De(request.Module)!;
        var recibos = modulo.Codigo == ModuloContable.Recaudo
            ? await db.ReceiptsPostings.AsNoTracking().Where(p => p.BatchPublicId != null).Select(p => new { Lote = p.BatchPublicId!.Value, p.AccountingDocumentId }).ToListAsync(ct)
            : await db.TreasuryPostings.AsNoTracking().Where(p => p.BatchPublicId != null).Select(p => new { Lote = p.BatchPublicId!.Value, p.AccountingDocumentId }).ToListAsync(ct);
        var porLote = recibos.GroupBy(r => r.Lote).ToDictionary(g => g.Key, g => g.ToList());
        var ids = porLote.Keys.ToList();
        var consulta = db.IntegrationBatches.AsNoTracking().Where(b => ids.Contains(b.PublicId));
        if (request.From is { } desde) consulta = consulta.Where(b => b.DateTo == null || b.DateTo >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(b => b.DateFrom == null || b.DateFrom <= hasta);
        var lotes = await consulta.OrderByDescending(b => b.Number).Take(200).ToListAsync(ct);
        var documentos = recibos.Where(r => r.AccountingDocumentId != null).Select(r => r.AccountingDocumentId!.Value).Distinct().ToList();
        var totales = await db.AccountingDocuments.AsNoTracking().Where(d => documentos.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => (d.TotalDebit, d.TotalCredit), ct);
        return Result.Success<IReadOnlyList<LoteDeModuloDto>>(lotes.Select(b =>
        {
            var deEste = porLote[b.PublicId];
            var comprobantes = deEste.Where(r => r.AccountingDocumentId != null).Select(r => r.AccountingDocumentId!.Value).Distinct().ToList();
            return new LoteDeModuloDto(b.PublicId, b.Number, b.Trigger.ToString(), b.Status.ToString(), b.CreatedAt, b.DateFrom, b.DateTo, deEste.Count, comprobantes.Count,
                deEste.Count(r => r.AccountingDocumentId == null), comprobantes.Sum(c => totales.GetValueOrDefault(c).TotalDebit),
                comprobantes.Sum(c => totales.GetValueOrDefault(c).TotalCredit));
        }).ToList());
    }
}
