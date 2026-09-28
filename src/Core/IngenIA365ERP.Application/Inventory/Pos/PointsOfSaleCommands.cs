using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Core.PaymentMeans;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Warehouses;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pos;

// Puntos de venta y cajas (feature 012, I3, T593; contracts/api.md §20.1; FR-058, FR-067). Las reglas son ReglasDePuntoDeVenta,
// que reusa la plantilla 10. Un punto fuera del alcance de quien pide es el mismo 404 que uno inexistente (T35). (nuevo)

/// <summary>Alta de un punto de venta (§20.1, <c>POST /api/inventory/points-of-sale</c> → 201). El código no cambia después. (nuevo)</summary>
public sealed record CreatePointOfSaleCommand(
    string Code, string Name, Guid BranchPublicId, Guid SalesChannelPublicId, bool PosEnabled, Guid DefaultWarehousePublicId, string? Address = null)
    : IRequest<Result<PointOfSaleDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreatePointOfSaleCommandValidator : AbstractValidator<CreatePointOfSaleCommand>
{
    public CreatePointOfSaleCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDePuntoDeVenta.LargoDeNombreDePunto);
        RuleFor(x => x.BranchPublicId).NotEmpty();
        RuleFor(x => x.SalesChannelPublicId).NotEmpty();
        RuleFor(x => x.DefaultWarehousePublicId).NotEmpty();
        RuleFor(x => x.Address).MaximumLength(ReglasDePuntoDeVenta.LargoDeDireccion);
    }
}

public sealed class CreatePointOfSaleCommandHandler(IApplicationDbContext db) : IRequestHandler<CreatePointOfSaleCommand, Result<PointOfSaleDto>>
{
    public async Task<Result<PointOfSaleDto>> Handle(CreatePointOfSaleCommand request, CancellationToken ct)
    {
        var sucursal = await db.Branches.FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId && !b.IsDeleted, ct);
        if (sucursal is null) return Result.Failure<PointOfSaleDto>(WarehouseErrors.BranchNotFound());
        var datos = await ReferenciasDelPunto.ResolverAsync(db, request.Code, request.Name, sucursal, request.SalesChannelPublicId, request.PosEnabled,
            request.DefaultWarehousePublicId, request.Address, true, ct);
        if (datos.IsFailure) return Result.Failure<PointOfSaleDto>(datos.Error);
        var punto = await ReglasDePuntoDeVenta.AltaDePuntoAsync(db, datos.Value, ct);
        if (punto.IsFailure) return Result.Failure<PointOfSaleDto>(punto.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDePuntosDeVenta.UnoAsync(db, punto.Value, detalle: true, ct));
    }
}

/// <summary>
/// Edición de un punto (§20.1, <c>PUT /{id}</c>): nombre, canal, POS, bodega por defecto, dirección y activo; con una sesión
/// abierta no se desactiva (<c>Inventory.PointOfSale.HasOpenSessions</c>). El código y la sucursal no cambian. (nuevo)
/// </summary>
public sealed record UpdatePointOfSaleCommand(
    Guid PointOfSalePublicId, string Name, Guid SalesChannelPublicId, bool PosEnabled, Guid DefaultWarehousePublicId, bool IsActive, string? Address = null)
    : IRequest<Result<PointOfSaleDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdatePointOfSaleCommandValidator : AbstractValidator<UpdatePointOfSaleCommand>
{
    public UpdatePointOfSaleCommandValidator()
    {
        RuleFor(x => x.PointOfSalePublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDePuntoDeVenta.LargoDeNombreDePunto);
        RuleFor(x => x.SalesChannelPublicId).NotEmpty();
        RuleFor(x => x.DefaultWarehousePublicId).NotEmpty();
        RuleFor(x => x.Address).MaximumLength(ReglasDePuntoDeVenta.LargoDeDireccion);
    }
}

public sealed class UpdatePointOfSaleCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance)
    : IRequestHandler<UpdatePointOfSaleCommand, Result<PointOfSaleDto>>
{
    public async Task<Result<PointOfSaleDto>> Handle(UpdatePointOfSaleCommand request, CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, request.PointOfSalePublicId, ct);
        if (punto is null) return Result.Failure<PointOfSaleDto>(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        var sucursal = await db.Branches.FirstAsync(b => b.Id == punto.BranchId, ct);
        var datos = await ReferenciasDelPunto.ResolverAsync(db, punto.Code, request.Name, sucursal, request.SalesChannelPublicId, request.PosEnabled,
            request.DefaultWarehousePublicId, request.Address, request.IsActive, ct);
        if (datos.IsFailure) return Result.Failure<PointOfSaleDto>(datos.Error);
        var r = await ReglasDePuntoDeVenta.ActualizarPuntoAsync(db, punto, datos.Value, ct);
        if (r.IsFailure) return Result.Failure<PointOfSaleDto>(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDePuntosDeVenta.UnoAsync(db, punto, detalle: true, ct));
    }
}

