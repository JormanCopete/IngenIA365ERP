using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// <c>SalesInvoiceFromShipments</c>, la factura desde remisiones (feature 012, I6, T881; FR-036, FR-052, FR-075, T9; contracts/api.md §18.2,
/// §18.4; decisiones-transversales §1.3 pasos 4 y 10). Es una venta como las de <see cref="SalidaPorVenta"/> —veredicto fiscal, impuestos,
/// pagos, crédito, <c>VentaFacturada</c>, numeración por <c>NumeradorFiscal</c> tras <c>GuardiaDeEmisionFiscal</c> y el documento electrónico en
/// <c>Pending</c>— con tres diferencias:
/// <list type="bullet">
/// <item><b>no descarga</b>: lo que factura ya salió y ya reconoció su costo en la remisión; no escribe kardex ni emite
/// <c>CostoDeVentaReconocido</c>;</item>
/// <item>sus remisiones (<c>FromShipment</c>) están confirmadas, no anuladas y son del mismo cliente (<c>Inventory.Shipment.CustomerMismatch</c>);
/// cada línea factura a lo sumo lo pendiente de su línea de remisión (<c>Inventory.Shipment.AlreadyInvoiced</c> con
/// <c>data.lines[] { shipmentLinePublicId, pending }</c>), y la confirmación bloquea las remisiones para que dos facturas no se pasen;</item>
/// <item>es <b>derivada</b>: no sella modo de paso, copia el de sus remisiones (T9), y remisiones con modos distintos responden
/// <c>Inventory.PostingMode.ChainMismatch</c>.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class EfectoDeFacturaDesdeRemisiones(
    RegistroDeKardex registro, EmisionDeInventario emision, IMaestrosDelDocumento maestros, ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion, IApplicationDbContext db, VinculosDelCiclo vinculos)
    : SalidaPorVenta(registro, emision, maestros, reglas, anulacion, db, null, vinculos)
{
    private readonly Dictionary<Guid, IReadOnlyList<InventoryDocument>> _remisiones = [];

    public override DocumentClass Clase => DocumentClass.SalesInvoiceFromShipments;

    protected override bool DescargaExistencia => false;

    protected override IReadOnlyList<int> OrigenesQueBloquea(ContextoDeEfecto contexto) =>
        _remisiones.TryGetValue(contexto.Documento.PublicId, out var r) ? r.Select(d => d.Id).ToList() : [];

    protected override async Task<Result> ValidarOrigenesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var remisiones = await vinculos.OrigenesAsync(documento, DocumentLinkKind.FromShipment, ct);
        if (remisiones.Count == 0 || remisiones.Any(r => r.Class != DocumentClass.Shipment || r.Status != DocumentStatus.Confirmed))
            return Result.Failure(ErroresDelCicloComercial.OriginInvalid(DocumentClass.SalesInvoiceFromShipments, DocumentClass.Shipment));
        if (remisiones.Any(r => r.CounterpartyPersonId != documento.CounterpartyPersonId))
            return Result.Failure(ErroresDelCicloComercial.ShipmentCustomerMismatch());
        if (remisiones.Select(r => r.PostingMode).Distinct().Count() > 1)
            return Result.Failure(ErroresDelCicloComercial.ShipmentsChainMismatch(
                remisiones.Select(r => VistaDeDocumentos.NumeroVisible(r.Prefix, r.Number) ?? r.PublicId.ToString()).ToList()));

        var exceso = await ExcesoAsync(vinculos, documento, remisiones, ct);
        if (exceso is not null) return Result.Failure(exceso);
        _remisiones[documento.PublicId] = remisiones;
        return Result.Success();
    }

    /// <summary>Las remisiones confirmadas de las que deriva (copia su modo y las nombra como relacionadas).</summary>
    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return [];
        var remisiones = _remisiones.TryGetValue(contexto.Documento.PublicId, out var r)
            ? r
            : await vinculos.OrigenesAsync(contexto.Documento, DocumentLinkKind.FromShipment, ct);
        return remisiones.Where(x => x.Status == DocumentStatus.Confirmed).ToList();
    }

    /// <summary>
    /// <c>Inventory.Shipment.AlreadyInvoiced</c> si alguna línea factura más de lo pendiente de su línea de remisión (sin contar esta factura);
    /// nulo si cabe. Lo usan la confirmación y el borrador (como aviso).
    /// </summary>
    public static async Task<Error?> ExcesoAsync(VinculosDelCiclo vinculos, InventoryDocument factura, IReadOnlyList<InventoryDocument> remisiones, CancellationToken ct)
    {
        var pares = await vinculos.ParesAsync(factura, DocumentLinkKind.FromShipment, ct);
        var lineas = remisiones.SelectMany(r => r.Lines).Where(l => !l.IsDeleted && pares.Any(p => p.SourceLineId == l.Id)).ToList();
        var pendiente = await vinculos.PendientePorFacturarAsync(lineas, factura.Id, ct);
        var excedidas = pares.GroupBy(p => p.SourceLineId)
            .Where(g => g.Sum(p => p.Destino.QuantityBase) > pendiente.GetValueOrDefault(g.Key))
            .Select(g => new ErroresDelCicloComercial.PendienteDeRemision(lineas.First(l => l.Id == g.Key).PublicId, pendiente.GetValueOrDefault(g.Key),
                g.Sum(p => p.Destino.QuantityBase)))
            .ToList();
        return excedidas.Count > 0 ? ErroresDelCicloComercial.ShipmentAlreadyInvoiced(excedidas) : null;
    }
}
