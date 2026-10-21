using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Domain.Tests.Lending.Operations;

/// <summary>
/// Feature 013, K3 (T0872; data-model §25.6, §23.7): las transiciones del lote de castigo, de sus ítems y de la evaluación de cartera, que
/// viajan con <c>CarteraNovedades</c>; su comportamiento completo (comandos, actas, cierre) lo construye la fase de US14.
/// </summary>
public class WriteOffAndEvaluationTests
{
    private static readonly DateTime Ahora = new(2026, 12, 20, 9, 0, 0);

    private static WriteOffBatchItem Item(bool cumple, decimal capital = 1_000_000m)
    {
        var verificacion = RequisitosDeCastigo.Verificar(
            new CreditoParaCastigar(1, DeudorEsAsociado: !cumple, capital, capital, 400)
            {
                AportesCruzados = true,
                InformeMotivado = Guid.NewGuid(),
                ConceptoDeIrrecuperabilidad = Guid.NewGuid(),
            },
            new PoliticaDeCastigo(true, 360, 0));
        return WriteOffBatchItem.Desde(1, verificacion, capital, 80_000m, 5_000m, capital, null, null);
    }

    [Fact]
    public void El_item_nace_incluido_o_rechazado_con_lo_que_falta()
    {
        var incluido = Item(cumple: true);
        (incluido.Status, incluido.RejectionReason).Should().Be((WriteOffItemStatus.Included, (string?)null));

        var rechazado = Item(cumple: false);
        rechazado.Status.Should().Be(WriteOffItemStatus.Rejected);
        rechazado.RejectionReason.Should().Contain("el deudor sigue siendo asociado");
        rechazado.RequirementsJson.Should().Contain("NotAnAssociate");
        ((Action)(() => rechazado.Castigar(new LoanOperation { Kind = LoanOperationKind.WriteOff }))).Should().Throw<ReglaDeCarteraException>();

        var operacion = new LoanOperation { Kind = LoanOperationKind.WriteOff };
        incluido.Castigar(operacion);
        (incluido.Status, incluido.WriteOffOperation).Should().Be((WriteOffItemStatus.WrittenOff, operacion));
    }

