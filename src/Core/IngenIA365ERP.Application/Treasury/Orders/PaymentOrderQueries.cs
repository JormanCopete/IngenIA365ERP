using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Orders;

// Feature 015, US10 (T0142; FR-107, FR-109, FR-121; contracts/api.md §10): la bandeja única de órdenes de pago, su cadena de regiros y volverlas a validar tras
// corregir el maestro. La bandeja no tiene alcance por cuenta: una orden todavía no tiene cuenta de origen (la elige el lote); lo que sí lo tiene son los lotes y
// sus archivos. (nuevo)

public sealed record PaymentOrderDto(
    Guid PublicId,
    string Number,
    PaymentOrderOrigin Origin,
    string OriginOrderNumber,
    string OrderKind,
    PaymentForm Form,
    Guid BeneficiaryPersonPublicId,
    string BeneficiaryName,
    string BeneficiaryTaxId,
    string? DestinationBankCode,
    string? DestinationAccountNumberMasked,
    decimal GrossAmount,
    decimal NetAmount,
    DateOnly PayDate,
    string Concept,
    PaymentOrderState Status,
    IReadOnlyList<string> PendingReasons,
    PaymentBatchItemFlags Flags,
    bool PaidByLendingFile,
    bool VoidRequested,
    bool GmfExempt,
    string? GmfExemptionReason,
    decimal? GmfEstimate,
    Guid? ReplacesOrderPublicId,
    string? BatchNumber,
    DateTime ReceivedAt);

/// <summary>Lo común de las órdenes: el DTO (cuenta destino enmascarada) y la del <c>PublicId</c>. (nuevo)</summary>
public static class OrdenesDePago
{
    public static async Task<IReadOnlyList<PaymentOrderDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<PaymentOrder> ordenes, CancellationToken ct)
    {
        var personas = ordenes.Select(o => o.BeneficiaryPersonId).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var reemplazadas = ordenes.Where(o => o.ReplacesOrderId is not null).Select(o => o.ReplacesOrderId!.Value).Distinct().ToList();
        var anteriores = await db.PaymentOrders.AsNoTracking().Where(o => reemplazadas.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.PublicId, ct);
        var ids = ordenes.Select(o => o.Id).ToList();
        var lotes = await (from i in db.PaymentBatchItems.AsNoTracking()
                           join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id
                           where ids.Contains(i.PaymentOrderId) && i.IsLive
                           select new { i.PaymentOrderId, b.Number }).ToListAsync(ct);
        var loteDe = lotes.GroupBy(l => l.PaymentOrderId).ToDictionary(g => g.Key, g => g.First().Number);
        return ordenes.Select(o => new PaymentOrderDto(o.PublicId, o.Number, o.Origin, o.OriginOrderNumber, o.OrderKind, o.Form,
            publicos.GetValueOrDefault(o.BeneficiaryPersonId), o.BeneficiaryName, o.BeneficiaryTaxId, o.DestinationBankCode,
            o.DestinationAccountNumber is null ? null : CuentasBancariasPropias.Enmascarar(o.DestinationAccountNumber), o.GrossAmount, o.NetAmount, o.PayDate,
            o.Concept, o.Status, ValidadorDeOrdenes.Motivos(o), o.Flags, o.PaidByLendingFile, o.VoidRequested, o.GmfExempt, o.GmfExemptionReason, o.GmfEstimate,
            o.ReplacesOrderId is { } r ? anteriores.GetValueOrDefault(r) : null, loteDe.GetValueOrDefault(o.Id), o.ReceivedAt)).ToList();
    }

    public static async Task<PaymentOrderDto> DtoAsync(IApplicationDbContext db, PaymentOrder orden, CancellationToken ct) => (await DtosAsync(db, [orden], ct))[0];
}

/// <summary><c>GET /api/treasury/payment-orders?state=&amp;origin=&amp;age=</c> (<c>Treasury.Orders.View</c>): la bandeja, las más recientes primero. (nuevo)</summary>
public sealed record ListPaymentOrdersQuery(PaymentOrderState? State = null, PaymentOrderOrigin? Origin = null, int? AgeDays = null, string? Search = null)
    : IRequest<Result<IReadOnlyList<PaymentOrderDto>>>;

