using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Invitación por correo con token de un solo uso (FR-023 a FR-031).
/// Estados terminales: Accepted, Expired, Revoked. Maps to [dbo].[ADM_Invitations].
/// </summary>
public class Invitation : AuditableEntity
{
    [MaxLength(256)]
    public string Email { get; private set; } = string.Empty;

    [MaxLength(256)]
    public string NormalizedEmail { get; private set; } = string.Empty;

    public Guid TenantId { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    /// <summary>Solo el master admin puede emitir invitaciones con este flag en true (FR-025, FR-026).</summary>
    public bool InviteAsTenantAdmin { get; private set; }

    /// <summary>Hash SHA-256 del token plano. El plano NUNCA se persiste.</summary>
    public byte[] TokenHash { get; private set; } = [];

    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? AcceptedAt { get; private set; }
    public Guid? AcceptedByCentralUserId { get; private set; }

    public DateTime? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    // ── Feature 017: invitación anclada al documento ──────────────────────────

    /// <summary>La identidad a la que ancla: la que ya existía o la que se creó al invitar.</summary>
    public Guid? CentralUserId { get; private set; }

    /// <summary>Código de acceso (CC, CE, PPT…). Nulo en invitaciones anteriores a la 017.</summary>
    [MaxLength(3)]
    public string? DocumentType { get; private set; }

    /// <summary>Número normalizado. Nunca sale en la vista previa (research R8).</summary>
    [MaxLength(20)]
    public string? DocumentNumber { get; private set; }

    [MaxLength(60)] public string? FirstName { get; private set; }
    [MaxLength(60)] public string? OtherNames { get; private set; }
    [MaxLength(60)] public string? LastName { get; private set; }
    [MaxLength(60)] public string? SecondLastName { get; private set; }
    [MaxLength(30)] public string? MobilePhone { get; private set; }

    /// <summary>Rol de la cooperativa (<c>SEC_Roles.PublicId</c>) que tendrá al activar.</summary>
    public Guid? RolePublicId { get; private set; }

    /// <summary>Cargo de la cooperativa (<c>COR_Positions.PublicId</c>), informativo (D-15).</summary>
    public Guid? PositionPublicId { get; private set; }

    /// <summary>Intentos fallidos de digitar el documento al activar.</summary>
    public int DocumentAttempts { get; private set; }

    /// <summary>Copia de la política de la cooperativa al emitir (D-06).</summary>
    public int MaxDocumentAttempts { get; private set; }

    /// <summary>Lo que digitó el administrador cuando la invitación salió a otro correo (auditoría).</summary>
    [MaxLength(256)]
    public string? TypedEmail { get; private set; }

    /// <summary>Si la invitación trae documento: las de antes de la 017 no lo piden al activar.</summary>
    public bool RequiresDocument => DocumentNumber is not null;

    /// <summary>
    /// Ancla la invitación recién creada a una identidad y a un documento, con los datos de la persona,
    /// el rol y el cargo. Se llama una sola vez, antes de guardar.
    /// </summary>
    public void AnclarADocumento(
        Guid centralUserId, string documentType, string documentNumber,
        string? firstName, string? otherNames, string? lastName, string? secondLastName, string? mobilePhone,
        Guid? rolePublicId, Guid? positionPublicId, int maxDocumentAttempts, string? typedEmail)
    {
        if (maxDocumentAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDocumentAttempts));
        CentralUserId = centralUserId;
        DocumentType = documentType;
        DocumentNumber = documentNumber;
        FirstName = firstName;
        OtherNames = otherNames;
        LastName = lastName;
        SecondLastName = secondLastName;
        MobilePhone = mobilePhone;
        RolePublicId = rolePublicId;
        PositionPublicId = positionPublicId;
        MaxDocumentAttempts = maxDocumentAttempts;
        TypedEmail = typedEmail;
    }

    /// <summary>Intentos que le quedan a la persona antes de pasar a revisión.</summary>
    public int AttemptsLeft => Math.Max(0, MaxDocumentAttempts - DocumentAttempts);

    /// <summary>
    /// Suma un intento fallido. Devuelve true si con éste se agotaron y la invitación pasó a
    /// <see cref="InvitationStatus.UnderReview"/>.
    /// </summary>
    public bool RegistrarIntentoFallido()
    {
        EnsurePending();
        DocumentAttempts++;
        if (DocumentAttempts < MaxDocumentAttempts)
            return false;
        Status = InvitationStatus.UnderReview;
        return true;
    }

    // EF Core
    private Invitation() { }

    /// <summary>Crea una invitación pendiente con token hash ya calculado.</summary>
    public static Invitation Create(
        string email,
        string normalizedEmail,
        Guid tenantId,
        Guid invitedByUserId,
        bool inviteAsTenantAdmin,
        byte[] tokenHash,
        DateTime createdAt,
        DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El email no puede estar vacío.", nameof(email));
        if (expiresAt <= createdAt)
            throw new ArgumentException("ExpiresAt debe ser posterior a CreatedAt.", nameof(expiresAt));
        if (tokenHash is null || tokenHash.Length == 0)
            throw new ArgumentException("TokenHash no puede estar vacío.", nameof(tokenHash));

        return new Invitation
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            TenantId = tenantId,
            InvitedByUserId = invitedByUserId,
            InviteAsTenantAdmin = inviteAsTenantAdmin,
            TokenHash = tokenHash,
            Status = InvitationStatus.Pending,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Retorna true si la invitación está pendiente y dentro del periodo de validez.</summary>
    public bool IsValid(DateTime now) =>
        Status == InvitationStatus.Pending && ExpiresAt > now;

    /// <summary>Pending → Accepted. La concurrencia (single-use estricto) se garantiza con
    /// RowVersion + lock distribuido en Application (research D-06).</summary>
    public void MarkAccepted(Guid acceptedByCentralUserId, DateTime now)
    {
        EnsurePending();
        if (ExpiresAt <= now)
            throw new InvalidOperationException("La invitación ha expirado.");

        Status = InvitationStatus.Accepted;
        AcceptedAt = now;
        AcceptedByCentralUserId = acceptedByCentralUserId;
    }

    /// <summary>Pending → Expired. Idempotente: ya expirada se queda igual sin lanzar.</summary>
    public bool MarkExpired(DateTime now)
    {
        if (Status != InvitationStatus.Pending)
            return false;
        if (ExpiresAt > now)
            throw new InvalidOperationException(
                $"No se puede marcar como expirada: ExpiresAt={ExpiresAt:O} es futuro respecto a {now:O}.");

        Status = InvitationStatus.Expired;
        return true;
    }

    /// <summary>Pending o UnderReview → Revoked.</summary>
    public void Revoke(Guid byUserId, DateTime now)
    {
        EnsurePendingOrUnderReview();
        Status = InvitationStatus.Revoked;
        RevokedAt = now;
        RevokedByUserId = byUserId;
    }

    /// <summary>
    /// Pending → Superseded. Se aplica cuando se emite una NUEVA invitación al
    /// mismo email+tenant — la anterior queda obsoleta para evitar que el
    /// destinatario use un token invalidado por la más reciente (T051).
    /// </summary>
    public void MarkSuperseded()
    {
        EnsurePendingOrUnderReview();
        Status = InvitationStatus.Superseded;
    }

    private void EnsurePendingOrUnderReview()
    {
        if (Status is not (InvitationStatus.Pending or InvitationStatus.UnderReview))
            throw new InvalidOperationException(
                $"La invitación no está pendiente ni en revisión (estado actual: {Status}).");
    }

    private void EnsurePending()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException(
                $"La invitación no está pendiente (estado actual: {Status}).");
    }
}
