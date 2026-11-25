using System.Security.Cryptography;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Sessions;

// 2026-11-23 (pedido del dueño), caja única (Caja.SesionUnica): quien vende y recauda abre y cierra una sola caja. La sesión de Recaudo es la dueña del
// dinero; la del punto de venta de su caja (RCD_CashBoxes.PosCashRegisterPublicId) abre con ella, con base cero, y al arquear se cierra cuadrada —su
// efectivo a la caja fuerte— y lo que recibió entra a la sesión de Recaudo (CashMovementKindR.FromPointOfSale) para contarlo todo junto. Cada paso es un
// comando con su propia transacción y su propia clave derivada: el coordinador no abre ninguna (TransaccionExplicita: nadie envía por ISender dentro
// de la suya) y repetir un paso es inocuo. (nuevo)

/// <summary>La caja del punto de venta de la caja única, para la pantalla de la sesión. Lo esperado sale nulo con arqueo ciego. (nuevo)</summary>
public sealed record CajaUnicaDto(bool Enabled, Guid? PosCashRegisterPublicId, Guid? PosSessionPublicId, string? CashRegisterCode, string? PointOfSaleName,
    bool PosSessionOpen, IReadOnlyList<CajaUnicaMedioDto> Means);

public sealed record CajaUnicaMedioDto(Guid PaymentMeansPublicId, string Code, decimal? Expected);

/// <summary>
/// Lo que recibió la caja vinculada del punto de venta entra a la sesión de Recaudo (<c>POST</c> interno del coordinador; sin ruta): un movimiento
/// <see cref="CashMovementKindR.FromPointOfSale"/> por medio sellado en la sesión, una sola vez por sesión. El efectivo emite <c>Caja</c> ←
/// <c>PuenteDelPuntoDeVenta</c>; los demás medios sólo cuentan en el arqueo. (nuevo)
/// </summary>
public sealed record RegisterPointOfSaleIntakeCommand(Guid SessionPublicId, Guid PosSessionPublicId, IReadOnlyList<LoDelPuntoDeVentaDto> Means)
    : IRequest<Result>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterPointOfSaleIntakeCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    EmisionDeRecaudo emision) : IRequestHandler<RegisterPointOfSaleIntakeCommand, Result>
{
    public async Task<Result> Handle(RegisterPointOfSaleIntakeCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: true, ct);
        if (sesion is null) return Result.Failure(ErroresDeRecaudo.NoEncontrado("La sesión"));
        if (sesion.Status is not (CashSessionState.Open or CashSessionState.ForgottenOpen)) return Result.Failure(ErroresDeRecaudo.SesionNoAbierta());

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);
        if (await db.CashMovements.AsNoTracking().AnyAsync(m => m.CashSessionId == sesion.Id && m.Kind == CashMovementKindR.FromPointOfSale, ct))
            return Result.Success();

        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var hoy = reloj.HoyLocal;
        var (_, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, punto, ct);
        foreach (var m in request.Means.Where(m => m.Expected > 0m))
        {
            var medio = sellados.Means.FirstOrDefault(x => x.PaymentMeansId == m.PaymentMeansId);
            if (medio is null) continue; // un medio que Recaudo no recibe se quedó arqueado y cuadrado en el punto de venta
            var movimiento = new CashMovement
            {
                DocumentTypeId = tipo.Id,
                Kind = CashMovementKindR.FromPointOfSale,
                CashSessionId = sesion.Id,
                FromMeansId = m.PaymentMeansId,
                Amount = m.Expected,
                Reason = $"Recibido en la caja del punto de venta (sesión {request.PosSessionPublicId:N})",
                MovedAt = reloj.UtcNow,
                RegisteredByUserId = usuario,
            };
            await numerador.NumerarAsync(movimiento, tipo.Code, prefijo, ct);
            db.CashMovements.Add(movimiento);
            await db.SaveChangesAsync(ct);
            if (medio.Class == PaymentMeansClass.Cash) await emision.MovimientoDeCajaAsync(movimiento, sesion, null, null, hoy, tipo.Code, ct);
        }
        await db.SaveChangesAsync(ct);
        await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// <c>PUT /api/receipts/cash-boxes/{id}/point-of-sale-register</c> (configuración de Recaudo): la caja del punto de venta que abre y cierra con esta caja de