/// <summary>
/// Alta de una caja en un punto (§20.1, <c>POST /points-of-sale/{id}/cash-registers</c> → 201): bodega de la sucursal del punto,
/// datáfono por defecto, formato de impresión y un tipo de documento por rol (<see cref="ReglasDePuntoDeVenta.AplicarCajaAsync"/>). (nuevo)
/// </summary>
public sealed record CreateCashRegisterCommand(
    Guid PointOfSalePublicId,
    string Code,
    string Name,
    Guid WarehousePublicId,
    Guid? DefaultCardTerminalPublicId,
    CashRegisterPrintFormat PrintFormat,
    IReadOnlyList<CashRegisterDocumentTypeInput> DocumentTypes,
    string? DianCashRegisterPlate = null,
    byte? PrintCopies = null)
    : IRequest<Result<CashRegisterDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCashRegisterCommandValidator : AbstractValidator<CreateCashRegisterCommand>
{
    public CreateCashRegisterCommandValidator()
    {
        RuleFor(x => x.PointOfSalePublicId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDePuntoDeVenta.LargoDeNombreDeCaja);
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.PrintFormat).IsInEnum();
        RuleFor(x => x.DocumentTypes).NotNull();
        RuleForEach(x => x.DocumentTypes).ChildRules(t =>
        {
            t.RuleFor(y => y.Role).IsInEnum();
            t.RuleFor(y => y.DocumentTypePublicId).NotEmpty();
        });
        RuleFor(x => x.DianCashRegisterPlate).MaximumLength(ReglasDePuntoDeVenta.LargoDePlaca);
        RuleFor(x => x.PrintCopies).InclusiveBetween((byte)1, (byte)5).When(x => x.PrintCopies is not null);
    }
}

public sealed class CreateCashRegisterCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, ILectorDeParametros parametros, IDateTimeService reloj)
    : IRequestHandler<CreateCashRegisterCommand, Result<CashRegisterDto>>
{
    public async Task<Result<CashRegisterDto>> Handle(CreateCashRegisterCommand request, CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, request.PointOfSalePublicId, ct);
        if (punto is null) return Result.Failure<CashRegisterDto>(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        var datos = await ReferenciasDeLaCaja.ResolverAsync(db, request.Code, request.Name, request.WarehousePublicId, request.DefaultCardTerminalPublicId,
            request.PrintFormat, request.DocumentTypes, request.DianCashRegisterPlate, request.PrintCopies, true, ct);
        if (datos.IsFailure) return Result.Failure<CashRegisterDto>(datos.Error);
        var obligada = await ReglasDePuntoDeVenta.ObligadaAFacturarAsync(parametros, reloj.HoyLocal, ct);
        var caja = await ReglasDePuntoDeVenta.AplicarCajaAsync(db, punto, null, datos.Value, obligada, reloj.UtcNow, ct);
        if (caja.IsFailure) return Result.Failure<CashRegisterDto>(caja.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDePuntosDeVenta.CajasAsync(db, punto, [caja.Value], ct))[0]);
    }
}

/// <summary>Edición de una caja (§20.1, <c>PUT /points-of-sale/{id}/cash-registers/{registerId}</c>): lo mismo que el alta, sin código. (nuevo)</summary>
public sealed record UpdateCashRegisterCommand(
    Guid PointOfSalePublicId,
    Guid CashRegisterPublicId,
    string Name,
    Guid WarehousePublicId,
    Guid? DefaultCardTerminalPublicId,
    CashRegisterPrintFormat PrintFormat,
    IReadOnlyList<CashRegisterDocumentTypeInput> DocumentTypes,
    string? DianCashRegisterPlate = null,
    byte? PrintCopies = null,
    bool IsActive = true)
    : IRequest<Result<CashRegisterDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCashRegisterCommandValidator : AbstractValidator<UpdateCashRegisterCommand>
{
    public UpdateCashRegisterCommandValidator()
    {
        RuleFor(x => x.PointOfSalePublicId).NotEmpty();
        RuleFor(x => x.CashRegisterPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDePuntoDeVenta.LargoDeNombreDeCaja);
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.PrintFormat).IsInEnum();
        RuleFor(x => x.DocumentTypes).NotNull();
        RuleForEach(x => x.DocumentTypes).ChildRules(t =>
        {
            t.RuleFor(y => y.Role).IsInEnum();
            t.RuleFor(y => y.DocumentTypePublicId).NotEmpty();
        });
        RuleFor(x => x.DianCashRegisterPlate).MaximumLength(ReglasDePuntoDeVenta.LargoDePlaca);
        RuleFor(x => x.PrintCopies).InclusiveBetween((byte)1, (byte)5).When(x => x.PrintCopies is not null);
    }
}

