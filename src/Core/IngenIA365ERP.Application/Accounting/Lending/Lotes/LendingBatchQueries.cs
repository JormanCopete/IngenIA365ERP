using FluentValidation;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Lotes;

// Feature 013, T0649 (contracts/contabilidad.md §10.2, §10.5 y §21.1): los lotes de hechos de Cartera vistos desde Contabilidad.
// GET /api/accounting/lending/batches y /batches/{id} con Accounting.LendingBatches.View (las rutas son de la API, bloque C). Reutilizan
// IntegrationBatchDto de la 012: un lote es un COR_IntegrationBatches de la plataforma. (nuevos)

/// <summary>Un hecho de un lote de Cartera: su tipo, su origen, su estado de entrega y los créditos que reúne (la lista por crédito de las partes).</summary>
public sealed record HechoDelLoteDeCarteraDto(
    Guid MessagePublicId,
    string Type,
    Guid OriginPublicId,
    string? OriginNumber,
    DateOnly OperationDate,
    string? Branch,
    DeliveryStatus Status,
    IReadOnlyList<string> Loans);

/// <summary>Un comprobante de un lote de Cartera: tipo, número, fecha, si es resumido y cuántos hechos y créditos reúne.</summary>
public sealed record ComprobanteDelLoteDeCarteraDto(
    Guid AccountingDocumentPublicId,
    string VoucherTypeCode,
    long? Number,
    DateOnly OperationDate,
    bool Summarized,
    int FactsCount,
    int LoansCount);

public sealed record RechazoDelLoteDeCarteraDto(Guid MessagePublicId, string Type, string? OriginNumber, string? Code, string? Message, QuienCorrigeDto? WhoFixes);

/// <summary>El detalle de un lote de Cartera: el lote, sus hechos con los créditos de cada parte, sus comprobantes y sus rechazos.</summary>
public sealed record LendingBatchDetailDto(
    IntegrationBatchDto Batch,
    IReadOnlyList<HechoDelLoteDeCarteraDto> Facts,
    IReadOnlyList<ComprobanteDelLoteDeCarteraDto> Vouchers,
    IReadOnlyList<RechazoDelLoteDeCarteraDto> Rejected);

/// <summary>
/// Los lotes de Cartera (§10.2): los de la causación (<c>LendingAccrualRun</c>), del cierre (<c>LendingMonthlyClose</c>), los programados con
/// clave <c>CAR.</c> y los manuales o de reproceso cuyos hechos son de Cartera. Más recientes primero. (nuevo)
/// </summary>
public sealed record ListLendingBatchesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    BatchStatus? Status = null,
    BatchTrigger? Trigger = null,
    PageRequest? Pagina = null,
    int LateToleranceMinutes = ListInventoryBatchesQuery.ToleranciaPorDefecto) : IRequest<Result<PagedResult<IntegrationBatchDto>>>;

public sealed class ListLendingBatchesQueryValidator : AbstractValidator<ListLendingBatchesQuery>
{
    public ListLendingBatchesQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.LateToleranceMinutes).GreaterThanOrEqualTo(0);
    }
}

public sealed class ListLendingBatchesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListLendingBatchesQuery, Result<PagedResult<IntegrationBatchDto>>>
{
    public async Task<Result<PagedResult<IntegrationBatchDto>>> Handle(ListLendingBatchesQuery request, CancellationToken ct)
    {
        var q = DeCartera(db).Where(b => !b.IsDeleted);
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

        var pagina = request.Pagina ?? new PageRequest();
        var total = await q.CountAsync(ct);
        var lotes = await q.OrderByDescending(b => b.Number)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var ahoraLocal = reloj.AhoraLocal.DateTime;
        return Result.Success(new PagedResult<IntegrationBatchDto>(
            lotes.Select(b => ListInventoryBatchesQueryHandler.Dto(b, ahoraLocal, request.LateToleranceMinutes)).ToList(),
            pagina.SafePage, pagina.SafePageSize, total));
    }

