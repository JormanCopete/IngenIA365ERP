using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Tests.Lending.Operations;

/// <summary>
/// Feature 013, K3 (T0860; data-model §25.1–§25.5, api.md §11.1–§11.2): <see cref="LoanNovelty"/>, <see cref="LoanNoveltyConsent"/> y
/// <see cref="Waiver"/>. Las transiciones de <see cref="LoanNoveltyStatus"/> y <see cref="WaiverStatus"/>, la edición sólo en borrador, la
/// novedad congelada al aplicarse, la aceptación de un garante con su documento firmado y la resolución de un rechazo sólo con
/// <c>Refused</c>.
/// </summary>
public class NoveltyAndWaiverTests
{
    private static readonly DateTime Ahora = new(2026, 9, 15, 10, 0, 0);

    private static LoanNovelty Novedad(NoveltyMechanism mecanismo = NoveltyMechanism.Extension, NoveltyCause causa = NoveltyCause.Restructuring) =>
        LoanNovelty.Nueva("NOV-0001", 10, mecanismo, causa, "{\"cause\":1}", "Dificultades de pago", "{\"installments\":18}",
            RatingCategory.C, usuarioId: 5, Ahora);

    private static LoanNovelty Aprobada(NoveltyMechanism mecanismo = NoveltyMechanism.Extension)
    {
        var n = Novedad(mecanismo);
        n.RegistrarSimulacion("{\"plan\":[]}", new string('a', 64));
        n.Radicar();
        n.Aprobar(decisionId: 3, "{\"rate\":0.24}");
        return n;
    }

    private static LoanOperation Operacion(LoanOperationKind clase) => new() { Kind = clase, LoanId = 10, Number = "OP-1" };

    // ------------------------------------------------------------------------------------------------------------- novedad --

    [Theory]
    [InlineData(LoanNoveltyStatus.Draft, LoanNoveltyStatus.Submitted, true)]
    [InlineData(LoanNoveltyStatus.Draft, LoanNoveltyStatus.Cancelled, true)]
    [InlineData(LoanNoveltyStatus.Draft, LoanNoveltyStatus.Approved, false)]
    [InlineData(LoanNoveltyStatus.Submitted, LoanNoveltyStatus.Approved, true)]
    [InlineData(LoanNoveltyStatus.Submitted, LoanNoveltyStatus.Rejected, true)]
    [InlineData(LoanNoveltyStatus.Submitted, LoanNoveltyStatus.Applied, false)]
    [InlineData(LoanNoveltyStatus.Approved, LoanNoveltyStatus.Applied, true)]
    [InlineData(LoanNoveltyStatus.Approved, LoanNoveltyStatus.Cancelled, true)]
    [InlineData(LoanNoveltyStatus.Rejected, LoanNoveltyStatus.Submitted, false)]
    [InlineData(LoanNoveltyStatus.Applied, LoanNoveltyStatus.Cancelled, false)]
    [InlineData(LoanNoveltyStatus.Cancelled, LoanNoveltyStatus.Draft, false)]
    public void Las_transiciones_de_la_novedad_son_las_de_la_api(LoanNoveltyStatus desde, LoanNoveltyStatus hacia, bool permitida) =>
        LoanNovelty.PuedePasar(desde, hacia).Should().Be(permitida);

