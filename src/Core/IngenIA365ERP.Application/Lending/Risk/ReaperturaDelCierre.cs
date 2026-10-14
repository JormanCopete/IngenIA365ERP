using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// <c>POST /api/lending/risk/monthly-closes/{id}/reopen</c> (feature 013, US9, T0706; api.md §12.3, <c>Lending.MonthlyClose.Reopen</c>; C58,
/// data-model §23.4): <b>sólo encola</b> (202) la corrida <c>CloseReopen</c> del cierre, con motivo y la persona como actor. Reglas: nunca el base
/// (<c>Lending.MonthlyClose.BaseNotReopenable</c>); sólo el último cierre vigente (<c>.NotLast</c>); antes del cierre contable del mes
/// (<c>.AccountingPeriodClosed</c>); ninguna corrida de cierre o reapertura en curso (<c>Lending.Process.AlreadyRunning</c>). El punto de acta es
/// opcional y no se valida aquí (api.md §12.3). La reapertura misma ocurre dentro de la corrida (<see cref="ReaperturaDelCierre"/>). (nuevo)
/// </summary>
public sealed record ReopenMonthlyCloseCommand(Guid MonthlyClosePublicId, string Reason, Guid? SessionItemPublicId = null)
    : IRequest<Result<ProcessRunRequestedDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReopenMonthlyCloseCommandValidator : ValidadorConMotivo<ReopenMonthlyCloseCommand>
{
    public ReopenMonthlyCloseCommandValidator() => RuleFor(x => x.MonthlyClosePublicId).NotEmpty();
}

public sealed class ReopenMonthlyCloseCommandHandler(
    IApplicationDbContext db,
    RequisitosDelCierreMensual requisitos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ReopenMonthlyCloseCommand, Result<ProcessRunRequestedDto>>
{
    public async Task<Result<ProcessRunRequestedDto>> Handle(ReopenMonthlyCloseCommand request, CancellationToken ct)
    {
        var cierre = await db.MonthlyCloses.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.MonthlyClosePublicId && !c.IsDeleted, ct);
        var validado = await ReaperturaDelCierre.ValidarAsync(db, requisitos, cierre, corridaPropia: null, ct);
        if (validado.IsFailure) return Result.Failure<ProcessRunRequestedDto>(validado.Error);

        var enCurso = await db.LendingProcessRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && (r.Kind == LendingProcessKind.MonthlyClose || r.Kind == LendingProcessKind.CloseReopen)
                && (r.Status == LendingProcessRunStatus.Requested || r.Status == LendingProcessRunStatus.Running))
            .Select(r => new { r.PublicId, r.Kind }).FirstOrDefaultAsync(ct);
        if (enCurso is not null) return Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.EnCurso(enCurso.Kind, enCurso.PublicId));
        var previa = await db.LendingProcessRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && r.Kind == LendingProcessKind.CloseReopen && r.PeriodYear == cierre!.PeriodYear && r.PeriodMonth == cierre.PeriodMonth
                && r.CloseVersion == cierre.Version)
            .Select(r => new { r.PublicId, r.Kind }).FirstOrDefaultAsync(ct);
        if (previa is not null) return Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.EnCurso(previa.Kind, previa.PublicId));

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is null) return Result.Failure<ProcessRunRequestedDto>(ErroresDeGobierno.SinUsuario());
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.CloseReopen,
            PeriodYear = cierre!.PeriodYear,
            PeriodMonth = cierre.PeriodMonth,
            CloseVersion = cierre.Version,
            Status = LendingProcessRunStatus.Requested,
            RequestedByKind = ActorKind.Person,
            RequestedByUserId = actor.UserId,
            RequestedByCentralUserId = actor.CentralUserId,
            RequestedByName = RequestLendingProcessRunCommandHandler.Recortar(actor.Name, 150),
            RequestedByIp = RequestLendingProcessRunCommandHandler.Recortar(actor.Ip, 45),
            ExecutionOrigin = actor.UserPublicId is { } usuario ? $"Orden:{usuario:D}" : "Orden:reapertura",
            Reason = RequestLendingProcessRunCommandHandler.Recortar(request.Reason.Trim(), 500),
            RequestedAt = reloj.UtcNow,
        };
        db.LendingProcessRuns.Add(corrida);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ProcessRunRequestedDto(corrida.PublicId, corrida.Status.ToString()));
    }
}

