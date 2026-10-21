using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Lending.Collections.Tareas;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0933 (feature 013, K4, US12; quickstart §6; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): la cobranza por sus rutas
/// (<c>/api/lending/collection-stages</c>, <c>/collector-assignments</c>, <c>/collection-actions</c>, <c>/promises</c>, <c>/campaigns</c>,
/// <c>/prior-notices</c>, <c>/collection-fees</c>, <c>/credit-report-claims</c>, <c>/payment-agreements</c>, <c>/legal-processes</c>) sobre cooperativas
/// aisladas activadas, con la contabilidad iniciada y la matriz contable completa por la plantilla. Créditos de 36 cuotas desembolsados hoy; el reloj de la
/// suite se lleva a instantes exactos (día y hora de Colombia) después de la fecha <b>exigible</b> de la primera cuota y el ciclo diario se corre a mano
/// (la fixture deja el procesador apagado), igual que <c>cartera.mensajes</c>.
/// <para>Cooperativa «cobranza» (una sola línea de tiempo): la versión de etapas del ensayo (las de la semilla con un gasto de cobranza en la
/// administrativa) y una regla de reparto al gestor;</para>
/// <list type="bullet">
/// <item>el primer ciclo en mora cae en <b>domingo</b>: los créditos entran a la etapa administrativa, se asignan al gestor por la regla, y el mensaje de
/// la etapa nace <c>Deferred</c> (<c>Sunday</c>) y sale el día hábil siguiente desde las 7:00 (US12-1, prueba independiente);</item>
/// <item>una llamada con contacto directo fija el canal de la semana: un SMS de cobranza inmediato días después responde
/// <c>Lending.DebtorMessage.WeeklyChannelUsed</c> (US12-2);</item>
/// <item>una promesa se cumple sola con el pago del recaudador de prueba y otra queda incumplida y alerta al gestor (US12-3);</item>
/// <item>una campaña por WhatsApp excluye con constancia al deudor que no autorizó el canal (US12-4);</item>
/// <item>el aviso previo del episodio exige dos comunicaciones en días distintos (umbral del ensayo) y queda válido con la fecha más temprana de
/// reporte (US12-5);</item>
/// <item>el gasto de cobranza de la etapa sólo se causa después de salir su aviso (<c>GastoDeCobranzaCausado</c>, FR-121);</item>
/// <item>un reclamo sobre el reporte vencido sin respuesta queda aceptado tácitamente por el ciclo;</item>
/// <item>pasados los 30 días de mora el crédito cambia a la prejurídica y conserva su gestor.</item>
/// </list>
/// Cooperativa «juridico» (etapas comprimidas para no esperar 91 días): un acuerdo de pago al día congela la etapa y, incumplido, el crédito vuelve a la
/// que le corresponde; el cobro jurídico sólo se abre en la etapa jurídica y una insolvencia lo suspende (FR-120, FR-124, FR-098). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CobranzaYAvisoPrevioTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Etapas_gestor_ventana_promesas_campana_aviso_previo_gasto_y_reclamo()
    {
        var esc = await EscenarioAsync(fx, "cobranza");
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var tenant = esc.Coop.TenantPublicId;
        var u = esc.Usuarios;
        var hoy = esc.Hoy;
        using var reloj = new CobranzaE2E.Reloj(fx);
        var t = new CobranzaE2E.Tokens(http, u);
        var cartera = await t.AdministradorAsync();

        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false);
        await UsuariosDeCartera.FijarLineasAsync(http, admin, u.Gestor, CobranzaE2E.Lineas(esc));
        // Las etapas de la semilla con un gasto de cobranza en la administrativa (10 % de lo vencido, hasta $ 5.000) y la acción «aviso previo».
        await CobranzaE2E.EtapasAsync(http, cartera, hoy, admin: (1, 30), prejuridica: (31, 90), juridica: 91, conGasto: true, conPlantillas: true);
        // El umbral del ensayo (5 SMMLV, más estricto que el legal): un crédito de un millón exige dos comunicaciones del aviso previo.
        await CarteraE2E.ExitoAsync(http, cartera, HttpMethod.Post, "/api/lending/parameters/AvisoPrevio.UmbralDosComunicaciones/versions", new
        {
            scopeKind = 0, value = "5", validFrom = hoy, reason = "El ensayo exige dos comunicaciones del aviso previo (e2e)",
        });
        var regla = await CarteraE2E.ExitoAsync(http, cartera, HttpMethod.Post, "/api/lending/collector-assignments/rules", new
        {
            priority = 1, distribution = "ByCount", collectors = new[] { new { userPublicId = u.Gestor.PublicId, weight = 1m } }, validFrom = hoy,
            reason = "Toda la cartera en cobranza al gestor del ensayo (e2e)",
        });
        // La plantilla aprobada ante el proveedor de WhatsApp (I15): sin ella el canal no se usa.
        (await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/message-templates/MORAADMIN/versions", new
        {
            purpose = "Collection", channel = "WhatsApp", providerTemplateId = "moraadmin_e2e_v1", validFrom = hoy,
            body = "{{NombreDeLaCooperativa}}: su credito {{NumeroDelCredito}} tiene {{ValorVencido}} vencido a la fecha {{FechaDeCorte}}.",
            reason = "Plantilla de WhatsApp aprobada ante el proveedor (e2e)",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        await CobranzaE2E.PoliticaDeContactoAsync(http, admin);
        var (ana, _) = await DeudorAsync(http, admin, "Ana");
        await CobranzaE2E.AutorizarCanalAsync(http, admin, ana, "Sms");
        await CobranzaE2E.AutorizarCanalAsync(http, admin, ana, "WhatsApp");
        var (beto, _) = await DeudorAsync(http, admin, "Beto");
        var (carla, _) = await DeudorAsync(http, admin, "Carla");
        var (dora, _) = await DeudorAsync(http, admin, "Dora");
        var (eva, _) = await DeudorAsync(http, admin, "Eva");
        var deAna = await CierreE2E.DesembolsadoAsync(http, esc, ana, 1_000_000m);
        var deBeto = await CierreE2E.DesembolsadoAsync(http, esc, beto, 1_000_000m);
        var deCarla = await CierreE2E.DesembolsadoAsync(http, esc, carla, 1_000_000m);
        var deDora = await CierreE2E.DesembolsadoAsync(http, esc, dora, 1_000_000m);
        var deEva = await CierreE2E.DesembolsadoAsync(http, esc, eva, 1_000_000m);

        // El reclamo sobre el reporte, recibido hoy: su plazo vence en días hábiles nacionales, antes de la mora.
        var reclamo = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/credit-report-claims", new
        {
            personPublicId = ana, loanPublicId = deAna, kind = "Claim", receivedAt = hoy, channel = "Escrito", description = "Pide corregir un reporte (e2e)",
        });
        reclamo.GetProperty("status").GetInt32().Should().Be((int)CreditReportClaimStatus.InProcess);
        var idReclamo = reclamo.GetProperty("publicId").GetGuid();
        var venceReclamo = DateOnly.Parse(reclamo.GetProperty("dueDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        venceReclamo.Should().BeAfter(hoy, "el plazo se cuenta en días hábiles");

        var exigible = await ExigibleAsync(http, admin, deAna);
        var calendario = await CobranzaE2E.CalendarioAsync(fx, esc, exigible, exigible.AddDays(60));
        var domingo = exigible.AddDays(1);
        while (domingo.DayOfWeek != DayOfWeek.Sunday) domingo = domingo.AddDays(1);

        // ======================================================= (1) el ciclo del domingo: etapa, gestor y mensaje diferido ==
        reloj.Ir(domingo, 10);
        await CicloAsync(http, t, tenant);
        var etapas = await CobranzaE2E.EtapasDelInformeAsync(http, cartera, domingo);
        foreach (var credito in new[] { deAna, deBeto, deCarla, deDora, deEva })
            etapas.GetValueOrDefault(credito).Should().Be("ADMIN", "en mora entre 1 y 30 días el crédito entra a la administrativa");
        var asignaciones = await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/collector-assignments?loanPublicId={deAna}");
        var asignacion = asignaciones.Should().ContainSingle().Subject;
        asignacion.GetProperty("collector").GetProperty("userPublicId").GetGuid().Should().Be(u.Gestor.PublicId, "la regla reparte al gestor");
        asignacion.GetProperty("rulePublicId").GetGuid().Should().Be(regla.GetProperty("publicId").GetGuid());

        var mensajesDeAna = await CobranzaE2E.MensajesAsync(fx, esc, ana);
        var deLaEtapa = mensajesDeAna.Should().ContainSingle(m => m.Plantilla == "MORAADMIN" && m.Origen == "CollectionStage").Subject;
        deLaEtapa.Canal.Should().Be(MessageChannel.Sms, "el correo no está autorizado y el SMS sí");
        deLaEtapa.Estado.Should().Be(DebtorMessageEventKind.Deferred, "el domingo no se contacta");
        deLaEtapa.Eventos[^1].Motivos.Should().Contain(ContactWindowReason.Sunday);
        var siguienteVentana = deLaEtapa.Eventos[^1].Vence!.Value;
        siguienteVentana.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        siguienteVentana.TimeOfDay.Should().Be(TimeSpan.FromHours(7), "la primera hora permitida entre semana");
        var habilSiguiente = DateOnly.FromDateTime(siguienteVentana);
        (await CobranzaE2E.MensajesAsync(fx, esc, carla)).Where(m => m.Origen == "CollectionStage").Should()
            .OnlyContain(m => m.Estado == DebtorMessageEventKind.Excluded, "Carla no autorizó ningún canal: queda la constancia");

        // El aviso previo del episodio, con dos comunicaciones exigidas; y el gasto de la etapa, propuesto e informado pero sin salir.
        var aviso = (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/prior-notices?loanPublicId={deAna}")).Should().ContainSingle().Subject;
        aviso.GetProperty("requiredCommunications").GetInt32().Should().Be(2, "el saldo está bajo el umbral del ensayo");
        var gastos = (await CarteraE2E.GetAsync(http, cartera, $"/api/lending/collection-fees/pending?personPublicId={ana}")).EnumerateArray().ToList();
        var gasto = gastos.Should().ContainSingle().Subject;
        gasto.GetProperty("stageCode").GetString().Should().Be("ADMIN");
        gasto.GetProperty("amount").GetDecimal().Should().BeInRange(0.01m, 5_000m);
        gasto.GetProperty("causable").GetBoolean().Should().BeFalse("su aviso todavía no sale");
        var idAvisoDeGasto = gasto.GetProperty("noticePublicId").GetGuid();
        var antes = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/collection-fees/cause",
            new { personPublicId = ana, noticePublicIds = new[] { idAvisoDeGasto } });
        antes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await antes.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(antes)).Should().Be("Lending.CollectionFee.NoticeNotSent");

        // El reclamo vencido sin respuesta lo acepta el ciclo.
        if (venceReclamo < domingo)
            (await ReclamoAsync(http, cartera, ana, idReclamo)).Should().Be((int)CreditReportClaimStatus.TacitlyAccepted);

        // ========================================= (2) el día hábil siguiente: sale el mensaje; gestiones con contacto directo ==
        await DiaAsync(habilSiguiente);
        var salioElDeLaEtapa = (await CobranzaE2E.MensajesAsync(fx, esc, ana)).Single(m => m.PublicId == deLaEtapa.PublicId).Salio;
        salioElDeLaEtapa.Should().NotBeNull("llegada la ventana, cartera.mensajes lo despacha");
        salioElDeLaEtapa!.En.Should().BeOnOrAfter(siguienteVentana);
        salioElDeLaEtapa.En.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);

        var semana = CobranzaE2E.SemanaDe(habilSiguiente);
        var otroDia = new[] { habilSiguiente.AddDays(2), habilSiguiente.AddDays(1), habilSiguiente.AddDays(3) }
            .First(d => calendario.EsHabil(d) && CobranzaE2E.SemanaDe(d) == semana);
        reloj.Ir(habilSiguiente, 10, 30);
        var gestor = await t.GestorAsync();
        var alas10 = CobranzaE2E.Local(habilSiguiente, 10);
        var llamada = await CobranzaE2E.GestionAsync(http, gestor, deBeto, beto, alas10, "ContactedNoCommitment");
        llamada.GetProperty("isDirectContact").GetBoolean().Should().BeTrue();
        llamada.GetProperty("windowDecision").GetString().Should().Be("Permitido");
        var promesaDeDora = await CobranzaE2E.GestionAsync(http, gestor, deDora, dora, alas10, "PromiseToPay", new { date = otroDia, amount = 20_000m });
        var promesaDeEva = await CobranzaE2E.GestionAsync(http, gestor, deEva, eva, alas10, "PromiseToPay", new { date = otroDia, amount = 20_000m });
        promesaDeDora.GetProperty("promise").GetProperty("status").GetInt32().Should().Be((int)PaymentPromiseStatus.Pending);
        // Beto autoriza el SMS después de la llamada: aun así, esa semana el canal es el teléfono.
        await CobranzaE2E.AutorizarCanalAsync(http, admin, beto, "Sms", reloj.UtcAhora.AddMinutes(-1));

        for (var d = habilSiguiente.AddDays(1); d < otroDia; d = d.AddDays(1)) await DiaAsync(d);

        // ============================================= (3) otro día de la semana: canal semanal, promesa cumplida y campaña ==
        await DiaAsync(otroDia);
        reloj.Ir(otroDia, 10);
        gestor = await t.GestorAsync();
        var porSms = await CarteraE2E.ConClaveAsync(http, gestor, HttpMethod.Post, CobranzaE2E.Mensajes, new
        {
            loanPublicId = deBeto, recipientPersonPublicId = beto, channel = "Sms", purpose = "Collection", templateCode = "MORAADMIN",
            variables = new Dictionary<string, string> { ["ValorVencido"] = "$ 40.000", ["DiasDeMora"] = "10", ["CuotasVencidas"] = "1", ["ValorParaPonerseAlDia"] = "$ 40.000" },
        });
        porSms.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await porSms.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(porSms)).Should().Be("Lending.DebtorMessage.WeeklyChannelUsed", "la llamada de la semana fijó el teléfono");
        var siguientePermitido = DateTimeOffset.Parse((await CarteraE2E.LeerAsync(porSms)).GetProperty("data").GetProperty("nextAllowedAt").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        CobranzaE2E.SemanaDe(DateOnly.FromDateTime(siguientePermitido.DateTime)).Should().BeAfter(semana, "el canal se libera la semana siguiente");

        await t.PagarAsync(Pago(deDora, dora, "FreeAmount", 20_000m, otroDia));
        var cumplida = (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/promises?loanPublicId={deDora}")).Should().ContainSingle().Subject;
        cumplida.GetProperty("status").GetInt32().Should().Be((int)PaymentPromiseStatus.Kept, "el pago llegó a tiempo: la promesa se cumple sola");
        cumplida.GetProperty("keptOn").GetString().Should().Be(otroDia.ToString("yyyy-MM-dd"));

        cartera = await t.AdministradorAsync();
        var campana = await CarteraE2E.ExitoAsync(http, cartera, HttpMethod.Post, "/api/lending/campaigns", new
        {
            code = "MORAWA1", name = "Mora administrativa por WhatsApp (e2e)", templateCode = "MORAADMIN", channel = "WhatsApp", purpose = "Collection",
            filters = new { minDaysPastDue = 1 }, scheduledFor = new DateTimeOffset(CobranzaE2E.Local(otroDia, 11), reloj.Desfase),
        });
        campana.GetProperty("status").GetInt32().Should().Be((int)MessageCampaignStatus.Draft);
        var idCampana = campana.GetProperty("publicId").GetGuid();
        var previa = await CarteraE2E.LeerAsync(await CarteraE2E.EnviarAsync(http, cartera, HttpMethod.Post, $"/api/lending/campaigns/{idCampana}/preview", null));
        previa.GetProperty("recipients").EnumerateArray().Select(r => r.GetProperty("loan").GetProperty("loanPublicId").GetGuid()).Should().Contain(deAna);
        var excluida = previa.GetProperty("excluded").EnumerateArray()
            .Single(x => x.GetProperty("loan").GetProperty("loanPublicId").GetGuid() == deCarla);
        excluida.GetProperty("reason").GetString().Should().Be("ChannelNotAuthorized", "Carla no autorizó WhatsApp");
        var programada = await CarteraE2E.LeerAsync(await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, $"/api/lending/campaigns/{idCampana}/schedule",
            new { reason = "Campaña de la semana (e2e)" }));
        programada.GetProperty("status").GetInt32().Should().Be((int)MessageCampaignStatus.Scheduled, programada.ToString());
        programada.GetProperty("counts").GetProperty("excluded").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        var deCampana = (await CobranzaE2E.MensajesAsync(fx, esc, carla)).Single(m => m.Origen == "Campaign");
        deCampana.Estado.Should().Be(DebtorMessageEventKind.Excluded, "la constancia de la exclusión queda en el registro");
        deCampana.Eventos[^1].Motivos.Should().Contain(ContactWindowReason.ChannelNotAuthorized);
        (await CobranzaE2E.MensajesAsync(fx, esc, ana)).Should().Contain(m => m.Origen == "Campaign" && m.Canal == MessageChannel.WhatsApp
            && m.Estado != DebtorMessageEventKind.Excluded, "Ana sí lo autorizó");

        // ============================================================ (4) el día siguiente: la promesa de Eva se incumple ==
        var despues = otroDia.AddDays(1);
        await DiaAsync(despues);
        var rota = (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/promises?loanPublicId={deEva}")).Should().ContainSingle().Subject;
        rota.GetProperty("status").GetInt32().Should().Be((int)PaymentPromiseStatus.Broken, "venció sin el pago");
        var alertas = await CobranzaE2E.ItemsAsync(http, cartera, "/api/lending/alerts?typeCode=Cartera.PromesaIncumplida&pageSize=50");
        alertas.Should().Contain(a => a.ToString().Contains(rota.GetProperty("loan").GetProperty("number").GetString()!, StringComparison.Ordinal),
            "la promesa incumplida vuelve a la agenda del gestor con su alerta");

        // ============================================== (5) los días siguientes: aviso previo con dos comunicaciones y gasto ==
        JsonElement avisoValido = default;
        var dia = despues;
        for (var i = 0; i < 15; i++)
        {
            avisoValido = (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/prior-notices?loanPublicId={deAna}")).Single();
            var informado = (await CarteraE2E.GetAsync(http, cartera, $"/api/lending/collection-fees/pending?personPublicId={ana}")).EnumerateArray()
                .Any(g => g.GetProperty("noticePublicId").GetGuid() == idAvisoDeGasto && g.GetProperty("causable").GetBoolean());
            if (avisoValido.GetProperty("status").GetInt32() == (int)PriorNoticeStatus.Valid && informado) break;
            dia = dia.AddDays(1);
            await DiaAsync(dia);
        }
        avisoValido.GetProperty("status").GetInt32().Should().Be((int)PriorNoticeStatus.Valid, avisoValido.ToString());
        var comunicaciones = avisoValido.GetProperty("communications").EnumerateArray().Where(c => c.GetProperty("evidence").GetBoolean()).ToList();
        comunicaciones.Should().HaveCount(2, "bajo el umbral se exigen dos comunicaciones");
        comunicaciones.Select(c => DateTimeOffset.Parse(c.GetProperty("sentAt").GetString()!, System.Globalization.CultureInfo.InvariantCulture).Date)
            .Distinct().Should().HaveCount(2, "en días distintos");
        avisoValido.GetProperty("earliestReportDate").ValueKind.Should().Be(JsonValueKind.String, "con la constancia se sabe desde cuándo se puede reportar");

        var causar = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/collection-fees/cause",
            new { personPublicId = ana, noticePublicIds = new[] { idAvisoDeGasto } });
        causar.StatusCode.Should().Be(HttpStatusCode.OK, await causar.Content.ReadAsStringAsync());
        var causado = (await CarteraE2E.LeerAsync(causar)).EnumerateArray().Should().ContainSingle().Subject;
        causado.GetProperty("amount").GetDecimal().Should().Be(gasto.GetProperty("amount").GetDecimal());
        (await OperacionesAsync(fx, esc, deAna)).Should().Contain(o => o.Clase == LoanOperationKind.Charge && o.PublicId == causado.GetProperty("operationPublicId").GetGuid());
        (await ActasE2E.HechosAsync(http, admin, deAna, "GastoDeCobranzaCausado")).Should().ContainSingle("el gasto causado emite su hecho contable");
        var otraVez = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/collection-fees/cause",
            new { personPublicId = ana, noticePublicIds = new[] { idAvisoDeGasto } });
        (await CarteraE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Lending.CollectionFee.AlreadyCaused");

        venceReclamo.Should().BeBefore(dia, "el ciclo ya corrió después del vencimiento del reclamo");
        (await ReclamoAsync(http, cartera, ana, idReclamo)).Should().Be((int)CreditReportClaimStatus.TacitlyAccepted, "vencido sin respuesta");

        // ================================================= (6) pasados los 30 días de mora: prejurídica con el mismo gestor ==
        var prejuridica = exigible.AddDays(31);
        if (prejuridica > dia)
        {
            reloj.Ir(prejuridica, 9);
            await CicloAsync(http, t, tenant);
            etapas = await CobranzaE2E.EtapasDelInformeAsync(http, cartera, prejuridica);
            etapas[deAna].Should().Be("PREJURID", "un crédito que pasa de 30 días cambia de etapa en el ciclo siguiente");
            (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/collector-assignments?loanPublicId={deAna}")).Should()
                .ContainSingle("quien ya tiene al gestor de la regla lo conserva");
        }

        // Un día del ensayo: el despacho de las 7:05, el ciclo de las 9:00 y otro despacho.
        async Task DiaAsync(DateOnly d)
        {
            reloj.Ir(d, 7, 5);
            await CobranzaE2E.DespacharAsync(fx, tenant);
            reloj.Ir(d, 9);
            await CicloAsync(http, t, tenant);
            reloj.Ir(d, 9, 5);
            await CobranzaE2E.DespacharAsync(fx, tenant);
        }
    }

    [Fact]
    public async Task El_acuerdo_incumplido_vuelve_a_su_etapa_y_la_insolvencia_suspende_el_cobro_juridico()
    {
        var esc = await EscenarioAsync(fx, "juridico");
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var tenant = esc.Coop.TenantPublicId;
        var u = esc.Usuarios;
        var hoy = esc.Hoy;
        using var reloj = new CobranzaE2E.Reloj(fx);
        var t = new CobranzaE2E.Tokens(http, u);
        var cartera = await t.AdministradorAsync();

        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false);
        await UsuariosDeCartera.FijarLineasAsync(http, admin, u.Gestor, CobranzaE2E.Lineas(esc));
        // Etapas comprimidas (la jurídica desde los 21 días) para no esperar los 91 de la semilla; sin mensajes ni gasto.
        await CobranzaE2E.EtapasAsync(http, cartera, hoy, admin: (1, 10), prejuridica: (11, 20), juridica: 21, conGasto: false, conPlantillas: false);
        var (fabio, _) = await DeudorAsync(http, admin, "Fabio");
        var (gina, _) = await DeudorAsync(http, admin, "Gina");
        var abogado = await CarteraE2E.PersonaAsync(http, admin, "Abogada", "Cobro");
        var deFabio = await CierreE2E.DesembolsadoAsync(http, esc, fabio, 1_000_000m);
        var deGina = await CierreE2E.DesembolsadoAsync(http, esc, gina, 1_000_000m);
        var exigible = await ExigibleAsync(http, admin, deFabio);

        reloj.Ir(exigible.AddDays(5), 9);
        await CicloAsync(http, t, tenant);

        // ===================================================== el acuerdo sobre lo vencido, aprobado por otra persona ==
        var propuesta = exigible.AddDays(9);
        reloj.Ir(propuesta, 9);
        await CicloAsync(http, t, tenant);
        var vence = exigible.AddDays(11);
        var acuerdo = await CarteraE2E.ExitoAsync(http, await t.GestorAsync(), HttpMethod.Post, "/api/lending/payment-agreements", new
        {
            loanPublicId = deFabio, installments = new[] { new { dueDate = vence, amount = 10_000m } }, reason = "Se pone al día en dos días (e2e)",
        });
        acuerdo.GetProperty("status").GetInt32().Should().Be((int)PaymentAgreementStatus.Proposed);
        var idAcuerdo = acuerdo.GetProperty("publicId").GetGuid();
        var aprobado = await CarteraE2E.ExitoAsync(http, cartera, HttpMethod.Post, $"/api/lending/payment-agreements/{idAcuerdo}/approve", new { });
        aprobado.GetProperty("status").GetInt32().Should().Be((int)PaymentAgreementStatus.Approved);
        aprobado.GetProperty("performance").GetInt32().Should().Be((int)PaymentAgreementPerformance.Current);

        reloj.Ir(exigible.AddDays(10), 9);
        await CicloAsync(http, t, tenant);
        reloj.Ir(vence, 9);
        await CicloAsync(http, t, tenant);
        var etapas = await CobranzaE2E.EtapasDelInformeAsync(http, cartera, vence);
        etapas[deGina].Should().Be("PREJURID", "a los 11 días de mora");
        etapas[deFabio].Should().Be("ADMIN", "el acuerdo al día congela la etapa");
        var fuera = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/legal-processes", new
        {
            loanPublicIds = new[] { deGina }, lawyerPersonPublicId = abogado, court = "Juzgado 1 Civil Municipal (e2e)", fileNumber = "2026-00001", startDate = vence,
        });
        fuera.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await fuera.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(fuera)).Should().Be("Lending.LegalProcess.LoanNotInLegalStage");

        // Sin el pago, al día siguiente del vencimiento el acuerdo se incumple y el crédito vuelve a la etapa que le corresponde.
        var incumplido = vence.AddDays(1);
        reloj.Ir(incumplido, 9);
        await CicloAsync(http, t, tenant);
        etapas = await CobranzaE2E.EtapasDelInformeAsync(http, cartera, incumplido);
        etapas[deFabio].Should().Be("PREJURID");
        var entrada = await EnLaBaseAsync(fx, esc, db => (from h in db.CollectionStageHistory.AsNoTracking()
                                                          join l in db.Loans.AsNoTracking() on h.LoanId equals l.Id
                                                          where l.PublicId == deFabio
                                                          orderby h.EnteredOn descending, h.Id descending
                                                          select new { h.StageCode, h.EnteredOn, h.Reason, h.Source }).FirstAsync());
        entrada.EnteredOn.Should().Be(incumplido);
        entrada.Reason.Should().Be("Acuerdo de pago incumplido");
        var roto = (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/payment-agreements?loanPublicId={deFabio}")).Single();
        roto.GetProperty("performance").GetInt32().Should().Be((int)PaymentAgreementPerformance.Broken);
        roto.GetProperty("compliance").GetProperty("brokenOn").GetString().Should().Be(incumplido.ToString("yyyy-MM-dd"));

        // ================================================== el cobro jurídico en la etapa jurídica; la insolvencia lo suspende ==
        var juridica = exigible.AddDays(21);
        reloj.Ir(juridica, 9);
        await CicloAsync(http, t, tenant);
        (await CobranzaE2E.EtapasDelInformeAsync(http, cartera, juridica))[deGina].Should().Be("JURIDICA");
        cartera = await t.AdministradorAsync();
        var abierto = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/legal-processes", new
        {
            loanPublicIds = new[] { deGina }, lawyerPersonPublicId = abogado, court = "Juzgado 1 Civil Municipal (e2e)", fileNumber = "2026-00002", startDate = juridica,
        });
        abierto.StatusCode.Should().Be(HttpStatusCode.Created, await abierto.Content.ReadAsStringAsync());
        var proceso = (await CarteraE2E.LeerAsync(abierto)).EnumerateArray().Should().ContainSingle().Subject;
        proceso.GetProperty("status").GetInt32().Should().Be((int)LegalProcessStatus.Assigned);
        proceso.GetProperty("lawyer").GetProperty("personPublicId").GetGuid().Should().Be(abogado);

        var soporte = await ActasE2E.SoporteAsync(http, cartera, gina, "LendingDebtorEventSupport", "insolvencia.pdf");
        var insolvencia = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/insolvency-proceedings", new
        {
            personPublicId = gina, startDate = juridica, regime = "NaturalPersonDebtNegotiation", authority = "Centro de conciliación (e2e)", fileNumber = "NEG-K4-001",
            supportAttachmentPublicId = soporte,
        });
        insolvencia.StatusCode.Should().Be(HttpStatusCode.Created, await insolvencia.Content.ReadAsStringAsync());
        var idInsolvencia = (await CarteraE2E.LeerAsync(insolvencia)).GetProperty("publicId").GetGuid();
        var suspendido = (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/legal-processes?loanPublicId={deGina}")).Should().ContainSingle().Subject;
        suspendido.GetProperty("status").GetInt32().Should().Be((int)LegalProcessStatus.Suspended, "la insolvencia suspende el cobro jurídico (FR-098)");
        suspendido.GetProperty("suspendedByInsolvencyPublicId").GetGuid().Should().Be(idInsolvencia);

        // ===================================== K4, bloque D: reanudar a mano, sólo cuando la insolvencia ya no está en negociación ==
        var idProceso = suspendido.GetProperty("publicId").GetGuid();
        var antesDeTiempo = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, $"/api/lending/legal-processes/{idProceso}/resume",
            new { reason = "Se intenta reanudar en plena negociación (e2e)" });
        antesDeTiempo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await antesDeTiempo.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(antesDeTiempo)).Should().Be("Lending.LegalProcess.InsolvencyNegotiating");
        var fracaso = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, $"/api/lending/insolvency-proceedings/{idInsolvencia}/failure", new
        {
            date = juridica, reason = "No hubo acuerdo (e2e)", supportAttachmentPublicId = soporte,
        });
        fracaso.StatusCode.Should().Be(HttpStatusCode.OK, await fracaso.Content.ReadAsStringAsync());
        (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/legal-processes?loanPublicId={deGina}")).Single().GetProperty("status").GetInt32()
            .Should().Be((int)LegalProcessStatus.Suspended, "terminar la insolvencia no reanuda el proceso solo");
        var reanudado = await CarteraE2E.ExitoAsync(http, cartera, HttpMethod.Post, $"/api/lending/legal-processes/{idProceso}/resume",
            new { reason = "La negociación de insolvencia fracasó (e2e)" });
        reanudado.GetProperty("status").GetInt32().Should().Be((int)LegalProcessStatus.Assigned, "sin radicación vuelve a asignado");
        reanudado.GetProperty("suspendedByInsolvencyPublicId").ValueKind.Should().Be(JsonValueKind.Null);

        // ================================================= K4, bloque D: Fabio se pone al día y sale de cobranza al día siguiente ==
        var vencidoDeFabio = (await CarteraE2E.GetAsync(http, await t.PruebasAsync(), $"{Recaudador}/obligations?personPublicId={fabio}"))
            .GetProperty("value").GetProperty("obligations").EnumerateArray().Single(o => o.GetProperty("obligationPublicId").GetGuid() == deFabio)
            .GetProperty("pastDue").GetDecimal();
        vencidoDeFabio.Should().BePositive();
        await t.PagarAsync(Pago(deFabio, fabio, "PastDue", vencidoDeFabio, juridica));
        var alDia = juridica.AddDays(1);
        reloj.Ir(alDia, 9);
        await CicloAsync(http, t, tenant);
        var salida = await EnLaBaseAsync(fx, esc, db => (from h in db.CollectionStageHistory.AsNoTracking()
                                                         join l in db.Loans.AsNoTracking() on h.LoanId equals l.Id
                                                         where l.PublicId == deFabio
                                                         orderby h.EnteredOn descending, h.Id descending
                                                         select new { h.Source, h.Reason, h.EnteredOn }).FirstAsync());
        (salida.Source, salida.Reason, salida.EnteredOn).Should().Be(("Exit", "Salida por pago al día", alDia));
        (await CobranzaE2E.EtapasDelInformeAsync(http, cartera, alDia)).Should().NotContainKey(deFabio, "fuera de cobranza no tiene etapa");
        var deSistema = (await CobranzaE2E.ItemsAsync(http, cartera, $"/api/lending/collection-actions?loanPublicId={deFabio}"))
            .Should().ContainSingle(g => g.GetProperty("channel").GetInt32() == (int)CollectionActionChannel.System).Subject;
        deSistema.GetProperty("result").GetInt32().Should().Be((int)CollectionActionResult.LeftCollection);
        deSistema.GetProperty("isDirectContact").GetBoolean().Should().BeFalse();
        deSistema.GetProperty("collector").ValueKind.Should().Be(JsonValueKind.Null);
        deSistema.GetProperty("notes").GetString().Should().Be($"Sale de cobranza: se puso al día el {alDia:dd/MM/yyyy}");
        var gestionFuera = await CarteraE2E.ConClaveAsync(http, await t.GestorAsync(), HttpMethod.Post, "/api/lending/collection-actions", new
        {
            loanPublicId = deFabio, at = new DateTimeOffset(CobranzaE2E.Local(alDia, 8, 30), TimeSpan.FromHours(-5)), channel = "Phone",
            contactedPersonPublicId = fabio, isDirectContact = true, resultCode = "ContactedNoCommitment",
        });
        (await CarteraE2E.CodigoDeErrorAsync(gestionFuera)).Should().Be("Lending.Collections.LoanNotInCollection");

        // ================================== K4, bloque D (5a): la orden manual del registro de la etapa migrada lleva su motivo ==
        var orden = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/process-runs",
            new { kind = "MigratedCollectionStages", reason = "Registro de la etapa migrada al desplegar K4 (e2e)" });
        orden.StatusCode.Should().Be(HttpStatusCode.Accepted, await orden.Content.ReadAsStringAsync());
        var sinMotivo = await CarteraE2E.ConClaveAsync(http, cartera, HttpMethod.Post, "/api/lending/process-runs", new { kind = "MigratedCollectionStages" });
        sinMotivo.StatusCode.Should().Be(HttpStatusCode.BadRequest, "sin motivo la validación lo rechaza");
    }

    /// <summary>Ordena y corre el ciclo del día del reloj de la suite con el jefe (access fresco).</summary>
    private async Task CicloAsync(HttpClient http, CobranzaE2E.Tokens t, Guid tenant) => await CicloDeHoyAsync(fx, http, await t.JefeAsync(), tenant);

    private static async Task<int> ReclamoAsync(HttpClient http, string token, Guid persona, Guid reclamo) =>
        (await CobranzaE2E.ItemsAsync(http, token, $"/api/lending/credit-report-claims?personPublicId={persona}"))
        .Single(r => r.GetProperty("publicId").GetGuid() == reclamo).GetProperty("status").GetInt32();
}

