using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Kardex;

/// <summary>
/// Traduce los filtros de verificar y reconstruir (<c>productPublicIds</c>, <c>warehousePublicIds</c>) a Ids y los acota al
/// alcance: una bodega inexistente o fuera de él es 404 <c>Inventory.Warehouse.NotFound</c>, un producto inexistente 404
/// <c>Inventory.Product.NotFound</c>; sin bodegas y sin alcance total, las del alcance (feature 012, T258). (nuevo)
/// </summary>
public static class FiltrosDeIntegridad
{
    public static async Task<Result<AlcanceDeVerificacion>> ResolverAsync(
        IApplicationDbContext db, AlcanceDeInventario alcance, IReadOnlyList<Guid>? productPublicIds, IReadOnlyList<Guid>? warehousePublicIds, CancellationToken ct)
    {
        List<int>? productos = null;
        if (productPublicIds is { Count: > 0 })
        {
            var pedidos = productPublicIds.Distinct().ToList();
            productos = await db.Products.AsNoTracking().Where(p => pedidos.Contains(p.PublicId)).Select(p => p.Id).ToListAsync(ct);
            if (productos.Count != pedidos.Count) return Result.Failure<AlcanceDeVerificacion>(ErroresDelDocumento.ProductoInexistente());
        }

        List<int>? bodegas = null;
        if (warehousePublicIds is { Count: > 0 })
        {
            var pedidas = warehousePublicIds.Distinct().ToList();
            bodegas = await db.Warehouses.AsNoTracking().Where(w => pedidas.Contains(w.PublicId)).Select(w => w.Id).ToListAsync(ct);
            if (bodegas.Count != pedidas.Count || !bodegas.All(alcance.IncluyeBodega))
                return Result.Failure<AlcanceDeVerificacion>(ErroresDeAlcance.BodegaInexistente());
        }
        else if (!alcance.TodasLasBodegas)
        {
            bodegas = alcance.Bodegas.ToList();
        }

        return Result.Success(new AlcanceDeVerificacion(productos, bodegas));
    }
}

/// <summary>
/// <c>POST /api/inventory/integrity/verify</c> (feature 012, T258; contracts/api.md §6.2; FR-003): compara el kardex con las
/// proyecciones en lo pedido (vacío = todo lo del alcance). Es consulta: sin clave de idempotencia. Con diferencias levanta
/// <c>Inventario.IncidenteDeIntegridad</c> —una pendiente por alcance verificado (<c>DedupKey</c>)— y deja el evento
/// <c>Inventory.Integrity.Verified</c>. La tarea nocturna <c>inventario.integridad</c> corre la misma consulta sobre todo. (nuevo)
/// </summary>
public sealed record VerifyInventoryIntegrityQuery(IReadOnlyList<Guid>? ProductPublicIds = null, IReadOnlyList<Guid>? WarehousePublicIds = null)
    : IRequest<Result<IntegrityReportDto>>;

public sealed class VerifyInventoryIntegrityQueryValidator : AbstractValidator<VerifyInventoryIntegrityQuery>
{
    public VerifyInventoryIntegrityQueryValidator()
    {
        RuleFor(x => x.ProductPublicIds).Must(p => p is null || p.Count <= 1000).WithMessage("Se verifican hasta 1.000 productos por vez.");
        RuleFor(x => x.WarehousePublicIds).Must(w => w is null || w.Count <= 200).WithMessage("Se verifican hasta 200 bodegas por vez.");
    }
}

