using System.Globalization;
using System.Text.Json.Serialization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Sessions;

// Feature 015, US2 (T0092; FR-022, FR-028 a FR-030, FR-181, FR-182; R-14; contracts/api.md §3; data-model §2): abrir la sesión con la base por
// denominaciones (sella los valores de la caja), consultarla, ver lo esperado (oculto con arqueo ciego), mover efectivo entre la caja, la bóveda y otra
// caja con doble custodia, y reclasificar un medio sin tocar el recibo. Toda escritura es una escritura de Recaudo (antes de activar,
// Receipts.NotActivated), lleva Idempotency-Key y toma los cerrojos en el orden fijo de data-model §0.5 con la numeración al final. (nuevo)

/// <summary><c>POST /api/receipts/sessions</c> (<c>Receipts.Session.Open</c>): abre la sesión del cajero en la caja con la base contada. (nuevo)</summary>
public sealed record OpenCashSessionCommand(Guid CashBoxPublicId, IReadOnlyList<DenominacionContadaDto> OpeningBase)
    : IRequest<Result<CashSessionDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class OpenCashSessionCommandValidator : AbstractValidator<OpenCashSessionCommand>
{
    public OpenCashSessionCommandValidator()
    {
        RuleFor(x => x.CashBoxPublicId).NotEmpty().WithMessage("Indique la caja.");
        RuleFor(x => x.OpeningBase).NotNull();
        RuleForEach(x => x.OpeningBase).Must(d => d.Quantity >= 0).WithMessage("Una cantidad no puede ser negativa.");
    }
}

