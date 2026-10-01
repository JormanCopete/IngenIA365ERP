using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Inventory.Replenishment;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Replenishment;

/// <summary>
/// Una política de reorden evaluada (feature 012, US17, T953–T956): el producto y la bodega con sus códigos, la posición que
/// leyó <see cref="PosicionDeReposicion"/>, la política y lo que dijo <see cref="CalculoDeReposicion"/>. (nuevo)
/// </summary>
public sealed record FilaDeReposicion(
    int ProductId,
    Guid ProductPublicId,
    string ProductCode,
    string ProductName,
    int WarehouseId,
    Guid WarehousePublicId,
    string WarehouseCode,
    Posicion Posicion,
    decimal Minimo,
    decimal PuntoDeReorden,
    decimal Maximo,
    ResultadoDeReposicion Resultado);

/// <summary>
/// Evalúa políticas de <c>INV_ReorderPolicies</c> (feature 012, US17; FR-035): les pone la posición de
/// <see cref="PosicionDeReposicion"/> (el único lector de disponible, en tránsito y por recibir) y las pasa por
/// <see cref="CalculoDeReposicion"/>. La comparten el aviso al confirmar (T953), la revisión nocturna (T954) y la vista
/// <c>reorder-alerts</c> (T956), así los tres dicen lo mismo de la misma pareja. No aplica alcance: la consulta de políticas
/// que recibe ya viene filtrada por quien la llama. (nuevo)
/// </summary>
public sealed class EvaluacionDeReposicion(IApplicationDbContext db, PosicionDeReposicion posiciones)
{
    /// <summary>Las políticas vivas de <paramref name="politicas"/> evaluadas, en el orden de bodega y producto.</summary>
    public async Task<IReadOnlyList<FilaDeReposicion>> EvaluarAsync(IQueryable<ReorderPolicy> politicas, CancellationToken ct)
    {
        var filas = await (
                from r in politicas
                join p in db.Products.AsNoTracking() on r.ProductId equals p.Id
                join w in db.Warehouses.AsNoTracking() on r.WarehouseId equals w.Id
                orderby w.Code, p.Code
                select new
                {
                    r.ProductId, ProductoPublico = p.PublicId, ProductoCodigo = p.Code, ProductoNombre = p.Name,
                    r.WarehouseId, BodegaPublica = w.PublicId, BodegaCodigo = w.Code,
                    r.MinimumQuantity, r.ReorderPoint, r.MaximumQuantity,
                })
            .ToListAsync(ct);
        if (filas.Count == 0) return [];

        var posicion = await posiciones.LeerAsync(filas.Select(f => (f.ProductId, f.WarehouseId)).ToList(), ct);
        return filas.Select(f =>
        {
            var p = posicion.GetValueOrDefault((f.ProductId, f.WarehouseId)) ?? Posicion.Cero;
            var resultado = CalculoDeReposicion.Calcular(new EntradaDeReposicion(
                p.Disponible, p.EnTransito, p.PorRecibir, f.MinimumQuantity, f.MaximumQuantity, f.ReorderPoint));
            return new FilaDeReposicion(f.ProductId, f.ProductoPublico, f.ProductoCodigo, f.ProductoNombre,
                f.WarehouseId, f.BodegaPublica, f.BodegaCodigo, p, f.MinimumQuantity, f.ReorderPoint, f.MaximumQuantity, resultado);
        }).ToList();
    }

    /// <summary>Las políticas vivas de estas parejas (producto, bodega), evaluadas.</summary>
    public async Task<IReadOnlyList<FilaDeReposicion>> EvaluarAsync(IReadOnlyCollection<(int ProductId, int WarehouseId)> parejas, CancellationToken ct)
    {
        if (parejas.Count == 0) return [];
        var productos = parejas.Select(p => p.ProductId).Distinct().ToList();
        var bodegas = parejas.Select(p => p.WarehouseId).Distinct().ToList();
        var pedidas = parejas.ToHashSet();
        var evaluadas = await EvaluarAsync(
            db.ReorderPolicies.AsNoTracking().Where(r => productos.Contains(r.ProductId) && bodegas.Contains(r.WarehouseId)), ct);
        return evaluadas.Where(f => pedidas.Contains((f.ProductId, f.WarehouseId))).ToList();
    }
}

/// <summary>
/// Las alertas de reposición de una pareja (feature 012, US17; FR-035, FR-022; decisiones-transversales §2.13):
/// <c>Inventario.Reorden</c> si la posición quedó igual o menor que el punto e <c>Inventario.Quiebre</c> si el disponible quedó bajo
/// el mínimo, una pendiente por producto y bodega (<see cref="ClaveDeLaAlerta"/>) y con la bodega como alcance, así la reciben
/// sólo quienes la tienen. La usan el aviso al confirmar y la revisión nocturna: los dos levantan la misma condición y la
/// segunda vez sólo suma la ocurrencia. (nuevo)
/// </summary>
public static class AlertasDeReposicion
{
    /// <summary>La condición: <c>{TypeCode}:{producto}:{bodega}</c> por PublicId.</summary>
    public static string ClaveDeLaAlerta(string tipo, Guid productoPublicId, Guid bodegaPublicId) =>
        $"{tipo}:{productoPublicId:D}:{bodegaPublicId:D}";

    /// <summary>Las alertas que merece la fila (ninguna si ni pide reorden ni está en quiebre).</summary>
    public static IReadOnlyList<AlertaALevantar> De(FilaDeReposicion f)
    {
        var alertas = new List<AlertaALevantar>(2);
        if (f.Resultado.RequiereReorden)
        {
            alertas.Add(new AlertaALevantar(
                TiposDeAlerta.Reorden,
                $"Reorden de {f.ProductCode} en {f.WarehouseCode}",
                string.Format(CultureInfo.InvariantCulture,
                    "La posición de {0} · {1} en la bodega {2} quedó en {3} (disponible {4}, en tránsito {5}, por recibir {6}), igual o menor que su punto de reorden {7}. Sugerido para llegar al máximo {8}: {9}.",
                    f.ProductCode, f.ProductName, f.WarehouseCode, N(f.Resultado.Posicion), N(f.Posicion.Disponible), N(f.Posicion.EnTransito),
                    N(f.Posicion.PorRecibir), N(f.PuntoDeReorden), N(f.Maximo), N(f.Resultado.Sugerido)),
                "Product", f.ProductPublicId, f.WarehousePublicId,
                DedupKey: ClaveDeLaAlerta(TiposDeAlerta.Reorden, f.ProductPublicId, f.WarehousePublicId)));
        }
        if (f.Resultado.Quiebre)
        {
            alertas.Add(new AlertaALevantar(
                TiposDeAlerta.Quiebre,
                $"Quiebre de {f.ProductCode} en {f.WarehouseCode}",
                string.Format(CultureInfo.InvariantCulture,
                    "El disponible de {0} · {1} en la bodega {2} quedó en {3}, por debajo de su mínimo {4}. Pida o traslade reposición.",
                    f.ProductCode, f.ProductName, f.WarehouseCode, N(f.Posicion.Disponible), N(f.Minimo)),
                "Product", f.ProductPublicId, f.WarehousePublicId,
                DedupKey: ClaveDeLaAlerta(TiposDeAlerta.Quiebre, f.ProductPublicId, f.WarehousePublicId)));
        }
        return alertas;
    }

    private static string N(decimal valor) => valor.ToString("0.####", CultureInfo.InvariantCulture);
}
