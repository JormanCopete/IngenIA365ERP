using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pos;

// 2026-11-25 (pedido del dueño): los cajeros de una caja del punto de venta, como los de una caja de Recaudo. Con Caja.SoloCajerosAsignados encendido, una
// caja con cajeros vigentes sólo la abren ellos (CajerosDeLaCaja). La lista trae también, de sólo lectura, los cajeros de la caja de Recaudo a la que está
// unida: ésos se asignan y retiran en Recaudo. Retirar cierra la vigencia; nada se borra. Consulta con Inventory.PointsOfSale.View; asignar y retirar con
// Inventory.PointsOfSale.Manage e Idempotency-Key. (nuevo)

/// <summary>
/// Un cajero de una caja del punto de venta. <see cref="CollectionBoxCode"/> no nulo: viene de la caja de Recaudo unida (aquí es de sólo lectura).
/// <see cref="Current"/>: vigente hoy. (nuevo)
/// </summary>
public sealed record CashRegisterCashierDto(Guid UserPublicId, string UserName, string Name, DateOnly? ValidFrom, DateOnly? ValidTo, bool Current,
    string? CollectionBoxCode = null);

/// <summary><c>GET /api/inventory/points-of-sale/{id}/cash-registers/{registerId}/cashiers</c>: los propios (vigentes y pasados) y los de Recaudo. (nuevo)</summary>
public sealed record ListCashRegisterCashiersQuery(Guid PointOfSalePublicId, Guid CashRegisterPublicId) : IRequest<Result<IReadOnlyList<CashRegisterCashierDto>>>;

public sealed class ListCashRegisterCashiersQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, IDateTimeService reloj)
    : IRequestHandler<ListCashRegisterCashiersQuery, Result<IReadOnlyList<CashRegisterCashierDto>>>
{
    public async Task<Result<IReadOnlyList<CashRegisterCashierDto>>> Handle(ListCashRegisterCashiersQuery request, CancellationToken ct)
    {
        var caja = await CajerosDeLaCajaDto.CajaAsync(db, alcance, request.PointOfSalePublicId, request.CashRegisterPublicId, ct);
        if (caja.IsFailure) return Result.Failure<IReadOnlyList<CashRegisterCashierDto>>(caja.Error);
        return Result.Success(await CajerosDeLaCajaDto.ListaAsync(db, caja.Value, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>POST /api/inventory/points-of-sale/{id}/cash-registers/{registerId}/cashiers</c> (<c>Inventory.PointsOfSale.Manage</c>): asigna un cajero desde una
/// fecha (hoy por defecto). Su vigencia no se cruza con otra suya en la misma caja (<c>Inventory.CashRegister.CashierOverlaps</c>). (nuevo)
/// </summary>
public sealed record AssignCashRegisterCashierCommand(Guid PointOfSalePublicId, Guid CashRegisterPublicId, Guid UserPublicId, DateOnly? ValidFrom = null,
    DateOnly? ValidTo = null) : IRequest<Result<IReadOnlyList<CashRegisterCashierDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AssignCashRegisterCashierCommandValidator : AbstractValidator<AssignCashRegisterCashierCommand>
{
    public AssignCashRegisterCashierCommandValidator()
    {
        RuleFor(x => x.PointOfSalePublicId).NotEmpty();
        RuleFor(x => x.CashRegisterPublicId).NotEmpty();
        RuleFor(x => x.UserPublicId).NotEmpty().WithMessage("Indique el usuario.");
        RuleFor(x => x).Must(x => x.ValidTo is null || x.ValidFrom is null || x.ValidTo >= x.ValidFrom)
            .WithMessage("El fin de la vigencia no puede ser anterior a su inicio.");
    }
}

public sealed class AssignCashRegisterCashierCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, IDateTimeService reloj)
    : IRequestHandler<AssignCashRegisterCashierCommand, Result<IReadOnlyList<CashRegisterCashierDto>>>
{
    public async Task<Result<IReadOnlyList<CashRegisterCashierDto>>> Handle(AssignCashRegisterCashierCommand request, CancellationToken ct)
    {
        var caja = await CajerosDeLaCajaDto.CajaAsync(db, alcance, request.PointOfSalePublicId, request.CashRegisterPublicId, ct);
        if (caja.IsFailure) return Result.Failure<IReadOnlyList<CashRegisterCashierDto>>(caja.Error);
        var usuario = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId && u.IsActive, ct);
        if (usuario is null) return Result.Failure<IReadOnlyList<CashRegisterCashierDto>>(ErroresDePuntoDeVenta.CashierUserNotFound());

        var desde = request.ValidFrom ?? reloj.HoyLocal;
        var suyas = await db.CashRegisterCashiers.AsNoTracking().Where(c => c.CashRegisterId == caja.Value.Id && c.UserId == usuario.Id).ToListAsync(ct);
        if (suyas.Any(s => s.ValidFrom <= (request.ValidTo ?? DateOnly.MaxValue) && desde <= (s.ValidTo ?? DateOnly.MaxValue)))
            return Result.Failure<IReadOnlyList<CashRegisterCashierDto>>(ErroresDePuntoDeVenta.CashierOverlaps());

        db.CashRegisterCashiers.Add(new CashRegisterCashier { CashRegisterId = caja.Value.Id, UserId = usuario.Id, ValidFrom = desde, ValidTo = request.ValidTo });
        await db.SaveChangesAsync(ct);
        return Result.Success(await CajerosDeLaCajaDto.ListaAsync(db, caja.Value, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>POST /api/inventory/points-of-sale/{id}/cash-registers/{registerId}/cashiers/{userId}/retire</c>: cierra la vigencia abierta del cajero (por
/// defecto, ayer); si aún no empezaba, la da de baja. (nuevo)
/// </summary>
public sealed record RetireCashRegisterCashierCommand(Guid PointOfSalePublicId, Guid CashRegisterPublicId, Guid UserPublicId, DateOnly? ValidTo = null)
    : IRequest<Result<IReadOnlyList<CashRegisterCashierDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RetireCashRegisterCashierCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, IDateTimeService reloj)
    : IRequestHandler<RetireCashRegisterCashierCommand, Result<IReadOnlyList<CashRegisterCashierDto>>>
{
    public async Task<Result<IReadOnlyList<CashRegisterCashierDto>>> Handle(RetireCashRegisterCashierCommand request, CancellationToken ct)
    {
        var caja = await CajerosDeLaCajaDto.CajaAsync(db, alcance, request.PointOfSalePublicId, request.CashRegisterPublicId, ct);
        if (caja.IsFailure) return Result.Failure<IReadOnlyList<CashRegisterCashierDto>>(caja.Error);
        var hoy = reloj.HoyLocal;
        // La consulta sigue la asignación: AsNoTracking en el join apagaría el seguimiento de toda la consulta.
        var asignacion = await (from c in db.CashRegisterCashiers
                                join u in db.Users on c.UserId equals u.Id
                                where c.CashRegisterId == caja.Value.Id && u.PublicId == request.UserPublicId && (c.ValidTo == null || c.ValidTo >= hoy)
                                orderby c.ValidFrom descending
                                select c).FirstOrDefaultAsync(ct);
        if (asignacion is null) return Result.Failure<IReadOnlyList<CashRegisterCashierDto>>(ErroresDePuntoDeVenta.CashierNotFound());
        var hasta = request.ValidTo ?? hoy.AddDays(-1);
        if (hasta < asignacion.ValidFrom) asignacion.IsDeleted = true;
        else asignacion.ValidTo = hasta;
        await db.SaveChangesAsync(ct);
        return Result.Success(await CajerosDeLaCajaDto.ListaAsync(db, caja.Value, hoy, ct));
    }
}

/// <summary>Lo común de las tres rutas: la caja del punto del alcance y su lista. (nuevo)</summary>
internal static class CajerosDeLaCajaDto
{
    public static async Task<Result<CashRegister>> CajaAsync(IApplicationDbContext db, IAlcanceDeInventario alcance, Guid puntoId, Guid cajaId,
        CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, puntoId, ct);
        if (punto is null) return Result.Failure<CashRegister>(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        var caja = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == cajaId && c.PointOfSaleId == punto.Id && !c.IsDeleted, ct);
        return caja is null ? Result.Failure<CashRegister>(ErroresDePuntoDeVenta.CashRegisterNotFound()) : Result.Success(caja);
    }

    /// <summary>Los propios, vigentes primero y luego los pasados y futuros; después los vigentes que vienen de la caja de Recaudo unida.</summary>
    public static async Task<IReadOnlyList<CashRegisterCashierDto>> ListaAsync(IApplicationDbContext db, CashRegister caja, DateOnly hoy, CancellationToken ct)
    {
        var propios = await db.CashRegisterCashiers.AsNoTracking().Where(c => c.CashRegisterId == caja.Id)
            .Select(c => new { c.UserId, c.ValidFrom, c.ValidTo }).ToListAsync(ct);
        var deRecaudo = (await CajerosDeLaCaja.DeAsync(db, caja, hoy, ct)).Where(c => c.CajaDeRecaudo is not null).ToList();
        var ids = propios.Select(p => p.UserId).Concat(deRecaudo.Select(c => c.UserId)).Distinct().ToList();
        var usuarios = await (from u in db.Users.AsNoTracking().IgnoreQueryFilters()
                              join p in db.People.AsNoTracking().IgnoreQueryFilters() on u.PersonId equals p.Id into pp
                              from p in pp.DefaultIfEmpty()
                              where ids.Contains(u.Id)
                              select new
                              {
                                  u.Id, u.PublicId, u.Username,
                                  FirstName = p == null ? null : p.FirstName, OtherNames = p == null ? null : p.OtherNames,
                                  LastName = p == null ? null : p.LastName, SecondLastName = p == null ? null : p.SecondLastName,
                                  BusinessName = p == null ? null : p.BusinessName,
                              }).ToDictionaryAsync(u => u.Id, ct);

        string Nombre(int id)
        {
            var u = usuarios[id];
            var nombre = u.FirstName is null && u.BusinessName is null
                ? string.Empty
                : PersonFactory.NombreVisible(u.FirstName, u.OtherNames, u.LastName, u.SecondLastName, u.BusinessName);
            return string.IsNullOrWhiteSpace(nombre) ? u.Username : nombre;
        }

        var lista = propios.Where(p => usuarios.ContainsKey(p.UserId))
            .Select(p => new CashRegisterCashierDto(usuarios[p.UserId].PublicId, usuarios[p.UserId].Username, Nombre(p.UserId), p.ValidFrom, p.ValidTo,
                p.ValidFrom <= hoy && (p.ValidTo is null || p.ValidTo >= hoy)))
            .OrderByDescending(p => p.Current).ThenByDescending(p => p.ValidFrom).ToList();
        var vigentesPropios = lista.Where(l => l.Current).Select(l => l.UserPublicId).ToHashSet();
        lista.AddRange(deRecaudo.Where(c => usuarios.ContainsKey(c.UserId) && !vigentesPropios.Contains(c.UserPublicId))
            .Select(c => new CashRegisterCashierDto(c.UserPublicId, usuarios[c.UserId].Username, c.Nombre, null, null, true, c.CajaDeRecaudo)));
        return lista;
    }
}