public sealed class UpdateCashRegisterCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, ILectorDeParametros parametros, IDateTimeService reloj)
    : IRequestHandler<UpdateCashRegisterCommand, Result<CashRegisterDto>>
{
    public async Task<Result<CashRegisterDto>> Handle(UpdateCashRegisterCommand request, CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, request.PointOfSalePublicId, ct);
        if (punto is null) return Result.Failure<CashRegisterDto>(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        var caja = await db.CashRegisters.FirstOrDefaultAsync(c => c.PublicId == request.CashRegisterPublicId && c.PointOfSaleId == punto.Id && !c.IsDeleted, ct);
        if (caja is null) return Result.Failure<CashRegisterDto>(ErroresDePuntoDeVenta.CashRegisterNotFound());
        var datos = await ReferenciasDeLaCaja.ResolverAsync(db, caja.Code, request.Name, request.WarehousePublicId, request.DefaultCardTerminalPublicId,
            request.PrintFormat, request.DocumentTypes, request.DianCashRegisterPlate, request.PrintCopies, request.IsActive, ct);
        if (datos.IsFailure) return Result.Failure<CashRegisterDto>(datos.Error);
        var obligada = await ReglasDePuntoDeVenta.ObligadaAFacturarAsync(parametros, reloj.HoyLocal, ct);
        var r = await ReglasDePuntoDeVenta.AplicarCajaAsync(db, punto, caja, datos.Value, obligada, reloj.UtcNow, ct);
        if (r.IsFailure) return Result.Failure<CashRegisterDto>(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDePuntosDeVenta.CajasAsync(db, punto, [caja], ct))[0]);
    }
}

// ================================================================================================ consultas --

/// <summary>Los puntos de venta del alcance de quien pregunta (§20.1, <c>GET /points-of-sale?branch=&amp;active=</c>). (nuevo)</summary>
public sealed record ListPointsOfSaleQuery(Guid? BranchPublicId = null, bool? Active = null) : IRequest<Result<IReadOnlyList<PointOfSaleDto>>>;

public sealed class ListPointsOfSaleQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListPointsOfSaleQuery, Result<IReadOnlyList<PointOfSaleDto>>>
{
    public async Task<Result<IReadOnlyList<PointOfSaleDto>>> Handle(ListPointsOfSaleQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var q = db.PointsOfSale.AsNoTracking().Where(p => !p.IsDeleted).PorPunto(alcance, p => p.Id);
        if (request.BranchPublicId is { } sucursal)
        {
            var id = await db.Branches.Where(b => b.PublicId == sucursal).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            q = q.Where(p => p.BranchId == (id ?? -1));
        }
        if (request.Active is { } activo) q = q.Where(p => p.IsActive == activo);
        var puntos = await q.OrderBy(p => p.Code).ToListAsync(ct);
        return Result.Success(await VistaDePuntosDeVenta.VariosAsync(db, puntos, ct));
    }
}

/// <summary>Un punto con sus cajas (§20.1, <c>GET /points-of-sale/{id}</c>); fuera del alcance, el 404 del punto. (nuevo)</summary>
public sealed record GetPointOfSaleQuery(Guid PointOfSalePublicId) : IRequest<Result<PointOfSaleDto>>;

public sealed class GetPointOfSaleQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcance)
    : IRequestHandler<GetPointOfSaleQuery, Result<PointOfSaleDto>>
{
    public async Task<Result<PointOfSaleDto>> Handle(GetPointOfSaleQuery request, CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, request.PointOfSalePublicId, ct);
        return punto is null
            ? Result.Failure<PointOfSaleDto>(ErroresDePuntoDeVenta.PointOfSaleNotFound())
            : Result.Success(await VistaDePuntosDeVenta.UnoAsync(db, punto, detalle: true, ct));
    }
}

/// <summary>Las cajas de un punto (§20.1, <c>GET /points-of-sale/{id}/cash-registers</c>). (nuevo)</summary>
public sealed record ListCashRegistersQuery(Guid PointOfSalePublicId) : IRequest<Result<IReadOnlyList<CashRegisterDto>>>;

