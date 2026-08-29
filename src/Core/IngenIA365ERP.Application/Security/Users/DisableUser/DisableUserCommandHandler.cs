using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.DisableUser;

public sealed class DisableUserCommandHandler : IRequestHandler<DisableUserCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IRefreshTokenStore? _refreshStore;
    private readonly IAdminDbContext _admin;
    private readonly ICurrentTenantService _cooperativaActual;
    private readonly ICentralIdentityProvider _identidadCentral;
    private readonly IMembershipChangedNotifier? _avisoDeMembresia;

    public DisableUserCommandHandler(
        IApplicationDbContext db,
        IAdminDbContext admin,
        ICurrentUserService currentUser,
        ICurrentTenantService cooperativaActual,
        ICentralIdentityProvider identidadCentral,
        IDateTimeService clock,
        IRefreshTokenStore? refreshStore = null,
        IMembershipChangedNotifier? avisoDeMembresia = null)
    {
        _db = db;
        _admin = admin;
        _currentUser = currentUser;
        _cooperativaActual = cooperativaActual;
        _identidadCentral = identidadCentral;
        _clock = clock;
        _refreshStore = refreshStore;
        _avisoDeMembresia = avisoDeMembresia;
    }

    public async Task<Result> Handle(DisableUserCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null)
        {
            return Result.Failure("Generic.NotFound", "El usuario no existe.");
        }

        // Auto-protección: no permitir que el usuario actual se deshabilite a sí mismo.
        if (_currentUser.UserId is { } actorId && actorId == user.Id)
        {
            return Result.Failure("Security.Users.CannotDisableSelf",
                "No puedes deshabilitar tu propia cuenta.");
        }

        if (user.IsDeleted)
        {
            return Result.Failure(UserErrorCodes.AlreadyDisabled,
                "El usuario ya estaba deshabilitado.");
        }

        var now = _clock.UtcNow;
        var actor = _currentUser.UserName ?? "SYSTEM";
        user.IsActive = false;
        user.IsDeleted = true;
        user.DeletedAt = now;
        user.DeletedBy = actor;
        user.UpdatedBy = actor;

        // Revocar todos los refresh activos.
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        var families = new HashSet<Guid>();
        foreach (var t in activeTokens)
        {
            t.RevokedAt = now;
            t.RevocationReason = "AdminRevoke";
            families.Add(t.FamilyId);
        }

        await _db.SaveChangesAsync(ct);

        if (_refreshStore is not null)
        {
            foreach (var fam in families)
            {
                await _refreshStore.InvalidateFamilyAsync(fam.ToString(), ct);
            }
        }

        await RevocarMembresiaAsync(user.Email ?? user.Username, now, ct);

        return Result.Success();
    }

    /// <summary>
    /// Corta también el acceso CENTRAL a esta cooperativa.
    ///
    /// <para>
    /// Antes esto sólo marcaba la fila de <c>SEC_Users</c> y revocaba los tokens de
    /// refresco operativos. Pero el acceso valida contra la identidad central y las
    /// sesiones vivas son las centrales: la persona seguía entrando y seguía
    /// recibiendo un token con esta cooperativa activa. El botón decía
    /// «deshabilitado» y no deshabilitaba nada.
    /// </para>
    ///
    /// <para>
    /// Se revoca la <b>membresía</b>, no la identidad: esa persona puede pertenecer
    /// a otras cooperativas y borrarla del todo la echaría de todas. Lo que se
    /// quita es su acceso a ésta.
    /// </para>
    ///
    /// <para>
    /// Si algo falla aquí no se deshace lo anterior: dejar al usuario deshabilitado
    /// en la cooperativa y avisar es mejor que revertir y dejarlo dentro.
    /// </para>
    /// </summary>
    private async Task RevocarMembresiaAsync(string? correo, DateTime ahora, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correo)) return;
        if (!Guid.TryParse(_cooperativaActual.TenantId, out var cooperativa)) return;

        var central = await _identidadCentral.FindByEmailAsync(correo, ct);
        if (central is null) return;

        var membresia = await _admin.TenantMemberships
            .FirstOrDefaultAsync(
                m => m.CentralUserId == central.Id && m.TenantId == cooperativa, ct);

        if (membresia is null || membresia.Status != MembershipStatus.Active) return;

        membresia.Revoke(byUserId: central.Id, ahora);
        await _admin.SaveChangesAsync(ct);

        if (_avisoDeMembresia is not null)
        {
            await _avisoDeMembresia.PublishAsync(membresia.CentralUserId, ct);
        }
    }
}
