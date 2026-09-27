using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// La implementación de <see cref="IDimensionesDeInventario"/> (feature 012, T519; T27, T31; contracts/contabilidad.md §1, §7.1):
/// lo único de Inventario que Contabilidad conoce, además de la bandeja. Contabilidad no lee tablas <c>INV_</c>; pide aquí los
/// códigos vivos de cada dimensión de la matriz y las combinaciones en uso.
/// <list type="bullet">
/// <item><b>Catálogo</b>: grupos contables, bodegas (con su sucursal de Core y su comportamiento), causas de ajuste y tipos de
/// documento activos; desde I3 (T622) también los puntos de venta activos y los medios de pago activos y vigentes hoy, con su clase
/// (Core no es de Contabilidad: Inventario los publica como dimensión para que la matriz no lea <c>COR_PaymentMeans</c>).</item>
/// <item><b>Combinaciones en uso</b> a una fecha: por cada tipo de documento cuyo <c>Contabilidad.ModoDePaso</c> vigente a esa
/// fecha no es <c>NoPasa</c> y cuya clase emite mensajes de negocio a Contabilidad, las operaciones de la matriz que trae su clase
/// × el grupo contable del producto × la bodega, tomadas del kardex de sus documentos (todas las bodegas que movieron, también el
/// tránsito) y de las líneas de los que no mueven kardex (la factura del proveedor, en la bodega del encabezado si la tiene), hasta
/// esa fecha. El grupo es el vigente del producto: la completitud pregunta por las reglas de hoy.</item>
/// </list>
/// La operación de cada clase se nombra aquí con el texto literal de la matriz (<c>AjustePositivo</c>, <c>Compra</c>…) porque
/// Inventario no ve el catálogo de operaciones de Contabilidad (<c>InventarioNoConoceContabilidadNiCartera</c>). (nuevo)
/// </summary>
public sealed class DimensionesDeInventario(IApplicationDbContext db, ILectorDeParametros parametros, IDateTimeService reloj) : IDimensionesDeInventario
{
    public async Task<CatalogoDeDimensionesDto> CatalogoAsync(CancellationToken ct)
    {
        var grupos = await db.AccountingGroups.AsNoTracking().Where(g => g.IsActive)
            .OrderBy(g => g.Code).Select(g => new CodigoDeDimensionDto(g.Code, g.Name)).ToListAsync(ct);
        var bodegas = await (from w in db.Warehouses.AsNoTracking()
                             join b in db.Branches.AsNoTracking() on w.BranchId equals b.Id into sb
                             from b in sb.DefaultIfEmpty()
                             where w.IsActive
                             orderby w.Code
                             select new BodegaDeDimensionDto(w.Code, w.Name, b == null ? null : b.PublicId, b == null ? null : b.LegacyCode, w.Behavior))
            .ToListAsync(ct);
        var causas = await db.AdjustmentCauses.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.Code).Select(c => new CodigoDeDimensionDto(c.Code, c.Name)).ToListAsync(ct);
        var tipos = await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.IsActive)
            .OrderBy(t => t.Code).Select(t => new CodigoDeDimensionDto(t.Code, t.Name)).ToListAsync(ct);
        // I3 (T622; FR-082, FR-098, SC-024): los puntos de venta activos y los medios de pago activos y vigentes hoy.
        var puntos = await db.PointsOfSale.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.Code).Select(p => new CodigoDeDimensionDto(p.Code, p.Name)).ToListAsync(ct);
        var hoy = reloj.HoyLocal;
        var medios = await db.PaymentMeans.AsNoTracking()
            .Where(m => m.IsActive && m.ValidFrom <= hoy && (m.ValidTo == null || m.ValidTo >= hoy))
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code)
            .Select(m => new MedioDePagoDeDimensionDto(m.Code, m.Name, m.Class)).ToListAsync(ct);
        return new CatalogoDeDimensionesDto(grupos, bodegas, puntos, causas, tipos, medios);
    }

    public async Task<IReadOnlyList<CombinacionEnUsoDto>> CombinacionesEnUsoAsync(DateOnly fecha, CancellationToken ct)
    {
        // 1. Los tipos que pasan a esa fecha y sus operaciones.
        var tipos = await db.InventoryDocumentTypes.AsNoTracking()
            .Select(t => new { t.Id, t.Code, t.Class, t.IsTaxableWithdrawal }).ToListAsync(ct);
        var operacionesPorTipo = new Dictionary<int, (string Codigo, IReadOnlyList<string> Operaciones)>();
        foreach (var t in tipos)
        {
            if (t.Class == DocumentClass.Voiding) continue; // la anulación usa las operaciones de su original
            var operaciones = OperacionesDeLaClase(t.Class, t.IsTaxableWithdrawal);
            if (operaciones.Count == 0) continue;
            var modo = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, fecha,
                ParameterScopeKind.DocumentType, t.Id, ct);
            if (modo.IsSuccess && modo.Value.Texto == "NoPasa") continue;
            operacionesPorTipo[t.Id] = (t.Code, operaciones);
        }
        if (operacionesPorTipo.Count == 0) return [];
        var ids = operacionesPorTipo.Keys.ToList();
        var vivos = new[] { DocumentStatus.Confirmed, DocumentStatus.Voided };

        // 2. El uso: el kardex de sus documentos y, para los que no mueven kardex, sus líneas.
        var delKardex = await (from k in db.KardexEntries.AsNoTracking()
                               join d in db.InventoryDocuments.AsNoTracking() on k.DocumentId equals d.Id
                               where ids.Contains(d.DocumentTypeId) && d.OperationDate <= fecha
                               group k by new { d.DocumentTypeId, k.WarehouseId, k.ProductId } into g
                               select new Uso(g.Key.DocumentTypeId, (int?)g.Key.WarehouseId, g.Key.ProductId, g.Max(x => x.OperationDate)))
            .ToListAsync(ct);
        var sinKardex = await (from l in db.InventoryDocumentLines.AsNoTracking()
                               join d in db.InventoryDocuments.AsNoTracking() on l.DocumentId equals d.Id
                               where ids.Contains(d.DocumentTypeId) && d.OperationDate <= fecha && vivos.Contains(d.Status) && !l.IsDeleted
                                     && !db.KardexEntries.Any(k => k.DocumentId == d.Id)
                               group l by new { d.DocumentTypeId, d.WarehouseId, l.ProductId } into g
                               select new Uso(g.Key.DocumentTypeId, g.Key.WarehouseId, g.Key.ProductId, g.Max(x => x.Document!.OperationDate)))
            .ToListAsync(ct);
        var usos = delKardex.Concat(sinKardex).ToList();
        if (usos.Count == 0) return [];

        var productos = usos.Select(u => u.ProductId).Distinct().ToList();
        var grupoDe = await (from p in db.Products.AsNoTracking().IgnoreQueryFilters()
                             join g in db.AccountingGroups.AsNoTracking().IgnoreQueryFilters() on p.AccountingGroupId equals g.Id
                             where productos.Contains(p.Id)
                             select new { p.Id, g.Code }).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        var bodegas = usos.Select(u => u.WarehouseId).OfType<int>().Distinct().ToList();
        var codigoDeBodega = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => bodegas.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Code, ct);

        // 3. Operación × grupo × bodega, con los tipos que la usan y la última fecha.
        return usos
            .SelectMany(u => operacionesPorTipo[u.DocumentTypeId].Operaciones.Select(o => new
            {
                Operacion = o,
                Grupo = grupoDe.GetValueOrDefault(u.ProductId),
                Bodega = u.WarehouseId is int w ? codigoDeBodega.GetValueOrDefault(w) : null,
                Tipo = operacionesPorTipo[u.DocumentTypeId].Codigo,
                u.Fecha,
            }))
            .GroupBy(x => (x.Operacion, x.Grupo, x.Bodega))
            .OrderBy(g => g.Key.Operacion, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Grupo, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Bodega, StringComparer.Ordinal)
            .Select(g => new CombinacionEnUsoDto(g.Key.Operacion, g.Key.Grupo, g.Key.Bodega,
                g.Select(x => x.Tipo).Distinct().Order(StringComparer.Ordinal).ToList(), g.Max(x => x.Fecha)))
            .ToList();
    }

    private sealed record Uso(int DocumentTypeId, int? WarehouseId, int ProductId, DateOnly Fecha);

    /// <summary>
    /// Las operaciones de la matriz que trae una clase (contracts/contabilidad.md §2.2), según sus mensajes de negocio a
    /// Contabilidad (<see cref="ClasesDeDocumento"/>). Vacío si la clase no pasa a Contabilidad.
    /// </summary>
    public static IReadOnlyList<string> OperacionesDeLaClase(DocumentClass clase, bool retiroGravado = false)
    {
        if (!ConfirmacionDeDocumento.EmiteNegocioAContabilidad(ClasesDeDocumento.De(clase))) return [];
        var operaciones = new List<string>();
        foreach (var mensaje in ClasesDeDocumento.De(clase).Messages)
        {
            var operacion = mensaje switch
            {
                AjusteInventarioAprobadoV1.Type => clase is DocumentClass.PositiveAdjustment or DocumentClass.NegativeAdjustment
                    or DocumentClass.InternalConsumption or DocumentClass.WriteOff or DocumentClass.Assembly
                    ? EmisionDeInventario.OperacionDeAjuste(clase, retiroGravado)
                    : null,
                CompraRecibidaV1.Type => "Compra",
                FacturaProveedorRegistradaV1.Type => "FacturaProveedor",
                DevolucionRegistradaV1.Type => clase == DocumentClass.SupplierReturn ? "DevolucionAProveedor" : "DevolucionDeCliente",
                TrasladoDespachadoV1.Type => "DespachoTraslado",
                TrasladoRecibidoV1.Type => "RecepcionTraslado",
                AjusteDeCostoReconocidoV1.Type => "AjusteDeCosto",
                VentaFacturadaV1.Type => "Venta",
                CostoDeVentaReconocidoV1.Type => "CostoDeVenta",
                NotaCreditoEmitidaV1.Type => "NotaCredito",
                NotaDebitoEmitidaV1.Type => "NotaDebito",
                MovimientoDeCajaRegistradoV1.Type => "MovimientoDeCaja",
                DiferenciaDeArqueoAprobadaV1.Type => "DiferenciaDeArqueo",
                _ => null,
            };
            if (operacion is not null && !operaciones.Contains(operacion)) operaciones.Add(operacion);
        }
        return operaciones;
    }
}