public sealed class ListCashRegistersQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcance)
    : IRequestHandler<ListCashRegistersQuery, Result<IReadOnlyList<CashRegisterDto>>>
{
    public async Task<Result<IReadOnlyList<CashRegisterDto>>> Handle(ListCashRegistersQuery request, CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, request.PointOfSalePublicId, ct);
        if (punto is null) return Result.Failure<IReadOnlyList<CashRegisterDto>>(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        var cajas = await db.CashRegisters.AsNoTracking().Where(c => c.PointOfSaleId == punto.Id && !c.IsDeleted).OrderBy(c => c.Code).ToListAsync(ct);
        return Result.Success(await VistaDePuntosDeVenta.CajasAsync(db, punto, cajas, ct));
    }
}

// ================================================================================================ apoyo --

/// <summary>Un punto por su <c>PublicId</c> dentro del alcance de quien pide (fuera = no existe, T35). (nuevo)</summary>
public static class PuntosDelAlcance
{
    public static async Task<PointOfSale?> BuscarAsync(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, Guid publicId, CancellationToken ct)
    {
        var punto = await db.PointsOfSale.FirstOrDefaultAsync(p => p.PublicId == publicId && !p.IsDeleted, ct);
        if (punto is null) return null;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        return alcance.IncluyePunto(punto.Id) ? punto : null;
    }
}

/// <summary>Resuelve lo que cita el cuerpo de un punto (canal y bodega). (nuevo)</summary>
public static class ReferenciasDelPunto
{
    public static async Task<Result<DatosDePunto>> ResolverAsync(
        IApplicationDbContext db, string codigo, string nombre, Domain.Entities.Core.Branch sucursal, Guid canalId, bool pos, Guid bodegaId,
        string? direccion, bool activo, CancellationToken ct)
    {
        var canal = await db.SalesChannels.FirstOrDefaultAsync(c => c.PublicId == canalId && !c.IsDeleted, ct);
        if (canal is null) return Result.Failure<DatosDePunto>(CatalogErrors.SalesChannelNotFound());
        var bodega = await db.Warehouses.FirstOrDefaultAsync(w => w.PublicId == bodegaId && !w.IsDeleted, ct);
        if (bodega is null) return Result.Failure<DatosDePunto>(WarehouseErrors.WarehouseNotFound());
        return Result.Success(new DatosDePunto(codigo, nombre, sucursal, canal, pos, bodega, direccion, activo));
    }
}

/// <summary>Resuelve lo que cita el cuerpo de una caja (bodega, datáfono y un tipo por rol). (nuevo)</summary>
public static class ReferenciasDeLaCaja
{
    public static async Task<Result<DatosDeCaja>> ResolverAsync(
        IApplicationDbContext db, string codigo, string nombre, Guid bodegaId, Guid? datafonoId, CashRegisterPrintFormat formato,
        IReadOnlyList<CashRegisterDocumentTypeInput> tipos, string? placa, byte? copias, bool activa, CancellationToken ct)
    {
        var bodega = await db.Warehouses.FirstOrDefaultAsync(w => w.PublicId == bodegaId && !w.IsDeleted, ct);
        if (bodega is null) return Result.Failure<DatosDeCaja>(WarehouseErrors.WarehouseNotFound());
        CardTerminal? datafono = null;
        if (datafonoId is { } d)
        {
            datafono = await db.CardTerminals.FirstOrDefaultAsync(t => t.PublicId == d && !t.IsDeleted, ct);
            if (datafono is null) return Result.Failure<DatosDeCaja>(PaymentMeansErrors.CardTerminalNotFound());
        }
        var ids = tipos.Select(t => t.DocumentTypePublicId).Distinct().ToList();
        var encontrados = await db.InventoryDocumentTypes.Include(t => t.Warehouses).Where(t => ids.Contains(t.PublicId) && !t.IsDeleted)
            .ToDictionaryAsync(t => t.PublicId, ct);
        var pares = new List<(CashRegisterDocumentRole, InventoryDocumentType)>();
        foreach (var t in tipos)
        {
            if (!encontrados.TryGetValue(t.DocumentTypePublicId, out var tipo)) return Result.Failure<DatosDeCaja>(InventoryErrors.DocumentTypeNotFound());
            pares.Add((t.Role, tipo));
        }
        return Result.Success(new DatosDeCaja(codigo, nombre, bodega, datafono, formato, pares, placa, copias, activa));
    }
}

