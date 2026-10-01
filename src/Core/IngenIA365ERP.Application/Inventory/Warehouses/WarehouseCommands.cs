using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Warehouses;

/// <summary>El código y el nombre que quien crea fija para la bodega de tránsito de la sucursal.</summary>
public sealed record BodegaDeTransitoPedida(string Code, string? Name);

/// <summary>Lo que se pide al crear una bodega (el alta unitaria y la plantilla 7). (nuevo)</summary>
public sealed record DatosDeBodega(string Code, string Name, Guid BranchPublicId, Guid WarehouseTypePublicId, string? Address, BodegaDeTransitoPedida? TransitWarehouse);

/// <summary>Lo que dejó el alta en el contexto (sin guardar): la bodega, la de tránsito si nació con ella y los avisos. (nuevo)</summary>
public sealed record AltaDeBodega(Warehouse Bodega, Warehouse? Transito, IReadOnlyList<AvisoDto> Avisos);

/// <summary>
/// Alta de una bodega (feature 012, T222; contracts/api.md §4.2, <c>POST /api/inventory/warehouses</c>; FR-032). Con la
/// primera bodega operativa de la sucursal nace, en la misma transacción, su bodega de tránsito: el código lo propone el
/// sistema (<c>TR</c> + código de la sucursal) o lo fija quien crea (<see cref="TransitWarehouse"/>, que en otra bodega es
/// 400 <c>Validation.Invalid</c>). Toda bodega nace <c>NotActivated</c> con su ubicación <c>GENERAL</c>; sin municipio en la
/// sucursal, el aviso <c>Inventory.Branch.MunicipalityMissing</c>. Un tipo de tránsito: <c>Inventory.WarehouseType.TransitIsSystem</c>.
/// Crearla no la asigna a nadie. La regla es <see cref="CreateWarehouseCommandHandler.AplicarReglasAsync"/>, que reusa la
/// plantilla de bodegas. (nuevo)
/// </summary>
public sealed record CreateWarehouseCommand(
    string Code, string Name, Guid BranchPublicId, Guid WarehouseTypePublicId, string? Address = null, BodegaDeTransitoPedida? TransitWarehouse = null)
    : IRequest<Result<CreateWarehouseResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.BranchPublicId).NotEmpty();
        RuleFor(x => x.WarehouseTypePublicId).NotEmpty();
        RuleFor(x => x.Address).MaximumLength(200);
        RuleFor(x => x.TransitWarehouse!.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron)
            .When(x => x.TransitWarehouse is not null);
        RuleFor(x => x.TransitWarehouse!.Name).MaximumLength(120).When(x => x.TransitWarehouse is not null);
    }
}

