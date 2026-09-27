using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Inventory.Sales;

// El modelo de la tirilla (feature 012, I3, T607; contracts/api.md §20.2 «TicketDto»). Todos (nuevo).

public sealed record TicketDto(
    CashRegisterPrintFormat Format,
    bool Copy,
    TicketHeaderDto Header,
    TicketDocumentDto Document,
    TicketPartyDto Party,
    IReadOnlyList<TicketLineDto> Lines,
    IReadOnlyList<TicketTaxDto> Taxes,
    IReadOnlyList<TicketWithholdingDto> Withholdings,
    TicketTotalsDto Totals,
    IReadOnlyList<TicketPaymentDto> Payments,
    decimal Change,
    TicketElectronicDto? Electronic,
    IReadOnlyList<string> Footer);

public sealed record TicketHeaderDto(string CompanyName, string Nit, string BranchName, string? Address, string? ResolutionText, string RegimeText);

public sealed record TicketDocumentDto(string ClassLabel, string Prefix, long? Number, DateTime? IssuedAt, string? CashRegisterCode, string? CashierName, string? SalespersonName);

public sealed record TicketPartyDto(string Name, string IdType, string IdNumber);

public sealed record TicketLineDto(string Code, string Description, decimal Quantity, string UnitCode, decimal UnitPrice, decimal Discount, decimal Total, string TaxMark);

public sealed record TicketTaxDto(string Label, decimal Base, decimal Amount);

public sealed record TicketWithholdingDto(string Label, decimal Amount);

public sealed record TicketTotalsDto(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal Total, decimal AmountDue);

public sealed record TicketPaymentDto(string MeansName, decimal Amount, string? Reference, string? Last4);

public sealed record TicketElectronicDto(string UniqueCodeKind, string UniqueCode, string QrContent, string? Legend);

/// <summary>
/// La opción de la tirilla <b>(nuevo)</b>. La fija la API por ambiente, como <c>PuestaEnMarchaOptions</c>:
/// <c>AmbienteDePruebas = !env.IsProduction()</c>. En pruebas toda representación dice «SIN VALIDEZ FISCAL» (quickstart §5.13).
/// </summary>
public sealed class TirillaOptions
{
    public bool AmbienteDePruebas { get; set; }
}

/// <summary>
/// Arma la tirilla de un documento confirmado (feature 012, I3, T607; §20.2, §20.3; T52, FR-011): el comprador sale de la <b>copia
/// fiscal vigente</b> (<c>INV_DocumentPartySnapshots</c>, la de mayor versión), no del maestro de hoy; sin copia es el consumidor final
/// de la Res. 202/2025. Los impuestos y retenciones son los guardados al confirmar (<c>INV_DocumentTaxLines</c>) y los pagos, los de
/// <c>INV_DocumentPayments</c> (nunca el número de la tarjeta: sólo <c>last4</c>). Con <c>copy</c> lleva la marca «COPIA»; en el ambiente
/// de pruebas, «SIN VALIDEZ FISCAL». El bloque electrónico lo agrega I4. (nuevo)
/// </summary>
public sealed class ConstructorDeTirilla(IApplicationDbContext db, IOptions<TirillaOptions>? opciones = null)
{
    public const string LeyendaDePruebas = "SIN VALIDEZ FISCAL";
    public const string MarcaDeCopia = "COPIA";

    public async Task<TicketDto> ConstruirAsync(InventoryDocument documento, CashRegisterPrintFormat formato, bool copia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id)
            .Select(c => new { c.Name, c.TaxId, c.TaxIdCheckDigit, c.Address }).FirstOrDefaultAsync(ct);
        var sucursal = await db.Branches.AsNoTracking().Where(b => b.Id == documento.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        var punto = documento.PointOfSaleId is int pid
            ? await db.PointsOfSale.AsNoTracking().Where(p => p.Id == pid).Select(p => new { p.Address }).FirstOrDefaultAsync(ct)
            : null;
        var caja = documento.CashRegisterId is int cid ? await db.CashRegisters.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Code).FirstOrDefaultAsync(ct) : null;
        var cajero = documento.CashSessionId is int sid ? await db.CashSessions.AsNoTracking().Where(s => s.Id == sid).Select(s => s.CashierName).FirstOrDefaultAsync(ct) : null;
        string? vendedor = null;
        if (documento.SalespersonId is int vid)
        {
            var persona = await db.Salespeople.AsNoTracking().Where(s => s.Id == vid).Select(s => s.Person).FirstOrDefaultAsync(ct);
            if (persona is not null) vendedor = BorradorDelPos.Nombre(persona);
        }

