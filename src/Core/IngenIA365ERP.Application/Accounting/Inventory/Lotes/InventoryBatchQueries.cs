using FluentValidation;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Lotes;

// Feature 012, T518 (api.md §26.4; contracts/contabilidad.md §5.4): los lotes de contabilización vistos desde Contabilidad.
// GET /api/accounting/inventory/batches y /batches/{id}, con Accounting.InventoryBatches.View (la ruta es de T528). (nuevos)

/// <summary>Quién pidió el lote: una persona o el proceso de integración.</summary>
public sealed record SolicitanteDelLoteDto(ActorKind Kind, string? Name);

public sealed record PeriodoDelLoteDto(int Year, int Month);

public sealed record TotalesDelLoteDto(int Messages, int Documents, int Vouchers, int Rejected, decimal Debit, decimal Credit);

/// <summary><c>IntegrationBatchDto</c> de api.md §26.4. <see cref="Late"/>: programado, sin correr y vencido más la tolerancia.</summary>
public sealed record IntegrationBatchDto(
    Guid BatchPublicId,
    long Number,
    string Destination,
    BatchTrigger Trigger,
    string? ScheduleKey,
    DateTime? ScheduledFor,
    Guid? CashSessionPublicId,
    PeriodoDelLoteDto? Period,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    PostingGranularity? Granularity,
    BatchStatus Status,
    SolicitanteDelLoteDto RequestedBy,
    DateTime RequestedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    TotalesDelLoteDto Totals,
    bool Late);

public sealed record ComprobanteDelLoteDto(
    Guid AccountingDocumentPublicId,
    string VoucherTypeCode,
    long? Number,
    DateOnly OperationDate,
    string? Branch,
    string? CostCenter,
    PostingGranularity Granularity,
    int DocumentsCount);

public sealed record RechazoDelLoteDto(Guid MessagePublicId, string? DocumentNumber, string? Code, string? Message, QuienCorrigeDto? WhoFixes);

/// <summary>El detalle de un lote: el lote, sus documentos, sus comprobantes y sus rechazos con quién los corrige.</summary>
public sealed record IntegrationBatchDetailDto(
    IntegrationBatchDto Batch,
    IReadOnlyList<DocumentoDeLoteDto> Documents,
    IReadOnlyList<ComprobanteDelLoteDto> Vouchers,
    IReadOnlyList<RechazoDelLoteDto> Rejected);

public sealed record ListInventoryBatchesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    BatchStatus? Status = null,
    BatchTrigger? Trigger = null,
    string? Destination = null,
    PageRequest? Pagina = null,
    int LateToleranceMinutes = ListInventoryBatchesQuery.ToleranciaPorDefecto) : IRequest<Result<PagedResult<IntegrationBatchDto>>>
{
    /// <summary>La tolerancia técnica de un lote programado cuando la ruta no pasa la de <c>Integration:Dispatcher:LateToleranceMinutes</c>.</summary>
    public const int ToleranciaPorDefecto = 30;
}

public sealed class ListInventoryBatchesQueryValidator : AbstractValidator<ListInventoryBatchesQuery>
{
    public ListInventoryBatchesQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.Destination).MaximumLength(20);
        RuleFor(x => x.LateToleranceMinutes).GreaterThanOrEqualTo(0);
    }
}

public sealed class ListInventoryBatchesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListInventoryBatchesQuery, Result<PagedResult<IntegrationBatchDto>>>
{
    public async Task<Result<PagedResult<IntegrationBatchDto>>> Handle(ListInventoryBatchesQuery request, CancellationToken ct)
    {
        var q = db.IntegrationBatches.AsNoTracking().Where(b => !b.IsDeleted);
        if (request.From is { } desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue);
            q = q.Where(b => b.RequestedAt >= inicio);
        }
        if (request.To is { } hasta)
        {
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(b => b.RequestedAt < fin);
        }
        if (request.Status is { } estado) q = q.Where(b => b.Status == estado);
        if (request.Trigger is { } disparador) q = q.Where(b => b.Trigger == disparador);
        if (!string.IsNullOrWhiteSpace(request.Destination)) q = q.Where(b => b.Destination == request.Destination);

        var pagina = request.Pagina ?? new PageRequest();
        var total = await q.CountAsync(ct);
        var lotes = await q.OrderByDescending(b => b.Number)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var ahoraLocal = reloj.AhoraLocal.DateTime;
        var items = lotes.Select(b => Dto(b, ahoraLocal, request.LateToleranceMinutes)).ToList();
        return Result.Success(new PagedResult<IntegrationBatchDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }

    public static IntegrationBatchDto Dto(IntegrationBatch b, DateTime ahoraLocal, int tolerancia) => new(
        b.PublicId, b.Number, b.Destination, b.Trigger, b.ScheduleKey, b.ScheduledFor, b.CashSessionPublicId,
        b.PeriodYear is { } anio && b.PeriodMonth is { } mes ? new PeriodoDelLoteDto(anio, mes) : null,
        b.DateFrom, b.DateTo, b.Granularity, b.Status, new SolicitanteDelLoteDto(b.RequestedByKind, b.RequestedByName), b.RequestedAt,
        b.StartedAt, b.FinishedAt,
        new TotalesDelLoteDto(b.MessageCount, b.DocumentCount, b.VoucherCount, b.RejectedCount, b.TotalDebit, b.TotalCredit),
        Atrasado(b, ahoraLocal, tolerancia));

