using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.ListUsers;

public sealed class ListUsersQueryHandler
    : IRequestHandler<ListUsersQuery, Result<PagedResult<UserListItemDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICentralIdentityProvider identidadCentral;
    private readonly ILoginAttemptCounter contadorDeIntentos;
    private readonly IAdminDbContext admin;
    private readonly ICurrentCentralUserContext sesion;

    public ListUsersQueryHandler(
        IApplicationDbContext db,
        ICentralIdentityProvider identidadCentral,
        ILoginAttemptCounter contadorDeIntentos,
        IAdminDbContext admin,
        ICurrentCentralUserContext sesion)
    {
        _db = db;
        this.identidadCentral = identidadCentral;
        this.contadorDeIntentos = contadorDeIntentos;
        this.admin = admin;
        this.sesion = sesion;
    }

    public async Task<Result<PagedResult<UserListItemDto>>> Handle(
        ListUsersQuery request, CancellationToken ct)
    {
        var paging = request.Paging ?? new PageRequest();
        var page = paging.SafePage;
        var pageSize = paging.SafePageSize;

        var query = request.IncludeDisabled
            ? _db.Users.IgnoreQueryFilters()
            : _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(u =>
                EF.Functions.Like(u.Username, $"%{s}%") ||
                (u.Email != null && EF.Functions.Like(u.Email, $"%{s}%")));
        }

        if (!string.IsNullOrWhiteSpace(request.RoleCode))
        {
            var code = request.RoleCode;
            query = query.Where(u => u.Roles.Any(r => r.Code == code));
        }

        var total = await query.LongCountAsync(ct);

        // La página, con lo que SÍ vive en la base de la cooperativa.
        var filas = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.PublicId,
                u.Username,
                u.Email,
                Activo = u.IsActive && !u.IsDeleted,
                u.CentralUserId,
                u.CentralUsername,
                Cargo = u.Position != null ? u.Position.Name : null,
                Roles = u.Roles.Select(r => r.Code).ToList(),
            })
            .ToListAsync(ct);

        // Segundo factor y último acceso salen de la identidad central, en un
        // solo viaje. En SEC_Users esas columnas existen pero no las escribe
        // nadie: mostraban «No» y vacío para todo el mundo.
        var identidades = filas
            .Where(f => f.CentralUserId is not null)
            .Select(f => f.CentralUserId!.Value)
            .Distinct()
            .ToList();

        var central = await identidadCentral.GetSecuritySnapshotsAsync(identidades, ct);

        // Feature 017 (D-09): el administrador corrige sólo a quien no tiene membresía viva en otra cooperativa.
        var activa = sesion.ActiveTenantPublicId;
        var enOtras = activa is { } coop && identidades.Count > 0
            ? (await admin.TenantMemberships.AsNoTracking()
                .Where(m => identidades.Contains(m.CentralUserId) && m.TenantId != coop
                            && m.Status != Domain.Entities.Admin.MembershipStatus.Revoked)
                .Select(m => m.CentralUserId).Distinct().ToListAsync(ct)).ToHashSet()
            : [];

        var pageItems = new List<UserListItemDto>(filas.Count);
        foreach (var f in filas)
        {
            CentralUserSecuritySnapshot? snapshot = null;
            if (f.CentralUserId is not null && central.TryGetValue(f.CentralUserId.Value, out var s))
            {
                snapshot = s;
            }

            pageItems.Add(new UserListItemDto(
                f.PublicId,
                f.Username,
                f.Email,
                f.Activo,
                snapshot?.TwoFactorEnabled,
                await EstaBloqueadoAsync(f.Email, f.CentralUserId, ct),
                snapshot?.LastLoginAt,
                f.Roles,
                DocumentUsername: snapshot?.DocumentUsername,
                DocumentMismatch: snapshot?.DocumentUsername is not null && f.CentralUsername != snapshot.DocumentUsername,
                CanCorrectDocument: f.CentralUserId is { } idCentral && activa is not null && !enOtras.Contains(idCentral),
                Position: f.Cargo));
        }

        return Result.Success(new PagedResult<UserListItemDto>(pageItems, page, pageSize, total));
    }

    /// <summary>
    /// El bloqueo por intentos fallidos vive en Redis, no en una columna.
    ///
    /// <para>
    /// <c>SEC_Users.LockoutEndAt</c> sólo se pone a <c>null</c> en dos sitios y
    /// no se rellena en ninguno, así que la columna «Bloqueado» estaba vacía
    /// siempre, incluso para una cuenta efectivamente bloqueada. Una consulta a
    /// Redis por fila es el precio de que el dato sea cierto; la página trae
    /// veinte como mucho.
    /// </para>
    /// </summary>
    private async Task<bool?> EstaBloqueadoAsync(string? correo, Guid? centralUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correo)) return null;

        var normalizado = correo.Trim().ToUpperInvariant();
        // Feature 017: la contraseña se cuenta por cuenta (se entra con correo o usuario); el segundo
        // factor sigue por correo. Una fila vieja sin vínculo central sólo puede tener el contador viejo.
        var claveDeContrasena = centralUserId is { } id ? ClaveDeIntentos.DeCuenta(id) : normalizado;

        // Se miran LOS DOS ámbitos. Sólo se consultaba el de contraseña, así que
        // quien se equivocaba con su código de segundo factor quedaba encerrado y
        // aquí aparecía como no bloqueado: el administrador que iba a ayudarle veía
        // una fila normal, pulsaba desbloquear, y recibía «el usuario no está
        // bloqueado». La pantalla decía la verdad sobre una pregunta que nadie
        // había hecho.
        var porContrasena = await contadorDeIntentos.CheckAsync(
            AmbitoDeIntentos.Password, claveDeContrasena, ct);
        if (porContrasena.IsLocked) return true;

        var porSegundoFactor = await contadorDeIntentos.CheckAsync(
            AmbitoDeIntentos.Mfa, normalizado, ct);

        return porSegundoFactor.IsLocked;
    }
}
