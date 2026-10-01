using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>LandedCost</c>, los costos adicionales (feature 012, I5, T799, T800; FR-046, FR-075, US13-3; api.md §14.9;
/// data-model §9.7; contabilidad.md §3.6; mensajes.md §6.10; decisiones-transversales §3 T42e). (nuevo)
/// <list type="bullet">
/// <item><b>antes de la aprobación</b>: la factura del flete sigue confirmada y de servicio, las recepciones siguen confirmadas, lo que se
/// reparte (el <c>Subtotal</c>) no pasa de lo que queda sin repartir de la factura (<c>Inventory.LandedCost.ExceedsInvoice</c>) y el reparto
/// se puede hacer (<c>.BasisMissing</c>, <c>.ManualNotBalanced</c>). El método y lo digitado son los de la propuesta que dejó el borrador en
/// <c>INV_LandedCostAllocations</c>;</item>
/// <item><b>el cerrojo</b>: las bodegas y los estados de costo de las entradas que corrige, y como orígenes las recepciones y la factura del
/// flete —dos costos adicionales sobre la misma factura se confirman uno detrás del otro—;</item>
/// <item><b>bajo el cerrojo</b>: vuelve a mirar lo que queda de la factura y reparte con la existencia de ese momento (D5); da de baja la
/// propuesta, escribe las filas definitivas (Σ = <c>Subtotal</c>, exacto) y, por <see cref="RegistroDeKardex.RegistrarCostosAdicionalesAsync"/>,
/// las líneas <c>CostAdjustment</c> <c>LandedCost</c> con <c>AffectsEntryId</c>;</item>
/// <item><b>mensajes</b>: un <c>AjusteDeCostoReconocido</c> por recepción afectada con su porción en existencia y vendida; cada uno sigue el
/// destino del mensaje de su recepción (<c>Confirmation:{recepción:N}</c>);</item>
/// <item><b>anularlo</b>: el <c>Voiding</c> escribe las líneas contrarias de las que dejó —las mismas porciones con el signo contrario, sin
/// recalcular— y un <c>AjusteDeCostoReconocido</c> por recepción con los signos contrarios.</item>
/// </list>
/// </summary>
public class EfectoDeCostosAdicionales(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    CostosAdicionalesDeCompra costos,
    VinculosDeCompra vinculos,
    IApplicationDbContext db,
    IDateTimeService reloj) : EfectoDeClaseBase
{
    private sealed record Preparado(
        InventoryDocument? Factura,
        IReadOnlyList<InventoryDocument> Recepciones,
        IReadOnlyList<LineaConCostoAdicional> Lineas,
        IReadOnlyDictionary<int, InventoryDocumentLine> LineaDelDocumento,
        LandedCostAllocationMethod Metodo,
        IReadOnlyDictionary<int, decimal> Manuales,
        IReadOnlyList<CostoAdicionalPedido> Reversa,
        PedidoDeCerrojo Cerrojo);

    private readonly Dictionary<Guid, Preparado> _preparados = [];
    private readonly Dictionary<Guid, IReadOnlyList<CostoAdicionalRegistrado>> _registrados = [];

    public override DocumentClass Clase => DocumentClass.LandedCost;

    /// <summary>Derivado de sus recepciones (FR-075): copia el modo de paso de la primera; cada ajuste sigue el de la suya.</summary>
    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        (_preparados.TryGetValue(contexto.Documento.PublicId, out var p) ? p.Recepciones : await vinculos.OrigenesAsync(contexto.Documento, DocumentLinkKind.LandedCostOf, ct))
            .Where(r => r.Status == DocumentStatus.Confirmed).ToList();

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await PrepararReversaAsync(contexto, ct);

        var documento = contexto.Documento;
        var factura = await costos.FacturaDeAsync(documento, ct);
        if (factura is null) return Result.Failure(InventoryErrors.FieldRequired("supplierInvoicePublicId"));
        var valida = await costos.ValidarFacturaAsync(factura, ct);
        if (valida.IsFailure) return Result.Failure(valida.Error);

        var recepciones = await vinculos.OrigenesAsync(documento, DocumentLinkKind.LandedCostOf, ct);
        if (recepciones.Count == 0) return Result.Failure(InventoryErrors.FieldRequired("receiptPublicIds"));
        var confirmadas = await costos.ConfirmadasAsync(recepciones, ct);
        if (confirmadas.IsFailure) return Result.Failure(confirmadas.Error);

        var propuesta = await costos.FilasAsync(documento, ct);
        if (propuesta.Count == 0) return Result.Failure(InventoryErrors.FieldRequired("method"));
        var metodo = propuesta[0].AllocationMethod;
        var manuales = metodo == LandedCostAllocationMethod.Manual
            ? propuesta.GroupBy(f => f.ReceiptLineId).ToDictionary(g => g.Key, g => g.First().Basis)
            : new Dictionary<int, decimal>();

        var lineas = await costos.LineasAsync(recepciones, ct);
        var pares = await vinculos.DeAsync(documento, DocumentLinkKind.LandedCostOf, ct);
        var delDocumento = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        var lineaDelDocumento = pares.Where(x => delDocumento.ContainsKey(x.TargetLineId))
            .GroupBy(x => x.SourceLineId).ToDictionary(g => g.Key, g => delDocumento[g.First().TargetLineId]);
        lineas = lineas.Where(l => lineaDelDocumento.ContainsKey(l.LineaDeRecepcion.Id)).ToList();

        var revision = await RevisarAsync(documento, factura, metodo, lineas, manuales, ct);
        if (revision.IsFailure) return Result.Failure(revision.Error);

        var cerrojo = await registro.CerrojoDeEntradasAsync(documento.OperationDate, lineas.Select(l => l.Entrada).ToList(), ct);
        if (cerrojo.IsFailure) return Result.Failure(cerrojo.Error);
        _preparados[documento.PublicId] = new Preparado(factura, recepciones, lineas, lineaDelDocumento, metodo, manuales, [], cerrojo.Value with
        {
            DocumentosDeOrigen = recepciones.Select(r => r.Id).Append(factura.Id).Distinct().ToList(),
            Bodegas = cerrojo.Value.Bodegas.Concat(recepciones.Select(r => r.WarehouseId).OfType<int>()).Distinct().ToList(),
        });
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p) ? p.Cerrojo : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var p = _preparados[documento.PublicId];

        // Bajo el cerrojo: lo que otro documento pudo cambiar mientras tanto (las recepciones, la factura, lo ya repartido, la existencia).
        var confirmadas = await costos.ConfirmadasAsync(p.Recepciones, ct);
        if (confirmadas.IsFailure) return Result.Failure(confirmadas.Error);
        var reparto = await RevisarAsync(documento, p.Factura!, p.Metodo, p.Lineas, p.Manuales, ct);
        if (reparto.IsFailure) return Result.Failure(reparto.Error);

        // La propuesta del borrador pasa a ser el reparto definitivo, en su sitio (Σ = Subtotal, exacto).
        var recepcionDeLinea = p.Lineas.ToDictionary(l => l.LineaDeRecepcion.Id);
        var filas = reparto.Value.Lineas
            .Select(r => LandedCostAllocation.Desde(documento.Id, recepcionDeLinea[r.ReceiptLineId].Recepcion.Id, p.Metodo, r)).ToList();
        LandedCostAllocation.VerificarSuma(filas, documento.Subtotal);
        await costos.EscribirFilasAsync(documento, filas, reloj.UtcNow, ct);

        foreach (var r in reparto.Value.Lineas)
        {
            var linea = p.LineaDelDocumento[r.ReceiptLineId];
            linea.GrossAmount = r.AllocatedAmount;
            linea.NetAmount = r.AllocatedAmount;
            linea.TotalCost = r.AllocatedAmount;
        }
        documento.CostTotal = reparto.Value.Lineas.Sum(r => r.AllocatedAmount);

        var pedidos = reparto.Value.Lineas
            .Select(r => new CostoAdicionalPedido(p.LineaDelDocumento[r.ReceiptLineId], recepcionDeLinea[r.ReceiptLineId].Entrada, r)).ToList();
        var registrados = await registro.RegistrarCostosAdicionalesAsync(documento, pedidos, ct);
        if (registrados.IsFailure) return Result.Failure(registrados.Error);
        _registrados[documento.PublicId] = registrados.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        _registrados.TryGetValue(contexto.Documento.PublicId, out var hechos)
            ? await AjustesPorRecepcionAsync(contexto.Documento.OperationDate, hechos, ct)
            : [];

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var p = _preparados[contexto.Documento.PublicId];
        var registrados = await registro.RegistrarCostosAdicionalesAsync(contexto.Documento, p.Reversa, ct);
        if (registrados.IsFailure) return Result.Failure(registrados.Error);
        _registrados[contexto.Documento.PublicId] = registrados.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var contenidos = new List<object>(await emision.AnulacionAsync(contexto.Documento, contexto.Original!, [], ct));
        if (_registrados.TryGetValue(contexto.Documento.PublicId, out var hechos))
            contenidos.AddRange(await AjustesPorRecepcionAsync(contexto.Documento.OperationDate, hechos, ct));
        return contenidos;
    }

    // ------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>Lo que se reparte cabe en lo que queda de la factura y se puede repartir hoy: el reparto, o el porqué no.</summary>
    private async Task<Result<ResultadoDeProrrateo>> RevisarAsync(
        InventoryDocument documento, InventoryDocument factura, LandedCostAllocationMethod metodo, IReadOnlyList<LineaConCostoAdicional> lineas,
        IReadOnlyDictionary<int, decimal> manuales, CancellationToken ct)
    {
        var valida = await costos.ValidarFacturaAsync(factura, ct);
        if (valida.IsFailure) return Result.Failure<ResultadoDeProrrateo>(valida.Error);
        var disponible = await costos.DisponibleAsync(factura, documento.Id, ct);
        if (documento.Subtotal <= 0m || documento.Subtotal > disponible)
            return Result.Failure<ResultadoDeProrrateo>(ErroresDeCompras.LandedCostExceedsInvoice(Math.Max(0m, disponible)));
        return await costos.RepartirAsync(documento.OperationDate, documento.Subtotal, metodo, lineas, manuales, ct);
    }

    /// <summary>
    /// La anulación: las porciones que dejó el original (<c>INV_LandedCostAllocations</c> definitivas), con el signo contrario, sobre las
    /// mismas entradas y bajo la línea del contrario con el mismo número.
    /// </summary>
    private async Task<Result> PrepararReversaAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var original = contexto.Original!;
        var filas = await db.LandedCostAllocations.AsNoTracking().Where(f => f.DocumentId == original.Id && !f.IsDeleted).OrderBy(f => f.Id).ToListAsync(ct);
        var pares = await vinculos.DeAsync(original, DocumentLinkKind.LandedCostOf, ct);
        var numeroDeLinea = original.Lines.ToDictionary(l => l.Id, l => l.LineNumber);
        var delContrario = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber);
        var lineaDeRecepcion = pares.Where(x => numeroDeLinea.ContainsKey(x.TargetLineId))
            .GroupBy(x => x.SourceLineId).ToDictionary(g => g.Key, g => numeroDeLinea[g.First().TargetLineId]);

        var ids = filas.Select(f => f.ReceiptLineId).Distinct().ToList();
        var entradas = (await db.KardexEntries.AsNoTracking()
                .Where(k => ids.Contains(k.DocumentLineId) && k.Kind == KardexEntryKind.Entry && k.ReversesEntryId == null)
                .OrderBy(k => k.Id).ToListAsync(ct))
            .GroupBy(k => k.DocumentLineId).ToDictionary(g => g.Key, g => g.First());

        var reversa = new List<CostoAdicionalPedido>();
        foreach (var f in filas.Where(f => f.AllocatedAmount != 0m))
        {
            if (!entradas.TryGetValue(f.ReceiptLineId, out var entrada)
                || !lineaDeRecepcion.TryGetValue(f.ReceiptLineId, out var numero) || !delContrario.TryGetValue(numero, out var linea))
                continue;
            reversa.Add(new CostoAdicionalPedido(linea, entrada, new RepartoDeLinea(f.ReceiptLineId, f.ProductId, f.Basis, -f.AllocatedAmount,
                -f.RoundingResidue, f.ExistingRatio, -f.ExistingAmount, -f.SoldAmount)));
        }

        var cerrojo = await registro.CerrojoDeEntradasAsync(documento.OperationDate, reversa.Select(r => r.Entrada).ToList(), ct);
        if (cerrojo.IsFailure) return Result.Failure(cerrojo.Error);
        _preparados[documento.PublicId] = new Preparado(null, [], [], new Dictionary<int, InventoryDocumentLine>(),
            filas.FirstOrDefault()?.AllocationMethod ?? LandedCostAllocationMethod.Value, new Dictionary<int, decimal>(), reversa, cerrojo.Value);
        return Result.Success();
    }

    /// <summary>Un <c>AjusteDeCostoReconocido</c> <c>LandedCost</c> por recepción afectada (§6.10).</summary>
    private async Task<IReadOnlyList<object>> AjustesPorRecepcionAsync(DateOnly fecha, IReadOnlyList<CostoAdicionalRegistrado> hechos, CancellationToken ct)
    {
        var contenidos = new List<object>();
        foreach (var porRecepcion in hechos.GroupBy(h => h.Pedido.Entrada.DocumentId).OrderBy(g => g.Key))
        {
            var recepcion = await db.InventoryDocuments.AsNoTracking().FirstAsync(d => d.Id == porRecepcion.Key, ct);
            contenidos.Add(await emision.AjusteDeCostosAdicionalesAsync(recepcion, fecha, porRecepcion.ToList(), ct));
        }
        return contenidos;
    }
}
