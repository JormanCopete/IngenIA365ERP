using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Cash;

/// <summary>
/// Cierra el día de un punto (<c>POST /api/inventory/day-closes</c>, <c>Inventory.DayClose.Execute</c>; feature 012, I3, T620; contracts/api.md
/// §21.4; FR-099, T50, F12). (nuevo)
/// </summary>
public sealed record ExecuteDayCloseCommand(Guid PointOfSalePublicId, DateOnly OperatingDate) : IRequest<Result<DayCloseDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ExecuteDayCloseCommandValidator : AbstractValidator<ExecuteDayCloseCommand>
{
    public ExecuteDayCloseCommandValidator() => RuleFor(x => x.PointOfSalePublicId).NotEmpty();
}

/// <summary>
/// En una transacción: el punto en el alcance (si no, 404); el <b>candado de la fila del punto</b> (el mismo de abrir sesión); el día no
/// está cerrado (<c>Inventory.DayClose.AlreadyClosed</c>; una carrera que llega al índice filtrado se traduce igual) y todas las sesiones del
/// punto con esa fecha operativa están cerradas (<c>Inventory.DayClose.SessionsOpen</c>). Consolida los arqueos de las sesiones en
/// <c>INV_DayCloseLines</c>: una línea por medio (lo esperado, lo contado y la diferencia de sus arqueos) y, en los medios con datáfono, una
/// por adquirente y datáfono con los pagos del día y sus lotes, con su <c>DetailKey</c>. Después de un cierre reabierto la versión es la
/// siguiente. <b>No emite mensajes</b>: lo contable ya salió con cada documento. Deja el evento <c>Inventory.DayClose.Executed</c>. (nuevo)
/// </summary>
public sealed class ExecuteDayCloseCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IAlcanceDeInventario alcanceDeLaPeticion,
    ICerrojoPorClave cerrojo,
    IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<ExecuteDayCloseCommand, Result<DayCloseDto>>
{
    public Task<Result<DayCloseDto>> Handle(ExecuteDayCloseCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var r = await CerrarAsync(request, ct);
            if (r.IsFailure) db.DescartarCambios();
            return r;
        }, ct);

    private async Task<Result<DayCloseDto>> CerrarAsync(ExecuteDayCloseCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());
        var punto = await PuntosDelAlcance.BuscarAsync(db, alcanceDeLaPeticion, request.PointOfSalePublicId, ct);
        if (punto is null) return Falla(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        await cerrojo.BloquearAsync(ClavesDeCerrojo.PuntoDeVenta(punto.Id), ct);

        var fecha = request.OperatingDate;
        if (await ColisionesDeVenta.DiaCerradoAsync(db, punto.Id, fecha, ct) is { } cerrado) return Falla(cerrado);
        var sesiones = await db.CashSessions.AsNoTracking().Where(s => s.PointOfSaleId == punto.Id && s.OperatingDate == fecha).OrderBy(s => s.Id).ToListAsync(ct);
        var abiertas = sesiones.Where(s => s.EstaAbierta).ToList();
        if (abiertas.Count > 0)
        {
            var cajas = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => abiertas.Select(s => s.CashRegisterId).Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Code, ct);
            return Falla(ErroresDeCaja.DayCloseSessionsOpen(abiertas
                .Select(s => new SesionAbiertaDto(s.PublicId, cajas.GetValueOrDefault(s.CashRegisterId) ?? string.Empty, s.CashierName)).ToList()));
        }

        var version = await db.DayCloses.AsNoTracking().Where(d => d.PointOfSaleId == punto.Id && d.OperatingDate == fecha)
            .Select(d => (short?)d.Version).MaxAsync(ct);
        var cierre = new DayClose
        {
            PointOfSaleId = punto.Id,
            OperatingDate = fecha,
            Version = (short)((version ?? 0) + 1),
            ClosedAt = reloj.UtcNow,
            ClosedByUserId = usuario,
            SessionCount = (short)sesiones.Count,
        };
        foreach (var linea in await ConsolidarAsync(sesiones.Select(s => s.Id).ToList(), ct)) cierre.Lines.Add(linea);
        var porMedio = cierre.Lines.Where(l => l.CardAcquirerId is null && l.CardTerminalId is null).ToList();
        cierre.TotalExpected = porMedio.Sum(l => l.ExpectedAmount);
        cierre.TotalCounted = porMedio.Sum(l => l.CountedAmount);
        cierre.TotalDifference = porMedio.Sum(l => l.DifferenceAmount);
        db.DayCloses.Add(cierre);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ColisionesDeVenta.Es(ex))
        {
            var error = await ColisionesDeVenta.TraducirAsync(db, ex, new DatosDeLaColision(PointOfSaleId: punto.Id, OperatingDate: fecha), ct);
            return Falla(error ?? throw ex);
        }

        await auditoria.AnotarAsync(AuditEventTypes.InventoryDayCloseExecuted, request, cierre.PublicId,
            new { pointOfSale = punto.Code, cierre.OperatingDate, cierre.Version, cierre.SessionCount, cierre.TotalExpected, cierre.TotalCounted, cierre.TotalDifference },
            ct, entidad: AuditoriaDelPuntoDeVenta.EntidadCierreDelDia);
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDeCierresDelDia.DtosAsync(db, [cierre], ct))[0]);
    }

    /// <summary>
    /// Las líneas del cierre: por medio, la suma de los arqueos de las sesiones; por (medio, adquirente, datáfono), los pagos confirmados
    /// del día con datáfono (recibido − reintegrado), lo contado en sus lotes y los números de lote.
    /// </summary>
    private async Task<IReadOnlyList<DayCloseLine>> ConsolidarAsync(IReadOnlyList<int> sesiones, CancellationToken ct)
    {
        var lineas = new List<DayCloseLine>();
        var arqueos = await (from l in db.CashCountLines.AsNoTracking()
                             join c in db.CashCounts.AsNoTracking() on l.CashCountId equals c.Id
                             where sesiones.Contains(c.CashSessionId) && !c.IsDeleted && !l.IsDeleted
                             select new { l.Id, l.PaymentMeansId, l.ExpectedAmount, l.CountedAmount, l.DifferenceAmount, l.PaymentCount }).ToListAsync(ct);
        foreach (var g in arqueos.GroupBy(a => a.PaymentMeansId).OrderBy(g => g.Key))
        {
            lineas.Add(new DayCloseLine
            {
                PaymentMeansId = g.Key,
                DetailKey = DayCloseLine.Clave(g.Key, null, null),
                ExpectedAmount = g.Sum(a => a.ExpectedAmount),
                CountedAmount = g.Sum(a => a.CountedAmount),
                DifferenceAmount = g.Sum(a => a.DifferenceAmount),
                PaymentCount = g.Sum(a => a.PaymentCount),
            });
        }

        var pagos = await (from p in db.DocumentPayments.AsNoTracking()
                           join d in db.InventoryDocuments.AsNoTracking() on p.DocumentId equals d.Id
                           join m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on p.PaymentMeansId equals m.Id
                           where p.CashSessionId != null && sesiones.Contains(p.CashSessionId.Value) && !p.IsDeleted && d.Status == DocumentStatus.Confirmed
                                 && p.CardTerminalId != null
                           select new { p.PaymentMeansId, m.CardAcquirerId, Terminal = p.CardTerminalId!.Value, p.Direction, p.Amount }).ToListAsync(ct);
        var lineaIds = arqueos.Select(a => a.Id).ToList();
        var lotes = await (from b in db.CashCountTerminalBatches.AsNoTracking()
                           join l in db.CashCountLines.AsNoTracking() on b.CashCountLineId equals l.Id
                           where lineaIds.Contains(b.CashCountLineId) && !b.IsDeleted
                           select new { l.PaymentMeansId, b.CardTerminalId, b.BatchNumber, b.BatchTotal }).ToListAsync(ct);
        var adquirentes = await db.CardTerminals.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(t => t.Id, t => t.CardAcquirerId, ct);

        var claves = pagos.Select(p => (Medio: p.PaymentMeansId, Terminal: p.Terminal))
            .Concat(lotes.Select(b => (Medio: b.PaymentMeansId, Terminal: b.CardTerminalId))).Distinct().OrderBy(k => k.Medio).ThenBy(k => k.Terminal);
        foreach (var (medio, terminal) in claves)
        {
            var suyos = pagos.Where(p => p.PaymentMeansId == medio && p.Terminal == terminal).ToList();
            var susLotes = lotes.Where(b => b.PaymentMeansId == medio && b.CardTerminalId == terminal).ToList();
            var adquirente = suyos.FirstOrDefault()?.CardAcquirerId ?? adquirentes.GetValueOrDefault(terminal);
            var esperado = suyos.Sum(p => p.Direction == PaymentDirection.Received ? p.Amount : -p.Amount);
            var contado = susLotes.Sum(b => b.BatchTotal);
            lineas.Add(new DayCloseLine
            {
                PaymentMeansId = medio,
                CardAcquirerId = adquirente,
                CardTerminalId = terminal,
                DetailKey = DayCloseLine.Clave(medio, adquirente, terminal),
                ExpectedAmount = esperado,
                CountedAmount = contado,
                DifferenceAmount = contado - esperado,
                PaymentCount = suyos.Count,
                BatchNumbersJson = susLotes.Count == 0 ? null : JsonSerializer.Serialize(susLotes.Select(b => b.BatchNumber).Distinct().ToList()),
            });
        }
        return lineas;
    }

    private static Result<DayCloseDto> Falla(Error error) => Result.Failure<DayCloseDto>(error);
}

