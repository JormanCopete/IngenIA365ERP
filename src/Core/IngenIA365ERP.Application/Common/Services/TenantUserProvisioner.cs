using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Common.Identidad;
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
    /// <param name="tenantDatabase">
    /// Base de datos de la cooperativa destino. Es obligatoria y no se deduce del
    /// ambiente: esta ruta la invoca la aceptacion de una invitacion, que es
    /// anonima y no tiene cooperativa activa. La cooperativa la dice la invitacion.
    /// </param>
    /// <param name="tenantConnectionOverride">Cadena propia de la cooperativa, si la tiene.</param>
    /// <param name="asTenantAdmin">
    /// Si la invitacion era para administrar la cooperativa. Decide el rol.
    /// </param>
    /// <param name="activacion">
    /// Feature 017: lo que trae una invitación anclada al documento —persona, rol, cargo y usuario
    /// central—. Nulo en las invitaciones de antes: se provisiona como siempre.
    /// </param>
    Task<Result<int>> EnsureExistsAsync(
        Guid centralUserId,
        string centralUserEmail,
        Guid tenantId,
        string tenantDatabase,
        string? tenantConnectionOverride,
        bool asTenantAdmin,
        CancellationToken ct,
        DatosDeActivacion? activacion = null);
}

/// <summary>
/// Lo que una invitación con documento aporta al activar (feature 017, research R7): la persona de la
/// cooperativa se busca por su usuario derivado y, si no existe, se crea con estos datos; el rol y el
/// cargo son los que eligió el administrador; el usuario central se copia para mostrar diferencias.
/// </summary>
public sealed record DatosDeActivacion(
    string? DocumentType,
    string? DocumentNumber,
    string? FirstName,
    string? OtherNames,
    string? LastName,
    string? SecondLastName,
    string? MobilePhone,
    Guid? RolePublicId,
    Guid? PositionPublicId,
    string? CentralUsername)
{
    public static DatosDeActivacion? De(IngenIA365ERP.Domain.Entities.Admin.Invitation invitacion, string? centralUsername) =>
        invitacion.RequiresDocument || invitacion.RolePublicId is not null || invitacion.PositionPublicId is not null || centralUsername is not null
            ? new(invitacion.DocumentType, invitacion.DocumentNumber, invitacion.FirstName, invitacion.OtherNames,
                invitacion.LastName, invitacion.SecondLastName, invitacion.MobilePhone,
                invitacion.RolePublicId, invitacion.PositionPublicId, centralUsername)
            : null;
}

