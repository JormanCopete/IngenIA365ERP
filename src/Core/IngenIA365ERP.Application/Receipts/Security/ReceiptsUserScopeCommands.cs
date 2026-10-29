using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Security;

// Feature 015, US12 (T0059; R-30; FR-011, FR-175; contracts/api.md §2): el alcance de Recaudo de un usuario —las oficinas, de sólo lectura (las asigna
// Seguridad), y sus puntos, cajas y pagadurías—. Quien asigna sólo reparte lo que opera: fuera de su alcance, el punto o la caja no existen. Molde:
// LendingUserScopeCommands. (nuevo)

public sealed record ScopeUserDto(Guid UserPublicId, string Name);

public sealed record ScopeItemDto(Guid PublicId, string Code, string Name);

public sealed record ReceiptsUserScopeDto(
    ScopeUserDto User,
    bool HasAll,
    IReadOnlyList<ScopeItemDto> Offices,
    IReadOnlyList<ScopeItemDto> CollectionPoints,
    IReadOnlyList<ScopeItemDto> CashBoxes,
    IReadOnlyList<ScopeItemDto> Paymasters);

/// <summary><c>GET /api/receipts/users/{userId}/scope</c> (<c>Receipts.Scope.Manage</c>). Usuario inexistente → <c>Generic.NotFound</c>. (nuevo)</summary>
public sealed record GetReceiptsUserScopeQuery(Guid UserPublicId) : IRequest<Result<ReceiptsUserScopeDto>>;

public sealed class GetReceiptsUserScopeQueryHandler(IApplicationDbContext db, IAdminDbContext admin, ICurrentTenantService cooperativa,
    OficinasDelUsuario oficinas, IDateTimeService reloj) : IRequestHandler<GetReceiptsUserScopeQuery, Result<ReceiptsUserScopeDto>>
{
    public async Task<Result<ReceiptsUserScopeDto>> Handle(GetReceiptsUserScopeQuery request, CancellationToken ct)
    {
        var usuario = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        return usuario is null
            ? Result.Failure<ReceiptsUserScopeDto>(ErroresDeRecaudo.NoEncontrado("El usuario"))
            : Result.Success(await AlcanceDeUsuarioDeRecaudo.DtoAsync(db, admin, cooperativa, oficinas, usuario, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>PUT /api/receipts/users/{userId}/scope</c> (<c>Receipts.Scope.Manage</c>): reemplaza los puntos, cajas y pagadurías del usuario. Las filas que siguen se
/// conservan, las nuevas se agregan y las retiradas quedan de baja lógica. Un punto o una caja inexistente o fuera del alcance de quien asigna, o una
/// pagaduría que no es persona del maestro, responde <c>Generic.NotFound</c>. (nuevo)
/// </summary>
public sealed record SetReceiptsUserScopeCommand(
    Guid UserPublicId,
    IReadOnlyList<Guid> CollectionPointPublicIds,
    IReadOnlyList<Guid> CashBoxPublicIds,
    IReadOnlyList<Guid> PaymasterPersonPublicIds) : IRequest<Result<ReceiptsUserScopeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetReceiptsUserScopeCommandValidator : AbstractValidator<SetReceiptsUserScopeCommand>
{
    public SetReceiptsUserScopeCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.CollectionPointPublicIds).NotNull().WithMessage("Mande la lista de puntos (puede ir vacía).");
        RuleFor(x => x.CashBoxPublicIds).NotNull().WithMessage("Mande la lista de cajas (puede ir vacía).");
        RuleFor(x => x.PaymasterPersonPublicIds).NotNull().WithMessage("Mande la lista de pagadurías (puede ir vacía).");
    }
}

public sealed class SetReceiptsUserScopeCommandHandler(
    IApplicationDbContext db,
    IAdminDbContext admin,
    ICurrentTenantService cooperativa,
    OficinasDelUsuario oficinas,
    IAlcanceDeRecaudo alcanceDeQuienAsigna,
    IDateTimeService reloj) : IRequestHandler<SetReceiptsUserScopeCommand, Result<ReceiptsUserScopeDto>>
{
    public async Task<Result<ReceiptsUserScopeDto>> Handle(SetReceiptsUserScopeCommand request, CancellationToken ct)
    {
        var usuario = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (usuario is null) return Result.Failure<ReceiptsUserScopeDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var mio = await alcanceDeQuienAsigna.ObtenerAsync(ct);

        var puntosPedidos = request.CollectionPointPublicIds.Distinct().ToList();
        var puntos = await db.CollectionPoints.AsNoTracking().Where(p => puntosPedidos.Contains(p.PublicId)).ToListAsync(ct);
        if (puntos.Count != puntosPedidos.Count || puntos.Any(p => !(mio.IncluyePunto(p.Id) || mio.IncluyeOficina(p.BranchId))))
            return Result.Failure<ReceiptsUserScopeDto>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));

        var cajasPedidas = request.CashBoxPublicIds.Distinct().ToList();
        var cajas = await (from c in db.CashBoxes.AsNoTracking()
                           join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                           where cajasPedidas.Contains(c.PublicId)
                           select new { Caja = c, Punto = p }).ToListAsync(ct);
        if (cajas.Count != cajasPedidas.Count || cajas.Any(c => !(mio.IncluyeCaja(c.Caja.Id, c.Punto.Id) || mio.IncluyeOficina(c.Punto.BranchId))))
            return Result.Failure<ReceiptsUserScopeDto>(ErroresDeRecaudo.NoEncontrado("La caja"));

        var pagaduriasPedidas = request.PaymasterPersonPublicIds.Distinct().ToList();
        var pagadurias = await db.People.AsNoTracking().Where(p => pagaduriasPedidas.Contains(p.PublicId)).Select(p => p.Id).ToListAsync(ct);
        if (pagadurias.Count != pagaduriasPedidas.Count || pagadurias.Any(p => !mio.IncluyePagaduria(p)))
            return Result.Failure<ReceiptsUserScopeDto>(ErroresDeRecaudo.NoEncontrado("La pagaduría"));

        var ahora = reloj.UtcNow;
        Reemplazar(db.UserCollectionPointScopes, await db.UserCollectionPointScopes.Where(s => s.UserId == usuario.Id).ToListAsync(ct),
            puntos.Select(p => p.Id).ToHashSet(), s => s.CollectionPointId, id => new UserCollectionPointScope { UserId = usuario.Id, CollectionPointId = id }, ahora);
        Reemplazar(db.UserCashBoxScopes, await db.UserCashBoxScopes.Where(s => s.UserId == usuario.Id).ToListAsync(ct),
            cajas.Select(c => c.Caja.Id).ToHashSet(), s => s.CashBoxId, id => new UserCashBoxScope { UserId = usuario.Id, CashBoxId = id }, ahora);
        Reemplazar(db.UserPaymasterScopes, await db.UserPaymasterScopes.Where(s => s.UserId == usuario.Id).ToListAsync(ct),
            pagadurias.ToHashSet(), s => s.PaymasterPersonId, id => new UserPaymasterScope { UserId = usuario.Id, PaymasterPersonId = id }, ahora);
        await db.SaveChangesAsync(ct);
        return Result.Success(await AlcanceDeUsuarioDeRecaudo.DtoAsync(db, admin, cooperativa, oficinas, usuario, reloj.HoyLocal, ct));
    }

    /// <summary>Las filas que siguen quedan, las nuevas se agregan y las retiradas quedan de baja lógica.</summary>
    private static void Reemplazar<T>(DbSet<T> tabla, IReadOnlyList<T> actuales, IReadOnlySet<int> pedidos, Func<T, int> objeto, Func<int, T> nueva, DateTime ahora)
        where T : Domain.Common.AuditableEntity
    {
        foreach (var sale in actuales.Where(a => !pedidos.Contains(objeto(a))))
        {
            sale.IsDeleted = true;
            sale.DeletedAt = ahora;
        }
        var siguen = actuales.Select(objeto).ToHashSet();
        foreach (var id in pedidos.Where(p => !siguen.Contains(p))) tabla.Add(nueva(id));
    }
}