        // El comprador: la copia fiscal vigente (T52).
        var foto = await db.DocumentPartySnapshots.AsNoTracking().Where(s => s.DocumentId == documento.Id).OrderByDescending(s => s.Version).FirstOrDefaultAsync(ct);
        TicketPartyDto comprador;
        if (foto is not null) comprador = new TicketPartyDto(foto.LegalName, foto.DianIdTypeCode, foto.CheckDigit is { Length: > 0 } dv ? $"{foto.TaxId}-{dv}" : foto.TaxId);
        else
        {
            var final = CatalogoDian.Embebido.ConsumidorFinal(documento.OperationDate);
            comprador = new TicketPartyDto(final?.Nombre ?? "Consumidor final", final?.TipoDeIdentificacion ?? string.Empty, final?.Numero ?? string.Empty);
        }

        // Líneas, impuestos, retenciones y pagos.
        var lineas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productoIds = lineas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Code, p.Name }, ct);
        var unidadIds = lineas.Select(l => l.UnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, ct);
        var tributos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == documento.Id).ToListAsync(ct);
        var generados = tributos.Where(t => t.Treatment == TaxTreatment.Generated).ToList();
        var pagos = await db.DocumentPayments.AsNoTracking().Where(p => p.DocumentId == documento.Id && !p.IsDeleted).OrderBy(p => p.LineNumber).ToListAsync(ct);

        var renglones = lineas.Select(l =>
        {
            var suyos = generados.Where(t => t.DocumentLineId == l.Id).ToList();
            var p = productos.GetValueOrDefault(l.ProductId);
            return new TicketLineDto(p?.Code ?? string.Empty, p?.Name ?? string.Empty, l.Quantity, unidades.GetValueOrDefault(l.UnitId) ?? string.Empty,
                l.UnitPrice, l.DiscountAmount, l.NetAmount + suyos.Sum(t => t.Amount), suyos.FirstOrDefault()?.TaxRateCode ?? string.Empty);
        }).ToList();

        var pie = new List<string>();
        if (copia) pie.Add(MarcaDeCopia);
        if (opciones?.Value.AmbienteDePruebas == true) pie.Add(LeyendaDePruebas);

        var nit = empresa is null ? string.Empty : empresa.TaxIdCheckDigit is { Length: > 0 } dvEmpresa ? $"{empresa.TaxId}-{dvEmpresa}" : empresa.TaxId;
        return new TicketDto(
            formato == CashRegisterPrintFormat.Ticket58 ? CashRegisterPrintFormat.Ticket58 : formato == CashRegisterPrintFormat.Letter ? CashRegisterPrintFormat.Letter : CashRegisterPrintFormat.Ticket80,
            copia,
            new TicketHeaderDto(empresa?.Name ?? string.Empty, nit, sucursal, punto?.Address ?? empresa?.Address, null, string.Empty),
            new TicketDocumentDto(Etiqueta(documento.Class), documento.Prefix, documento.Number, documento.ConfirmedAt, caja, cajero, vendedor),
            comprador,
            renglones,
            generados.GroupBy(t => t.TaxRateCode).Select(g => new TicketTaxDto(g.Key, g.Sum(t => t.Base), g.Sum(t => t.Amount))).ToList(),
            tributos.Where(t => t.Treatment == TaxTreatment.WithholdingSuffered).GroupBy(t => t.TaxRateCode)
                .Select(g => new TicketWithholdingDto(g.Key, g.Sum(t => t.Amount))).ToList(),
            new TicketTotalsDto(documento.Subtotal, documento.DiscountTotal, documento.TaxTotal, documento.Total, documento.AmountDue),
            pagos.Select(p => new TicketPaymentDto(p.MeansName, p.Amount, p.Reference, p.Last4)).ToList(),
            pagos.Sum(p => p.ChangeGiven ?? 0m),
            null,
            pie);
    }

    /// <summary>El nombre que la tirilla da a la clase del documento.</summary>
    public static string Etiqueta(DocumentClass clase) => clase switch
    {
        DocumentClass.NonElectronicSalesReceipt => "Comprobante de venta",
        DocumentClass.NonElectronicSalesNote => "Nota al comprobante de venta",
        DocumentClass.PosEquivalentDocument => "Documento equivalente electrónico POS",
        DocumentClass.PosAdjustmentNote => "Nota de ajuste al documento equivalente POS",
        DocumentClass.SalesInvoice => "Factura electrónica de venta",
        DocumentClass.CreditNote => "Nota crédito",
        _ => VistaDeDocumentos.GrupoDe(clase, null).ToString(),
    };
}
