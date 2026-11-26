using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.BulkCharges;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

/// <summary>
/// Lo común de los dos ejecutores de cargos masivos (2026-11-25, feature 026; R5 de la investigación): la corrida de la clase, las tandas por cursor, una
/// transacción por asociado con su cerrojo y, al terminar, el lote contable con las entregas del alcance de la corrida (un solo comprobante <c>CO</c>). (nuevo)
/// </summary>
public abstract class EjecutorDeCargoMasivoBase(IApplicationDbContext db, ICerrojoDeCartera cerrojo, IDateTimeService reloj, ILogger logger)
{
    /// <summary>Cuántos asociados se procesan por tanda.</summary>
    public const int Tanda = 25;

    protected IApplicationDbContext Db => db;
    protected IDateTimeService Reloj => reloj;

    /// <summary>Corre <paramref name="trabajo"/> para un asociado en su transacción con el cerrojo de su deudor; si falla, deja la línea con el motivo.</summary>
    protected async Task UnoAsync(LendingProcessRun corrida, BulkChargeRun run, int lineaId, Func<BulkChargeLine, Task<Result>> trabajo, string paso,
        CancellationToken ct)
    {
        string? falla = null;
        try
        {
            var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                var linea = await db.BulkChargeLines.FirstAsync(l => l.Id == lineaId, ct);
                await cerrojo.TomarSetupAsync(exclusivo: false, ct);
                await cerrojo.BloquearDeudoresAsync([linea.PersonId], ModoDeEspera.Esperar, ct);
                return await trabajo(linea);
            }, ct);
            if (r.IsFailure) falla = $"{r.Error.Code}: {r.Error.Message}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Cartera.CargoMasivo] La línea {Linea} de la corrida {Corrida} falló en {Paso}.", lineaId, run.Number, paso);
            falla = ex.Message;
        }
        if (falla is null) return;

        // Lo que la transacción dejó a medias se suelta; la línea se relee y queda con su motivo (o, en una anulación, sigue viva con el motivo).
        db.DescartarCambiosSalvo(corrida, run);
        var l = await db.BulkChargeLines.FirstAsync(x => x.Id == lineaId, ct);
        if (paso == PasoDeAnulacion) l.NoSePudoAnular(MotivosDeCargoMasivo.TieneMovimientos, falla);
        else l.Fallar(MotivosDeCargoMasivo.ReglaDeLinea, falla, reloj.UtcNow);
        db.LendingProcessRunErrors.Add(new LendingProcessRunError
        {
            ProcessRunId = corrida.Id,
            BatchNumber = corrida.BatchCount + 1,
            AttemptNumber = corrida.RetryCount + 1,
            Step = paso,
            ErrorCode = falla.Length > 100 ? falla[..100] : falla,
            ErrorMessage = falla.Length > 1000 ? falla[..1000] : falla,
            OccurredAt = reloj.UtcNow,
        });
        corrida.FailedLoans += 1;
        await db.SaveChangesAsync(ct);
    }

    public const string PasoDeCargo = "CargoMasivo";
    public const string PasoDeAnulacion = "AnulacionDeCargoMasivo";

    /// <summary>Recuenta la corrida desde sus líneas vivas.</summary>
    protected async Task RecontarAsync(BulkChargeRun run, CancellationToken ct)
    {
        var cuentas = await db.BulkChargeLines.AsNoTracking().Where(l => l.RunId == run.Id && !l.IsDeleted)
            .GroupBy(l => l.Status).Select(g => new { Estado = g.Key, Cuantas = g.Count(), Total = g.Sum(l => l.Amount) }).ToListAsync(ct);
        int Cuantas(BulkChargeLineStatus e) => cuentas.Where(c => c.Estado == e).Sum(c => c.Cuantas);
        run.Recontar(Cuantas(BulkChargeLineStatus.Pending), Cuantas(BulkChargeLineStatus.Created), Cuantas(BulkChargeLineStatus.Skipped),
            Cuantas(BulkChargeLineStatus.Failed), Cuantas(BulkChargeLineStatus.Voided),
            cuentas.Where(c => c.Estado == BulkChargeLineStatus.Created).Sum(c => c.Total));
    }

    /// <summary>
    /// El lote de un alcance: las entregas a Contabilidad que esperan sin lote con ese alcance, en un lote <c>Manual</c> creado en la misma transacción
    /// (como el cierre de Cartera). Nulo si nada pasa.
    /// </summary>
    protected async Task<(Guid Lote, long Numero)?> LoteAsync(string alcance, string motivo, DateOnly fecha, CancellationToken ct)
    {
        var entregas = await db.IntegrationMessageDeliveries
            .Where(x => x.Destination == IntegrationDestinations.Accounting && x.Status == DeliveryStatus.InBatch && x.BatchId == null && x.BatchScopeKey == alcance)
            .OrderBy(x => x.MessageId).ToListAsync(ct);
        if (entregas.Count == 0) return null;
        var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting, BatchTrigger.Manual,
            Actor.ProcesoDeIntegracion(Actor.OrigenDeTarea(NombresDeArrendamiento.ProcesoDeCartera)), motivo, reloj.UtcNow);
        lote.MessageCount = entregas.Count;
        lote.CutoffMessageId = entregas[^1].MessageId;
        lote.DateFrom = fecha;
        lote.DateTo = fecha;
        db.IntegrationBatches.Add(lote);
        await db.SaveChangesAsync(ct);
        LotesDeIntegracion.Asignar(lote, entregas);
        await db.SaveChangesAsync(ct);
        return (lote.PublicId, lote.Number);
    }
}

