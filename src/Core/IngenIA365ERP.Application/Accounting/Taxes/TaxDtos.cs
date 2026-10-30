using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Accounting.Taxes;

/// <summary>Un renglón de formulario tal como se guarda y se muestra (feature 009 E4, US10, FR-066).</summary>
public sealed record TaxFormLineDto(string LineCode, string Description, string Selector, string AccountPrefixes, short Sign, int Order);

public sealed record TaxFormDto(Guid PublicId, string Code, string Name, bool IsActive, IReadOnlyList<TaxFormLineDto> Lines);

/// <summary>Un certificado de retención expedido. <c>Status</c> dice <c>Outdated</c> si el libro cambió desde que se expidió (la huella no coincide).</summary>
public sealed record CertificadoDto(
    Guid PublicId,
    string TaxKind,
    long Number,
    int Year,
    byte? PeriodFrom,
    byte? PeriodTo,
    Guid PersonPublicId,
    string PersonName,
    string PersonTaxId,
    string? PersonEmail,
    DateTime IssuedAt,
    string IssuedBy,
    string Status,
    decimal TotalBase,
    decimal TotalAmount,
    DateTime? LastSentAt,
    string? LastSentTo,
    Guid? VoidedByPublicId);

public sealed record LineaDeCertificadoDto(string AccountCode, string ConceptCode, string ConceptName, decimal Base, decimal Rate, decimal Amount);

public sealed record CertificadoDetalleDto(CertificadoDto Certificate, IReadOnlyList<LineaDeCertificadoDto> Lines);

/// <summary>Lo que dejó una generación: cuántos certificados nuevos, cuántos terceros ya tenían uno vigente y cuántos quedaron sin retenciones.</summary>
public sealed record GeneracionDeCertificadosDto(int Issued, int AlreadyCurrent, int Outdated, IReadOnlyList<CertificadoDto> Certificates);

/// <summary>Errores de impuestos y certificados (feature 009 E4, US10). Mensajes accionables (Principio VIII).</summary>
public static class TaxErrors
{
    public static readonly Error FormNotFound = new("Accounting.Taxes.FormNotFound", "El formulario no existe.");
    public static readonly Error FormDuplicateLine = new("Accounting.Taxes.FormDuplicateLine", "Dos renglones del formulario tienen el mismo código.");
    public static readonly Error SelectorUnknown = new("Accounting.Taxes.SelectorUnknown",
        "Lo que suma un renglón debe ser Base, Amount, Debit, Credit, BaseDebit o BaseCredit.");
    public static readonly Error PrefixesRequired = new("Accounting.Taxes.PrefixesRequired", "Cada renglón necesita al menos un prefijo de cuenta (sólo dígitos, separados por coma).");
    public static readonly Error CatalogWithoutSuggestion = new("Accounting.Taxes.CatalogWithoutSuggestion",
        "El catálogo de la empresa no tiene formularios sugeridos: créelos a mano con sus renglones.");
    public static readonly Error KindNotCertifiable = new("Accounting.Certificate.KindNotCertifiable",
        "Sólo se certifican la retención en la fuente (Withholding), la de IVA (VatWithholding) y la de ICA (IcaWithholding).");
    public static readonly Error PeriodInvalid = new("Accounting.Certificate.PeriodInvalid", "El período debe ir de un mes a otro del mismo año, en orden.");
    public static readonly Error NotFound = new("Accounting.Certificate.NotFound", "El certificado no existe.");
    public static readonly Error Voided = new("Accounting.Certificate.Voided", "El certificado está anulado: use el que lo reemplazó.");
    public static readonly Error PersonWithoutEmail = new("Accounting.Certificate.PersonWithoutEmail",
        "El tercero no tiene correo registrado: regístrelo en Personas o entregue el PDF por otro medio.");
    public static readonly Error PersonNotFound = new("Accounting.Certificate.PersonNotFound", "El tercero no existe.");

    public static Error EmailNotConfigured(string descripcion) => new("Accounting.EmailNotConfigured",
        $"Este ambiente no tiene correo saliente configurado ({descripcion}). Descargue el certificado y entréguelo por otro medio.");
}

/// <summary>Las clases de impuesto en lenguaje del usuario y las que se certifican.</summary>
public static class ClasesDeImpuesto
{
    public static readonly IReadOnlySet<TaxKind> Certificables = new HashSet<TaxKind> { TaxKind.Withholding, TaxKind.VatWithholding, TaxKind.IcaWithholding };

    public static string Nombre(TaxKind kind) => kind switch
    {
        TaxKind.Withholding => "Retención en la fuente",
        TaxKind.VatWithholding => "Retención de IVA",
        TaxKind.IcaWithholding => "Retención de ICA",
        TaxKind.Vat => "IVA",
        TaxKind.Ica => "ICA",
        TaxKind.Gmf => "GMF",
        TaxKind.IncomeTax => "Impuesto de renta",
        _ => "Sin clase",
    };

    public static string Titulo(TaxKind kind) => kind switch
    {
        TaxKind.VatWithholding => "CERTIFICADO DE RETENCIÓN DE IVA",
        TaxKind.IcaWithholding => "CERTIFICADO DE RETENCIÓN DE INDUSTRIA Y COMERCIO",
        _ => "CERTIFICADO DE RETENCIÓN EN LA FUENTE",
    };
}
