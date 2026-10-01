using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

/// <summary>
/// Un evento RADIAN que emite la cooperativa como adquirente (feature 012; I4, T700 la declaró en <c>Channels</c>; I5, T803 la completó y
/// la trajo aquí; contracts/dian.md §3.1, §4.3 y §14.3): 030 acuse de recibo y 032 recibo del bien. Lo arma el único constructor
/// (<c>ConstructorDelCanonico.ConstruirEventoAsync</c>) desde la entrada neutral que entrega el módulo fuente
/// (<see cref="EntradaDeEventoRadian"/>) y lo sellado en el documento electrónico; es determinista (<c>SerializadorCanonico</c>), así el
/// procesador lo vuelve a armar y lo compara con el SHA-256 de su versión antes de transmitirlo. (nuevo)
/// </summary>
/// <param name="SchemaVersion">Versión de la forma; 1.</param>
/// <param name="Kind"><see cref="ElectronicDocumentKind.RadianEvent030"/> o <see cref="ElectronicDocumentKind.RadianEvent032"/>.</param>
/// <param name="DianDocumentTypeCode">Tipo DIAN del <c>ApplicationResponse</c> (96), de <c>CatalogoDian</c>.</param>
/// <param name="EventCode">Código del evento (030, 032), de <c>CatalogoDian</c>.</param>
/// <param name="Environment">Ambiente sellado.</param>
/// <param name="Number">Numeración propia del evento (prefijo fijo por tipo y consecutivo por ambiente y prefijo, T802).</param>
/// <param name="IssuedAt">Fecha y hora del evento, con −05:00 y sin fracciones.</param>
/// <param name="Issuer">La cooperativa: receptora de la factura y emisora del evento, con su perfil tributario.</param>
/// <param name="Supplier">El proveedor que expidió la factura.</param>
/// <param name="ReferencedInvoice">La factura del proveedor: número, CUFE y fecha de emisión.</param>
/// <param name="Receipt">El 032: la recepción confirmada enlazada a la factura (número y fecha); nula en el 030.</param>
/// <param name="IssuedBy">Quién pidió emitirlo (el usuario de la plataforma).</param>
/// <param name="Origin">De qué documento comercial sale (el registro de la factura del proveedor).</param>
/// <param name="Notes">Notas libres.</param>
public sealed record EventoRadianCanonico(
    int SchemaVersion,
    ElectronicDocumentKind Kind,
    string DianDocumentTypeCode,
    string EventCode,
    DianEnvironment Environment,
    NumeroCanonico Number,
    DateTimeOffset IssuedAt,
    ParteCanonica Issuer,
    ParteCanonica Supplier,
    DocumentoCorregidoCanonico ReferencedInvoice,
    RecepcionCanonica? Receipt,
    string IssuedBy,
    OrigenCanonico Origin,
    IReadOnlyList<string> Notes);

/// <summary>La recepción del bien que respalda el 032: número visible y fecha de la operación. (nuevo)</summary>
public sealed record RecepcionCanonica(string Number, DateOnly Date);

/// <summary>
/// La entrada neutral de un evento RADIAN (feature 012, I5, T803): lo que el módulo fuente sabe de la factura del proveedor y de su
/// recepción, sin nada de la plataforma (numeración, ambiente, emisor). La entrega <c>IFuenteDeDocumentoElectronico.LeerEventoRadianAsync</c>
/// —y, al pedir la emisión, <c>PrepararEventosRadianAsync</c>— con los mismos datos, para que las dos construcciones den los mismos bytes.
/// (nuevo)
/// </summary>
/// <param name="SourceModule">«INV».</param>
/// <param name="DocumentPublicId">El registro de la factura del proveedor (<c>INV_Documents</c>).</param>
/// <param name="DocumentClass">Su clase («SupplierInvoice»).</param>
/// <param name="DocumentTypeCode">El código de su tipo de documento (para la bandeja; máx. 10).</param>
/// <param name="DocumentNumber">El número visible del registro.</param>
/// <param name="Kind">030 o 032.</param>
/// <param name="InvoiceNumber">El número de la factura del proveedor (prefijo + número).</param>
/// <param name="InvoiceCufe">Su CUFE; sin él no hay evento (<c>ElectronicInvoicing.Document.MissingData</c>).</param>
/// <param name="InvoiceIssueDate">Su fecha de emisión.</param>
/// <param name="Supplier">El proveedor, de la copia fiscal del registro (o del maestro si el registro no la tiene).</param>
/// <param name="ReceiptNumber">El 032: la recepción confirmada enlazada (la primera por número de registro).</param>
/// <param name="ReceiptDate">Su fecha de operación.</param>
/// <param name="IssuedBy">Quién pidió emitirlo.</param>
public sealed record EntradaDeEventoRadian(
    string SourceModule,
    Guid DocumentPublicId,
    string DocumentClass,
    string DocumentTypeCode,
    string DocumentNumber,
    ElectronicDocumentKind Kind,
    string InvoiceNumber,
    string? InvoiceCufe,
    DateOnly InvoiceIssueDate,
    ProveedorDelEvento Supplier,
    string? ReceiptNumber,
    DateOnly? ReceiptDate,
    string IssuedBy);

/// <summary>El proveedor tal como lo ve el evento: identificación, dígito, tipo DIAN de identificación y de persona, y nombre. (nuevo)</summary>
public sealed record ProveedorDelEvento(string TaxId, string? CheckDigit, string IdTypeCode, string PersonTypeCode, string Name);
