using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

/// <summary>
/// El modelo canónico v1 de un documento electrónico (feature 012, I4, T701; contracts/dian.md §4.2): uno solo, independiente
/// del proveedor, con los códigos DIAN ya resueltos. Lo arma <b>sólo</b> <see cref="ConstructorDelCanonico"/>; cada adaptador lo
/// traduce a su formato. Es la evidencia de qué se mandó: <see cref="SerializadorCanonico"/> lo escribe en JSON canónico y su
/// SHA-256 queda en la versión del documento y en cada transmisión.
///
/// <para>
/// Las escalas son parte del contrato (T19): montos (18,2), cantidades (18,4), precio unitario (18,6) y tarifas como
/// <b>fracción</b> (9,6); las fija el atributo de cada propiedad. Los nombres de las propiedades son los del JSON de §4.2 en
/// PascalCase (el serializador los escribe en camelCase). (nuevo)
/// </para>
/// </summary>
public sealed record DocumentoElectronicoCanonico
{
    /// <summary>La versión del esquema que escribe este programa.</summary>
    public const int VersionDelEsquema = 1;

    public int SchemaVersion { get; init; } = VersionDelEsquema;

    public ElectronicDocumentKind Kind { get; init; }

    /// <summary>Por tipo y contingencia (tabla §4.3), de <c>CatalogoDian</c>.</summary>
    public string DianDocumentTypeCode { get; init; } = string.Empty;

    /// <summary>Tipo de operación del anexo, por tipo de documento, de <c>CatalogoDian</c>.</summary>
    public string OperationTypeCode { get; init; } = string.Empty;

    public DianEnvironment Environment { get; init; }

    public NumeroCanonico Number { get; init; } = new(string.Empty, 0, string.Empty);

    /// <summary>Nula en las notas: no tienen resolución.</summary>
    public ResolucionCanonica? Resolution { get; init; }

    /// <summary>Fecha y hora locales de la operación (-05:00).</summary>
    public DateTimeOffset IssuedAt { get; init; }

    /// <summary>Sólo con forma de pago crédito (y entonces obligatoria).</summary>
    public DateOnly? DueDate { get; init; }

    public string Currency { get; init; } = string.Empty;

    [JsonConverter(typeof(EscalaDeTarifa))]
    public decimal ExchangeRate { get; init; } = 1m;

    /// <summary>«Cash» o «Credit»: se deriva de la clase de los medios.</summary>
    public string PaymentForm { get; init; } = string.Empty;

    public IReadOnlyList<PagoCanonico> Payments { get; init; } = [];

    public ParteCanonica Issuer { get; init; } = ParteCanonica.Vacia;

    /// <summary>Comprador (ventas) o proveedor (documento soporte), de la copia fiscal vigente.</summary>
    public ParteCanonica Counterparty { get; init; } = ParteCanonica.Vacia;

    public IReadOnlyList<LineaCanonica> Lines { get; init; } = [];

    /// <summary>Informativas: las que practica el comprador agente retenedor (T26). Nunca como medio de pago.</summary>
    public IReadOnlyList<RetencionCanonica> Withholdings { get; init; } = [];

    public TotalesCanonicos Totals { get; init; } = new();

    public ReferenciasCanonicas References { get; init; } = new();

    /// <summary>En la transmisión de un documento de contingencia 03 (papel).</summary>
    public ContingenciaCanonica? Contingency { get; init; }

    /// <summary>Documento equivalente POS.</summary>
    public BloquePosCanonico? Pos { get; init; }

    /// <summary>Documento soporte.</summary>
    public DocumentoSoporteCanonico? SupportDocument { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public OrigenCanonico Source { get; init; } = new(string.Empty, Guid.Empty, string.Empty, string.Empty);
}

/// <summary>Número fiscal: prefijo, consecutivo y completo. (nuevo)</summary>
public sealed record NumeroCanonico(string Prefix, long Consecutive, string Full);

/// <summary>La resolución de numeración con que se numeró. (nuevo)</summary>
public sealed record ResolucionCanonica(string Number, DateOnly Date, long RangeFrom, long RangeTo, DateOnly ValidFrom, DateOnly ValidTo);

/// <summary>Un pago con su medio DIAN. La retención nunca es un pago. (nuevo)</summary>
public sealed record PagoCanonico(
    string DianPaymentMeansCode,
    [property: JsonConverter(typeof(EscalaDeMonto))] decimal Amount,
    string? Reference);

/// <summary>Dirección de una parte. (nuevo)</summary>
public sealed record DireccionCanonica(string? Line, string? CityDaneCode, string? CountryCode);

/// <summary>
/// Emisor o contraparte (contracts/dian.md §4.2, <c>issuer</c> y <c>counterparty</c>). En el emisor <see cref="Role"/>,
/// <see cref="IsFinalConsumer"/>, <see cref="ReceptionEmail"/> y <see cref="PartySnapshotVersion"/> van nulos. (nuevo)
/// </summary>
public sealed record ParteCanonica
{
    public static readonly ParteCanonica Vacia = new();

    /// <summary>«Buyer» (ventas) o «Supplier» (documento soporte); nulo en el emisor.</summary>
    public string? Role { get; init; }

    public bool? IsFinalConsumer { get; init; }

    public string TaxId { get; init; } = string.Empty;

    public string? CheckDigit { get; init; }

    public string IdTypeCode { get; init; } = string.Empty;

