using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

// La orden de compra en PDF (feature 012, I5, T790, T791; FR-048; contracts/api.md §14.9, §17.1). Application arma el modelo y la API lo
// dibuja con QuestPDF (PurchaseOrderReport): Application no conoce la librería. (nuevos)

/// <summary>La cooperativa que compra: nombre, NIT con su dígito y dirección. (nuevo)</summary>
public sealed record CooperativaDeLaOrden(string Name, string TaxId, string? Address, string? Phone, string? City);

/// <summary>
/// El proveedor de la orden: de su copia fiscal (<c>INV_DocumentPartySnapshots</c>, la vigente) si la orden ya se confirmó; si no, del
/// maestro de personas. (nuevo)
/// </summary>
public sealed record ProveedorDeLaOrden(string Name, string TaxId, string? CheckDigit, string? Address, string? MunicipalityDaneCode, string? Email, string? Phone);

/// <summary>La bodega que recibe. (nuevo)</summary>
public sealed record BodegaDeEntrega(string Code, string Name, string? Address);

/// <summary>Una línea de la orden: producto, unidad, cantidad, precio pactado, descuento y neto. (nuevo)</summary>
public sealed record LineaDeLaOrden(int LineNumber, string ProductCode, string ProductName, string UnitCode, decimal Quantity, decimal UnitPrice,
    decimal DiscountAmount, decimal NetAmount);

/// <summary>Un impuesto estimado de la orden, agrupado por tarifa (IVA, INC…; sin retenciones). (nuevo)</summary>
public sealed record ImpuestoDeLaOrden(string TaxRateCode, string Kind, decimal? Rate, decimal Base, decimal Amount);

/// <summary>Todo lo que dibuja <c>PurchaseOrderReport</c>. (nuevo)</summary>
public sealed record OrdenDeCompraImprimible(
    Guid PublicId,
    string? DisplayNumber,
    string DocumentTypeName,
    DocumentStatus Status,
    DateOnly OperationDate,
    DateOnly? ExpectedDate,
    string? PaymentTerms,
    CooperativaDeLaOrden Cooperativa,
    ProveedorDeLaOrden Proveedor,
    BodegaDeEntrega? Bodega,
    IReadOnlyList<LineaDeLaOrden> Lineas,
    IReadOnlyList<ImpuestoDeLaOrden> Impuestos,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal Total,
    string? ElaboradaPor,
    DateTime? ConfirmadaEl)
{
    /// <summary>El nombre del archivo: <c>orden-de-compra-{número}.pdf</c> o, sin número, <c>orden-de-compra-borrador-…</c>.</summary>
    public string NombreDelArchivo => DisplayNumber is { } n
        ? $"orden-de-compra-{n}.pdf"
        : $"orden-de-compra-borrador-{PublicId.ToString("N", CultureInfo.InvariantCulture)[..8]}.pdf";
}

/// <summary>
/// La orden de compra en PDF. La implementa la API con <c>PurchaseOrderReport</c> (QuestPDF). Sin implementación registrada, el PDF y
/// el envío responden <c>Inventory.Document.RepresentationUnavailable</c>. (nuevo)
/// </summary>
public interface IOrdenDeCompraEnPdf
{
    byte[] Generar(OrdenDeCompraImprimible orden);
}

/// <summary>El PDF de una orden y su nombre de archivo. (nuevo)</summary>
public sealed record OrdenDeCompraEnPdfDto(byte[] Pdf, string FileName, OrdenDeCompraImprimible Orden);

/// <summary>
/// Arma el modelo de la orden para el PDF (T790): encabezado de la cooperativa, proveedor (copia fiscal), bodega de entrega,
/// <c>expectedDate</c>, condiciones (las notas de la orden, data-model §9.8), líneas, impuestos estimados a la fecha de la orden por
/// <see cref="CalculoTributarioDeCompra"/> (sin retenciones: las liquida la factura) y totales. Sin el cálculo, los totales guardados. (nuevo)
/// </summary>
public sealed class ModeloDeOrdenDeCompra(IApplicationDbContext db, CalculoTributarioDeCompra calculo)
{
    public async Task<OrdenDeCompraImprimible> ArmarAsync(InventoryDocument orden, CancellationToken ct)
    {
        var tipo = orden.DocumentType ?? await db.InventoryDocumentTypes.AsNoTracking().FirstAsync(t => t.Id == orden.DocumentTypeId, ct);
        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        var cooperativa = empresa is null
            ? new CooperativaDeLaOrden(string.Empty, string.Empty, null, null, null)
            : new CooperativaDeLaOrden(empresa.Name, empresa.TaxIdCheckDigit is { Length: > 0 } dv ? $"{empresa.TaxId}-{dv}" : empresa.TaxId,
                empresa.Address, empresa.Phone, empresa.City);

        var foto = await db.DocumentPartySnapshots.AsNoTracking().Where(s => s.DocumentId == orden.Id).OrderByDescending(s => s.Version).FirstOrDefaultAsync(ct);
        ProveedorDeLaOrden proveedor;
        if (foto is not null)
        {
            proveedor = new ProveedorDeLaOrden(foto.LegalName, foto.TaxId, foto.CheckDigit, foto.Address, foto.MunicipalityDaneCode, foto.Email, foto.Phone);
        }
        else
        {
            var persona = orden.CounterpartyPersonId is int id ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) : null;
            var copia = persona is null ? null : FotoDeLaContraparte.De(orden, persona);
            proveedor = copia is null
                ? new ProveedorDeLaOrden(string.Empty, string.Empty, null, null, null, null, null)
                : new ProveedorDeLaOrden(copia.LegalName, copia.TaxId, copia.CheckDigit, copia.Address, copia.MunicipalityDaneCode, copia.Email, copia.Phone);
        }

