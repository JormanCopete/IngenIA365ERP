using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

// ------------------------------------------------------------------------------------------------ DTOs (nuevos) --

/// <summary>Un grupo contable como lo muestran el historial y el cambio (§3.6.4).</summary>
public sealed record GrupoDelCambioDto(Guid PublicId, string Code, string Name);

/// <summary>Una bodega del cambio: cantidad y valor a la fecha efectiva (el valor, con <c>Inventory.Costs.Read</c>).</summary>
public sealed record BodegaDelCambioDto(Guid WarehousePublicId, string WarehouseCode, decimal Quantity, decimal? Value);

/// <summary><c>POST /products/{id}/accounting-group</c> → 201.</summary>
public sealed record AccountingGroupChangeResultDto(
    Guid ChangePublicId,
    GrupoDelCambioDto From,
    GrupoDelCambioDto To,
    DateOnly EffectiveDate,
    IReadOnlyList<BodegaDelCambioDto> ByWarehouse,
    Guid? MessagePublicId);

/// <summary>Una fila del historial (<c>GET /products/{id}/accounting-group</c>).</summary>
public sealed record AccountingGroupChangeDto(
    Guid ChangePublicId,
    GrupoDelCambioDto From,
    GrupoDelCambioDto To,
    DateOnly EffectiveDate,
    string Reason,
    string? ChangedBy,
    DateTime ChangedAt,
    decimal Quantity,
    decimal? Value,
    Guid? MessagePublicId);

/// <summary>Lo que dejó una reclasificación: la fila, el desglose por bodega y el mensaje (nulo sin existencia).</summary>
public sealed record ReclasificacionHecha(ProductAccountingGroupChange Cambio, IReadOnlyList<FilaDeValorizado> PorBodega, Guid? MessagePublicId);

// ----------------------------------------------------------------------------------------- la regla (nueva) --

