using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Queries;

// ------------------------------------------------------------------------------------------------------------------ conciliación --

/// <summary>
/// <c>GET /api/lending/accounting/reconciliation?date=&amp;branch=</c> (feature 013, US7, T0658; api.md §16.3, <c>Lending.Reconciliation.View</c>;
/// contabilidad.md §14.2): la conciliación de Cartera con Contabilidad a una fecha contable por <see cref="ConciliacionContableDeCartera"/>, con
/// el alcance de oficinas de quien pregunta (<see cref="IAlcanceDeCartera"/>) y, si viene, una sola sucursal. Sin contabilidad iniciada,
/// <c>Lending.Accounting.NotStarted</c>. (nuevo)
/// </summary>
public sealed record GetLendingAccountingReconciliationQuery(DateOnly Date, Guid? BranchPublicId = null) : IRequest<Result<ConciliacionContableDto>>;

public sealed class GetLendingAccountingReconciliationQueryValidator : AbstractValidator<GetLendingAccountingReconciliationQuery>
{
    public GetLendingAccountingReconciliationQueryValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de corte.");
    }
}

public sealed class GetLendingAccountingReconciliationQueryHandler(
    IApplicationDbContext db, ConciliacionContableDeCartera conciliacion, IAlcanceDeCartera alcanceDeCartera)
    : IRequestHandler<GetLendingAccountingReconciliationQuery, Result<ConciliacionContableDto>>
{
    public async Task<Result<ConciliacionContableDto>> Handle(GetLendingAccountingReconciliationQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeCartera.ObtenerAsync(ct);
        if (request.BranchPublicId is { } sucursal)
        {
            var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == sucursal && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            if (oficina is not { } id || !alcance.IncluyeOficina(id)) return Result.Failure<ConciliacionContableDto>(Error.NotFound);
            alcance = alcance with { TodasLasOficinas = false, Oficinas = new HashSet<int> { id } };
        }
        return await conciliacion.ConciliarAsync(request.Date, alcance, ct);
    }
}

// ------------------------------------------------------------------------------------------------------------- hechos contables --

/// <summary>Un hecho <c>CAR → Accounting</c> con su estado y su comprobante, leídos de la entrega (sin leer <c>ACC_</c>). (nuevo)</summary>
public sealed record LendingAccountingFactDto(
    Guid MessagePublicId,
    string Type,
    DateOnly AccountingDate,
    Guid Branch,
    string? Mode,
    string? Status,
    string? Number,
    string? PrevalidationOutcome,
    decimal Totals,
    LendingAccountingFactPartRefDto? Part,
    LendingAccountingFactVoucherDto? Voucher);

public sealed record LendingAccountingFactPartRefDto(Guid PartPublicId, int? Tranche, int Loans);

public sealed record LendingAccountingFactVoucherDto(string? Reference, string? VoucherTypeCode, string? VoucherNumber, DateTime? ProcessedAt);

/// <summary>
/// <c>GET /api/lending/accounting/facts?type=&amp;status=&amp;from=&amp;to=&amp;branch=&amp;loanPublicId=&amp;page=</c> (feature 013, US7, T0658; api.md
/// §16.3, <c>Lending.Messages.Read</c> —nunca un <c>*.View</c>, §2.14—): los hechos de Cartera a Contabilidad por fecha contable, con su modo, su
/// estado y su comprobante desde la entrega, el total del contenido y, si su origen es una parte, la parte. Filtra por las oficinas del alcance de
/// quien pregunta (la sucursal del sobre). Por crédito, los hechos cuyo libro lo menciona o cuya operación es suya y, desde US9 (bloque 12C), los del
/// cierre mensual cuya lista por crédito lo trae (<see cref="HechosDelCierre"/>). (nuevo)
/// </summary>
public sealed record ListLendingAccountingFactsQuery(
    string? Type = null,
    DeliveryStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? BranchPublicId = null,
    Guid? LoanPublicId = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<LendingAccountingFactDto>>>;

public sealed class ListLendingAccountingFactsQueryValidator : AbstractValidator<ListLendingAccountingFactsQuery>
{
    public ListLendingAccountingFactsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
    }
}

