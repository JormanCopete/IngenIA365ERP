using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Warehouses;

/// <summary>
/// Las bodegas del alcance de quien pregunta (feature 012, T223; contracts/api.md §4.2, <c>GET /api/inventory/warehouses</c>),
/// por sucursal y código: filtra por <see cref="IAlcanceDeInventario"/> con <c>FiltroDeAlcance</c> (lo de afuera no aparece
/// ni cuenta). La de tránsito sólo con <see cref="IncludeTransit"/>. (nuevo)
/// </summary>
public sealed record ListWarehousesQuery(
    Guid? BranchPublicId = null,
    Guid? TypePublicId = null,
    WarehouseActivationStatus? ActivationStatus = null,
    bool IncludeTransit = false,
    bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<WarehouseDto>>>;

public sealed class ListWarehousesQueryValidator : AbstractValidator<ListWarehousesQuery>
{
    public ListWarehousesQueryValidator() => RuleFor(x => x.ActivationStatus).IsInEnum();
}

public sealed class ListWarehousesQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, VistaDeBodegas vista)
    : IRequestHandler<ListWarehousesQuery, Result<IReadOnlyList<WarehouseDto>>>
{
    public async Task<Result<IReadOnlyList<WarehouseDto>>> Handle(ListWarehousesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.Warehouses.AsNoTracking().PorBodega(alcance, w => w.Id);
        if (!request.IncludeTransit) consulta = consulta.Where(w => w.Behavior != WarehouseBehavior.Transit);
        if (!request.IncludeInactive) consulta = consulta.Where(w => w.IsActive);
        if (request.ActivationStatus is { } estado) consulta = consulta.Where(w => w.ActivationStatus == estado);
        if (request.BranchPublicId is { } bp) consulta = consulta.Where(w => db.Branches.Any(b => b.Id == w.BranchId && b.PublicId == bp));
        if (request.TypePublicId is { } tp) consulta = consulta.Where(w => w.WarehouseType!.PublicId == tp);

        var bodegas = await consulta.ToListAsync(ct);
        var vistas = await vista.ArmarAsync(bodegas, detalle: false, ct);
        return Result.Success<IReadOnlyList<WarehouseDto>>(vistas
            .OrderBy(v => v.Branch.Name, StringComparer.CurrentCulture).ThenBy(v => v.Code, StringComparer.Ordinal).ToList());
    }
}

/// <summary>
/// Una bodega con sus ubicaciones y su saldo inicial (T223; §4.2, <c>GET /warehouses/{id}</c>). Fuera del alcance
/// (<see cref="IAlcanceDeInventario"/>), el mismo 404 que si no existiera. (nuevo)
/// </summary>
public sealed record GetWarehouseQuery(Guid WarehousePublicId) : IRequest<Result<WarehouseDto>>;

public sealed class GetWarehouseQueryValidator : AbstractValidator<GetWarehouseQuery>
{
    public GetWarehouseQueryValidator() => RuleFor(x => x.WarehousePublicId).NotEmpty();
}

public sealed class GetWarehouseQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, VistaDeBodegas vista)
    : IRequestHandler<GetWarehouseQuery, Result<WarehouseDto>>
{
    public async Task<Result<WarehouseDto>> Handle(GetWarehouseQuery request, CancellationToken ct)
    {
        var bodega = await BodegasDelAlcance.BuscarAsync(db, alcanceDeLaPeticion, request.WarehousePublicId, ct);
        return bodega is null
            ? Result.Failure<WarehouseDto>(WarehouseErrors.WarehouseNotFound())
            : Result.Success(await vista.UnaAsync(bodega, detalle: true, ct));
    }
}

/// <summary>Los tipos de bodega (T223; §4.1, <c>GET /warehouse-types</c>), por código. (nuevo)</summary>
public sealed record ListWarehouseTypesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<WarehouseTypeDto>>>;

public sealed class ListWarehouseTypesQueryValidator : AbstractValidator<ListWarehouseTypesQuery>;

