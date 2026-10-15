using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Engine;

/// <summary>
/// Un crédito leído para la reconstrucción (feature 013, US4, T0493): el crédito, sus condiciones pactadas (la versión 1) y las vigentes a R,
/// sus operaciones conocidas en R, la entrada del motor con sus series y, si no se pudo armar, el error (los demás créditos de la tanda
/// siguen). (nuevo)
/// </summary>
public sealed record CreditoCargado(
    Loan Credito,
    LoanTermsVersion Pactadas,
    LoanTermsVersion Vigentes,
    IReadOnlyList<LoanOperation> Operaciones,
    EntradaDeReconstruccion? Entrada,
    SeriesDeModalidad? Series,
    Error? Error)
{
    public bool Listo => Entrada is not null && Error is null;
}

/// <summary>
/// El que arma <see cref="EntradaDeReconstruccion"/> en bloque para 1..N créditos con lo conocido en R (feature 013, US4, T0493; T24, R32): en
/// unas cinco consultas —créditos, condiciones, operaciones, saldos iniciales y plan de los migrados— más las series por modalidad
/// (<see cref="ISeriesRegulatorias"/>, una vez por modalidad). Sólo entran las operaciones con <c>RegisteredAt ≤ R</c> (un reverso registrado
/// después de R no cuenta: el motor vuelve a filtrar) y las versiones de condiciones registradas hasta R.
/// <list type="bullet">
/// <item>Condiciones: las <b>pactadas</b> (versión 1: originación, venta o migración) dan la tasa, la mora, la convención, la equivalencia, el
/// redondeo y el plan del primer tramo; las <b>vigentes</b> a R, las reestructuraciones. La tasa reajustada viaja en su operación
/// <c>RateReset</c> (la de la versión que la operación creó).</item>
/// <item>Plan: el del primer desembolso con las condiciones pactadas (monto y fecha del tramo 1, cuotas, primer vencimiento, extraordinarias,
/// gracia y seguros sellados). Un migrado no lleva plan: lo da su <c>OpeningBalance</c> con las cuotas del plan migrado.</item>
/// <item>Operaciones: todas menos las que no mueven saldos (<c>PreferenceChange</c>); una clase sin intérprete hace fallar el crédito con
/// <c>Lending.Replay.OperationKindNotSupported</c>, a propósito.</item>
/// <item>US5 (bloque 9B): cada pago y cada cobro provisional lleva su <b>decisión</b> (<see cref="Payments.DecisionDePago"/>: modalidad, valor
/// tomado, elección y cuota) y cada resolución de saldo la suya (<see cref="Payments.DecisionDeResolucion"/>); cada reverso, la operación que
/// reversa. La prelación es la sellada en las condiciones vigentes (<c>AllocationOrder</c>, <c>ConceptPriority</c>,
/// <c>SuspenseWithItsConcept</c>, <c>AllowsSpecificInstallmentOutOfOrder</c>) y el castigo, la fecha en que el crédito se castigó.</item>
/// <item>Seguros (decisión de US4): el libro y el motor causan <b>un</b> concepto <c>Insurance</c> por cuota —la suma de los seguros del
/// plan—; el desglose por seguro vive en las condiciones selladas y en el plan, y el hecho <c>SeguroCausado</c> lleva a la aseguradora como
/// contraparte sólo cuando el crédito tiene un único seguro. El momento de causación (<c>InsuranceAccrualTiming</c>) es el del primer
/// seguro de la versión de la línea, por código: una línea no mezcla seguros causados al vencer con seguros diarios.</item>
/// </list>
/// No escribe. (nuevo)
/// </summary>
public sealed class CargadorDeReconstruccion(IApplicationDbContext db, ISeriesRegulatorias series, ReglasDeParametrosDeCartera? parametros = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Las clases de operación que no mueven saldos y la reconstrucción no recibe.</summary>
    public static IReadOnlySet<LoanOperationKind> SinEfectoEnSaldos { get; } = new HashSet<LoanOperationKind> { LoanOperationKind.PreferenceChange };

    /// <summary>
    /// Los créditos de <paramref name="loanIds"/> con lo conocido en <paramref name="informacionHasta"/> (nulo = todo) y las series hasta
    /// <paramref name="hasta"/>. Los que no existen o no tienen condiciones no vienen.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, CreditoCargado>> CargarAsync(IReadOnlyCollection<int> loanIds, DateTime? informacionHasta, DateOnly hasta,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(loanIds);
        var ids = loanIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, CreditoCargado>();
        var r = informacionHasta ?? DateTime.MaxValue;

        var creditos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        var condiciones = (await db.LoanTermsVersions.AsNoTracking().Where(t => ids.Contains(t.LoanId)).ToListAsync(ct))
            .Where(t => t.RegisteredAt <= r).ToList();
        var operaciones = (await db.LoanOperations.AsNoTracking().Where(o => ids.Contains(o.LoanId)).ToListAsync(ct))
            .Where(o => o.RegisteredAt <= r).ToList();
        var migrados = creditos.Where(c => c.Origin == LoanOrigin.Migration).Select(c => c.Id).ToList();
        var saldosIniciales = migrados.Count == 0
            ? []
            : await db.LoanTransactions.AsNoTracking()
                .Where(t => migrados.Contains(t.LoanId) && t.Kind == LoanTransactionKind.OpeningBalance && t.ReversesTransactionId == null)
                .ToListAsync(ct);
        var planesMigrados = migrados.Count == 0
            ? []
            : await (from v in db.RepaymentScheduleVersions.AsNoTracking()
                     join i in db.RepaymentInstallments.AsNoTracking() on v.Id equals i.ScheduleVersionId
                     where migrados.Contains(v.LoanId) && v.IsMigratedPlan
                     select new { v.LoanId, v.OperationId, i.InstallmentNumber, i.PeriodStart, i.DueDate, i.Principal })
                .ToListAsync(ct);

        var giros = await PendientesDeGiroAsync(ids, operaciones, r, ct);
        var insolvencias = await InsolvenciasAsync(creditos, operaciones, r, ct);
        var fallecimientos = await FallecimientosAsync(creditos, r, informacionHasta, ct);

        var versionesDeLinea = condiciones.Where(t => t.VersionNumber == 1).Select(t => t.CreditLineVersionId).Distinct().ToList();
        var momentosDeSeguro = (await db.CreditLineInsurances.AsNoTracking().Where(s => versionesDeLinea.Contains(s.CreditLineVersionId))
                .Select(s => new { s.CreditLineVersionId, s.Code, s.AccrualTiming }).ToListAsync(ct))
            .GroupBy(s => s.CreditLineVersionId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.Code, StringComparer.Ordinal).First().AccrualTiming);

        var resultado = new Dictionary<int, CreditoCargado>();
        foreach (var credito in creditos)
        {
            var suyas = condiciones.Where(t => t.LoanId == credito.Id).OrderBy(t => t.VersionNumber).ToList();
            if (suyas.Count == 0) continue;
            var pactadas = suyas[0];
            var vigentes = suyas[^1];
            var ops = operaciones.Where(o => o.LoanId == credito.Id).OrderBy(o => o.ValueDate).ThenBy(o => o.Sequence).ToList();
            var origen = ops.Where(o => o.Kind is LoanOperationKind.Disbursement or LoanOperationKind.OpeningBalance)
                .Select(o => (DateOnly?)o.ValueDate).Min() ?? credito.OpenedOn;

            var leidas = await series.LeerAsync(pactadas.ModalityCode, pactadas.SubsegmentCode, origen, hasta, informacionHasta, ct);
            if (leidas.IsFailure)
            {
                resultado[credito.Id] = new CreditoCargado(credito, pactadas, vigentes, ops, null, null, leidas.Error);
                continue;
            }
            try
            {
                var entrada = new EntradaDeReconstruccion(
                    Condiciones(pactadas, AntesDeLasNovedades(suyas), leidas.Value) with
                    {
                        CausacionDeSeguros = momentosDeSeguro.GetValueOrDefault(pactadas.CreditLineVersionId, InsuranceAccrualTiming.AtDueDate),
                        Prelacion = PrelacionSellada(vigentes),
                        CastigoDesde = credito.WrittenOffOn,
                        InsolvenciaDesde = insolvencias.TryGetValue(credito.DebtorPersonId, out var insolvencia) ? insolvencia.Desde : null,
                        InsolvenciaHasta = insolvencias.ContainsKey(credito.DebtorPersonId) ? insolvencia.Hasta : null,
                        FallecimientoDesde = fallecimientos.TryGetValue(credito.Id, out var fallecimiento) ? fallecimiento : null,
                    },
                    credito.Origin == LoanOrigin.Migration ? null : Plan(pactadas, ops),
                    ops.Where(o => !SinEfectoEnSaldos.Contains(o.Kind))
                        .Select(o => Operacion(o, suyas, saldosIniciales.Where(t => t.OperationId == o.Id)
                                .Select(t => new SaldoInicialDeCredito(t.Concept, t.InstallmentNumber, t.Amount)).ToList(),
                            planesMigrados.Where(p => p.LoanId == credito.Id && p.OperationId == o.Id)
                                .OrderBy(p => p.InstallmentNumber)
                                .Select(p => new CuotaMigrada(p.InstallmentNumber, p.DueDate, p.Principal) { Desde = p.PeriodStart }).ToList()))
                        .ToList(),
                    r) { PendientesDeGiro = giros.TryGetValue(credito.Id, out var suyos) ? suyos : [] };
                resultado[credito.Id] = new CreditoCargado(credito, pactadas, vigentes, ops, leidas.Value.Aplicar(entrada), leidas.Value, null);
            }
            catch (ReglaDeCarteraException ex)
            {
                resultado[credito.Id] = new CreditoCargado(credito, pactadas, vigentes, ops, null, leidas.Value, ErroresDeGobierno.De(ex.Problema));
            }
        }
        return resultado;
    }

    /// <summary>
    /// K2 (T0839; T30, FR-114; data-model §24.8): lo descontado por la pagaduría y pendiente de giro de cada crédito, conocido en R —resultados
    /// <c>Deducted</c> o <c>PartiallyDeducted</c> registrados hasta R y no reemplazados por otro registrado hasta R—, con la cuota de su línea, la
    /// fecha del descuento y la ventana y el umbral sellados. Uno que ya giró un pago de las operaciones conocidas no reversado sigue en la entrada
    /// con <c>GiradoDesde</c> (la fecha valor del pago): cubre la cuota hasta la víspera, así girar no cambia lo que el libro ya causó. Una línea sin
    /// cuota (sólo vencido) no entra: la reconstrucción la mide como cualquier saldo vencido.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, IReadOnlyList<CuotaPendienteDeGiro>>> PendientesDeGiroAsync(IReadOnlyCollection<int> ids,
        IReadOnlyList<LoanOperation> operaciones, DateTime r, CancellationToken ct)
    {
        var resultados = (await (from res in db.PayrollDeductionResults.AsNoTracking()
                                 join lin in db.PayrollDeductionFileLines.AsNoTracking() on res.PayrollDeductionFileLineId equals lin.Id
                                 where ids.Contains(res.LoanId) && !res.IsDeleted && res.DeductedAmount > 0m
                                       && (res.Outcome == PayrollResultOutcome.Deducted || res.Outcome == PayrollResultOutcome.PartiallyDeducted)
                                 select new
                                 {
                                     res.Id, res.LoanId, res.DeductedAmount, res.DeductionDate, res.TransferDueDate, res.PaymasterArrearsThresholdDays,
                                     res.SupersedesResultId, res.CreatedAt, lin.InstallmentNumber,
                                 }).ToListAsync(ct))
            .Where(x => x.CreatedAt <= r).ToList();
        if (resultados.Count == 0) return new Dictionary<int, IReadOnlyList<CuotaPendienteDeGiro>>();
        var reemplazados = resultados.Where(x => x.SupersedesResultId is not null).Select(x => x.SupersedesResultId!.Value).ToHashSet();
        var reversados = operaciones.Where(o => o.ReversesOperationId is not null).Select(o => o.ReversesOperationId!.Value).ToHashSet();
        var girados = operaciones.Where(o => o.PayrollDeductionResultId is not null && !reversados.Contains(o.Id))
            .GroupBy(o => o.PayrollDeductionResultId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(o => o.ValueDate).ThenBy(o => o.Sequence).First());
        return resultados
            .Where(x => !reemplazados.Contains(x.Id) && x.InstallmentNumber is not null && x.DeductionDate is not null)
            .GroupBy(x => x.LoanId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CuotaPendienteDeGiro>)g.Select(x => new CuotaPendienteDeGiro(x.InstallmentNumber!.Value,
                x.DeductionDate!.Value, x.TransferDueDate ?? x.DeductionDate!.Value, x.DeductedAmount, x.CreatedAt)
            {
                UmbralDias = x.PaymasterArrearsThresholdDays,
                GiradoDesde = girados.TryGetValue(x.Id, out var girado) ? girado.ValueDate : null,
                GiradoPor = girado is null ? null : Id(girado.Id),
            }).ToList());
    }

    /// <summary>Un crédito (atajo de <see cref="CargarAsync"/>); nulo si no existe o no tiene condiciones.</summary>
    public async Task<CreditoCargado?> CargarAsync(int loanId, DateTime? informacionHasta, DateOnly hasta, CancellationToken ct) =>
        (await CargarAsync([loanId], informacionHasta, hasta, ct)).GetValueOrDefault(loanId);

    /// <summary>
    /// La entrada con una operación todavía sin guardar (la que se está registrando en la misma transacción): entra con su <c>Sequence</c> y
    /// su <c>RegisteredAt</c>, y R se corre hasta ella si hace falta.
    /// </summary>
    public static EntradaDeReconstruccion ConOperacion(EntradaDeReconstruccion entrada, LoanOperation operacion, string? reversa = null)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(operacion);
        var nueva = new EntradaDeOperacion(IdNuevo(operacion), operacion.Kind, operacion.ValueDate, operacion.RegisteredAt, operacion.Sequence)
        {
            Monto = operacion.Kind == LoanOperationKind.Disbursement ? operacion.Amount : null,
            Reversa = reversa ?? (operacion.ReversesOperationId is { } id ? Id(id) : null),
        };
        return entrada with
        {
            Operaciones = [.. entrada.Operaciones, nueva],
            InformacionHasta = entrada.InformacionHasta >= operacion.RegisteredAt ? entrada.InformacionHasta : operacion.RegisteredAt,
        };
    }

    /// <summary>El libro de los créditos hasta <paramref name="hasta"/> (incluido) en la forma que compara el motor (sin las filas en control).</summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<MovimientoDeLibro>>> LibroAsync(IReadOnlyCollection<int> loanIds, DateOnly hasta, CancellationToken ct)
    {
        var ids = loanIds.Distinct().ToList();
        var filas = await db.LoanTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.LoanId) && t.ValueDate <= hasta && !t.InControl)
            .Select(t => new { t.LoanId, t.ValueDate, t.Kind, t.Concept, t.InstallmentNumber, t.Amount, t.OperationId })
            .ToListAsync(ct);
        return ids.ToDictionary(id => id, id => (IReadOnlyList<MovimientoDeLibro>)filas.Where(f => f.LoanId == id)
            .Select(f => new MovimientoDeLibro(f.ValueDate, f.Kind, f.Concept, f.InstallmentNumber, f.Amount)
            {
                OperacionId = f.OperationId is { } o ? Id(o) : null,
            }).ToList());
    }

    /// <summary>
    /// La entrada con una operación de pago o de resolución todavía sin guardar y su contenido (US5, bloque 9B): la que se está aplicando en la
    /// misma transacción, para tomar del libro teórico sus filas y las diferencias que deja en los días ya causados.
    /// </summary>
    public static EntradaDeReconstruccion ConOperacion(EntradaDeReconstruccion entrada, LoanOperation operacion, object contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        var con = ConOperacion(entrada, operacion);
        return con with { Operaciones = [.. con.Operaciones.Take(con.Operaciones.Count - 1), con.Operaciones[^1] with { Contenido = contenido }] };
    }

    /// <summary>El identificador con que entra a la reconstrucción una operación (guardada o todavía sin <c>Id</c>).</summary>
    public static string IdEnLaEntrada(LoanOperation operacion) => IdNuevo(operacion);

    /// <summary>
    /// La prelación sellada en las condiciones (data-model §1.8; US5, T0533): el orden, la lista de conceptos, el suspenso con su concepto y la
    /// cuota específica fuera de orden. Unas condiciones sin orden sellado (anteriores a la prelación) usan la de defecto.
    /// </summary>
    public static Domain.Lending.Payments.Prelacion PrelacionSellada(LoanTermsVersion condiciones)
    {
        ArgumentNullException.ThrowIfNull(condiciones);
        return Enum.IsDefined(condiciones.AllocationOrder)
            ? Domain.Lending.Payments.Prelacion.DesdeSellada(condiciones.AllocationOrder, condiciones.ConceptPriority, condiciones.SuspenseWithItsConcept,
                condiciones.AllowsSpecificInstallmentOutOfOrder)
            : Domain.Lending.Payments.Prelacion.PorDefecto;
    }

    /// <summary>El identificador de una operación guardada en la entrada del motor.</summary>
    public static string Id(long operationId) => operationId.ToString(CultureInfo.InvariantCulture);

    private static string IdNuevo(LoanOperation operacion) =>
        operacion.Id != 0 ? Id(operacion.Id) : $"nueva:{operacion.Sequence.ToString(CultureInfo.InvariantCulture)}";

    // ---------------------------------------------------------------------------------------------------------- mapeos --


    /// <summary>
    /// La última versión de condiciones anterior a la primera que nació de una novedad (K3, T0874); sin novedades, la vigente. Su conteo de
    /// reestructuraciones es el de partida de la reconstrucción: las novedades lo cambian desde su fecha valor.
    /// </summary>
    private static LoanTermsVersion AntesDeLasNovedades(IReadOnlyList<LoanTermsVersion> ordenadas)
    {
        var primera = ordenadas.Select((t, i) => (t, i)).FirstOrDefault(x => x.t.NoveltyId is not null);
        return primera.t is null ? ordenadas[^1] : ordenadas[Math.Max(0, primera.i - 1)];
    }
    private static CondicionesDeCredito Condiciones(LoanTermsVersion p, LoanTermsVersion deLaReconstruccion, SeriesDeModalidad series) =>
        new(p.RulesVersion, p.AnnualRate ?? 0m, p.DefaultRateKind, p.DayCount, p.RateEquivalence, p.Periodicity, Redondeo(p))
        {
            MoraFijaEA = p.DefaultRateKind == DefaultRateKind.Fixed ? p.DefaultRate : null,
            FactorDeMora = p.DefaultRateKind == DefaultRateKind.RelativeToCurrent ? p.DefaultRateFactor : null,
            SobretasaDeCargos = p.ChargesSpread ?? 0m,
            MoraSobreIntereses = p.DefaultOnInterestAllowed,
            Margen = p.Margin ?? 0m,
            Piso = p.RateFloor,
            Techo = p.RateCeiling,
            // K3 (T0874): las de la última versión anterior a la primera novedad (lo migrado, lo heredado por un enlazado o una versión manual);
            // cada novedad las cambia desde su fecha valor (ContenidoDeNovedad.ReestructuracionesDespues).
            Reestructuraciones = deLaReconstruccion.RestructuringCount,
            UsuraRequerida = series.ExigeUsura,
        };

    /// <summary>El redondeo sellado; una unidad que el motor no admite cae al centavo (nunca debió sellarse).</summary>
    public static RedondeoDeCartera Redondeo(LoanTermsVersion t) =>
        RedondeoDeCartera.UnidadesAdmitidas.Contains(t.RoundingUnit) && Enum.IsDefined(t.RoundingMode) && Enum.IsDefined(t.RoundingMoment)
            ? new RedondeoDeCartera(t.RoundingUnit, t.RoundingMode, t.RoundingMoment)
            : RedondeoDeCartera.AlCentavo;

    /// <summary>
    /// El plan pactado del primer tramo con las condiciones selladas; nulo si todavía no hay desembolso. K3 (T0874): el crédito enlazado de una
    /// novación o refinanciación no tiene desembolso: su plan pactado es el de su operación de origen (<c>TermsChange</c> del lado enlazado), con su
    /// monto (lo trasladado más lo capitalizado) y su fecha.
    /// </summary>
    public static EntradaDePlan? Plan(LoanTermsVersion p, IEnumerable<LoanOperation> operaciones)
    {
        var lista = operaciones.ToList();
        var primero = lista.Where(o => o.Kind == LoanOperationKind.Disbursement).OrderBy(o => o.ValueDate).ThenBy(o => o.Sequence).FirstOrDefault()
            ?? lista.Where(EsOrigenDeEnlazado).OrderBy(o => o.ValueDate).ThenBy(o => o.Sequence).FirstOrDefault();
        if (primero is null) return null;
        return new EntradaDePlan(primero.Amount, primero.ValueDate, p.Installments, p.Periodicity, p.AmortizationSystem, p.AnnualRate ?? 0m,
            p.DayCount, p.RateEquivalence, Redondeo(p))
        {
            PrimerVencimiento = p.FirstDueDate,
            DiaDePago = p.PaymentDay,
            Extraordinarias = Extraordinarias(p.ExtraordinaryInstallments),
            Gracia = p.GraceKind ?? GraceKind.None,
            PeriodosDeGracia = p.GraceKind is null or GraceKind.None ? 0 : p.GraceInstallments ?? 0,
            TratamientoDeGracia = p.GraceInterestTreatment,
            CuotasDeDistribucion = p.GraceDistributionInstallments,
            Seguros = Seguros(p.Insurances),
        };
    }

    private static EntradaDeOperacion Operacion(LoanOperation o, IReadOnlyList<LoanTermsVersion> condiciones, IReadOnlyList<SaldoInicialDeCredito> saldos,
        IReadOnlyList<CuotaMigrada> cuotas) =>
        new(Id(o.Id), o.Kind, o.ValueDate, o.RegisteredAt, o.Sequence)
        {
            Monto = o.Kind == LoanOperationKind.Disbursement ? o.Amount : null,
            Saldos = o.Kind == LoanOperationKind.OpeningBalance ? saldos : [],
            CuotasMigradas = o.Kind == LoanOperationKind.OpeningBalance ? cuotas : [],
            TasaEfectivaAnual = o.Kind == LoanOperationKind.RateReset
                ? condiciones.FirstOrDefault(t => t.OperationId == o.Id)?.AnnualRate
                : null,
            Reversa = o.ReversesOperationId is { } r ? Id(r) : null,
            Contenido = Contenido(o),
        };

    /// <summary>La decisión de un pago, de un cobro provisional o de una resolución de saldo (US5) o de un ajuste de Comercial (US6); nula en las demás.</summary>
    private static object? Contenido(LoanOperation o) => o.Kind switch
    {
        LoanOperationKind.Payment or LoanOperationKind.ProvisionalCollection => Payments.DecisionDePago.ContenidoDe(o)
            ?? (o.Kind == LoanOperationKind.ProvisionalCollection ? new Domain.Lending.Payments.ContenidoDePago(PaymentModality.FreeAmount, o.Amount) : null),
        LoanOperationKind.CreditBalanceResolution => Payments.DecisionDeResolucion.Leer(o.Payload)?.Contenido(),
        // US6 (T0591): el ajuste de Comercial con su clase, su valor con signo y el recálculo sellado.
        LoanOperationKind.CommercialAdjustment => Integration.Comercial.DecisionDeAjusteComercial.Leer(o.Payload)?.Contenido(),
        // K3 (T0874, T0884): la novedad (en el crédito, en el anterior o en el enlazado) y la condonación, con su decisión.
        LoanOperationKind.TermsChange => Novelties.DecisionDeNovedad.Leer(o.Payload)?.Contenido(o.NoveltyId),
        LoanOperationKind.Waiver => Novelties.DecisionDeCondonacion.Leer(o.Payload)?.Contenido(),
        _ => null,
    };

    /// <summary>K3: la operación con que nace el enlazado de una novación o refinanciación.</summary>
    public static bool EsOrigenDeEnlazado(LoanOperation o) =>
        o.Kind == LoanOperationKind.TermsChange && Novelties.DecisionDeNovedad.Leer(o.Payload) is { } d
        && string.Equals(d.Side, nameof(LadoDeLaNovedad.Enlazado), StringComparison.Ordinal);

    /// <summary>
    /// K3 (T0886; T30, FR-098; data-model §25.8): la negociación de insolvencia de cada deudor conocida en R —la del inicio más reciente—, con su
    /// fin si ya terminó en R: el fracaso (<c>FailedOn</c>) o el acuerdo (la fecha valor de la novedad que lo tramitó). La causación de todos sus
    /// créditos queda en suspenso desde el inicio hasta ese fin; lo migrado y lo abierto por pantalla se leen igual.
    /// </summary>
    /// <summary>
    /// K3 (FR-097, T102): por crédito, el día del fallecimiento de su deudor (el evento <c>Deceased</c> vigente, conocido en R) cuando la causación no
    /// sigue tras él: <c>Causacion.ContinuaTrasFallecimiento</c> leído a esa fecha para la línea del crédito (con su excepción por línea). Con el valor
    /// por defecto (<c>true</c>) o sin lector de parámetros el crédito no aparece y causa como siempre.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, DateOnly>> FallecimientosAsync(IReadOnlyList<Loan> creditos, DateTime r, DateTime? informacionHasta,
        CancellationToken ct)
    {
        var resultado = new Dictionary<int, DateOnly>();
        if (parametros is null || creditos.Count == 0) return resultado;
        var personas = creditos.Select(c => c.DebtorPersonId).Distinct().ToList();
        var eventos = (await db.DebtorBlocks.AsNoTracking()
                .Where(b => personas.Contains(b.PersonId) && b.Reason == DebtorBlockReason.Deceased && !b.IsDeleted)
                .Select(b => new { b.PersonId, b.StartedOn, b.CreatedAt, b.LiftedAt })
                .ToListAsync(ct))
            .Where(b => b.CreatedAt <= r && (b.LiftedAt is not { } levantado || levantado > r))
            .GroupBy(b => b.PersonId).ToDictionary(g => g.Key, g => g.Min(b => b.StartedOn));
        if (eventos.Count == 0) return resultado;
        foreach (var credito in creditos)
        {
            if (!eventos.TryGetValue(credito.DebtorPersonId, out var dia)) continue;
            var sigue = await parametros.LeerAsync(ParametrosDeCartera.CausacionContinuaTrasFallecimiento, dia, informacionHasta,
                Domain.Enums.Parameters.ParameterScopeKind.CreditLine, credito.CreditLineId, ct);
            if (sigue.IsSuccess && sigue.Value.Valor is false) resultado[credito.Id] = dia;
        }
        return resultado;
    }

    private async Task<IReadOnlyDictionary<int, (DateOnly Desde, DateOnly? Hasta)>> InsolvenciasAsync(IReadOnlyList<Loan> creditos,
        IReadOnlyList<LoanOperation> operaciones, DateTime r, CancellationToken ct)
    {
        var personas = creditos.Select(c => c.DebtorPersonId).Distinct().ToList();
        if (personas.Count == 0) return new Dictionary<int, (DateOnly, DateOnly?)>();
        var procesos = (await db.InsolvencyProceedings.AsNoTracking().Where(i => personas.Contains(i.PersonId) && !i.IsDeleted).ToListAsync(ct))
            .Where(i => i.CreatedAt <= r).ToList();
        if (procesos.Count == 0) return new Dictionary<int, (DateOnly, DateOnly?)>();
        var novedades = procesos.Where(i => i.AgreementNoveltyId is not null).Select(i => i.AgreementNoveltyId!.Value).ToList();
        var acuerdos = novedades.Count == 0
            ? new Dictionary<int, DateOnly>()
            : await (from n in db.LoanNovelties.AsNoTracking()
                     join o in db.LoanOperations.AsNoTracking() on n.AppliedOperationId equals o.Id
                     where novedades.Contains(n.Id)
                     select new { n.Id, o.ValueDate }).ToDictionaryAsync(x => x.Id, x => x.ValueDate, ct);
        return procesos.GroupBy(i => i.PersonId).ToDictionary(g => g.Key, g =>
        {
            var p = g.OrderByDescending(i => i.StartDate).ThenByDescending(i => i.Id).First();
            DateOnly? hasta = null;
            if (p.ClosedAt is { } cerrado && cerrado <= r)
                hasta = p.Status == InsolvencyProceedingStatus.Failed
                    ? p.FailedOn
                    : p.AgreementNoveltyId is { } n && acuerdos.TryGetValue(n, out var fecha) ? fecha : null;
            return (p.StartDate, hasta);
        });
    }

    private sealed record ExtraordinariaSellada(int Installment, decimal Amount);

    private sealed record SeguroSellado(string Code, string Base, decimal Rate, string Periodicity, bool Computes);

    private static IReadOnlyList<CuotaExtraordinaria> Extraordinarias(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : (JsonSerializer.Deserialize<List<ExtraordinariaSellada>>(json, Json) ?? []).Select(x => new CuotaExtraordinaria(x.Installment, x.Amount)).ToList();

    private static IReadOnlyList<SeguroDelPlan> Seguros(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : (JsonSerializer.Deserialize<List<SeguroSellado>>(json, Json) ?? [])
                .Select(s => new SeguroDelPlan(s.Code, Enum.Parse<InsuranceBase>(s.Base), s.Rate, Enum.Parse<RepaymentPeriodicity>(s.Periodicity), s.Computes))
                .ToList();
}