/// <summary>
/// Los ayudantes de las e2e de la cobranza (feature 013, K4, T0933, T0934): el reloj de la suite en instantes exactos de Colombia, el calendario de la
/// cooperativa, las autorizaciones de canal, las etapas del ensayo, el informe de la cartera por etapa, los mensajes al deudor con su historia leídos en
/// la base (los instantes en hora local, igual en los dos motores) y las pasadas de <c>cartera.mensajes</c>. (nuevo)
/// </summary>
public static class CobranzaE2E
{
    public const string Mensajes = "/api/lending/debtor-messages";

    /// <summary>
    /// El reloj de la suite llevado a instantes locales exactos (<see cref="Ir(DateTime)"/>); al desecharlo vuelve a su desfase original. Las pruebas de
    /// la colección corren una tras otra, así que mover el reloj del host no pisa a nadie.
    /// </summary>
    public sealed class Reloj : IDisposable
    {
        private readonly RelojDeLaSuite _reloj;
        private readonly TimeSpan _original;

        public Reloj(CentralIdentityApiFixture fx)
        {
            _reloj = new ConductorDelDespachador(fx).Reloj;
            _original = _reloj.Desfase;
        }

        public DateTime Ahora => _reloj.AhoraLocal.DateTime;
        public DateTime UtcAhora => _reloj.UtcNow;
        public TimeSpan Desfase => _reloj.AhoraLocal.Offset;

