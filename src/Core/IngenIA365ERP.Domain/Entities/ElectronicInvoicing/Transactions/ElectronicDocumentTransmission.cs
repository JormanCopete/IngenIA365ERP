using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;

/// <summary>
/// Un intento contra el canal (<c>COR_ElectronicDocumentTransmissions</c>; feature 012, I4, T694; data-model §18;
/// contracts/dian.md §6.5). Es un hecho (<see cref="IHechoInmutable"/>): se inserta en la transacción que registra el
/// resultado, después de la llamada (la llamada nunca va dentro de la transacción de confirmación), y nunca se edita.
/// Si el proceso cae entre la llamada y este registro, el siguiente intento consulta antes de reenviar.
/// </summary>
public class ElectronicDocumentTransmission : AuditableEntityLong, IHechoInmutable
{
    public int ElectronicDocumentId { get; init; }

    public ElectronicDocument? ElectronicDocument { get; init; }

    public int VersionId { get; init; }

    public ElectronicDocumentVersion? Version { get; init; }

    /// <summary>Único por documento.</summary>
    public int AttemptNumber { get; init; }

    public TransmissionOperation Operation { get; init; }

    /// <summary>Máx. 40.</summary>
    public string ChannelCode { get; init; } = string.Empty;

    public DateTime RequestedAt { get; init; }

    public DateTime CompletedAt { get; init; }

    public int DurationMs { get; init; }

    public ActorKind RequestedByKind { get; init; }

    /// <summary><c>SEC_Users</c>, si la pidió una persona.</summary>
    public int? RequestedByUserId { get; init; }

    /// <summary>«Proceso de integración» o la persona (máx. 150).</summary>
    public string RequestedByName { get; init; } = string.Empty;

    /// <summary><c>{tenantPublicId:N}:{Environment}:{Prefix}{Consecutive}:v{VersionNumber}</c> (máx. 120).</summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>SHA-256 de lo enviado (char(64)).</summary>
    public string RequestSha256 { get; init; } = string.Empty;

    public short? HttpStatus { get; init; }

    public ChannelOutcome Outcome { get; init; }

    /// <summary>Máx. 20.</summary>
    public string? ProviderCode { get; init; }

    /// <summary>Máx. 10.</summary>
    public string? DianStatusCode { get; init; }

    public bool? IsValid { get; init; }

    /// <summary><c>[{ regla, tipo, texto }]</c> tal como llegaron.</summary>
    public string? RawMessagesJson { get; init; }

    /// <summary><c>[{ regla, tipo, texto, traducción }]</c>.</summary>
    public string? TranslatedMessagesJson { get; init; }

    /// <summary>trackId, zipKey o el identificador del proveedor (máx. 100).</summary>
    public string? ExternalReference { get; init; }

    public Guid? ApplicationResponseAttachmentPublicId { get; init; }

    /// <summary>Máx. 64.</summary>
    public string? CorrelationId { get; init; }
}
