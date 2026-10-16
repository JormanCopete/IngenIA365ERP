using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Lending.Collections.Stages;

// Feature 013, K4 (US12, T0944; C64; data-model §26.5, §9.1; DT §2.13): el paso de despliegue de K4 que registra la etapa de cobranza que traía la
// migración (LND_MigrationLoans.CollectionStageCode) como primera entrada de cada crédito vivo. Es una corrida por cooperativa activada
// (LendingProcessKind.MigratedCollectionStages = 6) que conduce el procesador de Cartera por IEjecutorEnCooperativa y se envía por MediatR; la pide
// una persona por la ruta de corridas o la tarea de una sola vez cartera.etapas-migradas-k4. No la corre una semilla. (nuevo)

/// <summary>
/// Una tanda del registro de la etapa migrada (feature 013, K4, T0944; C64), <b>sin ruta</b>: lo envía <see cref="EjecutorDeEtapasMigradas"/> desde el
/// procesador. Recorre los créditos vivos migrados después del cursor; a cada uno con etapa de origen y sin entrada <c>Migration</c> le registra la
/// primera entrada <c>Source = Migration</c> fechada al corte de su carga: la etapa de su código (normalizado) entre las vigentes al corte o, si el
/// código no casa, la que corresponde a sus días de mora a la fecha de la corrida (su última posición). Idempotente: el índice
/// <c>UK_LND_CollectionStageHistory_Migration</c> y la lectura previa impiden registrar dos veces la etapa de un crédito. Lleva en
/// <c>TotalsJson</c> la tabla de equivalencia de códigos y, al terminar, el evento resumen <c>Lending.CollectionStages.MigratedRegistered</c>. Los
/// créditos sin etapa de origen entran por el ciclo diario. Devuelve si quedan tandas. (nuevo)
/// </summary>
public sealed record RegisterMigratedCollectionStagesCommand(Guid ProcessRunPublicId) : IRequest<Result<AvanceDeCorrida>>;

public sealed class RegisterMigratedCollectionStagesCommandValidator : AbstractValidator<RegisterMigratedCollectionStagesCommand>
{
    public RegisterMigratedCollectionStagesCommandValidator() => RuleFor(x => x.ProcessRunPublicId).NotEmpty();
}

/// <summary>Los totales de la corrida: registrados, sin código, sin etapa, ya registrados y la equivalencia «código de origen → etapa». (nuevo)</summary>
public sealed record TotalesDeEtapasMigradas(int Registered, int WithoutCode, int Unmatched, int AlreadyRegistered, Dictionary<string, string> Equivalence)
{
    public int MatchedByDays { get; init; }
}

