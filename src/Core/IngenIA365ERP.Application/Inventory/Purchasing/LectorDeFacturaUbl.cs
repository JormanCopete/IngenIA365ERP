using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>Un impuesto de una línea leída del XML. (nuevo)</summary>
public sealed record ImpuestoLeido(string Code, decimal? Rate, decimal Amount);

/// <summary>Una línea leída del XML: códigos del proveedor y de barras, descripción, cantidad, unidad, precio, descuento e impuestos. (nuevo)</summary>
public sealed record LineaLeida(string? SupplierItemCode, string? Barcode, string Description, decimal Quantity, string? UnitCode,
    decimal UnitPrice, decimal Discount, IReadOnlyList<ImpuestoLeido> Taxes);

/// <summary>Lo que el lector saca del XML de la factura del proveedor. (nuevo)</summary>
public sealed record FacturaLeida(
    string SupplierTaxId,
    string SupplierName,
    string? Prefix,
    string Number,
    string? Cufe,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string PaymentForm,
    IReadOnlyList<LineaLeida> Lines,
    PrefillTotalsDto Totals);

/// <summary>
/// El lector de la factura electrónica del proveedor (feature 012, T345; api.md §14.4 <c>POST /prefill</c>; E10): lee en memoria
/// un XML UBL 2.1 de factura, el <c>AttachedDocument</c> que lo envuelve o el ZIP que trae cualquiera de los dos, y devuelve
/// prefijo, número, CUFE, fechas, forma de pago, líneas con sus impuestos y totales. <b>No guarda el archivo</b>: no conoce el
/// almacén de adjuntos. Un PDF, un archivo que no es XML o un XML que no es UBL: <c>Inventory.SupplierInvoice.FileUnreadable</c>;
/// una nota crédito o débito: <c>.NotAnInvoice</c>. (nuevo)
/// </summary>
public sealed class LectorDeFacturaUbl
{
    public const string EspacioFactura = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string EspacioNotaCredito = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    public const string EspacioNotaDebito = "urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2";
    public const string EspacioAdjunto = "urn:oasis:names:specification:ubl:schema:xsd:AttachedDocument-2";

    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Sts = "dian:gov:co:facturaelectronica:Structures-2-1";

    /// <summary>El tope de lo que se lee (un XML de factura no llega ni cerca).</summary>
    public const int TamanoMaximo = 10 * 1024 * 1024;

    public Result<FacturaLeida> Leer(byte[] contenido)
    {
        if (contenido.Length == 0) return Falla(ErroresDeCompras.FileUnreadable("El archivo está vacío."));
        if (contenido.Length > TamanoMaximo) return Falla(ErroresDeCompras.FileUnreadable("El archivo es demasiado grande."));
        if (EmpiezaCon(contenido, "%PDF")) return Falla(ErroresDeCompras.FileUnreadable("Es un PDF: suba el XML de la factura electrónica."));
        if (EmpiezaCon(contenido, "PK")) return DesdeZip(contenido);
        return DesdeXml(contenido, profundidad: 0);
    }