/// <summary>Reabre un cierre del día con su motivo (<c>POST /api/inventory/day-closes/{id}/reopen</c>, <c>Inventory.DayClose.Reopen</c>; §21.4, F12). (nuevo)</summary>
public sealed record ReopenDayCloseCommand(Guid DayClosePublicId, string Reason) : IRequest<Result<DayCloseDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReopenDayCloseCommandValidator : ValidadorConMotivo<ReopenDayCloseCommand>
{
    public ReopenDayCloseCommandValidator() => RuleFor(x => x.DayClosePublicId).NotEmpty();
}

/// <summary>
/// El cierre queda <c>Reopened</c> con sus líneas como evidencia; se vuelven a abrir sesiones con esa fecha y el siguiente cierre es otra fila
/// con <c>Version + 1</c>. Sólo el vigente (<c>Inventory.DayClose.NotClosed</c>). Queda auditado con el motivo. (nuevo)
/// </summary>
public sealed class ReopenDayCloseCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<ReopenDayCloseCommand, Result<DayCloseDto>>
{
    public async Task<Result<DayCloseDto>> Handle(ReopenDayCloseCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DayCloseDto>(ErroresDelDocumento.SinUsuario());
        var cierre = await VistaDeCierresDelDia.VisibleAsync(db, alcanceDeLaPeticion, request.DayClosePublicId, seguir: true, ct);
        if (cierre is null) return Result.Failure<DayCloseDto>(ErroresDeCaja.DayCloseNotFound());
        if (cierre.Status != DayCloseStatus.Closed) return Result.Failure<DayCloseDto>(ErroresDeCaja.DayCloseNotClosed());

        cierre.Reabrir(usuario, reloj.UtcNow, request.Reason);
        await auditoria.AnotarAsync(AuditEventTypes.InventoryDayCloseReopened, request, cierre.PublicId,
            new { cierre.OperatingDate, cierre.Version, reason = cierre.ReopenReason }, ct, entidad: AuditoriaDelPuntoDeVenta.EntidadCierreDelDia);
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDeCierresDelDia.DtosAsync(db, [cierre], ct))[0]);
    }
}

