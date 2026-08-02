namespace IngenIA365ERP.Application.Common.Audit;

/// <summary>
/// Constantes para los tipos de evento auditables introducidos por la feature
/// 002-identidad-central-federada (T032). El <c>AuditBehavior</c> existente
/// captura estos eventos automáticamente al pasar Commands por MediatR
/// (principio constitucional X — trazabilidad SIPLA/SARLAFT).
///
/// <para>
/// Convención: <c>{Aggregate}.{Action}.{Modifier?}</c>. Cada evento se persiste
/// en MongoDB <c>audit_events</c> con TTL 5 años heredado de Fase 0.
/// </para>
/// </summary>
public static class AuditEventTypes
{
    // -------------------- CentralUser --------------------
    public const string CentralUserLoginSuccess = "CentralUser.Login.Success";
    public const string CentralUserLoginFailed = "CentralUser.Login.Failed";
    public const string CentralUserLoginLocked = "CentralUser.Login.Locked";
    public const string CentralUserLogout = "CentralUser.Logout";

    public const string CentralUserMfaSuccess = "CentralUser.Mfa.Success";
    public const string CentralUserMfaFailed = "CentralUser.Mfa.Failed";
    public const string CentralUserMfaResetByMaster = "CentralUser.MfaResetByMaster";
    public const string CentralUserMfaRecoveryCodeUsed = "CentralUser.Mfa.RecoveryCodeUsed";
    public const string CentralUserMfaRecoveryCodeFailed = "CentralUser.Mfa.RecoveryCodeFailed";

    // -------------------- Invitations --------------------
    public const string InvitationIssuedByTenantAdmin = "Invitation.Issued.ByTenantAdmin";
    public const string InvitationIssuedByMaster = "Invitation.Issued.ByMaster";
    public const string InvitationAccepted = "Invitation.Accepted";
    public const string InvitationExpired = "Invitation.Expired";
    public const string InvitationRevoked = "Invitation.Revoked";
    public const string InvitationSuperseded = "Invitation.Superseded";

    // -------------------- Memberships --------------------
    public const string MembershipActivated = "Membership.Activated";
    public const string MembershipActivatedFromSuspension = "Membership.Activated.FromSuspension";
    public const string MembershipSuspended = "Membership.Suspended";
    public const string MembershipRevoked = "Membership.Revoked";
    public const string MembershipRevokedInvitationExpired = "Membership.Revoked.InvitationExpired";
    public const string MembershipPromotedToAdmin = "Membership.PromotedToAdmin";
    public const string MembershipDemotedFromAdmin = "Membership.DemotedFromAdmin";
    public const string MembershipLastAdminProtected = "Membership.LastAdminProtected";

    // -------------------- Sessions --------------------
    public const string SessionTenantSelected = "Session.TenantSelected";
    public const string SessionTenantSwitched = "Session.TenantSwitched";

    // -------------------- Tenant MFA policy --------------------
    public const string TenantMfaPolicyActivated = "TenantMfaPolicy.Activated";
    public const string TenantMfaPolicyDeactivated = "TenantMfaPolicy.Deactivated";

    // -------------------- Profile + Recovery --------------------
    public const string ProfileMfaEnrolled = "Profile.MfaEnrolled";
    public const string ProfileMfaDisabled = "Profile.MfaDisabled";
    public const string ProfileRecoveryCodesRegenerated = "Profile.RecoveryCodesRegenerated";
    public const string ProfilePasswordChanged = "Profile.PasswordChanged";
    public const string ProfilePasswordResetRequested = "Profile.PasswordResetRequested";
    public const string ProfilePasswordResetRequestedNoSuchEmail = "Profile.PasswordResetRequested.NoSuchEmail";
    public const string ProfilePasswordResetConsumed = "Profile.PasswordReset.Consumed";
    public const string ProfileDefaultTenantChanged = "Profile.DefaultTenantChanged";
    public const string ProfileDefaultTenantInvalidatedCleared = "Profile.DefaultTenantInvalidated.Cleared";

    // -------------------- Tenant (SaaS) --------------------
    public const string TenantCreated = "Tenant.Created";
}