    private Result<FacturaLeida> DesdeZip(byte[] contenido)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(contenido), ZipArchiveMode.Read);
            var xml = zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.FullName, StringComparer.Ordinal).FirstOrDefault();
            if (xml is null) return Falla(ErroresDeCompras.FileUnreadable("El ZIP no trae ningún XML."));
            if (xml.Length > TamanoMaximo) return Falla(ErroresDeCompras.FileUnreadable("El XML del ZIP es demasiado grande."));
            using var entrada = xml.Open();
            using var memoria = new MemoryStream();
            entrada.CopyTo(memoria);
            return DesdeXml(memoria.ToArray(), profundidad: 0);
        }
        catch (InvalidDataException ex)
        {
            return Falla(ErroresDeCompras.FileUnreadable($"El ZIP está dañado: {ex.Message}"));
        }
    }

    private Result<FacturaLeida> DesdeXml(byte[] contenido, int profundidad)
    {
        XDocument documento;
        try
        {
            using var lector = XmlReader.Create(new MemoryStream(contenido), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            documento = XDocument.Load(lector);
        }
        catch (XmlException ex)
        {
            return Falla(ErroresDeCompras.FileUnreadable($"No es un XML válido: {ex.Message}"));
        }
        return Interpretar(documento, profundidad);
    }

    private Result<FacturaLeida> Interpretar(XDocument documento, int profundidad)
    {
        var raiz = documento.Root!;
        var espacio = raiz.Name.NamespaceName;
        if (espacio == EspacioAdjunto && profundidad == 0)
        {
            // El AttachedDocument trae la factura como texto en cac:Attachment/cac:ExternalReference/cbc:Description.
            var interno = raiz.Element(Cac + "Attachment")?.Element(Cac + "ExternalReference")?.Element(Cbc + "Description")?.Value;
            if (string.IsNullOrWhiteSpace(interno)) return Falla(ErroresDeCompras.FileUnreadable("El AttachedDocument no trae la factura."));
            return DesdeXml(Encoding.UTF8.GetBytes(interno.Trim()), profundidad + 1);
        }
        if (espacio == EspacioNotaCredito) return Falla(ErroresDeCompras.NotAnInvoice("nota crédito"));
        if (espacio == EspacioNotaDebito) return Falla(ErroresDeCompras.NotAnInvoice("nota débito"));
        if (espacio != EspacioFactura || raiz.Name.LocalName != "Invoice")
            return Falla(ErroresDeCompras.FileUnreadable("No es una factura electrónica UBL 2.1."));

        try
        {
            var id = Texto(raiz, Cbc + "ID") ?? throw new FormatException("La factura no trae su número (cbc:ID).");
            var prefijo = raiz.Descendants(Sts + "Prefix").Select(e => e.Value.Trim()).FirstOrDefault(p => p.Length > 0 && id.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            prefijo ??= new string(id.TakeWhile(c => !char.IsDigit(c)).ToArray());
            var numero = id[prefijo.Length..];

            var medio = raiz.Element(Cac + "PaymentMeans");
            var formaDePago = Texto(medio, Cbc + "ID") == "2" ? "Credit" : "Cash";
            var vence = Fecha(Texto(raiz, Cbc + "DueDate") ?? Texto(medio, Cbc + "PaymentDueDate"));

            var proveedor = raiz.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party");
            var esquema = proveedor?.Element(Cac + "PartyTaxScheme");
            var nit = Texto(esquema, Cbc + "CompanyID") ?? Texto(proveedor?.Element(Cac + "PartyLegalEntity"), Cbc + "CompanyID") ?? string.Empty;
            var nombre = Texto(esquema, Cbc + "RegistrationName") ?? Texto(proveedor?.Element(Cac + "PartyLegalEntity"), Cbc + "RegistrationName") ?? string.Empty;

            var lineas = raiz.Elements(Cac + "InvoiceLine").Select(l =>
            {
                var cantidad = l.Element(Cbc + "InvoicedQuantity");
                var item = l.Element(Cac + "Item");
                var descuento = l.Elements(Cac + "AllowanceCharge")
                    .Where(a => Texto(a, Cbc + "ChargeIndicator") == "false")
                    .Sum(a => Decimal(Texto(a, Cbc + "Amount")));
                var impuestos = l.Elements(Cac + "TaxTotal").Elements(Cac + "TaxSubtotal").Select(s =>
                {
                    var categoria = s.Element(Cac + "TaxCategory");
                    var porcentaje = Texto(categoria, Cbc + "Percent");
                    return new ImpuestoLeido(Texto(categoria?.Element(Cac + "TaxScheme"), Cbc + "ID") ?? string.Empty,
                        porcentaje is null ? null : Decimal(porcentaje) / 100m, Decimal(Texto(s, Cbc + "TaxAmount")));
                }).ToList();
                return new LineaLeida(
                    Texto(item?.Element(Cac + "SellersItemIdentification"), Cbc + "ID"),
                    Texto(item?.Element(Cac + "StandardItemIdentification"), Cbc + "ID"),
                    Texto(item, Cbc + "Description") ?? string.Empty,
                    Decimal(cantidad?.Value),
                    cantidad?.Attribute("unitCode")?.Value,
                    Decimal(Texto(l.Element(Cac + "Price"), Cbc + "PriceAmount")),
                    descuento,
                    impuestos);
            }).ToList();

            var totales = raiz.Element(Cac + "LegalMonetaryTotal");
            return Result.Success(new FacturaLeida(
                nit.Trim(), nombre.Trim(),
                string.IsNullOrEmpty(prefijo) ? null : prefijo,
                numero,
                Texto(raiz, Cbc + "UUID")?.ToLowerInvariant(),
                Fecha(Texto(raiz, Cbc + "IssueDate")) ?? throw new FormatException("La factura no trae su fecha de emisión."),
                vence,
                formaDePago,
                lineas,
                new PrefillTotalsDto(Decimal(Texto(totales, Cbc + "LineExtensionAmount")), Decimal(Texto(totales, Cbc + "TaxExclusiveAmount")),
                    Decimal(Texto(totales, Cbc + "TaxInclusiveAmount")), Decimal(Texto(totales, Cbc + "PayableAmount")))));
        }
        catch (FormatException ex)
        {
            return Falla(ErroresDeCompras.FileUnreadable(ex.Message));
        }
    }

    private static string? Texto(XElement? padre, XName nombre) =>
        padre?.Element(nombre)?.Value is { } v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static decimal Decimal(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? 0m : decimal.Parse(texto.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static DateOnly? Fecha(string? texto) =>
        texto is null ? null : DateOnly.ParseExact(texto.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool EmpiezaCon(byte[] contenido, string firma) =>
        contenido.Length >= firma.Length && Encoding.ASCII.GetString(contenido, 0, firma.Length) == firma;

    private static Result<FacturaLeida> Falla(Error error) => Result.Failure<FacturaLeida>(error);
}
