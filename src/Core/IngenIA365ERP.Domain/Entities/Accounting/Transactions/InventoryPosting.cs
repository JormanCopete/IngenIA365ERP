using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.Accounting.Transactions;

/// <summary>
/// El recibo de un mensaje de Inventario procesado por Contabilidad (<c>ACC_InventoryPostings</c>; feature 012, T11, T479;
/// data-model §20; contracts/contabilidad.md §3.1). Uno por mensaje —único <c>MessagePublicId</c>: la colisión se traduce a
/// <c>AlreadyProcessed</c>— y escrito en el <b>mismo</b> <c>SaveChanges</c> que el comprobante. Es también el vínculo
/// comprobante ↔ documentos: en un resumido, los documentos de un comprobante son las filas con su
/// <see cref="AccountingDocumentId"/>.
///
/// <para>
/// Es un hecho (<see cref="IHechoInmutable"/>): no cambia después de insertarse. Contabilidad no depende de los tipos de
/// Inventario, así que clase, tipo y número del documento van como el texto del mensaje, sin llave a tablas <c>INV_</c>.
/// Los informativos y los de valor cero no tienen comprobante (<see cref="NoVoucherReason"/>).
/// </para>
/// </summary>
public class InventoryPosting : AuditableEntityLong, IHechoInmutable
{
    public const string SinComprobanteInformativo = "Informational";
    public const string SinComprobanteValorCero = "ZeroValue";

    public Guid MessagePublicId { get; init; }

    public string MessageType { get; init; } = string.Empty;

    public short MessageVersion { get; init; } = 1;

    public IntegrationMessageKind MessageKind { get; init; }

    /// <summary><c>INV</c>.</summary>
    public string SourceModule { get; init; } = string.Empty;

    /// <summary>El documento, o la operación (cierre, reapertura, reclasificación).</summary>
    public Guid SourcePublicId { get; init; }

    public string? SourceDocumentClass { get; init; }

    public string? SourceDocumentTypeCode { get; init; }

    public string? SourceDocumentNumber { get; init; }

    /// <summary>El original anulado, devuelto o corregido.</summary>
    public Guid? RelatedDocumentPublicId { get; init; }

    /// <summary>La fecha del comprobante.</summary>
    public DateOnly OperationDate { get; init; }

    /// <summary>Nulo en informativos y en valor cero.</summary>
    public long? AccountingDocumentId { get; init; }

    public AccountingDocument? AccountingDocument { get; init; }

    /// <summary><see cref="SinComprobanteInformativo"/> o <see cref="SinComprobanteValorCero"/>; nulo si hay comprobante.</summary>
    public string? NoVoucherReason { get; init; }

    /// <summary>El <c>COR_IntegrationBatches</c> por el que pasó, si pasó por uno.</summary>
    public Guid? BatchPublicId { get; init; }

    /// <summary>Usuario que originó el documento: dato, nunca actor.</summary>
    public Guid OriginUserCentralId { get; init; }

    public string OriginUserName { get; init; } = string.Empty;

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    /// <summary>«Proceso de integración» o la persona que ordenó el lote o el reproceso.</summary>
    public string ActorName { get; init; } = string.Empty;

    public DateTime ProcessedAt { get; init; }
}