/// <summary>
/// La reapertura de un cierre de Cartera, dentro de su corrida <c>CloseReopen</c> (feature 013, US9, T0706; C58; data-model §23.4; contracts/
/// contabilidad.md §11.4; mensajes.md §10.18, §10.23). En <b>una</b> transacción con <c>LND_Setup</c> exclusivo: revalida (último vigente, no el
/// base, mes abierto en Contabilidad), inserta <c>LND_MonthlyCloseReopenings</c> con lo que respondió Contabilidad, una parte <c>CloseReopening</c>
/// por cada parte del cierre (<c>ReversedPartId</c>) con su <c>ReversoDeCierre</c> —clave <c>Reopen:{versión}</c>, <c>related</c> la parte original,
/// la misma fecha contable, el modo heredado y las mismas líneas, que Contabilidad invierte—, el informativo <c>CierreDeCarteraReabierto</c> y su
/// propio lote <c>LendingMonthlyClose</c> con <c>LendingClose:{aaaa-mm}</c>. El encabezado y las líneas del cierre no se tocan: su estado
/// <c>Reopened</c> y la vigencia de sus líneas se derivan de la reapertura. (nuevo)
/// </summary>
public sealed class ReaperturaDelCierre(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    RequisitosDelCierreMensual requisitos,
    EmisionDeCartera emision,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Lo que se valida al ordenar y al ejecutar. Devuelve lo que respondió Contabilidad del mes.</summary>
    public static async Task<Result<string>> ValidarAsync(IApplicationDbContext db, RequisitosDelCierreMensual requisitos, MonthlyClose? cierre,
        int? corridaPropia, CancellationToken ct)
    {
        if (cierre is null) return Result.Failure<string>(ErroresDeRiesgo.CierreInexistente());
        if (cierre.Kind == MonthlyCloseKind.Base) return Result.Failure<string>(ErroresDeRiesgo.BaseNoSeReabre());
        var vigentes = await RequisitosDelCierreMensual.CierresVigentesAsync(db, ct);
        var ultimo = vigentes.FirstOrDefault(c => c.Kind == MonthlyCloseKind.Monthly);
        if (ultimo is null || ultimo.Id != cierre.Id) return Result.Failure<string>(ErroresDeRiesgo.NoEsElUltimo(cierre.PeriodYear, cierre.PeriodMonth));
        var (abierto, json, error) = await requisitos.EstadoContableAsync(cierre.PeriodYear, cierre.PeriodMonth, ct);
        return abierto ? Result.Success(json) : Result.Failure<string>(error!);
    }

    /// <summary>La única tanda de la corrida <c>CloseReopen</c>.</summary>
    public async Task<AvanceDeCorrida> TandaAsync(LendingProcessRun corrida, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (corrida.Kind != LendingProcessKind.CloseReopen || corrida.PeriodYear is not { } anio || corrida.PeriodMonth is not { } mes
            || corrida.CloseVersion is not { } version)
            throw new InvalidOperationException("La tanda de la reapertura es de una corrida CloseReopen con su mes y su versión.");
        var corridaId = corrida.Id;
        var resultado = await TransaccionExplicita.EjecutarAsync(db, () => ReabrirAsync(corridaId, anio, mes, version, ct), ct);
        if (resultado.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(resultado.Error.Code, resultado.Error.Message));
        var seguida = db.LendingProcessRuns.Local.FirstOrDefault(x => x.Id == corridaId) ?? corrida;
        seguida.InformationCutoffAt ??= reloj.UtcNow;
        seguida.TotalsJson = resultado.Value;
        seguida.ProcessedLoans = 0;
        return AvanceDeCorrida.Terminada;
    }

    private async Task<Result<string>> ReabrirAsync(int corridaId, int anio, int mes, int version, CancellationToken ct)
    {
        await cerrojo.TomarSetupAsync(exclusivo: true, ct);
        var corrida = db.LendingProcessRuns.Local.FirstOrDefault(x => x.Id == corridaId) ?? await db.LendingProcessRuns.FirstAsync(x => x.Id == corridaId, ct);
        var anioCorto = (short)anio;
        var mesCorto = (byte)mes;
        var cierre = await db.MonthlyCloses.AsNoTracking()
            .FirstOrDefaultAsync(c => !c.IsDeleted && c.Kind == MonthlyCloseKind.Monthly && c.PeriodYear == anioCorto && c.PeriodMonth == mesCorto && c.Version == version, ct);
        var validado = await ValidarAsync(db, requisitos, cierre, corrida.Id, ct);
        if (validado.IsFailure) return Result.Failure<string>(validado.Error);

        var ahora = reloj.UtcNow;
        var reapertura = new MonthlyCloseReopening
        {
            MonthlyCloseId = cierre!.Id,
            ProcessRunId = corrida.Id,
            Reason = corrida.Reason ?? string.Empty,
            ReopenedByUserId = corrida.RequestedByUserId ?? throw new InvalidOperationException("La reapertura la ordena una persona."),
            ReopenedAt = ahora,
            AccountingPeriodStatusJson = validado.Value,
            ReversalPartCount = await db.AccountingFactParts.AsNoTracking().CountAsync(p => p.MonthlyCloseId == cierre.Id && p.MessagePublicId != null, ct),
        };
        db.MonthlyCloseReopenings.Add(reapertura);
        await db.SaveChangesAsync(ct);

        var usuario = corrida.RequestedByCentralUserId is { } central ? new UserRefV1 { CentralUserId = central, Name = corrida.RequestedByName } : null;
        var originales = await db.AccountingFactParts.AsNoTracking().Where(p => p.MonthlyCloseId == cierre.Id && p.MessagePublicId != null)
            .OrderBy(p => p.Id).ToListAsync(ct);
        var deLasPartes = originales.Select(o => o.MessagePublicId!.Value).ToList();
        var mensajes = await db.IntegrationMessages.AsNoTracking()
            .Where(m => deLasPartes.Contains(m.PublicId))
            .ToDictionaryAsync(m => m.PublicId, ct);
        var total = 0m;
        foreach (var original in originales)
        {
            var mensaje = mensajes[original.MessagePublicId!.Value];
            var lineas = LineasDe(mensaje.Type, mensaje.PayloadJson);
            var general = GeneralDe(mensaje.Type, mensaje.PayloadJson);
            corrida.PartCount += 1;
            var partePublicId = Guid.NewGuid();
            var control = lineas.Sum(l => l.Amounts.Sum(a => a.Amount));
            var contenido = new ReversoDeCierreV1
            {
                RunKind = nameof(LendingProcessKind.CloseReopen), RunPublicId = corrida.PublicId, FactDate = original.AccountingDate,
                BatchNumber = original.BatchNumber ?? 1, PartNumber = corrida.PartCount, CloseVersion = version, ReopeningPublicId = reapertura.PublicId,
                Reason = reapertura.Reason,
                ReversedPart = new OriginalFactRefV1 { MessageId = mensaje.PublicId, Type = mensaje.Type, OriginPublicId = original.PublicId, Number = mensaje.OriginNumber ?? string.Empty },
                ReversedType = mensaje.Type, Totals = PartesDelCierre.Totales(lineas), Lines = lineas, LineCount = lineas.Count, ControlTotal = control,
                GeneralImpairment = general,
            };
            var sucursal = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == original.BranchId).Select(b => b.PublicId).FirstAsync(ct);
            var rotulo = PartesDelCierre.Rotulo(true, anio, mes, version, corrida.PartCount);
            var emitido = await emision.ReversoDeCierreAsync(new EmisionDeParte(partePublicId, rotulo, original.AccountingDate, sucursal, null, null, null, usuario),
                version, contenido, original, mensaje.OriginNumber ?? string.Empty, ct);
            if (emitido.IsFailure) return Result.Failure<string>(emitido.Error);
            db.AccountingFactParts.Add(new AccountingFactPart
            {
                PublicId = partePublicId,
                SourceKind = AccountingFactPartSource.CloseReopening,
                ProcessRunId = corrida.Id,
                // Una parte por parte revertida y todas del mismo tipo (ReversoDeCierre): la tanda de la fila es su número de parte en la
                // corrida, porque el único (corrida, tanda, sucursal, tipo) no admite dos reversos de la misma tanda —la reclasificación y el
                // deterioro de una tanda—. La tanda original viaja en el contenido (BatchNumber) y la parte original en ReversedPartId. Hasta
                // el bloque 12C llevaba la tanda original y la reapertura fallaba al guardar en las dos bases (las pruebas en memoria no lo veían).
                BatchNumber = corrida.PartCount,
                MonthlyCloseReopeningId = reapertura.Id,
                ReversedPartId = original.Id,
                CloseVersion = version,
                FactType = ReversoDeCierreV1.Type,
                PerLoanMessages = false,
                ValueDate = original.ValueDate,
                AccountingDate = original.AccountingDate,
                BranchId = original.BranchId,
                OriginEventKey = ClavesDeEvento.Reapertura(version),
                MessagePublicId = emitido.Value.PublicId,
                MessageCount = 1,
                LoanCount = original.LoanCount,
                TotalAmount = original.TotalAmount,
                TotalsByDimensionJson = JsonSerializer.Serialize(contenido.Totals, Json),
                LoanListSha256 = PartesDelCierre.Huella(lineas),
            });
            total += original.TotalAmount;
        }

        var informativo = await emision.CierreReabiertoAsync(cierre, new CierreDeCarteraReabiertoV1
        {
            Year = anio, Month = mes, ReopenedCloseVersion = version, ReopeningPublicId = reapertura.PublicId, Reason = reapertura.Reason,
            ReopenedBy = usuario ?? new UserRefV1 { Name = corrida.RequestedByName },
        }, usuario, ct);
        if (informativo.IsFailure) return Result.Failure<string>(informativo.Error);
        await db.SaveChangesAsync(ct);

        // Su propio lote con el alcance del mes: sólo los reversos (los del cierre ya están en el suyo).
        var fin = cierre.CloseDate;
        var alcance = ModoDePasoDeCartera.AlcanceDeCierre(fin);
        var entregas = await db.IntegrationMessageDeliveries
            .Where(x => x.Destination == IntegrationDestinations.Accounting && x.Status == DeliveryStatus.InBatch && x.BatchId == null && x.BatchScopeKey == alcance)
            .OrderBy(x => x.MessageId).ToListAsync(ct);
        if (entregas.Count > 0)
        {
            var actor = Actor.ProcesoDeIntegracion(Actor.OrigenDeTarea(CierreMensualDeCartera.TareaDelCierre));
            var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting,
                BatchTrigger.LendingMonthlyClose, actor, reapertura.Reason, ahora);
            lote.MessageCount = entregas.Count;
            lote.CutoffMessageId = entregas[^1].MessageId;
            lote.DateFrom = fin;
            lote.DateTo = fin;
            db.IntegrationBatches.Add(lote);
            await db.SaveChangesAsync(ct);
            LotesDeIntegracion.Asignar(lote, entregas);
            await db.SaveChangesAsync(ct);
        }
        return Result.Success(JsonSerializer.Serialize(new
        {
            reopeningPublicId = reapertura.PublicId, monthlyClosePublicId = cierre.PublicId, reversalParts = originales.Count, total,
        }, Json));
    }

    /// <summary>El general y el adicional de una parte de deterioro, leídos de su mensaje (nulo en una reclasificación).</summary>
    public static IReadOnlyList<GeneralImpairmentLineV1>? GeneralDe(string tipo, string? contenido) =>
        tipo == DeterioroCalculadoV1.Type && !string.IsNullOrWhiteSpace(contenido)
            ? JsonSerializer.Deserialize<DeterioroCalculadoV1>(contenido, OpcionesDeMensajes.Opciones)?.GeneralImpairment
            : null;

    /// <summary>Las líneas del hecho de una parte del cierre, leídas de su mensaje.</summary>
    public static IReadOnlyList<LoanFactLineV1> LineasDe(string tipo, string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return [];
        return tipo switch
        {
            CarteraReclasificadaPorCalificacionV1.Type => JsonSerializer.Deserialize<CarteraReclasificadaPorCalificacionV1>(contenido, OpcionesDeMensajes.Opciones)?.Lines ?? [],
            DeterioroCalculadoV1.Type => JsonSerializer.Deserialize<DeterioroCalculadoV1>(contenido, OpcionesDeMensajes.Opciones)?.Lines ?? [],
            _ => [],
        };
    }
}

/// <summary>El ejecutor de las corridas <c>CloseReopen</c> que conduce <see cref="ConductorDeCorridasDeCartera"/> (T0706, T0707, C58, C59). (nuevo)</summary>
public sealed class EjecutorDeLaReapertura(ReaperturaDelCierre reapertura) : IEjecutorDeCorridaDeCartera
{
    public LendingProcessKind Clase => LendingProcessKind.CloseReopen;

    public Task<AvanceDeCorrida> EjecutarTandaAsync(LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct) => reapertura.TandaAsync(corrida, ct);
}