/// <summary>
/// El cambio de grupo contable de un producto (feature 012, T288; FR-027; contracts/api.md §3.6.4; data-model §1.10), en un solo
/// sitio para el comando y la plantilla de productos (T228). Sin guardar:
/// <list type="number">
/// <item>el producto maneja existencias (<c>Inventory.Product.NotInventoriable</c>); el grupo destino está activo
/// (<c>Inventory.AccountingGroup.Inactive</c>) y es otro (<c>.AccountingGroupUnchanged</c>);</item>
/// <item>la fecha efectiva (por defecto <c>HoyLocal</c>) no es futura (<c>Inventory.Document.DateInFuture</c>), no cae en un
/// período cerrado (<c>Inventory.Period.Closed</c>) ni antes del inicio del módulo, y no es anterior al último movimiento del
/// producto (<c>Inventory.Product.MovementsAfterEffectiveDate</c>): afecta sólo lo futuro;</item>
/// <item>toma <c>INV_Setup</c> compartido y los <c>INV_CostStates</c> del producto (<see cref="ICerrojoDeInventario"/>) para leer
/// cantidad y valor coherentes por bodega a la fecha (<see cref="ValorizadoALaFecha"/>);</item>
/// <item>actualiza <c>Product.AccountingGroupId</c>, agrega el <see cref="ProductAccountingGroupChange"/> y, con existencia,
/// emite <c>GrupoContableReclasificado</c> (origen <c>Operation</c>, <c>Reclassification</c>, modo de paso general).</item>
/// </list>
/// Necesita una transacción en curso (el cerrojo). (nuevo)
/// </summary>
public sealed class ReclasificacionDeGrupo(
    IApplicationDbContext db,
    ICerrojoDeInventario cerrojo,
    ValorizadoALaFecha valorizado,
    EmisorDeMensajes emisor,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
{
    /// <summary>Las reglas de la fecha y del grupo, sin bloquear ni escribir (la revisión de la plantilla las usa solas).</summary>
    public async Task<Result<DateOnly>> ValidarAsync(Product producto, AccountingGroup destino, DateOnly? efectiva, CancellationToken ct)
    {
        if (!producto.EsInventariable) return Result.Failure<DateOnly>(CatalogErrors.ProductNotInventoriable(producto.Code));
        if (!destino.IsActive) return Result.Failure<DateOnly>(CatalogErrors.AccountingGroupInactive(destino.Code));
        if (producto.AccountingGroupId == destino.Id) return Result.Failure<DateOnly>(CatalogErrors.ProductAccountingGroupUnchanged(destino.Code));

        var hoy = reloj.HoyLocal;
        var fecha = efectiva ?? hoy;
        if (fecha > hoy) return Result.Failure<DateOnly>(InventoryErrors.DateInFuture(fecha, hoy));
        var corte = await db.InventorySetups.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.StartDate, s.LastClosedDate }).FirstOrDefaultAsync(ct);
        if (corte?.LastClosedDate is { } cerrado && fecha <= cerrado) return Result.Failure<DateOnly>(InventoryErrors.PeriodClosed(fecha.Year, fecha.Month, cerrado));
        if (corte is not null && fecha < corte.StartDate) return Result.Failure<DateOnly>(InventoryErrors.DateBeforeCutoff(corte.StartDate));

        var ultimo = await db.KardexEntries.AsNoTracking().Where(k => k.ProductId == producto.Id)
            .MaxAsync(k => (DateOnly?)k.OperationDate, ct);
        if (ultimo is { } u && u > fecha) return Result.Failure<DateOnly>(CatalogErrors.ProductMovementsAfterEffectiveDate(u));
        return Result.Success(fecha);
    }

    /// <summary>Valida, bloquea, reclasifica y emite. <paramref name="producto"/> va seguido por el contexto.</summary>
    public async Task<Result<ReclasificacionHecha>> AplicarAsync(Product producto, AccountingGroup destino, DateOnly? efectiva, string motivo, CancellationToken ct)
    {
        var validada = await ValidarAsync(producto, destino, efectiva, ct);
        if (validada.IsFailure) return Result.Failure<ReclasificacionHecha>(validada.Error);
        var fecha = validada.Value;

        var estados = await db.CostStates.AsNoTracking().Where(c => c.ProductId == producto.Id)
            .Select(c => new ClaveDeEstadoDeCosto(c.ProductId, c.ScopeWarehouseId, c.Method)).ToListAsync(ct);
        await cerrojo.BloquearAsync(new PedidoDeCerrojo { Setup = ModoDeBloqueoDelSetup.Compartido, EstadosDeCosto = estados }, ct);

        var porBodega = (await valorizado.CalcularAsync(fecha, [producto.Id], ct)).Where(f => f.Quantity != 0m || f.Value != 0m).ToList();
        var bodegas = await BodegasAsync(porBodega.Select(f => f.WarehouseId).ToList(), ct);
        var origen = producto.AccountingGroupId
            ?? throw new InvalidOperationException($"El producto {producto.Code} no tiene grupo contable que reclasificar.");
        var desde = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().FirstAsync(g => g.Id == origen, ct);

        var cambio = new ProductAccountingGroupChange
        {
            ProductId = producto.Id,
            FromAccountingGroupId = origen,
            ToAccountingGroupId = destino.Id,
            EffectiveDate = fecha,
            Quantity = porBodega.Sum(f => f.Quantity),
            Value = porBodega.Sum(f => f.Value),
            DetailJson = JsonSerializer.Serialize(porBodega.Select(f => new
            {
                warehouseId = f.WarehouseId, warehouseCode = bodegas[f.WarehouseId].Code, quantity = f.Quantity, value = f.Value,
            }), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Reason = motivo.Trim(),
        };
        db.ProductAccountingGroupChanges.Add(cambio);
        producto.AccountingGroupId = destino.Id;

        Guid? mensaje = null;
        if (porBodega.Any(f => f.Quantity != 0m))
        {
            var contenido = new GrupoContableReclasificadoV1
            {
                ProductPublicId = producto.PublicId,
                ProductCode = producto.Code,
                FromAccountingGroupCode = desde.Code,
                ToAccountingGroupCode = destino.Code,
                Reason = cambio.Reason,
                Lines = porBodega.Select(f => new ReclassificationLineV1
                {
                    WarehouseCode = bodegas[f.WarehouseId].Code,
                    WarehouseBehavior = bodegas[f.WarehouseId].Behavior,
                    BranchPublicId = bodegas[f.WarehouseId].Sucursal,
                    QuantityBase = f.Quantity,
                    Value = f.Value,
                }).OrderBy(l => l.WarehouseCode, StringComparer.Ordinal).ToList(),
            };
            var emitidos = await emisor.EmitirAsync(new SolicitudDeEmision(
                new OrigenDeEmision(MessageOriginKind.Operation, cambio.PublicId, null, null, producto.Code, fecha,
                    await OperacionesDeInventario.SucursalPrincipalAsync(db, ct)),
                ClavesDeEvento.Reclasificacion, [contenido],
                await OperacionesDeInventario.ModoGeneralAsync(parametros, GrupoContableReclasificadoV1.Type, fecha, ct)), ct);
            mensaje = emitidos[0].PublicId;
        }
        return Result.Success(new ReclasificacionHecha(cambio, porBodega, mensaje));
    }

    /// <summary>Código, comportamiento y sucursal de cada bodega (lo que viaja en el mensaje).</summary>
    public async Task<IReadOnlyDictionary<int, (Guid PublicId, string Code, WarehouseBehavior Behavior, Guid Sucursal)>> BodegasAsync(
        IReadOnlyCollection<int> ids, CancellationToken ct) =>
        ids.Count == 0
            ? new Dictionary<int, (Guid, string, WarehouseBehavior, Guid)>()
            : await (from w in db.Warehouses.AsNoTracking().IgnoreQueryFilters()
                     join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on w.BranchId equals b.Id
                     where ids.Contains(w.Id)
                     select new { w.Id, w.PublicId, w.Code, w.Behavior, Sucursal = b.PublicId })
                .ToDictionaryAsync(w => w.Id, w => (w.PublicId, w.Code, w.Behavior, w.Sucursal), ct);
}

