using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.PaymentMeans;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MedioDePago = IngenIA365ERP.Domain.Entities.Core.Payments.PaymentMeans;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>
/// Dónde se ofrece un medio (feature 012, I3, T594; contracts/api.md §22.3; T25): los tres conjuntos explícitos del módulo
/// (<c>INV_PaymentMeansPointsOfSale</c>, <c>…Channels</c>, <c>…DocumentTypes</c>) junto a las tres marcas «todos» del medio de
/// Core. Un conjunto vacío no es «todos». La regla que decide si se ofrece es <c>DisponibilidadDeMedio</c>, pura. (nuevo)
/// </summary>
public static class DisponibilidadDeMedioEnBase
{
    /// <summary>
    /// La disponibilidad del medio. Con <paramref name="alcance"/>, los puntos fuera del alcance de quien pregunta no se
    /// muestran (T35); sin él, todos.
    /// </summary>
    public static async Task<PaymentMeansAvailabilityDto> LeerAsync(IApplicationDbContext db, MedioDePago medio, AlcanceDeInventario? alcance, CancellationToken ct)
    {
        var puntos = await db.PaymentMeansPointsOfSale.AsNoTracking().Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted)
            .Join(db.PointsOfSale.AsNoTracking(), x => x.PointOfSaleId, p => p.Id, (x, p) => new { p.Id, p.PublicId }).ToListAsync(ct);
        var canales = await db.PaymentMeansChannels.AsNoTracking().Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted)
            .Join(db.SalesChannels.AsNoTracking(), x => x.SalesChannelId, c => c.Id, (x, c) => c.PublicId).ToListAsync(ct);
        var tipos = await db.PaymentMeansDocumentTypes.AsNoTracking().Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted)
            .Join(db.InventoryDocumentTypes.AsNoTracking(), x => x.DocumentTypeId, t => t.Id, (x, t) => t.PublicId).ToListAsync(ct);
        return new PaymentMeansAvailabilityDto(
            medio.OfferedAtAllPointsOfSale, puntos.Where(p => alcance is null || alcance.IncluyePunto(p.Id)).Select(p => p.PublicId).ToList(),
            medio.OfferedInAllChannels, canales,
            medio.OfferedForAllDocumentTypes, tipos);
    }

    /// <summary>
    /// Deja los tres conjuntos exactamente como se piden (bajas lógicas de lo que sale, altas de lo que entra). Los puntos que ya
    /// estaban y quedan fuera de <paramref name="puntosQueSeVen"/> se conservan: quien no los ve no puede estar quitándolos. No guarda.
    /// </summary>
    public static async Task ReemplazarAsync(
        IApplicationDbContext db, MedioDePago medio, IReadOnlyCollection<int> puntos, IReadOnlyCollection<int> canales, IReadOnlyCollection<int> tipos,
        Func<int, bool> puntosQueSeVen, DateTime ahora, CancellationToken ct)
    {
        var vivosP = medio.Id == 0 ? [] : await db.PaymentMeansPointsOfSale.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).ToListAsync(ct);
        foreach (var x in vivosP.Where(x => !puntos.Contains(x.PointOfSaleId) && puntosQueSeVen(x.PointOfSaleId))) { x.IsDeleted = true; x.DeletedAt = ahora; }
        foreach (var id in puntos.Where(id => vivosP.All(v => v.PointOfSaleId != id)))
            db.PaymentMeansPointsOfSale.Add(new PaymentMeansPointOfSale { PaymentMeans = medio, PaymentMeansId = medio.Id, PointOfSaleId = id });

        var vivosC = medio.Id == 0 ? [] : await db.PaymentMeansChannels.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).ToListAsync(ct);
        foreach (var x in vivosC.Where(x => !canales.Contains(x.SalesChannelId))) { x.IsDeleted = true; x.DeletedAt = ahora; }
        foreach (var id in canales.Where(id => vivosC.All(v => v.SalesChannelId != id)))
            db.PaymentMeansChannels.Add(new PaymentMeansChannel { PaymentMeans = medio, PaymentMeansId = medio.Id, SalesChannelId = id });

        var vivosT = medio.Id == 0 ? [] : await db.PaymentMeansDocumentTypes.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).ToListAsync(ct);
        foreach (var x in vivosT.Where(x => !tipos.Contains(x.DocumentTypeId))) { x.IsDeleted = true; x.DeletedAt = ahora; }
        foreach (var id in tipos.Where(id => vivosT.All(v => v.DocumentTypeId != id)))
            db.PaymentMeansDocumentTypes.Add(new PaymentMeansDocumentType { PaymentMeans = medio, PaymentMeansId = medio.Id, DocumentTypeId = id });
    }
}

/// <summary>Dónde se ofrece un medio (§22.3, <c>GET /api/inventory/payment-means/{id}/availability</c>). (nuevo)</summary>
public sealed record GetPaymentMeansAvailabilityQuery(Guid PaymentMeansPublicId) : IRequest<Result<PaymentMeansAvailabilityDto>>;

public sealed class GetPaymentMeansAvailabilityQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<GetPaymentMeansAvailabilityQuery, Result<PaymentMeansAvailabilityDto>>
{
    public async Task<Result<PaymentMeansAvailabilityDto>> Handle(GetPaymentMeansAvailabilityQuery request, CancellationToken ct)
    {
        var medio = await db.PaymentMeans.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId && !m.IsDeleted, ct);
        if (medio is null) return Result.Failure<PaymentMeansAvailabilityDto>(PaymentMeansErrors.NotFound());
        return Result.Success(await DisponibilidadDeMedioEnBase.LeerAsync(db, medio, await alcanceDeLaPeticion.ObtenerAsync(ct), ct));
    }
}

/// <summary>
/// Los tres conjuntos de un medio (§22.3, <c>PUT /api/inventory/payment-means/{id}/availability</c>). Un conjunto explícito junto
/// con la marca «todos» del medio en la misma dimensión es <c>Inventory.PaymentMeans.AvailabilityConflict</c> (las marcas se
/// escriben en el medio, §22.1). Un punto fuera del alcance de quien pide es el 404 del punto. (nuevo)
/// </summary>
public sealed record SetPaymentMeansAvailabilityCommand(
    Guid PaymentMeansPublicId,
    IReadOnlyList<Guid> PointOfSalePublicIds,
    IReadOnlyList<Guid> SalesChannelPublicIds,
    IReadOnlyList<Guid> DocumentTypePublicIds)
    : IRequest<Result<PaymentMeansAvailabilityDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetPaymentMeansAvailabilityCommandValidator : AbstractValidator<SetPaymentMeansAvailabilityCommand>
{
    public SetPaymentMeansAvailabilityCommandValidator()
    {
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
        RuleFor(x => x.PointOfSalePublicIds).NotNull();
        RuleFor(x => x.SalesChannelPublicIds).NotNull();
        RuleFor(x => x.DocumentTypePublicIds).NotNull();
    }
}

public sealed class SetPaymentMeansAvailabilityCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<SetPaymentMeansAvailabilityCommand, Result<PaymentMeansAvailabilityDto>>
{
    public async Task<Result<PaymentMeansAvailabilityDto>> Handle(SetPaymentMeansAvailabilityCommand request, CancellationToken ct)
    {
        var medio = await db.PaymentMeans.FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId && !m.IsDeleted, ct);
        if (medio is null) return Result.Failure<PaymentMeansAvailabilityDto>(PaymentMeansErrors.NotFound());

        if (medio.OfferedAtAllPointsOfSale && request.PointOfSalePublicIds.Count > 0)
            return Result.Failure<PaymentMeansAvailabilityDto>(ErroresDePuntoDeVenta.AvailabilityConflict("pointsOfSale"));
        if (medio.OfferedInAllChannels && request.SalesChannelPublicIds.Count > 0)
            return Result.Failure<PaymentMeansAvailabilityDto>(ErroresDePuntoDeVenta.AvailabilityConflict("salesChannels"));
        if (medio.OfferedForAllDocumentTypes && request.DocumentTypePublicIds.Count > 0)
            return Result.Failure<PaymentMeansAvailabilityDto>(ErroresDePuntoDeVenta.AvailabilityConflict("documentTypes"));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var pedidosP = request.PointOfSalePublicIds.Distinct().ToList();
        var puntos = await db.PointsOfSale.Where(p => pedidosP.Contains(p.PublicId) && !p.IsDeleted).Select(p => p.Id).ToListAsync(ct);
        if (puntos.Count != pedidosP.Count || !puntos.All(alcance.IncluyePunto))
            return Result.Failure<PaymentMeansAvailabilityDto>(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        var pedidosC = request.SalesChannelPublicIds.Distinct().ToList();
        var canales = await db.SalesChannels.Where(c => pedidosC.Contains(c.PublicId) && !c.IsDeleted).Select(c => c.Id).ToListAsync(ct);
        if (canales.Count != pedidosC.Count) return Result.Failure<PaymentMeansAvailabilityDto>(CatalogErrors.SalesChannelNotFound());
        var pedidosT = request.DocumentTypePublicIds.Distinct().ToList();
        var tipos = await db.InventoryDocumentTypes.Where(t => pedidosT.Contains(t.PublicId) && !t.IsDeleted).Select(t => t.Id).ToListAsync(ct);
        if (tipos.Count != pedidosT.Count) return Result.Failure<PaymentMeansAvailabilityDto>(InventoryErrors.DocumentTypeNotFound());

        await DisponibilidadDeMedioEnBase.ReemplazarAsync(db, medio, puntos, canales, tipos, alcance.IncluyePunto, reloj.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await DisponibilidadDeMedioEnBase.LeerAsync(db, medio, alcance, ct));
    }
}