public sealed class OpenCashSessionCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    ParametrosDeCaja parametros) : IRequestHandler<OpenCashSessionCommand, Result<CashSessionDto>>
{
    public async Task<Result<CashSessionDto>> Handle(OpenCashSessionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } cajero) return Result.Failure<CashSessionDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;

        var visible = await AlcanceDePuntos.CajaVisibleAsync(db, alcance, request.CashBoxPublicId, seguir: false, ct);
        if (visible is not { } encontrada) return Result.Failure<CashSessionDto>(ErroresDeRecaudo.NoEncontrado("La caja"));
        var (caja, punto) = encontrada;
        if (!caja.VigenteEn(hoy) || !punto.VigenteEn(hoy)) return Result.Failure<CashSessionDto>(ErroresDeRecaudo.CajaNoVigente());
        var asignaciones = await db.CashBoxCashiers.AsNoTracking().Where(a => a.CashBoxId == caja.Id && a.UserId == cajero).ToListAsync(ct);
        if (!asignaciones.Any(a => a.VigenteEn(hoy))) return Result.Failure<CashSessionDto>(ErroresDeRecaudo.NoEsCajeroDeLaCaja());

        await cerrojo.TomarSetupAsync(ct: ct);

        // El día del punto: se crea con la primera sesión del día (UK (CollectionPointId, BusinessDate)).
        var dia = await db.CollectionPointDays.FirstOrDefaultAsync(d => d.CollectionPointId == punto.Id && d.BusinessDate == hoy, ct);
        if (dia is null)
        {
            dia = new CollectionPointDay { CollectionPointId = punto.Id, BusinessDate = hoy };
            db.CollectionPointDays.Add(dia);
            await db.SaveChangesAsync(ct);
        }
        if (dia.Status == CollectionPointDayStatus.Closed) return Result.Failure<CashSessionDto>(ErroresDeRecaudo.DiaCerrado());
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [dia.Id], exclusivo: false, ct);

        // Una sesión viva por caja (la base también lo exige) y, con Caja.UnaSesionPorCajero, una por cajero.
        var vivas = CashSession.Vivas;
        var deLaCaja = await db.ReceiptsCashSessions.AsNoTracking().FirstOrDefaultAsync(s => s.CashBoxId == caja.Id && vivas.Contains(s.Status), ct);
        if (deLaCaja is not null)
            return Result.Failure<CashSessionDto>(ErroresDeRecaudo.SesionYaAbierta(await SesionesDeCaja.NombreAsync(db, deLaCaja.CashierUserId, ct), deLaCaja.PublicId));
        var unaPorCajero = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaUnaSesionPorCajero, hoy, ct);
        if (unaPorCajero)
        {
            var otra = await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.CashierUserId == cajero && vivas.Contains(s.Status))
                .Select(s => s.CashBoxId).FirstOrDefaultAsync(ct);
            if (otra != 0)
                return Result.Failure<CashSessionDto>(ErroresDeRecaudo.CajeroExclusivo(
                    await db.CashBoxes.AsNoTracking().Where(c => c.Id == otra).Select(c => c.Code).FirstAsync(ct)));
        }

        var medios = await SesionesDeCaja.MediosDelPuntoAsync(db, punto.Id, hoy, ct);
        if (medios.Count == 0) return Result.Failure<CashSessionDto>(ErroresDeRecaudo.SinMediosEnElPunto());
        var efectivo = medios.FirstOrDefault(m => m.Class == PaymentMeansClass.Cash);

        var denominaciones = await SesionesDeCaja.DenominacionesAsync(db, request.OpeningBase, ct);
        if (denominaciones.IsFailure) return Result.Failure<CashSessionDto>(denominaciones.Error);
        var baseContada = denominaciones.Value.Sum(d => d.Valor);
        if (baseContada > 0m && efectivo is null)
            return Result.Failure<CashSessionDto>(ErroresDeRecaudo.MovimientoInvalido("El punto no ofrece efectivo: la sesión abre sin base."));
        if (caja.BaseMode == CashBoxBaseMode.FixedFund && baseContada != caja.BaseAmount)
            return Result.Failure<CashSessionDto>(ErroresDeRecaudo.BaseDistintaDelFondo(caja.BaseAmount, baseContada));

        var tratamiento = await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.CajaTratamientoFaltante, hoy, ct) == nameof(CountTreatment.Expense)
            ? CountTreatment.Expense
            : CountTreatment.CashierReceivable;
        var sellados = new ValoresSelladosDeLaSesion(
            caja.BaseMode, caja.BaseAmount, caja.CashWarnCap, caja.CashBlockCap, caja.DailyCashOutCap,
            await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaArqueoCiego, hoy, ct),
            await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaDobleCustodia, hoy, ct),
            unaPorCajero,
            tratamiento,
            await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.CajaSobrantesDiasAntesDeIngreso, hoy, ct),
            efectivo?.PaymentMeansId,
            medios);

        var sesion = new CashSession
        {
            CashBoxId = caja.Id,
            CashierUserId = cajero,
            CollectionPointDayId = dia.Id,
            Status = CashSessionState.Open,
            OpenedAt = reloj.UtcNow,
            OpeningBase = baseContada,
            SealedSettingsJson = sellados.Json(),
        };
        db.ReceiptsCashSessions.Add(sesion);
        await db.SaveChangesAsync(ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);

        // El movimiento de apertura con sus denominaciones; la numeración al final.
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, punto.Id, ct);
        var apertura = new CashMovement
        {
            DocumentTypeId = tipo.Id,
            Kind = CashMovementKindR.Opening,
            CashSessionId = sesion.Id,
            FromMeansId = efectivo?.PaymentMeansId,
            Amount = baseContada,
            Reason = "Base de apertura",
            MovedAt = reloj.UtcNow,
            RegisteredByUserId = cajero,
        };
        apertura.Number = await numerador.AsignarAsync(tipo.Code, prefijo, ct);
        db.CashMovements.Add(apertura);
        await db.SaveChangesAsync(ct);
        foreach (var (d, cantidad, valor) in denominaciones.Value)
            db.CashMovementDenominations.Add(new CashMovementDenomination { CashMovementId = apertura.Id, CashDenominationId = d.Id, Quantity = cantidad, Amount = valor });
        await db.SaveChangesAsync(ct);
        await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await SesionesDeCaja.DtoAsync(db, sesion, ct));
    }

}