// ------------------------------------------------------------------------------------------------- el comando --

/// <summary>
/// <c>POST /api/inventory/products/{id}/accounting-group</c> (feature 012, T288; FR-027; contracts/api.md §3.6.4), permiso
/// <c>Inventory.Catalog.ReclassifyAccountingGroup</c>, motivo e <c>Idempotency-Key</c>: la regla de
/// <see cref="ReclasificacionDeGrupo"/> en una transacción y un guardado. (nuevo)
/// </summary>
public sealed record ChangeProductAccountingGroupCommand(Guid ProductPublicId, Guid AccountingGroupPublicId, DateOnly? EffectiveDate, string Reason)
    : IRequest<Result<AccountingGroupChangeResultDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ChangeProductAccountingGroupCommandValidator : ValidadorConMotivo<ChangeProductAccountingGroupCommand>
{
    public ChangeProductAccountingGroupCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.AccountingGroupPublicId).NotEmpty();
    }
}

public sealed class ChangeProductAccountingGroupCommandHandler(
    IApplicationDbContext db,
    ReclasificacionDeGrupo reclasificacion,
    IPermissionChecker permisos)
    : IRequestHandler<ChangeProductAccountingGroupCommand, Result<AccountingGroupChangeResultDto>>
{
    public const string PermisoDeCostos = "Inventory.Costs.Read";

    public Task<Result<AccountingGroupChangeResultDto>> Handle(ChangeProductAccountingGroupCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => CambiarAsync(request, ct), ct);

    private async Task<Result<AccountingGroupChangeResultDto>> CambiarAsync(ChangeProductAccountingGroupCommand request, CancellationToken ct)
    {
        var producto = await db.Products.FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Result.Failure<AccountingGroupChangeResultDto>(CatalogErrors.ProductNotFound());
        var destino = await db.AccountingGroups.FirstOrDefaultAsync(g => g.PublicId == request.AccountingGroupPublicId, ct);
        if (destino is null) return Result.Failure<AccountingGroupChangeResultDto>(CatalogErrors.AccountingGroupNotFound());
        var origenId = producto.AccountingGroupId;

        var hecha = await reclasificacion.AplicarAsync(producto, destino, request.EffectiveDate, request.Reason, ct);
        if (hecha.IsFailure) return Result.Failure<AccountingGroupChangeResultDto>(hecha.Error);
        await db.SaveChangesAsync(ct);

        var desde = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().FirstAsync(g => g.Id == origenId, ct);
        var bodegas = await reclasificacion.BodegasAsync(hecha.Value.PorBodega.Select(f => f.WarehouseId).ToList(), ct);
        var conCostos = await permisos.HasPermissionAsync(PermisoDeCostos, ct);
        var cambio = hecha.Value.Cambio;
        return Result.Success(new AccountingGroupChangeResultDto(
            cambio.PublicId,
            new GrupoDelCambioDto(desde.PublicId, desde.Code, desde.Name),
            new GrupoDelCambioDto(destino.PublicId, destino.Code, destino.Name),
            cambio.EffectiveDate,
            hecha.Value.PorBodega.Select(f => new BodegaDelCambioDto(bodegas[f.WarehouseId].PublicId, bodegas[f.WarehouseId].Code, f.Quantity,
                conCostos ? f.Value : null)).OrderBy(b => b.WarehouseCode, StringComparer.Ordinal).ToList(),
            hecha.Value.MessagePublicId));
    }
}

