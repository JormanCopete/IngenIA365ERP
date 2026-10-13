using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
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
public sealed class CargadorDeReconstruccion(IApplicationDbContext db, ISeriesRegulatorias series)
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
                    Condiciones(pactadas, vigentes, leidas.Value) with
                    {
                        CausacionDeSeguros = momentosDeSeguro.GetValueOrDefault(pactadas.CreditLineVersionId, InsuranceAccrualTiming.AtDueDate),
                        Prelacion = PrelacionSellada(vigentes),
                        CastigoDesde = credito.WrittenOffOn,
                    },
                    credito.Origin == LoanOrigin.Migration ? null : Plan(pactadas, ops),
                    ops.Where(o => !SinEfectoEnSaldos.Contains(o.Kind))
                        .Select(o => Operacion(o, suyas, saldosIniciales.Where(t => t.OperationId == o.Id)
                                .Select(t => new SaldoInicialDeCredito(t.Concept, t.InstallmentNumber, t.Amount)).ToList(),
                            planesMigrados.Where(p => p.LoanId == credito.Id && p.OperationId == o.Id)
                                .OrderBy(p => p.InstallmentNumber)
                                .Select(p => new CuotaMigrada(p.InstallmentNumber, p.DueDate, p.Principal) { Desde = p.PeriodStart }).ToList()))
                        .ToList(),
                    r);
                resultado[credito.Id] = new CreditoCargado(credito, pactadas, vigentes, ops, leidas.Value.Aplicar(entrada), leidas.Value, null);
            }
            catch (ReglaDeCarteraException ex)
            {
                resultado[credito.Id] = new CreditoCargado(credito, pactadas, vigentes, ops, null, leidas.Value, ErroresDeGobierno.De(ex.Problema));
            }
        }
        return resultado;
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

    private static CondicionesDeCredito Condiciones(LoanTermsVersion p, LoanTermsVersion vigentes, SeriesDeModalidad series) =>
        new(p.RulesVersion, p.AnnualRate ?? 0m, p.DefaultRateKind, p.DayCount, p.RateEquivalence, p.Periodicity, Redondeo(p))
        {
            MoraFijaEA = p.DefaultRateKind == DefaultRateKind.Fixed ? p.DefaultRate : null,
            FactorDeMora = p.DefaultRateKind == DefaultRateKind.RelativeToCurrent ? p.DefaultRateFactor : null,
            SobretasaDeCargos = p.ChargesSpread ?? 0m,
            MoraSobreIntereses = p.DefaultOnInterestAllowed,
            Margen = p.Margin ?? 0m,
            Piso = p.RateFloor,
            Techo = p.RateCeiling,
            Reestructuraciones = vigentes.RestructuringCount,
            UsuraRequerida = series.ExigeUsura,
        };

    /// <summary>El redondeo sellado; una unidad que el motor no admite cae al centavo (nunca debió sellarse).</summary>
    public static RedondeoDeCartera Redondeo(LoanTermsVersion t) =>
        RedondeoDeCartera.UnidadesAdmitidas.Contains(t.RoundingUnit) && Enum.IsDefined(t.RoundingMode) && Enum.IsDefined(t.RoundingMoment)
            ? new RedondeoDeCartera(t.RoundingUnit, t.RoundingMode, t.RoundingMoment)
            : RedondeoDeCartera.AlCentavo;

    /// <summary>El plan pactado del primer tramo con las condiciones selladas; nulo si todavía no hay desembolso.</summary>
    public static EntradaDePlan? Plan(LoanTermsVersion p, IEnumerable<LoanOperation> operaciones)
    {
        var primero = operaciones.Where(o => o.Kind == LoanOperationKind.Disbursement).OrderBy(o => o.ValueDate).ThenBy(o => o.Sequence).FirstOrDefault();
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
        _ => null,
    };

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
