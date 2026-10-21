using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Housing;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Una cuota extraordinaria pedida: los meses (desde el desembolso) en que se paga y su valor (FR-023; api.md §4.1). (nuevo)</summary>
public sealed record ScheduleExtraordinaryInput(IReadOnlyList<int> Months, decimal Amount);

/// <summary>La gracia pedida: clase, períodos y cómo se cobra su interés en la gracia total (api.md §4.1). (nuevo)</summary>
public sealed record ScheduleGraceInput(GraceKind Kind = GraceKind.None, int Periods = 0, GraceInterestTreatment? InterestTreatment = null);

/// <summary>
/// Lo que se simula (api.md §4.1 <c>ScheduleSimulationInput</c>; FR-034): la línea, el monto, el número de cuotas del plazo, y opcionales la
/// periodicidad, el sistema (los de defecto de la línea), las fechas de desembolso (hoy) y de primer vencimiento, las extraordinarias, la gracia
/// y los seguros opcionales elegidos (los obligatorios de la línea siempre van). La persona, si viene, decide la banda (empleado, tipo de
/// deudor, antigüedad). (nuevo)
/// </summary>
public sealed record ScheduleSimulationInput(
    Guid CreditLinePublicId,
    decimal Amount,
    int Installments,
    RepaymentPeriodicity? Periodicity = null,
    DateOnly? DisbursementDate = null,
    DateOnly? FirstDueDate = null,
    AmortizationSystem? AmortizationSystem = null,
    IReadOnlyList<ScheduleExtraordinaryInput>? Extraordinary = null,
    ScheduleGraceInput? Grace = null,
    IReadOnlyList<string>? Insurances = null,
    Guid? PersonPublicId = null);

/// <summary>Lo que la persona pone en la banda de tasas (data-model §1.4): tipo de deudor, si es empleado y su antigüedad. (nuevo)</summary>
public sealed record PerfilParaLaBanda(DebtorKind? TipoDeDeudor, bool EsEmpleado, int? AntiguedadMeses)
{
    public static PerfilParaLaBanda Ninguno { get; } = new(null, false, null);
}

/// <summary>
/// Una simulación calculada (feature 013, T0389): la versión de la línea que la rige, la entrada del motor, el plan, el control de usura, la
/// tasa pactada (la de la banda que casó), la combinación del catálogo, el plazo en meses y la huella <see cref="Huella"/>
/// (<c>SimulationSha256</c>). No escribe nada. (nuevo)
/// </summary>
public sealed record SimulacionCalculada(
    VersionDeLineaLeida Version,
    EntradaDePlan Entrada,
    PlanDePagos Plan,
    ResultadoDeUsura Usura,
    decimal TasaPactada,
    CombinacionDeModalidad Combinacion,
    int PlazoMeses,
    DateOnly Fecha,
    string Huella)
{
    /// <summary>
    /// K6b (T1054, T1055): en una línea en UVR, el plan tal como lo calcula el motor (en UVR) y la UVR del desembolso con que <see cref="Plan"/> lo
    /// valora en pesos (capital por acumulado contra el monto); nulos en pesos.
    /// </summary>
    public PlanDePagos? PlanEnUvr { get; init; }

    public decimal? ValorUvr { get; init; }

    /// <summary>La primera cuota que amortiza, llevada a un mes (FR-036: lo que la cuota simulada le quita a la capacidad de pago).</summary>
    public decimal CuotaMensual
    {
        get
        {
            var cuota = Plan.Cuotas.FirstOrDefault(c => !c.SoloInteres && c.Capital > 0m) ?? Plan.Cuotas.FirstOrDefault();
            if (cuota is null) return 0m;
            var p = Entrada.Periodicidad;
            if (p == RepaymentPeriodicity.SinglePayment) return 0m;
            return decimal.Round(cuota.Total * (int)RepaymentPeriodicity.Monthly / (int)p, 2, MidpointRounding.AwayFromZero);
        }
    }
}

