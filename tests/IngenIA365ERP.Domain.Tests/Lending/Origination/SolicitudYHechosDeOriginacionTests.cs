using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Scoring;

namespace IngenIA365ERP.Domain.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0388 (data-model §2.1–§2.15, §8.2): el comportamiento de la solicitud —estados y transiciones, edición sólo en borrador o
/// devuelta, aprobación con condiciones y vencimiento calculado— y de los hechos de la originación: validación con su huella, puntaje,
/// resultado de listas con su liberación única, consulta a centrales vigente, versiones de cupo, bloqueos con su levantamiento, preferencia
/// de abono de un crédito o de un cupo y la carga de aportes que usa el cupo.
/// </summary>
public class SolicitudYHechosDeOriginacionTests
{
    private static readonly DateTime Ahora = new(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc);

    private static CreditApplication Solicitud(CreditApplicationStatus estado = CreditApplicationStatus.Draft) =>
        new() { Status = estado, RequestedAmount = 10_000_000m, Kind = ApplicationKind.Loan };

    [Fact]
    public void El_ciclo_de_la_solicitud_sigue_el_diagrama_de_estados()
    {
        var s = Solicitud();
        s.Editable.Should().BeTrue();
        s.Radicar(7, Ahora);
        s.Status.Should().Be(CreditApplicationStatus.Submitted);
        s.SubmittedByUserId.Should().Be(7);
        s.Editable.Should().BeFalse();
        s.ExigirEditable()!.Codigo.Should().Be(CreditApplication.CodigoNoEditable);

        s.Enrutar(1, 2, new string('a', 64), 12_000_000m, vinculado: false, Ahora);
        s.Status.Should().Be(CreditApplicationStatus.PendingDecision);
        s.ConsolidatedExposure.Should().Be(12_000_000m);

        s.Devolver();
        s.Status.Should().Be(CreditApplicationStatus.Returned);
        s.Editable.Should().BeTrue();
        s.Radicar(7, Ahora.AddHours(1));
        s.Enrutar(1, 2, new string('b', 64), 12_000_000m, false, Ahora.AddHours(1));

        s.Aprobar(55, 8_000_000m, "{\"amount\":8000000}", new DateOnly(2026, 12, 31));
        s.Status.Should().Be(CreditApplicationStatus.Approved);
        s.Decidida.Should().BeTrue();
        s.AprobacionVencida(new DateOnly(2026, 12, 31)).Should().BeFalse();
        s.AprobacionVencida(new DateOnly(2027, 1, 1)).Should().BeTrue("vencida se calcula, no es estado (C53)");

        // Aprobación vencida o reenrutamiento: vuelve a PendingDecision y pierde la aprobación.
        s.Enrutar(1, 3, new string('c', 64), 15_000_000m, false, Ahora.AddDays(40));
        s.Status.Should().Be(CreditApplicationStatus.PendingDecision);
        s.ApprovalDecisionId.Should().BeNull();
        s.ApprovedAmount.Should().BeNull();
    }

    [Fact]
    public void Las_transiciones_que_el_diagrama_no_admite_se_rechazan()
    {
        CreditApplication.PuedePasar(CreditApplicationStatus.Draft, CreditApplicationStatus.Approved).Should().BeFalse();
        CreditApplication.PuedePasar(CreditApplicationStatus.Denied, CreditApplicationStatus.Cancelled).Should().BeFalse();
        CreditApplication.PuedePasar(CreditApplicationStatus.Approved, CreditApplicationStatus.Cancelled).Should().BeTrue();

        var negada = Solicitud(CreditApplicationStatus.Denied);
        negada.PuedeIrA(CreditApplicationStatus.Submitted)!.Codigo.Should().Be(CreditApplication.CodigoEstadoInvalido);
        var accion = () => negada.Radicar(1, Ahora);
        accion.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(CreditApplication.CodigoEstadoInvalido);

        var pendiente = Solicitud(CreditApplicationStatus.PendingDecision);
        var deMas = () => pendiente.Aprobar(1, 11_000_000m, null, new DateOnly(2027, 1, 1));
        deMas.Should().Throw<ReglaDeCarteraException>("con condiciones el monto puede ser menor, nunca mayor");
    }

