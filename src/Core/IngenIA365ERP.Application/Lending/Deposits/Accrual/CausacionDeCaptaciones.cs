using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>Lo que dejó la causación de una cuenta: los días que causó, lo causado, el ajuste y si estaba atrasada. (nuevo)</summary>
public sealed record CausacionDeCuenta(int CuentaId, int Dias, decimal Causado, decimal Ajuste, bool Atrasada);

/// <summary>
/// El subpaso «causación» del paso de captaciones (feature 014, US5, T131; research R04, R05, R10; FR-060, FR-062, FR-065; US5-5): causa cada día el interés
/// de cada cuenta de captación que gana interés —a la vista, permanente y contractual; el CDAT por su plan— por <b>diferencia de acumulados</b> sobre
/// la ventana del período de liquidación (<see cref="LectorDeInteres"/>): lo causado el día d es <c>Seis(A(d)) − Seis(A(d − 1))</c>, así el libro nunca
/// deriva. Si lo que ya está en el libro de la ventana no coincide con lo que el motor recalcula hasta la víspera (una fecha valor que nadie ajustó, una
/// versión registrada tarde), escribe primero el ajuste del proceso (<c>AccrualAdjustment</c>, hecho <c>AjusteDeCausacionDeCaptacion</c>, causa
/// <c>RateCorrection</c>). Las cuentas atrasadas se ponen al día <b>en orden</b> desde el último día causado y se avisa al jefe de captaciones
/// (<c>Cartera.CausacionDeCaptacionesAtrasada</c>). Con el titular fallecido o la cuenta embargada, si la línea lo dice, la causación queda suspendida desde
/// ese día (FR-065) y la marca se anota en la cuenta. La causación de la tanda va en bloque (<see cref="RegistroDeCuentas.CausarEnBloque"/>) con sus partes
/// <c>CCA-</c> por día y sucursal (<see cref="PartesDeHechosDeCaptaciones"/>), en una transacción con el cerrojo del proceso (salta las cuentas ocupadas y
/// las deja para la pasada siguiente). Lo que falla en una cuenta queda en <c>LND_ProcessRunErrors</c> sin detener la tanda. (nuevo)
/// </summary>
public sealed class CausacionDeCaptaciones(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    LectorDeInteres lector,
    PartesDeHechosDeCaptaciones partes,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    ICerrojoDeCartera cerrojo,
    IDateTimeService reloj,
    IAlertas? alertas = null,
    ILogger<CausacionDeCaptaciones>? logger = null,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null,
    TermDeposits.LectorDeCdat? lectorDeCdat = null) : ISubpasoDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "Causacion";

    /// <summary>
    /// Las clases que causan: la vista y el permanente por su método de saldo (K8); desde K9, el CDAT por su plan (<see cref="PlanDeCdat"/>; US6, T172) y el
    /// contractual por su método de saldo a la tasa de sus condiciones selladas, sin causar después de su vencimiento (US7, T182).
    /// </summary>
    public static IReadOnlyList<DepositClass> ClasesQueCausan { get; } =
        [DepositClass.Demand, DepositClass.Permanent, DepositClass.TermDeposit, DepositClass.Contractual];

    /// <summary>
    /// Las clases que liquidan sus intereses por períodos de liquidación (US5): la vista, el permanente y el contractual (US7, T182: se abonan al capital cada
    /// período; capitalizar a la misma tasa compuesta no cambia el valor final). El CDAT los paga por su plan.
    /// </summary>
    public static IReadOnlyList<DepositClass> ClasesQueLiquidan { get; } = [DepositClass.Demand, DepositClass.Permanent, DepositClass.Contractual];

    /// <summary>Los estados que causan: activa e inactiva (si la línea causa en la inactividad, lo decide la ventana).</summary>
    public static IReadOnlyList<MemberAccountStatus> EstadosQueCausan { get; } = [MemberAccountStatus.Active, MemberAccountStatus.Inactive];

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var clases = ClasesQueCausan.ToArray();
        var estados = EstadosQueCausan.ToArray();
        var candidatas = await (from a in db.MemberAccounts.AsNoTracking()
                                join c in db.MemberAccountControls.AsNoTracking() on a.Id equals c.MemberAccountId
                                where ids.Contains(a.Id) && a.Nature == LineNature.Deposit && a.DepositClass != null && clases.Contains(a.DepositClass.Value)
                                      && estados.Contains(a.Status) && c.LastAccruedDate < d
                                orderby a.Id
                                select a.Id).ToListAsync(ct);
        if (candidatas.Count == 0) return [];

        // US8 (T142; A8 de la 013, D23): con Contabilidad.FaltaDeRegla = Bloquear la tanda no se causa si su causación no se contabilizaría; las cuentas
        // quedan con el error y la alerta sale una vez por combinación. La pasada siguiente las pone al día desde su marca de agua.
        if (validacionPrevia is not null
            && await validacionPrevia.EnProcesoAsync(candidatas, d, x => [HechosRepresentativosDeCaptaciones.Causacion(x)],
                $"la causación de captaciones del {d:yyyy-MM-dd}", ct) is { } bloqueadas)
            return bloqueadas;

        var fallos = new List<FalloDeCuenta>();
        var atrasadas = 0;
        try
        {
            await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                await cerrojo.TomarSetupAsync(exclusivo: false, ct);
                var tomadas = await cerrojo.BloquearCuentasAsync(candidatas, ModoDeEspera.Saltar, ct);
                if (tomadas.Count == 0) return 0;
                var r = await CausarAsync(tomadas, d, new OrigenDePartesDeCuentas(AccountingFactPartSource.DailyCycleBatch, corrida, null, 0,
                    UsuarioDe(corrida)), fallos, ct);
                atrasadas = r.Count(x => x.Atrasada);
                return r.Count;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.DescartarCambiosSalvo(corrida);
            _log.LogWarning(ex, "[Cartera.CausacionDeCaptacionesFallida] La causación de la tanda del {Dia} falló.", d);
            return candidatas.Select(id => new FalloDeCuenta(id, "Lending.Savings.AccrualFailed", ex.Message)).ToList();
        }

        if (atrasadas > 0 && alertas is not null)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.CausacionDeCaptacionesAtrasada, $"Causación de captaciones atrasada al {d:yyyy-MM-dd}",
                $"{atrasadas} cuenta(s) de captación tenían la causación atrasada más de un día: se pusieron al día en orden desde el último día causado hasta el "
                + $"{d:yyyy-MM-dd}. Revise por qué el proceso diario no corrió.",
                nameof(LendingProcessRun), corrida.PublicId, DedupKey: $"{TiposDeAlerta.CausacionDeCaptacionesAtrasada}:{d:yyyyMMdd}",
                RecipientPermissions: ["Lending.Interest.Settle", "Lending.Processes.Run"], ModuloDeOrigen: "Lending"), ct);
        return fallos;
    }

    /// <summary>
    /// La puesta al día de una cuenta hasta <paramref name="hasta"/> dentro de la transacción y el cerrojo de quien llama (la cancelación, FR-039): las filas
    /// van en la operación que la dispara y sus partes son <c>ALD-{operación}-{n}</c>. No guarda.
    /// </summary>
    public async Task<Result<CausacionDeCuenta>> PonerAlDiaAsync(MemberAccount cuenta, DateOnly hasta, MemberAccountOperation disparadora, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(disparadora);
        var fallos = new List<FalloDeCuenta>();
        var r = await CausarAsync([cuenta.Id], hasta, new OrigenDePartesDeCuentas(AccountingFactPartSource.AccountCatchUp, null, disparadora, 1, null), fallos, ct);
        if (fallos.Count > 0) return Result.Failure<CausacionDeCuenta>(new Error(fallos[0].Codigo, fallos[0].Mensaje));
        return Result.Success(r.FirstOrDefault() ?? new CausacionDeCuenta(cuenta.Id, 0, 0m, 0m, false));
    }

    /// <summary>Causa las cuentas (ya bloqueadas) hasta <paramref name="d"/>, día por día, y emite y escribe en bloque. No guarda si no hay nada.</summary>
    private async Task<IReadOnlyList<CausacionDeCuenta>> CausarAsync(IReadOnlyList<int> ids, DateOnly d, OrigenDePartesDeCuentas origen, List<FalloDeCuenta> fallos,
        CancellationToken ct)
    {
        var cuentas = await db.MemberAccounts.Where(a => ids.Contains(a.Id)).OrderBy(a => a.Id).ToListAsync(ct);
        var controles = await db.MemberAccountControls.Where(c => ids.Contains(c.MemberAccountId)).ToDictionaryAsync(c => c.MemberAccountId, ct);
        var personas = cuentas.Select(c => c.HolderPersonId).Distinct().ToList();
        var fallecidos = (await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id) && p.IsDeceased).Select(p => p.Id).ToListAsync(ct))
            .ToHashSet();
        var ahora = reloj.UtcNow;
        var porDia = new SortedDictionary<DateOnly, (List<LineaDeCausacionDeCuenta> Causado, List<LineaDeCausacionDeCuenta> Ajustes)>();
        var filasPorCuenta = new Dictionary<int, List<(DateOnly Dia, decimal Causado, decimal Ajuste)>>();
        var hechosPorDia = new Dictionary<DateOnly, IReadOnlyDictionary<int, HechoDeCuentaMarcada>>();
        var resultado = new List<CausacionDeCuenta>();

        foreach (var cuenta in cuentas)
        {
            if (!controles.TryGetValue(cuenta.Id, out var control) || control.LastAccruedDate >= d) continue;
            try
            {
                if (cuenta.DepositClass == DepositClass.TermDeposit)
                {
                    if (lectorDeCdat is null) continue;
                    var delCdat = await CausarCdatAsync(cuenta, control, d, fallecidos.Contains(cuenta.HolderPersonId), ct);
                    filasPorCuenta[cuenta.Id] = delCdat.Select(x => (x.Dia, x.Causado, x.Ajuste)).ToList();
                    foreach (var x in delCdat) _delCdat[(cuenta.Id, x.Dia)] = (x.Version, x.Periodo);
                    resultado.Add(new CausacionDeCuenta(cuenta.Id, delCdat.Count, delCdat.Sum(p => p.Causado), delCdat.Sum(p => p.Ajuste),
                        delCdat.Count > 1 && origen.Fuente == AccountingFactPartSource.DailyCycleBatch));
                    continue;
                }
                var versiones = await lector.VersionesAsync(cuenta.CreditLineId, ct);
                var fallecido = fallecidos.Contains(cuenta.HolderPersonId);
                var propias = new List<(DateOnly Dia, decimal Causado, decimal Ajuste)>();
                // Lo de la ventana que ya está en el libro (una lectura por cuenta) más lo que se va agregando en esta pasada.
                var desdeDelLibro = control.LastSettledThrough ?? cuenta.OpenedOn;
                var enElLibro = await LibroCausadoAsync(cuenta.Id, desdeDelLibro, ct);
                for (var x = control.LastAccruedDate.AddDays(1); x <= d; x = x.AddDays(1))
                {
                    var version = LectorDeInteres.VersionDelDia(versiones, cuenta, x, DateTime.MaxValue)
                                  ?? throw new ReglaDeCarteraException(new ProblemaDeCartera("Lending.Savings.LineWithoutVersion", $"La línea de la cuenta {cuenta.Number} no tiene versión vigente el {x:yyyy-MM-dd}."));
                    MarcarSuspension(cuenta, version.Version, x, fallecido);
                    var inicio = LectorDeInteres.InicioDeLaVentana(cuenta, control, version.Version, x);
                    var ventana = await lector.VentanaAsync(cuenta, inicio, x, DateTime.MaxValue, fallecido, ct);
                    if (ventana.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(ventana.Error.Code, ventana.Error.Message));
                    var hastaLaVispera = ventana.Value.Interes.CausadoHasta(x.AddDays(-1));
                    var causadoEnLaVentana = enElLibro.Where(f => f.Dia > inicio && f.Dia < x).Sum(f => f.Monto)
                                             + propias.Where(f => f.Dia > inicio && f.Dia < x).Sum(f => f.Causado + f.Ajuste);
                    var ajuste = hastaLaVispera - causadoEnLaVentana;
                    var delDia = ventana.Value.Interes.Dias.Count == 0 ? 0m : ventana.Value.Interes.Dias[^1].Causado;
                    propias.Add((x, delDia, ajuste));
                }
                filasPorCuenta[cuenta.Id] = propias;
                resultado.Add(new CausacionDeCuenta(cuenta.Id, propias.Count, propias.Sum(p => p.Causado), propias.Sum(p => p.Ajuste),
                    propias.Count > 1 && origen.Fuente == AccountingFactPartSource.DailyCycleBatch));
            }
            catch (ReglaDeCarteraException ex)
            {
                fallos.Add(new FalloDeCuenta(cuenta.Id, ex.Codigo, ex.Problema.Mensaje));
            }
        }

        // Las partes y las filas, día por día (cada día con su fecha contable y su tanda).
        var causaciones = new List<CausacionDeCuentaAEscribir>();
        var ajustes = new List<MemberAccountTransaction>();
        var dias = filasPorCuenta.SelectMany(kv => kv.Value.Select(v => v.Dia)).Distinct().Order().ToList();
        var sinSucursal = new HashSet<int>();
        foreach (var dia in dias)
        {
            var contable = await fechaContable.CalcularAsync(dia, conCierreDeCartera: origen.Fuente != AccountingFactPartSource.DailyCycleBatch, ct);
            if (contable.IsFailure) throw new InvalidOperationException($"{contable.Error.Code}: {contable.Error.Message}");
            var delDia = filasPorCuenta.Where(kv => !sinSucursal.Contains(kv.Key) && kv.Value.Any(v => v.Dia == dia)).Select(kv => kv.Key).ToHashSet();
            var hechos = await emision.HechosDeCuentasAsync(cuentas.Where(c => delDia.Contains(c.Id)).ToList(), contable.Value, ct);
            hechosPorDia[dia] = hechos;
            var lineas = new List<LineaDeCausacionDeCuenta>();
            var lineasDeAjuste = new List<LineaDeCausacionDeCuenta>();
            foreach (var id in delDia.Order())
            {
                if (!hechos.TryGetValue(id, out var hecho))
                {
                    fallos.Add(new FalloDeCuenta(id, Disbursements.ErroresDeDesembolso.CodigoSinSucursalContable,
                        "La oficina de la cuenta no tiene sucursal contable vinculada: no se causa hasta vincularla."));
                    sinSucursal.Add(id);
                    continue;
                }
                var v = filasPorCuenta[id].First(f => f.Dia == dia);
                if (v.Causado != 0m) lineas.Add(new LineaDeCausacionDeCuenta(hecho, dia, v.Causado));
                if (v.Ajuste != 0m) lineasDeAjuste.Add(new LineaDeCausacionDeCuenta(hecho, dia, v.Ajuste));
            }
            porDia[dia] = (lineas, lineasDeAjuste);
        }

        // En el ciclo, una parte por día (cada día es su tanda); en la puesta al día de una operación, una sola con una línea por cuenta y día.
        var grupos = origen.Fuente == AccountingFactPartSource.DailyCycleBatch
            ? porDia.Select(kv => (Dias: (IReadOnlyList<DateOnly>)[kv.Key], kv.Value.Causado, kv.Value.Ajustes)).ToList()
            : porDia.Count == 0
                ? []
                : [((IReadOnlyList<DateOnly>)porDia.Keys.ToList(), porDia.Values.SelectMany(v => v.Causado).ToList(), porDia.Values.SelectMany(v => v.Ajustes).ToList())];
        foreach (var (diasDelGrupo, lineas, lineasDeAjuste) in grupos)
        {
            var tanda = origen;
            if (origen.Corrida is { } corrida && origen.Fuente == AccountingFactPartSource.DailyCycleBatch)
            {
                corrida.BatchCount += 1;
                tanda = origen with { Tanda = corrida.BatchCount };
            }
            var emitidas = await partes.MasivasAsync(tanda, InteresDeCaptacionCausadoV1.Type, lineas, ct: ct);
            if (emitidas.IsFailure) throw new InvalidOperationException($"{emitidas.Error.Code}: {emitidas.Error.Message}");
            var deAjuste = lineasDeAjuste.Count == 0
                ? PartesDeCuentasEmitidas.Vacias
                : (await partes.MasivasAsync(tanda, AjusteDeCausacionDeCaptacionV1.Type, lineasDeAjuste, EmisionDeCartera.CausaCorreccionDeTasa, ct)) is { IsSuccess: true } a
                    ? a.Value
                    : throw new InvalidOperationException("La parte del ajuste de la causación no se emitió.");
            foreach (var dia in diasDelGrupo)
            foreach (var (id, propias) in filasPorCuenta.Where(kv => !sinSucursal.Contains(kv.Key)))
            {
                var v = propias.FirstOrDefault(f => f.Dia == dia);
                if (v.Dia != dia) continue;
                var hecho = hechosPorDia[dia][id];
                var contable = hecho.Hecho.FechaContable;
                var cdat = _delCdat.TryGetValue((id, dia), out var delPlan) ? delPlan : default;
                if (v.Ajuste != 0m)
                    ajustes.Add(Fila(id, origen, MemberAccountTransactionKind.AccrualAdjustment, v.Ajuste, dia, contable, ahora, deAjuste.Mensaje(id, dia), cdat.Version,
                        cdat.Periodo));
                var filas = v.Causado == 0m
                    ? []
                    : new List<MemberAccountTransaction>
                    {
                        Fila(id, origen, MemberAccountTransactionKind.InterestAccrued, v.Causado, dia, contable, ahora, emitidas.Value.Mensaje(id, dia), cdat.Version,
                            cdat.Periodo),
                    };
                causaciones.Add(new CausacionDeCuentaAEscribir(controles[id], dia, filas));
            }
        }
        if (ajustes.Count > 0) registro.RegistrarTransacciones(ajustes);
        if (causaciones.Count > 0)
        {
            var resumen = registro.CausarEnBloque(causaciones);
            _log.LogInformation("[Cartera.CausacionDeCaptaciones] {Cuentas} cuenta(s), {Dias} día(s), {Filas} fila(s), {Total} causado.", resumen.Cuentas,
                resumen.Dias, resumen.Filas, resumen.Total);
            await db.SaveChangesAsync(ct);
        }
        return resultado.Where(r => filasPorCuenta.ContainsKey(r.CuentaId) && !sinSucursal.Contains(r.CuentaId)).ToList();
    }

    /// <summary>La versión de condiciones y el período del plan de cada día causado de un CDAT (las filas los nombran, research E16).</summary>
    private readonly Dictionary<(int Cuenta, DateOnly Dia), (int? Version, int? Periodo)> _delCdat = [];

    /// <summary>Un día causado de un CDAT: lo del día, el ajuste previo y la versión y el período del plan a los que pertenece.</summary>
    private sealed record DiaDeCdat(DateOnly Dia, decimal Causado, decimal Ajuste, int? Version, int? Periodo);

    /// <summary>
    /// US6 (T172; research R11): la causación de un CDAT por su plan. Cada día de (marca, d] cae en la versión de condiciones cuyo plazo (inicio, vencimiento]
    /// lo contiene y en su período: lo causado es <see cref="PlanDeCdat.CausadoDelDia"/> (el último día del período absorbe el redondeo contra el plan) y, si
    /// lo del libro del período hasta la víspera no es lo que el plan dice (una prórroga que empieza en el vencimiento con la gracia que causa), primero el
    /// ajuste. Fuera de toda versión (la gracia que no causa, el vencido, antes del fondeo) o con la causación suspendida (FR-065), cero.
    /// </summary>
    private async Task<IReadOnlyList<DiaDeCdat>> CausarCdatAsync(MemberAccount cuenta, MemberAccountControl control, DateOnly d, bool fallecido, CancellationToken ct)
    {
        var leido = await lectorDeCdat!.LeerAsync(cuenta, ct);
        var planes = new Dictionary<int, PlanDeCdatCalculado>();
        foreach (var v in leido.Versiones) planes[v.VersionNumber] = await lectorDeCdat.PlanAsync(v, ct);
        var lineas = await lector.VersionesAsync(cuenta.CreditLineId, ct);
        var clases = LectorDeInteres.ClasesDeLoCausado.ToArray();
        var enElLibro = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuenta.Id && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind) && !t.IsDeleted)
            .Select(t => new { t.ValueDate, t.Amount }).ToListAsync(ct);
        var dias = new List<DiaDeCdat>();
        for (var x = control.LastAccruedDate.AddDays(1); x <= d; x = x.AddDays(1))
        {
            var dia = x;
            var version = leido.Versiones.Where(v => v.StartDate is { } i && v.MaturityDate is { } m && dia > i && dia <= m).MaxBy(v => v.VersionNumber);
            if (version is null)
            {
                dias.Add(new DiaDeCdat(x, 0m, 0m, null, null));
                continue;
            }
            var linea = lineas.FirstOrDefault(l => l.Version.Id == version.DepositLineVersionId)?.Version;
            if (linea is not null) MarcarSuspension(cuenta, linea, x, fallecido);
            var plan = planes[version.VersionNumber];
            var periodo = plan.PeriodoDe(x)!;
            if (cuenta.SuspendidaEl(x))
            {
                dias.Add(new DiaDeCdat(x, 0m, 0m, version.Id, periodo.Numero));
                continue;
            }
            var esperado = x.AddDays(-1) > periodo.Desde ? PlanDeCdat.CausadoAcumulado(plan, x.AddDays(-1)) : 0m;
            var libro = enElLibro.Where(f => f.ValueDate > periodo.Desde && f.ValueDate < dia).Sum(f => f.Amount)
                        + dias.Where(f => f.Dia > periodo.Desde && f.Dia < dia).Sum(f => f.Causado + f.Ajuste);
            dias.Add(new DiaDeCdat(x, PlanDeCdat.CausadoDelDia(plan, x), esperado - libro, version.Id, periodo.Numero));
        }
        return dias;
    }

    /// <summary>FR-065: la suspensión que la ventana detecta se anota en la cuenta (desde el primer día en que rige), una vez.</summary>
    private static void MarcarSuspension(MemberAccount cuenta, Domain.Entities.Lending.Deposits.Configuration.DepositLineVersion version, DateOnly dia, bool fallecido)
    {
        // US11 (T189): la del embargo total la pone y la levanta RetencionDeEmbargos; aquí sólo la del titular fallecido.
        if (cuenta.AccrualSuspendedSince is not null && cuenta.AccrualResumedOn is null) return;
        if (fallecido && version.SuspendsAccrualOnDeceased)
        {
            cuenta.AccrualSuspendedSince = dia;
            cuenta.AccrualResumedOn = null;
            cuenta.AccrualSuspensionReason = "Deceased";
        }
    }

    /// <summary>Lo causado y ajustado en el libro de la cuenta desde <paramref name="desde"/> (excluido), por día valor.</summary>
    private async Task<List<(DateOnly Dia, decimal Monto)>> LibroCausadoAsync(int cuentaId, DateOnly desde, CancellationToken ct)
    {
        var clases = LectorDeInteres.ClasesDeLoCausado.ToArray();
        var filas = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind) && t.ValueDate > desde && !t.IsDeleted)
            .Select(t => new { t.ValueDate, t.Amount }).ToListAsync(ct);
        return filas.Select(f => (f.ValueDate, f.Amount)).ToList();
    }

    private static MemberAccountTransaction Fila(int cuenta, OrigenDePartesDeCuentas origen, MemberAccountTransactionKind clase, decimal monto, DateOnly dia,
        DateOnly contable, DateTime ahora, Guid? mensaje, int? terminos = null, int? periodo = null) => new()
    {
        TermsVersionId = terminos,
        SchedulePeriodNumber = periodo,
        MemberAccountId = cuenta,
        ProcessRunId = origen.Fuente == AccountingFactPartSource.DailyCycleBatch ? origen.Corrida?.Id : null,
        Operation = origen.Fuente == AccountingFactPartSource.AccountCatchUp ? origen.Disparadora : null,
        OperationId = origen.Fuente == AccountingFactPartSource.AccountCatchUp && origen.Disparadora is { Id: > 0 } op ? op.Id : null,
        Kind = clase,
        Concept = MemberAccountConcept.InterestPayable,
        Amount = monto,
        ValueDate = dia,
        AccountingDate = contable,
        RegisteredAt = ahora,
        MessagePublicId = mensaje,
    };

    private static UserRefV1? UsuarioDe(LendingProcessRun corrida) =>
        corrida.RequestedByCentralUserId is { } central ? new UserRefV1 { CentralUserId = central, Name = corrida.RequestedByName ?? string.Empty } : null;
}
