using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0429 (feature 013, US3; quickstart §1.7, nombre fijo; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): el desembolso por HTTP
/// con tokens reales en la cooperativa aislada «desembolso», activada con <see cref="CarteraE2E.ActivarCarteraAsync"/> (C107, sin <c>Skip</c>):
/// <list type="bullet">
/// <item>US3-1: un pagaré desmaterializado del depósito <c>SIMULADO</c> con documento terminado en 3 deja la firma pendiente; desembolsar
/// responde <c>Lending.Disbursement.GuaranteesPending</c> hasta que «actualizar estado» lo deja en custodia; el desembolso emite
/// <c>CreditoDesembolsado</c> y <c>OrdenDeGiroEmitida</c> hacia Tesorería y el pagaré pasa al crédito;</item>
/// <item>US3-2: con el reloj de la suite pasado el vencimiento de la aprobación, <c>Lending.Application.ApprovalExpired</c>;</item>
/// <item>US3-5: con la usura del día del desembolso menor que la de la aprobación (una modalidad que ningún otro ensayo del host usa, registrada
/// en la central de este host para esta prueba), <c>Lending.Usury.Exceeded</c>;</item>
/// <item>US3-6: la tesorería de prueba responde «rechazada», Cartera lo consume y reversa el desembolso desde su origen (espejos con
/// <c>ReversesTransactionId</c>, <c>DesembolsoReversado</c>) y el crédito queda pendiente de giro; anularlo lo deja <c>Voided</c>;</item>
/// <item>la segregación: el analista que decidió no desembolsa (<c>Lending.Segregation.Conflict</c>), también en la vista previa;</item>
/// <item>el archivo de desembolsos con <c>CSV-DESEMBOLSOS</c>: SHA-256, enlace firmado auditado, anulación antes de resultados y
/// <c>HasResults</c> después.</item>
/// </list>
/// Con el contrato real de obligaciones (fase 9, US5, T0527): la recogida de un crédito propio al día paga por el contrato (<c>OwnLoanPayoff</c>,
/// clave <c>CAR:</c>) y deja el comprobante, el neto, el enlace y <c>PagoAplicado</c> (US3-3); la de uno en mora responde
/// <c>Lending.Payoff.RequiresRestructuring</c>, también en la vista previa (US3-4); y anular el crédito nuevo tras el giro rechazado reversa la
/// recogida por el contrato y el recogido vuelve a <c>Active</c> con <c>PagoReversado</c> (US3-6, C11). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class DesembolsoYRecogidaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_pagare_sin_firma_impide_desembolsar_hasta_que_el_deposito_lo_deja_en_custodia()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (persona, _) = await DeudorAsync(http, esc.Coop.TokenAdmin, "Pagare", ultimoDigito: 3);
        var (solicitud, _) = await AprobadaAsync(http, esc, persona, 2_000_000m, esc.LineaConPagare);

        var creado = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.AsesorO1.Token, HttpMethod.Post, Pagares, new
        {
            ownerKind = "Application", ownerPublicId = solicitud, form = "Dematerialized",
        });
        creado.StatusCode.Should().Be(HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());
        var pagare = await CarteraE2E.LeerAsync(creado);
        pagare.GetProperty("channelCode").GetString().Should().Be("SIMULADO");
        pagare.GetProperty("simulated").GetBoolean().Should().BeTrue();
        pagare.GetProperty("perfected").GetBoolean().Should().BeFalse();
        var pagareId = pagare.GetProperty("publicId").GetGuid();

        var jefe = esc.Usuarios.Jefe.Token;
        var cuerpo = Desembolso(esc, solicitud, persona);
        var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"/api/lending/applications/{solicitud}/disbursement-preview", cuerpo);
        previa.GetProperty("collaterals").GetProperty("perfected").GetBoolean().Should().BeFalse();
        previa.GetProperty("blocking").EnumerateArray().Select(b => b.GetString()).Should().Contain("Lending.Disbursement.GuaranteesPending");

        var sinFirma = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Desembolsos, cuerpo);
        var pendiente = await InventarioE2E.FallaAsync(sinFirma, "Lending.Disbursement.GuaranteesPending");
        pendiente.GetProperty("data").GetProperty("pending").EnumerateArray().Should().Contain(p => p.GetProperty("kind").GetString() == "PromissoryNote");

        // Documento terminado en 3: el depósito simulado firma a la tercera consulta. «Actualizar estado» no lleva clave (es una consulta).
        string? estado = null;
        for (var i = 0; i < 5 && estado != "InCustody"; i++)
        {
            var refrescado = await CarteraE2E.EnviarAsync(http, esc.Usuarios.AsesorO1.Token, HttpMethod.Post, $"{Pagares}/{pagareId}/refresh", null);
            refrescado.StatusCode.Should().Be(HttpStatusCode.OK, await refrescado.Content.ReadAsStringAsync());
            estado = (await CarteraE2E.LeerAsync(refrescado)).GetProperty("status").GetString();
        }
        estado.Should().Be("InCustody", "firmado, el pagaré desmaterializado queda en custodia del depósito");

        var hecho = await DesembolsarAsync(http, esc, cuerpo);
        hecho.GetProperty("loanNumber").GetString().Should().StartWith("CR-");
        hecho.GetProperty("operationNumber").GetString().Should().StartWith("DS-");
        hecho.GetProperty("tranche").GetInt32().Should().Be(1);
        hecho.GetProperty("grossAmount").GetDecimal().Should().Be(2_000_000m);
        hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(2_000_000m, "sin recogidas el neto es el bruto");
        var orden = hecho.GetProperty("order");
        orden.GetProperty("number").GetString().Should().StartWith("OG-");
        orden.GetProperty("status").GetString().Should().Be("Issued");
        hecho.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("type").GetString())
            .Should().Contain(["CreditoDesembolsado", "OrdenDeGiroEmitida"]);

        var credito = hecho.GetProperty("loanPublicId").GetGuid();
        var delCredito = await CarteraE2E.GetAsync(http, esc.Usuarios.AsesorO1.Token, $"{Pagares}?loanPublicId={credito}");
        delCredito.GetProperty("items").EnumerateArray().Should().ContainSingle(p => p.GetProperty("publicId").GetGuid() == pagareId,
            "al desembolsar el pagaré pasa al crédito");
        var tramos = await CarteraE2E.GetAsync(http, jefe, $"{Desembolsos}?loanPublicId={credito}");
        tramos.GetProperty("items").EnumerateArray().Should().ContainSingle(t => t.GetProperty("tranche").GetInt32() == 1 && !t.GetProperty("reversed").GetBoolean());

        // La orden viaja a Tesorería y no a otro destino; el hecho del desembolso va a Contabilidad.
        var mensajes = await EnLaBaseAsync(fx, esc, db => db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == orden.GetProperty("publicId").GetGuid() || m.OriginPublicId == hecho.GetProperty("operationPublicId").GetGuid())
            .Select(m => m.Type).ToListAsync());
        mensajes.Should().Contain(["CreditoDesembolsado", "OrdenDeGiroEmitida"]);
        var destinoDeLaOrden = await EnLaBaseAsync(fx, esc, db => (from m in db.IntegrationMessages.AsNoTracking()
                                                                   join e in db.IntegrationMessageDeliveries.AsNoTracking() on m.Id equals e.MessageId
                                                                   where m.Type == "OrdenDeGiroEmitida" && m.OriginPublicId == orden.GetProperty("publicId").GetGuid()
                                                                   select e.Destination).ToListAsync());
        destinoDeLaOrden.Should().Equal(IntegrationDestinations.Treasury);
    }

    [Fact]
    public async Task Una_aprobacion_vencida_no_se_desembolsa()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (persona, _) = await DeudorAsync(http, esc.Coop.TokenAdmin, "Vencida");
        var (solicitud, decision) = await AprobadaAsync(http, esc, persona, 1_500_000m);
        var vence = DateOnly.Parse(decision.GetProperty("approvalExpiresAt").GetString()![..10], System.Globalization.CultureInfo.InvariantCulture);

        var conductor = new ConductorDelDespachador(fx);
        using (conductor.Adelantar(TimeSpan.FromDays(vence.DayNumber - esc.Hoy.DayNumber + 1)))
        {
            var tarde = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, Desembolsos, Desembolso(esc, solicitud, persona));
            var vencida = await InventarioE2E.FallaAsync(tarde, "Lending.Application.ApprovalExpired");
            vencida.GetProperty("data").GetProperty("expiredAt").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task La_usura_del_dia_del_desembolso_menor_que_la_de_la_aprobacion_lo_impide()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        var esteMes = new DateOnly(esc.Hoy.Year, esc.Hoy.Month, 1);
        var futuro = esteMes.AddMonths(2);

        // La usura de una modalidad que ningún otro ensayo del host usa: alta hasta la víspera de «futuro», más baja desde ese día.
        await LineasE2E.RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = Modalidad, validFrom = esteMes, validTo = futuro.AddDays(-1), value = 0.60m, unit = "EA",
            source = "Superintendencia Financiera, certificación de prueba del desembolso (e2e)",
        });
        await ValoresNacionalesE2E.ReplicarAsync(fx, esc.Coop.TenantPublicId);
        var version = LineasE2E.VersionFija(esc.Hoy, Modalidad, null, 0.40m, "Versión del desembolso bajo la usura (e2e)");
        version["amounts"] = new { min = 500_000m, max = 100_000_000m };
        var linea = await LineasE2E.CrearExitoAsync(http, admin, "POPRUR", "Popular productivo rural", version);
        var lineas = new List<Guid> { esc.Originacion.Origen, esc.LineaConPagare, linea };
        if (esc.Originacion.ConDocumento is { } d) lineas.Add(d);
        foreach (var u in new[] { esc.Usuarios.AsesorO1, esc.Usuarios.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, u, [.. lineas]);

        var (persona, _) = await DeudorAsync(http, admin, "Usura");
        var (solicitud, _) = await AprobadaAsync(http, esc, persona, 2_000_000m, linea);

        await LineasE2E.RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = Modalidad, validFrom = futuro, validTo = futuro.AddMonths(1).AddDays(-1), value = 0.30m, unit = "EA",
            source = "Superintendencia Financiera, certificación de prueba del desembolso, mes siguiente (e2e)",
        });
        await ValoresNacionalesE2E.ReplicarAsync(fx, esc.Coop.TenantPublicId);

        var conUsuraMenor = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, Desembolsos,
            Desembolso(esc, solicitud, persona, fechaValor: futuro));
        await InventarioE2E.FallaAsync(conUsuraMenor, "Lending.Usury.Exceeded");
    }

    /// <summary>La modalidad de la prueba de usura: ningún otro ensayo del host «Cartera e2e» la usa.</summary>
    private const string Modalidad = "POPULAR_PRODUCTIVO_RURAL";

    [Fact]
    public async Task Un_giro_rechazado_reversa_el_desembolso_desde_su_origen_y_el_credito_se_anula()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (_, _, hecho) = await CreditoDesembolsadoAsync(http, esc, "Rechazo", transferencia: true); // T0299: Tesorería real rechaza transferencias, no cheques
        var credito = hecho.GetProperty("loanPublicId").GetGuid();
        var operacion = hecho.GetProperty("operationPublicId").GetGuid();
        var orden = hecho.GetProperty("order").GetProperty("publicId").GetGuid();

        (await OrdenDeTesoreriaAsync(fx, esc, orden)).ConMensaje.Should().BeTrue("Tesorería real recibió la orden con su OrdenDeGiroEmitida (T0299)");

        var mensaje = await ResultadoDelGiroAsync(fx, http, esc, orden, "Rejected");
        var consumo = await ConsumirAsync(fx, esc, mensaje);
        consumo.Should().BeOfType<ResultadoDeConsumo.Processed>();

        var detalle = await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{Ordenes}/{orden}");
        detalle.GetProperty("status").GetString().Should().Be("Rejected");
        detalle.GetProperty("results").EnumerateArray().Should().ContainSingle(r => r.GetProperty("outcome").GetString() == "Rejected"
            && r.GetProperty("reversed").GetBoolean());

        var libro = await EnLaBaseAsync(fx, esc, async db =>
        {
            var loan = await db.Loans.AsNoTracking().SingleAsync(l => l.PublicId == credito);
            var original = await db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == operacion);
            var reverso = await db.LoanOperations.AsNoTracking().SingleAsync(o => o.LoanId == loan.Id && o.Kind == LoanOperationKind.DisbursementReversal);
            var espejos = await db.LoanTransactions.AsNoTracking().CountAsync(t => t.LoanId == loan.Id && t.ReversesTransactionId != null);
            var reversados = await db.IntegrationMessages.AsNoTracking().CountAsync(m => m.Type == "DesembolsoReversado" && m.RelatedPublicId == operacion);
            return (loan.Status, Original: original.Id, ReversoDe: reverso.ReversesOperationId, Espejos: espejos, Reversados: reversados);
        });
        libro.Status.Should().Be(LoanStatus.PendingDisbursement, "un giro rechazado deja el crédito pendiente de giro");
        libro.ReversoDe.Should().Be(libro.Original, "el reverso nace del desembolso, no de un ajuste");
        libro.Espejos.Should().BeGreaterThan(0, "cada fila del tramo tiene su espejo con ReversesTransactionId (C101)");
        libro.Reversados.Should().Be(1, "DesembolsoReversado nombra el desembolso como relacionado");

        var anulado = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, $"{Creditos}/{credito}/void",
            new { reason = "El deudor desistió tras el rechazo del banco" });
        anulado.StatusCode.Should().Be(HttpStatusCode.OK, await anulado.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(anulado)).GetProperty("status").GetString().Should().Be("Voided");
        (await EnLaBaseAsync(fx, esc, db => db.Loans.AsNoTracking().Where(l => l.PublicId == credito).Select(l => l.Status).SingleAsync()))
            .Should().Be(LoanStatus.Voided);

        var otraVez = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, $"{Creditos}/{credito}/void", new { reason = "Otra vez" });
        await InventarioE2E.FallaAsync(otraVez, "Lending.Loan.NotPendingDisbursement");
    }

    /// <summary>Radica con <c>asesor.o1</c> una solicitud que autoriza recoger <paramref name="recoger"/> y la aprueba con <c>analista</c>.</summary>
    private static async Task<Guid> AprobadaConRecogidaAsync(HttpClient http, Escenario esc, Guid persona, decimal monto, Guid recoger)
    {
        var asesor = esc.Usuarios.AsesorO1.Token;
        var radicada = await OriginacionE2E.RadicarAsync(http, asesor, new
        {
            applicantPersonPublicId = persona, officePublicId = esc.Usuarios.O1.AgenciaPublicId, creditLinePublicId = esc.Originacion.Origen,
            purpose = "Libre inversión con recogida (e2e)", amount = monto, installments = 36, periodicity = "Monthly", amortizationSystem = "LevelPayment",
            paymentForm = "Cashier", prepaymentPreference = "ReduceTerm",
            applicantCapacity = new { monthlyIncome = 40_000_000m, monthlyExpenses = 2_000_000m, externalInstallments = 0m },
            ownLoanPayoffs = new[] { new { loanPublicId = recoger, mode = "Full" } },
        });
        var id = radicada.GetProperty("publicId").GetGuid();
        var (_, huella) = await OriginacionE2E.EnrutamientoAsync(http, asesor, id);
        var aprobada = await OriginacionE2E.DecidirAsync(http, esc.Usuarios.Analista.Token, "applications", id, "Approve", huella);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        return id;
    }

    [Fact]
    public async Task La_recogida_de_un_credito_al_dia_paga_por_el_contrato_y_anular_el_nuevo_lo_devuelve_a_vivo()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var jefe = esc.Usuarios.Jefe.Token;
        var (persona, _) = await DeudorAsync(http, esc.Coop.TokenAdmin, "Recogida");
        var (anterior, _) = await AprobadaAsync(http, esc, persona, 1_000_000m);
        var recogido = (await DesembolsarAsync(http, esc, Desembolso(esc, anterior, persona))).GetProperty("loanPublicId").GetGuid();

        // US3-3: la recogida del crédito al día, el mismo día: lo que cuesta cancelarlo sale del bruto.
        var solicitud = await AprobadaConRecogidaAsync(http, esc, persona, 3_000_000m, recogido);
        var cuerpo = Desembolso(esc, solicitud, persona, transferencia: true); // T0299: el rechazo de Tesorería real es de una transferencia
        cuerpo["ownLoanPayoffs"] = new[] { new { loanPublicId = recogido, mode = "Full" } };
        var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"/api/lending/applications/{solicitud}/disbursement-preview", cuerpo);
        var enPrevia = previa.GetProperty("payoffs").EnumerateArray().Single();
        enPrevia.GetProperty("requiresRestructuring").GetBoolean().Should().BeFalse();
        var aCancelar = enPrevia.GetProperty("toPayOff").GetDecimal();
        aCancelar.Should().BePositive();

        var hecho = await DesembolsarAsync(http, esc, cuerpo);
        var recogida = hecho.GetProperty("payoffs").EnumerateArray().Single();
        recogida.GetProperty("loanPublicId").GetGuid().Should().Be(recogido);
        recogida.GetProperty("applied").GetDecimal().Should().Be(aCancelar);
        hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(3_000_000m - aCancelar, "el neto descuenta lo recogido");
        hecho.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("type").GetString())
            .Should().Contain(["CreditoDesembolsado", "OrdenDeGiroEmitida"]);
        (await PagosE2E.EstadoAsync(fx, esc, recogido)).Should().Be(LoanStatus.PaidOff);
        var pago = (await PagosE2E.OperacionesAsync(fx, esc, recogido)).Single(o => o.Clase == LoanOperationKind.Payment);
        pago.Clave.Should().StartWith("CAR:", "la recogida paga por el contrato como OwnLoanPayoff (T91)");
        pago.PublicId.Should().Be(recogida.GetProperty("receiptOperationPublicId").GetGuid());
        (await PagosE2E.MensajesAsync(fx, esc, pago.PublicId)).Should().Contain("PagoAplicado");

        // US3-6, C11: el giro rechazado deja el nuevo pendiente; anularlo reversa la recogida y el recogido vuelve a estar vivo.
        var nuevo = hecho.GetProperty("loanPublicId").GetGuid();
        var mensaje = await ResultadoDelGiroAsync(fx, http, esc, hecho.GetProperty("order").GetProperty("publicId").GetGuid(), "Rejected");
        (await ConsumirAsync(fx, esc, mensaje)).Should().BeOfType<ResultadoDeConsumo.Processed>();
        var anulado = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Creditos}/{nuevo}/void", new { reason = "El deudor desistió (e2e)" });
        anulado.StatusCode.Should().Be(HttpStatusCode.OK, await anulado.Content.ReadAsStringAsync());
        (await PagosE2E.EstadoAsync(fx, esc, nuevo)).Should().Be(LoanStatus.Voided);
        (await PagosE2E.EstadoAsync(fx, esc, recogido)).Should().Be(LoanStatus.Active, "anular el crédito reversa sus recogidas (C11)");
        var operaciones = await PagosE2E.OperacionesAsync(fx, esc, recogido);
        operaciones.Single(o => o.Clave == pago.Clave).ReversadaPor.Should().NotBeNull();
        var reverso = operaciones.Single(o => o.Clase == LoanOperationKind.PaymentReversal);
        reverso.Clave.Should().Be($"{pago.Clave}:reverso");
        (await PagosE2E.MensajesAsync(fx, esc, reverso.PublicId)).Should().Contain("PagoReversado");
    }

    [Fact]
    public async Task La_recogida_de_un_credito_en_mora_es_una_reestructuracion()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (persona, _) = await DeudorAsync(http, esc.Coop.TokenAdmin, "RecogidaEnMora");
        var (anterior, _) = await AprobadaAsync(http, esc, persona, 1_000_000m);
        var enMora = (await DesembolsarAsync(http, esc, Desembolso(esc, anterior, persona))).GetProperty("loanPublicId").GetGuid();
        // Tres días después de la fecha exigible (art. 829), no del vencimiento: la mora es 3 cualquier día de la semana.
        var exigible = await PagosE2E.ExigibleAsync(http, esc.Usuarios.Jefe.Token, enMora);

        using (CausacionE2E.RelojEn(fx, exigible.AddDays(3)))
        {
            var solicitud = await AprobadaConRecogidaAsync(http, esc, persona, 3_000_000m, enMora);
            var cuerpo = Desembolso(esc, solicitud, persona);
            cuerpo["ownLoanPayoffs"] = new[] { new { loanPublicId = enMora, mode = "Full" } };
            var previa = await CarteraE2E.ExitoAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/lending/applications/{solicitud}/disbursement-preview", cuerpo);
            var recogida = previa.GetProperty("payoffs").EnumerateArray().Single();
            recogida.GetProperty("requiresRestructuring").GetBoolean().Should().BeTrue();
            recogida.GetProperty("daysPastDue").GetInt32().Should().Be(3);

            var rechazado = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, Desembolsos, cuerpo);
            var error = await InventarioE2E.FallaAsync(rechazado, "Lending.Payoff.RequiresRestructuring");
            error.GetProperty("data").GetProperty("loans").EnumerateArray().Should().Contain(l => l.GetProperty("loanPublicId").GetGuid() == enMora);
            (await PagosE2E.EstadoAsync(fx, esc, enMora)).Should().Be(LoanStatus.Active);
        }
    }

    [Fact]
    public async Task Quien_decidio_no_desembolsa()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (persona, _) = await DeudorAsync(http, esc.Coop.TokenAdmin, "Segregada");
        var (solicitud, _) = await AprobadaAsync(http, esc, persona, 1_000_000m);
        var analista = esc.Usuarios.Analista.Token;
        var cuerpo = Desembolso(esc, solicitud, persona);

        var previa = await CarteraE2E.ExitoAsync(http, analista, HttpMethod.Post, $"/api/lending/applications/{solicitud}/disbursement-preview", cuerpo);
        previa.GetProperty("segregation").GetProperty("conflict").GetString().Should().Be("Lending.Segregation.Conflict");

        var conflicto = await CarteraE2E.ConClaveAsync(http, analista, HttpMethod.Post, Desembolsos, cuerpo);
        await InventarioE2E.FallaAsync(conflicto, "Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task El_archivo_de_desembolsos_se_genera_se_descarga_se_anula_y_con_resultados_ya_no()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var formato = await FormatoDeDesembolsosAsync(http, esc);
        var (_, _, hecho) = await CreditoDesembolsadoAsync(http, esc, "Archivo", transferencia: true, porArchivo: true);
        var orden = hecho.GetProperty("order").GetProperty("publicId").GetGuid();
        var jefe = esc.Usuarios.Jefe.Token;
        var pedido = new { formatPublicId = formato, sourceAccountPublicId = esc.CuentaDeOrigen, orderPublicIds = new[] { orden } };

        var cuentas = await CarteraE2E.GetAsync(http, jefe, $"{Archivos}/source-accounts?formatPublicId={formato}");
        cuentas.EnumerateArray().Should().Contain(c => c.GetProperty("accountPublicId").GetGuid() == esc.CuentaDeOrigen);

        var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"{Archivos}/preview", pedido);
        previa.GetProperty("lines").GetArrayLength().Should().Be(1);
        previa.GetProperty("lines")[0].GetString().Should().Contain(";" + CodigoDelBanco + ";").And.Contain(hecho.GetProperty("loanNumber").GetString()!);

        var generado = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Archivos, pedido);
        generado.StatusCode.Should().Be(HttpStatusCode.Created, await generado.Content.ReadAsStringAsync());
        var archivo = (await CarteraE2E.LeerAsync(generado)).GetProperty("file");
        var archivoId = archivo.GetProperty("publicId").GetGuid();
        archivo.GetProperty("sha256").GetString().Should().MatchRegex("^[0-9a-f]{64}$");
        archivo.GetProperty("orders").GetInt32().Should().Be(1);
        (await CarteraE2E.GetAsync(http, jefe, $"{Ordenes}/{orden}")).GetProperty("fileReference").GetString().Should().Be(archivo.GetProperty("number").GetString());

        var enlace = await CarteraE2E.EnviarAsync(http, jefe, HttpMethod.Post, $"{Archivos}/{archivoId}/download-link", null);
        enlace.StatusCode.Should().Be(HttpStatusCode.OK, "el enlace es una consulta por POST, sin clave");
        var url = (await CarteraE2E.LeerAsync(enlace)).GetProperty("url").GetString()!;
        using (var descarga = await http.GetAsync(Uri.TryCreate(url, UriKind.Absolute, out var absoluta) ? absoluta.PathAndQuery : url))
        {
            descarga.StatusCode.Should().Be(HttpStatusCode.OK);
            var bytes = await descarga.Content.ReadAsByteArrayAsync();
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant().Should().Be(archivo.GetProperty("sha256").GetString());
            Encoding.UTF8.GetString(bytes).Should().Contain(hecho.GetProperty("loanNumber").GetString()!);
        }

        // Anular antes de resultados libera la orden, que sigue pagándose por archivo y va a otro.
        var anular = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Archivos}/{archivoId}/void", new { reason = "Cuenta de origen errada" });
        anular.StatusCode.Should().Be(HttpStatusCode.NoContent, await anular.Content.ReadAsStringAsync());
        var otro = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Archivos, pedido);
        otro.StatusCode.Should().Be(HttpStatusCode.Created, await otro.Content.ReadAsStringAsync());
        var otroId = (await CarteraE2E.LeerAsync(otro)).GetProperty("file").GetProperty("publicId").GetGuid();

        // Con el resultado de Tesorería ya no se anula.
        var mensaje = await ResultadoDelGiroAsync(fx, http, esc, orden, "Disbursed");
        (await ConsumirAsync(fx, esc, mensaje)).Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await CarteraE2E.GetAsync(http, jefe, $"{Ordenes}/{orden}")).GetProperty("status").GetString().Should().Be("Disbursed");
        var tarde = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Archivos}/{otroId}/void", new { reason = "Ya no" });
        await InventarioE2E.FallaAsync(tarde, "Lending.DisbursementFile.HasResults");

        var lista = await CarteraE2E.GetAsync(http, jefe, Archivos);
        lista.GetProperty("items").EnumerateArray().Should().Contain(a => a.GetProperty("publicId").GetGuid() == archivoId && a.GetProperty("status").GetString() == "Voided");
        lista.GetProperty("items").EnumerateArray().Should().Contain(a => a.GetProperty("publicId").GetGuid() == otroId && a.GetProperty("hasResults").GetBoolean());
    }
}
