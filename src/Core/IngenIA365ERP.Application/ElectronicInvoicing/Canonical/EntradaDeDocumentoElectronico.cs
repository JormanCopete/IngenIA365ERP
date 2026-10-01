using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

/// <summary>
/// La entrada <b>neutral</b> con que el módulo fuente entrega su documento comercial ya confirmado a la plataforma (feature
/// 012, I4, T701; contracts/dian.md §4.1): líneas, fotos fiscales de la contraparte con su versión, foto tributaria, pagos,
/// referencias y bloques POS y DS. La plataforma nunca lee tablas del módulo (<c>INV_</c>): recibe esto por
/// <see cref="IFuenteDeDocumentoElectronico"/> y lo completa con lo suyo en <see cref="ConstructorDelCanonico"/>. Sin códigos
/// DIAN resueltos salvo los que el módulo ya copió al confirmar (unidad, medio de pago, tributo), que el constructor valida
/// contra <c>CatalogoDian</c>. (nuevo)
/// </summary>
public sealed record EntradaDeDocumentoElectronico
{
    /// <summary>Módulo fuente («INV»).</summary>
    public string SourceModule { get; init; } = string.Empty;

    public Guid DocumentPublicId { get; init; }

    /// <summary>Clase del documento comercial por nombre («SalesInvoice», «CreditNote», «SupportDocument»…).</summary>
    public string DocumentClass { get; init; } = string.Empty;

    public string DocumentTypeCode { get; init; } = string.Empty;

    /// <summary>Número visible del documento comercial.</summary>
    public string DocumentNumber { get; init; } = string.Empty;

    public ElectronicDocumentKind Kind { get; init; }

    public DateOnly OperationDate { get; init; }

    /// <summary>Cuándo se confirmó (UTC); el canónico lo escribe con el desfase local.</summary>
    public DateTime ConfirmedAtUtc { get; init; }

    public DateOnly? DueDate { get; init; }

    public string Currency { get; init; } = string.Empty;

    public decimal ExchangeRate { get; init; } = 1m;

    /// <summary>
    /// Todas las versiones de la copia fiscal de la contraparte (<c>INV_DocumentPartySnapshots</c>); el constructor usa la de
    /// mayor versión (FR-011, T52). Vacía si el documento no tiene contraparte (se usa el consumidor final del catálogo).
    /// </summary>
    public IReadOnlyList<FotoFiscalDeEntrada> Contrapartes { get; init; } = [];

    public IReadOnlyList<LineaDeEntrada> Lineas { get; init; } = [];

    /// <summary>La foto tributaria (<c>INV_DocumentTaxLines</c>): impuestos y retenciones, por línea o del documento.</summary>
    public IReadOnlyList<ImpuestoDeEntrada> Impuestos { get; init; } = [];

    public IReadOnlyList<PagoDeEntrada> Pagos { get; init; } = [];

    public TotalesDeEntrada Totales { get; init; } = new(0m, 0m, 0m, 0m, 0m, 0m);

    /// <summary>En notas: el documento comercial corregido y el concepto de corrección.</summary>
    public CorreccionDeEntrada? Correccion { get; init; }

    public string? OrderReference { get; init; }

    public IReadOnlyList<string> Despatches { get; init; } = [];

    /// <summary>Documento equivalente POS.</summary>
    public PosDeEntrada? Pos { get; init; }

    /// <summary>Documento soporte: por operación o semanal, con su período.</summary>
    public DocumentoSoporteDeEntrada? DocumentoSoporte { get; init; }

    public IReadOnlyList<string> Notas { get; init; } = [];
}

/// <summary>Una versión de la copia fiscal de la contraparte, tal como quedó al confirmar (o al corregir por el caso a). (nuevo)</summary>
public sealed record FotoFiscalDeEntrada
{
    public int Version { get; init; } = 1;

    /// <summary>«1» jurídica, «2» natural.</summary>
    public string OrganizationType { get; init; } = string.Empty;

    public string IdTypeCode { get; init; } = string.Empty;

    public string TaxId { get; init; } = string.Empty;

    public string? CheckDigit { get; init; }

    public string LegalName { get; init; } = string.Empty;

    public string? Address { get; init; }

    public string? MunicipalityDaneCode { get; init; }

    public string? CountryCode { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>Responsabilidades ya fijadas en la foto (separadas por «;»); si faltan, se derivan de las marcas.</summary>
    public string? Responsibilities { get; init; }

    public string? TaxSchemeCode { get; init; }

    public bool IsVatResponsible { get; init; }

    public bool IsLargeContributor { get; init; }

    public bool IsSelfWithholder { get; init; }

    public bool IsVatWithholdingAgent { get; init; }

    public bool IsSimpleTaxRegime { get; init; }

    public string? CiiuCode { get; init; }
}

/// <summary>Una línea del documento comercial. <see cref="DianUnitCode"/> nulo es un dato faltante. (nuevo)</summary>
public sealed record LineaDeEntrada(
    int LineNumber,
    string ProductCode,
    string Description,
    decimal Quantity,
    string UnitCode,
    string? DianUnitCode,
    decimal UnitPrice,
    decimal GrossAmount,
    decimal DiscountAmount);

/// <summary>
/// Un renglón de la foto tributaria. <see cref="LineNumber"/> nulo = renglón del documento. <see cref="EsRetencion"/> separa
/// las retenciones (informativas) de los impuestos de la línea; <see cref="Rate"/> es fracción. (nuevo)
/// </summary>
public sealed record ImpuestoDeEntrada(
    int? LineNumber,
    string TaxRateCode,
    string? DianTaxCode,
    bool EsRetencion,
    decimal? Rate,
    decimal? AmountPerUnit,
    decimal? TaxableUnits,
    decimal Base,
    decimal Amount);

/// <summary>Un pago recibido o hecho, con su clase y el medio DIAN que se copió al confirmar. (nuevo)</summary>
public sealed record PagoDeEntrada(
    string MeansCode,
    string MeansName,
    PaymentMeansClass MeansClass,
    string? DianPaymentMeansCode,
    decimal Amount,
    string? Reference);

/// <summary>Los totales del documento comercial (T26). (nuevo)</summary>
public sealed record TotalesDeEntrada(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal WithholdingTotal, decimal Total, decimal AmountDue);

/// <summary>La nota: qué documento comercial corrige y con qué concepto de corrección. (nuevo)</summary>
public sealed record CorreccionDeEntrada(Guid CorrectedDocumentPublicId, string CorrectedDocumentNumber, DateOnly CorrectedIssueDate, string? CorrectionConceptCode);

/// <summary>Los datos de la caja para el DEE. (nuevo)</summary>
public sealed record PosDeEntrada(string? CashRegisterPlate, string? CashRegisterTypeCode, string? Location, string? CashierName);

/// <summary>La generación del documento soporte. (nuevo)</summary>
public sealed record DocumentoSoporteDeEntrada(string Generation, DateOnly? PeriodFrom, DateOnly? PeriodTo);
