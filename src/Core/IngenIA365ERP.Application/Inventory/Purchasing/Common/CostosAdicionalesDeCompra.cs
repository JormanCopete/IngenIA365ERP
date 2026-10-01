using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>
/// Una línea de recepción sobre la que se reparten costos adicionales: la recepción, su línea, la entrada que dejó en el kardex (lo que
/// costó) y lo que el reparto necesita del producto (peso y volumen por unidad base). (nuevo, I5, T799)
/// </summary>
public sealed record LineaConCostoAdicional(
    InventoryDocument Recepcion,
    InventoryDocumentLine LineaDeRecepcion,
    KardexEntry Entrada,
    ReferenciaDto Producto,
    decimal? Peso,
    decimal? Volumen);

/// <summary>
/// Lo que comparten el borrador, la confirmación y el detalle de unos costos adicionales (<c>LandedCost</c>; feature 012, I5, T799, T800;
/// FR-046, US13-3; api.md §14.9; data-model §9.7; decisiones-transversales §3 T42e). (nuevo)
/// <list type="bullet">
/// <item><b>la factura del flete o del seguro</b>: una factura del proveedor (o un documento soporte) confirmada con algún renglón de un
/// producto <c>Service</c> (<c>Inventory.LandedCost.InvoiceNotService</c>; sin confirmar, <c>.InvoiceNotConfirmed</c>);</item>
/// <item><b>lo que queda sin repartir</b> de esa factura: sus renglones de servicio —neto más lo que fue al costo, lo mismo que la factura
/// llevó a la cuenta de costos por distribuir (contabilidad.md §3.6)— menos el <c>Subtotal</c> de los otros costos adicionales vigentes
/// (<c>PendingApproval</c> o <c>Confirmed</c>) que la reparten (<c>.ExceedsInvoice</c>, <c>data.available</c>);</item>
/// <item><b>las recepciones</b>, confirmadas (<c>Inventory.Purchase.ReceiptNotConfirmed</c>), y de cada línea la entrada que dejó en el
/// kardex: su cantidad y su valor son la base del reparto por cantidad o por valor;</item>
/// <item><b>el reparto</b> por <see cref="Prorrateo"/> con el redondeo vigente y la existencia actual de cada producto en su ámbito de
/// costo (regla D5): en el borrador es la vista previa, en la confirmación se recalcula bajo el cerrojo.</item>
/// </list>
/// Nada de esto escribe el kardex: lo escribe <see cref="RegistroDeKardex.RegistrarCostosAdicionalesAsync"/>.
/// </summary>
public sealed class CostosAdicionalesDeCompra(IApplicationDbContext db, ILectorDeParametros parametros, RegistroDeKardex registro)
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    /// <summary>Las clases que pueden traer el flete: la factura del proveedor y el documento soporte (un transportador no obligado).</summary>
    public static bool EsFacturaDeFlete(DocumentClass clase) => clase is DocumentClass.SupplierInvoice or DocumentClass.SupportDocument;

    // ------------------------------------------------------------------------------------------------ factura --

    /// <summary>La factura del flete por su PublicId: existe, está confirmada y trae algún servicio.</summary>
    public async Task<Result<InventoryDocument>> FacturaAsync(Guid publicId, CancellationToken ct)
    {
        var factura = await db.InventoryDocuments.AsNoTracking().Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.PublicId == publicId, ct);
        return factura is null || !EsFacturaDeFlete(factura.Class)
            ? Result.Failure<InventoryDocument>(InventoryErrors.DocumentNotFound())
            : await ValidarFacturaAsync(factura, ct);
    }

    /// <summary>La factura del flete de un documento <c>LandedCost</c> (su vínculo <c>LandedCostOf</c> a nivel de documento).</summary>
    public async Task<InventoryDocument?> FacturaDeAsync(InventoryDocument costos, CancellationToken ct)
    {
        var local = db.DocumentLinks.Local.FirstOrDefault(l => !l.IsDeleted && l.Kind == DocumentLinkKind.LandedCostOf
            && (l.TargetDocument == costos || (costos.Id != 0 && l.TargetDocumentId == costos.Id)) && l.LineLinks.Count == 0);
        var facturaId = local?.SourceDocumentId;
        if (facturaId is null && costos.Id != 0)
        {
            facturaId = await db.DocumentLinks.AsNoTracking()
                .Where(l => l.TargetDocumentId == costos.Id && l.Kind == DocumentLinkKind.LandedCostOf && !l.IsDeleted)
                .Join(db.InventoryDocuments.AsNoTracking(), l => l.SourceDocumentId, d => d.Id, (l, d) => d)
                .Where(d => d.Class == DocumentClass.SupplierInvoice || d.Class == DocumentClass.SupportDocument)
                .Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        }
        return facturaId is int id
            ? await db.InventoryDocuments.AsNoTracking().Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id, ct)
            : null;
    }

    /// <summary>Confirmada y con algún renglón de servicio.</summary>
    public async Task<Result<InventoryDocument>> ValidarFacturaAsync(InventoryDocument factura, CancellationToken ct)
    {
        var numero = VistaDeDocumentos.NumeroVisible(factura.Prefix, factura.Number);
        var estado = await db.InventoryDocuments.AsNoTracking().Where(d => d.Id == factura.Id).Select(d => d.Status).FirstAsync(ct);
        if (estado != DocumentStatus.Confirmed)
            return Result.Failure<InventoryDocument>(ErroresDeCompras.LandedCostInvoiceNotConfirmed(factura.PublicId, numero, estado));
        if ((await RenglonesDeServicioAsync(factura, ct)).Count == 0)
            return Result.Failure<InventoryDocument>(ErroresDeCompras.LandedCostInvoiceNotService(factura.PublicId, numero));
        return Result.Success(factura);
    }

    /// <summary>
    /// Lo que queda sin repartir de la factura del flete sin contar <paramref name="excluir"/>: sus renglones de servicio (neto más lo que
    /// fue al costo) menos el <c>Subtotal</c> de los otros costos adicionales vigentes que la reparten.
    /// </summary>
    public async Task<decimal> DisponibleAsync(InventoryDocument factura, int excluir, CancellationToken ct)
    {
        var servicios = await RenglonesDeServicioAsync(factura, ct);
        var ids = servicios.Select(l => l.Id).ToList();
        var alCosto = await db.DocumentTaxLines.AsNoTracking()
            .Where(t => t.DocumentLineId != null && ids.Contains(t.DocumentLineId.Value) && t.Treatment == TaxTreatment.AddedToCost)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        var repartido = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == factura.Id && l.Kind == DocumentLinkKind.LandedCostOf && l.TargetDocumentId != excluir && !l.IsDeleted)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => d.Class == DocumentClass.LandedCost && Vigentes.Contains(d.Status))
            .Select(d => new { d.Id, d.Subtotal })
            .Distinct()
            .ToListAsync(ct);
        return servicios.Sum(l => l.NetAmount) + alCosto - repartido.Sum(d => d.Subtotal);
    }

    private async Task<List<InventoryDocumentLine>> RenglonesDeServicioAsync(InventoryDocument factura, CancellationToken ct)
    {
        var vivas = factura.Lines.Where(l => !l.IsDeleted).ToList();
        var productos = vivas.Select(l => l.ProductId).Distinct().ToList();
        var servicios = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => productos.Contains(p.Id) && p.Kind == ProductKind.Service).Select(p => p.Id).ToListAsync(ct);
        return vivas.Where(l => servicios.Contains(l.ProductId)).ToList();
    }

    // ------------------------------------------------------------------------------------------- recepciones --

    /// <summary>Las recepciones por su PublicId, con sus líneas: todas existen y están confirmadas.</summary>
    public async Task<Result<IReadOnlyList<InventoryDocument>>> RecepcionesAsync(IReadOnlyCollection<Guid> publicos, CancellationToken ct)
    {
        var recepciones = await db.InventoryDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => publicos.Contains(d.PublicId) && d.Class == DocumentClass.PurchaseReceipt)
            .OrderBy(d => d.Id).ToListAsync(ct);
        if (recepciones.Count != publicos.Distinct().Count()) return Result.Failure<IReadOnlyList<InventoryDocument>>(InventoryErrors.DocumentNotFound());
        return await ConfirmadasAsync(recepciones, ct);
    }

    /// <summary>Todas confirmadas (leído de la base: bajo el cerrojo, el estado vigente).</summary>
    public async Task<Result<IReadOnlyList<InventoryDocument>>> ConfirmadasAsync(IReadOnlyList<InventoryDocument> recepciones, CancellationToken ct)
    {
        var ids = recepciones.Select(r => r.Id).ToList();
        var estados = await db.InventoryDocuments.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Status, ct);
        foreach (var recepcion in recepciones)
        {
            if (estados.GetValueOrDefault(recepcion.Id) != DocumentStatus.Confirmed)
                return Result.Failure<IReadOnlyList<InventoryDocument>>(
                    ErroresDeCompras.ReceiptNotConfirmed(recepcion.PublicId, VistaDeDocumentos.NumeroVisible(recepcion.Prefix, recepcion.Number)));
        }
        return Result.Success(recepciones);
    }

    /// <summary>
    /// Las líneas vivas de las recepciones que dejaron una entrada en el kardex (los servicios no entran), en el orden de la recepción y
    /// de la línea: el mismo orden en que el borrador arma sus líneas.
    /// </summary>
    public async Task<IReadOnlyList<LineaConCostoAdicional>> LineasAsync(IReadOnlyList<InventoryDocument> recepciones, CancellationToken ct)
    {
        var lineas = recepciones.SelectMany(r => r.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l => (Recepcion: r, Linea: l))).ToList();
        var ids = lineas.Select(x => x.Linea.Id).ToList();
        var entradas = (await db.KardexEntries.AsNoTracking()
                .Where(k => ids.Contains(k.DocumentLineId) && k.Kind == KardexEntryKind.Entry && k.ReversesEntryId == null)
                .OrderBy(k => k.Id).ToListAsync(ct))
            .GroupBy(k => k.DocumentLineId).ToDictionary(g => g.Key, g => g.First());
        var idsDeProducto = lineas.Select(x => x.Linea.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => idsDeProducto.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { Ref = new ReferenciaDto(p.PublicId, p.Code, p.Name), p.Weight, p.Volume }, ct);
        return lineas.Where(x => entradas.ContainsKey(x.Linea.Id) && productos.ContainsKey(x.Linea.ProductId))
            .Select(x =>
            {
                var p = productos[x.Linea.ProductId];
                return new LineaConCostoAdicional(x.Recepcion, x.Linea, entradas[x.Linea.Id], p.Ref, p.Weight, p.Volume);
            })
            .ToList();
    }

    // -------------------------------------------------------------------------------------------------- reparto --

    /// <summary>
    /// El reparto de <paramref name="monto"/> por <paramref name="metodo"/> entre <paramref name="lineas"/> con el redondeo vigente a
    /// <paramref name="fecha"/> y la existencia actual de cada producto en su ámbito de costo (D5); <paramref name="manuales"/> es lo
    /// digitado por línea de recepción (<c>Manual</c>). Un rechazo de <see cref="Prorrateo"/> vuelve como su error de api.md §14.10.
    /// </summary>
    public async Task<Result<ResultadoDeProrrateo>> RepartirAsync(
        DateOnly fecha, decimal monto, LandedCostAllocationMethod metodo, IReadOnlyList<LineaConCostoAdicional> lineas,
        IReadOnlyDictionary<int, decimal> manuales, CancellationToken ct)
    {
        if (lineas.Count == 0) return Result.Failure<ResultadoDeProrrateo>(InventoryErrors.Empty());
        var bodegas = lineas.Select(l => l.Entrada.WarehouseId).Distinct().ToList();
        var leidos = await registro.LeerParametrosAsync(fecha, bodegas, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeProrrateo>(leidos.Error);
        var p = leidos.Value;
        var residuo = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoResiduo, fecha, ct: ct);
        if (residuo.IsFailure) return Result.Failure<ResultadoDeProrrateo>(residuo.Error);

        // La existencia del ámbito, seguida: bajo el cerrojo es la bloqueada, con lo que esta unidad de trabajo ya le haya hecho.
        var productos = lineas.Select(l => l.Entrada.ProductId).Distinct().ToList();
        var estados = (await db.CostStates.Where(c => productos.Contains(c.ProductId)).ToListAsync(ct))
            .GroupBy(c => (c.ProductId, c.ScopeWarehouseId)).ToDictionary(g => g.Key, g => g.First().Quantity);
        // T843: con PEPS la existencia de la regla D5 es lo que queda de la capa de cada recepción (restante / original), no la del ámbito.
        var restantes = await registro.RestantePorEntradaAsync(fecha, lineas.Select(l => l.Entrada).ToList(), ct);

        var aRepartir = lineas.Select(l => new LineaAProrratear(
            l.LineaDeRecepcion.Id,
            l.Entrada.ProductId,
            l.Entrada.QuantityBase,
            l.Entrada.TotalCost,
            l.Peso,
            l.Volumen,
            manuales.TryGetValue(l.LineaDeRecepcion.Id, out var digitado) ? digitado : (metodo == LandedCostAllocationMethod.Manual ? 0m : null),
            restantes is not null
                ? restantes.GetValueOrDefault(l.Entrada.Id)
                : Math.Max(0m, estados.GetValueOrDefault((l.Entrada.ProductId, p.AmbitoDe(l.Entrada.WarehouseId)))))).ToList();

        var resultado = Prorrateo.Repartir(new PedidoDeProrrateo(monto, metodo, aRepartir, p.Montos, Redondeo.ResiduoDesde(residuo.Value.Texto)));
        if (resultado.Rechazo is not { } rechazo) return Result.Success(resultado);
        if (rechazo.Codigo == Prorrateo.CodigoManualNoCuadra)
            return Result.Failure<ResultadoDeProrrateo>(ErroresDeCompras.LandedCostManualNotBalanced(rechazo.Monto ?? monto, rechazo.Repartido ?? 0m));
        var sinBase = lineas.Where(l => rechazo.Productos.Contains(l.Entrada.ProductId))
            .Select(l => new ErroresDeCompras.ProductoSinBase(l.Producto.PublicId, l.Producto.Code)).DistinctBy(x => x.PublicId).ToList();
        return Result.Failure<ResultadoDeProrrateo>(ErroresDeCompras.LandedCostBasisMissing(rechazo.Mensaje, sinBase));
    }

    // ---------------------------------------------------------------------------------------------------- vista --

    /// <summary>
    /// <c>landedCost</c> del borrador o del detalle (api.md §14.9): las porciones en el orden de las líneas de recepción. Sin reparto (un
    /// rechazo), la lista va vacía y el porqué en los avisos.
    /// </summary>
    public static LandedCostDto Vista(
        InventoryDocument factura, LandedCostAllocationMethod metodo, decimal monto, decimal disponible,
        IReadOnlyList<LineaConCostoAdicional> lineas, IReadOnlyList<RepartoDeLinea> repartos)
    {
        var porLinea = repartos.ToDictionary(r => r.ReceiptLineId);
        var porciones = lineas.Where(l => porLinea.ContainsKey(l.LineaDeRecepcion.Id)).Select(l =>
        {
            var r = porLinea[l.LineaDeRecepcion.Id];
            return new LandedCostAllocationDto(
                new LandedCostReceiptLineDto(l.Recepcion.PublicId, VistaDeDocumentos.NumeroVisible(l.Recepcion.Prefix, l.Recepcion.Number),
                    l.LineaDeRecepcion.PublicId, l.LineaDeRecepcion.LineNumber),
                l.Producto, r.Basis, r.AllocatedAmount, r.RoundingResidue, r.ExistingAmount, r.SoldAmount);
        }).ToList();
        return new LandedCostDto(new DocumentoReferidoDto(factura.PublicId, VistaDeDocumentos.NumeroVisible(factura.Prefix, factura.Number)),
            metodo, monto, disponible, porciones, porciones.Sum(p => p.RoundingResidue));
    }

    /// <summary>Un reparto guardado (<c>INV_LandedCostAllocations</c>) como el del motor, para la vista.</summary>
    public static RepartoDeLinea ComoReparto(LandedCostAllocation fila) => new(fila.ReceiptLineId, fila.ProductId, fila.Basis, fila.AllocatedAmount,
        fila.RoundingResidue, fila.ExistingRatio, fila.ExistingAmount, fila.SoldAmount);

    /// <summary>Las filas vivas del reparto de un documento (la propuesta del borrador o las definitivas).</summary>
    public async Task<List<LandedCostAllocation>> FilasAsync(InventoryDocument costos, CancellationToken ct) =>
        costos.Id == 0
            ? db.LandedCostAllocations.Local.Where(f => f.Document == costos && !f.IsDeleted).ToList()
            : await db.LandedCostAllocations.Where(f => f.DocumentId == costos.Id && !f.IsDeleted).OrderBy(f => f.Id).ToListAsync(ct);

    /// <summary>
    /// Escribe el reparto de un documento sobre sus filas vivas: la de la misma línea de recepción se actualiza en su sitio, la que ya no
    /// está queda de baja lógica y la nueva se agrega. Así nunca conviven dos vivas con la misma <c>(DocumentId, ReceiptLineId)</c> en el
    /// mismo guardado (el índice único filtrado no depende del orden en que el motor mande las órdenes). Sólo mientras el documento es
    /// borrador: confirmado, sus filas no cambian.
    /// </summary>
    public async Task EscribirFilasAsync(InventoryDocument costos, IReadOnlyList<LandedCostAllocation> nuevas, DateTime ahora, CancellationToken ct)
    {
        var vivas = (await FilasAsync(costos, ct)).GroupBy(f => f.ReceiptLineId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var nueva in nuevas)
        {
            if (vivas.Remove(nueva.ReceiptLineId, out var existentes))
            {
                var fila = existentes[0];
                fila.ReceiptDocumentId = nueva.ReceiptDocumentId;
                fila.ProductId = nueva.ProductId;
                fila.AllocationMethod = nueva.AllocationMethod;
                fila.Basis = nueva.Basis;
                fila.AllocatedAmount = nueva.AllocatedAmount;
                fila.RoundingResidue = nueva.RoundingResidue;
                fila.ExistingRatio = nueva.ExistingRatio;
                fila.ExistingAmount = nueva.ExistingAmount;
                fila.SoldAmount = nueva.SoldAmount;
                foreach (var sobrante in existentes.Skip(1)) DarDeBaja(sobrante, ahora);
                continue;
            }
            nueva.Document = costos;
            nueva.DocumentId = costos.Id;
            db.LandedCostAllocations.Add(nueva);
        }
        foreach (var sobrante in vivas.Values.SelectMany(x => x)) DarDeBaja(sobrante, ahora);
    }

    private static void DarDeBaja(LandedCostAllocation fila, DateTime ahora)
    {
        fila.IsDeleted = true;
        fila.DeletedAt = ahora;
    }
}