/// <summary>Arma los DTO de puntos y cajas con lo citado en bloque. (nuevo)</summary>
public static class VistaDePuntosDeVenta
{
    public static async Task<PointOfSaleDto> UnoAsync(IApplicationDbContext db, PointOfSale punto, bool detalle, CancellationToken ct)
    {
        var dto = (await VariosAsync(db, [punto], ct))[0];
        if (!detalle || punto.Id == 0) return dto;
        var cajas = await db.CashRegisters.AsNoTracking().Where(c => c.PointOfSaleId == punto.Id && !c.IsDeleted).OrderBy(c => c.Code).ToListAsync(ct);
        return dto with { CashRegisters = await CajasAsync(db, punto, cajas, ct) };
    }

    public static async Task<IReadOnlyList<PointOfSaleDto>> VariosAsync(IApplicationDbContext db, IReadOnlyList<PointOfSale> puntos, CancellationToken ct)
    {
        var sucursalesIds = puntos.Select(p => p.BranchId).Distinct().ToList();
        var canalesIds = puntos.Select(p => p.SalesChannelId).Distinct().ToList();
        var bodegasIds = puntos.Select(p => p.DefaultWarehouseId).Distinct().ToList();
        var puntosIds = puntos.Select(p => p.Id).ToList();
        var sucursales = await db.Branches.AsNoTracking().Where(b => sucursalesIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        var canales = await db.SalesChannels.AsNoTracking().Where(c => canalesIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.PublicId, ct);
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegasIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.PublicId, ct);
        var cajas = await db.CashRegisters.AsNoTracking().Where(c => puntosIds.Contains(c.PointOfSaleId) && !c.IsDeleted)
            .GroupBy(c => c.PointOfSaleId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return puntos.Select(p => new PointOfSaleDto(
            p.PublicId, p.Code, p.Name, sucursales.GetValueOrDefault(p.BranchId), canales.GetValueOrDefault(p.SalesChannelId), p.PosEnabled,
            bodegas.GetValueOrDefault(p.DefaultWarehouseId), p.Address, p.IsActive, cajas.GetValueOrDefault(p.Id))).ToList();
    }

    public static async Task<IReadOnlyList<CashRegisterDto>> CajasAsync(IApplicationDbContext db, PointOfSale punto, IReadOnlyList<CashRegister> cajas, CancellationToken ct)
    {
        var cajasIds = cajas.Select(c => c.Id).ToList();
        var bodegasIds = cajas.Select(c => c.WarehouseId).Distinct().ToList();
        var datafonosIds = cajas.Where(c => c.DefaultCardTerminalId is not null).Select(c => c.DefaultCardTerminalId!.Value).Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegasIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => (w.PublicId, w.Code), ct);
        var datafonos = await db.CardTerminals.AsNoTracking().Where(t => datafonosIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.PublicId, ct);
        var tipos = await db.CashRegisterDocumentTypes.AsNoTracking().Include(t => t.DocumentType)
            .Where(t => cajasIds.Contains(t.CashRegisterId) && !t.IsDeleted).ToListAsync(ct);
        var sesiones = await db.CashSessions.AsNoTracking()
            .Where(s => cajasIds.Contains(s.CashRegisterId) && s.Status == CashSessionStatus.Open && !s.IsDeleted)
            .Select(s => new { s.CashRegisterId, s.PublicId, s.CashierName, s.OpenedAt }).ToListAsync(ct);

        return cajas.Select(c =>
        {
            var bodega = bodegas.GetValueOrDefault(c.WarehouseId);
            var sesion = sesiones.FirstOrDefault(s => s.CashRegisterId == c.Id);
            return new CashRegisterDto(
                c.PublicId, punto.PublicId, c.Code, c.Name, bodega.PublicId, bodega.Code ?? string.Empty,
                c.DefaultCardTerminalId is { } d && datafonos.TryGetValue(d, out var g) ? g : null,
                ReglasDePuntoDeVenta.FormatoDe(c.ReceiptWidthMm), c.DianCashRegisterPlate, c.PrintCopies, c.IsActive,
                tipos.Where(t => t.CashRegisterId == c.Id).OrderBy(t => t.Role)
                    .Select(t => new CashRegisterDocumentTypeDto(t.Role, t.DocumentType!.PublicId, t.DocumentType.Code, t.DocumentType.Class, t.DocumentType.FiscalPrefix))
                    .ToList(),
                sesion is null ? null : new CashRegisterOpenSessionDto(sesion.PublicId, sesion.CashierName, sesion.OpenedAt));
        }).ToList();
    }
}
