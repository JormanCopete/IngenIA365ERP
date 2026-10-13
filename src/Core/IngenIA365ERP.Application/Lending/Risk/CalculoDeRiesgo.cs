using System.Text.Json;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>La calificación y el deterioro de un crédito de la tanda, con lo que el riesgo cargó de él. (nuevo)</summary>
public sealed record RiesgoDeCredito(CreditoDeRiesgo Credito, CalificacionDeCredito Calificacion, DeterioroIndividual Deterioro, ModeloDelSegmento Modelo);

/// <summary>Lo que salió de una tanda: los créditos calculados y los que fallaron, con su error. (nuevo)</summary>
public sealed record RiesgoDeLaTanda(IReadOnlyList<RiesgoDeCredito> Calculados, IReadOnlyDictionary<int, Error> Errores);

/// <summary>
/// El paso puro del riesgo sobre lo cargado (feature 013, US9, T0699, T0705; T46): arma <see cref="CreditoParaCalificar"/> de todos los créditos
/// del grupo (el arrastre los necesita) —segmento SARC del catálogo, mora de la reconstrucción, meses en E de la línea anterior, codeudores, pago
/// único, crédito nuevo (sin línea en el cierre anterior), reestructurado, insolvencia del deudor y análisis vigente a R—, los califica con
/// <see cref="MotorDeCalificacion"/> sobre la norma efectiva y deteriora los de la tanda con <see cref="MotorDeDeterioroIndividual"/>. Lo usan
/// el cierre, la calificación informativa y su lista. No lee ni escribe. (nuevo)
/// </summary>
public static class CalculoDeRiesgo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <param name="sinAnalisis">Para comparar con la de las reglas (<c>SetAnalysisRatingCommand</c>): el análisis no compite.</param>
    public static RiesgoDeLaTanda Calcular(DatosDeRiesgo datos, ContextoDeRiesgo contexto, bool conArrastre, bool sinAnalisis = false)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(contexto);
        var errores = new Dictionary<int, Error>(datos.Errores);
        var paraCalificar = datos.Creditos.Values.Select(c => ParaCalificar(c, contexto, datos.CierreAnterior is not null, sinAnalisis)).ToList();
        var opciones = new OpcionesDeCalificacion(conArrastre, contexto.Aportes);
        var calificaciones = MotorDeCalificacion.Calificar(contexto.Norma, paraCalificar, opciones).ToDictionary(c => c.LoanId);

        var calculados = new List<RiesgoDeCredito>();
        foreach (var id in datos.Tanda.Order())
        {
            if (!datos.Creditos.TryGetValue(id, out var credito) || !calificaciones.TryGetValue(id, out var calificacion))
            {
                if (!errores.ContainsKey(id)) errores[id] = new Error("Lending.Risk.LoanNotLoaded", "El crédito no está vivo al corte o no se pudo cargar.");
                continue;
            }
            try
            {
                var deterioro = MotorDeDeterioroIndividual.Calcular(contexto.Norma, ParaDeteriorar(credito, calificacion), contexto.Aportes, contexto.Redondeo);
                calculados.Add(new RiesgoDeCredito(credito, calificacion, deterioro, contexto.Seleccion.ParaCredito(credito.Combinacion.ExpectedLossSegment)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ReglaDeCarteraException)
            {
                errores[id] = new Error(ex is ReglaDeCarteraException rc ? rc.Codigo : "Lending.Risk.ImpairmentFailed", ex.Message);
            }
        }
        return new RiesgoDeLaTanda(calculados, errores);
    }

    /// <summary>Lo que el motor de calificación necesita de un crédito (T0699).</summary>
    public static CreditoParaCalificar ParaCalificar(CreditoDeRiesgo c, ContextoDeRiesgo contexto, bool hayCierreAnterior, bool sinAnalisis = false)
    {
        ArgumentNullException.ThrowIfNull(c);
        var segmento = c.Combinacion.SarcSegment;
        return new CreditoParaCalificar(c.Credito.Id, c.Credito.DebtorPersonId, segmento, c.DiasDeMora)
        {
            MesesEnE = c.MesesEnE,
            Codeudores = c.Codeudores,
            PagoUnico = c.Vigentes.AmortizationSystem == AmortizationSystem.Bullet,
            Nuevo = hayCierreAnterior && c.LineaAnterior is null,
            Reestructuracion = c.Vigentes.RestructuringCount > 0
                ? new ReestructuracionParaCalificar(c.LineaAnterior?.FinalRating ?? contexto.Norma.CategoriaPorMora(segmento, c.DiasDeMora),
                    CuotasPagadasSeguidas(c))
                : null,
            Insolvencia = c.Insolvencia is { } i
                ? new InsolvenciaParaCalificar(i.Status == InsolvencyProceedingStatus.Failed && (i.FailedOn is not { } f || f <= contexto.Fecha)
                        ? InsolvencyProceedingStatus.Failed
                        : i.Status,
                    c.LineaAnterior?.FinalRating ?? contexto.Norma.CategoriaPorMora(segmento, c.DiasDeMora))
                : null,
            Analisis = sinAnalisis || c.Analisis is null ? null : new CalificacionImpuesta(c.Analisis.Rating, c.Analisis.Subcategory),
        };
    }

    /// <summary>Lo que el deterioro individual necesita de un crédito ya calificado (T0699).</summary>
    public static CreditoParaDeteriorar ParaDeteriorar(CreditoDeRiesgo c, CalificacionDeCredito calificacion)
    {
        var e = c.Estado;
        var saldos = new SaldosParaDeterioro(
            e.Saldo(LoanConcept.Principal),
            e.Saldo(LoanConcept.CurrentInterest),
            e.Saldo(LoanConcept.DefaultInterest),
            e.Saldo(LoanConcept.Insurance) + e.Saldo(LoanConcept.CollectionFee) + e.Saldo(LoanConcept.OtherCharge));
        return new CreditoParaDeteriorar(c.Credito.Id, c.Combinacion.SarcSegment, calificacion.Categoria, calificacion.Subcategoria, c.DiasDeMora,
            saldos, c.Suspendido)
        {
            MesesEnE = c.MesesEnE,
            Garantias = c.Garantias,
            AportesAProrrata = 0m,
        };
    }

    /// <summary>
    /// Las cuotas exigibles pagadas seguidas hasta el corte, contadas desde la más reciente hacia atrás (la mejora de un reestructurado por tramos,
    /// FR-093). La regla exacta de K3 (desde la reestructuración) llega con las novedades; aquí se cuentan las del plan vigente.
    /// </summary>
    public static int CuotasPagadasSeguidas(CreditoDeRiesgo c)
    {
        var seguidas = 0;
        foreach (var cuota in c.Estado.Cuotas.Where(q => q.FechaExigible <= c.Estado.Fecha).OrderByDescending(q => q.Numero))
        {
            if (cuota.CapitalPendiente != 0m) break;
            seguidas++;
        }
        return seguidas;
    }

    /// <summary>
    /// La línea del cierre de un crédito calculado (data-model §23.2): saldos al corte, calificación con candidatas y causa, contable homologada,
    /// deterioro individual por componente, la diferencia frente a la línea anterior y la explicación. El enlace lo pone quien la escribe: la
    /// corrida y su tanda en el mensual; el cierre en el base.
    /// </summary>
    public static MonthlyCloseLoan Linea(RiesgoDeCredito r, int? corridaId, int? tanda, int? cierreId)
    {
        ArgumentNullException.ThrowIfNull(r);
        var c = r.Credito;
        var e = c.Estado;
        var cal = r.Calificacion;
        var det = r.Deterioro;
        var anterior = c.LineaAnterior;
        var explicacion = new ExplicacionDeCartera().Agregar(cal.Explicacion).Agregar(det.Explicacion);
        return new MonthlyCloseLoan
        {
            ProcessRunId = corridaId,
            BatchNumber = tanda,
            MonthlyCloseId = cierreId,
            LoanId = c.Credito.Id,
            PersonId = c.Credito.DebtorPersonId,
            OfficePublicId = c.OficinaPublicId,
            BranchId = c.Credito.BranchId,
            CreditLineId = c.Credito.CreditLineId,
            ModalityCode = c.Vigentes.ModalityCode,
            SubsegmentCode = c.Vigentes.SubsegmentCode,
            SarcSegmentCode = c.Combinacion.SarcSegment,
            ExpectedLossSegmentCode = c.Combinacion.ExpectedLossSegment,
            ImpairmentModel = r.Modelo.Modelo,
            ModelBasis = r.Modelo.Fundamento,
            IsEmployee = c.DeudorEsEmpleado,
            IsRestructured = c.Vigentes.RestructuringCount > 0,
            RestructuringCount = c.Vigentes.RestructuringCount,
            IsSuspended = c.Suspendido,
            IsWrittenOff = c.Credito.Status == LoanStatus.WrittenOff,
            DaysPastDueAtClose = c.DiasDeMora,
            MaxDaysPastDueInMonth = c.MaxDiasDeMoraDelMes,
            MonthsInE = cal.Categoria == RatingCategory.E ? c.MesesEnE : null,
            ModelRating = cal.CategoriaDelModelo,
            ArrearsRating = cal.CategoriaPorMora,
            FinalRating = cal.Categoria,
            FinalSubcategory = cal.Subcategoria,
            AccountingRating = cal.Contable,
            RatingCause = cal.Causa,
            CandidatesJson = JsonSerializer.Serialize(cal.Candidatas.Select(x => new { category = x.Categoria.ToString(), cause = x.Causa.ToString(), detail = x.Detalle }), Json),
            PreviousAccountingRating = anterior?.AccountingRating,
            PrincipalBalance = e.Saldo(LoanConcept.Principal),
            CurrentInterestBalance = e.Saldo(LoanConcept.CurrentInterest),
            DefaultInterestBalance = e.Saldo(LoanConcept.DefaultInterest),
            SuspendedInterestBalance = e.Saldo(LoanConcept.CurrentInterestInSuspense) + e.Saldo(LoanConcept.DefaultInterestInSuspense),
            OtherBalance = e.Saldo(LoanConcept.Insurance) + e.Saldo(LoanConcept.CollectionFee) + e.Saldo(LoanConcept.OtherCharge),
            BalancesByConceptJson = JsonSerializer.Serialize(Enum.GetValues<LoanConcept>().ToDictionary(k => k.ToString(), k => e.Saldo(k)), Json),
            CollateralAdmissibleValue = det.ValorAdmisibleDeGarantias,
            ContributionsDeducted = det.AportesDescontados,
            ImpairmentBase = det.Base,
            ImpairmentPercentage = det.Porcentaje,
            PrincipalImpairment = det.DeterioroCapital,
            InterestImpairment = det.DeterioroIntereses,
            OtherImpairment = det.DeterioroOtros,
            TotalImpairment = det.Total,
            PreviousTotalImpairment = anterior?.TotalImpairment ?? 0m,
            ImpairmentDelta = det.Total - (anterior?.TotalImpairment ?? 0m),
            ExplanationJson = JsonSerializer.Serialize(explicacion.Lineas, Json),
        };
    }
}
