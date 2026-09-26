using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>SupplierNote</c> (feature 012, T342; FR-050; contracts/api.md §14.5; E9). (nuevo)
/// <list type="bullet">
/// <item>contra una factura confirmada del mismo proveedor (<c>NoteOf</c>, líneas enlazadas a las de la factura);</item>
/// <item>una nota crédito no pasa de lo que queda de la factura tras las otras notas (<c>Inventory.SupplierNote.ExceedsInvoice</c>,
/// <c>data.remaining</c>);</item>
/// <item>impuestos y retenciones con la <b>foto de la factura</b>, en proporción y sin volver a probar la base mínima (E9);</item>
/// <item>la misma unicidad del documento del proveedor que la factura;</item>
/// <item>no mueve existencia; con <c>affectsCost</c> deja la diferencia de precio sobre la recepción y su
/// <c>AjusteDeCostoReconocido</c>;</item>
/// <item>emite <c>FacturaProveedorRegistrada</c> con su signo (negativa la crédito) y copia el modo de la factura
/// (relacionado); el monto que se aprueba es su <c>Total</c> (rige el límite de <c>Purchases.Confirm</c>).</item>
/// </list>
/// </summary>
public sealed class EfectoNotaDeProveedor(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    CalculoTributarioDeCompra calculo,
    VinculosDeCompra vinculos,
    DiferenciasDePrecioDeCompra diferencias,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private sealed record Preparado(
        CalculoDeCompra Calculo,
        InventoryDocument? Factura,
        IReadOnlyList<DiferenciaDePrecioPedida> Diferencias,
        PedidoDeCerrojo Cerrojo);

    private readonly Dictionary<Guid, Preparado> _preparados = [];
    private readonly Dictionary<Guid, IReadOnlyList<DiferenciaDePrecioRegistrada>> _registradas = [];

    public override DocumentClass Clase => DocumentClass.SupplierNote;

    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p) && p.Factura is { } factura
            ? [factura]
            : (await vinculos.OrigenesAsync(contexto.Documento, DocumentLinkKind.NoteOf, ct)).Where(f => f.Status == DocumentStatus.Confirmed).ToList();

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (contexto.EsAnulacion)
        {
            var original = contexto.Original!;
            var detalleOriginal = await db.SupplierInvoiceDetails.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == original.Id, ct);
            var reversa = await diferencias.DeNotaAsync(original, detalleOriginal?.IsDebitNote == true, await diferencias.AlCostoGuardadoAsync(original, ct),
                documento.Lines.Where(l => !l.IsDeleted).ToList(), -1m, ct);
            var cerrojoDeReversa = await registro.CerrojoDeDiferenciasAsync(documento, reversa, ct);
            if (cerrojoDeReversa.IsFailure) return Result.Failure(cerrojoDeReversa.Error);
            _preparados[documento.PublicId] = new Preparado(new CalculoDeCompra([], TotalesDeCompra.Cero, [], false), null, reversa, cerrojoDeReversa.Value);
            return Result.Success();
        }

        var comunes = await ReglasDeCompra.ComunesAsync(contexto, maestros, ct);
        if (comunes is not null) return Result.Failure(comunes);
        var detalle = await vinculos.DetalleAsync(documento, ct);
        if (detalle is null) return Result.Failure(InventoryErrors.FieldRequired("supplier"));
        if (detalle.IsElectronic && detalle.Cufe is null) return Result.Failure(ErroresDeCompras.CufeRequired());
        var duplicado = await ColisionDeFacturaDeProveedor.BuscarAsync(db, detalle, ct);
        if (duplicado is not null) return Result.Failure(duplicado);

        var facturas = await vinculos.OrigenesAsync(documento, DocumentLinkKind.NoteOf, ct);
        if (facturas.Count != 1) return Result.Failure(InventoryErrors.FieldRequired("supplierInvoice"));
        var factura = facturas[0];
        var numero = VistaDeDocumentos.NumeroVisible(factura.Prefix, factura.Number);
        if (factura.Status != DocumentStatus.Confirmed) return Result.Failure(ErroresDeCompras.NoteInvoiceNotConfirmed(factura.PublicId, numero));
        if (factura.CounterpartyPersonId != documento.CounterpartyPersonId)
            return Result.Failure(ErroresDeCompras.NoteInvoiceFromOtherSupplier(factura.PublicId, numero));

        var pares = await vinculos.DeAsync(documento, DocumentLinkKind.NoteOf, ct);
        var porLinea = pares.GroupBy(p => p.TargetLineId).ToDictionary(g => g.Key, g => g.First().SourceLineId);
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        foreach (var linea in vivas)
            if (!porLinea.ContainsKey(linea.Id)) return Result.Failure(ErroresDeCompras.NoteLineRequired(linea.LineNumber));

        // Impuestos y retenciones con la foto de la factura, en proporción (E9).
        var numeros = factura.Lines.ToDictionary(l => l.Id, l => l.LineNumber);
        var foto = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == factura.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var lineaOriginal = vivas.ToDictionary(l => l.LineNumber, l => numeros.GetValueOrDefault(porLinea[l.Id]));
        var calculado = await calculo.CalcularAsync(documento, contexto.Tipo, ct, CalculoTributarioDeCompra.DesdeLaFoto(foto, numeros), lineaOriginal);
        if (calculado.IsFailure) return Result.Failure(calculado.Error);
        CalculoTributarioDeCompra.AplicarTotales(documento, calculado.Value.Totales);

        if (!detalle.IsDebitNote)
        {
            var restante = await vinculos.RestanteDeFacturaAsync(factura, documento.Id, ct);
            if (documento.Total > restante) return Result.Failure(ErroresDeCompras.NoteExceedsInvoice(restante));
        }

        var alCosto = vivas.ToDictionary(l => l.LineNumber, l => calculado.Value.AlCostoDeLinea(l.LineNumber));
        var pedidas = await diferencias.DeNotaAsync(documento, detalle.IsDebitNote, alCosto, vivas, 1m, ct);
        var cerrojo = await registro.CerrojoDeDiferenciasAsync(documento, pedidas, ct);
        if (cerrojo.IsFailure) return Result.Failure(cerrojo.Error);
        _preparados[documento.PublicId] = new Preparado(calculado.Value, factura, pedidas, cerrojo.Value with { DocumentosDeOrigen = [factura.Id] });
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var p = _preparados[documento.PublicId];
        var registradas = await registro.RegistrarDiferenciasDePrecioAsync(documento, p.Diferencias, ct);
        if (registradas.IsFailure) return Result.Failure(registradas.Error);
        _registradas[documento.PublicId] = registradas.Value;

        var lineas = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber);
        foreach (var r in p.Calculo.Renglones)
            db.DocumentTaxLines.Add(CalculoTributarioDeCompra.Foto(documento.Id, r.Linea is int n && lineas.TryGetValue(n, out var l) ? l.Id : null, r));
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (!_preparados.TryGetValue(documento.PublicId, out var p) || p.Factura is null) return [];
        var detalle = await vinculos.DetalleAsync(documento, ct);
        if (detalle is null) return [];

        // Cada línea hereda la bodega de la recepción de la línea de factura que corrige (o ninguna, si es un servicio).
        var pares = await vinculos.DeAsync(documento, DocumentLinkKind.NoteOf, ct);
        var lineaDeFactura = pares.GroupBy(x => x.TargetLineId).ToDictionary(g => g.Key, g => g.First().SourceLineId);
        var idsDeFactura = lineaDeFactura.Values.Distinct().ToList();
        var bodegas = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => idsDeFactura.Contains(x.TargetLineId))
            .Join(db.DocumentLinks.AsNoTracking(), x => x.DocumentLinkId, l => l.Id, (x, l) => new { x.TargetLineId, l.Kind, l.SourceDocumentId })
            .Where(y => y.Kind == DocumentLinkKind.InvoiceOfReceipt)
            .Join(db.InventoryDocuments.AsNoTracking(), y => y.SourceDocumentId, d => d.Id, (y, d) => new { y.TargetLineId, d.WarehouseId })
            .ToDictionaryAsync(y => y.TargetLineId, y => y.WarehouseId, ct);

        var lineas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l =>
        {
            int? bodega = lineaDeFactura.TryGetValue(l.Id, out var lf) && bodegas.TryGetValue(lf, out var b) ? b : null;
            return new EmisionDeInventario.LineaDeFacturaDeProveedor(l.LineNumber, l.ProductId, bodega, bodega is null,
                l.GrossAmount, l.DiscountAmount, l.NetAmount, p.Calculo.AlCostoDeLinea(l.LineNumber));
        }).ToList();

        var signo = detalle.IsDebitNote ? 1m : -1m;
        var contenidos = new List<object>
        {
            await emision.FacturaProveedorRegistradaAsync(documento, detalle, detalle.IsDebitNote ? "DebitNote" : "CreditNote", [], lineas,
                p.Calculo.Renglones, signo, ct),
        };
        if (_registradas.TryGetValue(documento.PublicId, out var hechas))
            contenidos.AddRange(await EfectoFacturaDeProveedor.AjustesPorRecepcionAsync(emision, db, documento.OperationDate, hechas, ct));
        return contenidos;
    }

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var p = _preparados[contexto.Documento.PublicId];
        var registradas = await registro.RegistrarDiferenciasDePrecioAsync(contexto.Documento, p.Diferencias, ct);
        if (registradas.IsFailure) return Result.Failure(registradas.Error);
        _registradas[contexto.Documento.PublicId] = registradas.Value;

        var detalle = await db.SupplierInvoiceDetails.FirstOrDefaultAsync(d => d.DocumentId == contexto.Original!.Id, ct);
        if (detalle is not null) detalle.IsReleased = true;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var contenidos = new List<object>(await emision.AnulacionAsync(contexto.Documento, contexto.Original!, [], ct));
        if (_registradas.TryGetValue(contexto.Documento.PublicId, out var hechas))
            contenidos.AddRange(await EfectoFacturaDeProveedor.AjustesPorRecepcionAsync(emision, db, contexto.Documento.OperationDate, hechas, ct));
        return contenidos;
    }
}
