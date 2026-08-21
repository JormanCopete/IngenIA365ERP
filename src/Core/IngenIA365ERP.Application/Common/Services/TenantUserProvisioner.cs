using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Services;

/// <summary>
/// T056 — Garantiza que para una <i>identidad central</i> que se incorpora
/// a un tenant exista exactamente una fila en la tabla <c>SEC_Users</c> de
/// la BD operacional del tenant: la crea si no existe o reactiva la fila
/// soft-deleted si la había.
///
/// <para>
/// <b>Invariantes</b>: 1 CentralUser ↔ 1 SEC_Users por tenant. La unicidad
/// definitiva la garantiza la columna <c>CentralUserId</c> introducida por
/// el script 15d (T017). Mientras ese cutover no se aplique, el predicado
/// de búsqueda usa <c>Username</c>/<c>Email</c> (ambos UNIQUE/indexed en el
/// schema actual) — el cambio a <c>CentralUserId</c> es de una línea cuando
/// se quite el <c>Ignore()</c> de <c>UserConfiguration</c>.
/// </para>
///
/// <para>
/// <b>Contexto del tenant</b>: este servicio asume que el
/// <see cref="IApplicationDbContext"/> inyectado ya apunta a la BD/schema
/// del tenant destino. El caller (handler de
/// <c>AcceptInvitationCommand</c>) es responsable de seleccionar el tenant
/// antes de invocar.
/// </para>
/// </summary>
public interface ITenantUserProvisioner
{
    /// <param name="tenantInternalId">
    /// Id interno de la cooperativa. Hace falta para encontrar SUS roles: los
    /// roles se discriminan por <c>Role.TenantId</c>, y los de
    /// <c>TenantId == null</c> son las plantillas SaaS — asignar una de esas
    /// daria permisos fuera de la cooperativa.
    /// </param>
    /// <param name="asTenantAdmin">
    /// Si la invitacion era para administrar la cooperativa. Decide el rol.
    /// </param>
    Task<Result<int>> EnsureExistsAsync(
        Guid centralUserId,
        string centralUserEmail,
        Guid tenantId,
        int tenantInternalId,
        bool asTenantAdmin,
        CancellationToken ct);
}

