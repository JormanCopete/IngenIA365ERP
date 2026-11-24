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

/// <summary>El coordinador de la caja única: abre, informa y prepara el cierre de la caja del punto de venta vinculada. Sin transacción propia. (nuevo)</summary>
public sealed class CajaUnica(IApplicationDbContext db, ISender sender, ParametrosDeCaja parametros, IDateTimeService reloj)
{
    /// <summary>Si la caja única rige y qué caja del punto de venta va con la caja de la sesión.</summary>
    private async Task<(bool Rige, Guid? Caja, CashSession? Sesion)> LeerAsync(Guid sesionDeRecaudo, CancellationToken ct)
    {
        var sesion = await db.ReceiptsCashSessions.AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == sesionDeRecaudo, ct);
        if (sesion is null) return (false, null, null);
        var caja = await db.CashBoxes.AsNoTracking().Where(c => c.Id == sesion.CashBoxId).Select(c => c.PosCashRegisterPublicId).FirstOrDefaultAsync(ct);
        var rige = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaSesionUnica, reloj.HoyLocal, ct);
        return (rige, caja, sesion);
    }

    /// <summary>Abre la caja del punto de venta vinculada si la caja única rige, la caja tiene una y no hay una abierta ya. El error, tal cual.</summary>
    public async Task<Result> AbrirAsync(Guid sesionDeRecaudo, Guid clave, CancellationToken ct)
    {
        var (rige, caja, sesion) = await LeerAsync(sesionDeRecaudo, ct);
        if (!rige || caja is not { } registro || sesion is null || sesion.Status != CashSessionState.Open) return Result.Success();
        var vinculada = await sender.Send(new GetLinkedCashSessionQuery(sesionDeRecaudo), ct);
        if (vinculada.IsSuccess && vinculada.Value is { Open: true }) return Result.Success();
        var abierta = await sender.Send(new Inventory.Cash.OpenCashSessionCommand(registro)
        {
            OperationKey = Derivada(clave, "abrir-punto-de-venta"),
            LinkedSessionPublicId = sesionDeRecaudo,
        }, ct);
        return abierta.IsSuccess ? Result.Success() : Result.Failure(abierta.Error);
    }

    /// <summary>
    /// Antes del arqueo que cierra: cierra cuadrada la caja vinculada (si sigue abierta) y entra lo que recibió a la sesión de Recaudo. Sin caja
    /// vinculada no hace nada. Repetirlo (un arqueo que pidió el motivo) no vuelve a cerrar ni a entrar nada.
    /// </summary>
    public async Task<Result> PrepararCierreAsync(Guid sesionDeRecaudo, Guid clave, CancellationToken ct)
    {
        var leida = await sender.Send(new GetLinkedCashSessionQuery(sesionDeRecaudo), ct);
        if (leida.IsFailure) return Result.Failure(leida.Error);
        if (leida.Value is not { } vinculada) return Result.Success();
        if (vinculada.Open)
        {
            var cerrada = await sender.Send(CajaVinculada.Cierre(vinculada, Derivada(clave, "cerrar-punto-de-venta")), ct);
            if (cerrada.IsFailure) return Result.Failure(cerrada.Error);
        }
        return await sender.Send(new RegisterPointOfSaleIntakeCommand(sesionDeRecaudo, vinculada.PublicId, vinculada.Means)
        {
            OperationKey = Derivada(clave, "entrar-punto-de-venta"),
        }, ct);
    }

    /// <summary>
    /// Para la pantalla: si rige, la caja del punto de venta y lo que recibió mientras sigue abierta (lo que ya entró a Recaudo está en lo esperado de
    /// Recaudo). Con arqueo ciego, sin cifras.
    /// </summary>
    public async Task<Result<CajaUnicaDto>> EstadoAsync(Guid sesionDeRecaudo, CancellationToken ct)
    {
        var (rige, caja, sesion) = await LeerAsync(sesionDeRecaudo, ct);
        if (sesion is null) return Result.Failure<CajaUnicaDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var leida = await sender.Send(new GetLinkedCashSessionQuery(sesionDeRecaudo), ct);
        var vinculada = leida.IsSuccess ? leida.Value : null;
        var ciego = ValoresSelladosDeLaSesion.De(sesion).BlindCount;
        var medios = vinculada is { Open: true }
            ? vinculada.Means.Where(m => m.Expected != 0m).Select(m => new CajaUnicaMedioDto(m.PaymentMeansPublicId, m.Code, ciego ? null : m.Expected)).ToList()
            : [];
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
