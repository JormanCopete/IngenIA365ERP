using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Compliance;

/// <summary>Una transacción en efectivo del registro, para el oficial. (nuevo)</summary>
public sealed record CashTransactionDto(Guid DocumentPublicId, string DocumentNumber, CashTransactionDocumentKind DocumentKind, Guid PersonPublicId, string PersonName,
    Guid PayerPersonPublicId, decimal CashAmount, decimal OtherMeansAmount, DateOnly OccurredOn, int Month, IReadOnlyList<string> Alerts);

/// <summary>Una señal evaluada con la versión del parámetro que la levantó. (nuevo)</summary>
public sealed record ComplianceSignalDto(Guid PublicId, Guid? PersonPublicId, string? PersonName, string? DepositorKey, SenalDeEfectivo Signal, DateOnly WindowFrom,
    DateOnly WindowTo, int Count, decimal Total, string? ParameterVersion, Guid? AlertPublicId, DateTime EvaluatedAt);

/// <summary>Una operación de caja retenida por cumplimiento, para liberar o devolver. (nuevo)</summary>
public sealed record WithheldOperationDto(Guid OperationPublicId, Guid SessionPublicId, Guid PayerPersonPublicId, string PayerName, decimal CashAmount, DateTime WithheldAt);

/// <summary>Lo común del cumplimiento en la caja (feature 015, US14; FR-189, FR-203): el efecto de las listas no vinculantes y el efectivo de una operación. (nuevo)</summary>
public static class CumplimientoDeCaja
{
    public const string EfectoRetiene = "Retiene";

    public static async Task<bool> RetieneAsync(ParametrosDeCaja parametros, DateOnly fecha, CancellationToken ct) =>
        string.Equals(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.SarlaftListasNoVinculantesEfecto, fecha, ct), EfectoRetiene, StringComparison.Ordinal);

    /// <summary>La custodia viva de una operación retenida (su movimiento de entrada), o nula.</summary>
    public static async Task<CashMovement?> CustodiaAsync(IApplicationDbContext db, CashOperation o, CancellationToken ct) =>
        o.CustodyMovementId is { } id ? await db.CashMovements.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct) : null;
}

