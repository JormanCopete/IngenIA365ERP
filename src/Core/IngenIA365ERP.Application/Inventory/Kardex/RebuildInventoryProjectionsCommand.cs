using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Kardex;

/// <summary>
/// <c>POST /api/inventory/integrity/rebuild</c> (feature 012, T258; contracts/api.md §6.2; FR-003; data-model §3.7): recalcula
/// <c>INV_StockBalances</c>, <c>INV_StockDetails</c> e <c>INV_CostStates</c> desde el kardex, con motivo y
/// <c>Inventory.Integrity.Rebuild</c>. <b>Nunca toca el kardex</b>: no inserta, modifica ni borra una <c>KardexEntry</c>. Es, con
/// <c>RegistroDeKardex</c>, el único que escribe las proyecciones (<c>NadieEscribeElKardexFueraDelRegistro</c>). (nuevo)
///
/// <para>
/// Va producto por producto: toma el cerrojo de las filas de ese producto (sus confirmaciones esperan), recalcula, crea lo que
/// falta, pone en cero lo que el kardex no respalda y guarda antes de pasar al siguiente. Como toda operación de pantalla lleva
/// clave de idempotencia (<c>LosComandosDeInventarioLlevanClave</c>), <c>IdempotencyBehavior</c> la envuelve en una transacción:
/// los guardados por producto quedan dentro de ella y se confirman juntos al final; el cerrojo de cada producto se toma al llegar
/// a él. Sin filtros puede tardar minutos: la pantalla espera con su indicador.
/// </para>
/// </summary>
public sealed record RebuildInventoryProjectionsCommand(IReadOnlyList<Guid>? ProductPublicIds, IReadOnlyList<Guid>? WarehousePublicIds, string Reason)
    : IRequest<Result<RebuildResultDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RebuildInventoryProjectionsCommandValidator : ValidadorConMotivo<RebuildInventoryProjectionsCommand>
{
    public RebuildInventoryProjectionsCommandValidator()
    {
        RuleFor(x => x.ProductPublicIds).Must(p => p is null || p.Count <= 1000).WithMessage("Se reconstruyen hasta 1.000 productos por vez.");
        RuleFor(x => x.WarehousePublicIds).Must(w => w is null || w.Count <= 200).WithMessage("Se reconstruyen hasta 200 bodegas por vez.");
    }
}

public sealed class RebuildInventoryProjectionsCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    ICerrojoDeInventario cerrojo,
    IDateTimeService reloj)
    : IRequestHandler<RebuildInventoryProjectionsCommand, Result<RebuildResultDto>>
{
    public Task<Result<RebuildResultDto>> Handle(RebuildInventoryProjectionsCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => ReconstruirAsync(request, ct), ct);

    private async Task<Result<RebuildResultDto>> ReconstruirAsync(RebuildInventoryProjectionsCommand request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var filtros = await FiltrosDeIntegridad.ResolverAsync(db, alcance, request.ProductPublicIds, request.WarehousePublicIds, ct);
        if (filtros.IsFailure) return Result.Failure<RebuildResultDto>(filtros.Error);
        var (productosPedidos, bodegas) = (filtros.Value.Productos, filtros.Value.Bodegas);

        var candidatos = db.KardexEntries.AsNoTracking().Select(k => new { k.ProductId, k.WarehouseId })
            .Concat(db.StockBalances.AsNoTracking().Select(s => new { s.ProductId, s.WarehouseId }))
            .Concat(db.StockDetails.AsNoTracking().Select(s => new { s.ProductId, s.WarehouseId }));
        if (productosPedidos is not null) candidatos = candidatos.Where(c => productosPedidos.Contains(c.ProductId));
        if (bodegas is not null) candidatos = candidatos.Where(c => bodegas.Contains(c.WarehouseId));
        var productos = (await candidatos.Select(c => c.ProductId).Distinct().ToListAsync(ct)).Order().ToList();

        var correcciones = new List<(string Kind, int ProductId, int? WarehouseId, string Field, decimal Before, decimal After)>();
        int existencias = 0, detalles = 0, costos = 0;
        foreach (var producto in productos)
        {
            var (e, d, c) = await ReconstruirProductoAsync(producto, bodegas, correcciones, ct);
            existencias += e;
            detalles += d;
            costos += c;
        }

        return Result.Success(new RebuildResultDto(reloj.UtcNow, new RebuildRowsDto(existencias, detalles, costos, 0),
            await CorreccionesAsync(correcciones, ct)));
    }

    /// <summary>Un producto: cerrojo de sus filas, recálculo desde el kardex y un guardado.</summary>
    private async Task<(int Existencias, int Detalles, int Costos)> ReconstruirProductoAsync(
        int producto, IReadOnlyCollection<int>? bodegas, List<(string, int, int?, string, decimal, decimal)> correcciones, CancellationToken ct)
    {
        var kardex = await db.KardexEntries.AsNoTracking().Where(k => k.ProductId == producto)
            .OrderBy(k => k.OperationDate).ThenBy(k => k.Id).ToListAsync(ct);
        var deBodegas = bodegas is null ? kardex : kardex.Where(k => bodegas.Contains(k.WarehouseId)).ToList();
        var ambitos = bodegas is null
            ? kardex.Select(k => k.CostScopeWarehouseId).Distinct().ToList()
            : kardex.Select(k => k.CostScopeWarehouseId).Where(a => a == 0 || bodegas.Contains(a)).Distinct().ToList();

        var existentes = await db.StockBalances.Where(s => s.ProductId == producto && (bodegas == null || bodegas.Contains(s.WarehouseId))).ToListAsync(ct);
        var detallesExistentes = await db.StockDetails.Where(s => s.ProductId == producto && (bodegas == null || bodegas.Contains(s.WarehouseId))).ToListAsync(ct);
        var costosExistentes = await db.CostStates.Where(c => c.ProductId == producto && (bodegas == null || c.ScopeWarehouseId == 0 || bodegas.Contains(c.ScopeWarehouseId))).ToListAsync(ct);

        var claves = deBodegas.Select(k => (k.WarehouseId, k.LocationId, k.LotId)).Distinct().ToList();
        await cerrojo.BloquearAsync(new PedidoDeCerrojo
        {
            EstadosDeCosto = ambitos.Select(a => new ClaveDeEstadoDeCosto(producto, a, kardex.LastOrDefault(k => k.CostScopeWarehouseId == a)?.CostMethod ?? CostMethod.WeightedAverage))
                .Concat(costosExistentes.Select(c => new ClaveDeEstadoDeCosto(producto, c.ScopeWarehouseId, c.Method))).ToList(),
            Existencias = claves.Select(c => new ClaveDeExistencia(producto, c.WarehouseId))
                .Concat(existentes.Select(s => new ClaveDeExistencia(producto, s.WarehouseId))).ToList(),
            Detalles = claves.Select(c => new ClaveDeDetalleDeExistencia(producto, c.WarehouseId, c.LocationId, c.LotId))
                .Concat(detallesExistentes.Select(s => new ClaveDeDetalleDeExistencia(producto, s.WarehouseId, s.LocationId, s.LotId))).ToList(),
        }, ct);

        // Las filas que el cerrojo pudo haber creado, ya bloqueadas.
        existentes = await db.StockBalances.Where(s => s.ProductId == producto && (bodegas == null || bodegas.Contains(s.WarehouseId))).ToListAsync(ct);
        detallesExistentes = await db.StockDetails.Where(s => s.ProductId == producto && (bodegas == null || bodegas.Contains(s.WarehouseId))).ToListAsync(ct);
        costosExistentes = await db.CostStates.Where(c => c.ProductId == producto && (bodegas == null || c.ScopeWarehouseId == 0 || bodegas.Contains(c.ScopeWarehouseId))).ToListAsync(ct);

        // ------------------------------------------------------------------------------ existencias --
        var porBodega = deBodegas.GroupBy(k => k.WarehouseId)
            .ToDictionary(g => g.Key, g => (Fisico: g.Sum(k => k.QuantityBase), Ultima: g.Max(k => k.OperationDate)));
        foreach (var (bodega, esperado) in porBodega)
        {
            var fila = existentes.FirstOrDefault(s => s.WarehouseId == bodega);
            if (fila is null)
            {
                fila = new StockBalance { ProductId = producto, WarehouseId = bodega };
                db.StockBalances.Add(fila);
                existentes.Add(fila);
            }
            if (fila.Physical != esperado.Fisico) correcciones.Add((TiposDeIncidente.StockBalance, producto, bodega, "Physical", fila.Physical, esperado.Fisico));
            fila.Physical = esperado.Fisico;
            fila.LastMovementDate = esperado.Ultima;
        }
        foreach (var huerfana in existentes.Where(s => !porBodega.ContainsKey(s.WarehouseId) && s.Physical != 0m))
        {
            correcciones.Add((TiposDeIncidente.StockBalance, producto, huerfana.WarehouseId, "Physical", huerfana.Physical, 0m));
            huerfana.Physical = 0m;
        }

        // ------------------------------------------------------------------------- ubicación y lote --
        var porUbicacion = deBodegas.GroupBy(k => (k.WarehouseId, k.LocationId, k.LotId)).ToDictionary(g => g.Key, g => g.Sum(k => k.QuantityBase));
        foreach (var (clave, cantidad) in porUbicacion)
        {
            var fila = detallesExistentes.FirstOrDefault(s => s.WarehouseId == clave.WarehouseId && s.LocationId == clave.LocationId && s.LotId == clave.LotId);
            if (fila is null)
            {
                fila = new StockDetail { ProductId = producto, WarehouseId = clave.WarehouseId, LocationId = clave.LocationId, LotId = clave.LotId };
                db.StockDetails.Add(fila);
                detallesExistentes.Add(fila);
            }
            if (fila.Quantity != cantidad) correcciones.Add((TiposDeIncidente.StockDetail, producto, clave.WarehouseId, "Quantity", fila.Quantity, cantidad));
            fila.Quantity = cantidad;
        }
        foreach (var huerfana in detallesExistentes.Where(s => !porUbicacion.ContainsKey((s.WarehouseId, s.LocationId, s.LotId)) && s.Quantity != 0m))
        {
            correcciones.Add((TiposDeIncidente.StockDetail, producto, huerfana.WarehouseId, "Quantity", huerfana.Quantity, 0m));
            huerfana.Quantity = 0m;
        }

        // ---------------------------------------------------------------------------- estado de costo --
        foreach (var ambito in ambitos)
        {
            var hechos = kardex.Where(k => k.CostScopeWarehouseId == ambito).ToList();
            var cantidad = hechos.Sum(k => k.QuantityBase);
            var valor = hechos.Sum(k => k.TotalCost);
            var fila = costosExistentes.FirstOrDefault(c => c.ScopeWarehouseId == ambito);
            if (fila is null)
            {
                fila = new CostState { ProductId = producto, ScopeWarehouseId = ambito };
                db.CostStates.Add(fila);
                costosExistentes.Add(fila);
            }
            var bodega = ambito == 0 ? (int?)null : ambito;
            var ultimo = hechos.LastOrDefault(k => k.Kind == KardexEntryKind.Entry)?.UnitCost ?? fila.LastUnitCost;
            if (fila.Quantity != cantidad) correcciones.Add((TiposDeIncidente.CostState, producto, bodega, "Quantity", fila.Quantity, cantidad));
            if (fila.Value != valor) correcciones.Add((TiposDeIncidente.CostState, producto, bodega, "Value", fila.Value, valor));
            fila.Method = hechos.LastOrDefault()?.CostMethod ?? fila.Method;
            fila.Quantity = cantidad;
            fila.Value = valor;
            fila.LastUnitCost = ultimo;
            fila.AverageCost = cantidad > 0m ? Math.Max(0m, Redondeo.CostoUnitario(valor / cantidad)) : ultimo;
        }
        foreach (var huerfana in costosExistentes.Where(c => !ambitos.Contains(c.ScopeWarehouseId) && (c.Quantity != 0m || c.Value != 0m)))
        {
            var bodega = huerfana.ScopeWarehouseId == 0 ? (int?)null : huerfana.ScopeWarehouseId;
            if (huerfana.Quantity != 0m) correcciones.Add((TiposDeIncidente.CostState, producto, bodega, "Quantity", huerfana.Quantity, 0m));
            if (huerfana.Value != 0m) correcciones.Add((TiposDeIncidente.CostState, producto, bodega, "Value", huerfana.Value, 0m));
            huerfana.Quantity = 0m;
            huerfana.Value = 0m;
            huerfana.AverageCost = huerfana.LastUnitCost;
        }

        await db.SaveChangesAsync(ct);
        return (existentes.Count, detallesExistentes.Count, costosExistentes.Count);
    }

    private async Task<IReadOnlyList<RebuildCorrectionDto>> CorreccionesAsync(
        List<(string Kind, int ProductId, int? WarehouseId, string Field, decimal Before, decimal After)> correcciones, CancellationToken ct)
    {
        if (correcciones.Count == 0) return [];
        var productoIds = correcciones.Select(c => c.ProductId).Distinct().ToList();
        var bodegaIds = correcciones.Where(c => c.WarehouseId is not null).Select(c => c.WarehouseId!.Value).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new IntegrityRefDto(p.PublicId, p.Code), ct);
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegaIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => new IntegrityRefDto(w.PublicId, w.Code), ct);
        return correcciones
            .Select(c => new RebuildCorrectionDto(c.Kind, productos.GetValueOrDefault(c.ProductId) ?? new IntegrityRefDto(Guid.Empty, string.Empty),
                c.WarehouseId is int w ? bodegas.GetValueOrDefault(w) : null, c.Field, c.Before, c.After))
            .ToList();
    }
}