public sealed class RegisterMigratedCollectionStagesCommandHandler(IApplicationDbContext db, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<RegisterMigratedCollectionStagesCommand, Result<AvanceDeCorrida>>
{
    /// <summary>Créditos por tanda.</summary>
    public const int Tanda = 250;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<AvanceDeCorrida>> Handle(RegisterMigratedCollectionStagesCommand request, CancellationToken ct)
    {
        var corrida = db.LendingProcessRuns.Local.FirstOrDefault(r => r.PublicId == request.ProcessRunPublicId)
                      ?? await db.LendingProcessRuns.FirstOrDefaultAsync(r => r.PublicId == request.ProcessRunPublicId && !r.IsDeleted, ct);
        if (corrida is null || corrida.Kind != LendingProcessKind.MigratedCollectionStages) return Result.Failure<AvanceDeCorrida>(ErroresDeCorridas.Inexistente());
        if (corrida.Status == LendingProcessRunStatus.Requested)
        {
            corrida.Status = LendingProcessRunStatus.Running;
            corrida.StartedAt ??= reloj.UtcNow;
        }
        var hoy = corrida.ProcessDate ?? reloj.HoyLocal;
        var totales = string.IsNullOrWhiteSpace(corrida.TotalsJson)
            ? new TotalesDeEtapasMigradas(0, 0, 0, 0, new Dictionary<string, string>(StringComparer.Ordinal))
            : JsonSerializer.Deserialize<TotalesDeEtapasMigradas>(corrida.TotalsJson, Json)!;

        var cursor = corrida.CursorLoanId ?? 0;
        var creditos = await db.Loans.AsNoTracking()
            .Where(l => l.Id > cursor && l.MigrationLoadId != null && !l.IsDeleted && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff))
            .OrderBy(l => l.Id).Take(Tanda).ToListAsync(ct);
        if (corrida.InformationCutoffAt is null)
        {
            corrida.InformationCutoffAt = reloj.UtcNow;
            corrida.TotalLoans = await db.Loans.AsNoTracking().CountAsync(l => l.MigrationLoadId != null && !l.IsDeleted
                && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff), ct);
        }
        if (creditos.Count == 0)
        {
            corrida.Status = LendingProcessRunStatus.Completed;
            corrida.FinishedAt = reloj.UtcNow;
            corrida.TotalsJson = JsonSerializer.Serialize(totales, Json);
            await db.SaveChangesAsync(ct);
            await auditoria.EmitirEtapasMigradasRegistradasAsync(corrida.PublicId, new
            {
                registered = totales.Registered, matchedByDays = totales.MatchedByDays, withoutCode = totales.WithoutCode, unmatched = totales.Unmatched,
                alreadyRegistered = totales.AlreadyRegistered, equivalence = totales.Equivalence,
            }, ct);
            return Result.Success(AvanceDeCorrida.Terminada);
        }

        var ids = creditos.Select(c => c.Id).ToList();
        var yaRegistrados = (await db.CollectionStageHistory.AsNoTracking()
            .Where(h => ids.Contains(h.LoanId) && h.Source == CollectionStageHistoryEntry.FuenteMigracion).Select(h => h.LoanId).ToListAsync(ct)).ToHashSet();
        var cargas = creditos.Select(c => c.MigrationLoadId!.Value).Distinct().ToList();
        var cortes = await db.MigrationLoads.AsNoTracking().IgnoreQueryFilters().Where(l => cargas.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.CutoffDate, ct);
        var partes = await db.MigrationLoadParts.AsNoTracking()
            .Where(p => cargas.Contains(p.MigrationLoadId) && p.Status == MigrationPartStatus.Imported && !p.IsDeleted)
            .Select(p => new { p.Id, p.MigrationLoadId }).ToListAsync(ct);
        var parteIds = partes.Select(p => p.Id).ToList();
        var numeros = creditos.Select(c => c.Number).ToList();
        var filas = (await db.MigrationLoanRows.AsNoTracking().Where(r => parteIds.Contains(r.MigrationLoadPartId) && numeros.Contains(r.SourceLoanNumber))
                .Select(r => new { r.MigrationLoadPartId, r.SourceLoanNumber, r.CollectionStageCode }).ToListAsync(ct))
            .Select(r => new { Carga = partes.First(p => p.Id == r.MigrationLoadPartId).MigrationLoadId, r.SourceLoanNumber, r.CollectionStageCode })
            .ToList();
        var posiciones = (await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.PositionDate <= hoy)
                .Select(p => new { p.LoanId, p.PositionDate, p.Version, p.DaysPastDue }).ToListAsync(ct))
            .GroupBy(p => p.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First().DaysPastDue);
        var etapasPorCorte = new Dictionary<DateOnly, IReadOnlyList<CollectionStage>>();

        var registrados = totales.Registered;
        int sinCodigo = totales.WithoutCode, sinEtapa = totales.Unmatched, ya = totales.AlreadyRegistered, porDias = totales.MatchedByDays;
        foreach (var c in creditos)
        {
            if (yaRegistrados.Contains(c.Id))
            {
                ya++;
                continue;
            }
            var fila = filas.FirstOrDefault(f => f.Carga == c.MigrationLoadId && f.SourceLoanNumber == c.Number);
            var codigo = CodigoDeCatalogo.Normalizar(fila?.CollectionStageCode) ?? fila?.CollectionStageCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                sinCodigo++;
                continue;
            }
            var corte = cortes.GetValueOrDefault(c.MigrationLoadId!.Value, hoy);
            if (!etapasPorCorte.TryGetValue(corte, out var etapas))
            {
                etapas = await CobranzaDeCartera.EtapasVigentesAsync(db, corte, ct);
                if (etapas.Count == 0) etapas = await CobranzaDeCartera.EtapasVigentesAsync(db, hoy, ct);
                etapasPorCorte[corte] = etapas;
            }
            var dias = posiciones.GetValueOrDefault(c.Id);
            var etapa = etapas.FirstOrDefault(e => e.Code == codigo);
            if (etapa is null && EtapasDeCobranza.EtapaPara(etapas.Select(e => e.Definicion()).ToList(), dias) is { } def)
            {
                etapa = etapas.First(e => e.Code == def.Codigo);
                porDias++;
            }
            if (etapa is null)
            {
                sinEtapa++;
                totales.Equivalence.TryAdd(codigo, string.Empty);
                continue;
            }
            totales.Equivalence.TryAdd(codigo, etapa.Code);
            db.CollectionStageHistory.Add(CollectionStageHistoryEntry.Registrar(c.Id, etapa, corte, dias, CollectionStageHistoryEntry.FuenteMigracion, corrida.Id,
                motivo: $"Etapa migrada «{codigo}»"));
            registrados++;
        }
        corrida.CursorLoanId = creditos[^1].Id;
        corrida.ProcessedLoans += creditos.Count;
        corrida.TotalsJson = JsonSerializer.Serialize(new TotalesDeEtapasMigradas(registrados, sinCodigo, sinEtapa, ya, totales.Equivalence) { MatchedByDays = porDias },
            Json);
        await db.SaveChangesAsync(ct);
        return Result.Success(AvanceDeCorrida.Pendiente);
    }
}

