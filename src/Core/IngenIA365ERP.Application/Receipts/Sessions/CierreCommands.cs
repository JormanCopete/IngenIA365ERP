using System.Globalization;
using System.Security.Cryptography;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Sessions;

// Feature 015, US2 (T0093; FR-031 a FR-034, FR-183, FR-184; D6, D7, Q7; contracts/api.md §3): registrar lo contado y cerrar, aprobar una diferencia por
// otra persona, cerrar la sesión olvidada con testigo, ajustar una sesión cerrada, el arqueo sorpresivo y el cierre del día del punto. Una sesión cerrada no
// se reabre. (nuevo)

/// <summary>El permiso con monto para aprobar diferencias de arqueo.</summary>
internal static class PermisosDeCaja
{
    public const string AprobarDiferencia = "Receipts.Count.ApproveDifference";
}

/// <summary>
/// <c>POST /api/receipts/sessions/{id}/count</c> (<c>Receipts.Session.Close</c>): registra lo contado por medio (con arqueo ciego, sin haber visto lo
/// esperado) y cierra: sin diferencias sobre la tolerancia, <c>Closed</c>; con ellas, <c>ClosedWithDifferencePending</c> hasta que otra persona las apruebe.
/// (nuevo)
/// </summary>
public sealed record RegisterClosingCountCommand(Guid SessionPublicId, IReadOnlyList<ConteoDeMedioDto> Means)
    : IRequest<Result<CashCountDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterClosingCountCommandValidator : AbstractValidator<RegisterClosingCountCommand>
{
    public RegisterClosingCountCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.Means).NotNull();
        RuleForEach(x => x.Means).ChildRules(m => m.RuleFor(c => c.Reason).MaximumLength(500));
    }
}

public sealed class RegisterClosingCountCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    ArqueosDeSesion arqueos,
    IAlertas alertas) : IRequestHandler<RegisterClosingCountCommand, Result<CashCountDto>>
{
    public async Task<Result<CashCountDto>> Handle(RegisterClosingCountCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: true, ct);
        if (sesion is null) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var operable = SesionesDeCaja.Operable(sesion);
        if (operable.IsFailure) return Result.Failure<CashCountDto>(operable.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);

        var registrado = await arqueos.RegistrarAsync(sesion, CashCountKind.Closing, request.Means, usuario, null, null, reloj.UtcNow, ct);
        if (registrado.IsFailure) return Result.Failure<CashCountDto>(registrado.Error);
        return Result.Success(await CierreDeSesion.CerrarAsync(db, arqueos, alertas, sesion, registrado.Value, usuario, reloj, ct));
    }
}

/// <summary>Lo común de cerrar una sesión con su arqueo. (nuevo)</summary>
internal static class CierreDeSesion
{
    public static async Task<CashCountDto> CerrarAsync(IApplicationDbContext db, ArqueosDeSesion arqueos, IAlertas alertas, CashSession sesion,
        ArqueosDeSesion.ArqueoRegistrado registrado, int cerro, IDateTimeService reloj, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        foreach (var linea in registrado.Lineas.Where(l => l.Difference != 0m && l.WithinTolerance))
            await arqueos.AplicarDiferenciaAsync(linea, sesion, cerro, hoy, ct);
        var pendiente = registrado.Lineas.Any(l => l.PorAprobar);
        sesion.Status = pendiente ? CashSessionState.ClosedWithDifferencePending : CashSessionState.Closed;
        sesion.ClosedAt = reloj.UtcNow;
        sesion.ClosedByUserId = cerro;
        await db.SaveChangesAsync(ct);
        await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
        await db.SaveChangesAsync(ct);
        if (pendiente)
        {
            var oficina = await (from c in db.CashBoxes.AsNoTracking()
                                 join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                                 join b in db.Branches.AsNoTracking() on p.BranchId equals b.Id
                                 where c.Id == sesion.CashBoxId
                                 select b.PublicId).FirstOrDefaultAsync(ct);
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoDiferenciaPorAprobar, "Diferencia de arqueo por aprobar",
                string.Create(CultureInfo.InvariantCulture, $"La sesión cerró con diferencias sobre la tolerancia por {registrado.Resultado.MontoAAprobar:N0}."),
                "CashSession", sesion.PublicId, DedupKey: $"Recaudo.DiferenciaPorAprobar:{sesion.PublicId:N}", ModuloDeOrigen: "Receipts",
                ScopeBranchPublicId: oficina), ct);
        }
        return await SesionesDeCaja.ArqueoDtoAsync(db, registrado.Arqueo, sesion.Status, ct);
    }
}

