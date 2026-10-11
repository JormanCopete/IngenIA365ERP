using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>Un reajuste registrado: la operación, la versión de condiciones, la versión del plan y la entrada del motor con la operación.</summary>
public sealed record ReajusteRegistrado(LoanOperation Operacion, LoanTermsVersion Condiciones, RepaymentScheduleVersion Plan, EntradaDeReconstruccion Entrada,
    bool IndiceDesactualizado);

/// <summary>
/// El reajuste de una tasa variable (feature 013, US4, T0498; paso (2) del ciclo, FR-067, C47, D10): en la fecha de reajuste toma el
/// <b>último índice publicado</b> (<see cref="IValoresNacionales.LeerUltimoPublicadoAsync"/>, que alerta <c>Cartera.IndiceDesactualizado</c> si
/// es viejo), le suma el margen en la expresión del índice, lo pasa a EA y aplica piso y techo (<see cref="MotorDeCartera.TasaVariable"/>; la
/// usura la topa la causación día a día). Por <see cref="RegistroDeCartera"/> registra:
/// <list type="bullet">
/// <item>la operación <c>RateReset</c> numerada, del proceso, <b>sin filas de libro</b> (C47: la tasa nueva no es una transacción de
/// dinero);</item>
/// <item>la versión de condiciones (<c>Cause = RateReset</c>) con la tasa y el próximo reajuste;</item>
/// <item>la versión del plan regenerada desde la primera cuota que vence después (cuota o plazo según lo pactado, D12).</item>
/// </list>
/// No guarda. (nuevo)
/// </summary>
public sealed class ReajusteDeTasa(
    IApplicationDbContext db,
    IValoresNacionales valores,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador)
{
    public const string PrefijoDeReajuste = "RR-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>La fecha del próximo reajuste de unas condiciones de tasa variable; nula si es fija o no tiene periodicidad de reajuste.</summary>
    public static DateOnly? ProximoReajuste(LoanTermsVersion vigentes, LoanTermsVersion pactadas)
    {
        if (vigentes.RateKind != RateKind.Variable || vigentes.ResetPeriodicity is not { } periodicidad) return null;
        if (vigentes.NextResetDate is { } fijada) return fijada;
        return Sumar(pactadas.EffectiveValueDate, periodicidad);
    }

    /// <summary>La fecha más un período de reajuste (los meses del período; las periodicidades de menos de un mes, en días).</summary>
    public static DateOnly Sumar(DateOnly fecha, RepaymentPeriodicity periodicidad)
    {
        var dias = (int)periodicidad;
        var diasDelMes = (int)RepaymentPeriodicity.Monthly;
        return dias >= diasDelMes ? fecha.AddMonths(dias / diasDelMes) : fecha.AddDays(Math.Max(1, dias));
    }

    /// <summary>
    /// Si la fecha de reajuste del crédito cae en (marca, <paramref name="hasta"/>], lo reajusta (uno por llamada: el siguiente se ve en la
    /// llamada siguiente con la versión nueva). Nulo si no le toca. Falla con <c>NationalValues.Missing</c> si no hay índice publicado.
    /// </summary>
    public async Task<Result<ReajusteRegistrado?>> SiCorrespondeAsync(CreditoCargado cargado, LoanControl control, DateOnly hasta, DateTime ahoraUtc,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cargado);
        if (!cargado.Listo || ProximoReajuste(cargado.Vigentes, cargado.Pactadas) is not { } fecha) return Result.Success<ReajusteRegistrado?>(null);
        if (fecha > hasta) return Result.Success<ReajusteRegistrado?>(null);
        var v = cargado.Vigentes;

        var indice = await valores.LeerUltimoPublicadoAsync(v.IndexCode!.Trim().ToUpperInvariant(), v.IndexKey!.Trim(), fecha, null, ct);
        if (indice.IsFailure) return Result.Failure<ReajusteRegistrado?>(indice.Error);
        // La tasa reajustada se sella como toda tasa pactada: a seis decimales de fracción (TasaAnual 9,6; T35). Sin eso la base la
        // redondearía al guardarla y la reconstrucción de mañana no daría lo que causó hoy.
        var tasa = decimal.Round(MotorDeCartera.TasaVariable(ReglasDeLineaDeCredito.Indice(indice.Value), v.Margin ?? 0m, v.RateFloor, v.RateCeiling),
            Domain.Lending.Rates.EquivalenciaDeTasas.DecimalesDeTasaPactada, MidpointRounding.AwayFromZero);

        // El plan vigente a la fecha del reajuste, regenerado con la tasa nueva.
        var anterior = MotorDeCartera.Reconstruir(cargado.Entrada!, fecha).Plan
            ?? throw new InvalidOperationException("El crédito no tiene plan a la fecha del reajuste.");
        var plan = MotorDeCartera.RegenerarPlan(anterior, new EntradaDeRegeneracion(fecha) { NuevaTasaEfectivaAnual = tasa });

        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.RateReset, PrefijoDeReajuste, ct);
        var payload = JsonSerializer.Serialize(new
        {
            index = v.IndexCode, key = v.IndexKey, indexValue = indice.Value.Valor, indexPublicId = indice.Value.PublicId,
            indexValidFrom = indice.Value.ValidFrom, stale = indice.Value.Desactualizado, margin = v.Margin, floor = v.RateFloor, ceiling = v.RateCeiling,
            rate = tasa,
        }, Json);
        var operacion = registro.RegistrarOperacion(control, ahoraUtc, sello => new LoanOperation
        {
            LoanId = cargado.Credito.Id,
            Kind = LoanOperationKind.RateReset,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = fecha,
            RegisteredAt = sello.RegisteredAt,
            Amount = 0m,
            ActorKind = ActorKind.Process,
            Reason = $"Reajuste de tasa variable con {v.IndexCode} {v.IndexKey}",
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = v.RulesVersion,
            PrevalidationOutcome = PrevalidationOutcome.NotApplicable.ToString(),
        });
        var condiciones = Version(v, operacion, fecha, tasa, Sumar(fecha, v.ResetPeriodicity!.Value));
        registro.RegistrarVersionDeCondiciones(condiciones);

        var numeroDelPlan = (await db.RepaymentScheduleVersions.AsNoTracking().Where(p => p.LoanId == cargado.Credito.Id)
            .MaxAsync(p => (int?)p.VersionNumber, ct) ?? 0) + 1;
        var version = new RepaymentScheduleVersion
        {
            LoanId = cargado.Credito.Id,
            VersionNumber = numeroDelPlan,
            TermsVersion = condiciones,
            Operation = operacion,
            EffectiveValueDate = fecha,
            RegisteredAt = operacion.RegisteredAt,
            PrincipalBase = plan.TotalCapital,
            FirstRegeneratedInstallment = plan.PrimeraCuotaRegenerada,
            InstallmentCount = plan.Cuotas.Count,
            RulesVersion = plan.RulesVersion,
            InputsSha256 = operacion.InputsSha256,
        };
        var articulo829 = FechasDeVencimiento.CalendarioDelArticulo829(cargado.Entrada!.Festivos);
        var cuotas = plan.Cuotas.Select(c => new RepaymentInstallment
        {
            ScheduleVersion = version,
            InstallmentNumber = c.Numero,
            PeriodStart = c.Desde,
            DueDate = c.Vencimiento,
            ExigibleDate = FechasDeVencimiento.FechaExigible(c.Vencimiento, articulo829),
            OpeningPrincipal = c.SaldoInicial,
            Principal = c.Capital,
            Interest = c.Interes,
            GraceInterest = c.InteresDeGracia,
            Insurance = c.Seguro,
            OtherCharges = 0m,
            Total = c.Total,
            ClosingPrincipal = c.SaldoFinal,
            IsExtraordinary = c.Extraordinaria != 0m,
            IsGraceInstallment = c.SoloInteres,
        }).ToList();
        registro.RegistrarPlan(version, cuotas);

        var conElReajuste = CargadorDeReconstruccion.ConOperacion(cargado.Entrada!, operacion);
        var entrada = conElReajuste with
        {
            Operaciones = [.. conElReajuste.Operaciones.Take(conElReajuste.Operaciones.Count - 1), conElReajuste.Operaciones[^1] with { TasaEfectivaAnual = tasa }],
        };
        return Result.Success<ReajusteRegistrado?>(new ReajusteRegistrado(operacion, condiciones, version, entrada, indice.Value.Desactualizado));
    }

    /// <summary>La versión de condiciones del reajuste: la vigente con la tasa nueva, el próximo reajuste y la operación.</summary>
    public static LoanTermsVersion Version(LoanTermsVersion v, LoanOperation operacion, DateOnly fecha, decimal tasa, DateOnly proximo) => new()
    {
        LoanId = v.LoanId, VersionNumber = v.VersionNumber + 1, Operation = operacion, EffectiveValueDate = fecha, RegisteredAt = operacion.RegisteredAt,
        Cause = TermsVersionCause.RateReset, RulesVersion = v.RulesVersion, CreditLineVersionId = v.CreditLineVersionId, ModalityCode = v.ModalityCode,
        SubsegmentCode = v.SubsegmentCode, ForEmployees = v.ForEmployees, InterestTaxTreatment = v.InterestTaxTreatment, RateKind = v.RateKind,
        AnnualRate = tasa, IndexCode = v.IndexCode, IndexKey = v.IndexKey, Margin = v.Margin, RateFloor = v.RateFloor, RateCeiling = v.RateCeiling,
        ResetPeriodicity = v.ResetPeriodicity, NextResetDate = proximo, DefaultRateKind = v.DefaultRateKind, DefaultRate = v.DefaultRate,
        DefaultRateFactor = v.DefaultRateFactor, DefaultOnInterestAllowed = v.DefaultOnInterestAllowed, DayCount = v.DayCount,
        RateEquivalence = v.RateEquivalence, AmortizationSystem = v.AmortizationSystem, Periodicity = v.Periodicity, Installments = v.Installments,
        TermMonths = v.TermMonths, FirstDueDate = v.FirstDueDate, PaymentDay = v.PaymentDay, GraceKind = v.GraceKind, GraceInstallments = v.GraceInstallments,
        GraceInterestTreatment = v.GraceInterestTreatment, GraceDistributionInstallments = v.GraceDistributionInstallments,
        ExtraordinaryInstallments = v.ExtraordinaryInstallments, RoundingUnit = v.RoundingUnit, RoundingMode = v.RoundingMode,
        RoundingMoment = v.RoundingMoment, AllocationOrder = v.AllocationOrder, ConceptPriority = v.ConceptPriority,
        SuspenseWithItsConcept = v.SuspenseWithItsConcept, AllowsSpecificInstallmentOutOfOrder = v.AllowsSpecificInstallmentOutOfOrder,
        NoteRecalculation = v.NoteRecalculation, Insurances = v.Insurances, ResultingEffectiveRate = v.ResultingEffectiveRate,
        ChargesSpread = v.ChargesSpread, UsuryValuePublicId = v.UsuryValuePublicId, RestructuringCount = v.RestructuringCount,
        RestructuredOn = v.RestructuredOn, InputsSha256 = operacion.InputsSha256,
    };
}
