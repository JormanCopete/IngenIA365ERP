using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals.SetPermissionAmountLimit;

/// <summary>
/// Registra el monto máximo de un permiso para un rol desde una fecha (feature 012, T34, T084; contracts/api.md §15.3,
/// <c>POST /api/inventory/amount-limits</c>). <see cref="MaxAmount"/> nulo = sin límite desde <see cref="ValidFrom"/>. La
/// vigencia nueva cierra la anterior del mismo (rol, permiso) la víspera.
/// </summary>
public sealed record SetPermissionAmountLimitCommand(
    Guid RolePublicId,
    string PermissionCode,
    decimal? MaxAmount,
    DateOnly ValidFrom,
    string Reason)
    : IRequest<Result<PermissionAmountLimitDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>Motivo (hasta 300), permiso, fecha y un monto no negativo con 2 decimales.</summary>
public sealed class SetPermissionAmountLimitCommandValidator : ValidadorConMotivo<SetPermissionAmountLimitCommand>
{
    public SetPermissionAmountLimitCommandValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(300).WithMessage("El motivo admite hasta 300 caracteres.");
        RuleFor(x => x.RolePublicId).NotEqual(Guid.Empty).WithMessage("Indicá el rol.");
        RuleFor(x => x.PermissionCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige.");
        RuleFor(x => x.MaxAmount!.Value)
            .GreaterThanOrEqualTo(0).WithMessage("El monto máximo no puede ser negativo.")
            .PrecisionScale(18, 2, true).WithMessage("El monto máximo admite hasta 2 decimales.")
            .When(x => x.MaxAmount is not null);
    }
}

/// <summary>
/// El único escritor de <c>SEC_PermissionAmountLimits</c> (T084). Rol inexistente: 404 <c>Generic.NotFound</c>; sólo
/// los permisos limitables (<c>NotLimitable</c> con <c>data.limitable</c>); el rol debe conceder el permiso
/// (<c>RoleLacksPermission</c>); sin cruces (<c>Overlaps</c>); moneda <c>COP</c>.
/// </summary>
public sealed class SetPermissionAmountLimitCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SetPermissionAmountLimitCommand, Result<PermissionAmountLimitDto>>
{
    public async Task<Result<PermissionAmountLimitDto>> Handle(SetPermissionAmountLimitCommand request, CancellationToken ct)
    {
        var rol = await db.Roles.FirstOrDefaultAsync(r => r.PublicId == request.RolePublicId, ct);
        if (rol is null) return Result.Failure<PermissionAmountLimitDto>(Error.NotFound);

        var permiso = request.PermissionCode.Trim();
        if (!PermisosLimitables.Admite(permiso))
            return Result.Failure<PermissionAmountLimitDto>(ErroresDeAprobaciones.NoLimitable(permiso));

        var concede = await db.RolePermissions
            .Where(rp => rp.RoleId == rol.Id && !rp.IsDeleted)
            .Join(db.Permissions.Where(p => !p.IsDeleted), rp => rp.PermissionId, p => p.Id, (rp, p) => p.Resource + "." + p.Action)
            .AnyAsync(c => c == permiso, ct);
        if (!concede)
            return Result.Failure<PermissionAmountLimitDto>(ErroresDeAprobaciones.RolSinPermiso(rol.Name, permiso));

        var vigencias = await db.PermissionAmountLimits.Where(l => l.RoleId == rol.Id && l.PermissionCode == permiso).ToListAsync(ct);
        var posterior = vigencias.Where(v => v.ValidFrom >= request.ValidFrom).OrderBy(v => v.ValidFrom).FirstOrDefault();
        if (posterior is not null)
            return Result.Failure<PermissionAmountLimitDto>(ErroresDeAprobaciones.LimiteSeCruza(posterior.ValidFrom));

        var vispera = request.ValidFrom.AddDays(-1);
        var anterior = vigencias.OrderByDescending(v => v.ValidFrom).FirstOrDefault();
        if (anterior is not null && (anterior.ValidTo is null || anterior.ValidTo > vispera))
            anterior.ValidTo = vispera;

        var nuevo = new PermissionAmountLimit
        {
            RoleId = rol.Id,
            Role = rol,
            PermissionCode = permiso,
            MaxAmount = request.MaxAmount,
            Currency = "COP",
            ValidFrom = request.ValidFrom,
            Reason = request.Reason.Trim(),
        };
        db.PermissionAmountLimits.Add(nuevo);
        await db.SaveChangesAsync(ct);

        return Result.Success(MontosMaximos.ADto(nuevo, rol));
    }
}

/// <summary>El DTO de un monto máximo (lo comparten el alta y la consulta). (nuevo)</summary>
internal static class MontosMaximos
{
    public static PermissionAmountLimitDto ADto(PermissionAmountLimit l, Role rol) => new(
        l.PublicId, new RolDto(rol.PublicId, rol.Name), l.PermissionCode, l.MaxAmount, l.Currency,
        l.ValidFrom, l.ValidTo, l.Reason, l.CreatedBy, l.CreatedAt);
}
