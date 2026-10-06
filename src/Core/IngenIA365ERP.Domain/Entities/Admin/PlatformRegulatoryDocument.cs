using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// <c>ADM_RegulatoryDocuments</c> (feature 013, T37, T45; data-model parte 3 §3.2): un documento normativo tipado en
/// JSON —catálogo de modalidades, tipos de organización, Anexo 1, regla de pérdida esperada y, desde K6a, Anexo 2— con
/// su versión de esquema y su cotejo (<see cref="Verified"/>, molde de la PILA). Lo valida el
/// <c>IValidadorDeDocumentoNormativo</c> de su clase antes de guardarlo y lo copia la réplica sin modificar.
///
/// <para>Hecho de sólo inserción, como <see cref="PlatformNationalValue"/>, con la misma numeración común.</para>
/// </summary>
public class PlatformRegulatoryDocument : AuditableEntity, IHechoInmutable, IVersionBitemporal
{
    /// <summary>Texto de <c>RegulatoryDocumentKind</c> (<c>CatalogoDeModalidades</c>, <c>SarcAnexo1</c>…).</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Identificador legible de la versión (<c>SARC-ANEXO1-2025-01-31</c>).</summary>
    public string Code { get; init; } = string.Empty;

    public DateOnly ValidFrom { get; init; }

    /// <summary>Sólo si la norma la fija.</summary>
    public DateOnly? ValidTo { get; init; }

    /// <summary>El JSON en su forma canónica (texto, no <c>jsonb</c>, por paridad entre motores).</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>SHA-256 del contenido canónico, en hexadecimal minúscula (64).</summary>
    public string ContentSha256 { get; init; } = string.Empty;

    /// <summary>Versión del esquema tipado; el lector sabe leer las anteriores para reproducir cierres viejos.</summary>
    public short SchemaVersion { get; init; }

    /// <summary>Cotejo de la plataforma contra la fuente oficial.</summary>
    public bool Verified { get; init; }

    /// <summary>Circular, anexo y fecha.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Obligatorio en una corrección.</summary>
    public string? Reason { get; init; }

    public long Sequence { get; init; }

    public DateTime RegisteredAt { get; init; }

    public string RegisteredBy { get; init; } = string.Empty;

    public Guid RegisteredByCentralUserId { get; init; }

    public Guid? SupersedesPublicId { get; init; }

    DateTime IVersionBitemporal.ConocidaEn => RegisteredAt;
}
