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
    Task<Result<int>> EnsureExistsAsync(
        Guid centralUserId,
        string centralUserEmail,
        Guid tenantId,
        CancellationToken ct);
}

internal sealed class TenantUserProvisioner(
    IApplicationDbContext db,
    IDateTimeService clock,
    ILogger<TenantUserProvisioner> logger) : ITenantUserProvisioner
{
    private const string ProvisionerActor = "TenantUserProvisioner";
    private const string CentralManagedPasswordSentinel = "central-managed";

    public async Task<Result<int>> EnsureExistsAsync(
        Guid centralUserId,
        string centralUserEmail,
        Guid tenantId,
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

        return Result.Success(newUser.Id);
    }
}
