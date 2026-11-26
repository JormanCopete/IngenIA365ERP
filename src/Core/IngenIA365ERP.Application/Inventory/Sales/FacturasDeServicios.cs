using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>Una factura de venta como la necesita otro módulo que factura por Comercial (feature 020, R10).</summary>
public sealed record FacturaDeServicios(
    Guid PublicId, DocumentStatus Status, string? DisplayNumber, DateOnly OperationDate, decimal AmountDue, Guid? CounterpartyPersonPublicId,
    IReadOnlyList<LineaDeFacturaDeServicios> Lines, IReadOnlyList<PagoDeFacturaDeServicios> Payments, string? ExternalReference = null);

/// <param name="Weight">El neto de la línea más sus impuestos: el peso para repartir el valor a pagar entre las líneas.</param>
/// <param name="ItemReference">La referencia de lo vendido (la placa del vehículo), si el producto la pide (2026-11-18).</param>
public sealed record LineaDeFacturaDeServicios(Guid PublicId, int LineNumber, Guid ProductPublicId, decimal NetAmount, decimal Weight, string? ItemReference = null);

public sealed record PagoDeFacturaDeServicios(Guid PublicId, short LineNumber, decimal Amount, string MeansCode, bool IsCredit);

/// <summary>
/// Lo que la anulación de una corrida de servicios necesita saber de una factura (2026-11-25): su clase, su estado, si se anula con documento
/// contrario (un comprobante no electrónico) o con su nota de anulación total (una factura electrónica), si ya quedó acreditada del todo y la
/// nota que haya quedado en borrador de un intento anterior.
/// </summary>
public sealed record FacturaParaAnular(Guid PublicId, DocumentClass Class, DocumentStatus Status, bool SeAnulaConAnulacion, bool AcreditadaDelTodo,
    Guid? NotaEnBorrador);