/// <summary>
/// <c>POST /api/receipts/sessions/{id}/differences/{lineId}/approve</c> (<c>Receipts.Count.ApproveDifference</c>, con monto): otra persona —ni el cajero, ni
/// quien contó, ni el testigo— aprueba con motivo la diferencia sobre la tolerancia y decide el tratamiento del faltante (cuenta por cobrar al cajero o gasto;
/// el defecto es el sellado de <c>Caja.TratamientoFaltante</c>). Aprobada la última, la sesión queda <c>Closed</c>. Emite <c>DiferenciaDeArqueoAprobada</c>.
/// (nuevo)
/// </summary>
public sealed record ApproveCountDifferenceCommand(Guid SessionPublicId, Guid LinePublicId, string Reason, CountTreatment? Treatment = null)
    : IRequest<Result<CashCountDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveCountDifferenceCommandValidator : AbstractValidator<ApproveCountDifferenceCommand>
{
    public ApproveCountDifferenceCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo de la aprobación.").MaximumLength(500);
    }
}

public sealed class ApproveCountDifferenceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    SegregacionDeRecaudo segregacion,
    ArqueosDeSesion arqueos,
    IAlertas alertas) : IRequestHandler<ApproveCountDifferenceCommand, Result<CashCountDto>>
{
    public async Task<Result<CashCountDto>> Handle(ApproveCountDifferenceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } aprobador) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: true, ct);
        if (sesion is null) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var linea = await (from l in db.ReceiptsCashCountLines
                           join c in db.ReceiptsCashCounts on l.CashCountId equals c.Id
                           where l.PublicId == request.LinePublicId && c.CashSessionId == sesion.Id
                           select l).FirstOrDefaultAsync(ct);
        if (linea is null) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("La línea del arqueo"));
        if (!linea.PorAprobar || sesion.Status != CashSessionState.ClosedWithDifferencePending)
            return Result.Failure<CashCountDto>(ErroresDeRecaudo.LineaSinDiferenciaPendiente());
        var arqueo = await db.ReceiptsCashCounts.AsNoTracking().FirstAsync(c => c.Id == linea.CashCountId, ct);

        var participantes = new List<int> { sesion.CashierUserId, arqueo.CountedByUserId };
        if (arqueo.WitnessUserId is { } testigo) participantes.Add(testigo);
        if (sesion.ClosedByUserId is { } cerro) participantes.Add(cerro);
        if (await segregacion.AlgunaEsLaMismaAsync(aprobador, participantes, ct)) return Result.Failure<CashCountDto>(ErroresDeRecaudo.AutoaprobacionDeArqueo());
        var monto = Math.Abs(linea.Difference);
        if (await LimitesDePermisoPorUsuario.DeAsync(db, aprobador, PermisosDeCaja.AprobarDiferencia, hoy, ct) is { } maximo && maximo < monto)
            return Result.Failure<CashCountDto>(ErroresDeRecaudo.AtribucionDeArqueoInsuficiente(maximo, monto));

        if (request.Treatment is { } tratamiento)
        {
            var valido = linea.Difference > 0m
                ? tratamiento == CountTreatment.Surplus
                : tratamiento is CountTreatment.CashierReceivable or CountTreatment.Expense;
            if (!valido) return Result.Failure<CashCountDto>(ErroresDeRecaudo.TratamientoInvalido());
        }

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);

        // La aprobación se escribe una sola vez en la línea ([EscrituraUnica]); el tratamiento elegido viaja en el hecho y en su efecto.
        linea.ApprovedByUserId = aprobador;
        linea.ApprovedAt = reloj.UtcNow;
        linea.ApprovalReason = request.Reason.Trim();
        var efecto = request.Treatment is { } elegido && elegido != linea.Treatment ? CopiaConTratamiento(linea, elegido) : linea;
        await arqueos.AplicarDiferenciaAsync(efecto, sesion, aprobador, hoy, ct);

        var quedan = await (from l in db.ReceiptsCashCountLines
                            join c in db.ReceiptsCashCounts on l.CashCountId equals c.Id
                            where c.CashSessionId == sesion.Id && l.Id != linea.Id && l.Difference != 0m && !l.WithinTolerance && l.ApprovedAt == null
                            select l.Id).AnyAsync(ct);
        if (!quedan)
        {
            sesion.Status = CashSessionState.Closed;
            await alertas.AtenderPorProcesoAsync($"Recaudo.DiferenciaPorAprobar:{sesion.PublicId:N}", "Diferencias aprobadas.", ct);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await SesionesDeCaja.ArqueoDtoAsync(db, arqueo, sesion.Status, ct));
    }

    /// <summary>
    /// La línea es un hecho y su tratamiento no se reescribe: el aprobador que elige otro tratamiento lo aplica sobre una copia sin seguimiento (con el mismo
    /// Id y PublicId), y el hecho y la cuenta por cobrar o el gasto llevan el elegido (DT §2.7).
    /// </summary>
    private static CashCountLine CopiaConTratamiento(CashCountLine l, CountTreatment tratamiento) => new()
    {
        Id = l.Id,
        PublicId = l.PublicId,
        CashCountId = l.CashCountId,
        PaymentMeansId = l.PaymentMeansId,
        Method = l.Method,
        Expected = l.Expected,
        Counted = l.Counted,
        Difference = l.Difference,
        Tolerance = l.Tolerance,
        WithinTolerance = l.WithinTolerance,
        Reason = l.Reason,
        Treatment = tratamiento,
        ApprovalRequestPublicId = l.ApprovalRequestPublicId,
        ApprovedByUserId = l.ApprovedByUserId,
        ApprovedAt = l.ApprovedAt,
        ApprovalReason = l.ApprovalReason,
    };
}