public sealed class CreateWarehouseCommandHandler(IApplicationDbContext db, VistaDeBodegas vista)
    : IRequestHandler<CreateWarehouseCommand, Result<CreateWarehouseResultDto>>
{
    public const string PrefijoDeTransito = "TR";

    public async Task<Result<CreateWarehouseResultDto>> Handle(CreateWarehouseCommand request, CancellationToken ct)
    {
        var alta = await AplicarReglasAsync(db,
            new DatosDeBodega(request.Code, request.Name, request.BranchPublicId, request.WarehouseTypePublicId, request.Address, request.TransitWarehouse), ct);
        if (alta.IsFailure) return Result.Failure<CreateWarehouseResultDto>(alta.Error);

        await db.SaveChangesAsync(ct);
        var t = alta.Value.Transito;
        return Result.Success(new CreateWarehouseResultDto(
            await vista.UnaAsync(alta.Value.Bodega, detalle: true, ct),
            t is null ? null : new WarehouseRefDto(t.PublicId, t.Code, t.Name),
            alta.Value.Avisos));
    }

    /// <summary>
    /// La regla única del alta de una bodega y de su tránsito (T222), que reusa <c>ImportWarehousesCommand</c> (T229): deja
    /// en el contexto la bodega, su ubicación por defecto y, si es la primera operativa de la sucursal, la de tránsito con la
    /// suya. No guarda.
    /// </summary>
    public static async Task<Result<AltaDeBodega>> AplicarReglasAsync(IApplicationDbContext db, DatosDeBodega datos, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(datos.Code)!;
        if (await DuplicadoAsync(db, codigo, ct) is { } dup) return Falla(dup);

        var sucursal = await db.Branches.FirstOrDefaultAsync(b => b.PublicId == datos.BranchPublicId, ct);
        if (sucursal is null) return Falla(WarehouseErrors.BranchNotFound());
        var tipo = await db.WarehouseTypes.FirstOrDefaultAsync(t => t.PublicId == datos.WarehouseTypePublicId, ct);
        if (tipo is null) return Falla(WarehouseErrors.WarehouseTypeNotFound());
        if (tipo.Behavior == WarehouseBehavior.Transit) return Falla(WarehouseErrors.WarehouseTypeTransitIsSystem());

        var bodega = Nueva(codigo, datos.Name.Trim(), sucursal.Id, tipo, datos.Address);

        // También lo que ya dejó en el contexto esta misma operación (la plantilla trae varias bodegas de una sucursal).
        var esLaPrimera = !db.Warehouses.Local.Any(w => w.BranchId == sucursal.Id && w != bodega && !w.IsDeleted)
            && !await db.Warehouses.AnyAsync(w => w.BranchId == sucursal.Id, ct);
        if (datos.TransitWarehouse is not null && !esLaPrimera)
            return Falla(Invalido("La bodega de tránsito sólo se fija con la primera bodega operativa de la sucursal: esta sucursal ya tiene bodegas."));

        Warehouse? transito = null;
        if (esLaPrimera)
        {
            var tipoTransito = await db.WarehouseTypes.Where(t => t.Behavior == WarehouseBehavior.Transit).OrderBy(t => t.Id).FirstOrDefaultAsync(ct);
            if (tipoTransito is null) return Falla(WarehouseErrors.WarehouseTypeNotFound("tránsito"));

            var codigoTransito = CodigoDeCatalogo.Normalizar(datos.TransitWarehouse?.Code)
                ?? (sucursal.LegacyCode is { Length: > 0 } cs ? PrefijoDeTransito + cs.Trim().ToUpperInvariant() : null);
            if (codigoTransito is null || codigoTransito.Length > CodigoDeCatalogo.LargoCorto
                || !System.Text.RegularExpressions.Regex.IsMatch(codigoTransito, CodigoDeCatalogo.Patron))
                return Falla(Invalido("No se puede proponer el código de la bodega de tránsito (la sucursal no tiene código o es muy largo): indíquelo en transitWarehouse.code."));
            if (codigoTransito == codigo)
                return Falla(Invalido("La bodega de tránsito necesita un código distinto del de la bodega."));
            if (await DuplicadoAsync(db, codigoTransito, ct) is { } dupT) return Falla(dupT);

            var nombre = string.IsNullOrWhiteSpace(datos.TransitWarehouse?.Name) ? $"Tránsito {sucursal.Name}" : datos.TransitWarehouse!.Name!.Trim();
            transito = Nueva(codigoTransito, nombre.Length > 120 ? nombre[..120] : nombre, sucursal.Id, tipoTransito, null);
        }

        var avisos = new List<AvisoDto>();
        if (string.IsNullOrWhiteSpace(sucursal.MunicipalityDaneCode))
        {
            var aviso = WarehouseErrors.BranchMunicipalityMissing(sucursal.PublicId);
            avisos.Add(new AvisoDto(aviso.Code, aviso.Message, ((ErrorConDatos)aviso).Data));
        }
        db.Warehouses.Add(bodega);
        if (transito is not null) db.Warehouses.Add(transito);
        return Result.Success(new AltaDeBodega(bodega, transito, avisos));

        Warehouse Nueva(string c, string n, int sucursalId, WarehouseType t, string? direccion)
        {
            var w = new Warehouse
            {
                Code = c, Name = n, BranchId = sucursalId, WarehouseTypeId = t.Id, WarehouseType = t, Behavior = t.Behavior,
                ActivationStatus = WarehouseActivationStatus.NotActivated, Address = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim(),
                IsActive = true,
            };
            w.Locations.Add(new WarehouseLocation
            {
                Warehouse = w, Code = WarehouseLocation.CodigoPorDefecto, Name = WarehouseLocation.NombrePorDefecto, IsDefault = true, IsActive = true,
            });
            return w;
        }
    }

    private static async Task<Error?> DuplicadoAsync(IApplicationDbContext db, string codigo, CancellationToken ct)
    {
        var local = db.Warehouses.Local.FirstOrDefault(w => w.Code == codigo && !w.IsDeleted);
        if (local is not null) return CodigoDeCatalogo.Duplicado("una bodega", codigo, local.Name, local.PublicId);
        var existente = await db.Warehouses.Where(w => w.Code == codigo).Select(w => new { w.PublicId, w.Name }).FirstOrDefaultAsync(ct);
        return existente is null ? null : CodigoDeCatalogo.Duplicado("una bodega", codigo, existente.Name, existente.PublicId);
    }

    private static Error Invalido(string mensaje) => new("Validation.Invalid", mensaje);

    private static Result<AltaDeBodega> Falla(Error e) => Result.Failure<AltaDeBodega>(e);
}

