using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Sales.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pricing;

// Topes de descuento por rol (feature 012, I3, T599; contracts/api.md §19.3; FR-054, T51). La regla es ReglasDeTopesDeDescuento,
// que comparte la plantilla 13; el tope efectivo de un usuario lo calcula TopeDeDescuento.Efectivo (puro). (nuevo)

/// <summary>
/// Alta de un tope (§19.3, <c>POST /api/inventory/discount-caps</c> → 201): cierra la vigencia anterior del rol la víspera; si
/// ya hay uno que empieza ese día o después → <c>Inventory.DiscountCap.Overlaps</c>. Los topes van como fracción (0,05 = 5 %).
/// (nuevo)
/// </summary>
public sealed record CreateDiscountCapCommand(
    Guid RolePublicId,
    decimal MaxLinePercent,
    decimal MaxDocumentPercent,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason)
    : IRequest<Result<Guid>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateDiscountCapCommandValidator : ValidadorConMotivo<CreateDiscountCapCommand>
{
    public CreateDiscountCapCommandValidator()
    {
        RuleFor(x => x.RolePublicId).NotEmpty();
        RuleFor(x => x.MaxLinePercent).InclusiveBetween(0m, 1m).PrecisionScale(9, 6, ignoreTrailingZeros: true)
            .WithMessage("El tope por línea es una fracción entre 0 y 1 (0,05 = 5 %), con hasta seis decimales.");
        RuleFor(x => x.MaxDocumentPercent).InclusiveBetween(0m, 1m).PrecisionScale(9, 6, ignoreTrailingZeros: true)
            .WithMessage("El tope por total es una fracción entre 0 y 1 (0,05 = 5 %), con hasta seis decimales.");
        RuleFor(x => x.ValidFrom).NotEmpty();
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La vigencia termina antes de empezar.");
    }
}

public sealed class CreateDiscountCapCommandHandler(IApplicationDbContext db, ICerrojoPorClave cerrojo) : IRequestHandler<CreateDiscountCapCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateDiscountCapCommand request, CancellationToken ct)
    {
        var rol = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RolePublicId && !r.IsDeleted, ct);
        if (rol is null) return Result.Failure<Guid>(ErroresDePrecios.DiscountCapRoleNotFound());
        var tope = await ReglasDeTopesDeDescuento.AltaAsync(db, cerrojo,
            new DatosDeTope(rol.Id, request.MaxLinePercent, request.MaxDocumentPercent, request.ValidFrom, request.ValidTo, request.Reason), ct);
        if (tope.IsFailure) return Result.Failure<Guid>(tope.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(tope.Value.PublicId);
    }
}

/// <summary>Los topes (§19.3, <c>GET /discount-caps?role=&amp;asOf=</c>), por rol y fecha. (nuevo)</summary>
public sealed record ListDiscountCapsQuery(Guid? RolePublicId = null, DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<DiscountCapDto>>>;

public sealed class ListDiscountCapsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListDiscountCapsQuery, Result<IReadOnlyList<DiscountCapDto>>>
{
    public async Task<Result<IReadOnlyList<DiscountCapDto>>> Handle(ListDiscountCapsQuery request, CancellationToken ct)
    {
        var consulta = from t in db.DiscountCaps.AsNoTracking()
                       join r in db.Roles.AsNoTracking().IgnoreQueryFilters() on t.RoleId equals r.Id
                       where !t.IsDeleted
                       select new { t, r.PublicId, r.Code, r.Name };
        if (request.RolePublicId is { } rol) consulta = consulta.Where(x => x.PublicId == rol);
        if (request.AsOf is { } fecha) consulta = consulta.Where(x => x.t.ValidFrom <= fecha && (x.t.ValidTo == null || x.t.ValidTo >= fecha));
        var filas = await consulta.OrderBy(x => x.Code).ThenBy(x => x.t.ValidFrom).ToListAsync(ct);
        IReadOnlyList<DiscountCapDto> lista = filas
            .Select(x => new DiscountCapDto(x.t.PublicId, x.PublicId, x.Code, x.Name, x.t.MaxLineRate, x.t.MaxDocumentRate, x.t.ValidFrom, x.t.ValidTo))
            .ToList();
        return Result.Success(lista);
    }
}

/// <summary>
/// El tope efectivo de quien pregunta (§19.3, <c>GET /discount-caps/mine?date=</c>): el mayor entre los topes vigentes de sus
/// roles activos, por línea y por total; un rol sin fila da 0. (nuevo)
/// </summary>
public sealed record GetMyDiscountCapQuery(DateOnly? Date = null) : IRequest<Result<MyDiscountCapDto>>;

public sealed class GetMyDiscountCapQueryHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<GetMyDiscountCapQuery, Result<MyDiscountCapDto>>
{
    public async Task<Result<MyDiscountCapDto>> Handle(GetMyDiscountCapQuery request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } yo) return Result.Success(new MyDiscountCapDto(0m, 0m, []));
        var tope = await TopesDeDescuento.DelUsuarioAsync(db, yo, request.Date ?? reloj.HoyLocal, ct);
        var roles = await db.Roles.AsNoTracking().Where(r => tope.FromRoles.Contains(r.Id))
            .OrderBy(r => r.Code).Select(r => new DiscountCapRoleDto(r.PublicId, r.Code, r.Name)).ToListAsync(ct);
        return Result.Success(new MyDiscountCapDto(tope.MaxLineRate, tope.MaxDocumentRate, roles));
    }
}