/// <summary>
/// <c>POST /api/receipts/sessions/{id}/close-forgotten</c> (<c>Receipts.Session.CloseForgotten</c>): el supervisor cierra la sesión olvidada con un arqueo
/// diferido y un testigo, otra persona que él y que el cajero (FR-034). Una diferencia sobre la tolerancia la aprueba un tercero. (nuevo)
/// </summary>
public sealed record CloseForgottenSessionCommand(Guid SessionPublicId, Guid WitnessUserPublicId, IReadOnlyList<ConteoDeMedioDto> Means)
    : IRequest<Result<CashCountDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseForgottenSessionCommandValidator : AbstractValidator<CloseForgottenSessionCommand>
{
    public CloseForgottenSessionCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.WitnessUserPublicId).NotEmpty().WithMessage("Indique el testigo del arqueo.");
        RuleFor(x => x.Means).NotNull();
    }
}

public sealed class CloseForgottenSessionCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    SegregacionDeRecaudo segregacion,
    ArqueosDeSesion arqueos,
    IAlertas alertas) : IRequestHandler<CloseForgottenSessionCommand, Result<CashCountDto>>
{
    public async Task<Result<CashCountDto>> Handle(CloseForgottenSessionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } supervisor) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: true, ct);
        if (sesion is null) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        if (sesion.Status != CashSessionState.ForgottenOpen) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEsOlvidada());
        var testigo = await db.Users.AsNoTracking().Where(u => u.PublicId == request.WitnessUserPublicId).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
        if (testigo is not { } idTestigo || await segregacion.AlgunaEsLaMismaAsync(idTestigo, [supervisor, sesion.CashierUserId], ct)
            || await segregacion.MismaPersonaAsync(supervisor, sesion.CashierUserId, ct))
            return Result.Failure<CashCountDto>(ErroresDeRecaudo.TestigoRequerido());

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);

        var registrado = await arqueos.RegistrarAsync(sesion, CashCountKind.Deferred, request.Means, supervisor, idTestigo, null, reloj.UtcNow, ct);
        if (registrado.IsFailure) return Result.Failure<CashCountDto>(registrado.Error);
        sesion.WitnessUserId = idTestigo;
        var dto = await CierreDeSesion.CerrarAsync(db, arqueos, alertas, sesion, registrado.Value, supervisor, reloj, ct);
        await alertas.AtenderPorProcesoAsync($"Recaudo.SesionOlvidada:{sesion.PublicId:N}", "Sesión cerrada con arqueo diferido.", ct);
        return Result.Success(dto);
    }
}