        public void Ir(DateTime local)
        {
            var actual = _reloj.AhoraLocal;
            _reloj.Desfase += new DateTimeOffset(local, actual.Offset) - actual;
        }

        public void Ir(DateOnly dia, int hora, int minuto = 0) => Ir(Local(dia, hora, minuto));

        public void Dispose() => _reloj.Desfase = _original;
    }

    /// <summary>Los access de los usuarios de Cartera, renovados cada diez minutos (el access dura quince).</summary>
    public sealed class Tokens(HttpClient http, UsuariosDeCartera u)
    {
        private readonly Dictionary<string, (string Token, DateTime En)> _cache = [];

        public Task<string> AdministradorAsync() => DeAsync(u.Administrador);
        public Task<string> GestorAsync() => DeAsync(u.Gestor);
        public Task<string> JefeAsync() => DeAsync(u.Jefe);

        public Task<string> PruebasAsync() => DeAsync(u.Pruebas);

        /// <summary>Un pago por el recaudador de prueba (<c>pruebas</c>, access fresco); exige <c>Applied</c>.</summary>
        public async Task<JsonElement> PagarAsync(Dictionary<string, object?> pago)
        {
            var resp = await CarteraE2E.ConClaveAsync(http, await PruebasAsync(), HttpMethod.Post, $"{PagosE2E.Recaudador}/payments",
                new { items = new[] { pago }, interactive = false });
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aplicar: «{await resp.Content.ReadAsStringAsync()}»");
            var r = (await CarteraE2E.LeerAsync(resp)).GetProperty("payments")[0];
            PagosE2E.Resultado(r).Should().Be(PagosE2E.Aplicado, r.ToString());
            return r.GetProperty("value");
        }

