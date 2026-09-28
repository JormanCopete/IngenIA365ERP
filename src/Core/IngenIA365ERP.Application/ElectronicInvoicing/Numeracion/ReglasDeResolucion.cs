using System.Text.RegularExpressions;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;

/// <summary>El estado calculado de una resolución a una fecha (api.md §24.2: texto calculado, no un enum guardado). (nuevo)</summary>
public enum EstadoDeResolucion
{
    Active = 1,
    NotYetValid = 2,
    Expired = 3,
    Exhausted = 4,
}

/// <summary>
/// Las reglas compartidas de las resoluciones de numeración DIAN (feature 012, I4, T705–T710; contracts/dian.md §9): qué tipo de
/// resolución numera cada tipo de documento, el prefijo, el estado calculado, la asociación al canal a una fecha y la clave técnica
/// enmascarada. Puras. (nuevo)
/// </summary>
public static partial class ReglasDeResolucion
{
    /// <summary>Hasta 4 letras o dígitos (Res. 165 art. 11 num. 4); puede ir vacío.</summary>
    [GeneratedRegex("^[A-Z0-9]{0,4}$")]
    private static partial Regex PatronDePrefijo();

    /// <summary>El prefijo normalizado: sin espacios alrededor y en mayúsculas.</summary>
    public static string Prefijo(string? prefijo) => (prefijo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>¿El prefijo (ya normalizado) es válido para una resolución?</summary>
    public static bool PrefijoValido(string prefijo) => PatronDePrefijo().IsMatch(prefijo);

    /// <summary>
    /// El tipo de resolución que numera un tipo de documento electrónico; nulo en las notas, que llevan su propio consecutivo por tipo y
    /// prefijo en la secuencia del módulo (§9) y en los eventos RADIAN.
    /// </summary>
    public static ResolutionKind? TipoDeResolucion(ElectronicDocumentKind tipo) => tipo switch
    {
        ElectronicDocumentKind.Invoice => ResolutionKind.Invoice,
        ElectronicDocumentKind.PosEquivalent => ResolutionKind.PosEquivalent,
        ElectronicDocumentKind.SupportDocument => ResolutionKind.SupportDocument,
        _ => null,
    };

    /// <summary>¿El tipo de documento es una nota (numera sin resolución)?</summary>
    public static bool EsNota(ElectronicDocumentKind tipo) => tipo is ElectronicDocumentKind.CreditNote or ElectronicDocumentKind.DebitNote
        or ElectronicDocumentKind.PosAdjustmentNote or ElectronicDocumentKind.SupportDocumentAdjustmentNote;

    /// <summary>El nombre del tipo de resolución para los mensajes.</summary>
    public static string Nombre(ResolutionKind tipo) => tipo switch
    {
        ResolutionKind.Invoice => "factura electrónica",
        ResolutionKind.PosEquivalent => "documento equivalente POS",
        ResolutionKind.SupportDocument => "documento soporte",
        ResolutionKind.Contingency => "contingencia",
        _ => tipo.ToString(),
    };

    /// <summary>El nombre del tipo de documento electrónico para los mensajes.</summary>
    public static string Nombre(ElectronicDocumentKind tipo) => tipo switch
    {
        ElectronicDocumentKind.Invoice => "factura electrónica",
        ElectronicDocumentKind.CreditNote => "nota crédito",
        ElectronicDocumentKind.DebitNote => "nota débito",
        ElectronicDocumentKind.PosEquivalent => "documento equivalente POS",
        ElectronicDocumentKind.PosAdjustmentNote => "nota de ajuste del documento equivalente",
        ElectronicDocumentKind.SupportDocument => "documento soporte",
        ElectronicDocumentKind.SupportDocumentAdjustmentNote => "nota de ajuste del documento soporte",
        _ => tipo.ToString(),
    };

    /// <summary>El ambiente para los mensajes.</summary>
    public static string Nombre(DianEnvironment ambiente) => ambiente == DianEnvironment.Production ? "de producción" : "de pruebas";

    /// <summary>El estado de la resolución a <paramref name="fecha"/>: la vigencia manda sobre el agotamiento.</summary>
    public static EstadoDeResolucion Estado(DianNumberingResolution r, DateOnly fecha) =>
        fecha < r.ValidFrom ? EstadoDeResolucion.NotYetValid
        : fecha > r.ValidTo ? EstadoDeResolucion.Expired
        : r.Agotada ? EstadoDeResolucion.Exhausted
        : EstadoDeResolucion.Active;

    /// <summary>La fracción del rango ya consumida (0 a 1).</summary>
    public static decimal FraccionConsumida(DianNumberingResolution r)
    {
        var total = r.RangeTo - r.RangeFrom + 1;
        if (total <= 0) return 0m;
        var emitidos = Math.Max(0, r.LastIssuedNumber - (r.RangeFrom - 1));
        return Math.Round((decimal)emitidos / total, 4);
    }

    /// <summary>Días que faltan para que venza (negativo si ya venció).</summary>
    public static int DiasParaVencer(DianNumberingResolution r, DateOnly fecha) => r.ValidTo.DayNumber - fecha.DayNumber;

    /// <summary>
    /// La asociación de la resolución al canal vigente a <paramref name="fecha"/>; en modo software propio, si la asociación nombra un
    /// software, tiene que ser el de la configuración (FR-064).
    /// </summary>
    public static DianResolutionChannel? AsociacionVigente(DianNumberingResolution r, string channelCode, string? softwareId, DateOnly fecha) =>
        r.Channels
            .Where(c => !c.IsDeleted && c.VigenteEn(fecha) && string.Equals(c.ChannelCode, channelCode, StringComparison.OrdinalIgnoreCase))
            .Where(c => c.SoftwareId is null || softwareId is null || string.Equals(c.SoftwareId, softwareId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.ValidFrom)
            .FirstOrDefault();

    /// <summary>La clave técnica enmascarada: sólo los 4 últimos caracteres (<c>••••ab12</c>).</summary>
    public static string? Enmascarar(string? clave) =>
        string.IsNullOrEmpty(clave) ? null : "••••" + (clave.Length <= 4 ? clave : clave[^4..]);

    /// <summary>El código del canal normalizado (mayúsculas, sin espacios alrededor).</summary>
    public static string Canal(string? channelCode) => (channelCode ?? string.Empty).Trim().ToUpperInvariant();
}
