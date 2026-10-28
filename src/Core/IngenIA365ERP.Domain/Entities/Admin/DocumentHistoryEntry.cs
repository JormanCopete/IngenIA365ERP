using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Un cambio del documento de acceso de una identidad (feature 017, FR-045). Sólo inserción: no se
/// edita ni se borra. Maps to [dbo].[ADM_DocumentHistory].
/// </summary>
public class DocumentHistoryEntry : AuditableEntity
{
    public Guid CentralUserId { get; private set; }

    [MaxLength(3)] public string? OldType { get; private set; }
    [MaxLength(20)] public string? OldNumber { get; private set; }
    [MaxLength(24)] public string? OldUsername { get; private set; }
    [MaxLength(3)] public string NewType { get; private set; } = string.Empty;
    [MaxLength(20)] public string NewNumber { get; private set; } = string.Empty;
    [MaxLength(24)] public string NewUsername { get; private set; } = string.Empty;

    public DocumentChangeReason Reason { get; private set; }

    [MaxLength(500)] public string? Notes { get; private set; }

    /// <summary>Referencia de texto del soporte (caso, radicado) cuando lo corrige el maestro.</summary>
    [MaxLength(200)] public string? SupportReference { get; private set; }

    /// <summary>Adjunto de soporte en la cooperativa del administrador que corrigió.</summary>
    public Guid? SupportAttachmentPublicId { get; private set; }
    public Guid? SupportTenantId { get; private set; }

    public Guid? ActorCentralUserId { get; private set; }
    public DocumentChangeActor ActorKind { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private DocumentHistoryEntry() { }

    public static DocumentHistoryEntry Registrar(
        Guid centralUserId,
        string? oldType, string? oldNumber, string? oldUsername,
        string newType, string newNumber, string newUsername,
        DocumentChangeReason reason, DocumentChangeActor actorKind, Guid? actorCentralUserId, DateTime now,
        string? notes = null, string? supportReference = null,
        Guid? supportAttachmentPublicId = null, Guid? supportTenantId = null) => new()
        {
            CentralUserId = centralUserId,
            OldType = oldType,
            OldNumber = oldNumber,
            OldUsername = oldUsername,
            NewType = newType,
            NewNumber = newNumber,
            NewUsername = newUsername,
            Reason = reason,
            ActorKind = actorKind,
            ActorCentralUserId = actorCentralUserId,
            OccurredAt = now,
            Notes = notes,
            SupportReference = supportReference,
            SupportAttachmentPublicId = supportAttachmentPublicId,
            SupportTenantId = supportTenantId,
        };
}

/// <summary>Por qué cambió el documento.</summary>
public enum DocumentChangeReason
{
    /// <summary>Lo registró la persona o vino con su invitación.</summary>
    Registration = 0,
    /// <summary>Se corrigió un error de digitación.</summary>
    Correction = 1,
    /// <summary>Cambió legítimamente (PPT → CE, TI → CC…). Misma identidad.</summary>
    Update = 2,
    /// <summary>Quedó en la identidad correcta por una fusión.</summary>
    Merge = 3,
    /// <summary>Lo subió la tarea de datos desde las cooperativas.</summary>
    Migration = 4,
}

/// <summary>Quién hizo el cambio.</summary>
public enum DocumentChangeActor
{
    Person = 0,
    TenantAdmin = 1,
    PlatformMaster = 2,
    Process = 3,
}
