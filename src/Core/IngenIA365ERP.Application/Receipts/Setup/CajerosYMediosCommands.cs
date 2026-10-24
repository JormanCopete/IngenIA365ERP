using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Setup;

// Feature 015, US1 (T0072; FR-022, FR-023, FR-024; contracts/api.md §2; data-model §1.4, §1.5): los cajeros de una caja y los medios del catálogo común
// que se ofrecen en un punto. Retirar cierra la vigencia; nada se borra (un medio con pagos no se borra). (nuevo)

// ------------------------------------------------------------------------------------------------------------- cajeros --

/// <summary><c>GET /api/receipts/cash-boxes/{id}/cashiers</c>: los cajeros de la caja, vigentes y pasados. (nuevo)</summary>
public sealed record ListCashiersQuery(Guid CashBoxPublicId) : IRequest<Result<IReadOnlyList<CashierDto>>>;

public sealed class ListCashiersQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<ListCashiersQuery, Result<IReadOnlyList<CashierDto>>>
{
    public async Task<Result<IReadOnlyList<CashierDto>>> Handle(ListCashiersQuery request, CancellationToken ct)
    {
        var hallada = await AlcanceDePuntos.CajaVisibleAsync(db, alcance, request.CashBoxPublicId, seguir: false, ct);
        if (hallada is null) return Result.Failure<IReadOnlyList<CashierDto>>(ErroresDeRecaudo.NoEncontrado("La caja"));
        return Result.Success(await CajerosDtoAsync(db, hallada.Value.Caja.Id, ct));
    }