    [Fact]
    public void El_lote_se_arma_en_borrador_lo_aprueba_el_consejo_y_se_aplica_una_vez()
    {
        var lote = new WriteOffBatch { Number = "CAS-1", CutoffDate = new DateOnly(2026, 12, 15), Reason = "Cartera irrecuperable", PreparedByUserId = 3 };
        ((Action)lote.Enviar).Should().Throw<ReglaDeCarteraException>("sin créditos que cumplan");

        lote.Recalcular([Item(true, 1_000_000m), Item(true, 500_000m), Item(false, 900_000m)]);
        (lote.LoanCount, lote.TotalPrincipal, lote.TotalInterest, lote.TotalOther).Should().Be((2, 1_500_000m, 160_000m, 10_000m), "los rechazados no cuentan");

        ((Action)(() => lote.Aplicar(9, Ahora, false, null))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(WriteOffBatch.CodigoAprobacionRequerida);
        lote.Enviar();
        ((Action)(() => lote.Recalcular([]))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(WriteOffBatch.CodigoNoBorrador);
        lote.Aprobar(puntoDeActaId: 77);
        ((Action)(() => lote.Aplicar(9, Ahora, true, null))).Should().Throw<ArgumentException>("el reporte al supervisor lleva su plazo");
        lote.Aplicar(9, Ahora, reporteRequerido: true, plazoDelReporte: new DateOnly(2027, 1, 30));
        (lote.Status, lote.SessionItemId, lote.SupervisorReportRequired, lote.SupervisorReportDueDate)
            .Should().Be((WriteOffBatchStatus.Applied, (int?)77, true, (DateOnly?)new DateOnly(2027, 1, 30)));

        lote.RegistrarReporteAlSupervisor(Ahora.AddDays(5), " RAD-2027-001 ", Guid.NewGuid());
        lote.SupervisorReportReference.Should().Be("RAD-2027-001");
        ((Action)(() => lote.RegistrarReporteAlSupervisor(Ahora.AddDays(6), "otro", null))).Should().Throw<ReglaDeCarteraException>("se registra una sola vez");
    }

    [Fact]
    public void Un_lote_rechazado_por_el_consejo_no_se_aplica()
    {
        var lote = new WriteOffBatch { Number = "CAS-2" };
        lote.Recalcular([Item(true)]);
        lote.Enviar();
        lote.Rechazar(puntoDeActaId: 78);
        lote.Status.Should().Be(WriteOffBatchStatus.Rejected);
        ((Action)(() => lote.Aplicar(9, Ahora, false, null))).Should().Throw<ReglaDeCarteraException>();
        ((Action)lote.Cancelar).Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void La_evaluacion_la_recomienda_el_comite_la_aprueba_el_consejo_y_rige_desde_su_mes_de_efecto()
    {
        var evaluacion = new PortfolioEvaluation { Number = "EVA-1", CutoffDate = new DateOnly(2026, 11, 30), EffectiveYear = 2026, EffectiveMonth = 12 };
        var linea = PortfolioEvaluationLoan.Nueva(evaluacion.Id, 5, RatingCategory.A, RatingCategory.C, null, "Sobreendeudamiento en centrales");
        evaluacion.Recalcular([linea]);
        evaluacion.EditarLinea(linea, RatingCategory.E, RatingSubcategory.E1, "Pérdida del empleo");
        (linea.ProposedRating, linea.ProposedSubcategory).Should().Be((RatingCategory.E, (RatingSubcategory?)RatingSubcategory.E1));
        ((Action)(() => evaluacion.EditarLinea(linea, RatingCategory.B, RatingSubcategory.E2, "x"))).Should().Throw<ArgumentException>("sólo E lleva subcategoría");

        evaluacion.Recomendar(puntoDelComiteId: 40);
        ((Action)(() => evaluacion.EditarLinea(linea, RatingCategory.D, null, "tarde"))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(PortfolioEvaluation.CodigoNoBorrador);
        ((Action)(() => evaluacion.Aplicar(9, Ahora))).Should().Throw<ReglaDeCarteraException>("falta el consejo");
        evaluacion.Aprobar(puntoDelConsejoId: 41);
        evaluacion.RigeAlCierre(2026, 12).Should().BeFalse("todavía no se aplica");
        evaluacion.Aplicar(9, Ahora);

        (evaluacion.Status, evaluacion.RecommendationSessionItemId, evaluacion.ApprovalSessionItemId)
            .Should().Be((PortfolioEvaluationStatus.Applied, (int?)40, (int?)41));
        evaluacion.RigeAlCierre(2026, 11).Should().BeFalse("nunca reescribe cierres anteriores al mes de efecto");
        evaluacion.RigeAlCierre(2026, 12).Should().BeTrue();
        evaluacion.RigeAlCierre(2027, 3).Should().BeTrue();
    }

    [Fact]
    public void La_evaluacion_se_rechaza_o_se_descarta()
    {
        var porElComite = new PortfolioEvaluation { Number = "EVA-2" };
        porElComite.Rechazar(puntoDeActaId: 50);
        (porElComite.Status, porElComite.RecommendationSessionItemId).Should().Be((PortfolioEvaluationStatus.Rejected, (int?)50));

        var porElConsejo = new PortfolioEvaluation { Number = "EVA-3" };
        porElConsejo.Recalcular([PortfolioEvaluationLoan.Nueva(0, 6, RatingCategory.B, RatingCategory.C, null, "Mora en otras entidades")]);
        porElConsejo.Recomendar(51);
        porElConsejo.Rechazar(52);
        (porElConsejo.Status, porElConsejo.ApprovalSessionItemId).Should().Be((PortfolioEvaluationStatus.Rejected, (int?)52));

        var descartada = new PortfolioEvaluation { Number = "EVA-4" };
        descartada.Descartar();
        descartada.Status.Should().Be(PortfolioEvaluationStatus.Discarded);
        ((Action)(() => descartada.Recomendar(1))).Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void Al_aplicar_el_que_ya_no_cumple_se_excluye_con_su_motivo_y_los_totales_quedan_con_los_que_siguen()
    {
        // Bloque D (decisión del dueño del 2026-10-15).
        var lote = new WriteOffBatch { Number = "CAS-3" };
        var sigue = Item(true, 1_000_000m);
        var yaNo = Item(true, 500_000m);
        lote.Recalcular([sigue, yaNo]);
        lote.Enviar();
        lote.Aprobar(79);

        yaNo.Excluir("Al aplicar ya no cumple: el deudor volvió a ser asociado", "{\"x\":1}");
        (yaNo.Status, yaNo.RejectionReason, yaNo.RequirementsJson)
            .Should().Be((WriteOffItemStatus.Rejected, "Al aplicar ya no cumple: el deudor volvió a ser asociado", "{\"x\":1}"));
        ((Action)(() => yaNo.Excluir("otra vez"))).Should().Throw<ReglaDeCarteraException>("sólo se excluye uno incluido");
        sigue.Castigar(new LoanOperation { Kind = LoanOperationKind.WriteOff });
        lote.RecalcularAlAplicar([sigue, yaNo]);
        (lote.LoanCount, lote.TotalPrincipal).Should().Be((1, 1_000_000m));
        lote.Aplicar(9, Ahora, false, null);
        ((Action)(() => lote.RecalcularAlAplicar([sigue]))).Should().Throw<ReglaDeCarteraException>("ya se aplicó");
    }

    [Fact]
    public void El_lote_se_anula_mientras_no_se_aplique_y_la_evaluacion_se_descarta_mientras_no_se_aplique()
    {
        var aprobado = new WriteOffBatch { Number = "CAS-4" };
        aprobado.Recalcular([Item(true)]);
        aprobado.Enviar();
        aprobado.Aprobar(80);
        aprobado.Cancelar();
        aprobado.Status.Should().Be(WriteOffBatchStatus.Cancelled);

        var aplicado = new WriteOffBatch { Number = "CAS-5" };
        aplicado.Recalcular([Item(true)]);
        aplicado.Enviar();
        aplicado.Aprobar(81);
        aplicado.Aplicar(9, Ahora, false, null);
        ((Action)aplicado.Cancelar).Should().Throw<ReglaDeCarteraException>("lo aplicado no se anula");

        var aprobada = new PortfolioEvaluation { Number = "EVA-5" };
        aprobada.Recalcular([PortfolioEvaluationLoan.Nueva(0, 6, RatingCategory.B, RatingCategory.C, null, "Mora en otras entidades")]);
        aprobada.Recomendar(53);
        aprobada.Aprobar(54);
        aprobada.Descartar();
        aprobada.Status.Should().Be(PortfolioEvaluationStatus.Discarded);

        var aplicada = new PortfolioEvaluation { Number = "EVA-6" };
        aplicada.Recalcular([PortfolioEvaluationLoan.Nueva(0, 6, RatingCategory.B, RatingCategory.C, null, "Mora en otras entidades")]);
        aplicada.Recomendar(55);
        aplicada.Aprobar(56);
        aplicada.Aplicar(9, Ahora);
        ((Action)aplicada.Descartar).Should().Throw<ReglaDeCarteraException>("lo aplicado no se descarta");
    }
}
