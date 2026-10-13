using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Lo que comparten el desembolso y el nuevo giro (feature 013, US3, T0444, T0446): la operación <c>Disbursement</c> del tramo con su fila
/// <c>+Principal</c> por <see cref="RegistroDeCartera"/> y su número por <see cref="NumeradorDeCartera"/>; la orden de giro con beneficiario y
/// cuenta sellados (I-1) y su <c>ResultDueOn</c> (+ <c>Giros.DiasSinResultado</c> hábiles); y las versiones de condiciones y del plan del crédito
/// que nace. No guarda. (nuevo)
/// </summary>
public sealed class TramoDeDesembolso(
    IApplicationDbContext db,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    ReglasDeParametrosDeCartera parametros,
    ICalendarioHabil calendario,
    IActorActual actorActual,
    IDateTimeService reloj,
    Engine.CargadorDeReconstruccion cargador)
{
    public const string PrefijoDeCredito = "CR-";
    public const string PrefijoDeDesembolso = "DS-";
    public const string PrefijoDeReverso = "DRV-";
    public const string PrefijoDeOrden = "OG-";

    /// <summary>Los días sin resultado por defecto si el parámetro no se puede leer (el defecto sembrado, data-model §10).</summary>
    public const int DiasSinResultadoPorDefecto = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// La operación <c>Disbursement</c> del tramo y su fila <c>+Principal</c> (data-model §7.1, §7.2), con el sello monótono del crédito y la
    /// persona que desembolsa como actor. El número se pide antes de armarla: es un hecho de sólo inserción.
    /// </summary>
    public async Task<LoanOperation> RegistrarAsync(Loan credito, LoanControl control, int tramo, DisbursementForm forma, decimal bruto,
        DateOnly fechaValor, DateOnly fechaContable, object entradas, string rulesVersion, PrevalidationOutcome prevalidacion, long? relacionada,
        CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.Disbursement, PrefijoDeDesembolso, ct);
        var payload = JsonSerializer.Serialize(entradas, Json);
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Loan = credito.Id == 0 ? credito : null,
            Kind = LoanOperationKind.Disbursement,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fechaValor,
            AccountingDate = fechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = bruto,
            ActorKind = ActorKind.Person,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            DisbursementForm = forma,
            TrancheNumber = tramo,
            RelatedOperationId = relacionada,
            Payload = payload,
            InputsSha256 = Sha256(payload),
            RulesVersion = rulesVersion,
            PrevalidationOutcome = prevalidacion.ToString(),
        });
        registro.RegistrarTransacciones(
        [
            new LoanTransaction
            {
                LoanId = credito.Id,
                Loan = credito.Id == 0 ? credito : null,
                Operation = operacion,
                Kind = LoanTransactionKind.Disbursement,
                Concept = LoanConcept.Principal,
                Amount = bruto,
                ValueDate = fechaValor,
                AccountingDate = fechaContable,
                RegisteredAt = operacion.RegisteredAt,
            },
        ]);
        return operacion;
    }

    /// <summary>La fecha hasta la que se espera el resultado del giro: <c>Giros.DiasSinResultado</c> días hábiles desde hoy.</summary>
    public async Task<DateOnly> ResultadoHastaAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leido = await parametros.LeerAsync(ParametrosDeCartera.GirosDiasSinResultado, hoy, ct: ct);
        var dias = leido.IsSuccess && leido.Value.Valor is int d && d >= 0 ? d : DiasSinResultadoPorDefecto;
        var habiles = await calendario.ObtenerAsync(hoy, hoy.AddDays(dias * 3 + 30), DiasDeDescanso.SabadoYDomingo, ct: ct);
        return habiles.SumarHabiles(hoy, dias);
    }

    /// <summary>La orden de giro del tramo (data-model §7.4), numerada al final; exige la operación ya guardada (su Id).</summary>
    public async Task<DisbursementOrder> EmitirOrdenAsync(Loan credito, LoanOperation operacion, BeneficiarioResuelto beneficiario, decimal bruto,
        decimal recogido, bool pagadaPorArchivo, int? ordenAnterior, CancellationToken ct)
    {
        if (operacion.Id == 0) throw new InvalidOperationException("La orden de giro se emite con la operación del tramo ya guardada.");
        var actor = await actorActual.ObtenerAsync(ct);
        var orden = DisbursementOrder.Emitir(credito.Id, operacion.Id, operacion.DisbursementForm ?? DisbursementForm.Transfer, beneficiario.PersonId,
            beneficiario.TaxIdType, beneficiario.TaxId, beneficiario.Name, beneficiario.BankId, beneficiario.AccountType, beneficiario.AccountNumber,
            bruto, recogido, reloj.HoyLocal, await ResultadoHastaAsync(ct), pagadaPorArchivo, reloj.UtcNow,
            actor.UserId ?? throw new InvalidOperationException("La orden de giro la emite una persona con usuario."), ordenAnterior);
        await numerador.NumerarOrdenAsync(orden, PrefijoDeOrden, ct);
        db.DisbursementOrders.Add(orden);
        return orden;
    }

    /// <summary>
    /// La versión de condiciones con que nace el crédito (<c>Origination</c>; data-model §4.3): la versión de la línea, la tasa de la banda que
    /// casó, la efectiva con cargos, la sobretasa y la usura del día (T28), <c>Mora.SobreIntereses</c> sellado y la prelación de la línea.
    /// </summary>
    public LoanTermsVersion Condiciones(Loan credito, LoanOperation operacion, RequisitosEvaluados r) =>
        Condiciones(credito, operacion, r.Simulacion, r.FechaValor, r.MoraSobreIntereses, TermsVersionCause.Origination);

    /// <summary>
    /// US6 (T0590): la versión de condiciones con que nace un crédito desde una simulación —la del desembolso (<c>Origination</c>) o la de una venta de
    /// Comercial (<c>CommercialSale</c>)—. Misma forma que la del primer tramo.
    /// </summary>
    public LoanTermsVersion Condiciones(Loan credito, LoanOperation operacion, SimulacionCalculada simulacion, DateOnly fechaValor, bool moraSobreIntereses,
        TermsVersionCause causa)
    {
        var v = simulacion.Version.Version;
        var e = simulacion.Entrada;
        var plan = simulacion.Plan;
        var usura = simulacion.Usura;
        var seguros = simulacion.Entrada.Seguros.Select(s => new { code = s.Codigo, @base = s.Base.ToString(), rate = s.Tarifa, periodicity = s.PeriodicidadDeCobro.ToString(), computes = s.Computa });
        var condiciones = new LoanTermsVersion
        {
            LoanId = credito.Id,
            Loan = credito.Id == 0 ? credito : null,
            VersionNumber = 1,
            Operation = operacion,
            EffectiveValueDate = fechaValor,
            RegisteredAt = operacion.RegisteredAt,
            Cause = causa,
            RulesVersion = plan.RulesVersion,
            CreditLineVersionId = v.Id,
            ModalityCode = v.ModalityCode,
            SubsegmentCode = v.SubsegmentCode,
            ForEmployees = v.ForEmployees,
            InterestTaxTreatment = v.InterestTaxTreatment,
            RateKind = v.RateKind,
            AnnualRate = simulacion.TasaPactada,
            IndexCode = v.IndexCode,
            IndexKey = v.IndexKey,
            Margin = v.Margin,
            RateFloor = v.RateFloor,
            RateCeiling = v.RateCeiling,
            ResetPeriodicity = v.ResetPeriodicity,
            DefaultRateKind = v.DefaultRateKind,
            DefaultRate = v.DefaultRate,
            DefaultRateFactor = v.DefaultRateFactor,
            DefaultOnInterestAllowed = moraSobreIntereses,
            DayCount = e.Convencion,
            RateEquivalence = e.Equivalencia,
            AmortizationSystem = e.Sistema,
            Periodicity = e.Periodicidad,
            Installments = e.Periodos,
            TermMonths = simulacion.PlazoMeses,
            FirstDueDate = plan.Cuotas[0].Vencimiento,
            PaymentDay = v.PaymentDay,
            GraceKind = e.Gracia == GraceKind.None ? null : e.Gracia,
            GraceInstallments = e.Gracia == GraceKind.None ? null : e.PeriodosDeGracia,
            GraceInterestTreatment = e.TratamientoDeGracia,
            GraceDistributionInstallments = e.CuotasDeDistribucion,
            ExtraordinaryInstallments = e.Extraordinarias.Count == 0 ? null : JsonSerializer.Serialize(e.Extraordinarias.Select(x => new { installment = x.Cuota, amount = x.Valor }), Json),
            RoundingUnit = v.RoundingUnit,
            RoundingMode = v.RoundingMode,
            RoundingMoment = v.RoundingMoment,
            AllocationOrder = v.AllocationOrder,
            ConceptPriority = v.ConceptPriority,
            SuspenseWithItsConcept = v.SuspenseWithItsConcept,
            AllowsSpecificInstallmentOutOfOrder = v.AllowsSpecificInstallmentOutOfOrder,
            NoteRecalculation = v.NoteRecalculation,
            Insurances = JsonSerializer.Serialize(seguros, Json),
            ResultingEffectiveRate = usura.EfectivaConCargos,
            ChargesSpread = usura.SobretasaDeCargos,
            UsuryValuePublicId = usura.UsuraPublicId,
            RestructuringCount = 0,
            InputsSha256 = simulacion.Huella,
        };
        registro.RegistrarVersionDeCondiciones(condiciones);
        return condiciones;
    }

    /// <summary>
    /// El plan del tramo (D12): el del simulador en el primero; en uno siguiente, el plan del crédito a la fecha valor (su reconstrucción)
    /// <b>regenerado</b> con el tramo desde la primera cuota que vence después (<see cref="Domain.Lending.MotorDeCartera.RegenerarPlan"/>, US4),
    /// que es lo mismo que la reconstrucción hará con la operación.
    /// </summary>
    public async Task<PlanDePagos> PlanDelTramoAsync(RequisitosEvaluados r, CancellationToken ct)
    {
        if (r.EsElPrimero || r.CreditoId is not { } creditoId) return r.Simulacion.Plan;
        var cargado = await cargador.CargarAsync(creditoId, null, r.FechaValor, ct);
        if (cargado is not { Listo: true })
            throw new InvalidOperationException($"No se pudo reconstruir el crédito para regenerar su plan: {cargado?.Error?.Code} {cargado?.Error?.Message}");
        var anterior = Domain.Lending.MotorDeCartera.Reconstruir(cargado.Entrada!, r.FechaValor).Plan
            ?? throw new InvalidOperationException("El crédito no tiene plan a la fecha valor del tramo.");
        return Domain.Lending.MotorDeCartera.RegenerarPlan(anterior, new EntradaDeRegeneracion(r.FechaValor) { CambioDeCapital = r.Bruto });
    }

    /// <summary>
    /// La versión del plan sobre lo desembolsado (D12; data-model §4.4): las cuotas de <paramref name="plan"/> (<see cref="PlanDelTramoAsync"/>)
    /// con su fecha exigible (art. 829, el siguiente hábil). Σ capital = lo desembolsado al centavo, o <c>Lending.Schedule.PrincipalMismatch</c>
    /// (SC-017).
    /// </summary>
    public Task<RepaymentScheduleVersion> PlanAsync(Loan credito, LoanTermsVersion condiciones, LoanOperation operacion, RequisitosEvaluados r,
        int numeroDeVersion, PlanDePagos plan, CancellationToken ct) =>
        PlanAsync(credito, condiciones, operacion, r.FechaValor, r.Desembolsado, r.Simulacion.Huella, numeroDeVersion, plan, ct);

    /// <summary>US6 (T0590): la versión del plan con lo desembolsado y la huella dados (la usa también el nacimiento de la obligación de una venta).</summary>
    public async Task<RepaymentScheduleVersion> PlanAsync(Loan credito, LoanTermsVersion condiciones, LoanOperation operacion, DateOnly fechaValor,
        decimal desembolsado, string huella, int numeroDeVersion, PlanDePagos plan, CancellationToken ct)
    {
        if (plan.TotalCapital != desembolsado)
            throw new InvalidOperationException(
                $"Lending.Schedule.PrincipalMismatch: el plan suma {plan.TotalCapital.ToString(CultureInfo.InvariantCulture)} de capital y lo desembolsado es {desembolsado.ToString(CultureInfo.InvariantCulture)}.");
        var ultima = plan.Cuotas[^1].Vencimiento;
        var habiles = await calendario.ObtenerAsync(fechaValor, ultima.AddDays(30), DiasDeDescanso.SoloDomingo, ct: ct);
        var version = new RepaymentScheduleVersion
        {
            LoanId = credito.Id,
            Loan = credito.Id == 0 ? credito : null,
            VersionNumber = numeroDeVersion,
            TermsVersionId = condiciones.Id,
            TermsVersion = condiciones.Id == 0 ? condiciones : null,
            Operation = operacion,
            EffectiveValueDate = fechaValor,
            RegisteredAt = operacion.RegisteredAt,
            PrincipalBase = desembolsado,
            FirstRegeneratedInstallment = plan.PrimeraCuotaRegenerada,
            InstallmentCount = plan.Cuotas.Count,
            RulesVersion = plan.RulesVersion,
            InputsSha256 = huella,
        };
        var cuotas = plan.Cuotas.Select(c => new RepaymentInstallment
        {
            ScheduleVersion = version,
            InstallmentNumber = c.Numero,
            PeriodStart = c.Desde,
            DueDate = c.Vencimiento,
            ExigibleDate = habiles.SiguienteHabil(c.Vencimiento),
            OpeningPrincipal = c.SaldoInicial,
            Principal = c.Capital,
            Interest = c.Interes,
            GraceInterest = c.InteresDeGracia,
            Insurance = c.Seguro,
            OtherCharges = 0m,
            Total = c.Total,
            ClosingPrincipal = c.SaldoFinal,
            IsExtraordinary = c.Extraordinaria != 0m || plan.Entrada.Extraordinarias.Any(x => x.Cuota == c.Numero),
            IsGraceInstallment = c.SoloInteres,
        }).ToList();
        registro.RegistrarPlan(version, cuotas);
        return version;
    }

    /// <summary>El plan resumido del hecho (<c>LoanPlanSummaryV1</c>, mensajes.md §10.1).</summary>
    public static LoanPlanSummaryV1 Resumen(RequisitosEvaluados r, int numeroDeVersion, PlanDePagos? regenerado = null) =>
        Resumen(r.Simulacion, numeroDeVersion, regenerado);

    /// <summary>US6 (T0593): el plan resumido de una simulación (también el de la obligación de una venta).</summary>
    public static LoanPlanSummaryV1 Resumen(SimulacionCalculada simulacion, int numeroDeVersion, PlanDePagos? regenerado = null)
    {
        var plan = regenerado ?? simulacion.Plan;
        var e = simulacion.Entrada;
        return new LoanPlanSummaryV1
        {
            ScheduleVersion = numeroDeVersion,
            AmortizationSystem = e.Sistema.ToString(),
            Periodicity = e.Periodicidad.ToString(),
            InstallmentCount = plan.Cuotas.Count,
            InstallmentAmount = plan.CuotaNivelada ?? plan.Cuotas[0].Total,
            FirstDueDate = plan.Cuotas[0].Vencimiento,
            FinalDueDate = plan.Cuotas[^1].Vencimiento,
            AnnualEffectiveRate = simulacion.TasaPactada,
            DayCountConvention = e.Convencion.ToString(),
            RulesVersion = plan.RulesVersion,
        };
    }

    /// <summary>La siguiente versión del plan del crédito (contando las de esta unidad de trabajo).</summary>
    public async Task<int> SiguienteVersionDelPlanAsync(int loanId, CancellationToken ct) =>
        (await db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == loanId).MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0) + 1;

    public static string Sha256(string texto) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
}