public sealed class ListWarehouseTypesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListWarehouseTypesQuery, Result<IReadOnlyList<WarehouseTypeDto>>>
{
    public async Task<Result<IReadOnlyList<WarehouseTypeDto>>> Handle(ListWarehouseTypesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<WarehouseTypeDto>>((await db.WarehouseTypes.AsNoTracking()
                .Where(t => request.IncludeInactive || t.IsActive).OrderBy(t => t.Code).ToListAsync(ct))
            .Select(CreateWarehouseTypeCommandHandler.Dto).ToList());
}

/// <summary>
/// Las ubicaciones de una bodega del alcance (T223; §4.3, <c>GET /warehouses/{id}/locations</c>): la por defecto primero.
/// Fuera del alcance (<see cref="IAlcanceDeInventario"/>), el 404 de la bodega. (nuevo)
/// </summary>
public sealed record ListLocationsQuery(Guid WarehousePublicId, bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<WarehouseLocationDto>>>;

public sealed class ListLocationsQueryValidator : AbstractValidator<ListLocationsQuery>
{
    public ListLocationsQueryValidator() => RuleFor(x => x.WarehousePublicId).NotEmpty();
}

public sealed class ListLocationsQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListLocationsQuery, Result<IReadOnlyList<WarehouseLocationDto>>>
{
    public async Task<Result<IReadOnlyList<WarehouseLocationDto>>> Handle(ListLocationsQuery request, CancellationToken ct)
    {
        var bodega = await BodegasDelAlcance.BuscarAsync(db, alcanceDeLaPeticion, request.WarehousePublicId, ct);
        if (bodega is null) return Result.Failure<IReadOnlyList<WarehouseLocationDto>>(WarehouseErrors.WarehouseNotFound());
        return Result.Success<IReadOnlyList<WarehouseLocationDto>>(await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.WarehouseId == bodega.Id && (request.IncludeInactive || l.IsActive))
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.Code)
            .Select(l => new WarehouseLocationDto(l.PublicId, l.Code, l.Name, l.IsDefault, l.IsActive))
            .ToListAsync(ct));
    }
}

/// <summary>
/// Las bodegas a las que se puede trasladar (feature 012, US10, T375; contracts/api.md §11, §17.2,
/// <c>GET /warehouses?purpose=TransferDestination</c> y <c>GET /transfers/destinations</c>): para quien tiene
/// <c>Inventory.Transfers.Create</c>, <b>todas</b> las operativas y activas de la cooperativa, fuera de su alcance
/// (<see cref="IAlcanceDeInventario"/> no se aplica a propósito: el despacho no mueve la existencia del destino), con sólo
/// <c>{ publicId, code, name, branch }</c> —ni existencias ni valores—. Sin el permiso, el mismo 404 que lo inexistente. (nuevo)
/// </summary>
public sealed record ListTransferDestinationsQuery : IRequest<Result<IReadOnlyList<Transfers.TransferDestinationDto>>>
{
    /// <summary>El único propósito admitido en <c>?purpose=</c>.</summary>
    public const string Proposito = "TransferDestination";

    public const string PermisoRequerido = "Inventory.Transfers.Create";
}

public sealed class ListTransferDestinationsQueryValidator : AbstractValidator<ListTransferDestinationsQuery>;

public sealed class ListTransferDestinationsQueryHandler(IApplicationDbContext db, Payroll.Services.IPermissionChecker permisos)
    : IRequestHandler<ListTransferDestinationsQuery, Result<IReadOnlyList<Transfers.TransferDestinationDto>>>
{
    public async Task<Result<IReadOnlyList<Transfers.TransferDestinationDto>>> Handle(ListTransferDestinationsQuery request, CancellationToken ct)
    {
        if (!await permisos.HasPermissionAsync(ListTransferDestinationsQuery.PermisoRequerido, ct))
            return Result.Failure<IReadOnlyList<Transfers.TransferDestinationDto>>(WarehouseErrors.WarehouseNotFound());

        var bodegas = await db.Warehouses.AsNoTracking()
            .Where(w => w.Behavior == WarehouseBehavior.Operational && w.IsActive && w.ActivationStatus == WarehouseActivationStatus.Active)
            .Join(db.Branches.AsNoTracking(), w => w.BranchId, b => b.Id, (w, b) => new { w.PublicId, w.Code, w.Name, Sucursal = new BranchRefDto(b.PublicId, b.LegacyCode, b.Name) })
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<Transfers.TransferDestinationDto>>(bodegas
            .OrderBy(b => b.Sucursal.Name, StringComparer.CurrentCulture).ThenBy(b => b.Code, StringComparer.Ordinal)
            .Select(b => new Transfers.TransferDestinationDto(b.PublicId, b.Code, b.Name, b.Sucursal))
            .ToList());
    }
}