    /// <summary>Un lote programado que no corrió a su hora más la tolerancia (el mismo criterio de <c>Integracion.LoteNoCorrio</c>).</summary>
    public static bool Atrasado(IntegrationBatch b, DateTime ahoraLocal, int tolerancia) =>
        b.Trigger == BatchTrigger.Scheduled && b.Status == BatchStatus.Requested && b.ScheduledFor is { } franja
        && franja.AddMinutes(tolerancia) < ahoraLocal;
}

public sealed record GetInventoryBatchQuery(Guid BatchPublicId, int LateToleranceMinutes = ListInventoryBatchesQuery.ToleranciaPorDefecto)
    : IRequest<Result<IntegrationBatchDetailDto>>;

public sealed class GetInventoryBatchQueryValidator : AbstractValidator<GetInventoryBatchQuery>
{
    public GetInventoryBatchQueryValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.LateToleranceMinutes).GreaterThanOrEqualTo(0);
    }
}

public sealed class GetInventoryBatchQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetInventoryBatchQuery, Result<IntegrationBatchDetailDto>>
{
    public async Task<Result<IntegrationBatchDetailDto>> Handle(GetInventoryBatchQuery request, CancellationToken ct)
    {
        var lote = await db.IntegrationBatches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.BatchPublicId && !b.IsDeleted, ct);
        if (lote is null) return Result.Failure<IntegrationBatchDetailDto>(Common.Integration.ErroresDeIntegracion.LoteNoEncontrado(request.BatchPublicId));

        // Los documentos: los de las entregas del lote, en orden de emisión.
        var mensajes = await (
                from d in db.IntegrationMessageDeliveries.AsNoTracking()
                join m in db.IntegrationMessages.AsNoTracking() on d.MessageId equals m.Id
                where d.BatchId == lote.Id
                orderby m.Id
                select new
                {
                    m.PublicId, m.OriginPublicId, m.OriginDocumentClass, m.OriginDocumentTypeCode, m.OriginNumber, m.OperationDate, m.BranchPublicId,
                    d.Status, d.LastErrorCode, d.LastErrorMessage,
                })
            .ToListAsync(ct);
        var sucursales = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.PublicId, b => b.Name, ct);

        var recibos = await db.InventoryPostings.AsNoTracking().Where(p => p.BatchPublicId == lote.PublicId && p.AccountingDocumentId != null)
            .Select(p => new { p.SourcePublicId, p.AccountingDocumentId }).ToListAsync(ct);
        var idsDeComprobante = recibos.Select(r => r.AccountingDocumentId!.Value).Distinct().ToList();
        var comprobantes = await db.AccountingDocuments.AsNoTracking().Where(d => idsDeComprobante.Contains(d.Id))
            .Select(d => new
            {
                d.Id, d.PublicId, Tipo = d.VoucherType!.Code, d.Number, d.Date, d.SourceType, d.TotalDebit,
                Sucursal = d.Lines.OrderBy(l => l.LineNumber).Select(l => l.Branch!.Name).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var documentos = mensajes.GroupBy(m => m.OriginPublicId).Select(g =>
        {
            var m = g.First();
            var suyos = recibos.Where(r => r.SourcePublicId == g.Key).Select(r => r.AccountingDocumentId).Distinct().ToList();
            var total = comprobantes.Where(c => suyos.Contains(c.Id) && c.SourceType == OrigenesDeInventario.Documento).Sum(c => c.TotalDebit);
            return new DocumentoDeLoteDto(g.Key, m.OriginDocumentClass, m.OriginDocumentTypeCode, m.OriginNumber, m.OperationDate,
                sucursales.GetValueOrDefault(m.BranchPublicId), total);
        }).ToList();

        var vouchers = comprobantes.OrderBy(c => c.Date).ThenBy(c => c.Tipo).ThenBy(c => c.Number)
            .Select(c => new ComprobanteDelLoteDto(c.PublicId, c.Tipo, c.Number, c.Date, c.Sucursal, null,
                c.SourceType == OrigenesDeInventario.LoteResumido ? PostingGranularity.Summarized : PostingGranularity.PerDocument,
                recibos.Where(r => r.AccountingDocumentId == c.Id).Select(r => r.SourcePublicId).Distinct().Count()))
            .ToList();

        var rechazos = mensajes.Where(m => m.Status == DeliveryStatus.Rejected)
            .Select(m => new RechazoDelLoteDto(m.PublicId, m.OriginNumber, m.LastErrorCode, m.LastErrorMessage,
                m.LastErrorCode is null ? null : QuienCorrige.De(m.LastErrorCode, Enum.TryParse<DocumentClass>(m.OriginDocumentClass, out var clase) ? clase : null)))
            .ToList();

        var dto = ListInventoryBatchesQueryHandler.Dto(lote, reloj.AhoraLocal.DateTime, request.LateToleranceMinutes);
        return Result.Success(new IntegrationBatchDetailDto(dto, documentos, vouchers, rechazos));
    }
}
