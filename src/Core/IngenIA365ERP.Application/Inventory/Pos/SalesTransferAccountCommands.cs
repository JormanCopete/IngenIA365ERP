using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.PaymentMeans;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pos;

// Feature 022, T040–T041 (FR-046, Q16): las cuentas bancarias propias de recaudo a las que entra una transferencia o una consignación recibida en
// una venta, por punto de venta (y, si se quiere, sólo en una caja) y medio, como las de Recaudo para sus puntos (CuentasDeTransferencia). La
// cuenta es de Tesorería: se lee por ICuentasBancariasPropias sólo al habilitarla y se guarda su rótulo, porque la caja no consulta Tesorería (su
// alcance falla cerrado para un cajero). La venta elige entre las habilitadas (CuentasPropiasDeVenta). (nuevo)

/// <summary>Una cuenta habilitada para un medio en un punto (o una caja). (nuevo)</summary>
public sealed record SalesTransferAccountDto(
    Guid PublicId,
    Guid PointOfSalePublicId,
    string PointOfSaleCode,
    Guid? CashRegisterPublicId,
    string? CashRegisterCode,
    Guid PaymentMeansPublicId,
    string PaymentMeansCode,
    Guid OwnBankAccountPublicId,
    string AccountLabel,
    bool IsActive);

/// <summary>Una cuenta que el cajero puede elegir para un medio en su sesión. (nuevo)</summary>
public sealed record OwnBankAccountOptionDto(Guid PaymentMeansPublicId, Guid OwnBankAccountPublicId, string AccountLabel);

/// <summary>Una cuenta propia de recaudo que se puede habilitar (la lista del administrador). (nuevo)</summary>
public sealed record CollectionBankAccountDto(Guid OwnBankAccountPublicId, string Code, string BankName, string AccountNumberMasked, string Label);

/// <summary>Los errores de la habilitación de cuentas. (nuevo)</summary>
public static class ErroresDeCuentasDeTransferencia
{
    public const string MeansNotTransferCode = "Inventory.TransferAccount.MeansNotTransfer";
    public const string MeansNotOfferedCode = "Inventory.TransferAccount.MeansNotOffered";
    public const string AccountNotForCollectionCode = "Inventory.TransferAccount.AccountNotForCollection";
    public const string AccountNotFoundCode = "Inventory.TransferAccount.AccountNotFound";
    public const string AlreadyEnabledCode = "Inventory.TransferAccount.AlreadyEnabled";
    public const string NotFoundCode = "Inventory.TransferAccount.NotFound";

    public static Error MeansNotTransfer() => new(MeansNotTransferCode, "Sólo los medios de transferencia o de consignación entran a una cuenta bancaria propia.");

    public static Error MeansNotOffered() => new(MeansNotOfferedCode, "El medio no se ofrece en el punto de venta: ofrézcalo primero en su disponibilidad.");

    public static Error AccountNotForCollection() => new(AccountNotForCollectionCode, "La cuenta bancaria propia tiene que estar activa y ser de recaudo.");

    public static Error AccountNotFound() => new(AccountNotFoundCode, "La cuenta bancaria propia no existe o está fuera de su alcance de Tesorería.");

    public static Error AlreadyEnabled() => new(AlreadyEnabledCode, "La cuenta ya está habilitada para ese medio en el punto (y la caja).");

    public static Error NotFound() => new(NotFoundCode, "No existe esa cuenta habilitada.");
}