/// <summary>
/// <c>POST /api/receipts/cash-operations/{id}/withhold</c> (<c>Receipts.Receipt.Register</c>, con clave; FR-189, FR-203): con una coincidencia no vinculante y
/// <c>Sarlaft.ListasNoVinculantes.Efecto = Retiene</c>, la confirmación responde <c>Receipts.Compliance.WithholdRequired</c> y la caja retiene: el efectivo de la operación
/// queda en la sesión en custodia (movimiento <c>Custody</c>, hecho <c>DineroRetenidoEnCustodia</c>) y la operación <c>Withheld</c> hasta que el oficial la libere o se
/// devuelva por orden de pago. (nuevo)
/// </summary>
public sealed record WithholdCashOperationCommand(Guid OperationPublicId) : IRequest<Result<CashOperationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class WithholdCashOperationCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    ParametrosDeCaja parametros,
    IListasDeRecaudo listas,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<WithholdCashOperationCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(WithholdCashOperationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var (operacion, sesion) = v;
        if (operacion.Status == CashOperationState.Withheld) return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
        if (operacion.Status != CashOperationState.Preparing) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoEnPreparacion());
        var hoy = reloj.HoyLocal;
        if (operacion.ReleasedAt is not null || !await CumplimientoDeCaja.RetieneAsync(parametros, hoy, ct))
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.RetencionNoAplica());
        // Las mismas personas que mira la confirmación: pagador, titular, quien efectúa y los titulares de los ítems.
        var items = OperacionesDeCaja.Items(operacion);
        var deLosItems = items.Select(i => i.BeneficiaryPersonPublicId).Distinct().ToList();
        var titulares = await db.People.AsNoTracking().Where(p => deLosItems.Contains(p.PublicId)).Select(p => p.Id).ToListAsync(ct);
        var personas = new[] { operacion.PayerPersonId, operacion.BeneficiaryPersonId ?? 0, operacion.DeliveredByPersonId ?? 0 }.Concat(titulares)
            .Where(x => x != 0).Distinct().OrderBy(x => x).ToList();
        // Segunda ronda del 2026-10-27 (D-e): las personas liberadas por el oficial en esta operación no se consultan otra vez.
        var liberadas = await LiberacionesDeLista.PersonasLiberadasAsync(db, operacion.Id, ct);
        var resultado = await listas.EvaluarAsync(personas.Where(p => !liberadas.Contains(p)).ToList(), operacion.PublicId, ct);
        if (resultado == ScreeningOutcomeR.BindingMatch) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.ListaVinculante());
        if (resultado != ScreeningOutcomeR.NonBindingMatch) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.RetencionNoAplica());
        operacion.ScreeningOutcome = resultado;
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var medios = OperacionesDeCaja.Medios(operacion);
        var efectivo = OperacionesDeCaja.Efectivo(medios, sellados);
        if (efectivo <= 0m || sellados.CashMeansId is not { } medioDelEfectivo) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.RetencionSinEfectivo());

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);
        var (_, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, punto, ct);
        var movimiento = new CashMovement
        {
            DocumentTypeId = tipo.Id, Kind = CashMovementKindR.Custody, CashSessionId = sesion.Id, FromMeansId = medioDelEfectivo, Amount = efectivo,
            Reason = "Retenido por coincidencia en lista no vinculante", MovedAt = reloj.UtcNow, RegisteredByUserId = usuario,
            Number = await numerador.AsignarAsync(tipo.Code, prefijo, ct),
        };
        db.CashMovements.Add(movimiento);
        await db.SaveChangesAsync(ct);
        operacion.CustodyMovementId = movimiento.Id;
        operacion.Status = CashOperationState.Withheld;
        await emision.DineroRetenidoEnCustodiaAsync(movimiento, sesion, operacion, hoy, tipo.Code, ct);
        await auditoria.AgregarAsync("Receipts.Compliance.Withheld", "CashOperation", operacion.PublicId, null, new { movement = movimiento.Number, cash = efectivo, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-operations/{id}/release</c> (<c>Receipts.Compliance.Release</c>, con clave; FR-189): el oficial de cumplimiento libera la operación retenida
/// con su soporte: la custodia sale de la sesión (movimiento <c>CustodyRelease</c>, hecho <c>CustodiaLiberada</c> hacia la caja) y la operación vuelve a preparación,
/// marcada como liberada (la confirmación ya no la retiene; la coincidencia sigue alertando). Quien libera no puede ser el cajero que la retuvo. (nuevo)
/// </summary>
public sealed record ReleaseWithheldOperationCommand(Guid OperationPublicId, Guid SupportAttachmentPublicId, string Reason) : IRequest<Result<CashOperationDto>>,
    IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReleaseWithheldOperationCommandValidator : AbstractValidator<ReleaseWithheldOperationCommand>
{
    public ReleaseWithheldOperationCommandValidator()
    {
        RuleFor(x => x.OperationPublicId).NotEmpty();
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte de la liberación.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReleaseWithheldOperationCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    SegregacionDeRecaudo segregacion,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ReleaseWithheldOperationCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(ReleaseWithheldOperationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var (operacion, sesion) = v;
        if (operacion.Status != CashOperationState.Withheld) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoRetenida());
        var custodia = await CumplimientoDeCaja.CustodiaAsync(db, operacion, ct);
        if (custodia is null) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoRetenida());
        if (await segregacion.MismaPersonaAsync(custodia.RegisteredByUserId, usuario, ct)) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.LiberacionPorQuienRetuvo());
        if (sesion.Status != CashSessionState.Open) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.LiberacionConSesionCerrada());
        var hoy = reloj.HoyLocal;
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);
        var (_, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, punto, ct);
        var salida = new CashMovement
        {
            DocumentTypeId = tipo.Id, Kind = CashMovementKindR.CustodyRelease, CashSessionId = sesion.Id, FromMeansId = custodia.FromMeansId, Amount = custodia.Amount,
            Reason = request.Reason.Trim(), SupportAttachmentPublicId = request.SupportAttachmentPublicId, MovedAt = reloj.UtcNow, RegisteredByUserId = usuario,
            Number = await numerador.AsignarAsync(tipo.Code, prefijo, ct),
        };
        db.CashMovements.Add(salida);
        operacion.Status = CashOperationState.Preparing;
        operacion.ReleasedByUserId = usuario;
        operacion.ReleasedAt = reloj.UtcNow;
        await emision.CustodiaLiberadaAsync(salida.PublicId, salida.Number, sesion, operacion, custodia.Amount, devolucion: false, null, request.SupportAttachmentPublicId, hoy,
            tipo.Code, custodia.FromMeansId, ct);
        await auditoria.AgregarAsync("Receipts.Compliance.Released", "CashOperation", operacion.PublicId, null,
            new { movement = salida.Number, cash = custodia.Amount, support = request.SupportAttachmentPublicId, reason = salida.Reason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-operations/{id}/refund-by-order</c> (<c>Receipts.Compliance.Release</c>, con clave; FR-189): el oficial decide devolver el dinero retenido; en
/// R0, por orden de pago a Tesorería (clase <c>WithheldMoneyRefund</c>, nueva); en efectivo, en R1. La custodia pasa a <c>PuenteDeDevolucion</c> (hecho
/// <c>CustodiaLiberada</c>) y el efectivo sigue en la caja hasta la remesa; la operación queda abandonada. (nuevo)
/// </summary>
public sealed record RefundWithheldMoneyByOrderCommand(Guid OperationPublicId, Guid SupportAttachmentPublicId, Guid? PersonBankAccountPublicId = null, string Form = "Transfer")
    : IRequest<Result<CashOperationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RefundWithheldMoneyByOrderCommandValidator : AbstractValidator<RefundWithheldMoneyByOrderCommand>
{
    public RefundWithheldMoneyByOrderCommandValidator()
    {
        RuleFor(x => x.OperationPublicId).NotEmpty();
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte de la decisión.");
        RuleFor(x => x.Form).Must(f => f is "Transfer" or "Check").WithMessage("La devolución se gira por transferencia (Transfer) o cheque (Check).");
    }
}

public sealed class RefundWithheldMoneyByOrderCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    IOrdenesDePagoDeTesoreria ordenes,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RefundWithheldMoneyByOrderCommand, Result<CashOperationDto>>
{
    public const string ClaseDeOrden = "WithheldMoneyRefund";

    public async Task<Result<CashOperationDto>> Handle(RefundWithheldMoneyByOrderCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var (operacion, sesion) = v;
        if (operacion.Status != CashOperationState.Withheld) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoRetenida());
        var custodia = await CumplimientoDeCaja.CustodiaAsync(db, operacion, ct);
        if (custodia is null) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoRetenida());
        var hoy = reloj.HoyLocal;
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);
        await cerrojo.BloquearPagadoresAsync([operacion.PayerPersonId], ct);
        var pagador = await db.People.AsNoTracking().Where(p => p.Id == operacion.PayerPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var (_, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, punto, ct);
        var numero = await numerador.AsignarAsync(tipo.Code, prefijo, ct);
        var orden = await ordenes.EmitirAsync(new SolicitudDeOrdenDePago(request.OperationKey, ClaseDeOrden, pagador, request.PersonBankAccountPublicId, request.Form,
            custodia.Amount, hoy, $"Devolución del dinero retenido de la operación {numero}", operacion.PublicId), ct);
        var liberacion = Guid.NewGuid();
        operacion.Status = CashOperationState.Abandoned;
        operacion.ReleasedByUserId = usuario;
        operacion.ReleasedAt = reloj.UtcNow;
        operacion.RefundPaymentOrderPublicId = orden.PaymentOrderPublicId;
        operacion.RefundReleasePublicId = liberacion;
        operacion.RefundReleaseNumber = numero.Length <= 30 ? numero : numero[..30];
        await emision.CustodiaLiberadaAsync(liberacion, numero, sesion, operacion, custodia.Amount, devolucion: true, orden.PaymentOrderPublicId, request.SupportAttachmentPublicId,
            hoy, tipo.Code, custodia.FromMeansId, ct);
        await auditoria.AgregarAsync("Receipts.Compliance.RefundedByOrder", "CashOperation", operacion.PublicId, null,
            new { number = numero, order = orden.Number, cash = custodia.Amount, support = request.SupportAttachmentPublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
    }
}

/// <summary><c>GET /api/receipts/compliance/withheld</c> (<c>Receipts.Compliance.View</c>): las operaciones retenidas por liberar o devolver. (nuevo)</summary>
public sealed record ListWithheldOperationsQuery : IRequest<Result<IReadOnlyList<WithheldOperationDto>>>;

public sealed class ListWithheldOperationsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListWithheldOperationsQuery, Result<IReadOnlyList<WithheldOperationDto>>>
{
    public async Task<Result<IReadOnlyList<WithheldOperationDto>>> Handle(ListWithheldOperationsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = await (from o in db.CashOperations.AsNoTracking()
                           join s in db.ReceiptsCashSessions.AsNoTracking() on o.CashSessionId equals s.Id
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on o.PayerPersonId equals p.Id
                           join m in db.CashMovements.AsNoTracking() on o.CustodyMovementId equals m.Id
                           where o.Status == CashOperationState.Withheld
                           select new { o.PublicId, Sesion = s.PublicId, s.CashBoxId, Pagador = p.PublicId, Nombre = (p.BusinessName ?? (p.FirstName + " " + p.LastName)).Trim(),
                               m.Amount, m.MovedAt }).ToListAsync(ct);
        var cajas = filas.Select(f => f.CashBoxId).Distinct().ToList();
        var puntos = await db.CashBoxes.AsNoTracking().Where(c => cajas.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CollectionPointId, ct);
        return Result.Success<IReadOnlyList<WithheldOperationDto>>(filas.Where(f => mio.IncluyeCaja(f.CashBoxId, puntos.GetValueOrDefault(f.CashBoxId)))
            .Select(f => new WithheldOperationDto(f.PublicId, f.Sesion, f.Pagador, f.Nombre, f.Amount, f.MovedAt)).ToList());
    }
}

/// <summary>
/// <c>GET /api/receipts/compliance/cash-transactions?person=&amp;month=</c> (<c>Receipts.Compliance.View</c>; FR-097, FR-204): el registro de transacciones en efectivo por
/// persona y mes. Lectura sensible: queda en la auditoría. (nuevo)
/// </summary>
public sealed record ListCashTransactionsQuery(Guid? PersonPublicId = null, int? Month = null) : IRequest<Result<IReadOnlyList<CashTransactionDto>>>;

public sealed class ListCashTransactionsQueryHandler(IApplicationDbContext db, IActorActual actorActual, AuditoriaDeRecaudo auditoria)
    : IRequestHandler<ListCashTransactionsQuery, Result<IReadOnlyList<CashTransactionDto>>>
{
    public async Task<Result<IReadOnlyList<CashTransactionDto>>> Handle(ListCashTransactionsQuery request, CancellationToken ct)
    {
        var consulta = db.ReceiptsCashTransactions.AsNoTracking();
        if (request.PersonPublicId is { } p)
        {
            var id = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(t => t.PersonId == id || t.PayerPersonId == id);
        }
        if (request.Month is { } m) consulta = consulta.Where(t => t.Month == m);
        var filas = await consulta.OrderByDescending(t => t.Id).Take(500).ToListAsync(ct);
        var ids = filas.SelectMany(f => new[] { f.PersonId, f.PayerPersonId }).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new { x.PublicId, Nombre = (x.BusinessName ?? (x.FirstName + " " + x.LastName)).Trim() }, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        await auditoria.AgregarAsync("Receipts.Compliance.CashTransactionsRead", "CashTransaction", null, null,
            new { person = request.PersonPublicId, month = request.Month, rows = filas.Count, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success<IReadOnlyList<CashTransactionDto>>(filas.Select(f => new CashTransactionDto(f.DocumentPublicId, f.DocumentNumber, f.DocumentKind,
            personas[f.PersonId].PublicId, personas[f.PersonId].Nombre, personas[f.PayerPersonId].PublicId, f.CashAmount, f.OtherMeansAmount, f.OccurredOn, f.Month,
            System.Text.Json.JsonSerializer.Deserialize<List<string>>(f.AlertsJson) ?? [])).ToList());
    }
}

/// <summary><c>GET /api/receipts/compliance/signals?person=&amp;from=</c> (<c>Receipts.Compliance.View</c>; FR-095, FR-097): las señales evaluadas. Lectura sensible. (nuevo)</summary>
public sealed record ListComplianceSignalsQuery(Guid? PersonPublicId = null, DateOnly? From = null) : IRequest<Result<IReadOnlyList<ComplianceSignalDto>>>;

public sealed class ListComplianceSignalsQueryHandler(IApplicationDbContext db, IActorActual actorActual, AuditoriaDeRecaudo auditoria)
    : IRequestHandler<ListComplianceSignalsQuery, Result<IReadOnlyList<ComplianceSignalDto>>>
{
    public async Task<Result<IReadOnlyList<ComplianceSignalDto>>> Handle(ListComplianceSignalsQuery request, CancellationToken ct)
    {
        var consulta = db.ReceiptsComplianceAlertLogs.AsNoTracking();
        if (request.PersonPublicId is { } p)
        {
            var id = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(t => t.PersonId == id);
        }
        if (request.From is { } d) consulta = consulta.Where(t => t.WindowTo >= d);
        var filas = await consulta.OrderByDescending(t => t.Id).Take(500).ToListAsync(ct);
        var ids = filas.Where(f => f.PersonId != null).Select(f => f.PersonId!.Value).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new { x.PublicId, Nombre = (x.BusinessName ?? (x.FirstName + " " + x.LastName)).Trim() }, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        await auditoria.AgregarAsync("Receipts.Compliance.SignalsRead", "ComplianceAlertLog", null, null,
            new { person = request.PersonPublicId, from = request.From, rows = filas.Count, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success<IReadOnlyList<ComplianceSignalDto>>(filas.Select(f =>
        {
            var persona = f.PersonId is { } i && personas.TryGetValue(i, out var x) ? x : null;
            return new ComplianceSignalDto(f.PublicId, persona?.PublicId, persona?.Nombre, f.DepositorKey, f.Signal, f.WindowFrom, f.WindowTo, f.Count, f.Total,
                f.ParameterVersion, f.AlertPublicId, f.EvaluatedAt);
        }).ToList());
    }
}
