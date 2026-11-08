using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>Una factura de venta como la necesita otro módulo que factura por Comercial (feature 020, R10).</summary>
public sealed record FacturaDeServicios(
    Guid PublicId, DocumentStatus Status, string? DisplayNumber, DateOnly OperationDate, decimal AmountDue, Guid? CounterpartyPersonPublicId,
    IReadOnlyList<LineaDeFacturaDeServicios> Lines, IReadOnlyList<PagoDeFacturaDeServicios> Payments);

/// <param name="Weight">El neto de la línea más sus impuestos: el peso para repartir el valor a pagar entre las líneas.</param>
public sealed record LineaDeFacturaDeServicios(Guid PublicId, int LineNumber, Guid ProductPublicId, decimal NetAmount, decimal Weight);

public sealed record PagoDeFacturaDeServicios(Guid PublicId, short LineNumber, decimal Amount, string MeansCode, bool IsCredit);

/// <summary>
/// La lectura de las facturas de Comercial que hace la facturación de servicios de Transporte (feature 020, R10): Transporte factura sólo
/// por los comandos de Comercial y lee lo que necesita por aquí, sin tocar las tablas <c>INV_</c> (<c>ElTransporteFacturaPorComercial</c>).
/// </summary>
public sealed class FacturasDeServicios(IApplicationDbContext db)
{
    /// <summary>La factura viva (no anulada ni borrada) con esa referencia externa, si hay una.</summary>
    public async Task<FacturaDeServicios?> PorReferenciaAsync(string referenciaExterna, CancellationToken ct)
    {
        var id = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.ExternalReference == referenciaExterna && d.Status != DocumentStatus.Voided)
            .OrderByDescending(d => d.Id).Select(d => (Guid?)d.PublicId).FirstOrDefaultAsync(ct);
        return id is { } publico ? await PorPublicIdAsync(publico, ct) : null;
    }

    /// <summary>La sesión de caja abierta del usuario, para cobrar de contado al facturar; nula si no tiene.</summary>
    public Task<Guid?> SesionDeCajaAbiertaAsync(int usuarioId, CancellationToken ct) =>
        db.CashSessions.AsNoTracking().Where(s => s.CashierUserId == usuarioId && s.Status == CashSessionStatus.Open)
            .OrderByDescending(s => s.Id).Select(s => (Guid?)s.PublicId).FirstOrDefaultAsync(ct);

    /// <summary>El medio de pago de efectivo activo para el cobro de contado.</summary>
    public Task<Guid?> MedioDeEfectivoAsync(CancellationToken ct) =>
        db.PaymentMeans.AsNoTracking().Where(m => m.Class == Domain.Enums.Core.PaymentMeansClass.Cash && m.IsActive)
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => (Guid?)m.PublicId).FirstOrDefaultAsync(ct);

    /// <summary>La unidad base de un producto, para la línea de la factura.</summary>
    public Task<Guid?> UnidadBaseAsync(Guid producto, CancellationToken ct) =>
        (from p in db.Products.AsNoTracking()
         join u in db.UnitsOfMeasure.AsNoTracking() on p.BaseUnitId equals u.Id
         where p.PublicId == producto
         select (Guid?)u.PublicId).FirstOrDefaultAsync(ct);

    public async Task<FacturaDeServicios?> PorPublicIdAsync(Guid documento, CancellationToken ct)
    {
        var d = await db.InventoryDocuments.AsNoTracking().Where(x => x.PublicId == documento)
            .Select(x => new { x.Id, x.PublicId, x.Status, x.Prefix, x.Number, x.OperationDate, x.AmountDue, x.CounterpartyPersonId })
            .FirstOrDefaultAsync(ct);
        if (d is null) return null;

        var lineas = await db.InventoryDocumentLines.AsNoTracking().Where(l => l.DocumentId == d.Id && !l.IsDeleted)
            .OrderBy(l => l.LineNumber).Select(l => new { l.Id, l.PublicId, l.LineNumber, l.ProductId, l.NetAmount }).ToListAsync(ct);
        var productos = await db.Products.AsNoTracking().Where(p => lineas.Select(l => l.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var impuestos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == d.Id && t.DocumentLineId != null)
            .GroupBy(t => t.DocumentLineId!.Value).Select(g => new { Linea = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.Linea, x => x.Total, ct);
        var pagos = await db.DocumentPayments.AsNoTracking().Where(p => p.DocumentId == d.Id && !p.IsDeleted)
            .OrderBy(p => p.LineNumber).Select(p => new { p.PublicId, p.LineNumber, p.Amount, p.MeansCode, p.MeansClass }).ToListAsync(ct);
        var contraparte = d.CounterpartyPersonId is int persona
            ? await db.People.AsNoTracking().Where(p => p.Id == persona).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct)
            : null;

        return new FacturaDeServicios(d.PublicId, d.Status, d.Number is { } n ? $"{d.Prefix}{n}" : null, d.OperationDate, d.AmountDue, contraparte,
            lineas.Select(l => new LineaDeFacturaDeServicios(l.PublicId, l.LineNumber, productos.GetValueOrDefault(l.ProductId), l.NetAmount,
                l.NetAmount + impuestos.GetValueOrDefault(l.Id))).ToList(),
            pagos.Select(p => new PagoDeFacturaDeServicios(p.PublicId, p.LineNumber, p.Amount, p.MeansCode, ClasesDeMedio.EsCredito(p.MeansClass))).ToList());
    }
}
