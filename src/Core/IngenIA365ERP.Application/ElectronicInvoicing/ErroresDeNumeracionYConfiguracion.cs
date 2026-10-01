using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.ElectronicInvoicing;

/// <summary>
/// Los errores de la numeración fiscal, de las resoluciones y de la configuración de emisión (feature 012, I4, T705–T710; contracts/dian.md
/// §9 y §10; api.md §24.1, §24.2 y §24.8). Aparte de <see cref="ErroresDeFacturacionElectronica"/>, que es de los documentos. (nuevo)
/// </summary>
public static class ErroresDeNumeracionYConfiguracion
{
    // ---------------------------------------------------------------------------------------- numeración (§9) --
    public const string ResolutionUnavailableCode = "Inventory.Numbering.ResolutionUnavailable";
    public const string ResolutionExpiredCode = "ElectronicInvoicing.Resolution.Expired";
    public const string ResolutionExhaustedCode = "ElectronicInvoicing.Resolution.Exhausted";

    // -------------------------------------------------------------------------------------- resoluciones (§24.2) --
    public const string ResolutionNotFoundCode = "ElectronicInvoicing.Resolution.NotFound";
    public const string PrefixInvalidCode = "ElectronicInvoicing.Resolution.PrefixInvalid";
    public const string RangeInvalidCode = "ElectronicInvoicing.Resolution.RangeInvalid";
    public const string ResolutionOverlapsCode = "ElectronicInvoicing.Resolution.Overlaps";
    public const string BackedKindRequiredCode = "ElectronicInvoicing.Resolution.BackedKindRequired";
    public const string TechnicalKeyNotAllowedCode = "ElectronicInvoicing.Resolution.TechnicalKeyNotAllowed";
    public const string TechnicalKeyUnavailableCode = "ElectronicInvoicing.Resolution.TechnicalKeyUnavailable";
    public const string ResolutionInUseCode = "ElectronicInvoicing.Resolution.InUse";
    public const string PrefixInUseCode = "ElectronicInvoicing.Resolution.PrefixInUse";

    // ------------------------------------------------------------------------------------ configuración (§24.1) --
    public const string ChannelUnknownCode = "ElectronicInvoicing.Settings.ChannelUnknown";
    public const string ChannelRejectsErpNumberCode = "ElectronicInvoicing.Settings.ChannelRejectsErpNumber";
    public const string SoftwareIdRequiredCode = "ElectronicInvoicing.Settings.SoftwareIdRequired";
    public const string NoResolutionForChannelCode = "ElectronicInvoicing.Settings.NoResolutionForChannel";
    public const string SimulatedInProductionCode = "ElectronicInvoicing.Settings.SimulatedInProduction";
    public const string ChannelCannotSendEmailCode = "ElectronicInvoicing.Settings.ChannelCannotSendEmail";
    public const string SettingsOverlapsCode = "ElectronicInvoicing.Settings.Overlaps";
    public const string SettingsMissingCode = "ElectronicInvoicing.Settings.Missing";
    public const string CredentialMismatchCode = "ElectronicInvoicing.CredentialMismatch";

    /// <summary>Sin resolución vigente del tipo, de ese prefijo y ambiente, o no asociada al canal sellado a la fecha.</summary>
    public static Error ResolutionUnavailable(string tipo, string prefijo, string ambiente, DateOnly fecha, string canal) =>
        new ErrorConDatos(ResolutionUnavailableCode,
            $"No hay una resolución de numeración de {tipo} con prefijo «{prefijo}» en el ambiente {ambiente}, vigente el {fecha:dd/MM/yyyy} y asociada al canal {canal}. Regístrela o asóciela en Maestros › Resoluciones DIAN.",
            new { kind = tipo, prefix = prefijo, environment = ambiente, date = fecha, channelCode = canal });

    /// <summary>La resolución que correspondía ya venció a la fecha de operación.</summary>
    public static Error ResolutionExpired(string numero, string prefijo, DateOnly validTo) =>
        new ErrorConDatos(ResolutionExpiredCode,
            $"La resolución {numero} (prefijo «{prefijo}») venció el {validTo:dd/MM/yyyy}. Registre la nueva resolución antes de seguir vendiendo.",
            new { resolutionNumber = numero, prefix = prefijo, validTo });