/// <summary><c>GET /api/receipts/sessions/current</c> (<c>Receipts.Session.Open</c>): la sesión viva del cajero, o nada. (nuevo)</summary>
public sealed record GetCurrentSessionQuery : IRequest<Result<CashSessionDto?>>;

public sealed class GetCurrentSessionQueryHandler(IApplicationDbContext db, IActorActual actorActual) : IRequestHandler<GetCurrentSessionQuery, Result<CashSessionDto?>>
{
    public async Task<Result<CashSessionDto?>> Handle(GetCurrentSessionQuery request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Success<CashSessionDto?>(null);
        var vivas = CashSession.Vivas;
        var sesion = await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.CashierUserId == usuario && vivas.Contains(s.Status))
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        return Result.Success(sesion is null ? null : await SesionesDeCaja.DtoAsync(db, sesion, ct));
    }
}

/// <summary><c>GET /api/receipts/sessions?state=&amp;point=&amp;date=</c> (<c>Receipts.Session.View</c>): las sesiones al alcance. (nuevo)</summary>
public sealed record ListCashSessionsQuery(CashSessionState? State = null, Guid? CollectionPointPublicId = null, DateOnly? Date = null)
    : IRequest<Result<IReadOnlyList<CashSessionDto>>>;

public sealed class ListCashSessionsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListCashSessionsQuery, Result<IReadOnlyList<CashSessionDto>>>
{
    private const int Maximo = 200;

    public async Task<Result<IReadOnlyList<CashSessionDto>>> Handle(ListCashSessionsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = await (from s in db.ReceiptsCashSessions.AsNoTracking()
                           join c in db.CashBoxes.AsNoTracking() on s.CashBoxId equals c.Id
                           join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                           join d in db.CollectionPointDays.AsNoTracking() on s.CollectionPointDayId equals d.Id
                           where (request.State == null || s.Status == request.State)
                                 && (request.CollectionPointPublicId == null || p.PublicId == request.CollectionPointPublicId)
                                 && (request.Date == null || d.BusinessDate == request.Date)
                           orderby s.Id descending
                           select new { s, CajaId = c.Id, PuntoId = p.Id, p.BranchId }).Take(Maximo * 2).ToListAsync(ct);
        var lista = new List<CashSessionDto>();
        foreach (var f in filas.Where(f => mio.IncluyeCaja(f.CajaId, f.PuntoId) || mio.IncluyeOficina(f.BranchId)).Take(Maximo))
            lista.Add(await SesionesDeCaja.DtoAsync(db, f.s, ct));
        return Result.Success<IReadOnlyList<CashSessionDto>>(lista);
    }
}

/// <summary><c>GET /api/receipts/sessions/{id}</c> (<c>Receipts.Session.View</c>). (nuevo)</summary>
public sealed record GetCashSessionQuery(Guid PublicId) : IRequest<Result<CashSessionDto>>;

public sealed class GetCashSessionQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetCashSessionQuery, Result<CashSessionDto>>
{
    public async Task<Result<CashSessionDto>> Handle(GetCashSessionQuery request, CancellationToken ct)
    {
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.PublicId, seguir: false, ct);
        return sesion is null
            ? Result.Failure<CashSessionDto>(ErroresDeRecaudo.NoEncontrado("La sesión"))
            : Result.Success(await SesionesDeCaja.DtoAsync(db, sesion, ct));
    }
}

/// <summary>
/// <c>GET /api/receipts/sessions/{id}/expected</c> (<c>Receipts.Session.ViewExpected</c>, lectura sensible): lo esperado por medio. Con arqueo ciego sellado
/// y la sesión abierta (nada contado todavía) responde <see cref="SessionExpectedDto.Hidden"/> sin valores (D6). (nuevo)
/// </summary>
public sealed record GetSessionExpectedQuery(Guid SessionPublicId) : IRequest<Result<SessionExpectedDto>>;