/// Recaudo en la caja única, o ninguna. Una caja del punto de venta va con una sola caja de Recaudo. (nuevo)
/// </summary>
public sealed record LinkPointOfSaleRegisterCommand(Guid CashBoxPublicId, Guid? PosCashRegisterPublicId)
    : IRequest<Result<CashBoxDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class LinkPointOfSaleRegisterCommandValidator : AbstractValidator<LinkPointOfSaleRegisterCommand>
{
    public LinkPointOfSaleRegisterCommandValidator() => RuleFor(x => x.CashBoxPublicId).NotEmpty();
}

public sealed class LinkPointOfSaleRegisterCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<LinkPointOfSaleRegisterCommand, Result<CashBoxDto>>
{
    public async Task<Result<CashBoxDto>> Handle(LinkPointOfSaleRegisterCommand request, CancellationToken ct)
    {
        var hallada = await AlcanceDePuntos.CajaVisibleAsync(db, alcance, request.CashBoxPublicId, seguir: true, ct);
        if (hallada is null) return Result.Failure<CashBoxDto>(ErroresDeRecaudo.NoEncontrado("La caja"));
        var (caja, punto) = hallada.Value;
        if (request.PosCashRegisterPublicId is { } pos)
        {
            if (!await db.CashRegisters.AsNoTracking().AnyAsync(r => r.PublicId == pos && r.IsActive, ct))
                return Result.Failure<CashBoxDto>(ErroresDeRecaudo.NoEncontrado("La caja del punto de venta"));
            var otra = await db.CashBoxes.AsNoTracking().Where(c => c.Id != caja.Id && c.PosCashRegisterPublicId == pos).Select(c => c.Code).FirstOrDefaultAsync(ct);
            if (otra is not null)
                return Result.Failure<CashBoxDto>(ErroresDeRecaudo.MovimientoInvalido($"Esa caja del punto de venta ya está unida a la caja de Recaudo {otra}."));
        }
        caja.PosCashRegisterPublicId = request.PosCashRegisterPublicId;
        await db.SaveChangesAsync(ct);
        return Result.Success(SetupMapeos.Caja(caja, punto, await SetupMapeos.CajerosVigentesAsync(db, caja.Id, reloj.HoyLocal, ct)));
    }
}