        private async Task<string> DeAsync(UsuariosDeCartera.Usuario usuario)
        {
            if (_cache.TryGetValue(usuario.Clave, out var c) && DateTime.UtcNow - c.En < TimeSpan.FromMinutes(10)) return c.Token;
            var token = await UsuariosDeCartera.IngresarAsync(http, usuario);
            _cache[usuario.Clave] = (token, DateTime.UtcNow);
            return token;
        }
    }

    public static DateTime Local(DateOnly dia, int hora, int minuto = 0) => dia.ToDateTime(new TimeOnly(hora, minuto));

    /// <summary>El lunes de la semana calendario (lunes a domingo) del día.</summary>
    public static DateOnly SemanaDe(DateOnly dia) => dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    /// <summary>Las líneas del escenario del desembolso (para el alcance del gestor).</summary>
    public static Guid[] Lineas(Escenario esc) =>
        esc.Originacion.ConDocumento is { } d ? [esc.Originacion.Origen, esc.LineaConPagare, d] : [esc.Originacion.Origen, esc.LineaConPagare];

    /// <summary>El calendario de días hábiles de la cooperativa (lunes a viernes sin festivos nacionales) entre las dos fechas.</summary>
    public static Task<CalendarioHabil> CalendarioAsync(CentralIdentityApiFixture fx, Escenario esc, DateOnly desde, DateOnly hasta,
        IReadOnlyCollection<DayOfWeek>? descanso = null) =>
        EnLaBaseAsync(fx, esc, db => new CalendarioDeDiasHabiles(db).ObtenerAsync(desde, hasta, descanso ?? DiasDeDescanso.SabadoYDomingo));

