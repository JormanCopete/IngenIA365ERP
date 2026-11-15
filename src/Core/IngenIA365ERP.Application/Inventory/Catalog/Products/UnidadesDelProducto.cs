using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// Las unidades de un producto y sus precios, en un solo sitio (2026-11-15, pedido del dueño tras el 07/11 de COOFLOPAL: la unidad
/// base de 21 aceites pasó de UND a CTO después de cargar sus precios, los precios quedaron en UND y la venta, que busca la base, no
/// los encontraba). Tres reglas:
/// <list type="bullet">
/// <item><c>Productos.VariasUnidades</c>: con <c>NoPermitir</c> (el defecto) un producto no tiene alternas; agregarlas responde
/// <c>Inventory.ProductUnit.NotAllowed</c>;</item>
/// <item><c>Productos.UnidadBasePorDefecto</c>: la unidad que toma el alta (pantalla y plantilla) cuando no se indica otra;</item>
/// <item><b>el precio acompaña a la unidad</b>: cambiar la unidad base (sólo sin movimientos) mueve sus precios a la nueva en todas las
/// listas, y retirar una alterna retira sus precios, en el mismo <c>SaveChanges</c> del cambio. Nunca queda un precio en una unidad
/// que el producto no tiene.</item>
/// </list>
/// No guarda: guarda quien llama. (nuevo)
/// </summary>
public sealed class UnidadesDelProducto(IApplicationDbContext db, ILectorDeParametros lector)
{
    /// <summary>¿La cooperativa permite unidades alternas a <paramref name="fecha"/>?</summary>
    public async Task<bool> PermiteVariasAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ProductosVariasUnidades, fecha, ct: ct);
        return leido.IsSuccess && string.Equals(leido.Value.Texto, ParametrosDeInventario.VariasUnidadesPermitir, StringComparison.Ordinal);
    }

    /// <summary>La unidad base por defecto a <paramref name="fecha"/>; nula si su código no existe o está dada de baja.</summary>
    public async Task<UnitOfMeasure?> UnidadBasePorDefectoAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ProductosUnidadBasePorDefecto, fecha, ct: ct);
        var codigo = leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto.Trim().ToUpperInvariant() : "UND";
        return await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == codigo && !u.IsDeleted, ct);
    }

    /// <summary>Con <c>NoPermitir</c>, el error de agregar alternas; con <c>Permitir</c>, éxito.</summary>
    public async Task<Result> AlternasAdmitidasAsync(DateOnly fecha, CancellationToken ct) =>
        await PermiteVariasAsync(fecha, ct) ? Result.Success() : Result.Failure(CatalogErrors.ProductUnitsNotAllowed());

    /// <summary>
    /// La unidad base de <paramref name="productoId"/> pasó de <paramref name="anterior"/> a <paramref name="nueva"/>: sus precios en
    /// la anterior pasan a la nueva en cada lista. Si la lista ya tenía precio en la nueva, ése queda y el de la anterior se retira.
    /// </summary>
    public static async Task MoverPreciosAsync(IApplicationDbContext db, int productoId, int anterior, int nueva, CancellationToken ct)
    {
        if (anterior == nueva || productoId == 0) return;
        var precios = await db.PriceListItems
            .Where(i => i.ProductId == productoId && !i.IsDeleted && (i.UnitId == anterior || i.UnitId == nueva))
            .ToListAsync(ct);
        foreach (var precio in precios.Where(p => p.UnitId == anterior))
        {
            if (precios.Any(p => p.PriceListId == precio.PriceListId && p.UnitId == nueva && !p.IsDeleted))
            {
                precio.IsDeleted = true;
                HistoriaDePrecios.Registrar(db, precio, PriceChangeKind.Removed, precio.Price, null,
                    "Cambio de unidad base: la lista ya tenía precio en la unidad nueva");
            }
            else
            {
                precio.UnitId = nueva;
                HistoriaDePrecios.Registrar(db, precio, PriceChangeKind.UnitChanged, precio.Price, precio.Price, "Cambio de unidad base del producto", anterior);
            }
        }
    }

    /// <summary>Retira los precios de <paramref name="productoId"/> en la unidad alterna <paramref name="unidadId"/> en todas las listas.</summary>
    public static async Task RetirarPreciosAsync(IApplicationDbContext db, int productoId, int unidadId, CancellationToken ct)
    {
        var precios = await db.PriceListItems.Where(i => i.ProductId == productoId && i.UnitId == unidadId && !i.IsDeleted).ToListAsync(ct);
        foreach (var precio in precios)
        {
            precio.IsDeleted = true;
            HistoriaDePrecios.Registrar(db, precio, PriceChangeKind.Removed, precio.Price, null, "Se retiró la unidad alterna del producto");
        }
    }
}