    /// <summary>La resolución que correspondía no tiene más números.</summary>
    public static Error ResolutionExhausted(string numero, string prefijo, long rangeTo) =>
        new ErrorConDatos(ResolutionExhaustedCode,
            $"La resolución {numero} (prefijo «{prefijo}») agotó su rango (último número {rangeTo}). Registre la nueva resolución antes de seguir vendiendo.",
            new { resolutionNumber = numero, prefix = prefijo, rangeTo });

    public static Error ResolutionNotFound() => new(ResolutionNotFoundCode, "La resolución de numeración no existe.");

    public static Error PrefixInvalid() =>
        new(PrefixInvalidCode, "El prefijo de la resolución tiene hasta 4 letras o dígitos, sin espacios ni signos.");

    public static Error RangeInvalid() =>
        new(RangeInvalidCode, "El rango empieza en 1 o más, el número inicial no supera al final y la vigencia termina después de empezar.");

    public static Error ResolutionOverlaps(string numero) =>
        new ErrorConDatos(ResolutionOverlapsCode,
            $"La resolución se cruza con la {numero}: mismo tipo, prefijo y ambiente, con rangos o vigencias que se superponen (o el mismo número de resolución).",
            new { resolutionNumber = numero });

    public static Error BackedKindRequired() =>
        new(BackedKindRequiredCode, "Una resolución de contingencia indica qué tipo respalda: factura, documento equivalente POS o documento soporte.");

    public static Error TechnicalKeyNotAllowed() =>
        new(TechnicalKeyNotAllowedCode, "La clave técnica sólo existe en las resoluciones de factura electrónica.");

    public static Error TechnicalKeyUnavailable(string canal) =>
        new ErrorConDatos(TechnicalKeyUnavailableCode,
            $"El canal {canal} no ofrece la consulta de rangos o no devolvió la clave técnica de esta resolución: digítela.",
            new { channelCode = canal });

    public static Error ResolutionInUse(long lastIssuedNumber) =>
        new ErrorConDatos(ResolutionInUseCode,
            "La resolución ya numeró documentos: sólo se puede adelantar su fecha final para retirarla. Si cambió el prefijo, el rango o el ambiente, registre otra.",
            new { lastIssuedNumber });

    public static Error PrefixInUse(string prefijo, string usadoPor) =>
        new ErrorConDatos(PrefixInUseCode,
            $"El prefijo «{prefijo}» ya lo usa {usadoPor}: las notas y las resoluciones comparten la numeración por prefijo y no pueden coincidir.",
            new { prefix = prefijo, usedBy = usadoPor });

    public static Error ChannelUnknown(string canal) =>
        new ErrorConDatos(ChannelUnknownCode, $"El canal «{canal}» no está registrado en esta instalación.", new { channelCode = canal });

    public static Error ChannelRejectsErpNumber(string canal) =>
        new ErrorConDatos(ChannelRejectsErpNumberCode,
            $"El canal {canal} no acepta el número que asigna el ERP; no se puede usar para emitir.", new { channelCode = canal });

    public static Error SoftwareIdRequired() =>
        new(SoftwareIdRequiredCode, "En modo software propio indique el identificador del software registrado ante la DIAN.");

    public static Error NoResolutionForChannel(IReadOnlyList<string> tipos, string canal) =>
        new ErrorConDatos(NoResolutionForChannelCode,
            $"Antes de emitir por {canal} asocie a ese canal una resolución vigente de: {string.Join(", ", tipos)}.",
            new { kinds = tipos, channelCode = canal });

    public static Error SimulatedInProduction() =>
        new(SimulatedInProductionCode, "El canal simulado sólo se usa en el ambiente de pruebas: en producción no tiene validez fiscal.");

    public static Error ChannelCannotSendEmail(string canal) =>
        new ErrorConDatos(ChannelCannotSendEmailCode,
            $"El canal {canal} no entrega el documento por correo: elija que lo envíe el ERP.", new { channelCode = canal });

    public static Error SettingsOverlaps(DateOnly validFrom) =>
        new ErrorConDatos(SettingsOverlapsCode,
            $"Ya hay una configuración de emisión que empieza el {validFrom:dd/MM/yyyy} o después: las vigencias no se cruzan.",
            new { validFrom });

    public static Error SettingsMissing() =>
        new(SettingsMissingCode, "No hay configuración de emisión vigente: configure el canal antes de verificar su credencial.");

    public static Error CredentialMismatch(string clave) =>
        new ErrorConDatos(CredentialMismatchCode,
            $"La credencial del canal no está en el secreto de la instalación o no corresponde a esta cooperativa (se esperaba la clave «{clave}»).",
            new { credentialKey = clave });
}
