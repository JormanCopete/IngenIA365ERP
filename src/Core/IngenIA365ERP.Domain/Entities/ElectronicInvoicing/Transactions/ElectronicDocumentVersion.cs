using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;

/// <summary>Los artefactos que una versión recibe después del commit. (nuevo)</summary>
public enum ArtefactoDeVersion
{
    Canonico = 1,
    XmlFirmado = 2,
    AttachedDocument = 3,
    RepresentacionGrafica = 4,
}

/// <summary>
/// Una versión de un documento electrónico (<c>COR_ElectronicDocumentVersions</c>; feature 012, I4, T694; data-model §18;
/// contracts/dian.md §8.1). Es un hecho (<see cref="IHechoInmutable"/>): nace con la confirmación (versión 1,
/// <c>Initial</c>) o con los casos a y b (n + 1), y no cambia más.
///
/// <para>
/// La única excepción son las cuatro referencias a artefactos, que llegan <b>después</b> del commit (el canónico se sube
/// tras confirmar; el XML firmado y el AttachedDocument con la respuesta del canal; el PDF al validarse): son de
/// <see cref="EscrituraUnicaAttribute">escritura única</see> —nulo → valor una sola vez— y sólo se llenan por
/// <see cref="FijarArtefacto"/>. La guarda de <c>SaveChangesAsync</c> rechaza cualquier otro cambio. Los adjuntos son
/// de dueño <c>ElectronicSalesDocument</c> o <c>ElectronicPurchaseDocument</c>, no borrables.
/// </para>
/// </summary>
public class ElectronicDocumentVersion : AuditableEntity, IHechoInmutable
{
    public int ElectronicDocumentId { get; init; }

    public ElectronicDocument? ElectronicDocument { get; init; }

    /// <summary>Único por documento.</summary>
    public short VersionNumber { get; init; }

    /// <summary>El documento comercial de esta versión.</summary>
    public Guid SourceDocumentPublicId { get; init; }

    public DocumentVersionReason Reason { get; init; }

    /// <summary>1 (<c>DocumentoElectronicoCanonico</c> v1).</summary>
    public short CanonicalSchemaVersion { get; init; } = 1;

    /// <summary>SHA-256 del canónico construido en la transacción de confirmación (char(64)).</summary>
    public string CanonicalSha256 { get; init; } = string.Empty;

    /// <summary>Huella económica (<see cref="IngenIA365ERP.Domain.ElectronicInvoicing.HuellaEconomica"/>, char(64)).</summary>
    public string EconomicFingerprint { get; init; } = string.Empty;

    /// <summary>Casos a y b (máx. 500).</summary>
    public string? CorrectionReason { get; init; }

    /// <summary>Caso a: antes y después de lo que cambió (incluida la copia fiscal).</summary>
    public string? ChangedFieldsJson { get; init; }

    [EscrituraUnica]
    public Guid? CanonicalAttachmentPublicId { get; private set; }

    [EscrituraUnica]
    public Guid? SignedXmlAttachmentPublicId { get; private set; }

    [EscrituraUnica]
    public Guid? AttachedDocumentAttachmentPublicId { get; private set; }

    /// <summary>Representación del ERP (<c>RepresentacionGraficaReport</c>, carta y 80 mm).</summary>
    [EscrituraUnica]
    public Guid? GraphicPdfAttachmentPublicId { get; private set; }

    /// <summary>
    /// Llena la referencia de un artefacto una sola vez. Devuelve falso si ya tenía ese mismo valor (repetir es inocuo) y
    /// lanza si tenía otro: una referencia escrita no se reemplaza.
    /// </summary>
    public bool FijarArtefacto(ArtefactoDeVersion artefacto, Guid attachmentPublicId)
    {
        if (attachmentPublicId == Guid.Empty) throw new ArgumentException("El adjunto no puede ser vacío.", nameof(attachmentPublicId));

        var actual = artefacto switch
        {
            ArtefactoDeVersion.Canonico => CanonicalAttachmentPublicId,
            ArtefactoDeVersion.XmlFirmado => SignedXmlAttachmentPublicId,
            ArtefactoDeVersion.AttachedDocument => AttachedDocumentAttachmentPublicId,
            ArtefactoDeVersion.RepresentacionGrafica => GraphicPdfAttachmentPublicId,
            _ => throw new ArgumentOutOfRangeException(nameof(artefacto), artefacto, null),
        };
        if (actual == attachmentPublicId) return false;
        if (actual is not null)
            throw new InvalidOperationException($"El artefacto {artefacto} de la versión {VersionNumber} ya está escrito: no se reemplaza.");

        switch (artefacto)
        {
            case ArtefactoDeVersion.Canonico: CanonicalAttachmentPublicId = attachmentPublicId; break;
            case ArtefactoDeVersion.XmlFirmado: SignedXmlAttachmentPublicId = attachmentPublicId; break;
            case ArtefactoDeVersion.AttachedDocument: AttachedDocumentAttachmentPublicId = attachmentPublicId; break;
            default: GraphicPdfAttachmentPublicId = attachmentPublicId; break;
        }
        return true;
    }
}