public sealed class GetSessionExpectedQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<GetSessionExpectedQuery, Result<SessionExpectedDto>>
{
    public async Task<Result<SessionExpectedDto>> Handle(GetSessionExpectedQuery request, CancellationToken ct)
    {
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<SessionExpectedDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var contado = await db.ReceiptsCashCounts.AsNoTracking().AnyAsync(c => c.CashSessionId == sesion.Id && c.Kind != CashCountKind.Surprise, ct);
        if (sellados.BlindCount && !contado) return Result.Success(new SessionExpectedDto(sesion.PublicId, true, [], null, false));
        var esperado = await SesionesDeCaja.EsperadoAsync(db, sesion, ct);
        var efectivo = esperado.Where(e => e.Medio.PaymentMeansId == sellados.CashMeansId).Sum(e => e.Expected);
        var dtos = esperado.Select(e =>
        {
            var medio = sellados.Medio(e.Medio.PaymentMeansId)!;
            return new EsperadoDeMedioDto(medio.PublicId, medio.Code, e.Base, e.Cobros, e.Anulaciones, e.Entradas, e.Salidas, e.Expected);
        }).ToList();
        return Result.Success(new SessionExpectedDto(sesion.PublicId, false, dtos, dtos.Sum(d => d.Expected),
            sellados.CashWarnCap is { } aviso && efectivo >= aviso));
    }
}

/// <summary>
/// <c>POST /api/receipts/sessions/{id}/movements</c> (<c>Receipts.Session.Move</c>): abastecer la caja desde la bóveda, retirar a la bóveda o trasladar a
/// otra caja abierta, en efectivo por denominaciones. Con <c>Caja.DobleCustodia</c> sellada, lo que toca la bóveda lleva un segundo custodio vigente de la
/// bóveda, otra persona (FR-182). Emite <c>MovimientoDeCajaRegistrado</c>. (nuevo)
/// </summary>
public sealed record RegisterCashMovementCommand(
    Guid SessionPublicId,
    CashMovementKindR Kind,
    IReadOnlyList<DenominacionContadaDto> Denominations,
    string Reason,
    Guid? CounterpartSessionPublicId = null,
    Guid? SecondCustodianUserPublicId = null,
    string? BagSeal = null) : IRequest<Result<CashMovementDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterCashMovementCommandValidator : AbstractValidator<RegisterCashMovementCommand>
{
    public RegisterCashMovementCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.Kind).Must(k => k is CashMovementKindR.SupplyFromVault or CashMovementKindR.WithdrawToVault or CashMovementKindR.TransferBetweenBoxes)
            .WithMessage("El movimiento es abastecer desde la bóveda, retirar a la bóveda o trasladar a otra caja.");
        RuleFor(x => x.Denominations).NotEmpty().WithMessage("Cuente el efectivo por denominaciones.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CounterpartSessionPublicId).NotEmpty().When(x => x.Kind == CashMovementKindR.TransferBetweenBoxes)
            .WithMessage("Indique la sesión de la caja que recibe.");
        RuleFor(x => x.BagSeal).MaximumLength(40);
    }
}