/// <summary>La ejecución de una corrida de cargos masivos confirmada (clase <see cref="LendingProcessKind.BulkCharge"/>). (nuevo)</summary>
public sealed class EjecutorDeCargoMasivo(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    CorridasDeCargoMasivo corridas,
    NacimientoDeCargoMasivo nacimiento,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    ILogger<EjecutorDeCargoMasivo> logger) : EjecutorDeCargoMasivoBase(db, cerrojo, reloj, logger), IEjecutorDeCorridaDeCartera
{
    public LendingProcessKind Clase => LendingProcessKind.BulkCharge;

    public async Task<AvanceDeCorrida> EjecutarTandaAsync(LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct)
    {
        var run = await Db.BulkChargeRuns.FirstAsync(r => r.ProcessRunId == corrida.Id, ct);
        if (run.Status == BulkChargeRunStatus.Queued)
        {
            run.Iniciar();
            await Db.SaveChangesAsync(ct);
        }
        var cursor = corrida.CursorLoanId ?? 0;
        var ids = await Db.BulkChargeLines.AsNoTracking()
            .Where(l => l.RunId == run.Id && !l.IsDeleted && l.Status == BulkChargeLineStatus.Pending && l.Id > cursor)
            .OrderBy(l => l.Id).Select(l => l.Id).Take(Tanda).ToListAsync(ct);
        if (ids.Count == 0) return await TerminarAsync(run, ct);

        var leida = await corridas.LineaAsync(run.CreditLineId, run.ChargeDate, ct);
        if (leida.IsFailure) throw new InvalidOperationException($"{leida.Error.Code}: {leida.Error.Message}");
        var contable = await fechaContable.CalcularAsync(run.ChargeDate, ct);
        if (contable.IsFailure) throw new InvalidOperationException($"{contable.Error.Code}: {contable.Error.Message}");
        var modo = await emision.ModoDeCargoMasivoAsync(CargoMasivoRegistradoV1.Type, "CMS", contable.Value,
            EmisionDeCartera.AlcanceDeCargoMasivo(run.PublicId), ct);
        if (modo.IsFailure) throw new InvalidOperationException($"{modo.Error.Code}: {modo.Error.Message}");
        var contraparte = run.CounterpartPersonId is { } cp
            ? await Db.People.AsNoTracking().Where(p => p.Id == cp).Select(p => (Guid?)p.PublicId).FirstAsync(ct)
            : null;
        var usuarioCentral = await Db.LendingProcessRuns.AsNoTracking().Where(r => r.Id == corrida.Id).Select(r => r.RequestedByCentralUserId).FirstAsync(ct);
        var contexto = new ContextoDeNacimiento(run, leida.Value.Linea, contraparte, modo.Value, PrevalidationOutcome.Postable, run.ConfirmedByUserId,
            usuarioCentral);

        var limite = Reloj.UtcNow + presupuesto;
        foreach (var id in ids)
        {
            await UnoAsync(corrida, run, id, async linea =>
            {
                // Lo que pudo cambiar desde la confirmación: retirado, fallecido o con el retiro en trámite queda fuera con su motivo.
                if (await corridas.ExclusionActualAsync(linea.PersonId, run.ChargeDate, ct) is { } motivo)
                {
                    linea.Omitir(motivo, null, Reloj.UtcNow);
                    await Db.SaveChangesAsync(ct);
                    return Result.Success();
                }
                var nacido = await nacimiento.NacerAsync(contexto, linea, ct);
                return nacido.IsSuccess ? Result.Success() : Result.Failure(nacido.Error);
            }, PasoDeCargo, ct);
            corrida.CursorLoanId = id;
            corrida.ProcessedLoans += 1;
            await Db.SaveChangesAsync(ct);
            if (Reloj.UtcNow >= limite) break;
        }
        return AvanceDeCorrida.Pendiente;
    }

    private async Task<AvanceDeCorrida> TerminarAsync(BulkChargeRun run, CancellationToken ct)
    {
        await RecontarAsync(run, ct);
        var lote = await LoteAsync(EmisionDeCartera.AlcanceDeCargoMasivo(run.PublicId), $"Cargos masivos {run.Number}", run.ChargeDate, ct);
        run.Terminar(lote?.Lote, lote?.Numero, Reloj.UtcNow);
        await Db.SaveChangesAsync(ct);
        await auditoria.EmitirCargoMasivoAsync(AuditEventTypes.LendingBulkChargeCompleted, run.PublicId, new
        {
            run.Number, created = run.CreatedCount, skipped = run.SkippedCount, failed = run.FailedCount, total = run.TotalAmount, batch = lote?.Numero,
        }, ct);
        return AvanceDeCorrida.Terminada;
    }
}

