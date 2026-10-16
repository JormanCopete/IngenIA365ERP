using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// El paso 7 del ciclo diario sobre un crédito <b>reconstruido por el motor</b> (feature 013, K4, T0924; FR-117, FR-119, FR-120, FR-123): el crédito que
/// pasa de 30 días entra a la etapa siguiente con su historia y su corrida, se asigna por la regla y encola el mensaje de la plantilla; el aviso previo
/// se abre por episodio; la promesa vencida sin pago alerta al gestor; el acuerdo incumplido lo devuelve a su etapa y el que va al día la congela; el
/// reclamo vencido sin respuesta se acepta tácitamente; el aviso sin constancia alerta; el fallecido con reclamación, la insolvencia y el cobro suspendido no
/// se tocan; repetir la tanda no duplica y sin la entrega K4 no corre. (nuevo)
/// </summary>
public sealed class CobranzaDelCicloDiarioTests : IAsyncLifetime
{
    private readonly CobranzaDePrueba _c = new();

    public async Task InitializeAsync()
    {
        _c.E.Autorizar(_c.Ana, MessageChannel.Sms);
        await _c.SembrarEtapasAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task<CollectionStageHistoryEntry[]> HistoriaAsync() =>
        await _c.Db.CollectionStageHistory.AsNoTracking().Where(h => h.LoanId == _c.Credito.Id).OrderBy(h => h.Id).ToArrayAsync();

    // ---------------------------------------------------------------------------------------------------- etapa y mensaje --

    [Fact]
    public async Task El_credito_que_pasa_a_mora_entra_a_la_etapa_y_su_historia_nombra_el_ciclo_y_la_corrida()
    {
        var corrida = await _c.CorridaAsync();
        var credito = _c.EnMora(15);
        credito.Estado.AlturaDeMora.Dias.Should().BeInRange(14, 16, "el motor reconstruyó el crédito con su primera cuota vencida hace unos 15 días");

        var r = await _c.Ciclo().CreditosAsync(corrida, _c.Hoy, [credito], [], default);
        await _c.Db.SaveChangesAsync();

        r.CambiosDeEtapa.Should().Be(1);
        var entrada = (await HistoriaAsync()).Should().ContainSingle().Subject;
        (entrada.StageCode, entrada.Source, entrada.ProcessRunId, entrada.EnteredOn).Should().Be(("ADMIN", CollectionStageHistoryEntry.FuenteCicloDiario, corrida.Id, _c.Hoy));
        entrada.DaysPastDueAtEntry.Should().Be(credito.Estado.AlturaDeMora.Dias);
    }

    [Fact]
    public async Task La_etapa_encola_el_mensaje_de_su_plantilla_con_origen_de_etapa_y_no_se_repite_con_la_tanda()
    {
        var primera = await _c.CorrerAsync(_c.EnMora(15));
        primera.Mensajes.Should().Be(1);
        (await _c.MensajesDeEtapaAsync()).Should().Be(1);
        var mensaje = await _c.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.OriginKind == "CollectionStage");
        (mensaje.Purpose, mensaje.PersonId, mensaje.Channel).Should().Be((DebtorMessagePurpose.Collection, _c.Ana.Id, MessageChannel.Sms));
        mensaje.RenderedBody.Should().Contain("CR-0001");

        var otra = await _c.CorrerAsync(_c.EnMora(15));

        (otra.CambiosDeEtapa, otra.Mensajes).Should().Be((0, 0), "ya entró hoy a la etapa: repetir la tanda no duplica");
        (await HistoriaAsync()).Should().HaveCount(1);
        (await _c.MensajesDeEtapaAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Otro_dia_en_la_misma_etapa_no_registra_entrada_nueva()
    {
        await _c.CorrerAsync(_c.EnMora(10));
        _c.E.EnLocal(new DateTime(2026, 12, 2, 10, 0, 0));

        var r = await _c.CorrerAsync(_c.EnMora(11));

        r.CambiosDeEtapa.Should().Be(0, "sigue dentro del rango de la administrativa");
        (await HistoriaAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Al_cruzar_a_la_etapa_siguiente_la_nueva_entrada_enlaza_la_anterior()
    {
        await _c.CorrerAsync(_c.EnMora(25));
        _c.E.EnLocal(new DateTime(2026, 12, 20, 10, 0, 0));

        var r = await _c.CorrerAsync(_c.EnMora(40));

        r.CambiosDeEtapa.Should().Be(1);
        var historia = await HistoriaAsync();
        historia.Select(h => h.StageCode).Should().Equal("ADMIN", "PREJURID");
        historia[1].PreviousEntryId.Should().Be(historia[0].Id);
    }

    // --------------------------------------------------------------------------------------------------------- reparto --

    [Fact]
    public async Task El_credito_que_entra_se_asigna_al_gestor_de_la_regla()
    {
        await _c.ReglaDeUnGestorAsync(_c.Gestora);

        var r = await _c.CorrerAsync(_c.EnMora(15));

        r.Asignaciones.Should().Be(1);
        var asignacion = await _c.Db.CollectorAssignments.AsNoTracking().SingleAsync();
        (asignacion.CollectorUserId, asignacion.LoanId, asignacion.AssignedOn).Should().Be((_c.Gestora.Id, _c.Credito.Id, _c.Hoy));
        asignacion.CollectorAssignmentRuleId.Should().NotBeNull("la asignó la regla, no una persona");
        asignacion.Reason.Should().BeNull("la primera asignación por regla va sin motivo");
    }

    [Fact]
    public async Task El_credito_causado_que_tambien_llega_como_al_dia_cuenta_una_sola_vez()
    {
        // T0933 lo destapó: el ciclo pasaba cada crédito causado también entre los «al día» (su control ya tenía la marca en el día) y, con una regla
        // de reparto vigente, el reparto fallaba con la clave repetida y la tanda entera no se guardaba.
        await _c.ReglaDeUnGestorAsync(_c.Gestora);
        var corrida = await _c.CorridaAsync();
        var credito = _c.EnMora(15);

        var r = await _c.Ciclo().CreditosAsync(corrida, _c.Hoy, [credito], [credito.Credito.Id], default);
        await _c.Db.SaveChangesAsync();

        (r.CambiosDeEtapa, r.Asignaciones).Should().Be((1, 1));
        (await HistoriaAsync()).Should().ContainSingle();
        (await _c.Db.CollectorAssignments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Sin_reglas_no_se_reparte_y_el_credito_igual_cambia_de_etapa()
    {
        var r = await _c.CorrerAsync(_c.EnMora(15));

        (r.CambiosDeEtapa, r.Asignaciones).Should().Be((1, 0));
        (await _c.Db.CollectorAssignments.CountAsync()).Should().Be(0);
    }

    // ----------------------------------------------------------------------------------------------------- aviso previo --

    [Fact]
    public async Task La_etapa_que_pide_aviso_abre_el_aviso_previo_del_episodio_una_sola_vez()
    {
        var credito = _c.EnMora(40);

        var r = await _c.CorrerAsync(credito);

        r.AvisosAbiertos.Should().Be(1);
        var aviso = await _c.Db.PriorNotices.AsNoTracking().SingleAsync();
        (aviso.LoanId, aviso.PersonId).Should().Be((_c.Credito.Id, _c.Ana.Id));
        aviso.EpisodeStartDate.Should().Be(_c.Hoy.AddDays(-credito.Estado.AlturaDeMora.Dias));
        aviso.RequiredCommunications.Should().BeInRange((byte)1, (byte)2);

        _c.E.EnLocal(new DateTime(2026, 12, 2, 10, 0, 0));
        // El mismo episodio al día siguiente: la etapa no cambia y no se abre otro aviso.
        var otra = await _c.CorrerAsync(_c.EnMora(41));
        otra.AvisosAbiertos.Should().Be(0);
        (await _c.Db.PriorNotices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task La_etapa_con_gasto_propone_y_encola_el_aviso_del_gasto_sin_causarlo()
    {
        var r = await _c.CorrerAsync(_c.EnMora(40));

        r.GastosPropuestos.Should().Be(1);
        var aviso = await _c.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.Purpose == DebtorMessagePurpose.CollectionFeeNotice);
        aviso.OriginKind.Should().Be("CollectionFee");
        (await _c.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.Charge)).Should().Be(0, "el ciclo propone; causarlo es de una persona");
    }

    // ---------------------------------------------------------------------------------------------------------- promesas --

    private async Task<PaymentPromise> PromesaAsync(DateOnly fecha, decimal valor)
    {
        var gestion = CollectionAction.Registrar(_c.Credito.Id, _c.Ana.Id, ContactRole.Debtor, CollectionActionChannel.Phone,
            MensajesDePrueba.Utc(new DateTime(2026, 11, 25, 10, 0, 0)), true, CollectionActionResult.PromiseToPay, _c.Gestor.Id);
        _c.Db.CollectionActions.Add(gestion);
        await _c.Db.SaveChangesAsync();
        var promesa = PaymentPromise.Prometer(gestion.Id, _c.Credito.Id, fecha, valor, 0m);
        _c.Db.PaymentPromises.Add(promesa);
        await _c.Db.SaveChangesAsync();
        return promesa;
    }

    [Fact]
    public async Task La_promesa_vencida_ayer_sin_pago_alerta_al_gestor()
    {
        var promesa = await PromesaAsync(_c.Hoy.AddDays(-1), 200_000m);

        var r = await _c.CorrerAsync(_c.EnMora(15));

        r.PromesasIncumplidas.Should().Be(1);
        var alerta = r.Alertas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.PromesaIncumplida).Subject;
        alerta.EntityPublicId.Should().Be(promesa.PublicId);
        alerta.Body.Should().Contain("gestor@coop.test");
    }

    [Fact]
    public async Task La_promesa_con_pago_a_tiempo_no_alerta()
    {
        await PromesaAsync(_c.Hoy.AddDays(-1), 200_000m);
        // El pago de la fecha prometida, ya en el libro: la promesa queda cumplida y no hay alerta.
        _c.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = _c.Credito.Id, Kind = LoanOperationKind.Payment, ValueDate = _c.Hoy.AddDays(-1), AccountingDate = _c.Hoy.AddDays(-1), Amount = 250_000m,
            RegisteredAt = _c.G.UtcNow.AddDays(-1), Number = "P-0001", Sequence = 2, IdempotencyKey = "pago-prueba",
        });
        await _c.Db.SaveChangesAsync();

        var r = await _c.CorrerAsync(_c.EnMora(15, null, (_c.Hoy.AddDays(-1), 250_000m)));

        r.PromesasIncumplidas.Should().Be(0);
        r.Alertas.Should().NotContain(a => a.TypeCode == TiposDeAlerta.PromesaIncumplida);
    }

    // ---------------------------------------------------------------------------------------------------------- acuerdos --

    private async Task<PaymentAgreement> AcuerdoAprobadoAsync(DateOnly vence, decimal valor)
    {
        var acuerdo = PaymentAgreement.Proponer("ACU-0001", _c.Credito.Id, _c.Ana.Id, 100_000m, new DateOnly(2026, 11, 20), [(vence, valor)], _c.Gestora.Id);
        acuerdo.Aprobar(_c.Gestor.Id, MensajesDePrueba.Utc(new DateTime(2026, 11, 20, 9, 0, 0)));
        _c.Db.PaymentAgreements.Add(acuerdo);
        await _c.Db.SaveChangesAsync();
        return acuerdo;
    }

    [Fact]
    public async Task El_acuerdo_al_dia_congela_la_etapa_y_el_incumplido_hoy_la_devuelve_con_motivo()
    {
        await _c.CorrerAsync(_c.EnMora(25));
        await AcuerdoAprobadoAsync(_c.Hoy.AddDays(20), 100_000m);
        _c.E.EnLocal(new DateTime(2026, 12, 20, 10, 0, 0));

        var congelada = await _c.CorrerAsync(_c.EnMora(40));
        congelada.CambiosDeEtapa.Should().Be(0, "mientras el acuerdo va al día la etapa no avanza");
        (await HistoriaAsync()).Should().HaveCount(1);

        // El acuerdo se vuelve incumplido: su cuota venció ayer sin pago.
        _c.Db.PaymentAgreements.Local.Single().Installments.Single().DueDate.Should().Be(new DateOnly(2026, 12, 21));
        _c.E.EnLocal(new DateTime(2026, 12, 22, 10, 0, 0));
        var rota = await _c.CorrerAsync(_c.EnMora(42));

        rota.AcuerdosIncumplidos.Should().Be(1);
        rota.CambiosDeEtapa.Should().Be(1);
        var ultima = (await HistoriaAsync()).Last();
        (ultima.StageCode, ultima.Reason).Should().Be(("PREJURID", CobranzaDelCicloDiario.MotivoAcuerdoIncumplido));
    }

    // ---------------------------------------------------------------------------------------------------------- reclamos --

    [Fact]
    public async Task El_reclamo_vencido_sin_respuesta_se_acepta_tacitamente_y_deja_alerta_en_el_cierre()
    {
        _c.Db.CreditReportClaims.Add(new CreditReportClaim
        {
            PersonId = _c.Ana.Id, LoanId = _c.Credito.Id, Kind = CreditReportClaimKind.Claim, Status = CreditReportClaimStatus.InProcess,
            ReceivedOn = new DateOnly(2026, 11, 2), Channel = "Correo", Description = "x", DueDate = new DateOnly(2026, 11, 23),
        });
        await _c.Db.SaveChangesAsync();

        var cierre = await _c.Ciclo().CierreAsync(_c.Hoy, default);
        await _c.Db.SaveChangesAsync();

        cierre.ReclamosAceptados.Should().Be(1);
        cierre.Alertas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.PlazoRegulatorioPorVencer && a.Body.Contains("aceptado tácitamente"));
        (await _c.Db.CreditReportClaims.SingleAsync()).Status.Should().Be(CreditReportClaimStatus.TacitlyAccepted);
    }

    [Fact]
    public async Task El_aviso_previo_que_cumplio_sus_dias_sin_constancia_alerta_en_el_cierre()
    {
        var credito = _c.EnMora(40);
        await _c.CorrerAsync(credito);
        var aviso = await _c.Db.PriorNotices.SingleAsync();
        aviso.CreatedAt = _c.G.UtcNow;
        await _c.Db.SaveChangesAsync();
        _c.E.EnLocal(new DateTime(2026, 12, 1, 10, 0, 0).AddDays(aviso.MinimumDays + 1));

        var cierre = await _c.Ciclo().CierreAsync(DateOnly.FromDateTime(_c.G.UtcNow.AddHours(-5)), default);

        cierre.Alertas.Should().Contain(a => a.TypeCode == TiposDeAlerta.AvisoPrevioSinConstancia && a.EntityPublicId == aviso.PublicId);
    }

    // ----------------------------------------------------------------------------------------------------- exclusiones --

    [Fact]
    public async Task La_insolvencia_en_negociacion_excluye_al_credito_del_ciclo()
    {
        _c.Db.InsolvencyProceedings.Add(new InsolvencyProceeding { PersonId = _c.Ana.Id, StartDate = _c.Hoy.AddDays(-5), Status = InsolvencyProceedingStatus.Negotiating });
        await _c.Db.SaveChangesAsync();

        var r = await _c.CorrerAsync(_c.EnMora(15));

        (r.CambiosDeEtapa, r.Mensajes).Should().Be((0, 0));
        (await HistoriaAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task La_reclamacion_al_seguro_en_tramite_por_fallecimiento_excluye_al_credito_del_ciclo()
    {
        _c.Db.InsuranceClaims.Add(new InsuranceClaim
        {
            LoanId = _c.Credito.Id, Status = InsuranceClaimStatus.InProcess, DeceasedPersonId = _c.Ana.Id, InsurerPersonId = _c.Beto.Id, DeathDate = _c.Hoy.AddDays(-3),
        });
        await _c.Db.SaveChangesAsync();

        var r = await _c.CorrerAsync(_c.EnMora(15));

        r.CambiosDeEtapa.Should().Be(0);
        (await HistoriaAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Un_credito_cancelado_o_en_borrador_no_entra_a_la_cobranza()
    {
        var cancelado = _c.E.NuevoCredito("CR-0002", MensajesDePrueba.Norte, _c.Ana);
        cancelado.Status = LoanStatus.Voided;
        await _c.Db.SaveChangesAsync();

        var r = await _c.CorrerAsync(_c.EnMora(15, cancelado));

        r.CambiosDeEtapa.Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------------------- entrega --

    [Fact]
    public async Task Sin_la_entrega_K4_el_paso_no_corre()
    {
        var corrida = await _c.CorridaAsync();

        var r = await _c.Ciclo(EntregaDeCartera.K3).CreditosAsync(corrida, _c.Hoy, [_c.EnMora(15)], [], default);
        var cierre = await _c.Ciclo(EntregaDeCartera.K3).CierreAsync(_c.Hoy, default);

        r.Should().Be(ResultadoDeCobranzaDelDia.Vacio);
        cierre.Should().Be(ResultadoDeCobranzaDelDia.Vacio);
        (await HistoriaAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_etapas_sembradas_no_hay_cambios_pero_el_cierre_sigue_funcionando()
    {
        _c.Db.CollectionStages.RemoveRange(_c.Db.CollectionStages);
        await _c.Db.SaveChangesAsync();

        var r = await _c.CorrerAsync(_c.EnMora(15));

        r.CambiosDeEtapa.Should().Be(0);
    }
}
