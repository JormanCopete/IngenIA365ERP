using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>Una cuenta que un subpaso no pudo procesar: queda en <c>LND_ProcessRunErrors</c> con <c>MemberAccountId</c>. (nuevo)</summary>
public sealed record FalloDeCuenta(int MemberAccountId, string Codigo, string Mensaje, string? DetalleJson = null);

/// <summary>
/// Un subpaso del paso de captaciones del ciclo diario (feature 014, T051; R10): causación, liquidación, cuotas, vencimientos y gracia, prórrogas,
/// contractual, inactividad, cargos periódicos y mayoría de edad. Cada uno lo registra la historia que lo trae (K7 a K10); procesa una tanda de cuentas
/// —con su propia transacción y su propio guardado— y devuelve las que fallaron, que el paso anota. (nuevo)
/// </summary>
public interface ISubpasoDeCaptaciones
{
    /// <summary>El nombre del subpaso, para el registro.</summary>
    string Nombre { get; }

    Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct);
}

/// <summary>Lo que hizo el paso de captaciones en una corrida: si corrió, cuántas cuentas, tandas y fallidas. (nuevo)</summary>
public sealed record ResultadoDelPasoDeCaptaciones(bool Corrio, int Cuentas, int Tandas, int Fallidas)
{
    public static ResultadoDelPasoDeCaptaciones NoCorrio { get; } = new(false, 0, 0, 0);
}

/// <summary>
/// El paso «captaciones» del ciclo diario de la Cartera (feature 014, T051; research R10, FR-031): corre <b>después de la causación de los créditos</b>,
/// al terminar la corrida <c>DailyCycle</c>, y <b>sólo</b> en cooperativas con aportes o captaciones activados con fecha ≤ d
/// (<see cref="ActivacionesDeCaptacion"/>). Recorre las cuentas vivas de las naturalezas activadas en tandas de <c>Lending:Process:BatchSize</c>, en orden
/// de <c>Id</c>, y entrega cada tanda a los <see cref="ISubpasoDeCaptaciones"/> registrados; lo que falla queda en <c>LND_ProcessRunErrors</c> con
/// <c>MemberAccountId</c> y el paso <c>Captaciones</c>, sin detener la corrida. En la fase fundacional no hay subpasos: el paso existe vacío y cada
/// historia suma el suyo (causación y liquidación en K8, CDAT y contractual en K9…). (nuevo)
/// </summary>
public sealed class PasoDeCaptaciones(
    IApplicationDbContext db,
    ParametrosDeLaTandaDeCartera tanda,
    IEnumerable<ISubpasoDeCaptaciones> subpasos,
    ILogger<PasoDeCaptaciones>? logger = null)
{
    private const int LargoDelMensaje = 1000;

    private readonly IReadOnlyList<ISubpasoDeCaptaciones> _subpasos = subpasos.ToList();
    private readonly ILogger _logger = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>Las cuentas que recorre el paso: las de las naturalezas que rigen, salvo las cerradas, después del cursor.</summary>
    public IQueryable<int> Cuentas(IReadOnlyList<LineNature> naturalezas, int cursor)
    {
        var lista = naturalezas.ToArray();
        return db.MemberAccounts.AsNoTracking()
            .Where(a => lista.Contains(a.Nature) && a.Status != MemberAccountStatus.Closed && a.Id > cursor)
            .OrderBy(a => a.Id)
            .Select(a => a.Id);
    }

    public async Task<ResultadoDelPasoDeCaptaciones> EjecutarAsync(LendingProcessRun corrida, DateOnly d, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        var naturalezas = (await ActivacionesDeCaptacion.LeerAsync(db, ct)).DeCaptacionEn(d);
        if (naturalezas.Count == 0) return ResultadoDelPasoDeCaptaciones.NoCorrio;

        corrida.CurrentStep = PasosDelCiclo.Captaciones;
        var cursor = 0;
        int cuentas = 0, tandas = 0, fallidas = 0;
        while (true)
        {
            var ids = await Cuentas(naturalezas, cursor).Take(Math.Max(1, tanda.TamanoDeTanda)).ToListAsync(ct);
            if (ids.Count == 0) break;
            tandas++;
            cuentas += ids.Count;

            var fallos = new List<FalloDeCuenta>();
            foreach (var subpaso in _subpasos)
            {
                try
                {
                    fallos.AddRange(await subpaso.ProcesarAsync(corrida, d, ids, ct));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // El subpaso entero falló en la tanda: cada cuenta queda con el error y la corrida sigue con la tanda siguiente.
                    _logger.LogWarning(ex, "[Cartera.CaptacionesTandaFallida] El subpaso {Subpaso} falló en la tanda {Tanda} del {Fecha}.", subpaso.Nombre,
                        tandas, d);
                    fallos.AddRange(ids.Select(id => new FalloDeCuenta(id, ex.GetType().Name, ex.Message)));
                }
            }

            // Una fila por cuenta y corrida (UK_LND_ProcessRunErrors_Run_Account_Attempt): el primer fallo de cada cuenta.
            foreach (var fallo in fallos.GroupBy(f => f.MemberAccountId).Select(g => g.First()))
            {
                db.LendingProcessRunErrors.Add(Error(corrida, fallo));
                fallidas++;
            }
            if (fallos.Count > 0) await db.SaveChangesAsync(ct);
            cursor = ids[^1];
        }
        return new ResultadoDelPasoDeCaptaciones(true, cuentas, tandas, fallidas);
    }

    private static LendingProcessRunError Error(LendingProcessRun corrida, FalloDeCuenta fallo)
    {
        var texto = string.IsNullOrWhiteSpace(fallo.Mensaje) ? fallo.Codigo : fallo.Mensaje;
        return new LendingProcessRunError
        {
            ProcessRunId = corrida.Id,
            MemberAccountId = fallo.MemberAccountId,
            BatchNumber = corrida.BatchCount + 1,
            AttemptNumber = 1 + corrida.RetryCount * 2,
            Step = PasosDelCiclo.Nombre(PasosDelCiclo.Captaciones),
            ErrorCode = fallo.Codigo.Length > 80 ? fallo.Codigo[..80] : fallo.Codigo,
            ErrorMessage = texto.Length > LargoDelMensaje ? texto[..LargoDelMensaje] : texto,
            DetailJson = fallo.DetalleJson,
            OccurredAt = corrida.LastHeartbeatAt ?? corrida.InformationCutoffAt ?? corrida.RequestedAt,
        };
    }
}