/// <summary>El DTO del alcance de Recaudo de un usuario (lo comparten la consulta y el comando). (nuevo)</summary>
internal static class AlcanceDeUsuarioDeRecaudo
{
    public static async Task<ReceiptsUserScopeDto> DtoAsync(IApplicationDbContext db, IAdminDbContext admin, ICurrentTenantService cooperativa,
        OficinasDelUsuario oficinas, Domain.Entities.Security.User usuario, DateOnly hoy, CancellationToken ct)
    {
        var permisos = await (from ur in db.UserRoles.AsNoTracking()
                              join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                              join rp in db.RolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                              join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                              where ur.UserId == usuario.Id && r.IsActive && !r.IsDeleted && !rp.IsDeleted && !p.IsDeleted
                              select p.Resource + "." + p.Action).Distinct().ToListAsync(ct);
        var filas = await AlcanceDeRecaudoDeUnUsuario.FilasAsync(db, admin, cooperativa, oficinas, usuario.Id, hoy, ct);

        var oficinasDto = await db.Branches.AsNoTracking().Where(b => filas.Oficinas.Contains(b.Id)).OrderBy(b => b.Name)
            .Select(b => new ScopeItemDto(b.PublicId, b.LegacyCode ?? string.Empty, b.Name)).ToListAsync(ct);
        var puntos = await db.CollectionPoints.AsNoTracking().Where(p => filas.Puntos.Contains(p.Id)).OrderBy(p => p.Code)
            .Select(p => new ScopeItemDto(p.PublicId, p.Code, p.Name)).ToListAsync(ct);
        var cajas = await db.CashBoxes.AsNoTracking().Where(c => filas.Cajas.Contains(c.Id)).OrderBy(c => c.Code)
            .Select(c => new ScopeItemDto(c.PublicId, c.Code, c.Name)).ToListAsync(ct);
        var personas = await db.People.AsNoTracking().Where(p => filas.Pagadurias.Contains(p.Id))
            .Select(p => new { p.PublicId, p.TaxId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName }).ToListAsync(ct);
        var pagadurias = personas
            .Select(p => new ScopeItemDto(p.PublicId, p.TaxId ?? string.Empty, PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName)))
            .OrderBy(p => p.Name).ToList();

        return new ReceiptsUserScopeDto(new ScopeUserDto(usuario.PublicId, usuario.Username),
            permisos.Contains(AlcanceDeRecaudoDelActor.TodoElAlcance, StringComparer.OrdinalIgnoreCase), oficinasDto, puntos, cajas, pagadurias);
    }
}
