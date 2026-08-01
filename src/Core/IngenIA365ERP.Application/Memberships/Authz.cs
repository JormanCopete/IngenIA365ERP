using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Memberships;

/// <summary>
/// Helpers de autorización compartidos por los handlers de Memberships (US4).
/// Consolida la verificación "es tenant admin del tenant indicado, o master".
/// </summary>
internal static class Authz
{
    public sealed record GuardResult(bool IsAllowed, string? ErrorCode, string? Message)
    {
        public static GuardResult Allowed() => new(true, null, null);
        public static GuardResult Fail(string code, string msg) => new(false, code, msg);
    }

    public static async Task<GuardResult> EnsureTenantAdminOrMasterAsync(
        ICurrentCentralUserContext currentUser,
        IAdminDbContext adminDb,
        Guid tenantPublicId,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is null)
            return GuardResult.Fail("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return GuardResult.Fail("Identity.WrongTokenPurpose", "Requiere purpose=full.");

        if (currentUser.IsGlobalMasterAdmin)
            return GuardResult.Allowed();

        var callerCentralUserId = currentUser.CentralUserId.Value;
        var isAdmin = await adminDb.TenantMemberships
            .AsNoTracking()
            .AnyAsync(m => m.CentralUserId == callerCentralUserId
                        && m.TenantId == tenantPublicId
                        && m.Status == MembershipStatus.Active
                        && m.IsTenantAdmin, ct);
        return isAdmin
            ? GuardResult.Allowed()
            : GuardResult.Fail("Membership.Forbidden",
                "Solo un administrador activo de la empresa puede ejecutar esta acción.");
    }
}
