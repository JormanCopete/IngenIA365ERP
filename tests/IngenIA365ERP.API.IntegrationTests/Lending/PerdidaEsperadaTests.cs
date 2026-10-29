using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using static IngenIA365ERP.API.IntegrationTests.Lending.MigracionE2E;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;
using H = IngenIA365ERP.Application.Lending.Risk.PlantillaDeHistorialComplementario.Columnas;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T1023 (feature 013, K6a, fase 22; quickstart §1.7, §8; SC-023; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): la pérdida esperada
/// por sus rutas, en tres cooperativas aisladas del mismo host.
/// <list type="bullet">
/// <item><b>coop_obligada</b> (cooperativa de ahorro y crédito, obligada por la regla sembrada): dos créditos migrados de consumo sin libranza
/// desembolsados 75 días antes del corte; activar se niega con <c>Lending.Activation.RequirementsMissing</c> y la lista de los créditos sin historial
/// (<c>ArrearsHistoryComplete</c>, <c>Lending.Migration.ArrearsHistoryRequired</c>; US8-10); la carga complementaria
/// (<c>/risk/arrears-history/import</c>) completa los meses y la activación pasa. El primer cierre mensual deja los dos créditos en
/// <c>ExpectedLossAnnex2</c> con PI, PDI y exposición, el Anexo 2 sellado en los documentos usados y el modelo por segmento «obligada» en los tres
/// segmentos y «Anexo 1» en los demás; desistir se rechaza citando la obligación (<c>Lending.ImpairmentModel.Obligated</c>; US9-6); registrar una
/// elección sin <c>Lending.ImpairmentModel.Manage</c> es el 404 genérico.</item>
/// <item><b>coop_ensayo</b> (multiactiva sin sección de ahorro, no obligada): un crédito comercial de persona natural y uno de consumo migrados; el
/// primer cierre va por el Anexo 1. La adopción voluntaria del comercial con acta del consejo, estudio y aviso subidos a la propuesta: rigiendo antes
/// del aviso más la antelación se rechaza con <c>earliestValidFrom</c>; en plazo se registra; el cierre del mes siguiente lleva el comercial en
/// pérdida esperada con fundamento «voluntaria» y su variación es la diferencia frente al cierre anterior (US9-7). El desistimiento vuelve al Anexo 1
/// desde su fecha sin tocar el cierre ya hecho (misma huella de líneas).</item>
/// <item><b>coop_ensayo</b>, después: la obligación sobrevenida por una versión del <b>perfil</b> de la cooperativa (nunca una
/// <c>ReglaDePerdidaEsperada</c> nueva en la central, que tocaría a todo el host): el alta no se niega por el historial, responde obligada y levanta
/// <c>Cartera.ObligacionSobrevenida</c> y <c>Cartera.HistorialDeMoraIncompleto</c> con el crédito de consumo sin historial completo (US9-9).</item>
/// </list>
/// <para>
/// <b>Anexo 2 de prueba</b>: la semilla no trae la PI de la aplicación voluntaria (la de la CE 100 está por registrar), así que la adopción la registra
/// el maestro por la vía de valores nacionales (<c>/api/saas/regulatory-documents</c>) como un Anexo 2 <b>de prueba</b> —código
/// <c>ANEXO2-E2E-PRUEBA</c>, <c>verified = false</c>, fuente que lo dice— igual al vigente más una PI <c>VOLUNTARIA</c> copiada de la de las
/// cooperativas de ahorro y crédito. Como la central es una sola por host, rige también para las demás cooperativas desde su fecha: sus tablas son
/// las del vigente, así que sólo cambia que la aplicación voluntaria pasa a ser posible.
/// </para>
/// <para>
/// Lo que no recorre y fijan los casos dorados y las pruebas de Application: los valores de PI, PDI y deterioro (pe01–pe10), la regla de meses
/// faltantes en el cierre (<c>ExecuteMonthlyCloseConPerdidaEsperadaTests</c>) y un crédito de un segmento fuera del Anexo 2 con sus líneas (aquí se ve
/// por el modelo por segmento). (nuevo)
/// </para>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class PerdidaEsperadaTests(CentralIdentityApiFixture fx)
{
    private const string RutaDeRiesgo = CierreE2E.Riesgo;
    private const string Elecciones = RutaDeRiesgo + "/impairment-model/elections";
    private const string Historial = RutaDeRiesgo + "/arrears-history";
    private const string DuenoDeSoportes = "LendingImpairmentModelElectionSupport";
    private const string LineaComercial = "MIGCOM";
    private const string CodigoDelAnexoDePrueba = "ANEXO2-E2E-PRUEBA";

    private static readonly string[] SegmentosPe = ["CONSUMO_LIBRANZA", "CONSUMO_SIN_LIBRANZA", "COMERCIAL_PN"];

    // =================================================================================================== obligada ==

    [Fact]
    public async Task La_obligada_completa_el_historial_para_activar_cierra_en_perdida_esperada_y_no_puede_desistir()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var corte = hoy.AddDays(-1);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "pe_obligada", corte.Year);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var tenant = coop.TenantPublicId;
        await PreparacionAsync(http, coop, corte, "COOP_AHORRO_CREDITO", financiera: true);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop);
        var oficina = $"Agencia {usuarios.O1.Codigo}";

        // ------------------------------------------------------- dos consumos migrados sin el historial de 36 meses --
        var desembolso = corte.AddDays(-75);
        var numeros = new[] { "PE-0001", "PE-0002" };
        var deudores = new List<string>();
        foreach (var n in numeros) deudores.Add((await PersonaAsync(http, admin, $"Deudor{n[^1]}", "Obligada")).Documento);
        var carga = await CargaAsync(http, admin, corte, numeros.Select((n, i) => Credito(n, deudores[i], oficina, desembolso, 1_200_000m, 12)).ToList());
        // Decisión del dueño del 2026-10-18: los requisitos de la activación se exigen en todos los ambientes.
        await CarteraE2E.RequisitosDeActivacionAsync(fx, coop, corte);
        var punto = await CarteraE2E.ActaDeActivacionAsync(http, coop, carga, hoy);
        await AceptarDiferenciasAsync(http, admin, carga, punto);

        // ------------------------------------------------------------------- activar sin historial: negada con la lista --
        var sinHistorial = await ActivarAsync(http, admin, carga, hoy, punto);
        sinHistorial.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await sinHistorial.Content.ReadAsStringAsync());
        var negada = await CarteraE2E.LeerAsync(sinHistorial);
        negada.GetProperty("code").GetString().Should().Be("Lending.Activation.RequirementsMissing");
        var historial = negada.GetProperty("data").GetProperty("missing").EnumerateArray()
            .Single(m => m.GetProperty("code").GetString() == "ArrearsHistoryComplete").GetProperty("detail").GetString()!;
        historial.Should().Contain("Lending.Migration.ArrearsHistoryRequired").And.Contain(numeros[0]).And.Contain(numeros[1],
            "la lista nombra los créditos migrados sin historial (US8-10)");

        // -------------------------------------------------- la carga complementaria y la activación (antes de activar) --
        var meses = MesesAntesDelCorte(desembolso, corte);
        meses.Should().NotBeEmpty();
        var revision = await ImportarHistorialAsync(http, admin, numeros.SelectMany(n => meses.Select(m => (n, m, 0))), "review");
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var revisado = await CarteraE2E.LeerAsync(revision);
        revisado.GetProperty("applied").GetBoolean().Should().BeFalse("revisar no guarda");
        var aplicada = await ImportarHistorialAsync(http, admin, numeros.SelectMany(n => meses.Select(m => (n, m, 0))), "apply");
        aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(aplicada)).GetProperty("applied").GetBoolean().Should().BeTrue();

        var activar = await ActivarAsync(http, admin, carga, hoy, punto);
        activar.StatusCode.Should().Be(HttpStatusCode.Created, $"con el historial completo la obligada activa: «{await activar.Content.ReadAsStringAsync()}»");

        // --------------------------------------------------------- el modelo por segmento: obligada en los tres de PE --
        var modelo = (await CarteraE2E.GetAsync(http, admin, $"{RutaDeRiesgo}/impairment-model")).EnumerateArray().ToList();
        foreach (var segmento in SegmentosPe)
        {
            var s = modelo.Single(x => x.GetProperty("segment").GetString() == segmento);
            s.GetProperty("model").GetString().Should().Be("ExpectedLossAnnex2", segmento);
            s.GetProperty("basis").GetString().Should().Be("Obligated", segmento);
            s.GetProperty("mark").ValueKind.Should().Be(JsonValueKind.Null, "con K6a no queda la marca del modelo no disponible");
        }
        modelo.Where(x => !SegmentosPe.Contains(x.GetProperty("segment").GetString()))
            .Should().OnlyContain(x => x.GetProperty("model").GetString() == "ArrearsAnnex1", "los segmentos fuera del Anexo 2 siguen en el Anexo 1");

        // -------------------------------------------------------- el primer cierre mensual en pérdida esperada (US9-6) --
        var fin = UltimoDia(hoy);
        using (CausacionE2E.RelojEn(fx, fin.AddDays(1)))
        {
            var cierre = await CerrarMesAsync(http, admin, tenant, fin.Year, fin.Month);
            var encabezado = await CarteraE2E.GetAsync(http, admin, $"{CierreE2E.Cierres}/{cierre}");
            encabezado.GetProperty("documentsUsed").ToString().Should().Contain("SarcAnexo2", "el cierre sella el Anexo 2 que usó");
            encabezado.GetProperty("close").GetProperty("marks").GetProperty("obligatoryModelUnavailable").GetBoolean().Should().BeFalse();
            encabezado.GetProperty("close").GetProperty("marks").GetProperty("incompleteHistory").GetBoolean()
                .Should().BeFalse("la carga complementaria completó el historial");

            var lineas = await LineasAsync(http, admin, cierre);
            lineas.Should().HaveCount(2);
            foreach (var linea in lineas)
            {
                linea.GetProperty("impairmentModel").GetString().Should().Be("ExpectedLossAnnex2", linea.ToString());
                var pe = linea.GetProperty("expectedLoss");
                pe.GetProperty("segment").GetString().Should().Be("CONSUMO_SIN_LIBRANZA");
                pe.GetProperty("probabilityOfDefault").GetDecimal().Should().BeInRange(0m, 1m);
                pe.GetProperty("lossGivenDefault").GetDecimal().Should().BeInRange(0m, 1m);
                pe.GetProperty("exposureAtDefault").GetDecimal().Should().BeGreaterThan(0m);
                pe.GetProperty("probabilityOfDefaultKey").GetString().Should().Be("COOP_AHORRO_CREDITO");
                linea.GetProperty("explanation").GetArrayLength().Should().BeGreaterThan(0, "la pérdida esperada se explica por componente (FR-106)");
            }

            // ------------------------------------------------- la obligada no desiste; sin el permiso, la ruta no existe --
            var desistir = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Elecciones, Eleccion(Guid.NewGuid(), "VoluntaryWithdrawal", fin.AddDays(20),
                Guid.NewGuid(), Guid.NewGuid(), fin.AddDays(1), Guid.NewGuid()));
            desistir.IsSuccessStatusCode.Should().BeFalse(await desistir.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(desistir)).Should().Be("Lending.ImpairmentModel.Obligated", "la obligación no admite desistimiento");

            var sinPermiso = await CarteraE2E.ConClaveAsync(http, usuarios.Jefe.Token, HttpMethod.Post, Elecciones, Eleccion(Guid.NewGuid(), "VoluntaryAdoption",
                fin.AddDays(20), Guid.NewGuid(), Guid.NewGuid(), fin.AddDays(1), Guid.NewGuid()));
            sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin Lending.ImpairmentModel.Manage la ruta es el 404 genérico");
        }
    }

    // ============================================================================== adopción, desistimiento y perfil ==

    [Fact]
    public async Task La_no_obligada_adopta_con_acta_y_aviso_cierra_en_delta_desiste_sin_recalcular_y_queda_obligada_por_su_perfil()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var corte = hoy.AddDays(-1);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "pe_ensayo", corte.Year);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var tenant = coop.TenantPublicId;
        await PreparacionAsync(http, coop, corte, "COOP_MULTIACTIVA", financiera: false);
        await LineaComercialAsync(fx, http, admin, corte);
        var oficina = await OficinaAsync(http, admin, tenant, "PEE");
        await AnexoDePruebaConVoluntariaAsync(http, tenant, corte);
        // La elección la registra la secretaría del consejo, no el administrador: él lleva el acta (registra los votos y la cierra) y quien
        // decide no radica (segregación, Lending.Segregation.Conflict).
        var secretaria = (await UsuariosDeCartera.CrearAsync(fx, coop)).Secretaria.Token;

        // Un comercial de persona natural con su historial completo (nace en el mes del corte) y un consumo sin el historial de 36 meses: a la no
        // obligada no se le exige historial para activar.
        var (deudorComercial, _) = await PersonaAsync(http, admin, "Comercial", "Ensayo");
        var (deudorConsumo, _) = await PersonaAsync(http, admin, "Consumo", "Ensayo");
        var comercial = Credito("EN-COM-01", deudorComercial, oficina, new DateOnly(corte.Year, corte.Month, 1), 2_000_000m, 12);
        comercial[C.Linea] = LineaComercial;
        var consumo = Credito("EN-CON-01", deudorConsumo, oficina, corte.AddDays(-75), 1_200_000m, 12);
        var carga = await CargaAsync(http, admin, corte, [comercial, consumo]);
        // Decisión del dueño del 2026-10-18: los requisitos de la activación se exigen en todos los ambientes.
        await CarteraE2E.RequisitosDeActivacionAsync(fx, coop, corte);
        var puntoDeActivacion = await CarteraE2E.ActaDeActivacionAsync(http, coop, carga, hoy);
        await AceptarDiferenciasAsync(http, admin, carga, puntoDeActivacion);
        var activar = await ActivarAsync(http, admin, carga, hoy, puntoDeActivacion);
        activar.StatusCode.Should().Be(HttpStatusCode.Created, $"la no obligada activa sin historial completo: «{await activar.Content.ReadAsStringAsync()}»");
        var creditos = (await CarteraE2E.GetAsync(http, admin, "/api/lending/loans?pageSize=10")).GetProperty("items").EnumerateArray()
            .ToDictionary(l => l.GetProperty("number").GetString()!, l => l.GetProperty("publicId").GetGuid());
        var idComercial = creditos["EN-COM-01"];

        // ------------------------------------------------------------------------ el primer cierre, por el Anexo 1 --
        var mes1 = UltimoDia(hoy);
        Guid cierre1;
        using (CausacionE2E.RelojEn(fx, mes1.AddDays(1)))
        {
            cierre1 = await CerrarMesAsync(http, admin, tenant, mes1.Year, mes1.Month);
            (await LineasAsync(http, admin, cierre1)).Should().OnlyContain(l => l.GetProperty("impairmentModel").GetString() == "ArrearsAnnex1",
                "sin adopción la no obligada cierra por el Anexo 1");
        }

        // --------------------------------------------------------------- la adopción voluntaria del comercial (US9-7) --
        var aviso = mes1.AddDays(1);
        var mes2 = UltimoDia(aviso);
        Guid cierre2;
        string huella2;
        using (CausacionE2E.RelojEn(fx, aviso))
        {
            var antelacion = (int)(await ReglaVigenteAsync(http))["voluntaryNoticeDays"]!;
            var masTemprana = aviso.AddDays(antelacion);
            masTemprana.Should().BeOnOrBefore(mes2, "la adopción rige dentro del mes siguiente");

            var propuesta = Guid.NewGuid();
            var estudio = await ActasE2E.SoporteAsync(http, admin, propuesta, DuenoDeSoportes, "estudio-de-impacto.pdf");
            var soporteDelAviso = await ActasE2E.SoporteAsync(http, admin, propuesta, DuenoDeSoportes, "aviso-supersolidaria.pdf");
            var punto = await ActaDelCambioAsync(http, coop, propuesta, aviso);

            var temprana = await CarteraE2E.ConClaveAsync(http, secretaria, HttpMethod.Post, Elecciones,
                Eleccion(propuesta, "VoluntaryAdoption", aviso.AddDays(Math.Max(1, antelacion - 5)), punto, estudio, aviso, soporteDelAviso, ["COMERCIAL_PN"]));
            temprana.IsSuccessStatusCode.Should().BeFalse(await temprana.Content.ReadAsStringAsync());
            var fueraDePlazo = await CarteraE2E.LeerAsync(temprana);
            fueraDePlazo.GetProperty("code").GetString().Should().Be("Lending.ImpairmentModel.NoticeOutOfTerm");
            fueraDePlazo.GetProperty("data").GetProperty("earliestValidFrom").GetString().Should().Be(Fecha(masTemprana), "la pantalla ofrece la más temprana");

            var adopcion = await CarteraE2E.ConClaveAsync(http, secretaria, HttpMethod.Post, Elecciones,
                Eleccion(propuesta, "VoluntaryAdoption", masTemprana, punto, estudio, aviso, soporteDelAviso, ["COMERCIAL_PN"]));
            adopcion.StatusCode.Should().Be(HttpStatusCode.Created, await adopcion.Content.ReadAsStringAsync());
            var adoptada = await CarteraE2E.LeerAsync(adopcion);
            adoptada.GetProperty("publicId").GetGuid().Should().Be(propuesta, "la elección nace con el identificador de la propuesta del acta");
            adoptada.GetProperty("noticeLeadDays").GetInt32().Should().Be(antelacion, "la antelación de la regla queda sellada");
            (await CarteraE2E.GetAsync(http, admin, Elecciones)).EnumerateArray().Should().ContainSingle();

            // Registrada la elección, sus soportes no se cambian.
            var otroSoporte = await AdjuntosE2E.PedirSubidaAsync(http, admin, propuesta, AdjuntosE2E.Pdf(1_024), "otro.pdf", ownerEntityType: DuenoDeSoportes);
            otroSoporte.IsSuccessStatusCode.Should().BeFalse("la elección registrada bloquea los soportes de su propuesta");
            (await CarteraE2E.CodigoDeErrorAsync(otroSoporte)).Should().Be("Attachments.OwnerLocked");

            var modeloDesde = (await CarteraE2E.GetAsync(http, admin, $"{RutaDeRiesgo}/impairment-model?date={Fecha(masTemprana)}")).EnumerateArray()
                .Single(s => s.GetProperty("segment").GetString() == "COMERCIAL_PN");
            modeloDesde.GetProperty("model").GetString().Should().Be("ExpectedLossAnnex2");
            modeloDesde.GetProperty("basis").GetString().Should().Be("Voluntary");
        }

        using (CausacionE2E.RelojEn(fx, mes2.AddDays(1)))
        {
            cierre2 = await CerrarMesAsync(http, admin, tenant, mes2.Year, mes2.Month);
            var anteriores = (await LineasAsync(http, admin, cierre1)).ToDictionary(l => l.GetProperty("loanPublicId").GetGuid());
            var lineas = (await LineasAsync(http, admin, cierre2)).ToDictionary(l => l.GetProperty("loanPublicId").GetGuid());
            var lineaComercial = lineas[idComercial];
            lineaComercial.GetProperty("impairmentModel").GetString().Should().Be("ExpectedLossAnnex2", "el comercial rige en pérdida esperada");
            lineaComercial.GetProperty("expectedLoss").GetProperty("probabilityOfDefaultKey").GetString().Should().Be("VOLUNTARIA");
            lineaComercial.GetProperty("impairmentDelta").GetDecimal().Should().Be(
                lineaComercial.GetProperty("totalImpairment").GetDecimal() - anteriores[idComercial].GetProperty("totalImpairment").GetDecimal(),
                "el primer cierre de la adopción emite sólo la diferencia frente al cierre anterior");
            lineas[creditos["EN-CON-01"]].GetProperty("impairmentModel").GetString().Should().Be("ArrearsAnnex1", "el consumo no se adoptó");
            huella2 = (await CarteraE2E.GetAsync(http, admin, $"{CierreE2E.Cierres}/{cierre2}")).GetProperty("linesSha256").GetString()!;
        }

        // ---------------------------------------------------------------- el desistimiento, sin recalcular el cierre --
        var avisoDeDesistimiento = mes2.AddDays(1);
        using (CausacionE2E.RelojEn(fx, avisoDeDesistimiento))
        {
            var antelacion = (int)(await ReglaVigenteAsync(http))["voluntaryNoticeDays"]!;
            var desde = avisoDeDesistimiento.AddDays(antelacion);
            var propuesta = Guid.NewGuid();
            var estudio = await ActasE2E.SoporteAsync(http, admin, propuesta, DuenoDeSoportes, "estudio-desistimiento.pdf");
            var soporteDelAviso = await ActasE2E.SoporteAsync(http, admin, propuesta, DuenoDeSoportes, "aviso-desistimiento.pdf");
            var punto = await ActaDelCambioAsync(http, coop, propuesta, avisoDeDesistimiento);
            var desistir = await CarteraE2E.ConClaveAsync(http, secretaria, HttpMethod.Post, Elecciones,
                Eleccion(propuesta, "VoluntaryWithdrawal", desde, punto, estudio, avisoDeDesistimiento, soporteDelAviso, ["COMERCIAL_PN"]));
            desistir.StatusCode.Should().Be(HttpStatusCode.Created, await desistir.Content.ReadAsStringAsync());

            var modelo = (await CarteraE2E.GetAsync(http, admin, $"{RutaDeRiesgo}/impairment-model?date={Fecha(desde)}")).EnumerateArray()
                .Single(s => s.GetProperty("segment").GetString() == "COMERCIAL_PN");
            modelo.GetProperty("model").GetString().Should().Be("ArrearsAnnex1", "desde su fecha vuelve al Anexo 1");
            modelo.GetProperty("basis").GetString().Should().Be("NotObligated");
            (await CarteraE2E.GetAsync(http, admin, $"{CierreE2E.Cierres}/{cierre2}")).GetProperty("linesSha256").GetString()
                .Should().Be(huella2, "el desistimiento no recalcula el cierre hecho en pérdida esperada");

            // ------------------------------------------------------ la obligación sobrevenida por el perfil (US9-9) --
            var obligadaDesde = desde.AddDays(1);
            var perfil = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/core/regulatory-profile/versions", new
            {
                organizationType = "COOP_AHORRO_CREDITO", financialActivity = true, supervisionLevel = 2, validFrom = obligadaDesde,
                source = "Resolución de la Supersolidaria que autoriza la actividad financiera (e2e)", reason = "La cooperativa pasa a ahorro y crédito",
            });
            perfil.StatusCode.Should().Be(HttpStatusCode.Created, $"el perfil nunca se niega por el historial: «{await perfil.Content.ReadAsStringAsync()}»");
            var evaluacion = (await CarteraE2E.LeerAsync(perfil)).GetProperty("expectedLoss");
            evaluacion.GetProperty("obligated").GetBoolean().Should().BeTrue();

            var sobrevenida = await AlertasAsync(http, admin, "Cartera.ObligacionSobrevenida");
            sobrevenida.Should().Contain(a => a.ToString().Contains("EN-CON-01", StringComparison.Ordinal), "la alerta nombra el crédito sin historial");
            var incompleto = await AlertasAsync(http, admin, "Cartera.HistorialDeMoraIncompleto");
            incompleto.Should().Contain(a => a.ToString().Contains("EN-CON-01", StringComparison.Ordinal), "y la del historial incompleto lo lista");

            var obligada = (await CarteraE2E.GetAsync(http, admin, $"{RutaDeRiesgo}/impairment-model?date={Fecha(obligadaDesde)}")).EnumerateArray()
                .Where(s => SegmentosPe.Contains(s.GetProperty("segment").GetString())).ToList();
            obligada.Should().OnlyContain(s => s.GetProperty("basis").GetString() == "Obligated" && s.GetProperty("model").GetString() == "ExpectedLossAnnex2",
                "desde la vigencia del perfil la pérdida esperada es obligatoria");
        }
    }

    // ================================================================================================ ayudantes ==

    /// <summary>La contabilidad, los dobles, «Advertir» sin regla contable, la línea MIGCON y el perfil regulatorio desde el corte.</summary>
    private async Task PreparacionAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, DateOnly corte, string tipo, bool financiera)
    {
        var admin = coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop);
        await LineaAsync(fx, coop, corte);
        await CarteraE2E.RecaudoYTesoreriaRealesAsync(fx, http, coop, corte);
        await CarteraE2E.AdvertirSinReglaContableAsync(http, admin, corte);
        var perfil = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/core/regulatory-profile/versions", new
        {
            organizationType = tipo, financialActivity = financiera, supervisionLevel = 2, validFrom = corte.AddDays(-90),
            source = "Certificado de la Supersolidaria (e2e)", reason = "Perfil de la cooperativa para la pérdida esperada",
        });
        perfil.StatusCode.Should().Be(HttpStatusCode.Created, $"el perfil {tipo}: «{await perfil.Content.ReadAsStringAsync()}»");
    }

    /// <summary>La línea comercial de persona natural <c>MIGCOM</c> para los créditos migrados (segmento <c>COMERCIAL_PN</c>).</summary>
    private static async Task LineaComercialAsync(CentralIdentityApiFixture fx, HttpClient http, string admin, DateOnly desde)
    {
        var version = LineasE2E.VersionFija(desde, "COMERCIAL", "PersonaNatural", 0.20m, "Línea comercial de la cartera migrada (e2e)");
        version["interestTaxConcept"] = new { taxed = false, supportAttachmentPublicId = Guid.NewGuid() };
        version["amounts"] = new { min = 100_000m, max = 30_000_000m };
        // Vigente en el corte (ayer): por la ruta una versión no rige antes de hoy, así que se registra con el reloj de la suite en el corte.
        using (desde < CarteraE2E.Hoy(fx) ? CausacionE2E.RelojEn(fx, desde) : null)
            await LineasE2E.CrearExitoAsync(http, admin, LineaComercial, "Comercial persona natural migrado", version);
    }

    /// <summary>La carga con los créditos dados (cuotas, saldos y riesgo al corte), aplicada; devuelve la carga.</summary>
    private static async Task<Guid> CargaAsync(HttpClient http, string token, DateOnly corte, IReadOnlyList<Dictionary<string, string?>> creditos)
    {
        var carga = await CrearCargaAsync(http, token, corte, "Cartera para la pérdida esperada (e2e)");
        var numeros = creditos.Select(c => c[C.Numero]!).ToList();
        await CargarAsync(http, token, carga, P.Loans, creditos);
        await CargarAsync(http, token, carga, P.Installments, numeros.SelectMany(n => Cuotas(n, corte, 10, 100_000m)));
        await CargarAsync(http, token, carga, P.Balances, numeros.Select(n => Saldo(n, "Principal", 1_000_000m)));
        await CargarAsync(http, token, carga, P.RiskData, numeros.Select(Riesgo));
        var aplicar = await AplicarAsync(http, token, carga, "Carga de la pérdida esperada");
        aplicar.StatusCode.Should().Be(HttpStatusCode.OK, await aplicar.Content.ReadAsStringAsync());
        return carga;
    }

    /// <summary>Acepta con el punto del acta las diferencias pendientes del cuadre (sin la matriz contable, el capital no cuadra).</summary>
    private static async Task AceptarDiferenciasAsync(HttpClient http, string token, Guid carga, Guid punto)
    {
        var diferencias = (await CarteraE2E.GetAsync(http, token, $"{Cargas}/{carga}/balance-check")).GetProperty("differences").EnumerateArray()
            .Where(d => d.GetProperty("acceptedAt").ValueKind == JsonValueKind.Null).Select(d => d.GetProperty("publicId").GetGuid()).ToList();
        if (diferencias.Count == 0) return;
        var aceptar = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Cargas}/{carga}/differences/accept",
            new { differencePublicIds = diferencias, sessionItemPublicId = punto, reason = "Cuadre aceptado por el consejo (e2e)" });
        aceptar.StatusCode.Should().Be(HttpStatusCode.OK, await aceptar.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> ActivarAsync(HttpClient http, string token, Guid carga, DateOnly fecha, Guid punto) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Activacion, new
        {
            migrationLoadPublicId = carga, activationDate = fecha, sessionItemPublicId = punto, acknowledgeWarnings = true,
            reason = "Activación de Cartera con la pérdida esperada (e2e)", payrollWarningAcknowledged = true,
        });

    /// <summary>Los meses desde el del desembolso hasta el anterior al del corte (el del corte lo cubre la mora migrada).</summary>
    private static List<string> MesesAntesDelCorte(DateOnly desembolso, DateOnly corte)
    {
        var meses = new List<string>();
        for (var m = new DateOnly(desembolso.Year, desembolso.Month, 1); m < new DateOnly(corte.Year, corte.Month, 1); m = m.AddMonths(1))
            meses.Add(m.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        return meses;
    }

    /// <summary><c>POST /risk/arrears-history/import?mode=</c> con un <c>.csv</c> de la plantilla <c>lending.arrears-history</c>.</summary>
    private static Task<HttpResponseMessage> ImportarHistorialAsync(HttpClient http, string token, IEnumerable<(string Credito, string Mes, int Mora)> filas,
        string modo)
    {
        var texto = new StringBuilder();
        texto.AppendLine(string.Join(';', H.NumeroDeCredito, H.Mes, H.MoraMaximaDias, H.MoraAlCierreDias, H.Fuente));
        foreach (var (credito, mes, mora) in filas)
            texto.AppendLine(string.Join(';', credito, mes, mora.ToString(CultureInfo.InvariantCulture), mora.ToString(CultureInfo.InvariantCulture),
                "Reporte de mora mensual del sistema anterior (e2e)"));
        var formulario = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(texto.ToString()));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        formulario.Add(bytes, "file", "historial-de-mora.csv");
        formulario.Add(new StringContent("Historial de mora que la migración no trajo (e2e)"), "reason");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"{Historial}/import?mode={modo}") { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }

    /// <summary>Causa hasta hoy (el reloj ya está en el día siguiente al fin del mes), ordena el cierre y lo corre; devuelve el cierre.</summary>
    private async Task<Guid> CerrarMesAsync(HttpClient http, string token, Guid tenant, int anio, int mes)
    {
        await CausacionE2E.CicloDeHoyAsync(fx, http, token, tenant);
        await new ConductorDelDespachador(fx).PasadasAsync(tenant, 3);
        var preparacion = await CarteraE2E.GetAsync(http, token, $"{CierreE2E.Cierres}/{anio}/{mes}/readiness");
        preparacion.GetProperty("ready").GetBoolean().Should().BeTrue(preparacion.ToString());
        var corrida = await CierreE2E.OrdenarAsync(http, token, anio, mes);
        await CierreE2E.CorrerAsync(fx, http, token, tenant, corrida);
        return await CierreE2E.MensualAsync(http, token, anio, mes, version: 1);
    }

    private static async Task<List<JsonElement>> LineasAsync(HttpClient http, string token, Guid cierre) =>
        (await CarteraE2E.GetAsync(http, token, $"{CierreE2E.Cierres}/{cierre}/loans?pageSize=200")).GetProperty("items").EnumerateArray().ToList();

    private static async Task<List<JsonElement>> AlertasAsync(HttpClient http, string token, string tipo) =>
        (await CarteraE2E.GetAsync(http, token, $"/api/lending/alerts?typeCode={tipo}&pageSize=50")).GetProperty("items").EnumerateArray().ToList();

    private static object Eleccion(Guid propuesta, string clase, DateOnly desde, Guid punto, Guid estudio, DateOnly aviso, Guid soporteDelAviso,
        string[]? segmentos = null) => new
        {
            electionPublicId = propuesta, kind = clase, segments = segmentos, validFrom = desde, sessionItemPublicId = punto,
            impactStudyAttachmentPublicId = estudio, noticeDate = aviso, noticeAttachmentPublicId = soporteDelAviso,
            reason = "Decisión del consejo de administración (e2e)",
        };

    private static DateOnly UltimoDia(DateOnly fecha) => new(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month));

    /// <summary>El contenido de la <c>ReglaDePerdidaEsperada</c> vigente en la central (la sembrada), sin sus comentarios.</summary>
    private async Task<JsonNode> ReglaVigenteAsync(HttpClient http) => await DocumentoVigenteAsync(http, "ReglaDePerdidaEsperada");

    private async Task<JsonNode> DocumentoVigenteAsync(HttpClient http, string clase, string? sinCodigo = null)
    {
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        var vigente = (await CarteraE2E.GetAsync(http, maestro, $"/api/saas/regulatory-documents?kind={clase}")).EnumerateArray()
            .Where(d => sinCodigo is null || d.GetProperty("code").GetString() != sinCodigo)
            .OrderBy(d => d.GetProperty("validFrom").GetString(), StringComparer.Ordinal)
            .ThenBy(d => d.TryGetProperty("sequence", out var s) ? s.GetInt64() : 0L)
            .Last();
        using var contenido = await CarteraE2E.MandarGetAsync(http, maestro, $"/api/saas/regulatory-documents/{vigente.GetProperty("publicId").GetGuid()}/content");
        contenido.StatusCode.Should().Be(HttpStatusCode.OK, await contenido.Content.ReadAsStringAsync());
        return JsonNode.Parse(await contenido.Content.ReadAsStringAsync(), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!;
    }

    /// <summary>
    /// El Anexo 2 <b>de prueba</b> (ver el resumen de la clase): el vigente más una PI <c>VOLUNTARIA</c> igual a la de COOP_AHORRO_CREDITO en cada
    /// segmento, registrado por el maestro con <c>verified = false</c> y una fuente que lo dice, vigente desde <paramref name="desde"/>; una sola vez por
    /// host. Después replica en la cooperativa.
    /// </summary>
    private async Task AnexoDePruebaConVoluntariaAsync(HttpClient http, Guid tenant, DateOnly desde)
    {
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        var registrados = (await CarteraE2E.GetAsync(http, maestro, "/api/saas/regulatory-documents?kind=SarcAnexo2")).EnumerateArray().ToList();
        if (!registrados.Any(d => d.GetProperty("code").GetString() == CodigoDelAnexoDePrueba))
        {
            var anexo = (await DocumentoVigenteAsync(http, "SarcAnexo2", sinCodigo: CodigoDelAnexoDePrueba)).AsObject();
            var pi = anexo["probabilityOfDefault"]!.AsArray();
            var voluntarias = pi.Where(f => (string?)f!["organizationType"] == "COOP_AHORRO_CREDITO").Select(f => f!.DeepClone()).ToList();
            voluntarias.Should().NotBeEmpty("el Anexo 2 sembrado trae la PI de las cooperativas de ahorro y crédito");
            foreach (var fila in voluntarias)
            {
                fila["organizationType"] = "VOLUNTARIA";
                pi.Add(fila);
            }
            var alta = await CarteraE2E.ConClaveAsync(http, maestro, HttpMethod.Post, "/api/saas/regulatory-documents", new
            {
                kind = "SarcAnexo2", code = CodigoDelAnexoDePrueba, validFrom = desde, schemaVersion = 1, content = anexo.ToJsonString(), verified = false,
                source = "DE PRUEBA (e2e): Anexo 2 vigente con una PI VOLUNTARIA inventada (copia de la de ahorro y crédito); no es la de la CE 100",
            });
            alta.StatusCode.Should().Be(HttpStatusCode.Created, $"registrar el Anexo 2 de prueba: «{await alta.Content.ReadAsStringAsync()}»");
        }
        await ValoresNacionalesE2E.ReplicarAsync(fx, tenant);
    }

    /// <summary>
    /// El acta del cambio del modelo: un órgano propio con tres miembros, la sesión del día con todos presentes, el punto <c>Decision</c> de sujeto
    /// <c>ImpairmentModelChange</c> sobre la propuesta, tres votos a favor que registra la secretaría (el administrador, con su persona) y el cierre
    /// con la huella revisada (como <see cref="CarteraE2E.ActaDeActivacionAsync"/>). Devuelve el punto.
    /// </summary>
    private static async Task<Guid> ActaDelCambioAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, Guid propuesta, DateOnly fecha)
    {
        var admin = coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop);
        var miembros = new List<Guid>();
        for (var i = 1; i <= 3; i++) miembros.Add(await CarteraE2E.PersonaAsync(http, admin, $"Consejero{i}", "Modelo"));
        var codigo = $"CMD{propuesta.ToString("N")[..6].ToUpperInvariant()}";
        var organo = (await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/governing-bodies", new
        {
            code = codigo, name = $"Consejo para el modelo {codigo} (e2e)", kind = "Other", decidesCredit = false, minutesPrefix = "CMD-",
            rules = new
            {
                quorum = new { numerator = 1, denominator = 2, comparison = "MoreThan" },
                ordinaryMajority = new { numerator = 1, denominator = 2, comparison = "MoreThan" },
                validFrom = fecha, reason = "Reglas del órgano (e2e)", source = "Estatutos de la cooperativa de prueba",
            },
        })).GetProperty("publicId").GetGuid();
        var delOrgano = new List<Guid>();
        foreach (var persona in miembros)
            delOrgano.Add(CarteraE2E.MiembroDe(await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/governing-bodies/{organo}/members", new
            {
                personPublicId = persona, position = "Member", votes = true, validFrom = fecha, appointmentNotes = "Nombramiento de prueba (e2e)",
            })));
        var sesion = (await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/sessions", new
        {
            governingBodyPublicId = organo, kind = "Extraordinary", mode = "InPerson", heldOn = fecha, notes = "Cambio del modelo de deterioro (e2e)",
        })).GetProperty("publicId").GetGuid();
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/sessions/{sesion}/attendance", new
        {
            attendees = delOrgano.Select(m => new { memberPublicId = m, present = true }).ToList(),
        });
        var punto = (await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/sessions/{sesion}/items", new
        {
            subject = LendingDecisionSubjects.ImpairmentModelChange, subjectPublicId = propuesta, kind = "Decision",
        })).GetProperty("itemPublicId").GetGuid();
        foreach (var persona in miembros)
            await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/sessions/{sesion}/items/{punto}/votes", new
            {
                personPublicId = persona, vote = "For", method = "RecordedBySecretary",
            });
        var huella = (await CarteraE2E.GetAsync(http, admin, $"/api/lending/sessions/{sesion}")).GetProperty("currentContentSha256").GetString();
        var cerrada = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/sessions/{sesion}/close", new { expectedContentSha256 = huella });
        cerrada.GetProperty("status").GetInt32().Should().Be((int)SessionStatus.Closed, $"el acta del cambio se cierra: {cerrada}");
        return punto;
    }
}