/// <summary><c>GET /api/inventory/day-closes</c> (§21.4): los cierres de los puntos del alcance, del más reciente al más antiguo. (nuevo)</summary>
public sealed record ListDayClosesQuery(Guid? PointOfSalePublicId = null, DateOnly? From = null, DateOnly? To = null) : IRequest<Result<IReadOnlyList<DayCloseDto>>>;

public sealed class ListDayClosesQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListDayClosesQuery, Result<IReadOnlyList<DayCloseDto>>>
{
    public async Task<Result<IReadOnlyList<DayCloseDto>>> Handle(ListDayClosesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var q = db.DayCloses.AsNoTracking().PorPunto(alcance, d => d.PointOfSaleId);
        if (request.PointOfSalePublicId is { } p)
        {
            var id = await db.PointsOfSale.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
            q = q.Where(d => d.PointOfSaleId == id);
        }
        if (request.From is { } desde) q = q.Where(d => d.OperatingDate >= desde);
        if (request.To is { } hasta) q = q.Where(d => d.OperatingDate <= hasta);
        var filas = await q.OrderByDescending(d => d.OperatingDate).ThenByDescending(d => d.Version).Take(500).ToListAsync(ct);
        return Result.Success(await VistaDeCierresDelDia.DtosAsync(db, filas, ct));
    }
}

/// <summary><c>GET /api/inventory/day-closes/{id}</c> (§21.4): el cierre con sus líneas por medio, sus tarjetas por adquirente y datáfono y sus sesiones. (nuevo)</summary>
public sealed record GetDayCloseQuery(Guid DayClosePublicId) : IRequest<Result<DayCloseDetailDto>>;