public sealed class ListPaymentOrdersQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListPaymentOrdersQuery, Result<IReadOnlyList<PaymentOrderDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentOrderDto>>> Handle(ListPaymentOrdersQuery request, CancellationToken ct)
    {
        var consulta = db.PaymentOrders.AsNoTracking();
        if (request.State is { } e) consulta = consulta.Where(o => o.Status == e);
        if (request.Origin is { } origen) consulta = consulta.Where(o => o.Origin == origen);
        if (request.AgeDays is { } dias)
        {
            var desde = reloj.UtcNow.AddDays(-dias);
            consulta = consulta.Where(o => o.ReceivedAt <= desde);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var texto = request.Search.Trim();
            consulta = consulta.Where(o => o.Number == texto || o.OriginOrderNumber == texto || o.BeneficiaryTaxId == texto || o.BeneficiaryName.Contains(texto));
        }
        var ordenes = await consulta.OrderByDescending(o => o.Id).Take(500).ToListAsync(ct);
        return Result.Success(await OrdenesDePago.DtosAsync(db, ordenes, ct));
    }
}

/// <summary><c>GET /api/treasury/payment-orders/{id}</c> (<c>Treasury.Orders.View</c>). (nuevo)</summary>
public sealed record GetPaymentOrderQuery(Guid PublicId) : IRequest<Result<PaymentOrderDto>>;

public sealed class GetPaymentOrderQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPaymentOrderQuery, Result<PaymentOrderDto>>
{
    public async Task<Result<PaymentOrderDto>> Handle(GetPaymentOrderQuery request, CancellationToken ct) =>
        await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.PublicId, ct) is { } o
            ? Result.Success(await OrdenesDePago.DtoAsync(db, o, ct))
            : Result.Failure<PaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("La orden de pago"));
}

/// <summary>
/// <c>GET /api/treasury/payment-orders/{id}/chain</c> (<c>Treasury.Orders.View</c>; FR-121): la cadena completa de regiros de una orden —la primera, las que la
/// reemplazaron y la vigente—, en orden. (nuevo)
/// </summary>
public sealed record GetPaymentOrderChainQuery(Guid PublicId) : IRequest<Result<IReadOnlyList<PaymentOrderDto>>>;

public sealed class GetPaymentOrderChainQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPaymentOrderChainQuery, Result<IReadOnlyList<PaymentOrderDto>>>
{
    public const int Tope = 50;

    public async Task<Result<IReadOnlyList<PaymentOrderDto>>> Handle(GetPaymentOrderChainQuery request, CancellationToken ct)
    {
        var orden = await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.PublicId, ct);
        if (orden is null) return Result.Failure<IReadOnlyList<PaymentOrderDto>>(ErroresDeTesoreria.NoEncontrado("La orden de pago"));
        var cadena = new List<PaymentOrder> { orden };
        var actual = orden;
        while (actual.ReplacesOrderId is { } anterior && cadena.Count < Tope)
        {
            actual = await db.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == anterior, ct);
            cadena.Insert(0, actual);
        }
        actual = orden;
        while (cadena.Count < Tope && await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.ReplacesOrderId == actual.Id, ct) is { } siguiente)
        {
            cadena.Add(siguiente);
            actual = siguiente;
        }
        return Result.Success(await OrdenesDePago.DtosAsync(db, cadena, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-orders/{id}/revalidate</c> (<c>Treasury.Orders.Manage</c>): vuelve a validar una orden recibida, pendiente o lista tras corregir el
/// maestro (cuenta registrada, banco con código, liberación del oficial) (FR-109). (nuevo)
/// </summary>
public sealed record RevalidatePaymentOrderCommand(Guid PublicId) : IRequest<Result<PaymentOrderDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RevalidatePaymentOrderCommandValidator : AbstractValidator<RevalidatePaymentOrderCommand>
{
    public RevalidatePaymentOrderCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class RevalidatePaymentOrderCommandHandler(IApplicationDbContext db, ICerrojoDeTesoreria cerrojo, ValidadorDeOrdenes validador)
    : IRequestHandler<RevalidatePaymentOrderCommand, Result<PaymentOrderDto>>
{
    public async Task<Result<PaymentOrderDto>> Handle(RevalidatePaymentOrderCommand request, CancellationToken ct)
    {
        var id = await db.PaymentOrders.AsNoTracking().Where(o => o.PublicId == request.PublicId).Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
        if (id is null) return Result.Failure<PaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("La orden de pago"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [id.Value], ct: ct);
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == id.Value, ct);
        if (!ValidadorDeOrdenes.Revalidable(orden.Status) || orden.PaidByLendingFile)
            return Result.Failure<PaymentOrderDto>(ErroresDeTesoreria.OrdenNoRevalidable(orden.Status.ToString()));
        await validador.ValidarAsync(orden, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OrdenesDePago.DtoAsync(db, orden, ct));
    }
}
