using System.Globalization;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>
/// Lo que dejó en la activación la opción «el período en curso al corte se causa completo» (2026-11-24, feature 025, fase 4): los créditos a los que sumó
/// interés y cuánto, los que quedaron marcados (con o sin interés que sumar: no entran al recálculo de la carga) y los que no se pudieron reconstruir
/// (quedan como hoy, sin marca). (nuevo)
/// </summary>
public sealed record ResultadoDelPeriodoCompleto(int Creditos, decimal Interes, int Marcados, int SinReconstruir)
{
    public static ResultadoDelPeriodoCompleto Vacio { get; } = new(0, 0m, 0, 0);
}

/// <summary>
/// La carga causa completo el período en curso (2026-11-24, feature 025, fase 4, HU4; FR-014, FR-015, D5). Con la opción de la carga
/// (<see cref="MigrationLoad.CauseFullInProgressPeriod"/>), después de que nacen los créditos y de <c>CarteraActivada</c>, por cada crédito vivo de la
/// carga:
/// <list type="bullet">
/// <item>lo reconstruye (<see cref="CargadorDeReconstruccion"/>) y calcula con la misma regla del recálculo de la carga (<see cref="AjusteDelCorteDeCarga.Calcular"/>,
/// fase 3): el interés del período completo de la primera cuota posterior al corte —del vencimiento anterior o del desembolso de la fila de la carga—
/// menos lo que el motor causa desde el corte hasta su vencimiento y lo que la cuota trajo en la carga (que cuenta como causado). Lo que falta, si pasa
/// de un peso, es el ajuste; la cuota destino es esa misma cuota (vence después del corte: el día de la activación todavía no está vencida);</item>
/// <item>el ajuste entra el <b>día de la activación</b>, ingreso de ese día: la fila <c>CutoverInterestAdjustment</c> por <see cref="RegistroDeCartera.RegistrarAjusteDelCorte"/>
/// con su hecho <c>InteresDelCorte</c> (IDC) por <see cref="PartesDeHechosDeCartera"/>, de una corrida <c>CutoverRecalculation</c> ya terminada, y una
/// versión nueva del plan con el ajuste en el interés de esa cuota (el capital no cambia), como el recálculo de la fase 3. La reconstrucción lo lee como
/// entrada (<c>AjustesDelCorte</c>), así que la causación y la verificación de la noche no lo revierten. <b>No</b> se cuadra contra el balance al corte:
/// es ingreso de la activación;</item>
/// <item>marca el crédito (<c>LND_LoanControls.CutoverAdjustedAt</c>) aunque no haya nada que sumar: un crédito nacido con la opción no entra al recálculo de
/// la carga (la HU1 y la HU4 no se suman).</item>
/// </list>
/// Quedan como hoy, sin ajuste y sin marca, los créditos en UVR, de pago único, con la causación suspendida al corte o con suspenso cargado (lo de antes del corte no es ingreso de
/// un crédito en suspenso) y los que no se pudieron reconstruir. Sin la opción no hace nada: el interés nace desde el corte (D5 de la 013). La validación
/// previa contable (<c>Contabilidad.FaltaDeRegla = Bloquear</c>) puede impedir la activación si la matriz no tiene el hecho <c>InteresDelCorte</c>. (nuevo)
/// </summary>
public sealed class PeriodoCompletoDeLaCarga(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    PartesDeHechosDeCartera partes,
    RegistroDeCartera registro,
    FechaContable fechaContable,
    IDateTimeService reloj,
    ValidacionPreviaDeProceso? validacion = null)
{
    /// <summary>Créditos que se reconstruyen a la vez.</summary>
    private const int Trozo = 200;

    /// <summary>El código del bloqueo contable de la activación con la opción.</summary>
    public const string CodigoBloqueado = "Lending.Activation.FullPeriodNotPostable";

    public async Task<Result<ResultadoDelPeriodoCompleto>> CausarAsync(MigrationLoad carga, DateOnly activacion, Actor actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        ArgumentNullException.ThrowIfNull(actor);
        if (!carga.CauseFullInProgressPeriod || carga.Nature != LineNature.Placement) return Result.Success(ResultadoDelPeriodoCompleto.Vacio);
        var corte = carga.CutoffDate;
        var candidatos = await db.Loans.AsNoTracking()
            .Where(l => l.MigrationLoadId == carga.Id && l.Origin == LoanOrigin.Migration && l.Status == LoanStatus.Active && !l.IsDeleted)
            .OrderBy(l => l.Id).Select(l => new { l.Id, l.Number }).ToListAsync(ct);
        if (candidatos.Count == 0) return Result.Success(ResultadoDelPeriodoCompleto.Vacio);
        var contable = await fechaContable.CalcularAsync(activacion, ct);
        if (contable.IsFailure) return Result.Failure<ResultadoDelPeriodoCompleto>(contable.Error);

        // El desembolso original de cada crédito (el de su fila en la carga): es el inicio del período de la cuota 1. El crédito nace con el corte.
        var desembolsos = (await (from fila in db.MigrationLoanRows.AsNoTracking()
                                  join parte in db.MigrationLoadParts.AsNoTracking() on fila.MigrationLoadPartId equals parte.Id
                                  where parte.MigrationLoadId == carga.Id && parte.Status == MigrationPartStatus.Imported && !parte.IsDeleted
                                  select new { fila.SourceLoanNumber, fila.DisbursementDate }).ToListAsync(ct))
            .GroupBy(x => x.SourceLoanNumber, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Min(x => x.DisbursementDate), StringComparer.Ordinal);
        var usuario = actor.CentralUserId is { } central ? new UserRefV1 { CentralUserId = central, Name = actor.Name } : null;
        var ahora = reloj.UtcNow;
        LendingProcessRun? corrida = null;
        var tanda = 0;
        int creditos = 0, marcados = 0, sinReconstruir = 0;
        var interes = 0m;

        foreach (var trozo in candidatos.Chunk(Trozo))
        {
            var ids = trozo.Select(c => c.Id).ToList();
            var cargados = await cargador.CargarAsync(ids, null, activacion, ct);
            var controles = await db.LoanControls.Where(c => ids.Contains(c.LoanId)).ToDictionaryAsync(c => c.LoanId, ct);
            var conAjuste = new List<(CreditoCargado Cargado, PropuestaDeAjusteDelCorte Propuesta)>();
            foreach (var c in trozo)
            {
                if (!cargados.TryGetValue(c.Id, out var cargado) || !cargado.Listo || !controles.TryGetValue(c.Id, out var control))
                {
                    sinReconstruir++;
                    continue;
                }
                var entrada = cargado.Entrada!;
                if (entrada.Uvr is not null || entrada.Condiciones.Periodicidad == RepaymentPeriodicity.SinglePayment) continue;
                if (MotorDeCartera.Reconstruir(entrada, corte).Suspendida || ConSuspensoCargado(entrada)) continue;
                var propuesta = AjusteDelCorteDeCarga.Calcular(entrada, corte, corte, desembolsos.TryGetValue(c.Number, out var d) ? d : null);
                if (propuesta is null) continue;
                control.CutoverAdjustedAt = ahora;
                marcados++;
                if (propuesta.Ajuste > 0m) conAjuste.Add((cargado, propuesta));
            }

            if (conAjuste.Count > 0)
            {
                corrida ??= await CorridaAsync(carga, activacion, actor, ahora, candidatos.Count, ct);
                tanda++;
                var datos = await partes.DatosAsync(conAjuste.Select(x => x.Cargado), ct);
                var origen = new OrigenDePartes(AccountingFactPartSource.CutoverRecalculation, corrida, null, new Dictionary<DateOnly, int> { [activacion] = tanda },
                    usuario) { FechaDeRegistro = activacion };
                var idc = await partes.MasivasAsync(origen, conAjuste.Select(x => new LineaDeCausacion(datos[x.Cargado.Credito.Id], activacion,
                    [new MovimientoDeLibro(activacion, LoanTransactionKind.CutoverInterestAdjustment, LoanConcept.CurrentInterest, Destino(x.Propuesta),
                        x.Propuesta.Ajuste)])).ToList(), InteresDelCorteV1.Type, ct: ct);
                if (idc.IsFailure) return Result.Failure<ResultadoDelPeriodoCompleto>(idc.Error);
                if (validacion is not null && await validacion.BloqueoAsync(ct) is { } bloqueo)
                    return Result.Failure<ResultadoDelPeriodoCompleto>(new Error(CodigoBloqueado,
                        $"Contabilidad no puede registrar el interés del período en curso al corte: {bloqueo.Bloqueo?.Message ?? "falta la regla del hecho InteresDelCorte en la matriz de Cartera."} "
                        + "Complete la matriz o cree la carga sin la opción «el período en curso al corte se causa completo»."));

                var ids2 = conAjuste.Select(x => x.Cargado.Credito.Id).ToList();
                var aperturas = (await db.LoanOperations.Where(o => ids2.Contains(o.LoanId) && o.Kind == LoanOperationKind.OpeningBalance).ToListAsync(ct))
                    .GroupBy(o => o.LoanId).ToDictionary(g => g.Key, g => g.OrderBy(o => o.ValueDate).ThenBy(o => o.Sequence).First());
                var versiones = await db.RepaymentScheduleVersions.AsNoTracking().Where(v => ids2.Contains(v.LoanId))
                    .GroupBy(v => v.LoanId).Select(g => new { LoanId = g.Key, Ultima = g.Max(v => v.VersionNumber) }).ToDictionaryAsync(x => x.LoanId, x => x.Ultima, ct);
                foreach (var (cargado, p) in conAjuste)
                {
                    var loanId = cargado.Credito.Id;
                    var destino = Destino(p);
                    registro.RegistrarAjusteDelCorte(loanId, destino, p.Ajuste, activacion, corrida.Id, contable.Value, ahora,
                        idc.Value.Mensaje(loanId, activacion, LoanConcept.CurrentInterest));
                    // El plan nuevo: el de la reconstrucción con el ajuste en el interés de la cuota (el capital no cambia), vigente desde la activación.
                    var entrada = cargado.Entrada! with
                    {
                        AjustesDelCorte = [.. cargado.Entrada!.AjustesDelCorte, new AjusteDelCorte(activacion, destino, p.Ajuste, ahora)],
                    };
                    if (MotorDeCartera.Reconstruir(entrada, activacion).Plan is { } plan && aperturas.TryGetValue(loanId, out var apertura))
                        registro.RegistrarPlanRegenerado(loanId, versiones.GetValueOrDefault(loanId) + 1, cargado.Vigentes, apertura, plan, entrada.Festivos, ahora,
                            new Dictionary<int, decimal> { [destino] = p.Ajuste }, activacion);
                    creditos++;
                    interes += p.Ajuste;
                }
            }
            await db.SaveChangesAsync(ct);
        }

        if (corrida is not null)
        {
            corrida.ProcessedLoans = creditos;
            corrida.TotalLoans = creditos;
            corrida.BatchCount = tanda;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success(new ResultadoDelPeriodoCompleto(creditos, interes, marcados, sinReconstruir));
    }

    /// <summary>
    /// ¿La carga trajo intereses en suspenso? El sistema anterior ya había suspendido la causación: lo de antes del corte no es ingreso aunque la altura al
    /// corte no alcance todavía el umbral (la vista previa aplica la misma regla).
    /// </summary>
    private static bool ConSuspensoCargado(EntradaDeReconstruccion entrada) =>
        entrada.Operaciones.Where(o => o.Clase == LoanOperationKind.OpeningBalance).SelectMany(o => o.Saldos)
            .Any(s => s.Monto > 0m && s.Concepto is LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense);

    /// <summary>La cuota donde se cobra: la primera sin vencer al corte (la misma cuota del corte).</summary>
    private static int Destino(PropuestaDeAjusteDelCorte p) => p.CuotaDestino ?? p.CuotaDelCorte;

    /// <summary>La corrida que escribe los ajustes, ya terminada, a nombre de quien activa.</summary>
    private async Task<LendingProcessRun> CorridaAsync(MigrationLoad carga, DateOnly activacion, Actor actor, DateTime ahora, int total, CancellationToken ct)
    {
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.CutoverRecalculation, ProcessDate = activacion, Status = LendingProcessRunStatus.Completed,
            RequestedByKind = actor.Kind, RequestedByUserId = actor.UserId, RequestedByCentralUserId = actor.CentralUserId, RequestedByName = actor.Name,
            ExecutionOrigin = string.Create(CultureInfo.InvariantCulture, $"Activación de Cartera (carga {carga.Number})"),
            Reason = "El período en curso al corte se causa completo (opción de la carga)", InformationCutoffAt = ahora, RequestedAt = ahora,
            StartedAt = ahora, FinishedAt = ahora, AffectedFrom = activacion, AffectedTo = activacion, TotalLoans = total, ProcessedLoans = 0, BatchCount = 0,
        };
        db.LendingProcessRuns.Add(corrida);
        await db.SaveChangesAsync(ct);
        return corrida;
    }
}
