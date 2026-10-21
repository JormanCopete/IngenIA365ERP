using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Compliance;

namespace IngenIA365ERP.Domain.Entities.Compliance;

/// <summary>
/// <c>COR_RestrictiveListLoads</c> (feature 013, T70, F-18; data-model parte 3 §4.8): una carga de una lista restrictiva (OFAC, ONU,
/// propia…) que mantiene el oficial de cumplimiento mientras no haya proveedor. <b>Hecho</b> (H): se inserta una vez con sus entradas y
/// no se modifica. El estado se <b>deriva</b>: vigente es la carga de mayor <see cref="ValidFrom"/> ≤ la fecha de su
/// <see cref="ListCode"/>; las anteriores, reemplazadas. La escribe sólo <c>ImportRestrictiveListCommand</c>; la lee
/// <c>CargaDeListasRestrictivasEnBase</c> (canal <c>CARGA-PROPIA</c>). Migración <c>PlataformaParaCartera</c>. (nuevo)
/// </summary>
public class RestrictiveListLoad : AuditableEntity, IHechoInmutable
{
    /// <summary><c>OFAC</c>, <c>ONU</c>, <c>PROPIA</c>… (código de catálogo, máx. 20).</summary>
    public string ListCode { get; init; } = string.Empty;

    /// <summary>Consecutivo por <see cref="ListCode"/>.</summary>
    public int Version { get; init; }

    /// <summary>De dónde se descargó (máx. 300).</summary>
    public string Source { get; init; } = string.Empty;

    public DateOnly? PublishedOn { get; init; }

    /// <summary>Desde cuándo la usa la consulta.</summary>
    public DateOnly ValidFrom { get; init; }

    public string FileName { get; init; } = string.Empty;

    /// <summary>SHA-256 del archivo, hexadecimal minúscula (64).</summary>
    public string FileSha256 { get; init; } = string.Empty;

    public int EntryCount { get; init; }

    public int LoadedByUserId { get; init; }

    public string LoadedByName { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public List<RestrictiveListEntry> Entries { get; init; } = [];
}

/// <summary>
/// <c>COR_RestrictiveListEntries</c> (feature 013, T70; data-model parte 3 §4.8): una entrada de una carga, ya normalizada. No es una
/// persona del maestro. Hecho, sin diferencias de auditoría fila por fila: la carga se audita con un evento (conteo y SHA-256). (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class RestrictiveListEntry : AuditableEntityLong, IHechoInmutable
{
    public int LoadId { get; init; }

    public RestrictiveListLoad? Load { get; init; }

    public RestrictiveListEntityKind EntityKind { get; init; }

    public string? DocumentType { get; init; }

    /// <summary>Sólo dígitos y letras, sin ceros a la izquierda (máx. 30).</summary>
    public string? DocumentNumberNormalized { get; init; }

    /// <summary>Como viene en la lista (máx. 300).</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Mayúsculas, sin tildes ni signos, tokens ordenados (<c>CoincidenciaDeNombres</c>).</summary>
    public string NameNormalized { get; init; } = string.Empty;

    /// <summary>JSON con los alias normalizados (máx. 2000).</summary>
    public string? Aliases { get; init; }

    public string? Program { get; init; }

    public string? Notes { get; init; }
}