internal sealed class TenantUserProvisioner(
    IApplicationDbContext db,
    IDateTimeService clock,
    ILogger<TenantUserProvisioner> logger) : ITenantUserProvisioner
{
    private const string ProvisionerActor = "TenantUserProvisioner";
    private const string CentralManagedPasswordSentinel = "central-managed";
    private const string RolAdministrador = "CompanyAdmin";
    private const string RolPorDefecto = "ReadOnly";

    public async Task<Result<int>> EnsureExistsAsync(
        Guid centralUserId,
        string centralUserEmail,
        Guid tenantId,
        int tenantInternalId,
        bool asTenantAdmin,
        CancellationToken ct)
    {
        if (centralUserId == Guid.Empty)
            return Result.Failure<int>("Provisioning.InvalidCentralUserId",
                "El CentralUserId no puede ser Guid.Empty.");
        if (string.IsNullOrWhiteSpace(centralUserEmail))
            return Result.Failure<int>("Provisioning.InvalidEmail",
                "El email del usuario central no puede estar vacío.");

        var normalized = centralUserEmail.Trim();

        // IgnoreQueryFilters: queremos detectar también las filas soft-deleted
        // para reactivarlas en lugar de crear una nueva (idempotencia tras
        // revocaciones previas).
        var existing = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => u.Username == normalized || u.Email == normalized,
                ct);

        if (existing is not null)
        {
            // Las propiedades CentralUserId / CentralUserPublicEmail están
            // Ignored() en EF hasta el cutover de T017 — se asignan aquí en
            // memoria para que el resto del handler las pueda leer dentro
            // del scope del request. No se persisten todavía.
            existing.CentralUserId = centralUserId;
            existing.CentralUserPublicEmail = normalized;

            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedBy = null;
                existing.IsActive = true;
                existing.UpdatedBy = ProvisionerActor;
                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "SEC_Users reactivado para CentralUser {CentralUserId} en tenant {TenantId} (UserId={UserId}).",
                    centralUserId, tenantId, existing.Id);
            }
            else
            {
                logger.LogDebug(
                    "SEC_Users ya existía y está activo para CentralUser {CentralUserId} en tenant {TenantId} (UserId={UserId}).",
                    centralUserId, tenantId, existing.Id);
            }

            await AsegurarRolAsync(existing.Id, tenantInternalId, asTenantAdmin, ct);
            return Result.Success(existing.Id);
        }

        var newUser = new User
        {
            Username = normalized,
            Email = normalized,
            // La columna PasswordHash sigue siendo NOT NULL en SEC_Users hasta
            // que se ejecute el cutover de T017. Como la auth real va contra
            // ADM_CentralUsers (BCrypt + ASP.NET Identity), usamos un valor
            // centinela inutilizable: cualquier intento de validarlo con BCrypt
            // arrojará false sin riesgo.
            PasswordHash = CentralManagedPasswordSentinel,
            IsActive = true,
            CentralUserId = centralUserId,
            CentralUserPublicEmail = normalized,
            CreatedBy = ProvisionerActor,
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "SEC_Users provisionado para CentralUser {CentralUserId} en tenant {TenantId} (nuevo UserId={UserId}).",
            centralUserId, tenantId, newUser.Id);

        await AsegurarRolAsync(newUser.Id, tenantInternalId, asTenantAdmin, ct);
        return Result.Success(newUser.Id);
    }

    /// <summary>
    /// Le da a la fila recien provisionada un rol dentro de SU cooperativa.
    ///
    /// <para>
    /// Sin esto el usuario existe y no puede hacer nada: el provisionado creaba
    /// la fila en <c>SEC_Users</c> y se detenia ahi, sin escribir en
    /// <c>SEC_UserRoles</c>. Y la unica via de asignar roles en caliente
    /// (<c>POST /api/admin/users/{id}/roles</c>) exige el permiso
    /// <c>Security.Users.AssignRole</c>, que sale de tener un rol: bloqueo
    /// circular que solo rompia el administrador maestro a mano.
    /// </para>
    ///
    /// <para>
    /// Quien fue invitado a administrar recibe <c>CompanyAdmin</c>; el resto,
    /// <c>ReadOnly</c>, que es lo conservador y se cambia desde la pantalla de
    /// usuarios. Nunca se toca un rol plantilla.
    /// </para>
    ///
    /// <para>
    /// No falla la invitacion si el rol no esta: que alguien no pueda entrar a
    /// una pantalla se arregla; que la invitacion se pierda, no.
    /// </para>
    /// </summary>
    private async Task AsegurarRolAsync(
        int userId, int tenantInternalId, bool asTenantAdmin, CancellationToken ct)
    {
        var codigo = asTenantAdmin ? RolAdministrador : RolPorDefecto;

        var rolId = await db.Roles
            .Where(r => r.TenantId == tenantInternalId
                     && r.Code == codigo
                     && r.IsActive
                     && !r.IsDeleted)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (rolId is null)
        {
            logger.LogWarning(
                "La cooperativa {TenantInternalId} no tiene el rol {Codigo}, asi que el usuario " +
                "{UserId} queda sin permisos. Suele significar que nunca se aprovisiono: " +
                "POST /api/saas/tenants/{{publicId}}/provision es idempotente y lo arregla.",
                tenantInternalId, codigo, userId);
            return;
        }

        var yaLoTiene = await db.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == rolId.Value, ct);
        if (yaLoTiene) return;

        db.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = rolId.Value,
            AssignedAt = clock.UtcNow,
            AssignedBy = ProvisionerActor,
            CreatedBy = ProvisionerActor,
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Usuario {UserId} asignado al rol {Codigo} de la cooperativa {TenantInternalId}.",
            userId, codigo, tenantInternalId);
    }
}