/// <summary>
/// El simulador de crédito (feature 013, T0389; FR-034, R40; api.md §4.1): el único sitio que arma un plan de pagos desde una versión de
/// línea para una persona que todavía no tiene crédito. Lo usan la simulación (<c>/simulations/schedule</c>, el PDF y el envío) y la
/// solicitud (monto, plazo, periodicidad y usura al crear, editar y radicar; la cuota para la capacidad de pago):
/// <list type="number">
/// <item>la línea admite créditos nuevos (<c>Lending.CreditLine.Closed</c>; cerrada por usura, <c>Lending.Usury.Exceeded</c>);</item>
/// <item>la versión vigente a la fecha de desembolso (<see cref="VersionVigenteDeLinea"/>);</item>
/// <item>monto, cuotas (por el plazo en meses de la línea), periodicidad, sistema, gracia y extraordinarias dentro de la línea
/// (<c>Lending.Schedule.OutOfLineRange</c> con <c>{ field, min, max }</c>); seguros opcionales sólo los de la línea;</item>
/// <item>la tasa de la banda que casa por monto, plazo, tipo de deudor, empleado y antigüedad (o la de la versión; variable con su índice);</item>
/// <item>el plan (<see cref="MotorDeCartera.GenerarPlan"/>) y el control de usura del día con la usura certificada de la modalidad
/// (<see cref="ControlDeUsura"/>; sin usura del día, <c>NationalValues.Missing</c>, que el lector alerta).</item>
/// </list>
/// Que la efectiva supere la usura no es error aquí: lo dice el resultado; simular muestra el aviso, radicar y decidir bloquean. (nuevo)
/// </summary>
public sealed class SimuladorDeCredito(IApplicationDbContext db, ReglasDeLineaDeCredito reglas)
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<Result<SimulacionCalculada>> SimularAsync(CreditLine linea, ScheduleSimulationInput pedido, PerfilParaLaBanda perfil,
        DateOnly hoy, CancellationToken ct) => await SimularAsync(linea, pedido, perfil, hoy, validarRangos: true, ct);

    /// <summary>
    /// US6 (T0590): la misma simulación; con <paramref name="validarRangos"/> falso no mira la periodicidad, el sistema, el monto, las cuotas ni el plazo
    /// contra la línea —la obligación de una venta de Comercial nace siempre (FR-050): que salga de los rangos lo dice la evaluación de la venta, no
    /// impide crearla—. La línea tiene que estar activa y sin bloqueo por usura igual.
    /// </summary>
    public async Task<Result<SimulacionCalculada>> SimularAsync(CreditLine linea, ScheduleSimulationInput pedido, PerfilParaLaBanda perfil,
        DateOnly hoy, bool validarRangos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(perfil);

        if (linea.Status == CreditLineStatus.Closed)
            return Result.Failure<SimulacionCalculada>(ErroresDeOriginacion.LineaCerrada(linea.Code, linea.CloseNotes ?? "cierre manual"));
        var fecha = pedido.DisbursementDate ?? hoy;
        if (linea.UsuryBlockedSince is { } bloqueada && bloqueada <= fecha)
            return Result.Failure<SimulacionCalculada>(ErroresDeOriginacion.LineaBloqueadaPorUsura(linea.Code, bloqueada));
        if (linea.Status != CreditLineStatus.Active)
            return Result.Failure<SimulacionCalculada>(ErroresDeOriginacion.LineaCerrada(linea.Code, "todavía no está activa"));

        var version = await new VersionVigenteDeLinea(db).LeerAsync(linea.Id, fecha, null, ct);
        if (version is null) return Result.Failure<SimulacionCalculada>(ErroresDeLineasDeCredito.LineaInexistente());
        var b = version.Borrador();

        var periodicidad = pedido.Periodicity ?? b.DefaultPeriodicity;
        if (validarRangos && !b.Periodicities.Contains(periodicidad))
            return Falla(ErroresDeOriginacion.FueraDeRango("periodicity", null, null,
                $"La línea no admite la periodicidad {periodicidad}. Admite: {string.Join(", ", b.Periodicities)}."));
        var sistema = periodicidad == RepaymentPeriodicity.SinglePayment ? AmortizationSystem.Bullet : pedido.AmortizationSystem ?? b.DefaultAmortizationSystem;
        if (validarRangos && !b.AmortizationSystems.Contains(sistema))
            return Falla(ErroresDeOriginacion.FueraDeRango("amortizationSystem", null, null,
                $"La línea no admite el sistema de amortización {sistema}. Admite: {string.Join(", ", b.AmortizationSystems)}."));

        if (validarRangos && (pedido.Amount < b.MinAmount || pedido.Amount > b.MaxAmount))
            return Falla(ErroresDeOriginacion.FueraDeRango("amount", b.MinAmount, b.MaxAmount,
                $"El monto tiene que estar entre {ErroresDeOriginacion.Pesos(b.MinAmount)} y {ErroresDeOriginacion.Pesos(b.MaxAmount)}."));

        var cuotas = periodicidad == RepaymentPeriodicity.SinglePayment ? 1 : pedido.Installments;
        var meses = PlazoEnMeses(cuotas, periodicidad, fecha, pedido.FirstDueDate);
        var minimo = PlanTipoDeLinea.Periodos(b.MinTermMonths, periodicidad);
        var maximo = Math.Min(PlanTipoDeLinea.Periodos(b.MaxTermMonths, periodicidad), b.MaxInstallments ?? int.MaxValue);
        if (validarRangos && periodicidad != RepaymentPeriodicity.SinglePayment && (cuotas < minimo || cuotas > maximo))
            return Falla(ErroresDeOriginacion.FueraDeRango("installments", minimo, maximo,
                $"Con periodicidad {periodicidad} la línea admite de {minimo.ToString(Inv)} a {maximo.ToString(Inv)} cuotas."));
        if (validarRangos && (meses < b.MinTermMonths || meses > b.MaxTermMonths))
            return Falla(ErroresDeOriginacion.FueraDeRango("installments", b.MinTermMonths, b.MaxTermMonths,
                $"El plazo ({meses.ToString(Inv)} meses) tiene que estar entre {b.MinTermMonths.ToString(Inv)} y {b.MaxTermMonths.ToString(Inv)} meses."));

        var gracia = pedido.Grace ?? new ScheduleGraceInput();
        if (gracia.Kind != GraceKind.None)
        {
            if (gracia.Kind != b.AllowedGraceKind)
                return Falla(ErroresDeOriginacion.FueraDeRango("grace.kind", null, null,
                    b.AllowedGraceKind == GraceKind.None ? "La línea no admite períodos de gracia." : $"La línea sólo admite gracia {b.AllowedGraceKind}."));
            var maxGracia = Math.Min(b.MaxGraceInstallments ?? 0, cuotas - 1);
            if (gracia.Periods < 1 || gracia.Periods > maxGracia)
                return Falla(ErroresDeOriginacion.FueraDeRango("grace.periods", 1, maxGracia,
                    $"La gracia va de 1 a {maxGracia.ToString(Inv)} períodos en esta línea."));
        }

        var extraordinarias = new List<CuotaExtraordinaria>();
        foreach (var x in pedido.Extraordinary ?? [])
            foreach (var mes in x.Months ?? [])
                extraordinarias.Add(new CuotaExtraordinaria(CuotaDelMes(mes, periodicidad, cuotas), x.Amount));
        if (extraordinarias.Count > 0)
        {
            if (!b.AllowsExtraordinaryInstallments)
                return Falla(ErroresDeOriginacion.FueraDeRango("extraordinary", 0m, 0m, "La línea no admite cuotas extraordinarias."));
            var fraccion = extraordinarias.Sum(x => x.Valor) / pedido.Amount;
            if (b.MaxExtraordinaryFraction is { } tope && fraccion > tope)
                return Falla(ErroresDeOriginacion.FueraDeRango("extraordinary", 0m, tope,
                    $"Las extraordinarias suman más de lo que admite la línea ({(tope * 100m).ToString("0.##", Inv)} % del monto)."));
        }

        var elegidos = (pedido.Insurances ?? []).Select(c => c.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        var desconocido = elegidos.FirstOrDefault(c => version.Seguros.All(s => s.Code != c));
        if (desconocido is not null)
            return Falla(ErroresDeLineasDeCredito.Invalido("insurances", $"La línea no ofrece el seguro «{desconocido}»."));
        var seguros = version.Seguros.Where(s => s.IsMandatory || elegidos.Contains(s.Code)).OrderBy(s => s.Code, StringComparer.Ordinal).ToList();

        var combinacion = await reglas.CombinacionAsync(b.ModalityCode, b.SubsegmentCode, fecha, ct);
        if (combinacion.IsFailure) return Result.Failure<SimulacionCalculada>(combinacion.Error);
        var usura = await reglas.UsuraAsync(combinacion.Value, fecha, ct);
        if (usura.IsFailure) return Result.Failure<SimulacionCalculada>(usura.Error);
        var indice = await reglas.IndiceAsync(b, fecha, ct);
        if (indice.IsFailure) return Result.Failure<SimulacionCalculada>(indice.Error);

        var banda = version.Bandas.OrderBy(x => x.Order)
            .FirstOrDefault(x => x.Casa(pedido.Amount, meses, perfil.TipoDeDeudor, perfil.EsEmpleado, perfil.AntiguedadMeses));
        var tasa = PlanTipoDeLinea.TasaEfectiva(b, banda is null ? null : b.RateBands.First(r => r.Order == banda.Order), indice.Value);

        // K6b (T1053): un crédito de vivienda sella la versión de reglas de vivienda (Ley 546 de 1999); los demás, la vigente.
        var esVivienda = ReglasDeLineaDeCredito.EsVivienda(b.ModalityCode.Trim().ToUpperInvariant(), combinacion.Value);
        var entrada = new EntradaDePlan(pedido.Amount, fecha, cuotas, periodicidad, sistema, tasa, b.DayCount, b.RateEquivalence, version.Version.Redondeo())
        {
            RulesVersion = VersionesDeReglas.ParaCreditoNuevo(esVivienda),
            PrimerVencimiento = periodicidad == RepaymentPeriodicity.SinglePayment ? pedido.FirstDueDate ?? fecha.AddMonths(Math.Max(1, meses)) : pedido.FirstDueDate,
            DiaDePago = b.PaymentDay,
            Extraordinarias = extraordinarias,
            Gracia = gracia.Kind,
            PeriodosDeGracia = gracia.Kind == GraceKind.None ? 0 : gracia.Periods,
            TratamientoDeGracia = gracia.Kind == GraceKind.Total ? gracia.InterestTreatment ?? b.GraceInterestTreatment : null,
            CuotasDeDistribucion = gracia.Kind == GraceKind.Total ? b.GraceDistributionInstallments : null,
            Seguros = seguros.Select(s => s.ParaElPlan()).ToList(),
        };

        PlanDePagos plan;
        ResultadoDeUsura control;
        PlanDePagos? planEnUvr = null;
        decimal? valorUvr = null;
        try
        {
            if (b.DenominationUnit == DenominationUnit.Uvr)
            {
                // K6b (T1054, T1055): en UVR el motor proyecta el plan en UVR (el monto a la UVR publicada del desembolso) y el plan en pesos lo
                // valora a esa UVR, con el capital por acumulado contra el monto.
                var uvr = await reglas.UvrAsync(fecha, ct);
                if (uvr.IsFailure) return Result.Failure<SimulacionCalculada>(uvr.Error);
                var modo = entrada.Redondeo.Modo;
                var entradaEnUvr = entrada with
                {
                    Monto = ConversionUvr.AUvr(entrada.Monto, uvr.Value, modo),
                    Redondeo = RedondeoDeCartera.EnUvr(entrada.Redondeo),
                    Extraordinarias = entrada.Extraordinarias.Select(x => x with { Valor = ConversionUvr.AUvr(x.Valor, uvr.Value, modo) }).ToList(),
                };
                (planEnUvr, control) = MotorDeCartera.SimularConUsura(entradaEnUvr, usura.Value);
                plan = ConversionUvr.PlanAlDesembolso(planEnUvr, uvr.Value, entrada);
                valorUvr = uvr.Value;
            }
            else
            {
                (plan, control) = MotorDeCartera.SimularConUsura(entrada, usura.Value);
            }
        }
        catch (ArgumentException e)
        {
            return Falla(ErroresDeLineasDeCredito.Invalido("schedule", e.Message.Split(" (Parameter", 2)[0]));
        }

        var huella = Huella(linea, version, entrada, usura.Value, pedido.PersonPublicId);
        return Result.Success(new SimulacionCalculada(version, entrada, plan, control, tasa, combinacion.Value, meses, fecha, huella)
        {
            PlanEnUvr = planEnUvr,
            ValorUvr = valorUvr,
        });
    }

    /// <summary>
    /// La huella <c>SimulationSha256</c> (R40): la línea y la versión, las entradas del plan, la tasa, la usura usada, la persona y la versión de
    /// reglas. Las mismas entradas dan la misma huella; la solicitud la guarda para saber qué simulación se mostró al radicar.
    /// </summary>
    public static string Huella(CreditLine linea, VersionDeLineaLeida version, EntradaDePlan e, UsuraCertificada usura, Guid? persona) =>
        new HuellaDeCartera()
            .Identificador("creditLine", linea.PublicId)
            .Identificador("version", version.Version.PublicId)
            .Texto("rulesVersion", e.RulesVersion)
            .Decimal("amount", e.Monto)
            .Fecha("disbursementDate", e.FechaDesembolso)
            .Entero("periods", e.Periodos)
            .Enumeracion("periodicity", (RepaymentPeriodicity?)e.Periodicidad)
            .Enumeracion("system", (AmortizationSystem?)e.Sistema)
            .Decimal("rate", e.TasaEfectivaAnual)
            .Fecha("firstDueDate", e.PrimerVencimiento)
            .Entero("paymentDay", e.DiaDePago)
            .Enumeracion("graceKind", (GraceKind?)e.Gracia)
            .Entero("gracePeriods", e.PeriodosDeGracia)
            .Enumeracion("graceTreatment", e.TratamientoDeGracia)
            .Lista("extraordinary", e.Extraordinarias.OrderBy(x => x.Cuota), (h, x) => h.Entero("installment", x.Cuota).Decimal("amount", x.Valor))
            .Lista("insurances", e.Seguros.OrderBy(s => s.Codigo, StringComparer.Ordinal), (h, s) => h.Texto("code", s.Codigo).Decimal("rate", s.Tarifa))
            .Identificador("usury", usura.PublicId)
            .Identificador("person", persona)
            .Calcular();

    /// <summary>El plazo en meses: cuotas × días del período ÷ 30, por arriba; en pago único, los meses hasta el vencimiento (al menos uno).</summary>
    public static int PlazoEnMeses(int cuotas, RepaymentPeriodicity periodicidad, DateOnly desembolso, DateOnly? vencimiento)
    {
        if (periodicidad == RepaymentPeriodicity.SinglePayment)
        {
            if (vencimiento is not { } v) return 1;
            var meses = (v.Year - desembolso.Year) * 12 + v.Month - desembolso.Month + (v.Day > desembolso.Day ? 1 : 0);
            return Math.Max(1, meses);
        }
        var dias = cuotas * (int)periodicidad;
        return Math.Max(1, (dias + (int)RepaymentPeriodicity.Monthly - 1) / (int)RepaymentPeriodicity.Monthly);
    }

    /// <summary>La cuota en que cae el mes <paramref name="mes"/> desde el desembolso: mes × 30 ÷ días del período, por arriba (FR-023).</summary>
    public static int CuotaDelMes(int mes, RepaymentPeriodicity periodicidad, int cuotas)
    {
        if (periodicidad == RepaymentPeriodicity.SinglePayment) return 1;
        var dias = mes * (int)RepaymentPeriodicity.Monthly;
        return Math.Clamp((dias + (int)periodicidad - 1) / (int)periodicidad, 1, Math.Max(1, cuotas));
    }

    private static Result<SimulacionCalculada> Falla(Error e) => Result.Failure<SimulacionCalculada>(e);
}
