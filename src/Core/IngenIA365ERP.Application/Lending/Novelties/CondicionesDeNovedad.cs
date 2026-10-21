using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// La versión de condiciones que deja una novedad (feature 013, K3, T0881; data-model §4.3; FR-091): la vigente del crédito con lo que la novedad cambia
/// —tasa, cuotas, periodicidad, día de pago, primer vencimiento, conteo de reestructuraciones— sellada con la <c>RulesVersion</c> vigente (T29), la causa
/// y la novedad (<c>NoveltyId</c>, por donde la reconoce la reconstrucción). En una novación o refinanciación es la versión 1 del enlazado. (nuevo)
/// </summary>
public static class CondicionesDeNovedad
{
    /// <summary>
    /// La copia de <paramref name="v"/> con lo nuevo. <paramref name="credito"/> es el crédito nuevo de una novación (navegación: todavía sin Id); nulo
    /// en el mismo crédito.
    /// </summary>
    public static LoanTermsVersion Version(LoanTermsVersion v, LoanNovelty n, LoanOperation operacion, PlanDePagos plan, int numero, int reestructuraciones,
        DateOnly fecha, Loan? credito = null)
    {
        var entrada = plan.Entrada;
        var reestructura = n.Cause == NoveltyCause.Restructuring;
        return new()
        {
            LoanId = credito is null ? v.LoanId : 0,
            Loan = credito,
            VersionNumber = numero,
            Operation = operacion,
            EffectiveValueDate = fecha,
            RegisteredAt = operacion.RegisteredAt,
            Cause = credito is not null ? TermsVersionCause.Novation : reestructura ? TermsVersionCause.Restructuring : TermsVersionCause.Modification,
            NoveltyId = n.Id,
            // K6b (T1053): un crédito de vivienda conserva las reglas de vivienda tras la novedad; los demás, la vigente.
            RulesVersion = VersionesDeReglas.ParaCreditoNuevo(VersionesDeReglas.EsDeVivienda(v.RulesVersion)),
            CreditLineVersionId = v.CreditLineVersionId,
            ModalityCode = v.ModalityCode,
            SubsegmentCode = v.SubsegmentCode,
            ForEmployees = v.ForEmployees,
            InterestTaxTreatment = v.InterestTaxTreatment,
            RateKind = v.RateKind,
            AnnualRate = entrada.TasaEfectivaAnual,
            IndexCode = v.IndexCode,
            IndexKey = v.IndexKey,
            Margin = v.Margin,
            RateFloor = v.RateFloor,
            RateCeiling = v.RateCeiling,
            ResetPeriodicity = v.ResetPeriodicity,
            NextResetDate = v.NextResetDate,
            DefaultRateKind = v.DefaultRateKind,
            DefaultRate = v.DefaultRate,
            DefaultRateFactor = v.DefaultRateFactor,
            DefaultOnInterestAllowed = v.DefaultOnInterestAllowed,
            DayCount = v.DayCount,
            RateEquivalence = v.RateEquivalence,
            AmortizationSystem = v.AmortizationSystem,
            Periodicity = entrada.Periodicidad,
            Installments = plan.Cuotas.Count,
            TermMonths = CalculoDeNovedad.Meses(plan.Cuotas.Count, entrada.Periodicidad),
            FirstDueDate = plan.Cuotas[0].Vencimiento,
            PaymentDay = entrada.DiaDePago is { } dia ? (byte)dia : v.PaymentDay,
            GraceKind = GraceKind.None,
            GraceInstallments = null,
            GraceInterestTreatment = null,
            GraceDistributionInstallments = null,
            ExtraordinaryInstallments = null,
            RoundingUnit = v.RoundingUnit,
            RoundingMode = v.RoundingMode,
            RoundingMoment = v.RoundingMoment,
            DenominationUnit = v.DenominationUnit,
            AllocationOrder = v.AllocationOrder,
            ConceptPriority = v.ConceptPriority,
            SuspenseWithItsConcept = v.SuspenseWithItsConcept,
            AllowsSpecificInstallmentOutOfOrder = v.AllowsSpecificInstallmentOutOfOrder,
            NoteRecalculation = v.NoteRecalculation,
            Insurances = v.Insurances,
            ResultingEffectiveRate = v.ResultingEffectiveRate,
            ChargesSpread = v.ChargesSpread,
            UsuryValuePublicId = v.UsuryValuePublicId,
            RestructuringCount = reestructuraciones,
            RestructuredOn = reestructura ? fecha : v.RestructuredOn,
            InputsSha256 = operacion.InputsSha256,
        };
    }
}