/// <summary>Arma los DTO de las cuentas habilitadas con lo citado en bloque. (nuevo)</summary>
public static class VistaDeCuentasDeTransferencia
{
    public static async Task<IReadOnlyList<SalesTransferAccountDto>> VariasAsync(IApplicationDbContext db, PointOfSale punto, IReadOnlyList<SalesTransferAccount> filas,
        CancellationToken ct)
    {
        var cajasIds = filas.Select(f => f.CashRegisterId).OfType<int>().Distinct().ToList();
        var mediosIds = filas.Select(f => f.PaymentMeansId).Distinct().ToList();
        var cajas = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => cajasIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.PublicId, c.Code), ct);
        var medios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => mediosIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => (m.PublicId, m.Code), ct);
        return filas.Select(f =>
        {
            (Guid PublicId, string Code)? caja = f.CashRegisterId is int c && cajas.TryGetValue(c, out var x) ? x : null;
            var medio = medios[f.PaymentMeansId];
            return new SalesTransferAccountDto(f.PublicId, punto.PublicId, punto.Code, caja?.PublicId, caja?.Code, medio.PublicId, medio.Code,
                f.OwnBankAccountPublicId, f.AccountLabel, f.IsActive);
        }).ToList();
    }
}

/// <summary><c>GET /api/inventory/points-of-sale/{id}/transfer-accounts</c> (<c>Inventory.PointsOfSale.View</c>): las cuentas habilitadas vivas del punto. (nuevo)</summary>
public sealed record ListSalesTransferAccountsQuery(Guid PointOfSalePublicId) : IRequest<Result<IReadOnlyList<SalesTransferAccountDto>>>;

public sealed class ListSalesTransferAccountsQueryValidator : AbstractValidator<ListSalesTransferAccountsQuery>
{
    public ListSalesTransferAccountsQueryValidator() => RuleFor(x => x.PointOfSalePublicId).NotEmpty();
}

public sealed class ListSalesTransferAccountsQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcance)
    : IRequestHandler<ListSalesTransferAccountsQuery, Result<IReadOnlyList<SalesTransferAccountDto>>>
{
    public async Task<Result<IReadOnlyList<SalesTransferAccountDto>>> Handle(ListSalesTransferAccountsQuery request, CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, request.PointOfSalePublicId, ct);
        if (punto is null) return Result.Failure<IReadOnlyList<SalesTransferAccountDto>>(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        var filas = await db.SalesTransferAccounts.AsNoTracking().Where(t => t.PointOfSaleId == punto.Id && !t.IsDeleted).OrderBy(t => t.AccountLabel).ThenBy(t => t.Id).ToListAsync(ct);
        return Result.Success(await VistaDeCuentasDeTransferencia.VariasAsync(db, punto, filas, ct));
    }
}

/// <summary>
/// <c>GET /api/inventory/points-of-sale/transfer-accounts/candidates</c> (<c>Inventory.PointsOfSale.Manage</c>): las cuentas propias de recaudo
/// activas que quien administra puede habilitar, por el puerto de Tesorería (con su alcance). (nuevo)
/// </summary>
public sealed record ListCollectionBankAccountsQuery : IRequest<Result<IReadOnlyList<CollectionBankAccountDto>>>;

public sealed class ListCollectionBankAccountsQueryValidator : AbstractValidator<ListCollectionBankAccountsQuery>;

public sealed class ListCollectionBankAccountsQueryHandler(ICuentasBancariasPropias cuentas)
    : IRequestHandler<ListCollectionBankAccountsQuery, Result<IReadOnlyList<CollectionBankAccountDto>>>
{
    public async Task<Result<IReadOnlyList<CollectionBankAccountDto>>> Handle(ListCollectionBankAccountsQuery request, CancellationToken ct)
    {
        var lista = await cuentas.DeRecaudoAsync(null, ct);
        return Result.Success<IReadOnlyList<CollectionBankAccountDto>>(lista.Where(c => c.IsActive && c.IsCollection)
            .Select(c => new CollectionBankAccountDto(c.PublicId, c.Code, c.BankName, c.AccountNumberMasked, EnableSalesTransferAccountCommandHandler.Rotulo(c)))
            .ToList());
    }
}

/// <summary>
/// <c>GET /api/inventory/cash-sessions/{id}/transfer-accounts</c> (<c>Inventory.Sales.Create</c> o <c>Inventory.Pos.Sell</c>): las cuentas que el
/// cajero puede elegir en su sesión, por medio (las del punto para todas las cajas y las de su caja). La usa la factura de oficina, que arma sus
/// medios en el cliente; el POS las recibe con cada medio de la venta. (nuevo)
/// </summary>
public sealed record GetCashSessionTransferAccountsQuery(Guid CashSessionPublicId) : IRequest<Result<IReadOnlyList<OwnBankAccountOptionDto>>>;

