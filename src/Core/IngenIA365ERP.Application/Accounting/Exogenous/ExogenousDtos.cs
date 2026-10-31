using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Accounting.Exogenous;

/// <summary>Una columna de valor del formato: el campo con que la nombran las cuentas, su rótulo y su atributo en el XML.</summary>
public sealed record ColumnaDeExogena(string Field, string Label, string Xml);

/// <summary>
/// Cómo se escribe un formato (<c>ACC_ExogenousFormats.LayoutJson</c>, feature 009 E4): el elemento de cada fila del XML, el atributo del
/// concepto (nulo si el formato no lleva concepto), los atributos de identificación del tercero en orden y las columnas de valor (hasta
/// diez, que van a <c>Value1..Value10</c> de la línea). Es dato: un anexo nuevo se corrige sin programa.
/// </summary>
public sealed record DisposicionDeExogena(string Element, string? ConceptAttribute, IReadOnlyList<string> Identity, IReadOnlyList<ColumnaDeExogena> Columns)
{
    public const int MaximoDeColumnas = 10;

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public string ComoJson() => JsonSerializer.Serialize(this, Opciones);

    public static DisposicionDeExogena? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<DisposicionDeExogena>(json, Opciones); }
        catch (JsonException) { return null; }
    }

    /// <summary>La posición (0..9) de la columna <paramref name="field"/>, o -1.</summary>
    public int Indice(string field)
    {
        for (var i = 0; i < Columns.Count; i++)
            if (string.Equals(Columns[i].Field, field, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}

/// <summary>Qué suma una cuenta en una columna: movimiento de un lado, neto, base gravable o saldo al 31 de diciembre.</summary>
public static class SelectoresDeExogena
{
    public const string Debito = "Debit";
    public const string Credito = "Credit";
    public const string Neto = "Net";
    public const string Base = "Base";
    public const string BaseDebito = "BaseDebit";
    public const string BaseCredito = "BaseCredit";
    public const string Saldo = "Balance";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Debito, Credito, Neto, Base, BaseDebito, BaseCredito, Saldo };
}

public sealed record CuentaDeConceptoDto(string ValueField, string AccountPrefix, string Selector);

public sealed record ConceptoDeExogenaDto(string ConceptCode, string Name, IReadOnlyList<CuentaDeConceptoDto> Accounts);

public sealed record FormatoDeExogenaDto(
    Guid PublicId, int TaxYear, string FormatCode, int FormatVersion, string Name, bool Applies, string Origin, decimal MinAmount, bool MinorAmountsRule,
    string MinorAmountsTaxId, DisposicionDeExogena? Layout, IReadOnlyList<ConceptoDeExogenaDto> Concepts);

/// <summary>Una generación: versión, estado, totales e inconsistencias; el archivo exportado si ya se exportó.</summary>
public sealed record GeneracionDeExogenaDto(
    Guid PublicId, int TaxYear, string FormatCode, string FormatName, int Version, string Status, DateTime GeneratedAt, string GeneratedBy,
    DateTime? ExportedAt, string? ExportedBy, Guid? XmlAttachmentPublicId, int Lines, int IssueCount, int BlockingIssueCount);

/// <summary>Una inconsistencia de una línea: el tercero (con su PublicId para abrirlo en Personas), qué falla y si bloquea el XML.</summary>
public sealed record InconsistenciaDeExogenaDto(Guid? PersonPublicId, string IdNumber, string? Name, string ConceptCode, string Code, string Message, bool Blocking);

public sealed record ArchivoDeExogena(byte[] Content, string FileName);

/// <summary>Errores de la exógena (feature 009 E4, US11). Mensajes accionables (Principio VIII).</summary>
public static class ExogenousErrors
{
    public static readonly Error FormatNotFound = new("Accounting.Exogenous.FormatNotFound", "El formato no existe para ese año.");
    public static readonly Error RunNotFound = new("Accounting.Exogenous.RunNotFound", "La generación no existe.");
    public static readonly Error NotApplicable = new("Accounting.Exogenous.NotApplicable", "El formato está marcado «no aplica»: márquelo como aplicable para generarlo.");
    public static readonly Error LayoutMissing = new("Accounting.Exogenous.LayoutMissing", "El formato no tiene definidas sus columnas: complete la disposición antes de generar.");
    public static readonly Error SelectorUnknown = new("Accounting.Exogenous.SelectorUnknown",
        "Lo que suma una cuenta debe ser Debit, Credit, Net, Base, BaseDebit, BaseCredit o Balance.");
    public static readonly Error ColumnUnknown = new("Accounting.Exogenous.ColumnUnknown", "Una cuenta apunta a una columna que el formato no tiene.");
    public static readonly Error TooManyColumns = new("Accounting.Exogenous.TooManyColumns", "Un formato admite hasta diez columnas de valor.");
    public static readonly Error SeedNotFound = new("Accounting.Exogenous.SeedNotFound",
        "No hay semilla de exógena para ese año: copie la parametrización del año anterior y ajústela.");
    public static readonly Error CatalogWithoutSeed = new("Accounting.Exogenous.CatalogWithoutSeed",
        "La semilla trae cuentas propuestas para el catálogo solidario y el comercial: con otro catálogo los formatos se cargan sin cuentas.");
    public static readonly Error SourceYearEmpty = new("Accounting.Exogenous.SourceYearEmpty", "El año de origen no tiene formatos parametrizados.");
    public static readonly Error BlockingIssues = new("Accounting.Exogenous.BlockingIssues",
        "La generación tiene inconsistencias que impiden el archivo de la DIAN: corrija los terceros en Personas y vuelva a generar.");
    public static readonly Error RunNotWorking = new("Accounting.Exogenous.RunNotWorking", "Sólo se exporta la última generación.");
}