/// <summary>Aplica <see cref="IAlcanceDeInventario"/> a los filtros; nunca escribe el kardex ni las proyecciones.</summary>
public sealed class VerifyInventoryIntegrityQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    VerificacionDeIntegridad verificacion,
    IAlertas alertas,
    InventoryAuditEmitter auditoria,
    IDateTimeService reloj)
    : IRequestHandler<VerifyInventoryIntegrityQuery, Result<IntegrityReportDto>>
{
    public async Task<Result<IntegrityReportDto>> Handle(VerifyInventoryIntegrityQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var filtros = await FiltrosDeIntegridad.ResolverAsync(db, alcance, request.ProductPublicIds, request.WarehousePublicIds, ct);
        if (filtros.IsFailure) return Result.Failure<IntegrityReportDto>(filtros.Error);

        var resultado = await verificacion.VerificarAsync(filtros.Value, ct);
        var incidentes = await IncidentesAsync(resultado.Incidentes, ct);

        Guid? alerta = null;
        if (incidentes.Count > 0)
        {
            var levantada = await alertas.LevantarAsync(new AlertaALevantar(
                TiposDeAlerta.IncidenteDeIntegridad,
                "Diferencia entre el kardex y las existencias",
                $"La verificación encontró {incidentes.Count} diferencia(s) entre el kardex y las proyecciones de existencias y costo. " +
                "Revíselas en Inventario › Integridad y reconstruya con motivo.",
                "InventoryIntegrity",
                DedupKey: ClaveDelAlcance(request)), ct);
            if (levantada.IsSuccess) alerta = levantada.Value.AlertPublicId;
        }

        var informe = new IntegrityReportDto(reloj.UtcNow,
            new IntegrityCheckedDto(resultado.StockBalances, resultado.StockDetails, resultado.CostStates, 0), incidentes, alerta);
        await auditoria.EmitAsync(AuditEventTypes.InventoryIntegrityVerified, "InventoryIntegrity", null, null,
            new { productos = request.ProductPublicIds, bodegas = request.WarehousePublicIds, informe.Checked, incidentes = incidentes.Count, alerta }, ct);
        return Result.Success(informe);
    }

    /// <summary>Una alerta pendiente por alcance verificado: la misma verificación repetida suma ocurrencias.</summary>
    public static string ClaveDelAlcance(VerifyInventoryIntegrityQuery request)
    {
        var productos = request.ProductPublicIds?.Distinct().Order().Select(p => p.ToString("N")) ?? [];
        var bodegas = request.WarehousePublicIds?.Distinct().Order().Select(b => b.ToString("N")) ?? [];
        var alcance = $"p={string.Join(',', productos)};b={string.Join(',', bodegas)}";
        var huella = alcance == "p=;b=" ? "todo" : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(alcance)))[..32];
        return $"{TiposDeAlerta.IncidenteDeIntegridad}:{huella}";
    }

    private async Task<List<IntegrityIncidentDto>> IncidentesAsync(IReadOnlyList<IncidenteDeKardex> incidentes, CancellationToken ct)
    {
        if (incidentes.Count == 0) return [];
        var productoIds = incidentes.Select(i => i.ProductId).Distinct().ToList();
        var bodegaIds = incidentes.Where(i => i.WarehouseId is not null).Select(i => i.WarehouseId!.Value).Distinct().ToList();
        var ubicacionIds = incidentes.Where(i => i.LocationId is not null).Select(i => i.LocationId!.Value).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new IntegrityRefDto(p.PublicId, p.Code), ct);
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegaIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => new IntegrityRefDto(w.PublicId, w.Code), ct);
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().Where(l => ubicacionIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => new IntegrityRefDto(l.PublicId, l.Code), ct);

        return incidentes
            .Select(i => new IntegrityIncidentDto(i.Kind,
                productos.GetValueOrDefault(i.ProductId) ?? new IntegrityRefDto(Guid.Empty, string.Empty),
                i.WarehouseId is int w ? bodegas.GetValueOrDefault(w) : null,
                i.LocationId is int l ? ubicaciones.GetValueOrDefault(l) : null,
                i.LotId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                i.Field, i.Expected, i.Actual, i.Difference))
            .OrderBy(i => i.Product.Code, StringComparer.Ordinal).ThenBy(i => i.Kind, StringComparer.Ordinal).ThenBy(i => i.Warehouse?.Code, StringComparer.Ordinal)
            .ToList();
    }
}
