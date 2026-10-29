using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Setup;

// Feature 015, segunda ronda del 2026-10-27 (D-f; FR-231): las cuentas bancarias propias a las que entra una transferencia o una consignación recibida en la caja. Se
// habilitan por punto (y, si se quiere, por caja) y medio; el cajero elige entre las habilitadas para el medio en su caja y, con una sola, se propone. Sin cuenta, el
// rol BancoRecaudo del ReciboEmitido no se podía resolver (Accounting.OwnBankAccount.NotLinked). La cuenta es de Tesorería: se lee por ICuentasBancariasPropias al
// habilitarla y se guarda su rótulo, porque la caja no lee cuentas de Tesorería.

/// <summary>Una cuenta habilitada para un medio en un punto (o una caja). (nuevo)</summary>
public sealed record TransferAccountDto(Guid PublicId, Guid CollectionPointPublicId, string CollectionPointCode, Guid? CashBoxPublicId, string? CashBoxCode,
    Guid PaymentMeansPublicId, string PaymentMeansCode, Guid OwnBankAccountPublicId, string AccountLabel);

/// <summary>Una cuenta que el cajero puede elegir para un medio de su sesión. (nuevo)</summary>
public sealed record OpcionDeCuentaDto(Guid PaymentMeansPublicId, Guid OwnBankAccountPublicId, string AccountLabel);

/// <summary>Lo común: las clases que entran a una cuenta propia, las opciones de una sesión y la resolución de los medios. (nuevo)</summary>
public static class CuentasDeTransferencia
{
    /// <summary>Las clases cuyo rol es <c>BancoRecaudo</c> (contabilidad.md §5): transferencia y consignación.</summary>
    public static bool EntraAUnaCuenta(PaymentMeansClass clase) => clase is PaymentMeansClass.Transfer or PaymentMeansClass.BankDeposit;

    public const string CodigoRequerida = "Receipts.Receipt.OwnBankAccountRequired";
    public const string CodigoNoHabilitada = "Receipts.Receipt.OwnBankAccountNotEnabled";

    public static Error Requerida(string medio, IReadOnlyList<OpcionDeCuentaDto> opciones) => new ErrorConDatos(CodigoRequerida,
        opciones.Count == 0
            ? $"El medio {medio} no tiene una cuenta bancaria propia habilitada en esta caja: pídale al administrador de Recaudo que la habilite."
            : $"Elija la cuenta bancaria propia a la que entró el pago con {medio}.",
        new { means = medio, options = opciones });

    public static Error NoHabilitada(string medio) => new(CodigoNoHabilitada, $"La cuenta elegida no está habilitada para {medio} en esta caja.");

    /// <summary>Las cuentas habilitadas en la caja de la sesión, por medio.</summary>
    public static async Task<IReadOnlyList<OpcionDeCuentaDto>> OpcionesAsync(IApplicationDbContext db, CashSession sesion, CancellationToken ct)
    {
        var (_, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        return await (from t in db.TransferAccounts.AsNoTracking()
                      join m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on t.PaymentMeansId equals m.Id
                      where t.CollectionPointId == punto && (t.CashBoxId == null || t.CashBoxId == sesion.CashBoxId)
                      orderby t.AccountLabel
                      select new OpcionDeCuentaDto(m.PublicId, t.OwnBankAccountPublicId, t.AccountLabel)).Distinct().ToListAsync(ct);
    }

    /// <summary>
    /// Pone en cada medio de transferencia o consignación su cuenta: la elegida, si está habilitada; si no la trae y hay una sola, la propone; si hay varias o ninguna,
    /// la pide (<c>OwnBankAccountRequired</c> con las opciones). Los demás medios no llevan cuenta.
    /// </summary>
    public static async Task<Result<List<MedioDeCajaDto>>> ResolverAsync(IApplicationDbContext db, CashSession sesion, ValoresSelladosDeLaSesion sellados,
        IReadOnlyList<MedioDeCajaDto> medios, CancellationToken ct)
    {
        IReadOnlyList<OpcionDeCuentaDto>? opciones = null;
        var resueltos = new List<MedioDeCajaDto>(medios.Count);
        foreach (var m in medios)
        {
            var sellado = sellados.Means.FirstOrDefault(x => x.PublicId == m.PaymentMeansPublicId);
            if (sellado is null || !EntraAUnaCuenta(sellado.Class))
            {
                resueltos.Add(m with { OwnBankAccountPublicId = null });
                continue;
            }
            opciones ??= await OpcionesAsync(db, sesion, ct);
            var delMedio = opciones.Where(o => o.PaymentMeansPublicId == m.PaymentMeansPublicId).ToList();
            if (m.OwnBankAccountPublicId is { } elegida)
            {
                if (!delMedio.Any(o => o.OwnBankAccountPublicId == elegida)) return Result.Failure<List<MedioDeCajaDto>>(NoHabilitada(sellado.Code));
                resueltos.Add(m);
            }
            else if (delMedio.Count == 1) resueltos.Add(m with { OwnBankAccountPublicId = delMedio[0].OwnBankAccountPublicId });
            else return Result.Failure<List<MedioDeCajaDto>>(Requerida(sellado.Code, delMedio));
        }
        return Result.Success(resueltos);
    }

    internal static async Task<TransferAccountDto> DtoAsync(IApplicationDbContext db, TransferAccount t, CancellationToken ct)
    {
        var punto = await db.CollectionPoints.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == t.CollectionPointId).Select(p => new { p.PublicId, p.Code }).FirstAsync(ct);
        var caja = t.CashBoxId is { } c
            ? await db.CashBoxes.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == c).Select(x => new { x.PublicId, x.Code }).FirstAsync(ct)
            : null;
        var medio = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => m.Id == t.PaymentMeansId).Select(m => new { m.PublicId, m.Code }).FirstAsync(ct);
        return new TransferAccountDto(t.PublicId, punto.PublicId, punto.Code, caja?.PublicId, caja?.Code, medio.PublicId, medio.Code, t.OwnBankAccountPublicId, t.AccountLabel);
    }
}

