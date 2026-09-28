using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.ElectronicInvoicing;

/// <summary>
/// Un dato que el canónico exige y el documento no tiene (contracts/dian.md §4.4; api.md §24.8): qué campo del canónico, dónde
/// se completa y con qué permiso (<c>data.missing[] { field, where, permission }</c>). Nunca sin salida. (nuevo)
/// </summary>
public sealed record DatoFaltante(string Field, string Where, string? Permission, string Message);

/// <summary>
/// Los errores de facturación electrónica que arma la aplicación (feature 012, I4; api.md §24.8; decisiones-transversales §2.17).
/// Los de la máquina de estados son de <c>TransicionesDelDocumentoElectronico</c> (Domain). (nuevo)
/// </summary>
public static class ErroresDeFacturacionElectronica
{
    public const string MissingDataCode = "ElectronicInvoicing.Document.MissingData";
    public const string ReplacementDraftExistsCode = "ElectronicInvoicing.Document.ReplacementDraftExists";
    public const string NotReplacementDraftCode = "ElectronicInvoicing.Document.NotReplacementDraft";

    /// <summary>
    /// 422: el documento no tiene un dato que el canónico exige y la confirmación se bloquea (Principio VIII: no se descubre en un
    /// rechazo de la DIAN).
    /// </summary>
    public static Error MissingData(IReadOnlyList<DatoFaltante> faltantes) =>
        new ErrorConDatos(MissingDataCode,
            faltantes.Count == 1
                ? faltantes[0].Message
                : $"Faltan {faltantes.Count} datos para el documento electrónico: {string.Join(" ", faltantes.Select(f => f.Message))}",
            new { missing = faltantes.Select(f => new { field = f.Field, where = f.Where, permission = f.Permission, message = f.Message }).ToList() });

    /// <summary>422: ya hay un borrador de reemplazo vivo para el documento rechazado (caso b).</summary>
    public static Error ReplacementDraftExists(Guid replacementDraftPublicId) =>
        new ErrorConDatos(ReplacementDraftExistsCode,
            "Ya hay un borrador de reemplazo para este documento rechazado: siga editándolo o descártelo antes de crear otro.",
            new { replacementDraftPublicId });

    /// <summary>422: el documento que se pide confirmar como reemplazo no es el borrador de reemplazo del rechazado. (nuevo)</summary>
    public static Error NotReplacementDraft() =>
        new(NotReplacementDraftCode,
            "El documento indicado no es un borrador de reemplazo de este documento rechazado (misma clase, vinculado como reemplazo y sin confirmar).");
}