    /// <summary>La política de datos de la finalidad «canal de contacto», para registrar autorizaciones de canal.</summary>
    public static async Task PoliticaDeContactoAsync(HttpClient http, string admin) =>
        (await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
        {
            title = "Política de datos: ContactChannel", contentMarkdown = "Texto de la política de contacto para las e2e de la cobranza.",
            effectiveFrom = DateTime.UtcNow.AddMinutes(-1), purpose = "ContactChannel",
        })).IsSuccessStatusCode.Should().BeTrue("hace falta la política de contacto para autorizar canales");

    /// <summary>La autorización (o la negativa) del canal con su evidencia, registrada en <paramref name="en"/> (UTC; ahora si no viene).</summary>
    public static async Task AutorizarCanalAsync(HttpClient http, string admin, Guid persona, string canal, DateTime? en = null, string decision = "Accepted")
    {
        var evidencia = await OriginacionE2E.SubirEvidenciaAsync(http, admin, persona);
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/authorizations", new
        {
            personPublicId = persona, purpose = "ContactChannel", contactChannel = canal, evidenceAttachmentPublicId = evidencia, grantedAt = en ?? DateTime.UtcNow,
            decision,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"autorización del canal {canal}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>
    /// Una versión de las cuatro etapas desde <paramref name="desde"/> con los rangos dados; con <paramref name="conGasto"/>, la administrativa propone un
    /// gasto del 10 % de lo vencido hasta $ 5.000 al entrar; con <paramref name="conPlantillas"/>, cada etapa lleva la plantilla de la semilla.
    /// </summary>
    public static async Task<JsonElement> EtapasAsync(HttpClient http, string token, DateOnly desde, (int Desde, int Hasta) admin, (int Desde, int Hasta) prejuridica,
        int juridica, bool conGasto, bool conPlantillas)
    {
        var etapas = new object[]
        {
            new { code = "PREVENTIVA", name = "Preventiva", daysFrom = -5, daysTo = (int?)0, isLegal = false, suggestedActions = new[] { "Message" },
                templateCodes = Array.Empty<string>() },
            new
            {
                code = "ADMIN", name = "Administrativa", daysFrom = admin.Desde, daysTo = (int?)admin.Hasta, isLegal = false,
                suggestedActions = new[] { "Call", "Message", "PriorNotice", "PaymentAgreement" },
                templateCodes = conPlantillas ? new[] { "MORAADMIN" } : Array.Empty<string>(),
                feeRule = conGasto ? new { @base = "PastDueAmount", rate = 0.10m, cap = (decimal?)5_000m, moment = "OnStageEntry" } : null,
            },
            new { code = "PREJURID", name = "Prejurídica", daysFrom = prejuridica.Desde, daysTo = (int?)prejuridica.Hasta, isLegal = false,
                suggestedActions = new[] { "Call", "Visit", "Message", "PaymentAgreement" }, templateCodes = conPlantillas ? new[] { "PREJURID" } : Array.Empty<string>() },
            new { code = "JURIDICA", name = "Jurídica", daysFrom = juridica, daysTo = (int?)null, isLegal = true, suggestedActions = new[] { "LegalHandover" },
                templateCodes = Array.Empty<string>() },
        };
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, "/api/lending/collection-stages/versions", new
        {
            stages = etapas, validFrom = desde, reason = "Etapas del ensayo de la cobranza (e2e)",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"versión de etapas: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>La etapa vigente de cada crédito al corte, por la vista <c>collection-stages</c> del centro de informes.</summary>
    public static async Task<Dictionary<Guid, string>> EtapasDelInformeAsync(HttpClient http, string token, DateOnly corte)
    {
        var tabla = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, token, $"/api/reports/lending/collection-stages?asOf={corte:yyyy-MM-dd}"));
        return tabla.Filas.ToDictionary(f => Guid.Parse(tabla.Oculta(f, "_credito")), f => tabla.Texto(f, "Etapa"));
    }

    /// <summary>Los <c>items</c> de una lista paginada (o el arreglo, si la ruta devuelve uno).</summary>
    public static async Task<List<JsonElement>> ItemsAsync(HttpClient http, string token, string url)
    {
        var r = await CarteraE2E.GetAsync(http, token, url);
        return (r.ValueKind == JsonValueKind.Array ? r : r.GetProperty("items")).EnumerateArray().ToList();
    }

    /// <summary>Registra una gestión por teléfono con contacto directo (y la promesa, si viene) con el gestor; exige el 201.</summary>
    public static async Task<JsonElement> GestionAsync(HttpClient http, string token, Guid credito, Guid persona, DateTime enLocal, string resultado,
        object? promesa = null, string canal = "Phone", bool directo = true)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, "/api/lending/collection-actions", new
        {
            loanPublicId = credito, at = new DateTimeOffset(enLocal, TimeSpan.FromHours(-5)), channel = canal, contactedPersonPublicId = persona,
            isDirectContact = directo, resultCode = resultado, notes = "Gestión del ensayo (e2e)", promise = promesa,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"gestión: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>Una pasada de <c>cartera.mensajes</c> ahora (el disparo manual de la fixture).</summary>
    public static Task DespacharAsync(CentralIdentityApiFixture fx, Guid tenant) => fx.CorrerTareaAsync(tenant, TareaDeMensajesAlDeudor.NombreDeLaTarea);

    // --------------------------------------------------------------------------------------------- mensajes en la base --

    /// <summary>Un evento de la historia de un mensaje, con sus instantes en hora local de Colombia.</summary>
    public sealed record Evento(DebtorMessageEventKind Clase, DateTime En, DateTime? Vence, IReadOnlyList<ContactWindowReason> Motivos);

    /// <summary>Un mensaje al deudor con su historia.</summary>
    public sealed record Mensaje(Guid PublicId, int Id, int PersonId, int? LoanId, DebtorMessagePurpose Proposito, MessageChannel Canal, string? Plantilla,
        string Origen, IReadOnlyList<Evento> Eventos)
    {
        public DebtorMessageEventKind Estado => Eventos[^1].Clase;
        public DateTime NacioEn => Eventos[0].En;

        /// <summary>El primer evento que dice que salió (<c>Sent</c>, <c>Delivered</c> o, fuera de producción, <c>Simulated</c>), o nulo.</summary>
        public Evento? Salio => Eventos.FirstOrDefault(e => e.Clase is DebtorMessageEventKind.Sent or DebtorMessageEventKind.Delivered or DebtorMessageEventKind.Simulated);
    }

    /// <summary>Los mensajes de la cooperativa (o de una persona) con su historia, leídos en la base.</summary>
    public static Task<List<Mensaje>> MensajesAsync(CentralIdentityApiFixture fx, Escenario esc, Guid? persona = null) =>
        EnLaBaseAsync(fx, esc, async db =>
        {
            var desfase = new ConductorDelDespachador(fx).Reloj.AhoraLocal.Offset;
            var consulta = db.DebtorMessages.AsNoTracking();
            if (persona is { } p)
            {
                var id = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => x.Id).SingleAsync();
                consulta = consulta.Where(m => m.PersonId == id);
            }
            var mensajes = await consulta.ToListAsync();
            var ids = mensajes.Select(m => m.Id).ToList();
            var eventos = await db.DebtorMessageEvents.AsNoTracking().Where(e => ids.Contains(e.DebtorMessageId)).ToListAsync();
            var plantillas = mensajes.Where(m => m.TemplateId != null).Select(m => m.TemplateId!.Value).Distinct().ToList();
            var codigos = await db.NotificationTemplates.AsNoTracking().IgnoreQueryFilters().Where(x => plantillas.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Code);
            return mensajes.Select(m => new Mensaje(m.PublicId, m.Id, m.PersonId, m.LoanId, m.Purpose, m.Channel,
                    m.TemplateId is { } t ? codigos.GetValueOrDefault(t) : null, m.OriginKind,
                    eventos.Where(e => e.DebtorMessageId == m.Id).OrderBy(e => e.Sequence)
                        .Select(e => new Evento(e.Kind, ALocal(e.OccurredAt, desfase), e.DueAt is { } d ? ALocal(d, desfase) : null, e.Motivos())).ToList()))
                .OrderBy(m => m.Id).ToList();
        });

    /// <summary>Un instante UTC de la base en hora local.</summary>
    public static DateTime ALocal(DateTime utc, TimeSpan desfase) => DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + desfase, DateTimeKind.Unspecified);
}