internal sealed class TenantUserProvisioner(
    ITenantDbContextFactory fabrica,
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
        string tenantDatabase,
        string? tenantConnectionOverride,
        bool asTenantAdmin,
        CancellationToken ct,
        DatosDeActivacion? activacion = null)
    {
        if (centralUserId == Guid.Empty)
            return Result.Failure<int>("Provisioning.InvalidCentralUserId",
                "El CentralUserId no puede ser Guid.Empty.");
        if (string.IsNullOrWhiteSpace(centralUserEmail))
            return Result.Failure<int>("Provisioning.InvalidEmail",
                "El email del usuario central no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(tenantDatabase) && string.IsNullOrWhiteSpace(tenantConnectionOverride))
            return Result.Failure<int>("Provisioning.InvalidDatabase",
                "Hace falta la base de datos de la cooperativa destino.");

        var normalized = centralUserEmail.Trim();

        // El contexto se abre sobre el esquema de la cooperativa destino, no se
        // toma del contenedor. El XML doc de arriba declaraba esa precondicion
        // desde el principio —"asume que el contexto ya apunta al schema del
        // tenant destino"— y no la cumplia nadie: esta ruta es anonima y el
        // contenedor no tiene cooperativa que resolver.
        await using var ambito = fabrica.Abrir(tenantDatabase, tenantConnectionOverride);
        var db = ambito.Db;

        // IgnoreQueryFilters: queremos detectar también las filas soft-deleted
        // para reactivarlas en lugar de crear una nueva (idempotencia tras
        // revocaciones previas).
        // Se busca por la IDENTIDAD CENTRAL, que es lo que de verdad identifica a
        // una persona entre cooperativas. El correo queda como respaldo, y solo
        // para filas creadas antes del cutover: dos correos iguales son la misma
        // persona hoy, pero un cambio de correo rompia el vinculo en silencio.
        var existing = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.CentralUserId == centralUserId, ct)
            ?? await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    u => u.CentralUserId == null &&
                         (u.Username == normalized || u.Email == normalized),
                    ct);

        if (existing is not null)
        {
            // Las propiedades CentralUserId / CentralUserPublicEmail están
            // Ignored() en EF hasta el cutover de T017 — se asignan aquí en
            // memoria para que el resto del handler las pueda leer dentro
            // del scope del request. No se persisten todavía.
            // Al encontrarla por correo, se sella el vinculo para que la proxima vez
            // se resuelva por identidad y no por cadena.
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

            await CompletarDesdeLaInvitacionAsync(db, existing, activacion, ct);
            await AsegurarRolAsync(db, existing.Id, tenantDatabase, asTenantAdmin, ct, activacion?.RolePublicId);
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
        await CompletarDesdeLaInvitacionAsync(db, newUser, activacion, ct);

        logger.LogInformation(
            "SEC_Users provisionado para CentralUser {CentralUserId} en tenant {TenantId} (nuevo UserId={UserId}).",
            centralUserId, tenantId, newUser.Id);

        await AsegurarRolAsync(db, newUser.Id, tenantDatabase, asTenantAdmin, ct, activacion?.RolePublicId);
        return Result.Success(newUser.Id);
    }

    /// <summary>
    /// Feature 017: enlaza la fila con la persona de la cooperativa (por usuario derivado; la crea con
    /// los datos de la invitación si no existe, sin tocar una que ya exista), el cargo y la copia del
    /// usuario central. Nunca se enlaza por correo ni por documento: por el <c>Id</c> de la persona.
    /// </summary>
    private async Task CompletarDesdeLaInvitacionAsync(
        IApplicationDbContext db, User usuario, DatosDeActivacion? activacion, CancellationToken ct)
    {
        if (activacion is null) return;

        if (usuario.PersonId is null && activacion.DocumentType is not null && activacion.DocumentNumber is not null)
        {
            var tipo = TiposDeDocumento.PorCodigo(activacion.DocumentType);
            if (tipo is not null)
            {
                var clave = UsuarioDeDocumento.DerivarParaPersona(tipo.CodigoDePersona, activacion.DocumentNumber);
                var persona = await db.People.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Username == clave, ct);
                if (persona is null)
                {
                    persona = new Person
                    {
                        IdType = tipo.CodigoDePersona,
                        TaxId = activacion.DocumentNumber,
                        Username = clave,
                        FirstName = activacion.FirstName ?? string.Empty,
                        OtherNames = activacion.OtherNames,
                        LastName = activacion.LastName ?? string.Empty,
                        SecondLastName = activacion.SecondLastName,
                        Mobile = activacion.MobilePhone,
                        Email = usuario.Email,
                        PersonType = "01",
                        Status = "A",
                        CreatedBy = ProvisionerActor,
                    };
                    db.People.Add(persona);
                    await db.SaveChangesAsync(ct);
                }
                // Una persona eliminada con ese documento no se resucita por una invitación: queda sin
                // enlace y la cooperativa la restaura desde Personas si corresponde.
                if (!persona.IsDeleted)
                    usuario.PersonId = persona.Id;
            }
        }

        if (activacion.PositionPublicId is { } cargo)
        {
            usuario.PositionId = await db.Positions
                .Where(p => p.PublicId == cargo && !p.IsDeleted)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct) ?? usuario.PositionId;
        }

        if (activacion.CentralUsername is not null)
            usuario.CentralUsername = activacion.CentralUsername;

        usuario.UpdatedBy = ProvisionerActor;
        await db.SaveChangesAsync(ct);
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
        IApplicationDbContext db, int userId, string tenantDatabase, bool asTenantAdmin,
        CancellationToken ct, Guid? rolElegido = null)
    {
        var codigo = asTenantAdmin ? RolAdministrador : RolPorDefecto;

        // Feature 017: el rol que eligió el administrador al invitar manda sobre «sólo lectura»; el
        // de administrador lo da sólo la invitación del maestro.
        int? rolId = null;
        if (!asTenantAdmin && rolElegido is { } elegido)
        {
            rolId = await db.Roles
                .Where(r => r.PublicId == elegido && r.IsActive && r.IsAssignable && !r.IsDeleted)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(ct);
        }

        rolId ??= await db.Roles
            .Where(r => r.Code == codigo && r.IsActive && !r.IsDeleted)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (rolId is null)
        {
            logger.LogWarning(
                "La base {Base} no tiene el rol {Codigo}, asi que el usuario " +
                "{UserId} queda sin permisos. Suele significar que nunca se aprovisiono: " +
                "POST /api/saas/tenants/{{publicId}}/provision es idempotente y lo arregla.",
                tenantDatabase, codigo, userId);
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
            "Usuario {UserId} asignado al rol {Codigo} de la cooperativa {Base}.",
            userId, codigo, tenantDatabase);
    }
}
