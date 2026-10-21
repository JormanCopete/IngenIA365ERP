using System.Text.Json;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// La calificación y el deterioro de un crédito de la tanda, con lo que el riesgo cargó de él. En un segmento en pérdida esperada (K6a) lleva el
/// puntaje, el historial completado y la pérdida esperada, y <see cref="Deterioro"/> es la pérdida esperada por concepto. (nuevo)
/// </summary>
public sealed record RiesgoDeCredito(CreditoDeRiesgo Credito, CalificacionDeCredito Calificacion, DeterioroIndividual Deterioro, ModeloDelSegmento Modelo)
{
    public PuntajeDelModelo? Puntaje { get; init; }

    public HistorialDeMoraCompletado? Historial { get; init; }

    public PerdidaEsperadaDeCredito? PerdidaEsperada { get; init; }

    /// <summary>El historial no estaba completo y sus meses faltantes se completaron con la regla.</summary>
    public bool HistorialIncompleto => Historial?.ReglaAplicada is not null;
}

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
    /// <param name="perdidaEsperada">K6a (T1036): los datos de la pérdida esperada de los créditos cuyo segmento está en ese modelo
    /// (<see cref="CargadorDePerdidaEsperada"/>); sin ellos, todo va por el Anexo 1.</param>
    public static RiesgoDeLaTanda Calcular(DatosDeRiesgo datos, ContextoDeRiesgo contexto, bool conArrastre, bool sinAnalisis = false,
        IReadOnlyDictionary<int, DatosDePerdidaEsperada>? perdidaEsperada = null)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(contexto);
        var errores = new Dictionary<int, Error>(datos.Errores);

        // K6a: el puntaje de cada crédito en pérdida esperada, también de los del grupo que no son de la tanda (la candidata del modelo entra al
        // arrastre). El historial se completa con la regla de meses faltantes; un crédito que no se puede puntuar queda con su error.
        var puntajes = new Dictionary<int, (PuntajeDelModelo Puntaje, HistorialDeMoraCompletado Historial)>();
        if (contexto.PerdidaEsperada is { } pe && perdidaEsperada is not null)
        {
            foreach (var c in datos.Creditos.Values)
            {
                var modelo = contexto.Seleccion.ParaCredito(c.Combinacion.ExpectedLossSegment);
                if (modelo.Modelo != ImpairmentModel.ExpectedLossAnnex2 || !perdidaEsperada.TryGetValue(c.Credito.Id, out var d)) continue;
                try
                {
                    puntajes[c.Credito.Id] = Puntuar(pe, modelo, c, d);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    if (datos.Tanda.Contains(c.Credito.Id)) errores[c.Credito.Id] = new Error("Lending.Risk.ExpectedLossFailed", ex.Message);
                }
            }
        }

        // Feature 014 (T084): los aportes del deudor al corte y su prorrata por capital entre sus créditos vivos del grupo.
        var capitalPorDeudor = datos.Creditos.Values.GroupBy(c => c.Credito.DebtorPersonId)
            .ToDictionary(g => g.Key, g => g.Sum(c => Math.Max(0m, c.Estado.Saldo(LoanConcept.Principal))));
        decimal Prorrata(CreditoDeRiesgo c)
        {
            var aportes = datos.AportesPorDeudor.GetValueOrDefault(c.Credito.DebtorPersonId);
            var total = capitalPorDeudor.GetValueOrDefault(c.Credito.DebtorPersonId);
            return aportes <= 0m || total <= 0m
                ? 0m
                : decimal.Round(aportes * Math.Max(0m, c.Estado.Saldo(LoanConcept.Principal)) / total, 2, MidpointRounding.AwayFromZero);
        }

        var paraCalificar = datos.Creditos.Values.Select(c =>
        {
            var p = ParaCalificar(c, contexto, datos.CierreAnterior is not null, sinAnalisis) with
            {
                Capital = c.Estado.Saldo(LoanConcept.Principal),
                AportesDelDeudor = datos.AportesPorDeudor.GetValueOrDefault(c.Credito.DebtorPersonId),
            };
            return puntajes.TryGetValue(c.Credito.Id, out var x) ? p with { CalificacionDelModelo = x.Puntaje.CandidataParaCalificar } : p;
        }).ToList();
        var opciones = new OpcionesDeCalificacion(conArrastre, contexto.Aportes);
        var calificaciones = MotorDeCalificacion.Calificar(contexto.Norma, paraCalificar, opciones).ToDictionary(c => c.LoanId);

        // K6a: la alineación del Anexo 2 (num. 4.1 y 5) por deudor y segmento SARC entre los créditos en pérdida esperada.
        var alinear = puntajes.Select(x => new CreditoParaAlinear(x.Key, datos.Creditos[x.Key].Credito.DebtorPersonId, datos.Creditos[x.Key].Combinacion.SarcSegment,
            datos.Creditos[x.Key].DiasDeMora, x.Value.Puntaje.Incumplido)).ToList();
        var porArrastre = MotorDePerdidaEsperada.IncumplidosPorArrastre(alinear);
        var moraActual = MotorDePerdidaEsperada.MoraActualPorDeudor(alinear);

        var calculados = new List<RiesgoDeCredito>();
        foreach (var id in datos.Tanda.Order())
        {
            if (errores.ContainsKey(id) && datos.Creditos.ContainsKey(id)) continue;
            if (!datos.Creditos.TryGetValue(id, out var credito) || !calificaciones.TryGetValue(id, out var calificacion))
            {
                if (!errores.ContainsKey(id)) errores[id] = new Error("Lending.Risk.LoanNotLoaded", "El crédito no está vivo al corte o no se pudo cargar.");
                continue;
            }
            var modelo = contexto.Seleccion.ParaCredito(credito.Combinacion.ExpectedLossSegment);
            try
            {
                if (puntajes.TryGetValue(id, out var x) && contexto.PerdidaEsperada is { } ctx)
                {
                    var perdida = CalcularPerdida(ctx, modelo, credito, calificacion, x.Puntaje, perdidaEsperada![id], porArrastre.Contains(id), moraActual[id],
                        contexto.Aportes, contexto.Redondeo, Prorrata(credito), contexto.Norma.Aportes.Deducibles);
                    calculados.Add(new RiesgoDeCredito(credito, calificacion, ComoDeterioro(perdida, credito), modelo)
                    {
                        Puntaje = x.Puntaje, Historial = x.Historial, PerdidaEsperada = perdida,
                    });
                    continue;
                }
                var deterioro = MotorDeDeterioroIndividual.Calcular(contexto.Norma, ParaDeteriorar(credito, calificacion) with { AportesAProrrata = Prorrata(credito) },
                    contexto.Aportes, contexto.Redondeo);
                calculados.Add(new RiesgoDeCredito(credito, calificacion, deterioro, modelo));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ReglaDeCarteraException or NotSupportedException)
            {
                errores[id] = new Error(ex is ReglaDeCarteraException rc ? rc.Codigo : "Lending.Risk.ImpairmentFailed", ex.Message);
            }
        }
        return new RiesgoDeLaTanda(calculados, errores);
    }

    /// <summary>K6a: el historial completado con la regla de meses faltantes y el puntaje del Anexo 2 (<see cref="MotorDePerdidaEsperada.Puntuar"/>).</summary>
    private static (PuntajeDelModelo, HistorialDeMoraCompletado) Puntuar(ContextoDePerdidaEsperada pe, ModeloDelSegmento modelo, CreditoDeRiesgo c,
        DatosDePerdidaEsperada d)
    {
        var historial = HistorialDeMora.Completar(d.Historial, d.MoraDeTransacciones, pe.ReglaDeMesesFaltantes, d.DiasDelCorte, pe.DiasFijos);
        var puntaje = MotorDePerdidaEsperada.Puntuar(pe.Efectivo, new CreditoParaPuntaje(c.Credito.Id, modelo.Segmento, c.DiasDeMora, historial.Meses)
        {
            AsociadoActivo = d.AsociadoActivo,
            ValorDeLaCuota = d.ValorDeLaCuota,
            PlazoEnMeses = c.Vigentes.TermMonths,
            PagoUnico = c.Vigentes.AmortizationSystem == AmortizationSystem.Bullet,
        }, pe.Modelo(modelo.Fundamento == ModelBasis.Voluntary));
        return (puntaje, historial);
    }

    /// <summary>
    /// K6a: la pérdida esperada del crédito (<see cref="MotorDePerdidaEsperada.Calcular"/>) con la calificación final de <see cref="MotorDeCalificacion"/>,
    /// la PDI del tipo de garantía más conservador (<see cref="TipoDeGarantiaDePdi.ElMasConservador"/>) y la suspensión de la reconstrucción del
    /// crédito —la causación suspendida por la altura de mora del Anexo 1, como dejó el bloque 1: una calificación C del modelo con el crédito al día
    /// no suspende; pendiente de validar—.
    /// </summary>
    private static PerdidaEsperadaDeCredito CalcularPerdida(ContextoDePerdidaEsperada pe, ModeloDelSegmento modelo, CreditoDeRiesgo c,
        CalificacionDeCredito calificacion, PuntajeDelModelo puntaje, DatosDePerdidaEsperada d, bool porArrastre, int moraActual, ContextoDeAportes aportes,
        RedondeoDeDeterioro redondeo, decimal aportesAProrrata, bool normaAdmiteAportes)
    {
        var incumplido = puntaje.Incumplido || porArrastre;
        var garantia = TipoDeGarantiaDePdi.ElMasConservador(pe.Efectivo, modelo.Segmento, d.TiposDeGarantia, incumplido,
            puntaje.Incumplido ? puntaje.DiasEnIncumplimiento : 0);
        var e = c.Estado;
        var saldos = new SaldosParaDeterioro(e.Saldo(LoanConcept.Principal), e.Saldo(LoanConcept.CurrentInterest), e.Saldo(LoanConcept.DefaultInterest),
            e.Saldo(LoanConcept.Insurance) + e.Saldo(LoanConcept.CollectionFee) + e.Saldo(LoanConcept.OtherCharge));
        return MotorDePerdidaEsperada.Calcular(pe.Efectivo, new CreditoParaPerdidaEsperada(puntaje, calificacion.Categoria, saldos, c.Suspendido, garantia)
        {
            IncumplidoPorArrastre = porArrastre,
            MoraActualDelDeudor = moraActual,
            // Feature 014 (D39): los aportes a prorrata se restan del VEA con las condiciones del Anexo 1, num. 5.1 (las del deterioro individual).
            AportesAProrrata = aportesAProrrata,
            NormaAdmiteAportes = normaAdmiteAportes,
        }, pe.Modelo(modelo.Fundamento == ModelBasis.Voluntary), aportes, redondeo);
    }

    /// <summary>La pérdida esperada en la forma del deterioro individual: base = exposición, porcentaje = PI × PDI, deterioro por concepto.</summary>
    private static DeterioroIndividual ComoDeterioro(PerdidaEsperadaDeCredito p, CreditoDeRiesgo c) =>
        new(p.LoanId, c.DiasDeMora / 30, c.Garantias.Sum(g => g.Valor), p.AportesDescontados, BaseDeDeterioro.TotalBalance, p.Exposicion,
            decimal.Round(p.ProbabilidadDeIncumplimiento * p.PerdidaDadoElIncumplimiento, 6, MidpointRounding.AwayFromZero),
            p.DeterioroCapital, p.DeterioroIntereses, p.DeterioroOtros, p.Explicacion);

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
            // K3 (T0913): la evaluación de cartera aplicada que rige al mes del corte (FR-102); compite como la peor, igual que el análisis.
            Evaluacion = c.Evaluacion is null ? null : new CalificacionImpuesta(c.Evaluacion.ProposedRating, c.Evaluacion.ProposedSubcategory ?? RatingSubcategory.None),
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
        var explicacion = new ExplicacionDeCartera().Agregar(cal.Explicacion);
        if (r.Historial is { } historial) explicacion.Agregar(historial.Explicacion);
        explicacion.Agregar(det.Explicacion);
        var pe = r.PerdidaEsperada;
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
            AccountingRating = pe?.CalificacionContable ?? cal.Contable,
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
            // K6a (T1036): las columnas de la pérdida esperada, nulas en el Anexo 1.
            ProbabilityOfDefault = pe?.ProbabilidadDeIncumplimiento,
            LossGivenDefault = pe?.PerdidaDadoElIncumplimiento,
            ExposureAtDefault = pe?.Exposicion,
            ExpectedLossZ = r.Puntaje?.Z,
            ExpectedLossScore = r.Puntaje?.Puntaje,
            IsInDefault = pe?.Incumplido,
            DaysInDefault = pe?.DiasEnIncumplimiento,
            ProbabilityOfDefaultKey = pe?.ClaveDePi,
            LossGivenDefaultCollateralType = pe?.TipoDeGarantia,
            IncompleteArrearsHistory = pe is null ? null : r.HistorialIncompleto,
            ExplanationJson = JsonSerializer.Serialize(explicacion.Lineas, Json),
        };
    }
}