/// <summary>
/// La lectura de las facturas de Comercial que hace la facturación de servicios de Transporte (feature 020, R10): Transporte factura sólo
/// por los comandos de Comercial y lee lo que necesita por aquí, sin tocar las tablas <c>INV_</c> (<c>ElTransporteFacturaPorComercial</c>).
/// </summary>
public sealed class FacturasDeServicios(IApplicationDbContext db)
{
    /// <summary>
    /// La factura viva con esa referencia externa, si hay una: no anulada ni borrada y, desde el 2026-11-25, <b>no acreditada del todo</b>. Una
    /// factura electrónica corregida con su nota de anulación total sigue confirmada, pero su período se vuelve a facturar con otra factura: si
    /// se la devolviera, la facturación le colgaría cargos nuevos sin emitir nada.
    /// </summary>
    public async Task<FacturaDeServicios?> PorReferenciaAsync(string referenciaExterna, CancellationToken ct)
    {
        var candidatas = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.ExternalReference == referenciaExterna && d.Status != DocumentStatus.Voided)
            .OrderByDescending(d => d.Id).Select(d => new { d.Id, d.PublicId }).ToListAsync(ct);
        foreach (var c in candidatas)
        {
            if (!await AcreditadaDelTodoAsync(c.Id, ct)) return await PorPublicIdAsync(c.PublicId, ct);
        }
        return null;
    }

    /// <summary>
    /// Lo acreditado de cada línea de la factura por sus notas confirmadas (vínculo <c>NoteOf</c> vivo), con impuestos —el mismo peso que
    /// <see cref="LineaDeFacturaDeServicios.Weight"/>—, por <c>PublicId</c> de la línea original (2026-11-25). Sin notas, vacío.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, decimal>> AcreditadoPorLineaAsync(Guid factura, CancellationToken ct)
    {
        var id = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == factura).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        if (id is not int documento) return new Dictionary<Guid, decimal>();
        var vinculos = await VinculosDeNotasConfirmadas(documento)
            .Select(x => new { Original = x.SourceLine!.PublicId, Nota = x.TargetLineId, x.TargetLine!.NetAmount }).ToListAsync(ct);
        if (vinculos.Count == 0) return new Dictionary<Guid, decimal>();
        var lineas = vinculos.Select(v => v.Nota).Distinct().ToList();
        var impuestos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentLineId != null && lineas.Contains(t.DocumentLineId.Value))
            .GroupBy(t => t.DocumentLineId!.Value).Select(g => new { Linea = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.Linea, x => x.Total, ct);
        return vinculos.GroupBy(v => v.Original)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.NetAmount + impuestos.GetValueOrDefault(v.Nota)));
    }

    /// <summary>La factura como la ve la anulación de una corrida de servicios; nula si no existe.</summary>
    public async Task<FacturaParaAnular?> ParaAnularAsync(Guid factura, CancellationToken ct)
    {
        var d = await db.InventoryDocuments.AsNoTracking().Where(x => x.PublicId == factura).Select(x => new { x.Id, x.PublicId, x.Class, x.Status })
            .FirstOrDefaultAsync(ct);
        if (d is null) return null;
        var borrador = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == d.Id && l.Kind == DocumentLinkKind.NoteOf && !l.IsDeleted && l.TargetDocument!.Status == DocumentStatus.Draft)
            .OrderByDescending(l => l.TargetDocumentId).Select(l => (Guid?)l.TargetDocument!.PublicId).FirstOrDefaultAsync(ct);
        return new FacturaParaAnular(d.PublicId, d.Class, d.Status, ClasesDeDocumento.SeAnulaConAnulacion(d.Class),
            await AcreditadaDelTodoAsync(d.Id, ct), borrador);
    }

    /// <summary>
    /// ¿La factura quedó acreditada del todo? Una nota confirmada de anulación total (<c>IsFullReversal</c>) o notas confirmadas que suman todo
    /// el neto de sus líneas vivas.
    /// </summary>
    private async Task<bool> AcreditadaDelTodoAsync(int factura, CancellationToken ct)
    {
        if (await db.DocumentLinks.AsNoTracking().AnyAsync(l => l.SourceDocumentId == factura && l.Kind == DocumentLinkKind.NoteOf && !l.IsDeleted
                && l.TargetDocument!.Status == DocumentStatus.Confirmed && l.TargetDocument.IsFullReversal, ct))
            return true;
        var acreditado = await VinculosDeNotasConfirmadas(factura).SumAsync(x => (decimal?)x.TargetLine!.NetAmount, ct) ?? 0m;
        if (acreditado <= 0m) return false;
        var neto = await db.InventoryDocumentLines.AsNoTracking().Where(l => l.DocumentId == factura && !l.IsDeleted).SumAsync(l => l.NetAmount, ct);
        return acreditado >= neto;
    }

    /// <summary>
    /// El estado DIAN de cada factura (2026-11-25, la exportación de una corrida de servicios): el de su documento electrónico más reciente, en
    /// palabras; las que no tienen documento electrónico no vienen (un comprobante no electrónico).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, string>> EstadosDianAsync(IReadOnlyCollection<Guid> facturas, CancellationToken ct)
    {
        if (facturas.Count == 0) return new Dictionary<Guid, string>();
        var ids = facturas.Distinct().ToList();
        var filas = await db.ElectronicDocuments.AsNoTracking().Where(e => ids.Contains(e.SourceDocumentPublicId))
            .Select(e => new { e.Id, e.SourceDocumentPublicId, e.Status }).ToListAsync(ct);
        return filas.GroupBy(f => f.SourceDocumentPublicId)
            .ToDictionary(g => g.Key, g => Reports.Vistas.DianDocumentsReportQueryHandler.Estado(g.OrderByDescending(f => f.Id).First().Status));
    }

    private IQueryable<Domain.Entities.Inventory.Documents.DocumentLineLink> VinculosDeNotasConfirmadas(int factura) =>
        db.DocumentLineLinks.AsNoTracking().Where(x => !x.IsDeleted && !x.DocumentLink!.IsDeleted && x.DocumentLink.Kind == DocumentLinkKind.NoteOf
            && x.DocumentLink.SourceDocumentId == factura && x.DocumentLink.TargetDocument!.Status == DocumentStatus.Confirmed && !x.TargetLine!.IsDeleted);

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

    /// <summary>La oficina (sucursal contable) de la factura: el origen del aviso al sistema externo (feature 020, T155).</summary>
    public Task<Guid?> OficinaAsync(Guid documento, CancellationToken ct) =>
        (from d in db.InventoryDocuments.AsNoTracking()
         join b in db.Branches.AsNoTracking() on d.BranchId equals b.Id
         where d.PublicId == documento
         select (Guid?)b.PublicId).FirstOrDefaultAsync(ct);

    public async Task<FacturaDeServicios?> PorPublicIdAsync(Guid documento, CancellationToken ct)
    {
        var d = await db.InventoryDocuments.AsNoTracking().Where(x => x.PublicId == documento)
            .Select(x => new { x.Id, x.PublicId, x.Status, x.Prefix, x.Number, x.OperationDate, x.AmountDue, x.CounterpartyPersonId, x.ExternalReference })
            .FirstOrDefaultAsync(ct);
        if (d is null) return null;

        var lineas = await db.InventoryDocumentLines.AsNoTracking().Where(l => l.DocumentId == d.Id && !l.IsDeleted)
            .OrderBy(l => l.LineNumber).Select(l => new { l.Id, l.PublicId, l.LineNumber, l.ProductId, l.NetAmount, l.ItemReference }).ToListAsync(ct);
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
                l.NetAmount + impuestos.GetValueOrDefault(l.Id), l.ItemReference)).ToList(),
            pagos.Select(p => new PagoDeFacturaDeServicios(p.PublicId, p.LineNumber, p.Amount, p.MeansCode, ClasesDeMedio.EsCredito(p.MeansClass))).ToList(),
            d.ExternalReference);
    }
}