/// <summary>
/// El coordinador de la caja única: abre, informa y prepara el cierre de la caja del punto de venta vinculada. Sin transacción propia. (nuevo)
/// 2026-11-25 (revisión de la caja única, pedido del dueño: «simple y seguro»):
/// <list type="bullet">
/// <item>el cierre se <b>comprueba antes</b> de tocar la caja del punto de venta (<see cref="CheckClosingCountQuery"/> con lo que ésta recibió): un arqueo
/// que se va a rechazar ya no la deja cerrada con Recaudo abierta;</item>
/// <item>lo que entra a Recaudo es lo que <b>contó</b> el cierre cuadrado (no el esperado en vivo, que tras el retiro a la caja fuerte deja el efectivo en
/// cero), y si ya entró no se vuelve a pedir: repetir el arqueo, con o sin otro cuerpo, ya no choca con su clave;</item>
/// <item>cerrada la caja del punto de venta con el arqueo, no se abre otra en la misma sesión (lo de esa segunda nunca entraba);</item>
/// <item>un medio que en el punto de venta quedó en negativo (salió más de lo que entró) se rechaza antes de cerrar, con qué hacer;</item>
/// <item>la sesión de Recaudo tiene que estar al alcance de quien pregunta, y la caja del punto de venta la abre su cajero.</item>
/// </list>
/// </summary>
public sealed class CajaUnica(IApplicationDbContext db, ISender sender, ParametrosDeCaja parametros, IDateTimeService reloj, IAlcanceDeRecaudo alcance,
    IActorActual actorActual, SegregacionDeRecaudo segregacion)
{
    private static readonly IReadOnlyDictionary<int, decimal> Nada = new Dictionary<int, decimal>();

    /// <summary>Si la caja única rige y qué caja del punto de venta va con la caja de la sesión (al alcance de quien pregunta).</summary>
    private async Task<(bool Rige, Guid? Caja, CashSession? Sesion)> LeerAsync(Guid sesionDeRecaudo, CancellationToken ct)
    {
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, sesionDeRecaudo, seguir: false, ct);
        if (sesion is null) return (false, null, null);
        var caja = await db.CashBoxes.AsNoTracking().Where(c => c.Id == sesion.CashBoxId).Select(c => c.PosCashRegisterPublicId).FirstOrDefaultAsync(ct);
        var rige = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaSesionUnica, reloj.HoyLocal, ct);
        return (rige, caja, sesion);
    }

    /// <summary>¿Ya entró a la sesión lo de la caja del punto de venta?</summary>
    private Task<bool> YaEntroAsync(Guid sesionDeRecaudo, CancellationToken ct) =>
        (from m in db.CashMovements.AsNoTracking()
         join s in db.ReceiptsCashSessions.AsNoTracking() on m.CashSessionId equals s.Id
         where s.PublicId == sesionDeRecaudo && m.Kind == CashMovementKindR.FromPointOfSale
         select m.Id).AnyAsync(ct);

    /// <summary>
    /// Abre la caja del punto de venta vinculada si la caja única rige, la caja tiene una y no hay una abierta ya. La abre el cajero de la sesión; si la de
    /// esta sesión ya se cerró con el arqueo, no abre otra (<c>Receipts.SingleSession.PointOfSaleAlreadyClosed</c>). El error, tal cual.
    /// </summary>
    public async Task<Result> AbrirAsync(Guid sesionDeRecaudo, Guid clave, CancellationToken ct)
    {
        var (rige, caja, sesion) = await LeerAsync(sesionDeRecaudo, ct);
        if (sesion is null) return Result.Failure(ErroresDeRecaudo.NoEncontrado("La sesión"));
        if (!rige || caja is not { } registro || sesion.Status != CashSessionState.Open) return Result.Success();
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario || !await segregacion.MismaPersonaAsync(usuario, sesion.CashierUserId, ct))
            return Result.Failure(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var vinculada = await sender.Send(new GetLinkedCashSessionQuery(sesionDeRecaudo), ct);
        if (vinculada.IsSuccess && vinculada.Value is { Open: true }) return Result.Success();
        if ((vinculada.IsSuccess && vinculada.Value is { Open: false }) || await YaEntroAsync(sesionDeRecaudo, ct))
            return Result.Failure(ErroresDeRecaudo.PuntoDeVentaYaCerrado());
        var abierta = await sender.Send(new Inventory.Cash.OpenCashSessionCommand(registro)
        {
            OperationKey = Derivada(clave, "abrir-punto-de-venta"),
            LinkedSessionPublicId = sesionDeRecaudo,
        }, ct);
        return abierta.IsSuccess ? Result.Success() : Result.Failure(abierta.Error);
    }

    /// <summary>
    /// Lo que la caja vinculada recibió y todavía no entró a la sesión, por <c>PaymentMeansId</c> (sólo los medios que se arquean y en positivo): abierta, su
    /// esperado en vivo; cerrada, lo que contó su cierre. Vacío si no hay caja vinculada o si ya entró.
    /// </summary>
    public async Task<(SesionVinculadaDto? Vinculada, bool YaEntro, IReadOnlyDictionary<int, decimal> Pendiente)> PendienteAsync(Guid sesionDeRecaudo,
        CancellationToken ct)
    {
        var leida = await sender.Send(new GetLinkedCashSessionQuery(sesionDeRecaudo), ct);
        if (leida.IsFailure || leida.Value is not { } vinculada) return (null, false, Nada);
        if (await YaEntroAsync(sesionDeRecaudo, ct)) return (vinculada, true, Nada);
        return (vinculada, false, Pendiente(vinculada));
    }

    private static IReadOnlyDictionary<int, decimal> Pendiente(SesionVinculadaDto vinculada) =>
        vinculada.Means.Where(m => m.CountMethod != CashCountMethod.None && m.Expected > 0m)
            .GroupBy(m => m.PaymentMeansId).ToDictionary(g => g.Key, g => g.Sum(m => m.Expected));

    /// <summary>
    /// Antes del arqueo que cierra (o del de la olvidada), en este orden: si lo del punto de venta ya entró, nada más; un medio en negativo, rechazado;
    /// <paramref name="comprobar"/> (el arqueo de Recaudo con lo pendiente, sin escribir); cerrar cuadrada la caja vinculada si sigue abierta, y entrar lo
    /// que contó. Sin caja vinculada no hace nada. Cada escritura con su clave derivada: repetir es inocuo.
    /// </summary>
    public async Task<Result> PrepararCierreAsync(Guid sesionDeRecaudo, Guid clave, Func<IReadOnlyDictionary<int, decimal>, Task<Result>> comprobar,
        CancellationToken ct)
    {
        var (vinculada, yaEntro, pendiente) = await PendienteAsync(sesionDeRecaudo, ct);
        if (vinculada is null || yaEntro) return Result.Success();
        if (vinculada.Open && vinculada.Means.FirstOrDefault(m => m.CountMethod != CashCountMethod.None && m.Expected < 0m) is { } negativo)
            return Result.Failure(ErroresDeRecaudo.PuntoDeVentaEnNegativo(negativo.Code, negativo.Expected));

        var comprobado = await comprobar(pendiente);
        if (comprobado.IsFailure) return comprobado;

        if (vinculada.Open)
        {
            var cerrada = await sender.Send(CajaVinculada.Cierre(vinculada, Derivada(clave, "cerrar-punto-de-venta")), ct);
            if (cerrada.IsFailure) return Result.Failure(cerrada.Error);
            var releida = await sender.Send(new GetLinkedCashSessionQuery(sesionDeRecaudo), ct);
            if (releida.IsFailure) return Result.Failure(releida.Error);
            vinculada = releida.Value ?? vinculada;
        }
        return await sender.Send(new RegisterPointOfSaleIntakeCommand(sesionDeRecaudo, vinculada.PublicId,
            vinculada.Means.Where(m => m.CountMethod != CashCountMethod.None).ToList())
        {
            OperationKey = Derivada(clave, "entrar-punto-de-venta"),
        }, ct);
    }

    /// <summary>
    /// Para la pantalla: si rige, la caja del punto de venta y lo que recibió y todavía no entró a Recaudo (lo que ya entró está en lo esperado de Recaudo).
    /// Con arqueo ciego, sin cifras.
    /// </summary>
    public async Task<Result<CajaUnicaDto>> EstadoAsync(Guid sesionDeRecaudo, CancellationToken ct)
    {
        var (rige, caja, sesion) = await LeerAsync(sesionDeRecaudo, ct);
        if (sesion is null) return Result.Failure<CajaUnicaDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var (vinculada, _, pendiente) = await PendienteAsync(sesionDeRecaudo, ct);
        var ciego = ValoresSelladosDeLaSesion.De(sesion).BlindCount && await ArqueoEstricto.EsAsync(parametros, reloj.HoyLocal, ct);
        var medios = vinculada is null
            ? []
            : vinculada.Means.Where(m => pendiente.ContainsKey(m.PaymentMeansId)).GroupBy(m => m.PaymentMeansId)
                .Select(g => new CajaUnicaMedioDto(g.First().PaymentMeansPublicId, g.First().Code, ciego ? null : pendiente[g.Key])).ToList();
        return Result.Success(new CajaUnicaDto(rige, caja, vinculada?.PublicId, vinculada?.CashRegisterCode, vinculada?.PointOfSaleName,
            vinculada?.Open ?? false, medios));
    }

    /// <summary>Una clave de operación por paso, derivada de la de la petición: la misma petición repetida da las mismas claves.</summary>
    public static Guid Derivada(Guid clave, string paso)
    {
        var bytes = MD5.HashData([.. clave.ToByteArray(), .. System.Text.Encoding.UTF8.GetBytes(paso)]);
        return new Guid(bytes);
    }
}
