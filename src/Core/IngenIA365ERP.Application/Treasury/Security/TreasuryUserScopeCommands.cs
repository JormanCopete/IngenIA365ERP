using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Security;

// Feature 015, US12 (T0059, mitad de Tesorería que esperaba TES_BankAccounts; R-30; FR-011, FR-106, FR-175; contracts/api.md §9): el alcance de Tesorería de un
// usuario —las cuentas bancarias propias que opera, y con ellas los canales sin caja de Recaudo—. Quien asigna sólo reparte lo que opera: una cuenta fuera de su
// alcance no existe. Molde: SetReceiptsUserScopeCommand. (nuevo)

public sealed record TreasuryScopeItemDto(Guid PublicId, string Code, string Name);

public sealed record TreasuryUserScopeDto(Guid UserPublicId, string Name, bool HasAll, IReadOnlyList<TreasuryScopeItemDto> BankAccounts);

/// <summary><c>GET /api/treasury/users/{userId}/scope</c> (<c>Treasury.Scope.Manage</c>). Usuario inexistente → <c>Generic.NotFound</c>. (nuevo)</summary>
public sealed record GetTreasuryUserScopeQuery(Guid UserPublicId) : IRequest<Result<TreasuryUserScopeDto>>;

public sealed class GetTreasuryUserScopeQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTreasuryUserScopeQuery, Result<TreasuryUserScopeDto>>
{
    public async Task<Result<TreasuryUserScopeDto>> Handle(GetTreasuryUserScopeQuery request, CancellationToken ct)
    {
        var usuario = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        return usuario is null
            ? Result.Failure<TreasuryUserScopeDto>(ErroresDeTesoreria.NoEncontrado("El usuario"))
            : Result.Success(await AlcanceDeUsuarioDeTesoreria.DtoAsync(db, usuario.Id, usuario.PublicId, usuario.Username, ct));
    }
}

/// <summary>
/// <c>PUT /api/treasury/users/{userId}/scope</c> (<c>Treasury.Scope.Manage</c>): reemplaza las cuentas propias del usuario. Las que siguen se conservan, las nuevas
/// se agregan y las retiradas quedan de baja lógica. Una cuenta inexistente o fuera del alcance de quien asigna responde <c>Generic.NotFound</c>. (nuevo)
/// </summary>
public sealed record SetTreasuryUserScopeCommand(Guid UserPublicId, IReadOnlyList<Guid> BankAccountPublicIds) : IRequest<Result<TreasuryUserScopeDto>>,
    IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetTreasuryUserScopeCommandValidator : AbstractValidator<SetTreasuryUserScopeCommand>
{
    public SetTreasuryUserScopeCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.BankAccountPublicIds).NotNull().WithMessage("Mande la lista de cuentas (puede ir vacía).");
    }
}

public sealed class SetTreasuryUserScopeCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcanceDeQuienAsigna, IDateTimeService reloj)
    : IRequestHandler<SetTreasuryUserScopeCommand, Result<TreasuryUserScopeDto>>
{
    public async Task<Result<TreasuryUserScopeDto>> Handle(SetTreasuryUserScopeCommand request, CancellationToken ct)
    {
        var usuario = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (usuario is null) return Result.Failure<TreasuryUserScopeDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var mio = await alcanceDeQuienAsigna.ObtenerAsync(ct);
        var pedidas = request.BankAccountPublicIds.Distinct().ToList();
        var cuentas = await db.OwnBankAccounts.AsNoTracking().Where(c => pedidas.Contains(c.PublicId)).Select(c => c.Id).ToListAsync(ct);
        if (cuentas.Count != pedidas.Count || cuentas.Any(c => !mio.IncluyeCuenta(c)))
            return Result.Failure<TreasuryUserScopeDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));

        var actuales = await db.UserBankAccountScopes.Where(s => s.UserId == usuario.Id).ToListAsync(ct);
        var queda = cuentas.ToHashSet();
        foreach (var sale in actuales.Where(a => !queda.Contains(a.BankAccountId)))
        {
            sale.IsDeleted = true;
            sale.DeletedAt = reloj.UtcNow;
        }
        var siguen = actuales.Select(a => a.BankAccountId).ToHashSet();
        foreach (var id in queda.Where(c => !siguen.Contains(c))) db.UserBankAccountScopes.Add(new UserBankAccountScope { UserId = usuario.Id, BankAccountId = id });
        await db.SaveChangesAsync(ct);
        return Result.Success(await AlcanceDeUsuarioDeTesoreria.DtoAsync(db, usuario.Id, usuario.PublicId, usuario.Username, ct));
    }
}

/// <summary>El DTO del alcance de Tesorería de un usuario (lo comparten la consulta y el comando). (nuevo)</summary>
internal static class AlcanceDeUsuarioDeTesoreria
{
    public static async Task<TreasuryUserScopeDto> DtoAsync(IApplicationDbContext db, int usuarioId, Guid usuarioPublicId, string nombre, CancellationToken ct)
    {
        var todo = await (from ur in db.UserRoles.AsNoTracking()
                          join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                          join rp in db.RolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                          join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                          where ur.UserId == usuarioId && r.IsActive && !r.IsDeleted && !rp.IsDeleted && !p.IsDeleted
                                && p.Resource + "." + p.Action == AlcanceDeTesoreriaDelActor.TodoElAlcance
                          select p.Id).AnyAsync(ct);
        var ids = await db.UserBankAccountScopes.AsNoTracking().Where(s => s.UserId == usuarioId && !s.IsDeleted).Select(s => s.BankAccountId).ToListAsync(ct);
        var cuentas = await db.OwnBankAccounts.AsNoTracking().Where(c => ids.Contains(c.Id)).OrderBy(c => c.Code)
            .Select(c => new TreasuryScopeItemDto(c.PublicId, c.Code, c.Name)).ToListAsync(ct);
        return new TreasuryUserScopeDto(usuarioPublicId, nombre, todo, cuentas);
    }
}
