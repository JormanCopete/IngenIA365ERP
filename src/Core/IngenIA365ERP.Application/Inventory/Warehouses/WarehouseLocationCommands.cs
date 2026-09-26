using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Warehouses;

/// <summary>
/// Alta de una ubicación en una bodega (feature 012, T223; contracts/api.md §4.3; FR-032, FR-039): código único dentro de la
/// bodega (<c>Catalogo.CodigoDuplicado</c>); la de tránsito sólo tiene su ubicación por defecto
/// (<c>Inventory.Location.TransitHasOnlyDefault</c>). Marcarla por defecto desmarca la anterior. (nuevo)
/// </summary>
public sealed record CreateWarehouseLocationCommand(Guid WarehousePublicId, string Code, string Name, bool IsDefault = false)
    : IRequest<Result<WarehouseLocationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateWarehouseLocationCommandValidator : AbstractValidator<CreateWarehouseLocationCommand>
{
    public CreateWarehouseLocationCommandValidator()
    {
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoLargo)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class CreateWarehouseLocationCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance)
    : IRequestHandler<CreateWarehouseLocationCommand, Result<WarehouseLocationDto>>
{
    public async Task<Result<WarehouseLocationDto>> Handle(CreateWarehouseLocationCommand request, CancellationToken ct)
    {
        var bodega = await BodegasDelAlcance.BuscarAsync(db, alcance, request.WarehousePublicId, ct);
        if (bodega is null) return Result.Failure<WarehouseLocationDto>(WarehouseErrors.WarehouseNotFound());
        if (bodega.EsTransito) return Result.Failure<WarehouseLocationDto>(WarehouseErrors.LocationTransitHasOnlyDefault());

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.WarehouseLocations.Where(l => l.WarehouseId == bodega.Id && l.Code == codigo)
            .Select(l => new { l.PublicId, l.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return Result.Failure<WarehouseLocationDto>(CodigoDeCatalogo.Duplicado("una ubicación en esta bodega", codigo, existente.Name, existente.PublicId));

        var ubicacion = new WarehouseLocation { WarehouseId = bodega.Id, Code = codigo, Name = request.Name.Trim(), IsActive = true };
        if (request.IsDefault) await UbicacionPorDefecto.MarcarAsync(db, bodega.Id, ubicacion, ct);
        db.WarehouseLocations.Add(ubicacion);
        await db.SaveChangesAsync(ct);
        return Result.Success(UbicacionPorDefecto.Dto(ubicacion));
    }
}

/// <summary>
/// Edición de una ubicación (T223; §4.3, <c>PUT /{locationId}</c>): nombre y marca por defecto. Marcar otra por defecto
/// desmarca la anterior; desmarcar la única por defecto no se puede (<c>Inventory.Location.IsDefault</c>). (nuevo)
/// </summary>
public sealed record UpdateWarehouseLocationCommand(Guid WarehousePublicId, Guid LocationPublicId, string Name, bool IsDefault)
    : IRequest<Result<WarehouseLocationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateWarehouseLocationCommandValidator : AbstractValidator<UpdateWarehouseLocationCommand>
{
    public UpdateWarehouseLocationCommandValidator()
    {
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.LocationPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class UpdateWarehouseLocationCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance)
    : IRequestHandler<UpdateWarehouseLocationCommand, Result<WarehouseLocationDto>>
{
    public async Task<Result<WarehouseLocationDto>> Handle(UpdateWarehouseLocationCommand request, CancellationToken ct)
    {
        var (bodega, ubicacion) = await UbicacionPorDefecto.BuscarAsync(db, alcance, request.WarehousePublicId, request.LocationPublicId, ct);
        if (bodega is null) return Result.Failure<WarehouseLocationDto>(WarehouseErrors.WarehouseNotFound());
        if (ubicacion is null) return Result.Failure<WarehouseLocationDto>(WarehouseErrors.LocationNotFound());

        if (ubicacion.IsDefault && !request.IsDefault) return Result.Failure<WarehouseLocationDto>(WarehouseErrors.LocationIsDefault());
        if (!ubicacion.IsDefault && request.IsDefault)
        {
            if (!ubicacion.IsActive) return Result.Failure<WarehouseLocationDto>(WarehouseErrors.LocationNotFound());
            await UbicacionPorDefecto.MarcarAsync(db, bodega.Id, ubicacion, ct);
        }
        ubicacion.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success(UbicacionPorDefecto.Dto(ubicacion));
    }
}

/// <summary>
/// Inactivar o reactivar una ubicación con motivo (T223; §4.3): la por defecto no se inactiva
/// (<c>Inventory.Location.IsDefault</c>) y ninguna con existencia (<c>.HasStock</c>, por
/// <see cref="IExistenciasParaElCatalogo"/>). (nuevo)
/// </summary>
public sealed record SetWarehouseLocationActiveCommand(Guid WarehousePublicId, Guid LocationPublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetWarehouseLocationActiveCommandValidator : ValidadorConMotivo<SetWarehouseLocationActiveCommand>
{
    public SetWarehouseLocationActiveCommandValidator()
    {
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.LocationPublicId).NotEmpty();
    }
}

public sealed class SetWarehouseLocationActiveCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, IExistenciasParaElCatalogo existencias)
    : IRequestHandler<SetWarehouseLocationActiveCommand, Result>
{
    public async Task<Result> Handle(SetWarehouseLocationActiveCommand request, CancellationToken ct)
    {
        var (bodega, ubicacion) = await UbicacionPorDefecto.BuscarAsync(db, alcance, request.WarehousePublicId, request.LocationPublicId, ct);
        if (bodega is null) return Result.Failure(WarehouseErrors.WarehouseNotFound());
        if (ubicacion is null) return Result.Failure(WarehouseErrors.LocationNotFound());
        if (ubicacion.IsActive == request.Active) return Result.Success();
        if (!request.Active)
        {
            if (ubicacion.IsDefault) return Result.Failure(WarehouseErrors.LocationIsDefault());
            var hay = await existencias.DeUbicacionAsync(ubicacion.Id, ct);
            if (hay.HayExistencia) return Result.Failure(WarehouseErrors.LocationHasStock(hay.Products, hay.Quantity));
        }
        ubicacion.IsActive = request.Active;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Una sola ubicación por defecto por bodega (data-model §2.3). (nuevo)</summary>
internal static class UbicacionPorDefecto
{
    public static async Task MarcarAsync(IApplicationDbContext db, int bodegaId, WarehouseLocation nueva, CancellationToken ct)
    {
        foreach (var anterior in await db.WarehouseLocations.Where(l => l.WarehouseId == bodegaId && l.IsDefault).ToListAsync(ct))
            if (anterior != nueva) anterior.IsDefault = false;
        nueva.IsDefault = true;
    }

    public static async Task<(Warehouse? Bodega, WarehouseLocation? Ubicacion)> BuscarAsync(
        IApplicationDbContext db, IAlcanceDeInventario alcance, Guid bodega, Guid ubicacion, CancellationToken ct)
    {
        var b = await BodegasDelAlcance.BuscarAsync(db, alcance, bodega, ct);
        if (b is null) return (null, null);
        return (b, await db.WarehouseLocations.FirstOrDefaultAsync(l => l.PublicId == ubicacion && l.WarehouseId == b.Id, ct));
    }

    public static WarehouseLocationDto Dto(WarehouseLocation l) => new(l.PublicId, l.Code, l.Name, l.IsDefault, l.IsActive);
}