    /// <summary>Los lotes a Contabilidad que son de Cartera: por su disparador, por su clave <c>CAR.</c> o por los hechos que tienen.</summary>
    public static IQueryable<IntegrationBatch> DeCartera(IApplicationDbContext db) =>
        db.IntegrationBatches.AsNoTracking().Where(b => b.Destination == IntegrationDestinations.Accounting
            && (b.Trigger == BatchTrigger.LendingAccrualRun || b.Trigger == BatchTrigger.LendingMonthlyClose
                || (b.ScheduleKey != null && b.ScheduleKey.StartsWith(ClavesDeLote.PrefijoDeCartera))
                || db.IntegrationMessageDeliveries.Any(d => d.BatchId == b.Id && d.Message!.OriginModule == ModulosDeOrigen.Cartera)));
}

public sealed record GetLendingBatchQuery(Guid BatchPublicId, int LateToleranceMinutes = ListInventoryBatchesQuery.ToleranciaPorDefecto)
    : IRequest<Result<LendingBatchDetailDto>>;

public sealed class GetLendingBatchQueryValidator : AbstractValidator<GetLendingBatchQuery>
{
    public GetLendingBatchQueryValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.LateToleranceMinutes).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// <c>GET /api/accounting/lending/batches/{id}</c> (§10.4): los hechos del lote con los créditos de cada parte (leídos por
/// <see cref="IMensajesEntrantes"/>, que verifica la huella), los comprobantes que dejó (por sus recibos de <c>ACC_LendingPostings</c>) y los
/// rechazos con quién los corrige. (nuevo)
/// </summary>
public sealed class GetLendingBatchQueryHandler(IApplicationDbContext db, IMensajesEntrantes entrantes, IDateTimeService reloj)
    : IRequestHandler<GetLendingBatchQuery, Result<LendingBatchDetailDto>>
{
    public async Task<Result<LendingBatchDetailDto>> Handle(GetLendingBatchQuery request, CancellationToken ct)
    {
        var lote = await ListLendingBatchesQueryHandler.DeCartera(db).FirstOrDefaultAsync(b => b.PublicId == request.BatchPublicId && !b.IsDeleted, ct);
        if (lote is null) return Result.Failure<LendingBatchDetailDto>(Posting.AccountingErrors.LendingBatchNotFound);

        var entregas = await (
                from d in db.IntegrationMessageDeliveries.AsNoTracking()
                join m in db.IntegrationMessages.AsNoTracking() on d.MessageId equals m.Id
                where d.BatchId == lote.Id && m.OriginModule == ModulosDeOrigen.Cartera
                orderby m.Id
                select new { m.PublicId, d.Status, d.LastErrorCode, d.LastErrorMessage })
            .ToListAsync(ct);
        var leidos = entregas.Count == 0
            ? Result.Success<IReadOnlyList<MensajeEntrante>>([])
            : await entrantes.LeerAsync(entregas.Select(e => e.PublicId).ToList(), IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Failure<LendingBatchDetailDto>(leidos.Error);
        var porId = leidos.Value.ToDictionary(m => m.MessageId);
        var sucursales = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.PublicId, b => b.Name, ct);

        var hechos = entregas.Where(e => porId.ContainsKey(e.PublicId)).Select(e =>
        {
            var m = porId[e.PublicId];
            var creditos = ConstructorDeLineasDeCartera.LineasPorCredito(m.Payload).Select(l => l.Loan.LoanNumber)
                .Concat(ConstructorDeLineasDeCartera.Credito(m.Payload) is { } c ? [c.LoanNumber] : [])
                .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal).ToList();
            return new HechoDelLoteDeCarteraDto(m.MessageId, m.Type, m.Envelope.Origin.PublicId, m.Envelope.Origin.Number, m.Envelope.Origin.OperationDate,
                sucursales.GetValueOrDefault(m.Envelope.BranchPublicId), e.Status, creditos);
        }).ToList();

        var recibos = await db.LendingPostings.AsNoTracking().Where(p => p.BatchPublicId == lote.PublicId && p.AccountingDocumentId != null)
            .Select(p => new { p.MessagePublicId, p.AccountingDocumentId }).ToListAsync(ct);
        var ids = recibos.Select(r => r.AccountingDocumentId!.Value).Distinct().ToList();
        var comprobantes = await db.AccountingDocuments.AsNoTracking().Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, Tipo = d.VoucherType!.Code, d.Number, d.Date, d.SourceType })
            .ToListAsync(ct);
        var vouchers = comprobantes.OrderBy(c => c.Date).ThenBy(c => c.Tipo).ThenBy(c => c.Number).Select(c =>
        {
            var suyos = recibos.Where(r => r.AccountingDocumentId == c.Id).Select(r => r.MessagePublicId).ToHashSet();
            return new ComprobanteDelLoteDeCarteraDto(c.PublicId, c.Tipo, c.Number, c.Date, c.SourceType == OrigenesDeCartera.LoteResumido, suyos.Count,
                hechos.Where(h => suyos.Contains(h.MessagePublicId)).SelectMany(h => h.Loans).Distinct(StringComparer.Ordinal).Count());
        }).ToList();

        var rechazos = entregas.Where(e => e.Status == DeliveryStatus.Rejected && porId.ContainsKey(e.PublicId))
            .Select(e => new RechazoDelLoteDeCarteraDto(e.PublicId, porId[e.PublicId].Type, porId[e.PublicId].Envelope.Origin.Number, e.LastErrorCode,
                e.LastErrorMessage, e.LastErrorCode is null ? null : QuienCorrigeEnCartera.De(e.LastErrorCode)))
            .ToList();

        var dto = ListInventoryBatchesQueryHandler.Dto(lote, reloj.AhoraLocal.DateTime, request.LateToleranceMinutes);
        return Result.Success(new LendingBatchDetailDto(dto, hechos, vouchers, rechazos));
    }
}

