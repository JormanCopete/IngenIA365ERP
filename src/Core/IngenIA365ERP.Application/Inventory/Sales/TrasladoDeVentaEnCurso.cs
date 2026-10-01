using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// La factura después del documento equivalente en curso en esta petición (feature 012, I4, T739; api.md §18.3.1; FR-063). La anota
/// <c>ReplacePosDocumentWithInvoiceCommand</c> antes de confirmar los dos documentos, y la confirmación de la venta la lee para que el cambio
/// de documento no mueva nada dos veces:
/// <list type="bullet">
/// <item>la <b>nota de ajuste</b> de anulación total no reintegra: no exige reintegros que sumen su valor, no libera bonos ni toca la caja; su
/// ajuste al crédito es el de un reintegro total a cada pago de crédito del documento equivalente (<c>CreditNote</c>);</item>
/// <item>la <b>factura</b> no cobra ni saca mercancía: los pagos son los del documento equivalente trasladados (sin sesión de caja), sus impuestos
/// y totales los del documento equivalente, no escribe kardex (la salida ya la hizo el original) ni redime bonos, y su crédito es un
/// <c>AjusteDeVentaACredito</c> <c>Replacement</c> sobre el del documento equivalente.</item>
/// </list>
/// Scoped: vive lo que dura la petición. (nuevo)
/// </summary>
public sealed class TrasladoDeVentaEnCurso
{
    private readonly Dictionary<Guid, Guid> _notas = [];
    private readonly Dictionary<Guid, Guid> _facturas = [];

    /// <summary>La nota <paramref name="nota"/> anula el documento equivalente <paramref name="documentoEquivalente"/> sin reintegrar.</summary>
    public void Nota(Guid nota, Guid documentoEquivalente) => _notas[nota] = documentoEquivalente;

    /// <summary>La factura <paramref name="factura"/> reemplaza al documento equivalente <paramref name="documentoEquivalente"/>.</summary>
    public void Factura(Guid factura, Guid documentoEquivalente) => _facturas[factura] = documentoEquivalente;

    /// <summary>El documento equivalente que anula la nota, o nulo si es una nota corriente.</summary>
    public Guid? DeLaNota(Guid nota) => _notas.TryGetValue(nota, out var d) ? d : null;

    /// <summary>El documento equivalente que reemplaza la factura, o nulo si es una venta corriente.</summary>
    public Guid? DeLaFactura(Guid factura) => _facturas.TryGetValue(factura, out var d) ? d : null;

    /// <summary>
    /// Los impuestos del documento equivalente, copiados a las líneas de <paramref name="factura"/> por número de línea (sin guardar): la
    /// factura lleva los mismos impuestos, precios y totales (§18.3.1).
    /// </summary>
    public static async Task<IReadOnlyList<DocumentTaxLine>> ImpuestosCopiadosAsync(IApplicationDbContext db, InventoryDocument factura, Guid documentoEquivalente,
        CancellationToken ct)
    {
        var original = await db.InventoryDocuments.AsNoTracking().Include(d => d.Lines).FirstAsync(d => d.PublicId == documentoEquivalente, ct);
        var numeroDe = original.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id, l => l.LineNumber);
        var idDe = factura.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber, l => l.Id);
        var filas = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == original.Id && !t.IsDeleted).OrderBy(t => t.Id).ToListAsync(ct);
        return filas.Select(t => new DocumentTaxLine
        {
            DocumentId = factura.Id,
            DocumentLineId = t.DocumentLineId is int l && numeroDe.TryGetValue(l, out var n) && idDe.TryGetValue(n, out var nueva) ? nueva : null,
            TaxDefinitionId = t.TaxDefinitionId,
            TaxRateId = t.TaxRateId,
            TaxRateCode = t.TaxRateCode,
            Kind = t.Kind,
            Treatment = t.Treatment,
            WithholdingConceptId = t.WithholdingConceptId,
            MunicipalityDaneCode = t.MunicipalityDaneCode,
            Rate = t.Rate,
            AmountPerUnit = t.AmountPerUnit,
            TaxableUnits = t.TaxableUnits,
            Base = t.Base,
            Amount = t.Amount,
            DianTaxCode = t.DianTaxCode,
            ExplanationJson = t.ExplanationJson,
        }).ToList();
    }

    /// <summary>
    /// Los reintegros «virtuales» de la nota que anula el documento equivalente sin reintegrar: uno por pago de crédito, por todo su valor, para
    /// que el ajuste al crédito (<c>CreditNote</c>) deje la venta a crédito del documento equivalente en cero antes del <c>Replacement</c> de la
    /// factura. No se guardan.
    /// </summary>
    public static async Task<IReadOnlyList<DocumentPayment>> ReintegrosDeCreditoAsync(IApplicationDbContext db, InventoryDocument original, CancellationToken ct) =>
        (await db.DocumentPayments.AsNoTracking()
            .Where(p => p.DocumentId == original.Id && !p.IsDeleted && p.Direction == PaymentDirection.Received)
            .OrderBy(p => p.LineNumber).ToListAsync(ct))
        .Where(p => p.EsCredito)
        .Select(p => new DocumentPayment
        {
            PaymentMeansId = p.PaymentMeansId, Direction = PaymentDirection.Refunded, Amount = p.Amount, RefundsPaymentId = p.Id,
            MeansCode = p.MeansCode, MeansName = p.MeansName, MeansClass = p.MeansClass,
        })
        .ToList();
}