public sealed class ListLendingAccountingFactsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeCartera)
    : IRequestHandler<ListLendingAccountingFactsQuery, Result<PagedResult<LendingAccountingFactDto>>>
{
    public async Task<Result<PagedResult<LendingAccountingFactDto>>> Handle(ListLendingAccountingFactsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeCartera.ObtenerAsync(ct);
        var sucursales = alcance.TodasLasOficinas ? null
            : await db.Branches.AsNoTracking().Where(b => alcance.Oficinas.Contains(b.Id)).Select(b => b.PublicId).ToListAsync(ct);

        var consulta = from m in db.IntegrationMessages.AsNoTracking()
                       where m.OriginModule == ModulosDeOrigen.Cartera
                       let entrega = db.IntegrationMessageDeliveries.Where(d => d.MessageId == m.Id && d.Destination == IntegrationDestinations.Accounting).FirstOrDefault()
                       where entrega != null
                       select new { m, entrega };
        if (sucursales is not null) consulta = consulta.Where(x => sucursales.Contains(x.m.BranchPublicId));
        if (request.Type is { Length: > 0 } tipo) consulta = consulta.Where(x => x.m.Type == tipo);
        if (request.Status is { } estado) consulta = consulta.Where(x => x.entrega!.Status == estado);
        if (request.From is { } desde) consulta = consulta.Where(x => x.m.OperationDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(x => x.m.OperationDate <= hasta);
        if (request.BranchPublicId is { } sucursal) consulta = consulta.Where(x => x.m.BranchPublicId == sucursal);
        if (request.LoanPublicId is { } credito)
        {
            var id = await db.Loans.AsNoTracking().Where(l => l.PublicId == credito).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
            if (id is null) return Result.Success(new PagedResult<LendingAccountingFactDto>([], 1, request.PageSize, 0));
            var mensajes = db.LoanTransactions.Where(t => t.LoanId == id && t.MessagePublicId != null).Select(t => t.MessagePublicId!.Value);
            var operaciones = db.LoanOperations.Where(o => o.LoanId == id).Select(o => o.PublicId);
            // US9 (bloque 12C): los hechos del cierre (reclasificación, deterioro y sus reversos) no tienen filas del libro: el crédito va en la
            // lista por crédito de su contenido.
            var delCierre = HechosDelCierre.Tipos;
            var texto = credito.ToString("D");
            consulta = consulta.Where(x => mensajes.Contains(x.m.PublicId) || operaciones.Contains(x.m.OriginPublicId)
                                           || (delCierre.Contains(x.m.Type) && x.m.PayloadJson != null && x.m.PayloadJson.Contains(texto)));
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.m.OperationDate).ThenByDescending(x => x.m.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Select(x => new
            {
                x.m.PublicId, x.m.Type, x.m.OperationDate, x.m.BranchPublicId, x.m.OriginNumber, x.m.OriginPublicId, x.m.PayloadJson, x.m.PrevalidationOutcome,
                Mode = (DeliveryMode?)x.entrega!.Mode, Status = (DeliveryStatus?)x.entrega.Status, x.entrega.ResultReference, x.entrega.ResultVoucherTypeCode,
                x.entrega.ResultVoucherNumber, x.entrega.ProcessedAt,
            })
            .ToListAsync(ct);
        var origenes = filas.Select(f => f.OriginPublicId).Distinct().ToList();
        var partes = await db.AccountingFactParts.AsNoTracking().Where(p => origenes.Contains(p.PublicId))
            .ToDictionaryAsync(p => p.PublicId, p => new LendingAccountingFactPartRefDto(p.PublicId, p.BatchNumber, p.LoanCount), ct);

        var items = filas.Select(f => new LendingAccountingFactDto(f.PublicId, f.Type, f.OperationDate, f.BranchPublicId, f.Mode?.ToString(),
            f.Status?.ToString(), f.OriginNumber, f.PrevalidationOutcome?.ToString(), TotalDelContenido(f.PayloadJson), partes.GetValueOrDefault(f.OriginPublicId),
            f.Status == DeliveryStatus.Processed ? new LendingAccountingFactVoucherDto(f.ResultReference, f.ResultVoucherTypeCode, f.ResultVoucherNumber, f.ProcessedAt) : null))
            .ToList();
        return Result.Success(new PagedResult<LendingAccountingFactDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }

    /// <summary>La suma de los importes del contenido (<c>amounts[].amount</c>, también en las líneas por crédito y en los bloques anidados).</summary>
    public static decimal TotalDelContenido(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return Sumar(doc.RootElement);
        }
        catch (JsonException)
        {
            return 0m;
        }
    }

    private static decimal Sumar(JsonElement e)
    {
        var total = 0m;
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (p.NameEquals("amounts") && p.Value.ValueKind == JsonValueKind.Array)
                    total += p.Value.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.Object && a.TryGetProperty("amount", out var v) && v.ValueKind == JsonValueKind.Number)
                        .Sum(a => a.GetProperty("amount").GetDecimal());
                else if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) total += Sumar(p.Value);
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
            total += e.EnumerateArray().Sum(Sumar);
        return total;
    }
}