/// <summary>
/// Edición de una bodega (T222; §4.2, <c>PUT /{id}</c>): nombre, tipo y dirección. La sucursal y el código no cambian; el
/// tipo sólo por otro del mismo comportamiento (<c>Inventory.Warehouse.BehaviorLocked</c>). Fuera del alcance, el 404 de
/// la bodega. (nuevo)
/// </summary>
public sealed record UpdateWarehouseCommand(Guid WarehousePublicId, string Name, Guid WarehouseTypePublicId, string? Address = null)
    : IRequest<Result<WarehouseDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.WarehouseTypePublicId).NotEmpty();
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public sealed class UpdateWarehouseCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, VistaDeBodegas vista)
    : IRequestHandler<UpdateWarehouseCommand, Result<WarehouseDto>>
{
    public async Task<Result<WarehouseDto>> Handle(UpdateWarehouseCommand request, CancellationToken ct)
    {
        var bodega = await BodegasDelAlcance.BuscarAsync(db, alcance, request.WarehousePublicId, ct);
        if (bodega is null) return Result.Failure<WarehouseDto>(WarehouseErrors.WarehouseNotFound());
        var tipo = await db.WarehouseTypes.FirstOrDefaultAsync(t => t.PublicId == request.WarehouseTypePublicId, ct);
        if (tipo is null) return Result.Failure<WarehouseDto>(WarehouseErrors.WarehouseTypeNotFound());
        if (tipo.Behavior != bodega.Behavior) return Result.Failure<WarehouseDto>(WarehouseErrors.WarehouseBehaviorLocked());

        bodega.Name = request.Name.Trim();
        bodega.WarehouseTypeId = tipo.Id;
        bodega.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success(await vista.UnaAsync(bodega, detalle: true, ct));
    }
}

/// <summary>
/// Inactivar o reactivar una bodega con motivo (T222; §4.2): con existencia en cualquier ubicación,
/// <c>Inventory.Warehouse.HasStock</c> (<c>products</c>, <c>quantity</c>); la de tránsito con mercancía en camino,
/// <c>.TransitHasStock</c>. La existencia la informa <see cref="IExistenciasParaElCatalogo"/> (US2). (nuevo)
/// </summary>
public sealed record SetWarehouseActiveCommand(Guid WarehousePublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetWarehouseActiveCommandValidator : ValidadorConMotivo<SetWarehouseActiveCommand>
{
    public SetWarehouseActiveCommandValidator() => RuleFor(x => x.WarehousePublicId).NotEmpty();
}

public sealed class SetWarehouseActiveCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, IExistenciasParaElCatalogo existencias)
    : IRequestHandler<SetWarehouseActiveCommand, Result>
{
    public async Task<Result> Handle(SetWarehouseActiveCommand request, CancellationToken ct)
    {
        var bodega = await BodegasDelAlcance.BuscarAsync(db, alcance, request.WarehousePublicId, ct);
        if (bodega is null) return Result.Failure(WarehouseErrors.WarehouseNotFound());
        if (bodega.IsActive == request.Active) return Result.Success();
        if (!request.Active)
        {
            var hay = await existencias.DeBodegaAsync(bodega.Id, ct);
            if (hay.HayExistencia)
                return Result.Failure(bodega.EsTransito
                    ? WarehouseErrors.WarehouseTransitHasStock(hay.Products, hay.Quantity)
                    : WarehouseErrors.WarehouseHasStock(hay.Products, hay.Quantity));
        }
        bodega.IsActive = request.Active;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Buscar una bodega por su PublicId dentro del alcance de quien pide (fuera = no existe, §2.2). (nuevo)</summary>
public static class BodegasDelAlcance
{
    public static async Task<Warehouse?> BuscarAsync(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, Guid publicId, CancellationToken ct)
    {
        var bodega = await db.Warehouses.FirstOrDefaultAsync(w => w.PublicId == publicId, ct);
        if (bodega is null) return null;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        return alcance.IncluyeBodega(bodega.Id) ? bodega : null;
    }
}