/// <summary>
/// <c>POST /api/receipts/sessions/{id}/adjustments</c> (<c>Receipts.Session.Adjust</c>): el ajuste de una sesión <c>Closed</c> —corrige lo contado o el medio—
/// con soporte, aprobado por un aprobador presente que no es quien lo registra ni el cajero (US2-5). La sesión no se reabre. Emite
/// <c>AjusteDeSesionRegistrado</c> enlazado con la diferencia que corrige. (nuevo)
/// </summary>
public sealed record RegisterSessionAdjustmentCommand(
    Guid SessionPublicId,
    SessionAdjustmentKind Kind,
    Guid PaymentMeansPublicId,
    decimal Amount,
    string Reason,
    Guid? SupportAttachmentPublicId) : IRequest<Result<CashSessionAdjustmentDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    [System.Text.Json.Serialization.JsonIgnore] public Guid? ApprovalRequestPublicId { get; init; }
}

public sealed class RegisterSessionAdjustmentCommandValidator : AbstractValidator<RegisterSessionAdjustmentCommand>
{
    public RegisterSessionAdjustmentCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
        RuleFor(x => x.Amount).NotEqual(0m).WithMessage("El ajuste tiene un valor distinto de cero.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte del ajuste.");
    }
}

public sealed class RegisterSessionAdjustmentCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    AprobacionPresenteEnCaja aprobacion,
    EmisionDeRecaudo emision) : IRequestHandler<RegisterSessionAdjustmentCommand, Result<CashSessionAdjustmentDto>>
{
    public async Task<Result<CashSessionAdjustmentDto>> Handle(RegisterSessionAdjustmentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashSessionAdjustmentDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<CashSessionAdjustmentDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        if (sesion.Status != CashSessionState.Closed) return Result.Failure<CashSessionAdjustmentDto>(ErroresDeRecaudo.SesionNoCerrada());
        var medio = ValoresSelladosDeLaSesion.De(sesion).Means.FirstOrDefault(m => m.PublicId == request.PaymentMeansPublicId);
        if (medio is null) return Result.Failure<CashSessionAdjustmentDto>(ErroresDeRecaudo.NoEncontrado("El medio de la sesión"));

        var obtenida = await aprobacion.ExigirAsync(new PedidoDeAprobacionPresente(
            ApprovalSubjects.CashSessionAdjustment, sesion.PublicId, $"Ajuste de {request.Amount:N0} en {medio.Code}", Math.Abs(request.Amount), hoy,
            string.Create(CultureInfo.InvariantCulture, $"{request.Kind}:{medio.PublicId:N}:{request.Amount:0.00}"), null, [sesion.CashierUserId],
            request.ApprovalRequestPublicId), ct);
        if (obtenida.IsFailure) return Result.Failure<CashSessionAdjustmentDto>(obtenida.Error);

        var (_, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);

        // El hecho de la diferencia que corrige: la de la línea del medio en el arqueo que cerró la sesión, si la tuvo.
        var lineaPublicId = await (from l in db.ReceiptsCashCountLines.AsNoTracking()
                                   join c in db.ReceiptsCashCounts.AsNoTracking() on l.CashCountId equals c.Id
                                   where c.CashSessionId == sesion.Id && l.PaymentMeansId == medio.PaymentMeansId && l.Difference != 0m
                                   orderby c.Id descending
                                   select (Guid?)l.PublicId).FirstOrDefaultAsync(ct);
        var original = lineaPublicId is { } p
            ? await db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == p).OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct)
            : null;

        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.SessionAdjustment, punto, ct);
        var ajuste = new CashSessionAdjustment
        {
            CashSessionId = sesion.Id,
            Kind = request.Kind,
            PaymentMeansId = medio.PaymentMeansId,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            ApprovalRequestPublicId = obtenida.Value.ApprovalRequestPublicId ?? Guid.Empty,
            ApprovedByUserId = obtenida.Value.AprobadorUserId ?? usuario,
            SupportAttachmentPublicId = request.SupportAttachmentPublicId,
            AdjustedAt = reloj.UtcNow,
            RegisteredByUserId = usuario,
        };
        await numerador.NumerarAsync(ajuste, tipo.Code, prefijo, ct);
        db.CashSessionAdjustments.Add(ajuste);
        await emision.AjusteDeSesionAsync(ajuste, sesion, original, hoy, tipo.Code, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new CashSessionAdjustmentDto(ajuste.PublicId, ajuste.Number, ajuste.Kind, medio.PublicId, ajuste.Amount, ajuste.Reason, ajuste.AdjustedAt));
    }
}