public sealed class GetCashSessionTransferAccountsQueryValidator : AbstractValidator<GetCashSessionTransferAccountsQuery>
{
    public GetCashSessionTransferAccountsQueryValidator() => RuleFor(x => x.CashSessionPublicId).NotEmpty();
}

public sealed class GetCashSessionTransferAccountsQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcance)
    : IRequestHandler<GetCashSessionTransferAccountsQuery, Result<IReadOnlyList<OwnBankAccountOptionDto>>>
{
    public async Task<Result<IReadOnlyList<OwnBankAccountOptionDto>>> Handle(GetCashSessionTransferAccountsQuery request, CancellationToken ct)
    {
        var sesion = await db.CashSessions.AsNoTracking().Where(s => s.PublicId == request.CashSessionPublicId)
            .Select(s => new { s.PointOfSaleId, s.CashRegisterId }).FirstOrDefaultAsync(ct);
        if (sesion is null || !(await alcance.ObtenerAsync(ct)).IncluyePunto(sesion.PointOfSaleId))
            return Result.Failure<IReadOnlyList<OwnBankAccountOptionDto>>(ErroresDelPos.CashSessionNotOpen());
        var opciones = await CuentasPropiasDeVenta.OpcionesAsync(db, sesion.PointOfSaleId, sesion.CashRegisterId, ct);
        return Result.Success<IReadOnlyList<OwnBankAccountOptionDto>>(
            opciones.Select(o => new OwnBankAccountOptionDto(o.PaymentMeansPublicId, o.OwnBankAccountPublicId, o.AccountLabel)).ToList());
    }
}

/// <summary>
/// <c>POST /api/inventory/points-of-sale/{id}/transfer-accounts</c> (<c>Inventory.PointsOfSale.Manage</c>, con clave): habilita una cuenta propia de
/// recaudo, activa, para un medio de transferencia o consignación ofrecido en el punto (y, si se da, sólo en una de sus cajas). (nuevo)
/// </summary>
public sealed record EnableSalesTransferAccountCommand(Guid PointOfSalePublicId, Guid PaymentMeansPublicId, Guid OwnBankAccountPublicId, Guid? CashRegisterPublicId = null)
    : IRequest<Result<SalesTransferAccountDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class EnableSalesTransferAccountCommandValidator : AbstractValidator<EnableSalesTransferAccountCommand>
{
    public EnableSalesTransferAccountCommandValidator()
    {
        RuleFor(x => x.PointOfSalePublicId).NotEmpty();
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty().WithMessage("Indique el medio de pago.");
        RuleFor(x => x.OwnBankAccountPublicId).NotEmpty().WithMessage("Indique la cuenta bancaria propia.");
        RuleFor(x => x.CashRegisterPublicId).Must(c => c is null || c != Guid.Empty).WithMessage("La caja no es válida.");
    }
}

public sealed class EnableSalesTransferAccountCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, ICuentasBancariasPropias cuentas)
    : IRequestHandler<EnableSalesTransferAccountCommand, Result<SalesTransferAccountDto>>
{
    public async Task<Result<SalesTransferAccountDto>> Handle(EnableSalesTransferAccountCommand request, CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, request.PointOfSalePublicId, ct);
        if (punto is null) return Result.Failure<SalesTransferAccountDto>(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        int? caja = null;
        if (request.CashRegisterPublicId is { } c)
        {
            caja = await db.CashRegisters.AsNoTracking().Where(x => x.PublicId == c && x.PointOfSaleId == punto.Id && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (caja is null) return Result.Failure<SalesTransferAccountDto>(ErroresDePuntoDeVenta.CashRegisterNotFound());
        }
        var medio = await db.PaymentMeans.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId && !m.IsDeleted, ct);
        if (medio is null) return Result.Failure<SalesTransferAccountDto>(PaymentMeansErrors.NotFound());
        if (!CuentasPropiasDeVenta.EntraAUnaCuenta(medio.Class)) return Result.Failure<SalesTransferAccountDto>(ErroresDeCuentasDeTransferencia.MeansNotTransfer());
        var ofrecido = medio.OfferedAtAllPointsOfSale
            || await db.PaymentMeansPointsOfSale.AsNoTracking().AnyAsync(o => o.PointOfSaleId == punto.Id && o.PaymentMeansId == medio.Id, ct);
        if (!ofrecido) return Result.Failure<SalesTransferAccountDto>(ErroresDeCuentasDeTransferencia.MeansNotOffered());
        var cuenta = await cuentas.PorPublicIdAsync(request.OwnBankAccountPublicId, ct);
        if (cuenta is null) return Result.Failure<SalesTransferAccountDto>(ErroresDeCuentasDeTransferencia.AccountNotFound());
        if (!cuenta.IsActive || !cuenta.IsCollection) return Result.Failure<SalesTransferAccountDto>(ErroresDeCuentasDeTransferencia.AccountNotForCollection());
        if (await db.SalesTransferAccounts.AsNoTracking().AnyAsync(t => !t.IsDeleted && t.PointOfSaleId == punto.Id && t.CashRegisterId == caja && t.PaymentMeansId == medio.Id
                                                                       && t.OwnBankAccountPublicId == cuenta.PublicId, ct))
            return Result.Failure<SalesTransferAccountDto>(ErroresDeCuentasDeTransferencia.AlreadyEnabled());

        var fila = new SalesTransferAccount
        {
            PointOfSaleId = punto.Id, CashRegisterId = caja, PaymentMeansId = medio.Id, OwnBankAccountPublicId = cuenta.PublicId, AccountLabel = Rotulo(cuenta),
        };
        db.SalesTransferAccounts.Add(fila);
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDeCuentasDeTransferencia.VariasAsync(db, punto, [fila], ct))[0]);
    }

    /// <summary>Lo que el cajero ve de la cuenta: código, banco y los cuatro últimos dígitos (150 a lo sumo).</summary>
    public static string Rotulo(CuentaBancariaPropiaDto c)
    {
        var texto = $"{c.Code} · {c.BankName} {c.AccountNumberMasked}".Trim();
        return texto.Length <= 150 ? texto : texto[..150];
    }
}

/// <summary>
/// <c>POST /api/inventory/points-of-sale/{id}/transfer-accounts/{accountId}/disable</c> (<c>Inventory.PointsOfSale.Manage</c>, con clave): deja de
/// ofrecer la cuenta (baja lógica). Lo ya cobrado conserva su cuenta. (nuevo)
/// </summary>
public sealed record DisableSalesTransferAccountCommand(Guid PointOfSalePublicId, Guid TransferAccountPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DisableSalesTransferAccountCommandValidator : AbstractValidator<DisableSalesTransferAccountCommand>
{
    public DisableSalesTransferAccountCommandValidator()
    {
        RuleFor(x => x.PointOfSalePublicId).NotEmpty();
        RuleFor(x => x.TransferAccountPublicId).NotEmpty();
    }
}

public sealed class DisableSalesTransferAccountCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<DisableSalesTransferAccountCommand, Result>
{
    public async Task<Result> Handle(DisableSalesTransferAccountCommand request, CancellationToken ct)
    {
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcance, request.PointOfSalePublicId, ct);
        if (punto is null) return Result.Failure(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        var fila = await db.SalesTransferAccounts.FirstOrDefaultAsync(t => t.PublicId == request.TransferAccountPublicId && t.PointOfSaleId == punto.Id && !t.IsDeleted, ct);
        if (fila is null) return Result.Failure(ErroresDeCuentasDeTransferencia.NotFound());
        var actor = await actorActual.ObtenerAsync(ct);
        fila.IsActive = false;
        fila.IsDeleted = true;
        fila.DeletedAt = reloj.UtcNow;
        fila.DeletedBy = actor.Name;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