/// <summary>
/// Los débitos y créditos de los comprobantes de Cartera que dejó un lote (§6, <c>GetLendingBatchTotalsQuery</c>): la suma de
/// <c>ACC_Documents.TotalDebit/TotalCredit</c> de los comprobantes distintos que nombran sus recibos (<c>ACC_LendingPostings.BatchPublicId</c>).
/// <c>DestinoContabilidad.TotalesDelLoteAsync</c> la suma con la de Inventario. Sin ruta propia: la lista de lotes la muestra. (nuevo)
/// </summary>
public sealed record GetLendingBatchTotalsQuery(Guid BatchPublicId) : IRequest<Result<TotalesDeLoteEnDestino>>;

public sealed class GetLendingBatchTotalsQueryValidator : AbstractValidator<GetLendingBatchTotalsQuery>
{
    public GetLendingBatchTotalsQueryValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty);
    }
}

public sealed class GetLendingBatchTotalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLendingBatchTotalsQuery, Result<TotalesDeLoteEnDestino>>
{
    public async Task<Result<TotalesDeLoteEnDestino>> Handle(GetLendingBatchTotalsQuery request, CancellationToken ct)
    {
        var comprobantes = await db.LendingPostings.AsNoTracking()
            .Where(p => p.BatchPublicId == request.BatchPublicId && p.AccountingDocumentId != null)
            .Select(p => p.AccountingDocumentId!.Value)
            .Distinct()
            .ToListAsync(ct);
        if (comprobantes.Count == 0) return Result.Success(TotalesDeLoteEnDestino.Cero);

        var totales = await db.AccountingDocuments.AsNoTracking()
            .Where(d => comprobantes.Contains(d.Id))
            .Select(d => new { d.TotalDebit, d.TotalCredit })
            .ToListAsync(ct);
        return Result.Success(new TotalesDeLoteEnDestino(totales.Sum(t => t.TotalDebit), totales.Sum(t => t.TotalCredit)));
    }
}
