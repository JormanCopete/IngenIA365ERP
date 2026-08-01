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

    /// <summary>Pending → Revoked.</summary>
    public void Revoke(Guid byUserId, DateTime now)
    {
        EnsurePending();
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
        EnsurePending();
        Status = InvitationStatus.Superseded;
    }

    private void EnsurePending()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException(
                $"La invitación no está pendiente (estado actual: {Status}).");
    }
}
