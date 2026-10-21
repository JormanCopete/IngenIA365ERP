using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Tests.Lending.Collections;

/// <summary>
/// Feature 013, K4, T0936 (data-model §26.3–§26.10): las entidades de cobranza nacen validadas y sólo avanzan por sus transiciones. Las
/// enumeraciones de K4 llevan la numeración de data-model §9 (desde 1, en el orden escrito; T0935).
/// </summary>
public class EntidadesDeCobranzaTests
{
    [Fact]
    public void Las_enumeraciones_de_K4_siguen_la_numeracion_del_modelo()
    {
        Enum.GetValues<CollectionActionChannel>().Select(v => (int)v).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        Enum.GetValues<CollectionActionResult>().Select(v => (int)v).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9);
        Enum.GetValues<AssignmentDistribution>().Select(v => v.ToString()).Should().Equal("ByCount", "ByBalance");
        Enum.GetValues<PaymentPromiseStatus>().Select(v => v.ToString()).Should().Equal("Pending", "Kept", "Broken");
        Enum.GetValues<PaymentAgreementStatus>().Select(v => v.ToString()).Should().Equal("Proposed", "Approved", "Rejected", "Cancelled");
        Enum.GetValues<PaymentAgreementPerformance>().Select(v => v.ToString()).Should().Equal("Current", "Fulfilled", "Broken");
        Enum.GetValues<LegalProcessStatus>().Select(v => (int)v).Should().Equal(1, 2, 3, 4, 5, 6);
        Enum.GetValues<LegalActionKind>().Select(v => (int)v).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9);
        Enum.GetValues<MessageCampaignStatus>().Select(v => v.ToString()).Should().Equal("Draft", "Scheduled", "Running", "Completed", "Cancelled");
    }

    [Fact]
    public void La_etapa_cierra_su_vigencia_una_vez_y_no_antes_de_empezar()
    {
        var etapa = CollectionStage.Nueva("admin", "Administrativa", 2, 1, 30, false, new DateOnly(2026, 11, 1));
        etapa.Code.Should().Be("ADMIN");

        var antes = () => etapa.CerrarVigencia(new DateOnly(2026, 10, 31));
        antes.Should().Throw<ReglaDeCarteraException>();
        etapa.CerrarVigencia(new DateOnly(2026, 12, 31));
        etapa.VigenteEn(new DateOnly(2026, 12, 31)).Should().BeTrue();
        etapa.VigenteEn(new DateOnly(2027, 1, 1)).Should().BeFalse();
        var dosVeces = () => etapa.CerrarVigencia(new DateOnly(2027, 1, 31));
        dosVeces.Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void La_entrada_manual_a_una_etapa_lleva_persona_y_motivo_y_la_del_ciclo_su_corrida()
    {
        var etapa = CollectionStage.Nueva("PREJ", "Prejurídica", 3, 31, 90, false, new DateOnly(2026, 11, 1));
        var sinMotivo = () => CollectionStageHistoryEntry.Registrar(1, etapa, new DateOnly(2026, 11, 20), 31, CollectionStageHistoryEntry.FuenteManual, usuarioId: 5);
        sinMotivo.Should().Throw<ArgumentException>();
        var sinCorrida = () => CollectionStageHistoryEntry.Registrar(1, etapa, new DateOnly(2026, 11, 20), 31, CollectionStageHistoryEntry.FuenteCicloDiario);
        sinCorrida.Should().Throw<ArgumentException>();

        var entrada = CollectionStageHistoryEntry.Registrar(1, etapa, new DateOnly(2026, 11, 20), 31, CollectionStageHistoryEntry.FuenteCicloDiario, corridaId: 9);
        entrada.StageCode.Should().Be("PREJ");
    }

    [Fact]
    public void La_regla_de_reparto_guarda_sus_gestores_y_los_devuelve()
    {
        var g1 = Guid.Parse("e0000000-0000-0000-0000-000000000001");
        var regla = CollectorAssignmentRule.Nueva(1, AssignmentDistribution.ByBalance, [new GestorDeRegla(g1, 2m)], new DateOnly(2026, 11, 1),
            etapa: "prej", deudorCompleto: true);

        regla.Gestores().Should().Equal(new GestorDeRegla(g1, 2m));
        regla.Regla().Etapa.Should().Be("PREJ");
        regla.Regla().DeudorCompleto.Should().BeTrue();
        var sinGestores = () => CollectorAssignmentRule.Nueva(1, AssignmentDistribution.ByCount, [], new DateOnly(2026, 11, 1));
        sinGestores.Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void La_asignacion_manual_y_la_reasignacion_llevan_motivo()
    {
        var manual = () => CollectorAssignment.Asignar(1, 2, 3, new DateOnly(2026, 11, 1), null, null, 4);
        manual.Should().Throw<ArgumentException>();
        var reasignacion = () => CollectorAssignment.Asignar(1, 2, 3, new DateOnly(2026, 11, 1), 7, null, null, anteriorId: 10);
        reasignacion.Should().Throw<ArgumentException>();
        CollectorAssignment.Asignar(null, 2, 3, new DateOnly(2026, 11, 1), 7, null, null).EsManual.Should().BeFalse();
    }

    [Fact]
    public void La_gestion_ante_la_pagaduria_no_es_contacto_directo_y_la_correccion_lleva_motivo()
    {
        var pagaduria = () => CollectionAction.Registrar(1, 2, ContactRole.Paymaster, CollectionActionChannel.Phone, DateTime.UtcNow, true,
            CollectionActionResult.Other, 3);
        pagaduria.Should().Throw<ArgumentException>();
        var sinMotivo = () => CollectionAction.Registrar(1, 2, ContactRole.Debtor, CollectionActionChannel.Phone, DateTime.UtcNow, false,
            CollectionActionResult.NoAnswer, 3, anulaA: 8);
        sinMotivo.Should().Throw<ArgumentException>();

        var gestion = CollectionAction.Registrar(1, 2, ContactRole.Debtor, CollectionActionChannel.WhatsApp, DateTime.UtcNow, true,
            CollectionActionResult.PromiseToPay, 3, decision: new Diferido(new DateTime(2026, 11, 17, 7, 0, 0),
                [ContactWindowReason.DailyLimitReached, ContactWindowReason.WeeklyChannelLocked]));
        gestion.WindowDecision.Should().Be("Diferido");
        gestion.WindowReasonCodes.Should().Be("DailyLimitReached,WeeklyChannelLocked");
    }

    [Fact]
    public void La_promesa_tiene_tolerancia_menor_que_lo_prometido()
    {
        var mucha = () => PaymentPromise.Prometer(1, 1, new DateOnly(2026, 11, 13), 1_000m, 1_000m);
        mucha.Should().Throw<ArgumentOutOfRangeException>();
        PaymentPromise.Prometer(1, 1, new DateOnly(2026, 11, 13), 1_000m, 0m).ToleranceAmount.Should().Be(0m);
    }

    [Fact]
    public void El_acuerdo_valida_su_calendario_y_lo_aprueba_otra_persona()
    {
        var inicio = new DateOnly(2026, 11, 15);
        PaymentAgreement.ValidarCalendario(inicio, [(new DateOnly(2026, 11, 30), 100m), (new DateOnly(2026, 11, 30), 100m)])!.Codigo
            .Should().Be(PaymentAgreement.CodigoCalendarioInvalido);
        PaymentAgreement.ValidarCalendario(inicio, [(new DateOnly(2026, 11, 14), 100m)])!.Codigo.Should().Be(PaymentAgreement.CodigoCalendarioInvalido);

        var acuerdo = PaymentAgreement.Proponer("AC-1", 1, 2, 600_000m, inicio,
            [(new DateOnly(2026, 11, 30), 300_000m), (new DateOnly(2026, 12, 30), 300_000m)], usuarioId: 10);
        acuerdo.TotalAmount.Should().Be(600_000m);
        acuerdo.Installments.Select(c => c.Number).Should().Equal(1, 2);

        var mismo = () => acuerdo.Aprobar(10, DateTime.UtcNow);
        mismo.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(PaymentAgreement.CodigoSegregacion);
        acuerdo.Aprobar(11, DateTime.UtcNow);
        var otraVez = () => acuerdo.Aprobar(12, DateTime.UtcNow);
        otraVez.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(PaymentAgreement.CodigoNoPropuesto);
        acuerdo.Anular("El deudor pagó todo", DateTime.UtcNow);
        acuerdo.Status.Should().Be(PaymentAgreementStatus.Cancelled);
    }

    [Fact]
    public void El_proceso_juridico_se_suspende_por_insolvencia_se_reanuda_y_se_cierra()
    {
        var proceso = LegalProcess.Abrir("PJ-1", 1, 2, 3, new DateOnly(2026, 11, 1));
        proceso.Avanzar(LegalProcessStatus.Filed);
        proceso.Suspender(insolvenciaId: 7);
        proceso.SuspendedByInsolvencyId.Should().Be(7);
        proceso.Avanzar(LegalProcessStatus.InProgress);
        proceso.SuspendedByInsolvencyId.Should().BeNull();
        proceso.Cerrar(LegalProcessStatus.Concluded, "Sentencia y pago", DateTime.UtcNow);
        var cerrado = () => proceso.Suspender();
        cerrado.Should().Throw<ReglaDeCarteraException>();

        var mismoAbogado = () => LegalProcess.Abrir("PJ-2", 1, 2, 2, new DateOnly(2026, 11, 1));
        mismoAbogado.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Solo_un_honorario_o_gasto_con_monto_se_cobra_al_deudor()
    {
        var auto = () => LegalProcessAction.Registrar(1, new DateOnly(2026, 11, 5), LegalActionKind.Order, "Auto admisorio", 3, cobrableAlDeudor: true);
        auto.Should().Throw<ArgumentException>();
        LegalProcessAction.Registrar(1, new DateOnly(2026, 11, 5), LegalActionKind.Fee, "Honorarios", 3, 250_000m, true).ChargeOperationId.Should().BeNull();
    }

    [Fact]
    public void La_campana_avanza_por_su_ciclo_y_cuenta_sus_destinatarios()
    {
        var campana = MessageCampaign.Nueva("nov26", "Mora noviembre", DebtorMessagePurpose.Collection, MessageChannel.WhatsApp, 4, "{}",
            new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc), 1);
        campana.Programar(2, encolados: 8, excluidos: 2);
        campana.TargetCount.Should().Be(10);
        var completar = () => campana.Completar();
        completar.Should().Throw<ReglaDeCarteraException>();
        campana.Iniciar();
        campana.Cancelar(2, "Cambio de plantilla", DateTime.UtcNow);
        campana.Status.Should().Be(MessageCampaignStatus.Cancelled);
        var aviso = () => MessageCampaign.Nueva("X", "X", DebtorMessagePurpose.PriorNotice, MessageChannel.Sms, 4, "{}", DateTime.UtcNow, 1);
        aviso.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------------------------------- K4, bloque D (2026-10-16) --

    [Fact]
    public void Reanudar_vuelve_al_estado_que_dicen_las_actuaciones_y_suelta_la_insolvencia()
    {
        LegalProcess.EstadoAlReanudar([]).Should().Be(LegalProcessStatus.Assigned);
        LegalProcess.EstadoAlReanudar([LegalActionKind.Fee, LegalActionKind.Other]).Should().Be(LegalProcessStatus.Assigned, "sin radicación sigue asignado");
        LegalProcess.EstadoAlReanudar([LegalActionKind.Filing]).Should().Be(LegalProcessStatus.Filed);
        LegalProcess.EstadoAlReanudar([LegalActionKind.Filing, LegalActionKind.Expense]).Should().Be(LegalProcessStatus.Filed, "un gasto no es actuación de fondo");
        LegalProcess.EstadoAlReanudar([LegalActionKind.Filing, LegalActionKind.Order]).Should().Be(LegalProcessStatus.InProgress);

        var proceso = LegalProcess.Abrir("PJ-3", 1, 2, 3, new DateOnly(2026, 11, 1));
        proceso.Avanzar(LegalProcessStatus.Filed);
        proceso.Suspender(insolvenciaId: 9);
        proceso.Reanudar(LegalProcessStatus.Filed);
        proceso.Status.Should().Be(LegalProcessStatus.Filed);
        proceso.SuspendedByInsolvencyId.Should().BeNull();

        var otraVez = () => proceso.Reanudar(LegalProcessStatus.Filed);
        otraVez.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(LegalProcess.CodigoEstadoInvalido, "sólo uno suspendido se reanuda");
        var aCerrado = () =>
        {
            proceso.Suspender(9);
            proceso.Reanudar(LegalProcessStatus.Concluded);
        };
        aCerrado.Should().Throw<ArgumentOutOfRangeException>("reanudar no es cerrar");
    }

    [Fact]
    public void La_salida_de_cobranza_lleva_la_etapa_de_la_que_sale_y_la_gestion_de_sistema_no_es_contacto()
    {
        var etapa = CollectionStage.Nueva("ADMIN", "Administrativa", 1, 1, 30, false, new DateOnly(2026, 1, 1));
        var entrada = CollectionStageHistoryEntry.Registrar(7, etapa, new DateOnly(2026, 11, 2), 3, CollectionStageHistoryEntry.FuenteCicloDiario, 4);
        entrada.EsSalida.Should().BeFalse();
        var salida = CollectionStageHistoryEntry.Salir(entrada, new DateOnly(2026, 11, 10), -20, 5, CollectionStageHistoryEntry.MotivoSalidaAlDia);
        salida.Should().BeEquivalentTo(new { LoanId = 7, StageCode = "ADMIN", Source = "Exit", DaysPastDueAtEntry = -20, ProcessRunId = 5, Reason = "Salida por pago al día" });
        salida.EsSalida.Should().BeTrue();
        var dosVeces = () => CollectionStageHistoryEntry.Salir(salida, new DateOnly(2026, 11, 11), -19, 6, "otra");
        dosVeces.Should().Throw<ArgumentException>();

        var gestion = CollectionAction.DeSistema(7, 2, DateTime.UtcNow, CollectionActionResult.LeftCollection, "Sale de cobranza: se puso al día el 10/11/2026");
        gestion.Should().BeEquivalentTo(new { Channel = CollectionActionChannel.System, IsDirectContact = false, CollectorUserId = (int?)null, ContactRole = ContactRole.Debtor });
        gestion.EsDeSistema.Should().BeTrue();
        var aMano = () => CollectionAction.Registrar(7, 2, ContactRole.Debtor, CollectionActionChannel.System, DateTime.UtcNow, false, CollectionActionResult.Other, 3);
        aMano.Should().Throw<ArgumentException>("el canal de sistema no lo registra una persona");

        var cierre = CollectorAssignment.Cerrar(7, 2, new DateOnly(2026, 11, 10), CollectionStageHistoryEntry.MotivoSalidaAlDia, 11);
        cierre.Should().BeEquivalentTo(new { CollectorUserId = (int?)null, PreviousAssignmentId = (int?)11, EsCierre = true, EsManual = false });
    }

    [Fact]
    public void El_tope_del_gasto_recorta_al_menor_y_rechaza_una_tarifa_mayor()
    {
        var regla = new ReglaDeGastoDeCobranza { Rate = 0.10m, Cap = 50_000m };
        regla.Calcular(300_000m, 100_000m, TopeDelGastoDeCobranza.SinTope).Should().Be(30_000m);
        regla.Calcular(300_000m, 100_000m, new TopeDelGastoDeCobranza(0.05m, null)).Should().Be(15_000m, "5 % de la base");
        regla.Calcular(300_000m, 100_000m, new TopeDelGastoDeCobranza(0.05m, 12_000m)).Should().Be(12_000m, "con los dos rige el menor");
        regla.Calcular(300_000m, 100_000m, new TopeDelGastoDeCobranza(null, 40_000m)).Should().Be(30_000m, "por debajo del tope no cambia");

        new TopeDelGastoDeCobranza(0.10m, null).Validar(regla, "ADMIN").Should().BeNull("igual al tope cabe");
        var encima = new TopeDelGastoDeCobranza(0.08m, null).Validar(regla, "ADMIN");
        encima!.Codigo.Should().Be(TopeDelGastoDeCobranza.CodigoPorEncimaDelTope);
        encima.Datos!["capRate"].Should().Be(0.08m);
        new TopeDelGastoDeCobranza(null, 1m).Validar(regla, "ADMIN").Should().BeNull("el valor máximo recorta, no rechaza");
    }
}
