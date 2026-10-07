using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Lending.Processes;

/// <summary>Qué hizo el conductor en una cooperativa. (nuevo)</summary>
public enum ResultadoDeConduccion
{
    /// <summary>Cartera no está activada (<c>LND_Setup.ActivationDate</c> nulo): no se toca nada (T85).</summary>
    SinActivar = 1,

    /// <summary>Recorrió las corridas pendientes hasta agotarlas o agotar el presupuesto.</summary>
    Conducida = 2,

    /// <summary>Una corrida falló: quedó <c>Failed</c> con su error y la pasada sigue con la cooperativa siguiente.</summary>
    ConFalla = 3,
}

/// <summary>
/// Conduce las corridas de Cartera de una cooperativa dentro de un presupuesto (feature 013, T5, PM-13, C59, T0226; data-model §22.1).
/// Corre dentro de <c>IEjecutorEnCooperativa</c> (lo llama <see cref="PasadaDeCartera"/>), con la base y el actor del proceso de esa
/// cooperativa, y usa sólo el reloj de <see cref="IDateTimeService"/> (T9).
/// <list type="number">
/// <item>salta la cooperativa si Cartera no está activada;</item>
/// <item>toma las corridas <c>Running</c> y <c>Requested</c> de las cuatro clases de C59, en el orden de §22.1: la que está
/// corriendo, la corrección por valor nacional, el ciclo diario y el cierre o su reapertura; dentro de cada clase, la más antigua;</item>
/// <item>una clase sin <see cref="IEjecutorDeCorridaDeCartera"/> registrado deja la corrida <c>Requested</c> con advertencia;</item>
/// <item>con ejecutor, la pasa a <c>Running</c> y la conduce tanda por tanda hasta que termina (<c>Completed</c> o
/// <c>CompletedWithErrors</c>) o se agota el presupuesto (sigue <c>Running</c>);</item>
/// <item>una excepción del ejecutor descarta lo de la tanda, deja la corrida <c>Failed</c> con su fila en <c>LND_ProcessRunErrors</c>
/// y corta la cooperativa: la pasada sigue con la siguiente.</item>
/// </list>
/// Es un esqueleto: los ejecutores los agregan US4 (<c>DailyCycle</c>, <c>NationalValueCorrection</c>) y US9 (<c>MonthlyClose</c>,
/// <c>CloseReopen</c>). (nuevo)
/// </summary>
public sealed class ConductorDeCorridasDeCartera(
    IApplicationDbContext db,
    IEnumerable<IEjecutorDeCorridaDeCartera> ejecutores,
    IDateTimeService reloj,
    ILogger<ConductorDeCorridasDeCartera> logger)
{
    /// <summary>Las cuatro clases que conduce el procesador, en su orden de prioridad después de la que ya corre (§22.1, C59).</summary>
    public static IReadOnlyList<LendingProcessKind> Clases { get; } =
    [
        LendingProcessKind.NationalValueCorrection,
        LendingProcessKind.DailyCycle,
        LendingProcessKind.MonthlyClose,
        LendingProcessKind.CloseReopen,
    ];

    /// <summary>El paso que se anota en el error de una corrida que falló entera.</summary>
    public const string PasoDeLaCorrida = "Corrida";

    private const int LargoDelMensaje = 1000;

    private readonly IReadOnlyList<IEjecutorDeCorridaDeCartera> _ejecutores = [.. ejecutores];

    public async Task<ResultadoDeConduccion> ConducirAsync(TimeSpan presupuesto, CancellationToken ct)
    {
        var activada = await db.LendingSetups.AsNoTracking().AnyAsync(s => s.ActivationDate != null, ct);
        if (!activada) return ResultadoDeConduccion.SinActivar;

        var limite = reloj.UtcNow + presupuesto;
        var corridas = (await db.LendingProcessRuns
                .Where(r => r.Status == LendingProcessRunStatus.Running || r.Status == LendingProcessRunStatus.Requested)
                .ToListAsync(ct))
            .Where(r => Clases.Contains(r.Kind))
            .OrderBy(r => r.Status == LendingProcessRunStatus.Running ? 0 : 1)
            .ThenBy(r => Clases.ToList().IndexOf(r.Kind))
            .ThenBy(r => r.RequestedAt)
            .ToList();

        foreach (var corrida in corridas)
        {
            if (reloj.UtcNow >= limite) break;

            var ejecutor = _ejecutores.FirstOrDefault(e => e.Clase == corrida.Kind);
            if (ejecutor is null)
            {
                logger.LogWarning("[Cartera.CorridaSinEjecutor] La corrida {Corrida} de clase {Clase} queda {Estado}: su ejecutor llega con su entrega.",
                    corrida.PublicId, corrida.Kind, corrida.Status);
                continue;
            }

            var id = corrida.Id;
            try
            {
                await ConducirUnaAsync(corrida, ejecutor, limite, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await MarcarFallidaAsync(id, ex, ct);
                return ResultadoDeConduccion.ConFalla;
            }
        }
        return ResultadoDeConduccion.Conducida;
    }

    private async Task ConducirUnaAsync(LendingProcessRun corrida, IEjecutorDeCorridaDeCartera ejecutor, DateTime limite, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        corrida.Status = LendingProcessRunStatus.Running;
        corrida.StartedAt ??= ahora;
        corrida.LastHeartbeatAt = ahora;
        await db.SaveChangesAsync(ct);

        while (reloj.UtcNow < limite)
        {
            var restante = limite - reloj.UtcNow;
            var avance = await ejecutor.EjecutarTandaAsync(corrida, restante, ct);
            corrida.BatchCount += 1;
            corrida.LastHeartbeatAt = reloj.UtcNow;
            if (avance == AvanceDeCorrida.Terminada)
            {
                corrida.Status = corrida.FailedLoans > 0 ? LendingProcessRunStatus.CompletedWithErrors : LendingProcessRunStatus.Completed;
                corrida.FinishedAt = reloj.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task MarcarFallidaAsync(int corridaId, Exception ex, CancellationToken ct)
    {
        // Lo que la tanda dejó a medias en el seguimiento no se guarda: la corrida se relee y sólo se marca su falla.
        db.DescartarCambios();
        var corrida = await db.LendingProcessRuns.FirstAsync(r => r.Id == corridaId, ct);
        var ahora = reloj.UtcNow;
        corrida.Status = LendingProcessRunStatus.Failed;
        corrida.FinishedAt = ahora;
        corrida.LastHeartbeatAt = ahora;
        var mensaje = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
        db.LendingProcessRunErrors.Add(new LendingProcessRunError
        {
            ProcessRunId = corrida.Id,
            LoanId = null,
            BatchNumber = corrida.BatchCount + 1,
            AttemptNumber = corrida.RetryCount + 1,
            Step = PasoDeLaCorrida,
            ErrorCode = ex.GetType().Name,
            ErrorMessage = mensaje.Length > LargoDelMensaje ? mensaje[..LargoDelMensaje] : mensaje,
            OccurredAt = ahora,
        });
        await db.SaveChangesAsync(ct);
        logger.LogError(ex, "[Cartera.CorridaFallida] La corrida {Corrida} de clase {Clase} falló; queda Failed con su error.",
            corrida.PublicId, corrida.Kind);
    }
}