// ---------------------------------------------------------------------------------------------------------------- una parte --

/// <summary>Una parte de hechos (<c>LND_AccountingFactParts</c>) con sus totales por combinación y la lista por crédito. (nuevo)</summary>
public sealed record AccountingFactPartDto(
    Guid PublicId,
    string FactType,
    string SourceKind,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    int? BatchNumber,
    int LoanCount,
    decimal TotalAmount,
    JsonElement TotalsByDimension,
    string? PrevalidationOutcome,
    IReadOnlyList<Guid> Messages,
    PagedResult<AccountingFactPartLoanDto> Loans);

/// <summary>Un crédito de la parte: número, tercero y valores por concepto (del libro). (nuevo)</summary>
public sealed record AccountingFactPartLoanDto(Guid LoanPublicId, string LoanNumber, string Debtor, IReadOnlyDictionary<string, decimal> ByConcept, decimal Total);

/// <summary>
/// <c>GET /api/lending/accounting/fact-parts/{id}?page=</c> (feature 013, US7, T0658; api.md §16.3, <c>Lending.Messages.Read</c>): la parte con sus
/// totales por combinación de dimensiones y la lista por crédito —número, tercero y valores por concepto, de las filas del libro que llevaron
/// sus mensajes—. Fuera del alcance de oficinas de quien pregunta, el mismo 404 que inexistente
/// (<c>Lending.AccountingFactPart.NotFound</c>). (nuevo)
/// </summary>
public sealed record GetAccountingFactPartQuery(Guid PartPublicId, int Page = 1, int PageSize = 100) : IRequest<Result<AccountingFactPartDto>>;

public sealed class GetAccountingFactPartQueryValidator : AbstractValidator<GetAccountingFactPartQuery>
{
    public GetAccountingFactPartQueryValidator()
    {
        RuleFor(x => x.PartPublicId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
    }
}

public sealed class GetAccountingFactPartQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeCartera)
    : IRequestHandler<GetAccountingFactPartQuery, Result<AccountingFactPartDto>>
{
    public const string CodigoInexistente = "Lending.AccountingFactPart.NotFound";

    public static Error Inexistente() => new(CodigoInexistente, "La parte de hechos contables no existe.");

    public async Task<Result<AccountingFactPartDto>> Handle(GetAccountingFactPartQuery request, CancellationToken ct)
    {
        var p = await db.AccountingFactParts.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.PartPublicId && !x.IsDeleted, ct);
        var alcance = await alcanceDeCartera.ObtenerAsync(ct);
        if (p is null || !alcance.IncluyeOficina(p.BranchId)) return Result.Failure<AccountingFactPartDto>(Inexistente());

        var mensajes = await db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == p.PublicId).OrderBy(m => m.Id)
            .Select(m => m.PublicId).ToListAsync(ct);
        if (p.MessagePublicId is { } propio && !mensajes.Contains(propio)) mensajes.Insert(0, propio);

        var filas = await (from t in db.LoanTransactions.AsNoTracking()
                           join l in db.Loans.AsNoTracking() on t.LoanId equals l.Id
                           join per in db.People.AsNoTracking().IgnoreQueryFilters() on l.DebtorPersonId equals per.Id
                           where t.MessagePublicId != null && mensajes.Contains(t.MessagePublicId.Value)
                           select new { l.PublicId, l.Number, Deudor = ReferenciasDeGobierno.Nombre(per), t.Concept, t.Amount })
            .ToListAsync(ct);
        var porCredito = filas.GroupBy(f => (f.PublicId, f.Number, f.Deudor))
            .Select(g => new AccountingFactPartLoanDto(g.Key.PublicId, g.Key.Number, g.Key.Deudor,
                g.GroupBy(f => f.Concept).ToDictionary(c => c.Key.ToString(), c => c.Sum(f => f.Amount)), g.Sum(f => f.Amount)))
            .OrderBy(c => c.LoanNumber, StringComparer.Ordinal)
            .ToList();
        // US9 (bloque 12C): las partes del cierre y de su reapertura no mueven el libro; su lista por crédito es la de su contenido. El deterioro
        // y los movimientos con motivo se muestran aparte del saldo («Principal · Individual», «CurrentInterest · Increase»).
        if (porCredito.Count == 0 && p.SourceKind is AccountingFactPartSource.MonthlyClose or AccountingFactPartSource.CloseReopening)
            porCredito = await HechosDelCierre.PorCreditoAsync(db, mensajes, ct);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var creditos = new PagedResult<AccountingFactPartLoanDto>(
            porCredito.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList(), pagina.SafePage, pagina.SafePageSize, porCredito.Count);

        using var totales = JsonDocument.Parse(string.IsNullOrWhiteSpace(p.TotalsByDimensionJson) ? "[]" : p.TotalsByDimensionJson);
        return Result.Success(new AccountingFactPartDto(p.PublicId, p.FactType, p.SourceKind.ToString(), p.ValueDate, p.AccountingDate, p.BatchNumber,
            p.LoanCount, p.TotalAmount, totales.RootElement.Clone(), p.PrevalidationOutcome, mensajes, creditos));
    }
}

