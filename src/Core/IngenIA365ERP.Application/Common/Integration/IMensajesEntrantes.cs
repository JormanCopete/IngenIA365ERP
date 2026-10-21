using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Cómo lee un consumidor lo que le llegó (feature 012, T496; contracts/mensajes.md §13). Es lo único de la bandeja de
/// salida que ve un destino: nunca lee ni escribe <c>COR_Integration*</c> por su cuenta. (nuevo)
/// </summary>
public interface IMensajesEntrantes
{
    /// <summary>
    /// Los mensajes pedidos con su entrega a <paramref name="destino"/>, en orden de emisión. Verifica que
    /// <c>PayloadSha256</c> sea el de los bytes guardados (<c>Integration.Message.PayloadAltered</c> si no) y deserializa
    /// el contenido a su <c>&lt;Type&gt;V&lt;n&gt;</c> del catálogo; un tipo o versión fuera del catálogo llega como
    /// <c>JsonElement</c>, para que el destino lo rechace con <c>Integration.VersionNotAccepted</c>. Un mensaje sin entrega
    /// a ese destino es <c>Integration.Message.NotFound</c>.
    /// </summary>
    Task<Result<IReadOnlyList<MensajeEntrante>>> LeerAsync(IReadOnlyCollection<Guid> messagePublicIds, string destino, CancellationToken ct);

    /// <summary>
    /// Los mensajes de <paramref name="tipos"/> que todavía esperan en <paramref name="destino"/> (entrega <c>Pending</c>,
    /// <c>InBatch</c> o <c>Rejected</c>) con fecha de operación hasta <paramref name="hastaFechaDeOperacion"/>, en orden de
    /// emisión y por páginas (feature 013, T19; contracts/mensajes.md §15.4). <b>No consume ni intenta nada</b>: es la lectura de
    /// la conciliación de las ventas acumuladas antes de activar Cartera (FR-143). Verifica la huella como <see cref="LeerAsync"/>.
    /// </summary>
    Task<Result<PagedResult<MensajeEntrante>>> PendientesAsync(
        string destino, IReadOnlyCollection<string> tipos, DateOnly hastaFechaDeOperacion, PageRequest pagina, CancellationToken ct);
}

/// <summary>
/// Un mensaje tal como lo recibe su destino (contracts/mensajes.md §13): el sobre con el contenido ya deserializado, los
/// bytes y su hash, el resultado de la validación previa y los datos de la entrega. (nuevo)
/// </summary>
/// <param name="Payload">El contenido <c>&lt;Type&gt;V1</c>, o un <c>JsonElement</c> si el tipo o la versión no están en el catálogo.</param>
public sealed record MensajeEntrante(
    IntegrationEnvelopeV1 Envelope,
    object? Payload,
    string PayloadJson,
    string PayloadSha256,
    PrevalidationOutcome? PrevalidationOutcome,
    EntregaEntrante Entrega)
{
    public Guid MessageId => Envelope.MessageId;

    public string Type => Envelope.Type;

    public int Version => Envelope.Version;

    /// <summary>
    /// La clase del documento de origen como <b>texto</b>, tal como se guardó (feature 013, T10): el sobre la tipa con el enum de
    /// Inventario, así que para <c>CAR</c>, <c>TES</c> y <c>REC</c> <c>Envelope.Origin.DocumentClass</c> llega nulo y el consumidor
    /// lee esto.
    /// </summary>
    public string? OriginDocumentClassText { get; init; }

    /// <summary>La clase del documento relacionado como texto (feature 013, T10); nula si no hay relacionado.</summary>
    public string? RelatedDocumentClassText { get; init; }

    /// <summary>El módulo que lo emitió (<c>INV</c>, <c>CAR</c>, <c>TES</c>, <c>REC</c>).</summary>
    public string OriginModule => Envelope.OriginModule;
}

/// <summary>Los datos de la entrega que ve el consumidor: nunca el <c>Id</c> interno. (nuevo)</summary>
public sealed record EntregaEntrante(
    string Destination,
    DeliveryMode Mode,
    DeliveryStatus Status,
    string? ScheduleKey,
    string? BatchScopeKey,
    Guid? BatchPublicId,
    int Attempts,
    DateTime? NextAttemptAt);