    internal static async Task<IReadOnlyList<CashierDto>> CajerosDtoAsync(IApplicationDbContext db, int caja, CancellationToken ct)
    {
        var filas = await (from c in db.CashBoxCashiers.AsNoTracking()
                           join u in db.Users.AsNoTracking().IgnoreQueryFilters() on c.UserId equals u.Id
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on u.PersonId equals p.Id
                           where c.CashBoxId == caja
                           orderby c.ValidFrom descending
                           select new { u.PublicId, u.Username, Persona = p.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName,
                               c.ValidFrom, c.ValidTo }).ToListAsync(ct);
        return filas.Select(f => new CashierDto(f.PublicId, f.Username, f.Persona,
            Core.People.Services.PersonFactory.NombreVisible(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName, f.BusinessName), f.ValidFrom, f.ValidTo)).ToList();
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-boxes/{id}/cashiers</c> (<c>Receipts.Setup.Manage</c>): asigna un cajero desde una fecha. El usuario tiene que tener persona
/// (<c>Receipts.Cashier.PersonRequired</c>) y su vigencia no se cruza con otra suya en la misma caja (<c>Receipts.Cashier.Overlaps</c>). (nuevo)
/// </summary>
public sealed record AssignCashierCommand(Guid CashBoxPublicId, Guid UserPublicId, DateOnly? ValidFrom = null, DateOnly? ValidTo = null)
    : IRequest<Result<IReadOnlyList<CashierDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AssignCashierCommandValidator : AbstractValidator<AssignCashierCommand>
{
    public AssignCashierCommandValidator()
    {
        RuleFor(x => x.CashBoxPublicId).NotEmpty();
        RuleFor(x => x.UserPublicId).NotEmpty().WithMessage("Indique el usuario.");
        RuleFor(x => x).Must(x => x.ValidTo is null || x.ValidFrom is null || x.ValidTo >= x.ValidFrom)
            .WithMessage("El fin de la vigencia no puede ser anterior a su inicio.");
    }
}

public sealed class AssignCashierCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<AssignCashierCommand, Result<IReadOnlyList<CashierDto>>>
{
    public async Task<Result<IReadOnlyList<CashierDto>>> Handle(AssignCashierCommand request, CancellationToken ct)
    {
        var hallada = await AlcanceDePuntos.CajaVisibleAsync(db, alcance, request.CashBoxPublicId, seguir: false, ct);
        if (hallada is null) return Result.Failure<IReadOnlyList<CashierDto>>(ErroresDeRecaudo.NoEncontrado("La caja"));
        var caja = hallada.Value.Caja;
        var usuario = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (usuario is null) return Result.Failure<IReadOnlyList<CashierDto>>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (usuario.PersonId is null) return Result.Failure<IReadOnlyList<CashierDto>>(ErroresDeRecaudo.CajeroSinPersona());

        var desde = request.ValidFrom ?? reloj.HoyLocal;
        var suyas = await db.CashBoxCashiers.AsNoTracking().Where(c => c.CashBoxId == caja.Id && c.UserId == usuario.Id).ToListAsync(ct);
        if (suyas.Any(s => VigenciaDeRecaudo.SeCruzan(s.ValidFrom, s.ValidTo, desde, request.ValidTo)))
            return Result.Failure<IReadOnlyList<CashierDto>>(ErroresDeRecaudo.CajeroSeCruza());

        db.CashBoxCashiers.Add(new CashBoxCashier { CashBoxId = caja.Id, UserId = usuario.Id, ValidFrom = desde, ValidTo = request.ValidTo });
        await db.SaveChangesAsync(ct);
        return Result.Success(await ListCashiersQueryHandler.CajerosDtoAsync(db, caja.Id, ct));
    }
}

/// <summary><c>POST /api/receipts/cash-boxes/{id}/cashiers/{userId}/retire</c>: cierra la vigencia abierta del cajero (por defecto, ayer). (nuevo)</summary>
public sealed record RetireCashierCommand(Guid CashBoxPublicId, Guid UserPublicId, DateOnly? ValidTo = null)
    : IRequest<Result<IReadOnlyList<CashierDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RetireCashierCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<RetireCashierCommand, Result<IReadOnlyList<CashierDto>>>
{
    public async Task<Result<IReadOnlyList<CashierDto>>> Handle(RetireCashierCommand request, CancellationToken ct)
    {
        var hallada = await AlcanceDePuntos.CajaVisibleAsync(db, alcance, request.CashBoxPublicId, seguir: false, ct);
        if (hallada is null) return Result.Failure<IReadOnlyList<CashierDto>>(ErroresDeRecaudo.NoEncontrado("La caja"));
        var caja = hallada.Value.Caja;
        var hoy = reloj.HoyLocal;
        // La consulta sigue la asignación: AsNoTracking en el join apagaría el seguimiento de toda la consulta.
        var asignacion = await (from c in db.CashBoxCashiers
                                join u in db.Users on c.UserId equals u.Id
                                where c.CashBoxId == caja.Id && u.PublicId == request.UserPublicId && (c.ValidTo == null || c.ValidTo >= hoy)
                                orderby c.ValidFrom descending
                                select c).FirstOrDefaultAsync(ct);
        if (asignacion is null) return Result.Failure<IReadOnlyList<CashierDto>>(ErroresDeRecaudo.NoEncontrado("El cajero"));
        var hasta = request.ValidTo ?? hoy.AddDays(-1);
        if (hasta < asignacion.ValidFrom) asignacion.IsDeleted = true;
        else asignacion.ValidTo = hasta;
        await db.SaveChangesAsync(ct);
        return Result.Success(await ListCashiersQueryHandler.CajerosDtoAsync(db, caja.Id, ct));
    }
}

// -------------------------------------------------------------------------------------------------------- medios por punto --

/// <summary>
/// Qué clases de medio admite Recaudo y en qué entrega (feature 015, T0072; FR-023, FR-024; data-model §1.5): las de crédito nunca; en R0 efectivo,
/// transferencia, consignación y cheque (éste sólo con <c>Caja.AdmiteCheque</c>); las demás desde R1. (nuevo)
/// </summary>
public static class MediosDeRecaudo
{
    public static readonly IReadOnlySet<PaymentMeansClass> DeR0 =
        new HashSet<PaymentMeansClass> { PaymentMeansClass.Cash, PaymentMeansClass.Transfer, PaymentMeansClass.BankDeposit, PaymentMeansClass.Check };

    public static bool EsDeCredito(PaymentMeansClass clase) => clase is PaymentMeansClass.AssociateCredit or PaymentMeansClass.CustomerCredit;

    public static bool EsTarjeta(PaymentMeansClass clase) => clase is PaymentMeansClass.CreditCard or PaymentMeansClass.DebitCard;

    public static bool EnLaEntrega(PaymentMeansClass clase, EntregaDeRecaudo entrega) => entrega >= EntregaDeRecaudo.R1 || DeR0.Contains(clase);
}

/// <summary><c>GET /api/receipts/collection-points/{id}/payment-means</c>: los medios ofrecidos en el punto (con <c>AsOf</c>, los vigentes a esa fecha). (nuevo)</summary>
public sealed record ListPointPaymentMeansQuery(Guid CollectionPointPublicId, DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<PointPaymentMeansDto>>>;

public sealed class ListPointPaymentMeansQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListPointPaymentMeansQuery, Result<IReadOnlyList<PointPaymentMeansDto>>>
{
    public async Task<Result<IReadOnlyList<PointPaymentMeansDto>>> Handle(ListPointPaymentMeansQuery request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<IReadOnlyList<PointPaymentMeansDto>>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        return Result.Success(await MediosDelPuntoAsync(db, punto.Id, request.AsOf, ct));
    }

    /// <summary>Los medios del punto; con <paramref name="fecha"/>, sólo los vigentes y activos en el catálogo (un medio inactivo no se ofrece).</summary>
    internal static async Task<IReadOnlyList<PointPaymentMeansDto>> MediosDelPuntoAsync(IApplicationDbContext db, int punto, DateOnly? fecha, CancellationToken ct)
    {
        var filas = await (from o in db.PaymentMeansCollectionPoints.AsNoTracking()
                           join m in db.PaymentMeans.AsNoTracking() on o.PaymentMeansId equals m.Id
                           where o.CollectionPointId == punto
                           where fecha == null || (o.ValidFrom <= fecha && (o.ValidTo == null || o.ValidTo >= fecha) && m.IsActive)
                           orderby m.DisplayOrder, m.Code
                           select new PointPaymentMeansDto(m.PublicId, m.Code, m.Name, m.Class, m.CountMethod, m.ToleranceAmount, m.AllowsChange,
                               m.RequiresReference, o.ValidFrom, o.ValidTo, m.IsActive)).ToListAsync(ct);
        return filas;
    }
}

/// <summary>
/// <c>POST /api/receipts/collection-points/{id}/payment-means</c> (<c>Receipts.Setup.Manage</c>): ofrece un medio del catálogo común en el punto. Rechaza
/// la clase de crédito (<c>CreditClassNotAllowed</c>, US1-1), la tarjeta sin adquirente o datáfono (<c>CardSetupIncomplete</c>, US1-2), el cheque con
/// <c>Caja.AdmiteCheque</c> apagado (<c>CheckDisabled</c>), las clases fuera de la entrega (<c>NotInDelivery</c>) y el medio inactivo. (nuevo)
/// </summary>
public sealed record OfferPaymentMeansCommand(Guid CollectionPointPublicId, Guid PaymentMeansPublicId, DateOnly? ValidFrom = null)
    : IRequest<Result<IReadOnlyList<PointPaymentMeansDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class OfferPaymentMeansCommandValidator : AbstractValidator<OfferPaymentMeansCommand>
{
    public OfferPaymentMeansCommandValidator()
    {
        RuleFor(x => x.CollectionPointPublicId).NotEmpty();
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty().WithMessage("Indique el medio de pago.");
    }
}

public sealed class OfferPaymentMeansCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, ParametrosDeCaja parametros, IDateTimeService reloj)
    : IRequestHandler<OfferPaymentMeansCommand, Result<IReadOnlyList<PointPaymentMeansDto>>>
{
    /// <summary>La entrega de Recaudo con que se evalúa la clase; la vigente del despliegue.</summary>
    public EntregaDeRecaudo Entrega { get; init; } = CatalogoDeRecaudo.EntregaVigente;

    public async Task<Result<IReadOnlyList<PointPaymentMeansDto>>> Handle(OfferPaymentMeansCommand request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<IReadOnlyList<PointPaymentMeansDto>>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        var medio = await db.PaymentMeans.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId, ct);
        if (medio is null) return Result.Failure<IReadOnlyList<PointPaymentMeansDto>>(ErroresDeRecaudo.NoEncontrado("El medio de pago"));

        var desde = request.ValidFrom ?? reloj.HoyLocal;
        var admitido = await AdmitidoAsync(medio, desde, ct);
        if (admitido.IsFailure) return Result.Failure<IReadOnlyList<PointPaymentMeansDto>>(admitido.Error);

        var vigentes = await db.PaymentMeansCollectionPoints.AsNoTracking()
            .Where(o => o.CollectionPointId == punto.Id && o.PaymentMeansId == medio.Id).ToListAsync(ct);
        if (vigentes.Any(o => VigenciaDeRecaudo.SeCruzan(o.ValidFrom, o.ValidTo, desde, null)))
            return Result.Failure<IReadOnlyList<PointPaymentMeansDto>>(ErroresDeRecaudo.MedioYaOfrecido());

        db.PaymentMeansCollectionPoints.Add(new PaymentMeansCollectionPoint { CollectionPointId = punto.Id, PaymentMeansId = medio.Id, ValidFrom = desde });
        await db.SaveChangesAsync(ct);
        return Result.Success(await ListPointPaymentMeansQueryHandler.MediosDelPuntoAsync(db, punto.Id, null, ct));
    }

    private async Task<Result> AdmitidoAsync(PaymentMeans medio, DateOnly fecha, CancellationToken ct)
    {
        if (MediosDeRecaudo.EsDeCredito(medio.Class)) return Result.Failure(ErroresDeRecaudo.MedioDeCredito());
        if (!medio.IsActive) return Result.Failure(ErroresDeRecaudo.MedioInactivo());
        if (MediosDeRecaudo.EsTarjeta(medio.Class))
        {
            if (medio.CardAcquirerId is not { } adquirente) return Result.Failure(ErroresDeRecaudo.TarjetaIncompleta("el adquirente"));
            if (!await db.CardTerminals.AsNoTracking().AnyAsync(t => t.CardAcquirerId == adquirente && t.IsActive, ct))
                return Result.Failure(ErroresDeRecaudo.TarjetaIncompleta("un datáfono del adquirente"));
        }
        if (medio.Class == PaymentMeansClass.Check && !await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaAdmiteCheque, fecha, ct))
            return Result.Failure(ErroresDeRecaudo.ChequeApagado());
        if (!MediosDeRecaudo.EnLaEntrega(medio.Class, Entrega)) return Result.Failure(ErroresDeRecaudo.MedioFueraDeLaEntrega(medio.Class.ToString()));
        return Result.Success();
    }
}