    [Fact]
    public void Nace_en_borrador_con_su_causa_y_se_edita_solo_en_borrador()
    {
        var n = Novedad();
        (n.Status, n.Cause, n.Editable, n.RatingAtRequest).Should().Be((LoanNoveltyStatus.Draft, NoveltyCause.Restructuring, true, RatingCategory.C));

        n.RegistrarSimulacion("{\"plan\":[1]}", new string('b', 64));
        n.Editar(NoveltyMechanism.RateChange, NoveltyCause.Modification, "{\"cause\":2}", "Baja de tasa", "{\"rate\":0.2}");
        (n.Mechanism, n.Cause, n.SimulationSha256).Should().Be((NoveltyMechanism.RateChange, NoveltyCause.Modification, string.Empty),
            "editarla borra la simulación anterior");

        n.RegistrarSimulacion("{\"plan\":[2]}", new string('c', 64));
        n.Radicar();
        n.Editable.Should().BeFalse();
        var editar = () => n.Editar(NoveltyMechanism.Extension, NoveltyCause.Restructuring, "{}", "x", "{}");
        editar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(LoanNovelty.CodigoNoEditable);
        ((Action)(() => n.RegistrarSimulacion("{}", "d"))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(LoanNovelty.CodigoNoEditable);
    }

    [Fact]
    public void No_se_radica_sin_simular()
    {
        var n = Novedad();
        ((Action)n.Radicar).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(LoanNovelty.CodigoEstadoInvalido);
    }

    [Fact]
    public void El_acuerdo_de_insolvencia_solo_es_reestructuracion()
    {
        var crear = () => LoanNovelty.Nueva("NOV-2", 10, NoveltyMechanism.Extension, NoveltyCause.Modification, "{}", "Acuerdo", "{}",
            RatingCategory.B, 5, Ahora, procesoDeInsolvenciaId: 4);
        crear.Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void Solo_se_aplica_aprobada_y_al_aplicarse_queda_congelada()
    {
        var borrador = Novedad();
        var aplicarBorrador = () => borrador.Aplicar(Operacion(LoanOperationKind.TermsChange), new LoanTermsVersion(), new RepaymentScheduleVersion(),
            null, 1, 9, Ahora);
        aplicarBorrador.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(LoanNovelty.CodigoNoAprobada);

        var n = Aprobada();
        var operacion = Operacion(LoanOperationKind.TermsChange);
        var condiciones = new LoanTermsVersion();
        var plan = new RepaymentScheduleVersion();
        n.Aplicar(operacion, condiciones, plan, null, reestructuracionesDespues: 1, usuarioId: 9, Ahora);

        n.Status.Should().Be(LoanNoveltyStatus.Applied);
        (n.AppliedOperation, n.NewTermsVersion, n.NewScheduleVersion, n.RestructuringCountAfter, n.AppliedByUserId)
            .Should().Be((operacion, condiciones, plan, (int?)1, (int?)9));
        n.Decidida.Should().BeTrue();
        ((Action)(() => n.Cancelar(Ahora, "tarde"))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(LoanNovelty.CodigoYaAplicada);
        ((Action)(() => n.Aplicar(operacion, condiciones, plan, null, 1, 9, Ahora))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(LoanNovelty.CodigoYaAplicada);
        ((Action)(() => n.AnotarPagare(1))).Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void Aplicar_exige_lo_que_deja_cada_mecanismo()
    {
        var prorroga = Aprobada();
        ((Action)(() => prorroga.Aplicar(Operacion(LoanOperationKind.Waiver), new(), new(), null, 1, 9, Ahora))).Should().Throw<ArgumentException>("es una TermsChange");
        ((Action)(() => prorroga.Aplicar(Operacion(LoanOperationKind.TermsChange), null, null, null, 1, 9, Ahora))).Should().Throw<ArgumentException>("conserva el número: versiones nuevas");
        ((Action)(() => prorroga.Aplicar(Operacion(LoanOperationKind.TermsChange), new(), new(), null, null, 9, Ahora))).Should().Throw<ArgumentException>("una reestructuración deja su conteo");

        var novacion = Aprobada(NoveltyMechanism.Novation);
        novacion.CreaCreditoEnlazado.Should().BeTrue();
        ((Action)(() => novacion.Aplicar(Operacion(LoanOperationKind.TermsChange), null, null, null, 1, 9, Ahora))).Should().Throw<ArgumentException>("crea el enlazado");
        var enlazado = new Loan();
        novacion.Aplicar(Operacion(LoanOperationKind.TermsChange), null, null, enlazado, 1, 9, Ahora);
        novacion.ResultingLoan.Should().BeSameAs(enlazado);
    }

    [Fact]
    public void Se_rechaza_o_se_anula_antes_de_aplicar()
    {
        var n = Novedad();
        n.RegistrarSimulacion("{}", new string('a', 64));
        n.Radicar();
        n.Rechazar(decisionId: 8);
        (n.Status, n.FinalDecisionId).Should().Be((LoanNoveltyStatus.Rejected, (int?)8));

        var otra = Aprobada();
        otra.AnotarPagare(44);
        otra.Cancelar(Ahora, "  El deudor desistió ");
        (otra.Status, otra.CancelReason, otra.PromissoryNoteId).Should().Be((LoanNoveltyStatus.Cancelled, "El deudor desistió", (int?)44));
    }

    // ------------------------------------------------------------------------------------------------------- consentimiento --

    [Fact]
    public void Aceptar_exige_el_documento_firmado()
    {
        var c = new LoanNoveltyConsent { LoanNoveltyId = 1, PersonId = 20, PartyRole = ConsentPartyRole.CoDebtor };
        c.Status.Should().Be(ConsentStatus.Pending);
        c.Resuelto.Should().BeFalse();

        ((Action)(() => c.Aceptar(Guid.Empty, 5, Ahora))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(LoanNoveltyConsent.CodigoSinAdjunto);

        var firmado = Guid.NewGuid();
        c.Aceptar(firmado, 5, Ahora);
        (c.Status, c.SignedAttachmentPublicId, c.RecordedByUserId, c.Resuelto).Should().Be((ConsentStatus.Accepted, (Guid?)firmado, (int?)5, true));
        ((Action)(() => c.Rehusar(5, Ahora))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(LoanNoveltyConsent.CodigoNoPendiente);
    }

    [Fact]
    public void La_resolucion_de_un_rechazo_solo_con_Refused()
    {
        var pendiente = new LoanNoveltyConsent { LoanNoveltyId = 1, PersonId = 21, PartyRole = ConsentPartyRole.Guarantor };
        ((Action)(() => pendiente.ResolverRechazo(ConsentRefusalResolution.DoNotApply, "x"))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(LoanNoveltyConsent.CodigoNoRechazado);

        pendiente.Rehusar(5, Ahora);
        pendiente.Resuelto.Should().BeFalse("el aprobador todavía no decide");
        ((Action)(() => pendiente.ResolverRechazo(ConsentRefusalResolution.ApplyWithGuaranteeExtinguished, "sin cobertura"))).Should()
            .Throw<ArgumentException>("extinguir la garantía deja la cobertura restante");

        pendiente.ResolverRechazo(ConsentRefusalResolution.ApplyWithGuaranteeExtinguished, "La cobertura restante cumple", "{\"remaining\":1.2}", excepcionId: null);
        (pendiente.RefusalResolution, pendiente.CoverageAfterJson, pendiente.Resuelto)
            .Should().Be(((ConsentRefusalResolution?)ConsentRefusalResolution.ApplyWithGuaranteeExtinguished, "{\"remaining\":1.2}", true));

        pendiente.ResolverRechazo(ConsentRefusalResolution.DoNotApply, "Mejor no aplicar", "{\"ignorada\":true}", 9);
        (pendiente.CoverageAfterJson, pendiente.ExceptionDecisionId).Should().Be(((string?)null, (int?)null), "no aplicar no extingue nada");
    }

    // ------------------------------------------------------------------------------------------------------------ condonación --

    [Theory]
    [InlineData(WaiverStatus.Draft, WaiverStatus.Submitted, true)]
    [InlineData(WaiverStatus.Submitted, WaiverStatus.Approved, true)]
    [InlineData(WaiverStatus.Submitted, WaiverStatus.Rejected, true)]
    [InlineData(WaiverStatus.Approved, WaiverStatus.Applied, true)]
    [InlineData(WaiverStatus.Draft, WaiverStatus.Applied, false)]
    [InlineData(WaiverStatus.Rejected, WaiverStatus.Approved, false)]
    [InlineData(WaiverStatus.Applied, WaiverStatus.Cancelled, false)]
    public void Las_transiciones_de_la_condonacion(WaiverStatus desde, WaiverStatus hacia, bool permitida) =>
        Waiver.PuedePasar(desde, hacia).Should().Be(permitida);

    private static Waiver Condonacion() => Waiver.Nueva("CON-0001", 10, new DateOnly(2026, 9, 15), "Acuerdo de pago", 75, vinculado: false, 5, Ahora);

    [Fact]
    public void Las_lineas_se_arman_en_borrador_no_superan_el_saldo_y_suman_el_total()
    {
        var w = Condonacion();
        (w.DaysPastDueAtRequest, w.IsRelatedParty).Should().Be((75, false));

        w.AgregarLinea(LoanConcept.DefaultInterest, null, 40_000m, 52_310.40m);
        w.AgregarLinea(LoanConcept.CollectionFee, 3, 15_000m, 15_000m);
        w.TotalAmount.Should().Be(55_000m);
        w.CondonaCapital.Should().BeFalse();

        var excede = () => w.AgregarLinea(LoanConcept.CurrentInterest, null, 10_000.01m, 10_000m);
        var e = excede.Should().Throw<ReglaDeCarteraException>().Which;
        e.Codigo.Should().Be(WaiverLine.CodigoExcedeElSaldo);
        e.Problema.Datos.Should().Contain("concept", "CurrentInterest").And.Contain("balance", 10_000m);
        ((Action)(() => w.AgregarLinea(LoanConcept.DefaultInterest, null, 1m, 5m))).Should().Throw<ReglaDeCarteraException>("concepto y cuota repetidos");
        ((Action)(() => w.AgregarLinea(LoanConcept.Advance, null, 1m, 5m))).Should().Throw<ReglaDeCarteraException>("el anticipo no se condona");

        w.AgregarLinea(LoanConcept.Principal, null, 100_000m, 900_000m);
        w.CondonaCapital.Should().BeTrue("el capital sólo con nivel de excepción");
        w.QuitarLinea(w.Lines.Single(l => l.Concept == LoanConcept.Principal));
        w.TotalAmount.Should().Be(55_000m);

        w.Radicar();
        ((Action)(() => w.AgregarLinea(LoanConcept.Insurance, null, 1m, 5m))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(Waiver.CodigoNoEditable);
    }

    [Fact]
    public void Sin_lineas_no_se_radica_y_solo_se_aplica_aprobada()
    {
        var w = Condonacion();
        ((Action)w.Radicar).Should().Throw<ReglaDeCarteraException>();

        w.AgregarLinea(LoanConcept.DefaultInterest, null, 1_000m, 1_000m);
        w.Radicar();
        ((Action)(() => w.Aplicar(Operacion(LoanOperationKind.Waiver), 9, Ahora))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(Waiver.CodigoNoAprobada);
        w.Aprobar(decisionId: 12);
        ((Action)(() => w.Aplicar(Operacion(LoanOperationKind.TermsChange), 9, Ahora))).Should().Throw<ArgumentException>();

        var operacion = Operacion(LoanOperationKind.Waiver);
        w.Aplicar(operacion, 9, Ahora);
        (w.Status, w.FinalDecisionId, w.AppliedOperation, w.AppliedByUserId).Should().Be((WaiverStatus.Applied, (int?)12, operacion, (int?)9));
        ((Action)w.Cancelar).Should().Throw<ReglaDeCarteraException>();
    }
}