// ================================================================================================ reglas --

/// <summary>Lo que se escribe de un tope, con el rol ya resuelto. (nuevo)</summary>
public sealed record DatosDeTope(int RoleId, decimal MaxLineRate, decimal MaxDocumentRate, DateOnly ValidFrom, DateOnly? ValidTo, string Reason);

/// <summary>
/// La regla única de los topes (feature 012, I3, T599): la usan el alta y la plantilla 13. Toma el candado del rol, rechaza un
/// tope que empiece el mismo día o antes que el último del rol (<c>Inventory.DiscountCap.Overlaps</c>) y cierra el anterior la
/// víspera. (nuevo)
/// </summary>
public static class ReglasDeTopesDeDescuento
{
    public static async Task<Result<DiscountCap>> AltaAsync(IApplicationDbContext db, ICerrojoPorClave cerrojo, DatosDeTope datos, CancellationToken ct)
    {
        if (datos.MaxLineRate is < 0m or > 1m || datos.MaxDocumentRate is < 0m or > 1m)
            return Result.Failure<DiscountCap>(new Error(Error.Validation.Code, "Un tope es una fracción entre 0 y 1."));
        if (datos.ValidTo is { } hasta && hasta < datos.ValidFrom)
            return Result.Failure<DiscountCap>(new Error(Error.Validation.Code, "La vigencia termina antes de empezar."));

        await cerrojo.BloquearAsync(ClavesDeCerrojo.TopeDelRol(datos.RoleId), ct);

        // Seguidas: las guardadas se cargan al contexto y se suman las que este mismo contexto agregó sin guardar (la plantilla 13).
        var delRol = await db.DiscountCaps.Where(t => t.RoleId == datos.RoleId && !t.IsDeleted).ToListAsync(ct);
        delRol.AddRange(db.DiscountCaps.Local.Where(t => t.RoleId == datos.RoleId && !t.IsDeleted && !delRol.Contains(t)));

        var posterior = delRol.Where(t => t.ValidFrom >= datos.ValidFrom).OrderBy(t => t.ValidFrom).FirstOrDefault();
        if (posterior is not null)
            return Result.Failure<DiscountCap>(ErroresDePrecios.DiscountCapOverlaps(posterior.PublicId, posterior.ValidFrom, posterior.ValidTo));

        var vispera = datos.ValidFrom.AddDays(-1);
        foreach (var anterior in delRol.Where(t => t.ValidTo is null || t.ValidTo > vispera))
            anterior.ValidTo = vispera;

        var tope = new DiscountCap
        {
            RoleId = datos.RoleId,
            MaxLineRate = datos.MaxLineRate,
            MaxDocumentRate = datos.MaxDocumentRate,
            ValidFrom = datos.ValidFrom,
            ValidTo = datos.ValidTo,
            Reason = datos.Reason.Trim(),
        };
        db.DiscountCaps.Add(tope);
        return Result.Success(tope);
    }
}

/// <summary>
/// El tope de descuento de un usuario leído de la base (feature 012, I3, T599, T601, T602): sus roles activos (<c>SEC_UserRoles</c>
/// vivas sobre <c>SEC_Roles</c> activos y vivos) y los topes de esos roles, resueltos por <see cref="TopeDeDescuento.Efectivo"/>.
/// Lo usan <c>GET /discount-caps/mine</c>, la precificación de la venta y la aprobación de descuentos (el tope del aprobador).
/// (nuevo)
/// </summary>
public static class TopesDeDescuento
{
    public static async Task<TopeDelUsuario> DelUsuarioAsync(IApplicationDbContext db, int userId, DateOnly fecha, CancellationToken ct)
    {
        var roles = await (from ur in db.UserRoles.AsNoTracking()
                           join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                           where ur.UserId == userId && !r.IsDeleted && r.IsActive // la junción rol-usuario no tiene borrado lógico
                           select r.Id).Distinct().ToListAsync(ct);
        if (roles.Count == 0) return TopeDelUsuario.Ninguno;

        var topes = await db.DiscountCaps.AsNoTracking()
            .Where(t => roles.Contains(t.RoleId) && !t.IsDeleted && t.ValidFrom <= fecha && (t.ValidTo == null || t.ValidTo >= fecha))
            .Select(t => new TopeDeRol(t.RoleId, t.MaxLineRate, t.MaxDocumentRate, t.ValidFrom, t.ValidTo))
            .ToListAsync(ct);
        return TopeDeDescuento.Efectivo(topes, fecha);
    }
}