public sealed class RegisterCashMovementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    SegregacionDeRecaudo segregacion,
    EmisionDeRecaudo emision) : IRequestHandler<RegisterCashMovementCommand, Result<CashMovementDto>>
{
    public async Task<Result<CashMovementDto>> Handle(RegisterCashMovementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: true, ct);
        if (sesion is null) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var operable = SesionesDeCaja.Operable(sesion);
        if (operable.IsFailure) return Result.Failure<CashMovementDto>(operable.Error);
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        if (sellados.CashMeansId is not { } efectivo)
            return Result.Failure<CashMovementDto>(ErroresDeRecaudo.MovimientoInvalido("La sesión no tiene efectivo sellado."));
        var (oficina, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);

        CashSession? contraparte = null;
        if (request.Kind == CashMovementKindR.TransferBetweenBoxes)
        {
            contraparte = await SesionesDeCaja.VisibleAsync(db, alcance, request.CounterpartSessionPublicId!.Value, seguir: true, ct);
            if (contraparte is null || contraparte.Id == sesion.Id) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.NoEncontrado("La sesión que recibe"));
            var operableContraparte = SesionesDeCaja.Operable(contraparte);
            if (operableContraparte.IsFailure) return Result.Failure<CashMovementDto>(operableContraparte.Error);
            if (ValoresSelladosDeLaSesion.De(contraparte).CashMeansId is null)
                return Result.Failure<CashMovementDto>(ErroresDeRecaudo.MovimientoInvalido("La caja que recibe no tiene efectivo sellado."));
        }

        Vault? boveda = null;
        int? segundo = null;
        if (request.Kind is CashMovementKindR.SupplyFromVault or CashMovementKindR.WithdrawToVault)
        {
            boveda = await db.Vaults.FirstOrDefaultAsync(v => v.BranchId == oficina, ct);
            if (boveda is null) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.NoEncontrado("La bóveda de la oficina"));
            if (boveda.Status != ReceiptsCatalogStatus.Active) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.BovedaInactiva());
            var custodia = await MovimientosDeBoveda.SegundoCustodioAsync(db, segregacion, boveda, usuario, request.SecondCustodianUserPublicId,
                sellados.DoubleCustody, hoy, ct);
            if (custodia.IsFailure) return Result.Failure<CashMovementDto>(custodia.Error);
            segundo = custodia.Value;
        }

        var denominaciones = await SesionesDeCaja.DenominacionesAsync(db, request.Denominations, ct);
        if (denominaciones.IsFailure) return Result.Failure<CashMovementDto>(denominaciones.Error);
        var valor = denominaciones.Value.Sum(d => d.Valor);
        if (valor <= 0m) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.MovimientoInvalido("El movimiento tiene que llevar efectivo."));

        // Cerrojos: setup → día (compartido) → sesiones por Id → bóveda → numeración.
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto,
            new[] { sesion.CollectionPointDayId, contraparte?.CollectionPointDayId ?? sesion.CollectionPointDayId }.Distinct().Select(i => (long)i).ToList(),
            exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones,
            new[] { sesion.Id, contraparte?.Id ?? sesion.Id }.Distinct().OrderBy(i => i).Select(i => (long)i).ToList(), ct: ct);
        if (boveda is not null) await cerrojo.BloquearAsync(NivelDeRecaudo.Boveda, TablasDeRecaudo.Bovedas, [boveda.Id], ct: ct);

        if (request.Kind is CashMovementKindR.WithdrawToVault or CashMovementKindR.TransferBetweenBoxes)
        {
            var disponible = await SesionesDeCaja.EfectivoAsync(db, sesion, ct);
            if (valor > disponible) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.EfectivoInsuficiente(disponible));
        }
        if (request.Kind == CashMovementKindR.SupplyFromVault)
        {
            var saldo = await MovimientosDeBoveda.DescontarAsync(db, boveda!, denominaciones.Value, ct);
            if (saldo.IsFailure) return Result.Failure<CashMovementDto>(saldo.Error);
        }
        else if (request.Kind == CashMovementKindR.WithdrawToVault)
            await MovimientosDeBoveda.SumarAsync(db, boveda!, denominaciones.Value, ct);

        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, punto, ct);
        var movimiento = new CashMovement
        {
            DocumentTypeId = tipo.Id,
            Kind = request.Kind,
            CashSessionId = sesion.Id,
            CounterpartSessionId = contraparte?.Id,
            VaultId = boveda?.Id,
            FromMeansId = efectivo,
            Amount = valor,
            Reason = request.Reason.Trim(),
            SecondCustodianUserId = segundo,
            MovedAt = reloj.UtcNow,
            RegisteredByUserId = usuario,
        };
        db.CashMovements.Add(movimiento);
        if (boveda is not null)
        {
            var (tipoBoveda, prefijoBoveda) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, null, ct);
            movimiento.Number = await numerador.AsignarAsync(tipo.Code, prefijo, ct);
            var deBoveda = new VaultMovement
            {
                VaultId = boveda.Id,
                Kind = request.Kind == CashMovementKindR.SupplyFromVault ? VaultMovementKind.SupplyToBox : VaultMovementKind.ReceiveFromBox,
                Amount = request.Kind == CashMovementKindR.SupplyFromVault ? -valor : valor,
                CustodianUserId = usuario,
                SecondCustodianUserId = segundo,
                BagSeal = request.BagSeal,
                Reason = request.Reason.Trim(),
                MovedAt = reloj.UtcNow,
                Number = await numerador.AsignarAsync(tipoBoveda.Code, MovimientosDeBoveda.PrefijoDeBoveda + prefijoBoveda, ct),
            };
            await db.SaveChangesAsync(ct);
            deBoveda.CashMovementId = movimiento.Id;
            db.VaultMovements.Add(deBoveda);
            await db.SaveChangesAsync(ct);
            MovimientosDeBoveda.AgregarDenominaciones(db, deBoveda, denominaciones.Value, request.Kind == CashMovementKindR.SupplyFromVault ? -1 : 1);
        }
        else
            movimiento.Number = await numerador.AsignarAsync(tipo.Code, prefijo, ct);
        await db.SaveChangesAsync(ct);
        foreach (var (d, cantidad, monto) in denominaciones.Value)
            db.CashMovementDenominations.Add(new CashMovementDenomination { CashMovementId = movimiento.Id, CashDenominationId = d.Id, Quantity = cantidad, Amount = monto });

        await emision.MovimientoDeCajaAsync(movimiento, sesion, contraparte, boveda, hoy, tipo.Code, ct);
        await db.SaveChangesAsync(ct);
        await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
        if (contraparte is not null) await SesionesDeCaja.ProyectarAsync(db, contraparte, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await MovimientosDeCaja.DtoAsync(db, movimiento, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/sessions/{id}/reclassifications</c> (<c>Receipts.Session.Reclassify</c>): lo esperado de un medio pasa a otro (el cajero registró
/// transferencia lo que entró en efectivo, o al revés) <b>sin tocar el recibo</b> (US2-7). Desde efectivo exige el soporte cotejado adjunto y un aprobador
/// presente que no sea el cajero (FR-030). Entra al informe de excepciones y emite <c>MedioReclasificado</c>. (nuevo)
/// </summary>
public sealed record ReclassifyMeansCommand(
    Guid SessionPublicId,
    Guid FromMeansPublicId,
    Guid ToMeansPublicId,
    decimal Amount,
    string Reason,
    Guid? SupportAttachmentPublicId = null) : IRequest<Result<CashMovementDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    /// <summary>La solicitud aprobada que trae <c>X-Approval-Request</c> al repetir (no entra a la huella de la clave).</summary>
    [JsonIgnore] public Guid? ApprovalRequestPublicId { get; init; }
}

public sealed class ReclassifyMeansCommandValidator : AbstractValidator<ReclassifyMeansCommand>
{
    public ReclassifyMeansCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.FromMeansPublicId).NotEmpty();
        RuleFor(x => x.ToMeansPublicId).NotEmpty().NotEqual(x => x.FromMeansPublicId).WithMessage("El medio de destino tiene que ser otro.");
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReclassifyMeansCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    AprobacionPresenteEnCaja aprobacion,
    EmisionDeRecaudo emision) : IRequestHandler<ReclassifyMeansCommand, Result<CashMovementDto>>
{
    public async Task<Result<CashMovementDto>> Handle(ReclassifyMeansCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: true, ct);
        if (sesion is null) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var operable = SesionesDeCaja.Operable(sesion);
        if (operable.IsFailure) return Result.Failure<CashMovementDto>(operable.Error);
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var desde = sellados.Means.FirstOrDefault(m => m.PublicId == request.FromMeansPublicId);
        var hacia = sellados.Means.FirstOrDefault(m => m.PublicId == request.ToMeansPublicId);
        if (desde is null || hacia is null) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.NoEncontrado("El medio de la sesión"));
        var desdeEfectivo = desde.Class == PaymentMeansClass.Cash;
        if (desdeEfectivo && request.SupportAttachmentPublicId is null) return Result.Failure<CashMovementDto>(ErroresDeRecaudo.SoporteRequerido());

        var (_, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId],
            exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);

        var esperado = (await SesionesDeCaja.EsperadoAsync(db, sesion, ct)).First(e => e.Medio.PaymentMeansId == desde.PaymentMeansId).Expected;
        if (request.Amount > esperado)
            return Result.Failure<CashMovementDto>(ErroresDeRecaudo.MovimientoInvalido(
                string.Create(CultureInfo.InvariantCulture, $"El medio {desde.Code} sólo tiene {esperado:N0} esperado.")));

        Guid? solicitud = null;
        if (desdeEfectivo)
        {
            var obtenida = await aprobacion.ExigirAsync(new PedidoDeAprobacionPresente(
                ApprovalSubjects.CashMeansReclassification, sesion.PublicId,
                $"Reclasificar {request.Amount:N0} de {desde.Code} a {hacia.Code}", request.Amount, hoy,
                string.Create(CultureInfo.InvariantCulture, $"{desde.PublicId:N}:{hacia.PublicId:N}:{request.Amount:0.00}"),
                null, [sesion.CashierUserId], request.ApprovalRequestPublicId), ct);
            if (obtenida.IsFailure) return Result.Failure<CashMovementDto>(obtenida.Error);
            solicitud = obtenida.Value.ApprovalRequestPublicId;
        }

        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, punto, ct);
        var movimiento = new CashMovement
        {
            DocumentTypeId = tipo.Id,
            Kind = CashMovementKindR.MeansReclassification,
            CashSessionId = sesion.Id,
            FromMeansId = desde.PaymentMeansId,
            ToMeansId = hacia.PaymentMeansId,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            SupportAttachmentPublicId = request.SupportAttachmentPublicId,
            ApprovalRequestPublicId = solicitud,
            MovedAt = reloj.UtcNow,
            RegisteredByUserId = usuario,
        };
        movimiento.Number = await numerador.AsignarAsync(tipo.Code, prefijo, ct);
        db.CashMovements.Add(movimiento);
        await emision.MedioReclasificadoAsync(movimiento, sesion, null, hoy, tipo.Code, ct);
        await db.SaveChangesAsync(ct);
        await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await MovimientosDeCaja.DtoAsync(db, movimiento, ct));
    }
}

/// <summary>El DTO de un movimiento de caja. (nuevo)</summary>
public static class MovimientosDeCaja
{
    public static async Task<CashMovementDto> DtoAsync(IApplicationDbContext db, CashMovement m, CancellationToken ct)
    {
        async Task<Guid?> MedioAsync(int? id) => id is { } x
            ? await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == x).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var contraparte = m.CounterpartSessionId is { } c
            ? await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.Id == c).Select(s => (Guid?)s.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var boveda = m.VaultId is { } v ? await db.Vaults.AsNoTracking().Where(x => x.Id == v).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        return new CashMovementDto(m.PublicId, m.Number, m.Kind, m.Amount, await MedioAsync(m.FromMeansId), await MedioAsync(m.ToMeansId), contraparte, boveda,
            m.Reason, m.MovedAt);
    }
}
