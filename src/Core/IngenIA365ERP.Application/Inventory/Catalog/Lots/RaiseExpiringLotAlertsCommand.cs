using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Catalog.Lots;

/// <summary>
/// Levanta <c>Inventario.ProximoAVencer</c> por cada lote con existencia que vence dentro de <c>Informes.DiasProximoAVencer</c> desde
/// <c>HoyLocal</c> —o ya venció—, una por lote y bodega (feature 012, I6, T932; FR-022, FR-026; decisiones-transversales §2.13). <b>Sin
/// ruta</b>: lo corre la tarea programada <see cref="TareaDeLotesProximosAVencer"/> en cada cooperativa (el programador la ejecuta por
/// <c>IEjecutorEnCooperativa</c>, con el actor «Proceso de integración»). La condición es <c>ProximoAVencer:{lotPublicId}:{warehousePublicId}</c>:
/// <c>IAlertas</c> no la repite mientras siga pendiente. Responde cuántas levantó. (nuevo)
/// </summary>
public sealed record RaiseExpiringLotAlertsCommand : IRequest<Result<int>>;

/// <summary>Un comando de proceso sin datos de entrada: no hay nada que validar (Principio VIII lo pide igual). (nuevo)</summary>
public sealed class RaiseExpiringLotAlertsCommandValidator : FluentValidation.AbstractValidator<RaiseExpiringLotAlertsCommand>;

public sealed class RaiseExpiringLotAlertsCommandHandler(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IAlertas alertas,
    IDateTimeService reloj)
    : IRequestHandler<RaiseExpiringLotAlertsCommand, Result<int>>
{
    /// <summary>La condición de la alerta de un lote en una bodega (§2.13).</summary>
    public static string Condicion(Guid lote, Guid bodega) => $"ProximoAVencer:{lote}:{bodega}";

    public async Task<Result<int>> Handle(RaiseExpiringLotAlertsCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.LeerComoAsync<int>(ParametrosDeInventario.Modulo, ParametrosDeInventario.InformesDiasProximoAVencer, hoy, ct: ct);
        if (dias.IsFailure) return Result.Failure<int>(dias.Error);
        var limite = hoy.AddDays(Math.Max(0, dias.Value));

        var porVencer = await db.StockDetails.AsNoTracking()
            .Where(s => s.LotId != null && s.Quantity > 0m)
            .GroupBy(s => new { LotId = s.LotId!.Value, s.WarehouseId })
            .Select(g => new { g.Key.LotId, g.Key.WarehouseId, Cantidad = g.Sum(s => s.Quantity) })
            .Join(db.Lots.AsNoTracking().Where(l => l.ExpiryDate != null && l.ExpiryDate <= limite), x => x.LotId, l => l.Id,
                (x, l) => new { x.WarehouseId, x.Cantidad, Lote = l.PublicId, l.Code, Vence = l.ExpiryDate!.Value, l.ProductId })
            .ToListAsync(ct);
        if (porVencer.Count == 0) return Result.Success(0);

        var productoIds = porVencer.Select(x => x.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Code, p.Name }, ct);
        var bodegaIds = porVencer.Select(x => x.WarehouseId).Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegaIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => new { w.PublicId, w.Code }, ct);

        var levantadas = 0;
        foreach (var x in porVencer.OrderBy(x => x.Vence).ThenBy(x => x.Code, StringComparer.Ordinal))
        {
            if (!bodegas.TryGetValue(x.WarehouseId, out var bodega) || !productos.TryGetValue(x.ProductId, out var producto)) continue;
            var estado = x.Vence < hoy ? $"venció el {x.Vence:dd/MM/yyyy}" : $"vence el {x.Vence:dd/MM/yyyy}";
            var levantada = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.ProximoAVencer,
                $"Lote próximo a vencer: {producto.Code} lote {x.Code}",
                $"El lote {x.Code} de {producto.Code} · {producto.Name} {estado}; quedan {x.Cantidad:0.####} en la bodega {bodega.Code}.",
                "Lot", x.Lote, bodega.PublicId, null, Condicion(x.Lote, bodega.PublicId)), ct);
            if (levantada.IsSuccess) levantadas++;
        }
        return Result.Success(levantadas);
    }
}

/// <summary>
/// La tarea programada <c>inventario.lotes</c> (feature 012, I6, T932): una vez al día corre <see cref="RaiseExpiringLotAlertsCommand"/> en
/// cada cooperativa. Antes de que el despliegue llegue a I6 no corre (no hay lotes). Singleton. (nuevo)
/// </summary>
public sealed class TareaDeLotesProximosAVencer(EntregaDelComercio entrega = CatalogoDeParametros.EntregaVigente) : ITareaProgramada
{
    public const string NombreDeLaTarea = "inventario.lotes";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        entrega >= EntregaDelComercio.I6
        && (ultimaCorrida is null || DateOnly.FromDateTime(ultimaCorrida.Value.DateTime) < DateOnly.FromDateTime(ahoraLocal.DateTime));

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<ISender>().Send(new RaiseExpiringLotAlertsCommand(), ct);
}
