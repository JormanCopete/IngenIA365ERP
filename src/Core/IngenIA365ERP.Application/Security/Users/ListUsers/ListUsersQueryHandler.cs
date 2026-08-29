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

    public ListUsersQueryHandler(
        IApplicationDbContext db,
        ICentralIdentityProvider identidadCentral,
        ILoginAttemptCounter contadorDeIntentos)
    {
        _db = db;
        this.identidadCentral = identidadCentral;
        this.contadorDeIntentos = contadorDeIntentos;
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
                await EstaBloqueadoAsync(f.Email, ct),
                snapshot?.LastLoginAt,
                f.Roles));
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
    private async Task<bool?> EstaBloqueadoAsync(string? correo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correo)) return null;

        var normalizado = correo.Trim().ToUpperInvariant();

        // Se miran LOS DOS ámbitos. Sólo se consultaba el de contraseña, así que
        // quien se equivocaba con su código de segundo factor quedaba encerrado y
        // aquí aparecía como no bloqueado: el administrador que iba a ayudarle veía
        // una fila normal, pulsaba desbloquear, y recibía «el usuario no está
        // bloqueado». La pantalla decía la verdad sobre una pregunta que nadie
        // había hecho.
        var porContrasena = await contadorDeIntentos.CheckAsync(
            AmbitoDeIntentos.Password, normalizado, ct);
        if (porContrasena.IsLocked) return true;

        var porSegundoFactor = await contadorDeIntentos.CheckAsync(
            AmbitoDeIntentos.Mfa, normalizado, ct);

        return porSegundoFactor.IsLocked;
    }
}