    [Fact]
    public void Anular_exige_motivo_y_no_aplica_a_lo_desembolsado()
    {
        var s = Solicitud();
        s.Cancelar(3, Ahora, "  Desiste el asociado ");
        s.Status.Should().Be(CreditApplicationStatus.Cancelled);
        s.CancelReason.Should().Be("Desiste el asociado");

        var desembolsada = Solicitud(CreditApplicationStatus.Disbursed);
        var accion = () => desembolsada.Cancelar(3, Ahora, "x");
        accion.Should().Throw<ReglaDeCarteraException>();
        var sinMotivo = () => Solicitud().Cancelar(3, Ahora, " ");
        sinMotivo.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_cupo_se_otorga_y_un_credito_se_desembolsa()
    {
        var cupo = Solicitud(CreditApplicationStatus.Approved);
        cupo.Kind = ApplicationKind.RevolvingLimit;
        var mal = () => cupo.Desembolsar(1);
        mal.Should().Throw<ReglaDeCarteraException>();
        cupo.Otorgar(9);
        cupo.Status.Should().Be(CreditApplicationStatus.Granted);
        cupo.CreditLimitId.Should().Be(9);

        var credito = Solicitud(CreditApplicationStatus.Approved);
        credito.Desembolsar(4);
        credito.LoanId.Should().Be(4);
    }

    [Fact]
    public void Una_persona_no_esta_dos_veces_en_la_solicitud()
    {
        CreditApplicationParty.ValidarPartes([(1, CreditPartyRole.Applicant), (2, CreditPartyRole.CoDebtor)]).Should().BeNull();
        var problema = CreditApplicationParty.ValidarPartes([(1, CreditPartyRole.Applicant), (2, CreditPartyRole.CoDebtor), (2, CreditPartyRole.CoDebtor)],
            id => $"persona-{id}");
        problema!.Codigo.Should().Be(CreditApplicationParty.CodigoParteRepetida);
        problema.Datos!["personPublicId"].Should().Be("persona-2");
        CreditApplicationParty.ValidarPartes([(1, CreditPartyRole.Applicant), (1, CreditPartyRole.CoDebtor)]).Should().NotBeNull();
    }

    [Fact]
    public void Un_documento_rechazado_o_viejo_no_cuenta()
    {
        var hoy = new DateOnly(2026, 12, 1);
        CreditApplicationDocument.Nuevo(1, 2, null, Guid.NewGuid(), hoy.AddDays(-10)).Cuenta(hoy, 30).Should().BeTrue();
        CreditApplicationDocument.Nuevo(1, 2, null, Guid.NewGuid(), hoy.AddDays(-31)).Cuenta(hoy, 30).Should().BeFalse();
        CreditApplicationDocument.Nuevo(1, 2, null, Guid.NewGuid(), null).Cuenta(hoy, 30).Should().BeFalse("sin fecha no se sabe su antigüedad");
        CreditApplicationDocument.Nuevo(1, 2, null, Guid.NewGuid(), null).Cuenta(hoy, null).Should().BeTrue();
        var rechazado = CreditApplicationDocument.Nuevo(1, 2, null, Guid.NewGuid(), hoy);
        rechazado.RejectionReason = "Ilegible";
        rechazado.Cuenta(hoy, null).Should().BeFalse();
    }

    [Fact]
    public void La_validacion_lleva_su_huella_y_la_vigente_es_la_ultima()
    {
        var a = CreditApplicationCheck.Registrar(1, 2, ApplicationCheckKind.PaymentCapacity, CheckStage.AtSubmission, CheckOutcome.Blocking, false,
            "Lending.Capacity.PaymentInsufficient", "{\"available\":-10}", Ahora);
        var b = CreditApplicationCheck.Registrar(1, 2, ApplicationCheckKind.PaymentCapacity, CheckStage.AtSubmission, CheckOutcome.Blocking, false,
            "Lending.Capacity.PaymentInsufficient", "{\"available\":-10}", Ahora);
        a.EvidenceSha256.Should().MatchRegex("^[0-9a-f]{64}$").And.Be(b.EvidenceSha256, "mismas entradas, misma huella");
        a.Bloquea.Should().BeTrue();

        var despues = CreditApplicationCheck.Registrar(1, 2, ApplicationCheckKind.PaymentCapacity, CheckStage.AtSubmission, CheckOutcome.Passed, false,
            null, "{\"available\":10}", Ahora.AddMinutes(5));
        despues.EvidenceSha256.Should().NotBe(a.EvidenceSha256);
        var otraEtapa = CreditApplicationCheck.Registrar(1, 2, ApplicationCheckKind.PaymentCapacity, CheckStage.BeforeDecision, CheckOutcome.Warning, false,
            null, null, Ahora);
        CreditApplicationCheck.Vigentes([a, despues, otraEtapa]).Should().HaveCount(2).And.Contain(despues).And.Contain(otraEtapa);
        otraEtapa.EsAlerta.Should().BeTrue();
    }

    [Fact]
    public void El_puntaje_se_sella_con_su_explicacion()
    {
        var modelo = new ModeloDePuntaje("EXCEP",
            [new VariableDePuntaje("CENTRAL", "Central", FuentesDePuntaje.PuntajeDeCentrales, 1m, [new RangoDePuntaje(0m, null, 800m)])]);
        var r = MotorDePuntaje.Calcular(modelo, 900m, new Dictionary<string, decimal?> { [FuentesDePuntaje.PuntajeDeCentrales] = 700m });
        var puntaje = CreditScore.Registrar(1, 2, 3, r, Ahora);
        puntaje.Score.Should().Be(800m);
        puntaje.BelowMinimum.Should().BeTrue();
        puntaje.MinimumScore.Should().Be(900m);
        puntaje.InputsSha256.Should().Be(r.HuellaDeEntradas);
        puntaje.Explanation.Should().Contain("\"code\":\"CENTRAL\"").And.Contain("\"points\":800");
    }

    [Fact]
    public void Una_coincidencia_se_libera_una_sola_vez_y_con_motivo()
    {
        var r = new ScreeningResult { Outcome = ScreeningOutcome.PossibleMatch, ChannelCode = "CARGA-PROPIA" };
        r.Bloquea.Should().BeTrue();
        r.CuentaComoConsulta(enProduccion: true).Should().BeTrue();
        r.Liberar(9, Ahora, "Homónimo verificado con el documento");
        r.Bloquea.Should().BeFalse();
        r.ReleasedByUserId.Should().Be(9);
        var otraVez = () => r.Liberar(9, Ahora, "x");
        otraVez.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(ScreeningResult.CodigoNoPendiente);

        new ScreeningResult { Outcome = ScreeningOutcome.NoMatch }.PuedeLiberar()!.Codigo.Should().Be(ScreeningResult.CodigoNoPendiente);
        new ScreeningResult { Outcome = ScreeningOutcome.Unavailable }.CuentaComoConsulta(false).Should().BeFalse();
        var simulada = new ScreeningResult { Outcome = ScreeningOutcome.NoMatch, IsSimulated = true };
        simulada.CuentaComoConsulta(enProduccion: true).Should().BeFalse("en producción un simulado no cuenta (T73)");
        simulada.CuentaComoConsulta(enProduccion: false).Should().BeTrue();
    }

    [Fact]
    public void La_consulta_a_centrales_vale_hasta_su_vigencia()
    {
        var vieja = new BureauQuery { PersonId = 1, Outcome = ExternalOutcome.Ok, QueriedAt = Ahora.AddDays(-40), ValidUntil = new DateOnly(2026, 11, 20) };
        var nueva = new BureauQuery { PersonId = 1, Outcome = ExternalOutcome.Ok, QueriedAt = Ahora.AddDays(-5), ValidUntil = new DateOnly(2026, 12, 26) };
        var deOtro = new BureauQuery { PersonId = 2, Outcome = ExternalOutcome.Ok, QueriedAt = Ahora, ValidUntil = new DateOnly(2027, 1, 1) };
        BureauQuery.UltimaVigente([vieja, nueva, deOtro], 1, new DateOnly(2026, 12, 1)).Should().BeSameAs(nueva);
        BureauQuery.UltimaVigente([vieja], 1, new DateOnly(2026, 12, 1)).Should().BeNull();
        new BureauQuery { Outcome = ExternalOutcome.ChannelUnavailable }.Respondio.Should().BeFalse();
    }

    [Fact]
    public void El_cupo_se_versiona_y_valida_su_forma()
    {
        CreditLimit.Validar(CreditLimitKind.ByLine, null, 1m, "m")!.Datos!["field"].Should().Be("creditLinePublicId");
        CreditLimit.Validar(CreditLimitKind.Consolidated, 3, 1m, "m").Should().NotBeNull();
        CreditLimit.Validar(CreditLimitKind.Revolving, null, 1m, "m").Should().BeNull("el rotativo sin línea vale para toda línea comercial (C54)");
        CreditLimit.Validar(CreditLimitKind.Consolidated, null, -1m, "m").Should().NotBeNull();
        var v2 = CreditLimit.NuevaVersion(1, CreditLimitKind.Revolving, null, 500_000m, CreditLimitOrigin.Assigned, new DateOnly(2026, 12, 1), 2, Ahora,
            " Otorgado ", 7);
        v2.VersionNumber.Should().Be(2);
        v2.Reason.Should().Be("Otorgado");
        v2.EsDelMismoCupo(1, CreditLimitKind.Revolving, null).Should().BeTrue();
    }

    [Fact]
    public void Un_bloqueo_se_levanta_una_vez_y_los_manuales_llevan_motivo()
    {
        var manual = () => DebtorBlock.Nuevo(1, DebtorBlockReason.Manual, "x", new DateOnly(2026, 12, 1), null, 7);
        manual.Should().Throw<ArgumentException>();
        var embargo = DebtorBlock.Nuevo(1, DebtorBlockReason.Garnishment, "oficio-1", new DateOnly(2026, 12, 1), "Embargo del juzgado 3", 7);
        embargo.IsAutomatic.Should().BeFalse();
        var listas = DebtorBlock.Nuevo(1, DebtorBlockReason.RestrictiveListMatch, Guid.NewGuid().ToString(), new DateOnly(2026, 12, 1), null, null);
        listas.IsAutomatic.Should().BeTrue();
        listas.Vigente.Should().BeTrue();
        listas.Levantar(new DateOnly(2026, 12, 2), Ahora, 9, "Liberado por el oficial");
        listas.Vigente.Should().BeFalse();
        var otra = () => listas.Levantar(new DateOnly(2026, 12, 3), Ahora, 9, "x");
        otra.Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void La_preferencia_es_de_un_credito_o_de_un_cupo()
    {
        var sinAmbito = () => PrepaymentPreference.Registrar(1, null, null, PrepaymentChoice.ReduceTerm, 1, Ahora, PreferenceOrigin.Application, 7);
        sinAmbito.Should().Throw<ArgumentException>("CK_LND_PrepaymentPreferences_Scope");
        var conDos = () => PrepaymentPreference.Registrar(1, 2, 3, PrepaymentChoice.ReduceTerm, 1, Ahora, PreferenceOrigin.Application, 7);
        conDos.Should().Throw<ArgumentException>();
        var v1 = PrepaymentPreference.Registrar(1, null, 3, PrepaymentChoice.ReduceTerm, 1, Ahora, PreferenceOrigin.CreditLimitGrant, 7);
        var v2 = PrepaymentPreference.Registrar(1, null, 3, null, 2, Ahora.AddDays(1), PreferenceOrigin.DebtorRequest, 7);
        PrepaymentPreference.Vigente([v1, v2], Ahora.AddHours(1)).Should().BeSameAs(v1);
        PrepaymentPreference.Vigente([v1, v2], Ahora.AddDays(2))!.Choice.Should().BeNull("revocada");
    }

    [Fact]
    public void El_cupo_usa_la_ultima_carga_de_aportes_conocida()
    {
        var lote = Guid.NewGuid();
        var a = ContributionBalanceLoad.Registrar(1, new DateOnly(2026, 10, 31), 1_000_000m, 0m, lote, Ahora.AddDays(-20), 7);
        var b = ContributionBalanceLoad.Registrar(1, new DateOnly(2026, 11, 30), 1_200_000m, 0m, lote, Ahora.AddDays(-1), 7);
        var futura = ContributionBalanceLoad.Registrar(1, new DateOnly(2026, 12, 31), 9m, 0m, lote, Ahora.AddDays(-1), 7);
        ContributionBalanceLoad.Vigente([a, b, futura], new DateOnly(2026, 12, 1), Ahora).Should().BeSameAs(b);
        ContributionBalanceLoad.Vigente([a, b], new DateOnly(2026, 12, 1), Ahora.AddDays(-10)).Should().BeSameAs(a, "lo que se sabía en R");
        var negativa = () => ContributionBalanceLoad.Registrar(1, new DateOnly(2026, 11, 30), -1m, 0m, lote, Ahora, 7);
        negativa.Should().Throw<ArgumentException>();
    }
}
