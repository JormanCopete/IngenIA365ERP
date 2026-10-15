using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0869 (feature 013, K3, US11; quickstart §5, pasos 1–7; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): las novedades, las
/// condonaciones y los eventos del deudor por sus rutas (<c>/api/lending/novelties</c>, <c>/waivers</c>, <c>/debtor-events</c>,
/// <c>/insurance-claims</c>, <c>/insolvency-proceedings</c>), sobre una cooperativa aislada activada con la contabilidad iniciada y la matriz contable
/// completa por la plantilla. Cinco créditos de 36 cuotas desembolsados hoy y el reloj llevado a 15 días después de la fecha <b>exigible</b> de la
/// primera cuota (art. 829, <see cref="PagosE2E.ExigibleAsync"/>), con el ciclo diario corrido: todos en mora.
/// <list type="bullet">
/// <item>una prórroga pedida «a petición del deudor» sobre un crédito en mora se clasifica <b>reestructuración</b> (US11-1); se simula (plan nuevo,
/// calificación, conteo) y se radica al nivel ANALISTA; el codeudor sin aceptación firmada no la deja aplicar (<c>ConsentMissing</c> con
/// <c>parties[]</c>); la primera salida, <c>DoNotApply</c>, la anula sin tocar el crédito; en una segunda, <c>ApplyWithGuaranteeExtinguished</c> la
/// aplica: el crédito conserva su número, el plan anterior queda como versión, queda marcado reestructurado y su <c>CreditoReestructurado</c> se
/// contabiliza en un <c>CN</c> (US11-2, US11-3); la vista <c>restructured</c> lo lista;</item>
/// <item>una novación deja el anterior <c>Novated</c> y un crédito enlazado con otro número, marcado reestructurado (US11-3);</item>
/// <item>una condonación de interés de mora que la matriz lleva por regla a GERENCIA (el nivel siguiente al del analista): más que el saldo del
/// concepto responde <c>ExceedsBalance</c> con el saldo; quien la pidió no la decide (<c>Segregation.Conflict</c>), la decisión exige motivo, y
/// aplicada emite <c>CondonacionAprobada</c> (US11-5);</item>
/// <item>la insolvencia abre en <c>Negotiating</c> con el bloqueo <c>Insolvency</c>, no se abre dos veces (<c>AlreadyOpen</c>) y su fracaso la
/// deja <c>Failed</c> (después, <c>NotNegotiating</c>);</item>
/// <item>el fallecimiento abre la reclamación <c>InProcess</c> con la aseguradora del seguro de vida deudores de la línea, no se registra dos
/// veces (<c>Duplicate</c>) y el pago de la aseguradora por el recaudador de prueba, con ella como pagadora, la cierra <c>Paid</c> con la
/// operación (T102).</item>
/// </list>
/// Lo que no recorre (anotado en tasks.md, T0869): la mejora de una categoría por tramo de cuotas consecutivas y la recaída en los cierres siguientes
/// (US11-4) —pedirían un cierre mensual por cada cuota pagada—: la fijan los casos dorados <c>r05</c>, <c>r-k3-*</c> y <c>MotorDeCalificacion</c>; el
/// acuerdo de insolvencia como novedad lo fija <c>AplicacionDeNovedadTests</c>. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class NovedadesYCondonacionesTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "novedades";
    private const string Novedades = "/api/lending/novelties";
    private const string Condonaciones = "/api/lending/waivers";
    private const string Eventos = "/api/lending/debtor-events";
    private const string Reclamaciones = "/api/lending/insurance-claims";
    private const string Insolvencias = "/api/lending/insolvency-proceedings";

    [Fact]
    public async Task Novedades_condonaciones_insolvencia_y_fallecimiento_por_sus_rutas()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var tenant = esc.Coop.TenantPublicId;
        var u = esc.Usuarios;
        var hoy = esc.Hoy;
        var despachador = new ConductorDelDespachador(fx);
        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false);

        // ---------------------------------------------------------- la línea con seguro de vida deudores y su aseguradora --
        var aseguradora = await CarteraE2E.PersonaAsync(http, admin, "Aseguradora", "Vida");
        var version = OriginacionE2E.VersionDeOriginacion(hoy, conDocumento: false);
        version["insurances"] = new[]
        {
            new
            {
                code = "VIDA", name = "Vida deudores", insurerPersonPublicId = aseguradora, @base = "OutstandingBalance", rate = 0.0005m,
                periodicity = "Monthly", mandatory = true, retainedFraction = 0m, isDebtorLifeInsurance = true,
            },
        };
        var lineaVida = await LineasE2E.CrearExitoAsync(http, admin, "VIDA", "Consumo con seguro de vida deudores", version);
        var lineas = new List<Guid> { esc.Originacion.Origen, esc.LineaConPagare, lineaVida };
        if (esc.Originacion.ConDocumento is { } d) lineas.Add(d);
        foreach (var usuario in new[] { u.AsesorO1, u.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, usuario, [.. lineas]);

        // ------------------------------------------------------------------------------------- los cinco créditos --
        var (codeudor, _) = await DeudorAsync(http, admin, "Beto");
        var (ana, _) = await DeudorAsync(http, admin, "Ana");
        var creditoDeAna = await ConCodeudorAsync(http, esc, ana, codeudor, 2_000_000m);
        var (carla, _) = await DeudorAsync(http, admin, "Carla");
        var creditoDeCarla = await CierreE2E.DesembolsadoAsync(http, esc, carla, 1_500_000m);
        var (diego, _) = await DeudorAsync(http, admin, "Diego");
        var creditoDeDiego = await CierreE2E.DesembolsadoAsync(http, esc, diego, 1_000_000m);
        var (fabio, _) = await DeudorAsync(http, admin, "Fabio");
        await CierreE2E.DesembolsadoAsync(http, esc, fabio, 1_000_000m);
        var (marta, _) = await DeudorAsync(http, admin, "Marta");
        var creditoDeMarta = await CierreE2E.DesembolsadoAsync(http, esc, marta, 1_000_000m, lineaVida);
        var numeroDeAna = (await CarteraE2E.GetAsync(http, admin, $"{Creditos}/{creditoDeAna}")).GetProperty("number").GetString();

        // Quien decide la condonación en GERENCIA (el gerente la pide: por la segregación no la decide). La plantilla cartera.analista no trae
        // Lending.Novelties.Read, que la ruta de decisiones de novedades exige (api.md §11.1): el ensayo se lo da aparte (decisión del dueño).
        var gerencia = await OriginacionE2E.RolPropioAsync(http, admin, "ENS-NOV-GERENCIA", "Lending.Decisions.Management");
        await OriginacionE2E.DarRolAsync(http, admin, u.Jefe, gerencia);
        var verNovedades = await OriginacionE2E.RolPropioAsync(http, admin, "ENS-NOV-ANALISTA", "Lending.Novelties.Read");
        await OriginacionE2E.DarRolAsync(http, admin, u.Analista, verNovedades);

        var enMora = (await ExigibleAsync(http, admin, creditoDeAna)).AddDays(15);
        using (RelojEn(fx, enMora))
        {
            await CicloDeHoyAsync(fx, http, u.Jefe.Token, tenant);
            await despachador.PasadasAsync(tenant, 3);

            // ======================================================== (1) prórroga en mora: reestructuración (US11-1) ==
            var prorroga = await PedirAsync(http, u.Jefe.Token, creditoDeAna, "Extension", "DebtorRequest", new { installments = 36 });
            prorroga.GetProperty("cause").GetInt32().Should().Be((int)NoveltyCause.Restructuring,
                "un crédito en mora se reestructura aunque la pida el deudor sin alegar dificultades");
            var idProrroga = prorroga.GetProperty("publicId").GetGuid();
            var consentimientos = prorroga.GetProperty("consents").EnumerateArray().ToList();
            consentimientos.Should().ContainSingle(c => c.GetProperty("party").GetProperty("personPublicId").GetGuid() == codeudor
                && c.GetProperty("role").GetInt32() == (int)ConsentPartyRole.CoDebtor && c.GetProperty("status").GetInt32() == (int)ConsentStatus.Pending,
                "el codeudor tiene que consentir la novedad");

            var simulacion = await CarteraE2E.EnviarAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Novedades}/{idProrroga}/simulate", null);
            simulacion.StatusCode.Should().Be(HttpStatusCode.OK, await simulacion.Content.ReadAsStringAsync());
            var simulada = await CarteraE2E.LeerAsync(simulacion);
            simulada.GetProperty("newSchedule").GetArrayLength().Should().Be(36);
            simulada.GetProperty("restructuringCount").GetInt32().Should().Be(1);
            simulada.GetProperty("ratingAfter").GetString().Should().NotBeNullOrEmpty();

            // Aceptar sin el documento firmado no se admite; el codeudor rehúsa.
            var sinDocumento = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Novedades}/{idProrroga}/consents",
                new { partyPersonPublicId = codeudor, consented = true });
            sinDocumento.IsSuccessStatusCode.Should().BeFalse(await sinDocumento.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(sinDocumento)).Should().Be("Lending.Novelty.ConsentAttachmentRequired");
            var ajena = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Novedades}/{idProrroga}/consents",
                new { partyPersonPublicId = carla, consented = false });
            (await CarteraE2E.CodigoDeErrorAsync(ajena)).Should().Be("Lending.Novelty.PartyUnknown");
            var rehusa = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Novedades}/{idProrroga}/consents",
                new { partyPersonPublicId = codeudor, consented = false });
            rehusa.StatusCode.Should().Be(HttpStatusCode.Created, await rehusa.Content.ReadAsStringAsync());

            var radicada = await RadicarYAprobarAsync(http, u, idProrroga);
            radicada.GetProperty("routing").GetProperty("levelCode").GetString().Should().Be("ANALISTA");

            // Sin decidir sobre el codeudor no se aplica; la primera salida: no aplicarla.
            var sinSalida = await AplicarAsync(http, u.Administrador.Token, idProrroga, null);
            sinSalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await sinSalida.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(sinSalida)).Should().Be("Lending.Novelty.ConsentMissing");
            (await CarteraE2E.LeerAsync(sinSalida)).GetProperty("data").GetProperty("parties").GetArrayLength().Should().Be(1);
            var noAplicar = await AplicarAsync(http, u.Administrador.Token, idProrroga,
                new[] { new { partyPersonPublicId = codeudor, decision = "DoNotApply", reason = "El codeudor no firma el otrosí (e2e)" } });
            noAplicar.IsSuccessStatusCode.Should().BeTrue(await noAplicar.Content.ReadAsStringAsync());
            (await CarteraE2E.GetAsync(http, u.Jefe.Token, $"{Novedades}/{idProrroga}")).GetProperty("status").GetInt32()
                .Should().Be((int)LoanNoveltyStatus.Cancelled, "con DoNotApply la novedad no se aplica");
            (await CarteraE2E.GetAsync(http, admin, $"{Creditos}/{creditoDeAna}")).GetProperty("marks").GetProperty("restructured").GetBoolean()
                .Should().BeFalse("el crédito no se tocó");

            // La segunda salida: aplicarla con el codeudor liberado. Quien la pidió no la aplica.
            // Con capitalización del interés que se debe: así el hecho mueve saldos (sin ella, una prórroga no cambia ningún rol y no hay comprobante).
            var segunda = await PedirAsync(http, u.Jefe.Token, creditoDeAna, "Extension", "Hardship", new { installments = 36, capitalizeInterest = true });
            var idSegunda = segunda.GetProperty("publicId").GetGuid();
            await RadicarYAprobarAsync(http, u, idSegunda);
            var salida = new[] { new { partyPersonPublicId = codeudor, decision = "ApplyWithGuaranteeExtinguished", reason = "Se libera al codeudor (e2e)" } };
            var propia = await AplicarAsync(http, u.Jefe.Token, idSegunda, salida);
            propia.IsSuccessStatusCode.Should().BeFalse("quien la radica no la aplica");
            var aplicada = await AplicarAsync(http, u.Administrador.Token, idSegunda, salida);
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
            var resultado = await CarteraE2E.LeerAsync(aplicada);
            resultado.GetProperty("operationPublicId").GetGuid().Should().NotBeEmpty();
            resultado.GetProperty("messages").GetArrayLength().Should().BePositive("emite el hecho de la novedad");
            var deAna = await CarteraE2E.GetAsync(http, admin, $"{Creditos}/{creditoDeAna}");
            deAna.GetProperty("number").GetString().Should().Be(numeroDeAna, "prórroga, fecha o tasa conservan el número");
            deAna.GetProperty("marks").GetProperty("restructured").GetBoolean().Should().BeTrue();
            deAna.GetProperty("marks").GetProperty("restructuringCount").GetInt32().Should().Be(1);
            (await CarteraE2E.GetAsync(http, admin, $"{Creditos}/{creditoDeAna}/schedule/versions")).GetArrayLength()
                .Should().BeGreaterThanOrEqualTo(2, "el plan anterior queda como versión");
            var otraVez = await AplicarAsync(http, u.Administrador.Token, idSegunda, salida);
            (await CarteraE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Lending.Novelty.AlreadyApplied");

            // CreditoReestructurado llega a Contabilidad en un CN.
            var hecho = await HechoProcesadoAsync(http, admin, despachador, tenant, creditoDeAna, "CreditoReestructurado");
            (await TipoDeComprobanteAsync(http, admin, hecho)).Should().Be("CN");

            // La vista de reestructurados lo lista.
            var vista = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, u.Jefe.Token,
                $"/api/reports/lending/restructured?asOf={enMora:yyyy-MM-dd}"));
            vista.Filas.Should().Contain(f => ContabilidadE2E.Tabla.Celda(f, vista.IndicePorClave("_credito")).GetString() == creditoDeAna.ToString());

            // =============================================================== (2) novación: crédito enlazado (US11-3) ==
            var novacion = await PedirAsync(http, u.Jefe.Token, creditoDeCarla, "Novation", "Hardship", new { installments = 30 });
            var idNovacion = novacion.GetProperty("publicId").GetGuid();
            await RadicarYAprobarAsync(http, u, idNovacion);
            var novada = await AplicarAsync(http, u.Administrador.Token, idNovacion, null);
            novada.StatusCode.Should().Be(HttpStatusCode.OK, await novada.Content.ReadAsStringAsync());
            var resultadoDeNovacion = await CarteraE2E.LeerAsync(novada);
            var enlazado = resultadoDeNovacion.GetProperty("newLoanPublicId").GetGuid();
            (await HechosAsync(http, admin, creditoDeCarla, "CreditoNovado")).Concat(await HechosAsync(http, admin, enlazado, "CreditoNovado"))
                .Should().NotBeEmpty("la novación emite CreditoNovado");
            enlazado.Should().NotBe(creditoDeCarla);
            (await EstadoAsync(fx, esc, creditoDeCarla)).Should().Be(LoanStatus.Novated);
            var nuevo = await CarteraE2E.GetAsync(http, admin, $"{Creditos}/{enlazado}");
            nuevo.GetProperty("marks").GetProperty("restructured").GetBoolean().Should().BeTrue("el enlazado hereda la marca");
            nuevo.GetProperty("number").GetString().Should().NotBe((await CarteraE2E.GetAsync(http, admin, $"{Creditos}/{creditoDeCarla}")).GetProperty("number").GetString());

            // ====================================================== (3) condonación al nivel siguiente (US11-5) ==
            await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/attribution-matrices", MatrizConCondonacionEnGerencia(esc, enMora));
            var demasiado = await CarteraE2E.ConClaveAsync(http, u.Gerente.Token, HttpMethod.Post, Condonaciones, new
            {
                loanPublicId = creditoDeDiego, concepts = new[] { new { concept = "DefaultInterest", amount = 900_000m } }, reason = "Condonación de mora (e2e)",
            });
            demasiado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await demasiado.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(demasiado)).Should().Be("Lending.Waiver.ExceedsBalance");
            var saldo = (await CarteraE2E.LeerAsync(demasiado)).GetProperty("data").GetProperty("balance").GetDecimal();
            saldo.Should().BePositive("quince días de mora causan interés de mora");
            var pedida = await CarteraE2E.ConClaveAsync(http, u.Gerente.Token, HttpMethod.Post, Condonaciones, new
            {
                loanPublicId = creditoDeDiego, concepts = new[] { new { concept = "DefaultInterest", amount = saldo } }, reason = "Condonación de mora (e2e)",
            });
            pedida.StatusCode.Should().Be(HttpStatusCode.Created, await pedida.Content.ReadAsStringAsync());
            var condonacion = await CarteraE2E.LeerAsync(pedida);
            var idCondonacion = condonacion.GetProperty("publicId").GetGuid();
            condonacion.GetProperty("daysPastDueAtRequest").GetInt32().Should().Be(15);
            var ruta = condonacion.GetProperty("routing");
            ruta.GetProperty("levelCode").GetString().Should().Be("GERENCIA", "la regla de la matriz lleva la mora condonada sobre el analista");
            var huella = ruta.GetProperty("contentSha256").GetString();

            var delQuePide = await OriginacionE2E.DecidirAsync(http, u.Gerente.Token, "waivers", idCondonacion, "Approve", huella, "Aprobada (e2e)");
            (await CarteraE2E.CodigoDeErrorAsync(delQuePide)).Should().Be("Lending.Segregation.Conflict");
            var delAnalista = await OriginacionE2E.DecidirAsync(http, u.Analista.Token, "waivers", idCondonacion, "Approve", huella, "Aprobada (e2e)");
            delAnalista.IsSuccessStatusCode.Should().BeFalse("el analista no tiene la atribución de GERENCIA");
            var sinMotivo = await OriginacionE2E.DecidirAsync(http, u.Jefe.Token, "waivers", idCondonacion, "Approve", huella);
            sinMotivo.IsSuccessStatusCode.Should().BeFalse("toda condonación se decide con motivo");
            var aprobadaW = await OriginacionE2E.DecidirAsync(http, u.Jefe.Token, "waivers", idCondonacion, "Approve", huella, "Deudor con calamidad (e2e)");
            aprobadaW.StatusCode.Should().Be(HttpStatusCode.Created, await aprobadaW.Content.ReadAsStringAsync());
            var aplicarW = await CarteraE2E.ConClaveAsync(http, u.Administrador.Token, HttpMethod.Post, $"{Condonaciones}/{idCondonacion}/apply", new { });
            aplicarW.StatusCode.Should().Be(HttpStatusCode.OK, await aplicarW.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(aplicarW)).GetProperty("messages").GetArrayLength().Should().BePositive();
            (await HechosAsync(http, admin, creditoDeDiego, "CondonacionAprobada")).Should().ContainSingle("aplicada, emite CondonacionAprobada");
            (await CarteraE2E.GetAsync(http, admin, $"{Condonaciones}/{idCondonacion}")).GetProperty("status").GetInt32().Should().Be((int)WaiverStatus.Applied);

            // =================================================================================== (4) insolvencia ==
            var soporteDeFabio = await SoporteAsync(http, admin, fabio);
            var abierta = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Insolvencias, new
            {
                personPublicId = fabio, startDate = enMora, regime = "NaturalPersonDebtNegotiation", authority = "Centro de conciliación (e2e)",
                fileNumber = "NEG-001", supportAttachmentPublicId = soporteDeFabio,
            });
            abierta.StatusCode.Should().Be(HttpStatusCode.Created, await abierta.Content.ReadAsStringAsync());
            var proceso = await CarteraE2E.LeerAsync(abierta);
            proceso.GetProperty("status").GetInt32().Should().Be((int)InsolvencyProceedingStatus.Negotiating);
            var idProceso = proceso.GetProperty("publicId").GetGuid();
            (await CarteraE2E.GetAsync(http, admin, $"/api/lending/blocks?personPublicId={fabio}&kind=Insolvency&active=true"))
                .GetProperty("items").GetArrayLength().Should().Be(1, "la insolvencia bloquea crédito nuevo");
            var otraInsolvencia = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Insolvencias, new
            {
                personPublicId = fabio, startDate = enMora, regime = "NaturalPersonDebtNegotiation", authority = "Otro (e2e)", fileNumber = "NEG-002",
                supportAttachmentPublicId = soporteDeFabio,
            });
            (await CarteraE2E.CodigoDeErrorAsync(otraInsolvencia)).Should().Be("Lending.Insolvency.AlreadyOpen");
            var fracaso = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Insolvencias}/{idProceso}/failure", new
            {
                date = enMora, reason = "No hubo acuerdo (e2e)", supportAttachmentPublicId = soporteDeFabio,
            });
            fracaso.StatusCode.Should().Be(HttpStatusCode.OK, await fracaso.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(fracaso)).GetProperty("status").GetInt32().Should().Be((int)InsolvencyProceedingStatus.Failed);
            var otroFracaso = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Insolvencias}/{idProceso}/failure", new
            {
                date = enMora, reason = "Otra vez (e2e)", supportAttachmentPublicId = soporteDeFabio,
            });
            (await CarteraE2E.CodigoDeErrorAsync(otroFracaso)).Should().Be("Lending.Insolvency.NotNegotiating");
            (await CarteraE2E.GetAsync(http, admin, $"{Insolvencias}?personPublicId={fabio}")).GetProperty("items").GetArrayLength().Should().Be(1);

            // =========================================================================== (5) fallecimiento (T102) ==
            var soporteDeMarta = await SoporteAsync(http, admin, marta);
            var fallecimiento = new { personPublicId = marta, kind = "Deceased", eventDate = enMora, supportAttachmentPublicId = soporteDeMarta };
            var evento = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Eventos, fallecimiento);
            evento.StatusCode.Should().Be(HttpStatusCode.Created, await evento.Content.ReadAsStringAsync());
            var repetido = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Eventos, fallecimiento);
            (await CarteraE2E.CodigoDeErrorAsync(repetido)).Should().Be("Lending.DebtorEvent.Duplicate");
            (await CarteraE2E.GetAsync(http, admin, $"{Eventos}?personPublicId={marta}")).GetProperty("items").GetArrayLength().Should().Be(1);

            var reclamacion = (await CarteraE2E.GetAsync(http, admin, $"{Reclamaciones}?loanPublicId={creditoDeMarta}")).GetProperty("items")
                .EnumerateArray().Should().ContainSingle("el fallecimiento abre la reclamación del seguro de vida deudores").Subject;
            reclamacion.GetProperty("status").GetInt32().Should().Be((int)InsuranceClaimStatus.InProcess);
            reclamacion.GetProperty("insurer").GetProperty("personPublicId").GetGuid().Should().Be(aseguradora, "la aseguradora de la línea");

            // La aseguradora paga por el recaudador de prueba, como pagadora: la reclamación se cierra pagada con la operación.
            var pago = await AplicadoAsync(http, u, Pago(creditoDeMarta, aseguradora, "FreeAmount", 20_000m, enMora));
            pago.GetProperty("operationPublicId").GetGuid().Should().NotBeEmpty();
            var pagada = (await CarteraE2E.GetAsync(http, admin, $"{Reclamaciones}?loanPublicId={creditoDeMarta}")).GetProperty("items")[0];
            pagada.GetProperty("status").GetInt32().Should().Be((int)InsuranceClaimStatus.Paid, pagada.ToString());
            pagada.GetProperty("paymentOperationPublicId").GetGuid().Should().Be(pago.GetProperty("operationPublicId").GetGuid());
            var cambiar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Reclamaciones}/{pagada.GetProperty("publicId").GetGuid()}/status",
                new { status = "Denied", reason = "Ya pagada (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(cambiar)).Should().Be("Lending.InsuranceClaim.NotInProcess");
        }
    }

    // ---------------------------------------------------------------------------------------------- ayudantes --

    /// <summary>Una solicitud de ORIGEN con <paramref name="codeudor"/>, aprobada por el analista y desembolsada por cheque; devuelve el crédito.</summary>
    private static async Task<Guid> ConCodeudorAsync(HttpClient http, Escenario esc, Guid persona, Guid codeudor, decimal monto)
    {
        var cuerpo = JsonSerializer.SerializeToNode(OriginacionE2E.Solicitud(esc.Originacion, persona, monto))!.AsObject();
        cuerpo["coDebtors"] = JsonSerializer.SerializeToNode(new[]
        {
            new { personPublicId = codeudor, monthlyIncome = 10_000_000m, monthlyExpenses = 1_000_000m, externalInstallments = 0m },
        });
        var asesor = esc.Usuarios.AsesorO1.Token;
        var radicada = await OriginacionE2E.RadicarAsync(http, asesor, cuerpo);
        var id = radicada.GetProperty("publicId").GetGuid();
        var (nivel, huella) = await OriginacionE2E.EnrutamientoAsync(http, asesor, id);
        nivel.Should().Be("ANALISTA");
        var aprobada = await OriginacionE2E.DecidirAsync(http, esc.Usuarios.Analista.Token, "applications", id, "Approve", huella);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        var hecho = await DesembolsarAsync(http, esc, Desembolso(esc, id, persona));
        return hecho.GetProperty("loanPublicId").GetGuid();
    }

    /// <summary>Pide una novedad con <paramref name="token"/> y exige el 201; devuelve la novedad en borrador.</summary>
    private static async Task<JsonElement> PedirAsync(HttpClient http, string token, Guid credito, string mecanismo, string motivo, object condiciones)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Novedades, new
        {
            loanPublicIds = new[] { credito }, mechanism = mecanismo, requestMotive = motivo, newTerms = condiciones, reason = $"{mecanismo} pedida (e2e)",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"pedir la novedad: «{await resp.Content.ReadAsStringAsync()}»");
        var novedad = await CarteraE2E.LeerAsync(resp);
        novedad.GetProperty("status").GetInt32().Should().Be((int)LoanNoveltyStatus.Draft);
        return novedad;
    }

    /// <summary>Radica con <c>jefe</c> (quien la pidió) y aprueba con <c>analista</c> con la huella del enrutamiento; devuelve la radicada.</summary>
    private static async Task<JsonElement> RadicarYAprobarAsync(HttpClient http, UsuariosDeCartera u, Guid novedad)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Novedades}/{novedad}/submit", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"radicar la novedad: «{await resp.Content.ReadAsStringAsync()}»");
        var radicada = await CarteraE2E.LeerAsync(resp);
        radicada.GetProperty("status").GetInt32().Should().Be((int)LoanNoveltyStatus.Submitted);
        var huella = radicada.GetProperty("routing").GetProperty("contentSha256").GetString();
        var propia = await OriginacionE2E.DecidirAsync(http, u.Jefe.Token, "novelties", novedad, "Approve", huella, "La decido yo (e2e)");
        propia.IsSuccessStatusCode.Should().BeFalse("quien radica no decide");
        var decision = await OriginacionE2E.DecidirAsync(http, u.Analista.Token, "novelties", novedad, "Approve", huella);
        decision.StatusCode.Should().Be(HttpStatusCode.Created, await decision.Content.ReadAsStringAsync());
        (await CarteraE2E.GetAsync(http, u.Jefe.Token, $"{Novedades}/{novedad}")).GetProperty("status").GetInt32().Should().Be((int)LoanNoveltyStatus.Approved);
        return radicada;
    }

    private static Task<HttpResponseMessage> AplicarAsync(HttpClient http, string token, Guid novedad, object? sinConsentimiento) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Novedades}/{novedad}/apply", new { withoutConsent = sinConsentimiento });

    /// <summary>La matriz del ensayo con una regla más: condonar interés de mora con mora va, como mínimo, a GERENCIA.</summary>
    private static object MatrizConCondonacionEnGerencia(Escenario esc, DateOnly desde) => new
    {
        validFrom = desde, reason = "Reglamento con condonaciones (e2e)", source = "Acuerdo 13 del consejo (e2e)",
        levels = new object[]
        {
            new { code = "ANALISTA", name = "Analista", order = 10, kind = "Individual", permissionCode = "Lending.Decisions.Analyst", consolidatedAmountCeiling = 5_000_000m },
            new { code = "EXCEP", name = "Excepción", order = 15, kind = "Individual", permissionCode = "Lending.Decisions.Exceptions", consolidatedAmountCeiling = 5_000_000m, isExceptionLevel = true },
            new { code = "GERENCIA", name = "Gerencia", order = 20, kind = "Individual", permissionCode = "Lending.Decisions.Management", consolidatedAmountCeiling = 20_000_000m },
            new { code = "COMITE", name = "Comité de crédito", order = 30, kind = "Collegiate", governingBodyPublicId = esc.Originacion.Comite, consolidatedAmountCeiling = 100_000_000m },
            new { code = "CONSEJO", name = "Consejo de administración", order = 40, kind = "Collegiate", governingBodyPublicId = esc.Originacion.Consejo, isBoardLevel = true },
        },
        rules = new object[]
        {
            new { order = 1, subject = "Condonation", minimumLevelCode = "GERENCIA", conditions = new { concept = "DefaultInterest", minDaysPastDue = 1 } },
        },
    };

    /// <summary>Sube el soporte de un evento del deudor (dueño <c>LendingDebtorEventSupport</c>, la persona) como el navegador.</summary>
    private static async Task<Guid> SoporteAsync(HttpClient http, string token, Guid persona)
    {
        var contenido = AdjuntosE2E.Pdf(4_096);
        using var pedido = await AdjuntosE2E.PedirSubidaAsync(http, token, persona, contenido, "soporte-del-evento.pdf", ownerEntityType: "LendingDebtorEventSupport");
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await CarteraE2E.LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");
        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, "soporte-del-evento.pdf");
        var url = subida.GetProperty("url").GetString()!;
        using var almacen = await http.PostAsync(Uri.TryCreate(url, UriKind.Absolute, out var destino) ? destino.PathAndQuery : url, formulario);
        almacen.IsSuccessStatusCode.Should().BeTrue($"el almacén recibe el soporte: {(int)almacen.StatusCode}");
        using var confirmacion = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return adjunto;
    }

    /// <summary>Los hechos contables de ese tipo del crédito (<c>GET /api/lending/accounting/facts</c>).</summary>
    private static async Task<List<JsonElement>> HechosAsync(HttpClient http, string token, Guid credito, string tipo) =>
        (await CarteraE2E.GetAsync(http, token, $"/api/lending/accounting/facts?type={tipo}&loanPublicId={credito}&pageSize=50"))
            .GetProperty("items").EnumerateArray().ToList();

    /// <summary>Pasadas del despachador hasta que el último hecho de ese tipo del crédito quede procesado; devuelve el hecho.</summary>
    private static async Task<JsonElement> HechoProcesadoAsync(HttpClient http, string token, ConductorDelDespachador despachador, Guid tenant, Guid credito,
        string tipo)
    {
        JsonElement hecho = default;
        for (var i = 0; i < 10; i++)
        {
            var lista = (await CarteraE2E.GetAsync(http, token, $"/api/lending/accounting/facts?type={tipo}&loanPublicId={credito}&pageSize=50"))
                .GetProperty("items").EnumerateArray().ToList();
            lista.Should().NotBeEmpty($"el crédito emitió un «{tipo}»");
            hecho = lista[0];
            if (hecho.GetProperty("status").GetString() is not ("Pending" or "InBatch")) break;
            await despachador.PasadasAsync(tenant, 3);
        }
        if (hecho.GetProperty("status").GetString() != "Processed")
        {
            var detalle = await CarteraE2E.GetAsync(http, token, $"/api/lending/messages/{hecho.GetProperty("messagePublicId").GetGuid()}");
            throw new Xunit.Sdk.XunitException($"El hecho {tipo} no se contabilizó: {hecho} · {detalle}");
        }
        return hecho;
    }

    /// <summary>El tipo de comprobante del recibo contable del hecho.</summary>
    private async Task<string> TipoDeComprobanteAsync(HttpClient http, string token, JsonElement hecho)
    {
        var mensaje = hecho.GetProperty("messagePublicId").GetGuid();
        var recibos = (await CarteraE2E.GetAsync(http, token, $"/api/accounting/lending/postings?message={mensaje}"))
            .GetProperty("items").EnumerateArray().Where(r => r.GetProperty("messagePublicId").GetGuid() == mensaje).ToList();
        recibos.Should().ContainSingle($"el hecho {mensaje} tiene un recibo");
        recibos[0].GetProperty("accountingDocumentPublicId").ValueKind.Should().Be(JsonValueKind.String, $"el recibo tiene comprobante: {recibos[0]} · {hecho}");
        var documento = recibos[0].GetProperty("accountingDocumentPublicId").GetGuid();
        var esc = await EscenarioAsync(fx, Cooperativa);
        return await EnLaBaseAsync(fx, esc, db => db.AccountingDocuments.AsNoTracking().Where(x => x.PublicId == documento)
            .Select(x => x.VoucherType!.Code).SingleAsync());
    }
}