/// <summary>
/// Los hechos del cierre mensual de Cartera vistos por crédito (feature 013, US9, bloque 12C): la reclasificación, el deterioro y sus reversos no
/// tienen filas del libro, así que la lista por crédito de una parte y el filtro por crédito de los hechos salen de las líneas de su contenido
/// (<c>lines[]</c> con el crédito, el deudor y los importes por concepto). (nuevo)
/// </summary>
public static class HechosDelCierre
{
    /// <summary>Los tipos de hecho que emite el cierre mensual y su reapertura con lista por crédito.</summary>
    public static readonly string[] Tipos =
    [
        CarteraReclasificadaPorCalificacionV1.Type,
        DeterioroCalculadoV1.Type,
        ReversoDeCierreV1.Type,
    ];

    /// <summary>Las líneas por crédito del contenido de un hecho del cierre.</summary>
    public static IReadOnlyList<LoanFactLineV1> Lineas(string tipo, string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return [];
        if (tipo == ReversoDeCierreV1.Type)
            return JsonSerializer.Deserialize<ReversoDeCierreV1>(contenido, OpcionesDeMensajes.Opciones)?.Lines ?? [];
        return Risk.ReaperturaDelCierre.LineasDe(tipo, contenido);
    }

    /// <summary>La lista por crédito de los mensajes de una parte del cierre, con el nombre del deudor.</summary>
    public static async Task<List<AccountingFactPartLoanDto>> PorCreditoAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> mensajes, CancellationToken ct)
    {
        var contenidos = await db.IntegrationMessages.AsNoTracking().Where(m => mensajes.Contains(m.PublicId))
            .Select(m => new { m.Type, m.PayloadJson }).ToListAsync(ct);
        var lineas = contenidos.SelectMany(c => Lineas(c.Type, c.PayloadJson)).ToList();
        var personas = lineas.Select(l => l.DebtorPersonPublicId).Distinct().ToList();
        var nombres = (await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.PublicId)).ToListAsync(ct))
            .ToDictionary(p => p.PublicId, ReferenciasDeGobierno.Nombre);
        return lineas.GroupBy(l => (l.Loan.LoanPublicId, l.Loan.LoanNumber, Deudor: nombres.GetValueOrDefault(l.DebtorPersonPublicId) ?? string.Empty))
            .Select(g => new AccountingFactPartLoanDto(g.Key.LoanPublicId, g.Key.LoanNumber, g.Key.Deudor,
                g.SelectMany(l => l.Amounts).GroupBy(a => a.ReasonCode is { Length: > 0 } motivo ? $"{a.Concept} · {motivo}" : a.Concept)
                    .ToDictionary(c => c.Key, c => c.Sum(a => a.Amount)),
                g.SelectMany(l => l.Amounts).Sum(a => a.Amount)))
            .OrderBy(c => c.LoanNumber, StringComparer.Ordinal)
            .ToList();
    }
}