/// <summary><c>GET /api/receipts/transfer-accounts?collectionPoint=</c> (<c>Receipts.Setup.View</c>): las cuentas habilitadas vivas. (nuevo)</summary>
public sealed record ListTransferAccountsQuery(Guid? CollectionPointPublicId = null) : IRequest<Result<IReadOnlyList<TransferAccountDto>>>;

public sealed class ListTransferAccountsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListTransferAccountsQuery, Result<IReadOnlyList<TransferAccountDto>>>
{
    public async Task<Result<IReadOnlyList<TransferAccountDto>>> Handle(ListTransferAccountsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.TransferAccounts.AsNoTracking();
        if (request.CollectionPointPublicId is { } p)
        {
            var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, p, seguir: false, ct);
            if (punto is null) return Result.Failure<IReadOnlyList<TransferAccountDto>>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
            consulta = consulta.Where(t => t.CollectionPointId == punto.Id);
        }
        var filas = await consulta.OrderBy(t => t.Id).ToListAsync(ct);
        var puntos = await db.CollectionPoints.AsNoTracking().Where(x => filas.Select(f => f.CollectionPointId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var lista = new List<TransferAccountDto>();
        foreach (var f in filas.Where(f => puntos.TryGetValue(f.CollectionPointId, out var pt) && AlcanceDePuntos.VePunto(mio, pt)))
            lista.Add(await CuentasDeTransferencia.DtoAsync(db, f, ct));
        return Result.Success<IReadOnlyList<TransferAccountDto>>(lista);
    }
}

/// <summary><c>GET /api/receipts/sessions/{id}/transfer-accounts</c> (<c>Receipts.Receipt.Register</c>): lo que el cajero elige en su sesión, por medio. (nuevo)</summary>
public sealed record GetSessionTransferAccountsQuery(Guid SessionPublicId) : IRequest<Result<IReadOnlyList<OpcionDeCuentaDto>>>;

public sealed class GetSessionTransferAccountsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<GetSessionTransferAccountsQuery, Result<IReadOnlyList<OpcionDeCuentaDto>>>
{
    public async Task<Result<IReadOnlyList<OpcionDeCuentaDto>>> Handle(GetSessionTransferAccountsQuery request, CancellationToken ct)
    {
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<IReadOnlyList<OpcionDeCuentaDto>>(ErroresDeRecaudo.SesionRequerida());
        return Result.Success(await CuentasDeTransferencia.OpcionesAsync(db, sesion, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/transfer-accounts</c> (<c>Receipts.Setup.Manage</c>, con clave): habilita una cuenta propia de recaudo, activa, para un medio de transferencia o
/// consignación ofrecido en el punto (y, si se da, sólo en una de sus cajas). Auditado. (nuevo)
/// </summary>
public sealed record EnableTransferAccountCommand(Guid CollectionPointPublicId, Guid PaymentMeansPublicId, Guid OwnBankAccountPublicId, Guid? CashBoxPublicId = null)
    : IRequest<Result<TransferAccountDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class EnableTransferAccountCommandValidator : AbstractValidator<EnableTransferAccountCommand>
{
    public EnableTransferAccountCommandValidator()
    {
        RuleFor(x => x.CollectionPointPublicId).NotEmpty();
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty().WithMessage("Indique el medio de pago.");
        RuleFor(x => x.OwnBankAccountPublicId).NotEmpty().WithMessage("Indique la cuenta bancaria propia.");
    }
}

public sealed class EnableTransferAccountCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, ICuentasBancariasPropias cuentas, IActorActual actorActual,
    AuditoriaDeRecaudo auditoria) : IRequestHandler<EnableTransferAccountCommand, Result<TransferAccountDto>>
{
    public async Task<Result<TransferAccountDto>> Handle(EnableTransferAccountCommand request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<TransferAccountDto>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        int? caja = null;
        if (request.CashBoxPublicId is { } c)
        {
            caja = await db.CashBoxes.AsNoTracking().Where(x => x.PublicId == c && x.CollectionPointId == punto.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (caja is null) return Result.Failure<TransferAccountDto>(ErroresDeRecaudo.NoEncontrado("La caja del punto"));
        }
        var medio = await db.PaymentMeans.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId, ct);
        if (medio is null) return Result.Failure<TransferAccountDto>(ErroresDeRecaudo.NoEncontrado("El medio de pago"));
        if (!CuentasDeTransferencia.EntraAUnaCuenta(medio.Class))
            return Result.Failure<TransferAccountDto>(new Error("Receipts.TransferAccount.MeansNotTransfer",
                "Sólo los medios de transferencia o de consignación entran a una cuenta bancaria propia."));
        if (!await db.PaymentMeansCollectionPoints.AsNoTracking().AnyAsync(o => o.CollectionPointId == punto.Id && o.PaymentMeansId == medio.Id, ct))
            return Result.Failure<TransferAccountDto>(new Error("Receipts.TransferAccount.MeansNotOffered", "El medio no se ofrece en el punto: ofrézcalo primero."));
        var cuenta = await cuentas.PorPublicIdAsync(request.OwnBankAccountPublicId, ct);
        if (cuenta is null) return Result.Failure<TransferAccountDto>(ErroresDeRecaudo.NoEncontrado("La cuenta bancaria propia"));
        if (!cuenta.IsActive || !cuenta.IsCollection)
            return Result.Failure<TransferAccountDto>(new Error("Receipts.TransferAccount.AccountNotForCollection",
                "La cuenta bancaria propia tiene que estar activa y ser de recaudo."));
        if (await db.TransferAccounts.AsNoTracking().AnyAsync(t => t.CollectionPointId == punto.Id && t.CashBoxId == caja && t.PaymentMeansId == medio.Id
                                                                  && t.OwnBankAccountPublicId == cuenta.PublicId, ct))
            return Result.Failure<TransferAccountDto>(new Error("Receipts.TransferAccount.AlreadyEnabled", "La cuenta ya está habilitada para ese medio."));

        var fila = new TransferAccount
        {
            CollectionPointId = punto.Id, CashBoxId = caja, PaymentMeansId = medio.Id, OwnBankAccountPublicId = cuenta.PublicId,
            AccountLabel = Rotulo($"{cuenta.Code} · {cuenta.BankName} {cuenta.AccountNumberMasked}"),
        };
        db.TransferAccounts.Add(fila);
        var actor = await actorActual.ObtenerAsync(ct);
        await auditoria.AgregarAsync("Receipts.TransferAccount.Enabled", "TransferAccount", fila.PublicId, null,
            new { point = punto.Code, cashBox = request.CashBoxPublicId, means = medio.Code, account = cuenta.Code, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await CuentasDeTransferencia.DtoAsync(db, fila, ct));
    }

    private static string Rotulo(string texto) => texto.Length <= 150 ? texto : texto[..150];
}

/// <summary><c>POST /api/receipts/transfer-accounts/{id}/disable</c> (<c>Receipts.Setup.Manage</c>, con clave): deja de ofrecer la cuenta (baja lógica, auditada). (nuevo)</summary>
public sealed record DisableTransferAccountCommand(Guid PublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DisableTransferAccountCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IActorActual actorActual, AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<DisableTransferAccountCommand, Result>
{
    public async Task<Result> Handle(DisableTransferAccountCommand request, CancellationToken ct)
    {
        var fila = await db.TransferAccounts.FirstOrDefaultAsync(t => t.PublicId == request.PublicId, ct);
        if (fila is null) return Result.Failure(ErroresDeRecaudo.NoEncontrado("La cuenta habilitada"));
        var mio = await alcance.ObtenerAsync(ct);
        var punto = await db.CollectionPoints.AsNoTracking().FirstAsync(p => p.Id == fila.CollectionPointId, ct);
        if (!AlcanceDePuntos.VePunto(mio, punto)) return Result.Failure(ErroresDeRecaudo.NoEncontrado("La cuenta habilitada"));
        var actor = await actorActual.ObtenerAsync(ct);
        fila.IsDeleted = true;
        fila.DeletedAt = reloj.UtcNow;
        fila.DeletedBy = actor.Name;
        await auditoria.AgregarAsync("Receipts.TransferAccount.Disabled", "TransferAccount", fila.PublicId, new { account = fila.AccountLabel }, null, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