    /// <summary>1 jurídica, 2 natural (de la copia fiscal).</summary>
    public string PersonTypeCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<string> Responsibilities { get; init; } = [];

    public string TaxSchemeCode { get; init; } = string.Empty;

    public DireccionCanonica Address { get; init; } = new(null, null, null);

    public string? Ciiu { get; init; }

    /// <summary>Correo del emisor.</summary>
    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>Correo de recepción del adquirente.</summary>
    public string? ReceptionEmail { get; init; }

    /// <summary>Qué versión de la copia fiscal (<c>INV_DocumentPartySnapshots</c>) se usó.</summary>
    public int? PartySnapshotVersion { get; init; }
}

/// <summary>Una línea: cantidad (18,4), precio unitario (18,6), base bruta (18,2), descuentos e impuestos. (nuevo)</summary>
public sealed record LineaCanonica
{
    public int LineNumber { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    [JsonConverter(typeof(EscalaDeCantidad))]
    public decimal Quantity { get; init; }

    /// <summary>Unidad UN/ECE Rec. 20.</summary>
    public string UnitCode { get; init; } = string.Empty;

    [JsonConverter(typeof(EscalaDePrecio))]
    public decimal UnitPrice { get; init; }

    /// <summary>Cantidad × precio, antes de descuentos.</summary>
    [JsonConverter(typeof(EscalaDeMonto))]
    public decimal LineExtension { get; init; }

    public IReadOnlyList<DescuentoCanonico> Allowances { get; init; } = [];

    public IReadOnlyList<ImpuestoCanonico> Taxes { get; init; } = [];
}

/// <summary>Un descuento de línea: porcentaje como fracción (9,6), base y valor. (nuevo)</summary>
public sealed record DescuentoCanonico(
    string? ReasonCode,
    [property: JsonConverter(typeof(EscalaDeTarifa))] decimal? Percent,
    [property: JsonConverter(typeof(EscalaDeMonto))] decimal Base,
    [property: JsonConverter(typeof(EscalaDeMonto))] decimal Amount);

/// <summary>Un impuesto de línea, redondeado por línea. (nuevo)</summary>
public sealed record ImpuestoCanonico(
    string DianTaxCode,
    [property: JsonConverter(typeof(EscalaDeTarifa))] decimal? Rate,
    [property: JsonConverter(typeof(EscalaDeMonto))] decimal? AmountPerUnit,
    [property: JsonConverter(typeof(EscalaDeCantidad))] decimal? TaxableUnits,
    [property: JsonConverter(typeof(EscalaDeMonto))] decimal Base,
    [property: JsonConverter(typeof(EscalaDeMonto))] decimal Amount);

/// <summary>Una retención del documento (informativa). (nuevo)</summary>
public sealed record RetencionCanonica(
    string DianTaxCode,
    [property: JsonConverter(typeof(EscalaDeTarifa))] decimal? Rate,
    [property: JsonConverter(typeof(EscalaDeMonto))] decimal Base,
    [property: JsonConverter(typeof(EscalaDeMonto))] decimal Amount);

/// <summary>
/// Los totales (contracts/dian.md §4.2): <see cref="Payable"/> es el <c>Total</c> del documento y <see cref="AmountDue"/> su
/// <c>AmountDue</c> (T26); <see cref="Rounding"/> es lo que separa el total del documento de la suma de las líneas. (nuevo)
/// </summary>
public sealed record TotalesCanonicos
{
    [JsonConverter(typeof(EscalaDeMonto))] public decimal LineExtension { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal Allowances { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal TaxExclusive { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal Taxes { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal TaxInclusive { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal Charges { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal Rounding { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal Payable { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal Withholdings { get; init; }
    [JsonConverter(typeof(EscalaDeMonto))] public decimal AmountDue { get; init; }
}

/// <summary>Referencias: el documento corregido (notas), el pedido y las remisiones. (nuevo)</summary>
public sealed record ReferenciasCanonicas
{
    public DocumentoCorregidoCanonico? Corrected { get; init; }

    public string? Order { get; init; }

    public IReadOnlyList<string> Despatches { get; init; } = [];
}

/// <summary>El documento que corrige una nota: número, código único, fecha y concepto de corrección. (nuevo)</summary>
public sealed record DocumentoCorregidoCanonico(string Number, string? UniqueCode, DateOnly IssueDate, string? CorrectionConceptCode);

/// <summary>Contingencia 03 en la transmisión del documento de papel. (nuevo)</summary>
public sealed record ContingenciaCanonica(string Type, string PaperNumber, DateTimeOffset PaperIssuedAt);

/// <summary>Bloque del documento equivalente POS. (nuevo)</summary>
public sealed record BloquePosCanonico(
    string? CashRegisterPlate,
    string? Location,
    string? CashierName,
    string? CashRegisterTypeCode,
    SoftwarePosCanonico? Software);

/// <summary>El software del POS que pide el DEE. (nuevo)</summary>
public sealed record SoftwarePosCanonico(string Name, string ManufacturerTaxId, string ManufacturerName);

/// <summary>Bloque del documento soporte: por operación o semanal (Res. 167/2021). (nuevo)</summary>
public sealed record DocumentoSoporteCanonico(string Generation, DateOnly? PeriodFrom, DateOnly? PeriodTo);

/// <summary>De qué documento comercial sale: módulo, <c>PublicId</c>, clase y número visible. (nuevo)</summary>
public sealed record OrigenCanonico(string Module, Guid DocumentPublicId, string DocumentClass, string DocumentNumber);