/// <summary>La anulación de lo vivo de una corrida (clase <see cref="LendingProcessKind.BulkChargeVoid"/>): un solo comprobante de reverso. (nuevo)</summary>
public sealed class EjecutorDeAnulacionDeCargoMasivo(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    AnulacionDeCargoMasivo anulacion,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    ILogger<EjecutorDeAnulacionDeCargoMasivo> logger) : EjecutorDeCargoMasivoBase(db, cerrojo, reloj, logger), IEjecutorDeCorridaDeCartera
{
    public LendingProcessKind Clase => LendingProcessKind.BulkChargeVoid;

    public async Task<AvanceDeCorrida> EjecutarTandaAsync(LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct)
    {
        var run = await Db.BulkChargeRuns.FirstAsync(r => r.VoidProcessRunId == corrida.Id, ct);
        var cursor = corrida.CursorLoanId ?? 0;
        var ids = await Db.BulkChargeLines.AsNoTracking()
            .Where(l => l.RunId == run.Id && !l.IsDeleted && l.Status == BulkChargeLineStatus.Created && l.Id > cursor)
            .OrderBy(l => l.Id).Select(l => l.Id).Take(Tanda).ToListAsync(ct);
        var alcance = EmisionDeCartera.AlcanceDeCargoMasivo(run.PublicId, $"A{corrida.Id}");
        if (ids.Count == 0)
        {
            await RecontarAsync(run, ct);
            var hoy = Reloj.HoyLocal;
            await LoteAsync(alcance, $"Anulación de los cargos masivos {run.Number}", hoy, ct);
            run.TerminarAnulacion(run.CreatedCount > 0);
            await Db.SaveChangesAsync(ct);
            await auditoria.EmitirCargoMasivoAsync(AuditEventTypes.LendingBulkChargeVoided, run.PublicId, new
            {
                run.Number, reason = run.VoidReason, voided = run.VoidedCount, remaining = run.CreatedCount,
            }, ct);
            return AvanceDeCorrida.Terminada;
        }

        var contable = await fechaContable.CalcularAsync(Reloj.HoyLocal, ct);
        if (contable.IsFailure) throw new InvalidOperationException($"{contable.Error.Code}: {contable.Error.Message}");
        var modo = await emision.ModoDeCargoMasivoAsync(CargoMasivoAnuladoV1.Type, "CMA", contable.Value, alcance, ct);
        if (modo.IsFailure) throw new InvalidOperationException($"{modo.Error.Code}: {modo.Error.Message}");
        var usuarioCentral = await Db.LendingProcessRuns.AsNoTracking().Where(r => r.Id == corrida.Id).Select(r => r.RequestedByCentralUserId).FirstAsync(ct);
        var limite = Reloj.UtcNow + presupuesto;
        foreach (var id in ids)
        {
            await UnoAsync(corrida, run, id,
                linea => anulacion.AnularAsync(run, linea, run.VoidReason ?? "Anulación de la corrida", run.VoidedByUserId, usuarioCentral, modo.Value, ct),
                PasoDeAnulacion, ct);
            corrida.CursorLoanId = id;
            corrida.ProcessedLoans += 1;
            await Db.SaveChangesAsync(ct);
            if (Reloj.UtcNow >= limite) break;
        }
        return AvanceDeCorrida.Pendiente;
    }
}