/// <summary><c>POST /api/receipts/collection-points/{id}/payment-means/{meansId}/retire</c>: deja de ofrecerse desde mañana; la fila no se borra. (nuevo)</summary>
public sealed record RetirePaymentMeansCommand(Guid CollectionPointPublicId, Guid PaymentMeansPublicId)
    : IRequest<Result<IReadOnlyList<PointPaymentMeansDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RetirePaymentMeansCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<RetirePaymentMeansCommand, Result<IReadOnlyList<PointPaymentMeansDto>>>
{
    public async Task<Result<IReadOnlyList<PointPaymentMeansDto>>> Handle(RetirePaymentMeansCommand request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<IReadOnlyList<PointPaymentMeansDto>>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        var hoy = reloj.HoyLocal;
        var oferta = await (from o in db.PaymentMeansCollectionPoints
                            join m in db.PaymentMeans on o.PaymentMeansId equals m.Id
                            where o.CollectionPointId == punto.Id && m.PublicId == request.PaymentMeansPublicId && (o.ValidTo == null || o.ValidTo >= hoy)
                            select o).FirstOrDefaultAsync(ct);
        if (oferta is null) return Result.Failure<IReadOnlyList<PointPaymentMeansDto>>(ErroresDeRecaudo.NoEncontrado("El medio del punto"));
        oferta.ValidTo = oferta.ValidFrom > hoy ? oferta.ValidFrom : hoy;
        await db.SaveChangesAsync(ct);
        return Result.Success(await ListPointPaymentMeansQueryHandler.MediosDelPuntoAsync(db, punto.Id, null, ct));
    }
}