// ------------------------------------------------------------------------------------------------- el historial --

/// <summary>
/// <c>GET /api/inventory/products/{id}/accounting-group</c> (feature 012, T288; §3.6.4), <c>Inventory.Catalog.View</c>: el
/// historial del grupo contable, el más reciente primero; <c>value</c> sólo con <c>Inventory.Costs.Read</c>. (nuevo)
/// </summary>
public sealed record GetProductAccountingGroupHistoryQuery(Guid ProductPublicId) : IRequest<Result<IReadOnlyList<AccountingGroupChangeDto>>>;

public sealed class GetProductAccountingGroupHistoryQueryValidator : AbstractValidator<GetProductAccountingGroupHistoryQuery>
{
    public GetProductAccountingGroupHistoryQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class GetProductAccountingGroupHistoryQueryHandler(IApplicationDbContext db, IPermissionChecker permisos)
    : IRequestHandler<GetProductAccountingGroupHistoryQuery, Result<IReadOnlyList<AccountingGroupChangeDto>>>
{
    public async Task<Result<IReadOnlyList<AccountingGroupChangeDto>>> Handle(GetProductAccountingGroupHistoryQuery request, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().Where(p => p.PublicId == request.ProductPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (producto is null) return Result.Failure<IReadOnlyList<AccountingGroupChangeDto>>(CatalogErrors.ProductNotFound());

        var cambios = await db.ProductAccountingGroupChanges.AsNoTracking().Where(c => c.ProductId == producto)
            .OrderByDescending(c => c.EffectiveDate).ThenByDescending(c => c.Id).ToListAsync(ct);
        var grupoIds = cambios.SelectMany(c => new[] { c.FromAccountingGroupId, c.ToAccountingGroupId }).Distinct().ToList();
        var grupos = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(g => grupoIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => new GrupoDelCambioDto(g.PublicId, g.Code, g.Name), ct);
        var origenes = cambios.Select(c => c.PublicId).ToList();
        var mensajes = await db.IntegrationMessages.AsNoTracking()
            .Where(m => origenes.Contains(m.OriginPublicId) && m.Type == GrupoContableReclasificadoV1.Type)
            .Select(m => new { m.OriginPublicId, m.PublicId }).ToListAsync(ct);
        var conCostos = await permisos.HasPermissionAsync(ChangeProductAccountingGroupCommandHandler.PermisoDeCostos, ct);

        return Result.Success<IReadOnlyList<AccountingGroupChangeDto>>(cambios.Select(c => new AccountingGroupChangeDto(
            c.PublicId, grupos[c.FromAccountingGroupId], grupos[c.ToAccountingGroupId], c.EffectiveDate, c.Reason, c.CreatedBy, c.CreatedAt,
            c.Quantity, conCostos ? c.Value : null,
            mensajes.FirstOrDefault(m => m.OriginPublicId == c.PublicId)?.PublicId)).ToList());
    }
}
