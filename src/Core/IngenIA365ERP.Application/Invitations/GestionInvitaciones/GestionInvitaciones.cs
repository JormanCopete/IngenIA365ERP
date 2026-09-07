using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Invitations.Services;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Invitations.GestionInvitaciones;

// ---------------------------------------------------------------- listar ----

/// <summary>
/// Invitaciones emitidas para una cooperativa.
///
/// <para>
/// Faltaba: se podía emitir una invitación y revocarla conociendo su
/// identificador, pero no había forma de VER cuáles existían. Si el correo no
/// llegaba, no quedaba rastro consultable de a quién se había invitado ni en
/// qué estado estaba.
/// </para>
/// </summary>
public sealed record ListarInvitacionesQuery(Guid TenantPublicId, bool IncluirCerradas = false)
    : IRequest<Result<IReadOnlyList<InvitacionDto>>>;

public sealed record InvitacionDto(
    Guid PublicId,
    string Email,
    string Estado,
    bool ComoAdministrador,
    DateTime CreadaEn,
    DateTime ExpiraEn,
    bool Expirada,
    DateTime? AceptadaEn);

public sealed class ListarInvitacionesQueryHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext db,
    IDateTimeService clock)
    : IRequestHandler<ListarInvitacionesQuery, Result<IReadOnlyList<InvitacionDto>>>
{
    public async Task<Result<IReadOnlyList<InvitacionDto>>> Handle(
        ListarInvitacionesQuery request, CancellationToken ct)
    {
        var autorizacion = await GestionInvitacionesGuardias.VerificarAdministradorAsync(
            currentUser, db, request.TenantPublicId, ct);
        if (autorizacion is not null)
            return Result.Failure<IReadOnlyList<InvitacionDto>>(autorizacion.Code, autorizacion.Message);

        var ahora = clock.UtcNow;
        var consulta = db.Invitations
            .AsNoTracking()
            .Where(i => i.TenantId == request.TenantPublicId);

        // Por defecto sólo interesan las que siguen en juego. Las aceptadas,
        // revocadas y reemplazadas son historia y ensucian la pantalla.
        if (!request.IncluirCerradas)
            consulta = consulta.Where(i => i.Status == InvitationStatus.Pending);

        var invitaciones = await consulta
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitacionDto(
                i.PublicId,
                i.Email,
                i.Status.ToString(),
                i.InviteAsTenantAdmin,
                i.CreatedAt,
                i.ExpiresAt,
                i.Status == InvitationStatus.Pending && i.ExpiresAt <= ahora,
                i.AcceptedAt))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<InvitacionDto>>(invitaciones);
    }
}

// -------------------------------------------------------------- reenviar ----

/// <summary>
/// Vuelve a enviar una invitación al mismo correo.
///
/// <para>
/// <b>Emite un token NUEVO, no reenvía el anterior.</b> El token original sólo
/// existe en la base como hash SHA-256; recuperar el texto plano para
/// reenviarlo es justamente lo que el hash impide. Así que reenviar es
/// reemplazar: la invitación previa queda <c>Superseded</c> y nace otra con
/// token y vencimiento nuevos.
/// </para>
///
/// <para>
/// Consecuencia práctica que conviene conocer: si el destinatario encuentra el
/// correo viejo después, ese enlace ya no funciona. Es el precio de no guardar
/// tokens en claro, y es el precio correcto.
/// </para>
/// </summary>
public sealed record ReenviarInvitacionCommand(Guid InvitacionPublicId)
    : IRequest<Result<ReenvioResultado>>;

public sealed record ReenvioResultado(Guid NuevaInvitacionPublicId, string Email, DateTime ExpiraEn);