        BodegaDeEntrega? bodega = null;
        if (orden.WarehouseId is int b)
            bodega = await db.Warehouses.AsNoTracking().Where(w => w.Id == b).Select(w => new BodegaDeEntrega(w.Code, w.Name, w.Address)).FirstOrDefaultAsync(ct);

        var vivas = orden.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Code, p.Name }, ct);
        var unidadIds = vivas.Select(l => l.UnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, ct);
        var lineas = vivas.Select(l => new LineaDeLaOrden(l.LineNumber, productos.GetValueOrDefault(l.ProductId)?.Code ?? string.Empty,
            productos.GetValueOrDefault(l.ProductId)?.Name ?? string.Empty, unidades.GetValueOrDefault(l.UnitId) ?? string.Empty,
            l.Quantity, l.UnitPrice, l.DiscountAmount, l.NetAmount)).ToList();

        IReadOnlyList<ImpuestoDeLaOrden> impuestos = [];
        decimal subtotal = orden.Subtotal, descuentos = orden.DiscountTotal, impuestosTotal = orden.TaxTotal, total = orden.Total;
        if (orden.CounterpartyPersonId is not null && vivas.Count > 0)
        {
            var calculado = await calculo.CalcularAsync(orden, tipo, ct);
            if (calculado.IsSuccess)
            {
                impuestos = calculado.Value.Renglones.Where(r => !r.EsRetencion)
                    .GroupBy(r => (r.TaxRateCode, r.Kind, r.Rate))
                    .Select(g => new ImpuestoDeLaOrden(g.Key.TaxRateCode, Etiqueta(g.Key.Kind), g.Key.Rate, g.Sum(r => r.Base), g.Sum(r => r.Amount)))
                    .ToList();
                (subtotal, descuentos, impuestosTotal, total) = (calculado.Value.Totales.Subtotal, calculado.Value.Totales.DiscountTotal,
                    calculado.Value.Totales.TaxTotal, calculado.Value.Totales.Total);
            }
        }

        static string Etiqueta(Domain.Enums.Core.TaxKind kind) => kind switch
        {
            Domain.Enums.Core.TaxKind.Iva => "IVA",
            Domain.Enums.Core.TaxKind.Inc => "INC",
            Domain.Enums.Core.TaxKind.Ica => "ICA",
            _ => "Impuesto",
        };

        var elaboro = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == orden.CreatedByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct);
        return new OrdenDeCompraImprimible(orden.PublicId, VistaDeDocumentos.NumeroVisible(orden.Prefix, orden.Number), tipo.Name, orden.Status,
            orden.OperationDate, orden.ExpectedDate, orden.Notes, cooperativa, proveedor, bodega, lineas, impuestos,
            subtotal, descuentos, impuestosTotal, total, elaboro, orden.ConfirmedAt);
    }
}

/// <summary>
/// La orden de compra en PDF (<c>GET /api/inventory/purchases/orders/{id}/pdf</c>, <c>Inventory.Purchases.View</c>; T790). Con alcance:
/// fuera de él, o si no es una orden, el 404 del documento. Un borrador también se imprime (sin número). (nuevo)
/// </summary>
public sealed record GetPurchaseOrderPdfQuery(Guid OrderPublicId) : IRequest<Result<OrdenDeCompraEnPdfDto>>;

public sealed class GetPurchaseOrderPdfQueryHandler(VistaDeDocumentos vista, ModeloDeOrdenDeCompra modelo, IEnumerable<IOrdenDeCompraEnPdf> generadores)
    : IRequestHandler<GetPurchaseOrderPdfQuery, Result<OrdenDeCompraEnPdfDto>>
{
    public async Task<Result<OrdenDeCompraEnPdfDto>> Handle(GetPurchaseOrderPdfQuery request, CancellationToken ct)
    {
        var orden = await vista.BuscarAsync(request.OrderPublicId, DocumentClassGroup.Purchases, seguir: false, ct);
        if (orden is null || orden.Class != DocumentClass.PurchaseOrder) return Result.Failure<OrdenDeCompraEnPdfDto>(InventoryErrors.DocumentNotFound());
        return await OrdenesDeCompraEnPdf.GenerarAsync(orden, modelo, generadores, ct);
    }
}

/// <summary>Lo que comparten el PDF y el envío. (nuevo)</summary>
public static class OrdenesDeCompraEnPdf
{
    /// <summary>El código cuando el despliegue no tiene quien dibuje el PDF (el mismo de la carta de ventas).</summary>
    public const string RepresentationUnavailableCode = "Inventory.Document.RepresentationUnavailable";

    public static async Task<Result<OrdenDeCompraEnPdfDto>> GenerarAsync(InventoryDocument orden, ModeloDeOrdenDeCompra modelo,
        IEnumerable<IOrdenDeCompraEnPdf> generadores, CancellationToken ct)
    {
        var generador = generadores.FirstOrDefault();
        if (generador is null)
            return Result.Failure<OrdenDeCompraEnPdfDto>(new Error(RepresentationUnavailableCode, "La orden de compra en PDF todavía no está disponible en este despliegue."));
        var imprimible = await modelo.ArmarAsync(orden, ct);
        return Result.Success(new OrdenDeCompraEnPdfDto(generador.Generar(imprimible), imprimible.NombreDelArchivo, imprimible));
    }
}