/// <summary>
/// <c>POST /api/receipts/surprise-counts</c> (<c>Receipts.Session.SurpriseCount</c>): el arqueo sorpresivo de una sesión abierta, con su acta y un testigo
/// opcional. Queda como hecho y no cierra la sesión ni mueve dinero: una diferencia se explica en el acta (FR-033). La bóveda se arquea con un movimiento de
/// conteo (<c>RegisterVaultMovementCommand</c>). (nuevo)
/// </summary>
public sealed record RegisterSurpriseCountCommand(Guid SessionPublicId, Guid MinutesAttachmentPublicId, IReadOnlyList<ConteoDeMedioDto> Means,
    Guid? WitnessUserPublicId = null) : IRequest<Result<CashCountDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterSurpriseCountCommandValidator : AbstractValidator<RegisterSurpriseCountCommand>
{
    public RegisterSurpriseCountCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.MinutesAttachmentPublicId).NotEmpty().WithMessage("Adjunte el acta del arqueo sorpresivo.");
        RuleFor(x => x.Means).NotNull();
    }
}

public sealed class RegisterSurpriseCountCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    ArqueosDeSesion arqueos) : IRequestHandler<RegisterSurpriseCountCommand, Result<CashCountDto>>
{
    public async Task<Result<CashCountDto>> Handle(RegisterSurpriseCountCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<CashCountDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        if (sesion.Status is not (CashSessionState.Open or CashSessionState.ForgottenOpen)) return Result.Failure<CashCountDto>(ErroresDeRecaudo.SesionNoAbierta());
        int? testigo = null;
        if (request.WitnessUserPublicId is { } t)
        {
            testigo = await db.Users.AsNoTracking().Where(u => u.PublicId == t).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
            if (testigo is null) return Result.Failure<CashCountDto>(ErroresDeRecaudo.TestigoRequerido());
        }

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);

