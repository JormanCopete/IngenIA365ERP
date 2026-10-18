using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocumentEvents</c> (feature 013, C.3; data-model §27.3): emisión, entrega y anulación de un
/// <see cref="IssuedLendingDocument"/>. Hecho de sólo inserción (<see cref="IHechoInmutable"/>); una sola anulación por documento.
/// La reimpresión, la descarga y la verificación son lecturas auditadas, no eventos.
///
/// <para>US13 (T0776): nace sólo por <see cref="Emitido"/>, <see cref="Entregado"/> o <see cref="Anulado"/>, que validan lo que cada clase exige
/// (la anulación lleva motivo; la entrega por correo, su mensaje).</para>
/// </summary>
public class IssuedDocumentEvent : AuditableEntity, IHechoInmutable
{
    /// <summary>Largo del motivo (<c>nvarchar(500)</c>).</summary>
    public const int LargoDelMotivo = 500;

    public int IssuedDocumentId { get; init; }

    public IssuedDocumentEventKind Kind { get; init; }

    public DateTime OccurredAt { get; init; }

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    /// <summary>Obligatorio en la anulación manual (<c>Lending.Documents.Void</c>).</summary>
    public string? Reason { get; init; }

    /// <summary>El reverso que lo anuló (<c>VigenciaDeDocumentos</c>).</summary>
    public long? CausingOperationId { get; init; }

    /// <summary>
    /// Feature 014 (T028; data-model §1.5): el reverso de una operación de <b>cuenta</b> que lo anuló (<c>LND_MemberAccountOperations</c>);
    /// excluyente con <see cref="CausingOperationId"/> (<c>CK_LND_IssuedDocumentEvents_Cause</c>).
    /// </summary>
    public long? CausingAccountOperationId { get; init; }

    /// <summary><c>Delivered</c> por correo.</summary>
    public int? DebtorMessageId { get; init; }

    /// <summary>La emisión del documento (quien lo emitió, en el mismo instante que <c>IssuedAt</c>).</summary>
    public static IssuedDocumentEvent Emitido(IssuedLendingDocument documento, DateTime en, ActorKind actorKind, int? actorUserId)
    {
        ArgumentNullException.ThrowIfNull(documento);
        return Nuevo(documento.Id, IssuedDocumentEventKind.Issued, en, actorKind, actorUserId);
    }

    /// <summary>La entrega por correo: el mensaje al deudor que la llevó quedó enviado.</summary>
    public static IssuedDocumentEvent Entregado(int documentoId, int debtorMessageId, DateTime en, ActorKind actorKind, int? actorUserId)
    {
        if (debtorMessageId <= 0) throw new ArgumentOutOfRangeException(nameof(debtorMessageId), "La entrega por correo cita su mensaje.");
        var evento = Nuevo(documentoId, IssuedDocumentEventKind.Delivered, en, actorKind, actorUserId);
        return new IssuedDocumentEvent
        {
            IssuedDocumentId = evento.IssuedDocumentId, Kind = evento.Kind, OccurredAt = evento.OccurredAt, ActorKind = evento.ActorKind,
            ActorUserId = evento.ActorUserId, DebtorMessageId = debtorMessageId,
        };
    }

    /// <summary>
    /// La anulación: manual (una persona, con motivo) o por el reverso de <paramref name="operacionCausa"/> (con su motivo «Se reversó…»). El
    /// motivo es obligatorio en las dos: es lo que muestra la verificación.
    /// </summary>
    public static IssuedDocumentEvent Anulado(int documentoId, string motivo, DateTime en, ActorKind actorKind, int? actorUserId, long? operacionCausa = null,
        long? operacionDeCuentaCausa = null)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("La anulación de un documento lleva motivo.", nameof(motivo));
        // Feature 014 (T087; data-model §1.5): la causa es una operación de crédito o una de cuenta, nunca las dos (CK_LND_IssuedDocumentEvents_Cause).
        if (operacionCausa is not null && operacionDeCuentaCausa is not null)
            throw new ArgumentException("La anulación la causa una operación de crédito o una de cuenta, no las dos.", nameof(operacionDeCuentaCausa));
        _ = Nuevo(documentoId, IssuedDocumentEventKind.Voided, en, actorKind, actorUserId);
        var texto = motivo.Trim();
        return new IssuedDocumentEvent
        {
            IssuedDocumentId = documentoId,
            Kind = IssuedDocumentEventKind.Voided,
            OccurredAt = en,
            ActorKind = actorKind,
            ActorUserId = actorUserId,
            Reason = texto.Length <= LargoDelMotivo ? texto : texto[..LargoDelMotivo],
            CausingOperationId = operacionCausa,
            CausingAccountOperationId = operacionDeCuentaCausa,
        };
    }

    private static IssuedDocumentEvent Nuevo(int documentoId, IssuedDocumentEventKind clase, DateTime en, ActorKind actorKind, int? actorUserId)
    {
        if ((actorKind == ActorKind.Person) != actorUserId.HasValue)
            throw new ArgumentException("Lo registra una persona con su usuario, o el proceso sin usuario.", nameof(actorUserId));
        return new IssuedDocumentEvent { IssuedDocumentId = documentoId, Kind = clase, OccurredAt = en, ActorKind = actorKind, ActorUserId = actorUserId };
    }
}