public sealed class GetDayCloseQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<GetDayCloseQuery, Result<DayCloseDetailDto>>
{
    public async Task<Result<DayCloseDetailDto>> Handle(GetDayCloseQuery request, CancellationToken ct)
    {
        var cierre = await VistaDeCierresDelDia.VisibleAsync(db, alcanceDeLaPeticion, request.DayClosePublicId, seguir: false, ct);
        if (cierre is null) return Result.Failure<DayCloseDetailDto>(ErroresDeCaja.DayCloseNotFound());

        var lineas = await db.DayCloseLines.AsNoTracking().Where(l => l.DayCloseId == cierre.Id && !l.IsDeleted).OrderBy(l => l.Id).ToListAsync(ct);
        var medioIds = lineas.Select(l => l.PaymentMeansId).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => medioIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code, ct);
        var adquirenteIds = lineas.Select(l => l.CardAcquirerId).OfType<int>().Distinct().ToList();
        var adquirentes = await db.CardAcquirers.AsNoTracking().IgnoreQueryFilters().Where(a => adquirenteIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Code, ct);
        var terminalIds = lineas.Select(l => l.CardTerminalId).OfType<int>().Distinct().ToList();
        var terminales = await db.CardTerminals.AsNoTracking().IgnoreQueryFilters().Where(t => terminalIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var sesiones = await (from s in db.CashSessions.AsNoTracking()
                              join c in db.CashRegisters.AsNoTracking().IgnoreQueryFilters() on s.CashRegisterId equals c.Id
                              where s.PointOfSaleId == cierre.PointOfSaleId && s.OperatingDate == cierre.OperatingDate
                              orderby s.Id
                              select new DayCloseSessionDto(s.PublicId, c.Code, s.CashierName, s.OpenedAt, s.ClosedAt)).ToListAsync(ct);

        return Result.Success(new DayCloseDetailDto(
            (await VistaDeCierresDelDia.DtosAsync(db, [cierre], ct))[0],
            lineas.Where(l => l.CardAcquirerId is null && l.CardTerminalId is null)
                .Select(l => new DayCloseLineDto(medios.GetValueOrDefault(l.PaymentMeansId) ?? string.Empty, l.ExpectedAmount, l.CountedAmount, l.DifferenceAmount, l.PaymentCount))
                .ToList(),
            lineas.Where(l => l.CardTerminalId is not null)
                .Select(l => new DayCloseCardDto(medios.GetValueOrDefault(l.PaymentMeansId) ?? string.Empty,
                    l.CardAcquirerId is int a ? adquirentes.GetValueOrDefault(a) : null, terminales.GetValueOrDefault(l.CardTerminalId!.Value),
                    l.BatchNumbersJson is null ? [] : JsonSerializer.Deserialize<List<string>>(l.BatchNumbersJson) ?? [],
                    l.ExpectedAmount, l.CountedAmount, l.PaymentCount))
                .ToList(),
            sesiones));
    }
}

/// <summary>Leer un cierre del día con su alcance y armar su <see cref="DayCloseDto"/>. (nuevo)</summary>
public static class VistaDeCierresDelDia
{
    /// <summary>El cierre si su punto está en el alcance; nulo si no (el mismo 404 que el inexistente).</summary>
    public static async Task<DayClose?> VisibleAsync(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.DayCloses : db.DayCloses.AsNoTracking();
        var cierre = await consulta.FirstOrDefaultAsync(d => d.PublicId == publicId, ct);
        if (cierre is null) return null;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        return alcance.IncluyePunto(cierre.PointOfSaleId) ? cierre : null;
    }

    public static async Task<IReadOnlyList<DayCloseDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<DayClose> cierres, CancellationToken ct)
    {
        if (cierres.Count == 0) return [];
        var puntoIds = cierres.Select(c => c.PointOfSaleId).Distinct().ToList();
        var puntos = await db.PointsOfSale.AsNoTracking().IgnoreQueryFilters().Where(p => puntoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ReferenciaDto(p.PublicId, p.Code, p.Name), ct);
        var usuarioIds = cierres.Select(c => c.ClosedByUserId).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarioIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        return cierres.Select(c => new DayCloseDto(c.PublicId, puntos[c.PointOfSaleId], c.OperatingDate, c.Version, c.Status, c.ClosedAt,
            usuarios.GetValueOrDefault(c.ClosedByUserId) ?? c.CreatedBy, c.ReopenedAt, c.ReopenReason, c.TotalCounted)).ToList();
    }
}
