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

    /// <summary>
    /// Cambio en QUÉ métodos acepta la cooperativa, sin tocar si los exige. Evento
    /// propio porque no es ni activar ni desactivar: con el nombre de aquellos, un
    /// cambio que deja fuera a media cooperativa quedaría registrado como
    /// «activada» un día en que ya estaba activada.
    /// </summary>
    public const string TenantMfaPolicyMethodsChanged = "TenantMfaPolicy.MethodsChanged";

    /// <summary>Cambio en los métodos que acepta la plataforma para el maestro.</summary>
    public const string PlatformMfaPolicyMethodsChanged = "PlatformMfaPolicy.MethodsChanged";

    // ---------- Recuperación del segundo factor por correo ----------
    //
    // Los cuatro son eventos de seguridad de primera línea: describen un intento de
    // retirarle a alguien su segundo factor. Se separan —pedida, cancelada,
    // ejecutada, confirmación fallida— porque cada uno significa algo distinto el
    // día que haya que reconstruir un incidente, y «cancelada» seguida de nada es
    // exactamente la traza de un ataque que no prosperó.

    public const string MfaRecoveryRequested = "MfaRecovery.Requested";
    public const string MfaRecoveryCancelled = "MfaRecovery.Cancelled";
    public const string MfaRecoveryExecuted = "MfaRecovery.Executed";
    public const string MfaRecoveryConfirmFailed = "MfaRecovery.ConfirmFailed";

    // -------------------- Profile + Recovery --------------------
    public const string ProfileMfaEnrolled = "Profile.MfaEnrolled";
    public const string ProfileMfaDisabled = "Profile.MfaDisabled";

    /// <summary>Retiró UNA credencial, conservando las demás.</summary>
    public const string ProfileMfaCredentialRevoked = "Profile.MfaCredentialRevoked";

    /// <summary>Le cambió el nombre a una credencial. No altera la seguridad,
    /// pero sí quién puede reconocer qué dispositivo es cuál.</summary>
    public const string ProfileMfaCredentialRenamed = "Profile.MfaCredentialRenamed";
    public const string ProfileRecoveryCodesRegenerated = "Profile.RecoveryCodesRegenerated";
    public const string ProfilePasswordChanged = "Profile.PasswordChanged";
    public const string ProfilePasswordResetRequested = "Profile.PasswordResetRequested";
    public const string ProfilePasswordResetRequestedNoSuchEmail = "Profile.PasswordResetRequested.NoSuchEmail";
    public const string ProfilePasswordResetConsumed = "Profile.PasswordReset.Consumed";
    public const string ProfileDefaultTenantChanged = "Profile.DefaultTenantChanged";
    public const string ProfileDefaultTenantInvalidatedCleared = "Profile.DefaultTenantInvalidated.Cleared";

    // -------------------- Tenant (SaaS) --------------------
    public const string TenantCreated = "Tenant.Created";

    // -------------------- Nómina (feature 005) --------------------
    // Eventos explícitos además del AuditBehavior genérico (contracts/permissions.md §Auditoría):
    // llevan EntityPublicId y valores antes/después.
    public const string PayrollSalaryChanged = "Payroll.SalaryChanged";
    public const string PayrollRunApproved = "Payroll.Run.Approved";
    public const string PayrollRunReversed = "Payroll.Run.Reversed";
    public const string PayrollPaymentsMarked = "Payroll.Payments.Marked";
    public const string PayrollPaymentMarkReverted = "Payroll.Payments.MarkReverted";
    public const string PayrollPayslipsSent = "Payroll.Payslips.Sent";
    public const string PayrollEmployeeWithholdingChanged = "Payroll.EmployeeWithholding.Changed";
    public const string PayrollRunExported = "Payroll.Run.Exported";

    // -------------------- Database (feature 004 multi-motor) --------------------
    public const string DatabaseSeedExecuted = "Database.Seed.Executed";
    public const string DatabaseSeedTestSeedEnabledInProduction = "Database.Seed.TestSeedEnabledInProduction";
}