/// <summary>El ejecutor de las corridas <c>MigratedCollectionStages</c> (K4, T0944): envía cada tanda por MediatR. (nuevo)</summary>
public sealed class EjecutorDeEtapasMigradas(ISender sender) : IEjecutorDeCorridaDeCartera
{
    public LendingProcessKind Clase => LendingProcessKind.MigratedCollectionStages;

    public async Task<AvanceDeCorrida> EjecutarTandaAsync(LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct)
    {
        var r = await sender.Send(new RegisterMigratedCollectionStagesCommand(corrida.PublicId), ct);
        if (r.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(r.Error.Code, r.Error.Message));
        return r.Value;
    }
}

/// <summary>
/// Pide la corrida del registro de la etapa migrada desde la tarea de una sola vez (K4, T0944): si Cartera está activada, hay créditos migrados y la
/// cooperativa no tiene todavía una corrida <c>MigratedCollectionStages</c> terminada ni en curso, la deja <c>Requested</c> con el actor del proceso
/// (origen <c>Tarea:cartera.etapas-migradas-k4</c>); si no, no hace nada. Sin ruta. Devuelve si la pidió. (nuevo)
/// </summary>
public sealed record ScheduleMigratedCollectionStagesCommand : IRequest<Result<bool>>;

public sealed class ScheduleMigratedCollectionStagesCommandValidator : AbstractValidator<ScheduleMigratedCollectionStagesCommand>;

public sealed class ScheduleMigratedCollectionStagesCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ScheduleMigratedCollectionStagesCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ScheduleMigratedCollectionStagesCommand request, CancellationToken ct)
    {
        if (!await TareasDeCartera.CarteraActivadaAsync(db, ct)) return Result.Success(false);
        if (!await db.Loans.AsNoTracking().AnyAsync(l => l.MigrationLoadId != null && !l.IsDeleted, ct)) return Result.Success(false);
        if (await db.LendingProcessRuns.AsNoTracking().AnyAsync(r => r.Kind == LendingProcessKind.MigratedCollectionStages && !r.IsDeleted
                && r.Status != LendingProcessRunStatus.Failed, ct))
            return Result.Success(false);
        db.LendingProcessRuns.Add(new LendingProcessRun
        {
            Kind = LendingProcessKind.MigratedCollectionStages,
            ProcessDate = reloj.HoyLocal,
            Status = LendingProcessRunStatus.Requested,
            RequestedByKind = ActorKind.Process,
            ExecutionOrigin = Actor.OrigenDeTarea(TareaDeEtapasMigradas.NombreDeLaTarea),
            Reason = "Registro de la etapa de cobranza migrada al desplegar K4 (C64)",
            RequestedAt = reloj.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>
/// La tarea de una sola vez del despliegue de K4 (T0944; C64): <c>cartera.etapas-migradas-k4</c>, una vez al día mientras la cooperativa no tenga la
/// corrida; pide la corrida y después no vuelve a hacer nada (<see cref="ScheduleMigratedCollectionStagesCommand"/>). (nuevo)
/// </summary>
public sealed class TareaDeEtapasMigradas : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.etapas-migradas-k4";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => TareasDeCartera.UnaVezAlDia(ahoraLocal, ultimaCorrida);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<ISender>().Send(new ScheduleMigratedCollectionStagesCommand(), ct);
}