public sealed class ReenviarInvitacionCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext db,
    ISecureTokenGenerator tokens,
    IInvitationEmailDispatcher emailDispatcher,
    IDateTimeService clock,
    IOptions<IdentityEmailOptions> identityEmailOptions,
    ILogger<ReenviarInvitacionCommandHandler> logger)
    : IRequestHandler<ReenviarInvitacionCommand, Result<ReenvioResultado>>
{
    private readonly IdentityEmailOptions _opciones = identityEmailOptions.Value;

    public async Task<Result<ReenvioResultado>> Handle(
        ReenviarInvitacionCommand request, CancellationToken ct)
    {
        var original = await db.Invitations
            .FirstOrDefaultAsync(i => i.PublicId == request.InvitacionPublicId, ct);

        if (original is null)
            return Result.Failure<ReenvioResultado>(
                "Invitation.NotFound", "No existe la invitación indicada.");

        var autorizacion = await GestionInvitacionesGuardias.VerificarAdministradorAsync(
            currentUser, db, original.TenantId, ct);
        if (autorizacion is not null)
            return Result.Failure<ReenvioResultado>(autorizacion.Code, autorizacion.Message);

        // Reenviar una invitación ya aceptada crearía un segundo camino de
        // entrada para alguien que ya entró.
        if (original.Status == InvitationStatus.Accepted)
            return Result.Failure<ReenvioResultado>(
                "Invitation.YaAceptada",
                "Esa invitación ya fue aceptada. Si la persona perdió el acceso, " +
                "usá la recuperación de contraseña.");

        if (original.Status == InvitationStatus.Revoked)
            return Result.Failure<ReenvioResultado>(
                "Invitation.Revocada",
                "Esa invitación fue revocada. Emití una nueva si querés volver a invitar.");

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == original.TenantId && t.IsActive, ct);

        if (tenant is null)
            return Result.Failure<ReenvioResultado>(
                "Tenant.NotFound", "La cooperativa de esa invitación no existe o está inactiva.");

        var ahora = clock.UtcNow;

        // La anterior deja de valer ANTES de crear la nueva: dos tokens vivos
        // para el mismo correo es una puerta de más.
        original.MarkSuperseded();

        var (tokenPlano, hash) = tokens.Generate();
        var expira = ahora.AddDays(_opciones.InvitationLifetimeDays);

        var nueva = Invitation.Create(
            email: original.Email,
            normalizedEmail: original.NormalizedEmail,
            tenantId: original.TenantId,
            invitedByUserId: currentUser.CentralUserId!.Value,
            inviteAsTenantAdmin: original.InviteAsTenantAdmin,
            tokenHash: hash,
            createdAt: ahora,
            expiresAt: expira);

        db.Invitations.Add(nueva);
        await db.SaveChangesAsync(ct);

        // El envío va DESPUÉS de persistir y sin try/catch: si el correo falla,
        // que el error llegue a quien pulsó el botón. Tragárselo fue justamente
        // lo que dejó al registro de cooperativa sin avisar de nada.
        await emailDispatcher.DispatchAsync(new InvitationEmailRequest(
            Invitation: nueva,
            Tenant: tenant,
            InviterDisplayName: currentUser.Email ?? "Administrador",
            PlainTokenBase64Url: tokenPlano), ct);

        logger.LogInformation(
            "Invitación {Anterior} reenviada como {Nueva} para {Email} → tenant {Tenant} (expira {Expira}).",
            original.PublicId, nueva.PublicId, nueva.Email, tenant.Name, expira);

        return Result.Success(new ReenvioResultado(nueva.PublicId, nueva.Email, expira));
    }
}

// -------------------------------------------------------------- guardias ----

internal static class GestionInvitacionesGuardias
{
    internal sealed record Error(string Code, string Message);

    /// <summary>
    /// Master admin global pasa siempre; el resto debe ser administrador ACTIVO
    /// de esa cooperativa. Mismo criterio que al emitir: quien puede invitar
    /// puede ver y reenviar.
    /// </summary>
    internal static async Task<Error?> VerificarAdministradorAsync(
        ICurrentCentralUserContext currentUser,
        IAdminDbContext db,
        Guid tenantPublicId,
        CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return new Error("Identity.Unauthenticated", "Se requiere autenticación válida.");

        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return new Error("Identity.WrongTokenPurpose", "Este endpoint requiere purpose=full.");

        if (currentUser.IsGlobalMasterAdmin)
            return null;

        var esAdmin = await db.TenantMemberships
            .AsNoTracking()
            .AnyAsync(m => m.CentralUserId == currentUser.CentralUserId.Value
                        && m.TenantId == tenantPublicId
                        && m.Status == MembershipStatus.Active
                        && m.IsTenantAdmin, ct);

        return esAdmin
            ? null
            : new Error("Invitation.Forbidden",
                "Sólo un administrador activo de la cooperativa puede gestionar sus invitaciones.");
    }
}