        var registrado = await arqueos.RegistrarAsync(sesion, CashCountKind.Surprise, request.Means, usuario, testigo, request.MinutesAttachmentPublicId,
            reloj.UtcNow, ct);
        if (registrado.IsFailure) return Result.Failure<CashCountDto>(registrado.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(await SesionesDeCaja.ArqueoDtoAsync(db, registrado.Value.Arqueo, sesion.Status, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/collection-points/{id}/days/{date}/close</c> (<c>Receipts.Day.Close</c>): cierra el día del punto una sola vez, con todas sus
/// sesiones <c>Closed</c> (<c>Receipts.Day.SessionsOpen</c>) y sin diferencias por aprobar (<c>.DifferencesPending</c>), con el día en exclusivo. Si quien
/// cierra es el suplente con una suplencia vigente del punto, el cierre la registra (FR-184). Deja los totales por medio y el código de verificación del
/// informe (FR-156). (nuevo)
/// </summary>
public sealed record CloseCollectionPointDayCommand(Guid CollectionPointPublicId, DateOnly BusinessDate)
    : IRequest<Result<DayCloseDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseCollectionPointDayCommandValidator : AbstractValidator<CloseCollectionPointDayCommand>
{
    public CloseCollectionPointDayCommandValidator() => RuleFor(x => x.CollectionPointPublicId).NotEmpty();
}

public sealed class CloseCollectionPointDayCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo) : IRequestHandler<CloseCollectionPointDayCommand, Result<DayCloseDto>>
{
    public async Task<Result<DayCloseDto>> Handle(CloseCollectionPointDayCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DayCloseDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var punto = await Setup.AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<DayCloseDto>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        var dia = await db.CollectionPointDays.FirstOrDefaultAsync(d => d.CollectionPointId == punto.Id && d.BusinessDate == request.BusinessDate, ct);
        if (dia is null) return Result.Failure<DayCloseDto>(ErroresDeRecaudo.NoEncontrado("El día del punto"));
        if (dia.Status == CollectionPointDayStatus.Closed) return Result.Failure<DayCloseDto>(ErroresDeRecaudo.DiaCerrado());

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [dia.Id], exclusivo: true, ct);

        var sesiones = await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.CollectionPointDayId == dia.Id).ToListAsync(ct);
        var abiertas = sesiones.Count(s => s.Status is CashSessionState.Open or CashSessionState.Closing or CashSessionState.ForgottenOpen);
        if (abiertas > 0) return Result.Failure<DayCloseDto>(ErroresDeRecaudo.DiaConSesionesAbiertas(abiertas));
        var pendientes = sesiones.Count(s => s.Status == CashSessionState.ClosedWithDifferencePending);
        if (pendientes > 0) return Result.Failure<DayCloseDto>(ErroresDeRecaudo.DiaConDiferencias(pendientes));

        var suplencias = await db.SupervisorDelegations.AsNoTracking().Where(s => s.CollectionPointId == punto.Id && s.DelegateUserId == usuario).ToListAsync(ct);
        var suplencia = suplencias.FirstOrDefault(s => s.VigenteEn(request.BusinessDate) || s.VigenteEn(reloj.HoyLocal));

        // Los totales por medio salen de la última línea de cada medio en el arqueo que cerró cada sesión (cierre o diferido).
        var ids = sesiones.Select(s => s.Id).ToList();
        var lineas = await (from l in db.ReceiptsCashCountLines.AsNoTracking()
                            join c in db.ReceiptsCashCounts.AsNoTracking() on l.CashCountId equals c.Id
                            where c.CashSessionId != null && ids.Contains(c.CashSessionId.Value) && c.Kind != CashCountKind.Surprise
                            select new { l.PaymentMeansId, l.Expected, l.Counted, l.Difference }).ToListAsync(ct);
        var porMedio = lineas.GroupBy(l => l.PaymentMeansId)
            .Select(g => new DayCloseLine { PaymentMeansId = g.Key, Expected = g.Sum(x => x.Expected), Counted = g.Sum(x => x.Counted), Difference = g.Sum(x => x.Difference) })
            .ToList();
        var cierre = new DayClose
        {
            CollectionPointDayId = dia.Id,
            SessionCount = sesiones.Count,
            ReceiptCount = await db.IssuedReceipts.AsNoTracking().CountAsync(r => r.CashSessionId != null && ids.Contains(r.CashSessionId.Value), ct),
            VoidingCount = 0,
            Expected = porMedio.Sum(l => l.Expected),
            Counted = porMedio.Sum(l => l.Counted),
            Difference = porMedio.Sum(l => l.Difference),
            ClosedAt = reloj.UtcNow,
            ClosedByUserId = usuario,
            SupervisorDelegationId = suplencia?.Id,
            VerificationCode = CodigoDeVerificacion(),
        };
        db.ReceiptsDayCloses.Add(cierre);
        dia.Status = CollectionPointDayStatus.Closed;
        dia.ClosedByUserId = usuario;
        dia.ClosedAt = cierre.ClosedAt;
        await db.SaveChangesAsync(ct);
        foreach (var l in porMedio) { l.DayCloseId = cierre.Id; db.ReceiptsDayCloseLines.Add(l); }
        await db.SaveChangesAsync(ct);
        return Result.Success(await CierresDelDia.DtoAsync(db, cierre, ct));
    }

    /// <summary>El código de verificación del informe: 16 caracteres aleatorios en base 32 sin ambiguos (FR-156).</summary>
    public static string CodigoDeVerificacion()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(16);
        return new string(bytes.Select(b => alfabeto[b % alfabeto.Length]).ToArray());
    }
}

/// <summary>El DTO del cierre del día. (nuevo)</summary>
public static class CierresDelDia
{
    public static async Task<DayCloseDto> DtoAsync(IApplicationDbContext db, DayClose c, CancellationToken ct)
    {
        var dia = await (from d in db.CollectionPointDays.AsNoTracking()
                         join p in db.CollectionPoints.AsNoTracking() on d.CollectionPointId equals p.Id
                         where d.Id == c.CollectionPointDayId
                         select new { p.PublicId, d.BusinessDate }).FirstAsync(ct);
        var lineas = await (from l in db.ReceiptsDayCloseLines.AsNoTracking()
                            join m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on l.PaymentMeansId equals m.Id
                            where l.DayCloseId == c.Id
                            orderby m.Code
                            select new DayCloseLineDto(m.PublicId, m.Code, l.Expected, l.Counted, l.Difference)).ToListAsync(ct);
        return new DayCloseDto(c.PublicId, dia.PublicId, dia.BusinessDate, c.SessionCount, c.ReceiptCount, c.VoidingCount, c.Expected, c.Counted, c.Difference,
            c.ClosedAt, c.VerificationCode, c.SupervisorDelegationId is not null, lineas);
    }
}
