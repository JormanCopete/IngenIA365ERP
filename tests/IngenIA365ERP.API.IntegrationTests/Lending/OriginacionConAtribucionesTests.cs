using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Domain.Lending.Attributions;
using static IngenIA365ERP.API.IntegrationTests.Lending.OriginacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0384 (feature 013, US2; quickstart §1.7 y §3.5; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>), todo por HTTP con los
/// usuarios del ensayo (T0267) y la matriz del ensayo (analista 5, gerencia 20, comité 100 millones, consejo):
/// <list type="bullet">
/// <item><b>sin activar</b> (cooperativa «originacion»): simular sin escribir y el PDF; radicar → <c>Lending.NotActivated</c>; centrales sin
/// autorización y con el simulado; cupo rotativo dentro del monto operativo → <c>Active</c>; cupo sobre él → <c>PendingDecision</c> al
/// analista, que <c>sinpersona</c> no decide; cupo de 60 millones al comité, que excluye el voto de quien lo pidió y aprueba con condiciones en
/// un acta con SHA-256 y PDF; el cupo de un vinculado al consejo, sin que vote el interesado y con 4/5 de la composición; la coincidencia en
/// listas que bloquea el cupo y sólo libera el oficial; la concentración excedida con límite y exceso y, sin patrimonio técnico, el bloqueo
/// con su alerta;</item>
/// <item><b>con Cartera activada</b> (cooperativa «originacion_activa», C107, sin <c>Skip</c>): el documento faltante; tres solicitudes a
/// tres niveles; el reenrutamiento cuando cambia el consolidado; quien radicó no decide; las listas en la radicación no se exceptúan.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class OriginacionConAtribucionesTests(CentralIdentityApiFixture fx)
{
    private const string SinActivar = "originacion";
    private const string Activada = "originacion_activa";

    // -------------------------------------------------------------------------------------------- sin activar --

    [Fact]
    public async Task Simular_no_escribe_el_pdf_sale_y_radicar_sin_activar_se_niega()
    {
        var esc = await EscenarioAsync(fx, SinActivar, activada: false);
        using var http = fx.CreateClient();
        var asesor = esc.Usuarios.AsesorO1.Token;
        var cuerpo = new { creditLinePublicId = esc.Origen, amount = 12_000_000m, installments = 36, periodicity = "Monthly" };

        var simulada = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, $"{Simulaciones}/schedule", cuerpo);
        simulada.StatusCode.Should().Be(HttpStatusCode.OK, await simulada.Content.ReadAsStringAsync());
        var plan = await CarteraE2E.LeerAsync(simulada);
        plan.GetProperty("installments").GetArrayLength().Should().Be(36);
        plan.GetProperty("installments").EnumerateArray().Sum(c => c.GetProperty("principal").GetDecimal()).Should().Be(12_000_000m);
        plan.GetProperty("usury").GetProperty("passes").GetBoolean().Should().BeTrue();
        plan.GetProperty("effectiveAnnualRate").GetDecimal().Should().Be(0.22m);
        var huella = plan.GetProperty("simulationSha256").GetString();
        (await CarteraE2E.LeerAsync(await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, $"{Simulaciones}/schedule", cuerpo)))
            .GetProperty("simulationSha256").GetString().Should().Be(huella, "la huella de la simulación es reproducible");

        var pdf = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, $"{Simulaciones}/schedule/pdf", cuerpo);
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync())[..4].Should().Equal("%PDF"u8.ToArray());

        (await CarteraE2E.GetAsync(http, asesor, Solicitudes)).GetProperty("totalCount").GetInt32().Should().Be(0, "simular no registra nada");

        var persona = await DeudorAsync(http, esc.Coop.TokenAdmin, "Radicada", conCentrales: false);
        var radicar = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, Solicitudes, Solicitud(esc, persona, 3_000_000m));
        await InventarioE2E.FallaAsync(radicar, "Lending.NotActivated");
    }

    [Fact]
    public async Task Centrales_sin_autorizacion_se_impiden_y_con_ella_responde_el_simulado()
    {
        var esc = await EscenarioAsync(fx, SinActivar, activada: false);
        using var http = fx.CreateClient();
        var analista = esc.Usuarios.Analista.Token;
        var persona = await DeudorAsync(http, esc.Coop.TokenAdmin, "Federico", conCentrales: false);
        var consulta = new { personPublicId = persona, bureauCode = Central, purpose = "Review" };

        var sin = await CarteraE2E.ConClaveAsync(http, analista, HttpMethod.Post, Centrales, consulta);
        var error = await InventarioE2E.FallaAsync(sin, "Lending.Authorization.Missing");
        error.GetProperty("data").GetProperty("purpose").ToString().Should().Contain("CreditBureauQuery");

        await AutorizarAsync(http, esc.Coop.TokenAdmin, persona, "CreditBureauQuery");
        var con = await CarteraE2E.ConClaveAsync(http, analista, HttpMethod.Post, Centrales, consulta);
        con.StatusCode.Should().Be(HttpStatusCode.Created, await con.Content.ReadAsStringAsync());
        var registrada = await CarteraE2E.LeerAsync(con);
        registrada.GetProperty("simulated").GetBoolean().Should().BeTrue();
        registrada.GetProperty("status").GetString().Should().Be("Ok");
        var id = registrada.GetProperty("bureauQueryPublicId").GetGuid();

        var detalle = await CarteraE2E.GetAsync(http, analista, $"{Centrales}/{id}");
        detalle.GetProperty("artifact").GetProperty("attachmentPublicId").GetGuid().Should().NotBeEmpty("el informe crudo queda como adjunto de módulo");
        var enlace = await CarteraE2E.EnviarAsync(http, analista, HttpMethod.Post, $"{Centrales}/{id}/download-link", null);
        enlace.StatusCode.Should().Be(HttpStatusCode.OK, "el enlace es una consulta por POST, sin clave");

        (await CarteraE2E.MandarGetAsync(http, esc.Usuarios.AsesorO1.Token, $"{Centrales}/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "el asesor consulta pero no lee informes");
    }

    [Fact]
    public async Task El_cupo_dentro_del_monto_operativo_rige_y_el_que_lo_supera_espera_al_analista_que_sinpersona_no_decide()
    {
        var esc = await EscenarioAsync(fx, SinActivar, activada: false);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        await DarRolAsync(http, admin, esc.Usuarios.Comite1, esc.RolDeRadicacion);
        var quien = esc.Usuarios.Comite1.Token;

        var g = await DeudorAsync(http, admin, "Gabriel", conCentrales: false);
        var rotativo = await OtorgarCupoAsync(http, quien, g, 500_000m, esc.Hoy, "Revolving");
        rotativo.StatusCode.Should().Be(HttpStatusCode.Created, await rotativo.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(rotativo)).GetProperty("status").GetString().Should().Be("Active", "cabe en el monto operativo de 1.000.000");
        var disponible = await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{Cupos}/availability?personPublicId={g}");
        disponible.GetProperty("revolving").EnumerateArray().Single().GetProperty("available").GetDecimal().Should().Be(500_000m);

        var h = await DeudorAsync(http, admin, "Hilda", conCentrales: false);
        var mayor = await OtorgarCupoAsync(http, quien, h, 3_000_000m, esc.Hoy);
        mayor.StatusCode.Should().Be(HttpStatusCode.Created, await mayor.Content.ReadAsStringAsync());
        var pendiente = await CarteraE2E.LeerAsync(mayor);
        pendiente.GetProperty("status").GetString().Should().Be("PendingDecision", "supera el monto operativo de quien lo pide");
        var ruta = pendiente.GetProperty("routing");
        ruta.GetProperty("levelCode").GetString().Should().Be("ANALISTA");
        var cupo = pendiente.GetProperty("publicId").GetGuid();
        var huella = ruta.GetProperty("contentSha256").GetString();

        var sinPersona = await DecidirAsync(http, esc.Usuarios.SinPersona.Token, "credit-limits", cupo, "Approve", huella);
        await InventarioE2E.FallaAsync(sinPersona, "Lending.Security.UserWithoutPerson");

        var aprobada = await DecidirAsync(http, esc.Usuarios.Analista.Token, "credit-limits", cupo, "Approve", huella);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        var firme = (await CarteraE2E.LeerAsync(aprobada)).GetProperty("resultingSubjectPublicId").GetGuid();
        var lista = await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{Cupos}?personPublicId={h}");
        lista.GetProperty("items").EnumerateArray().Should().Contain(c => c.GetProperty("publicId").GetGuid() == firme
            && c.GetProperty("status").GetString() == "Active" && c.GetProperty("amount").GetDecimal() == 3_000_000m);
    }

    [Fact]
    public async Task El_cupo_de_60_millones_va_al_comite_que_excluye_a_quien_lo_pidio_y_aprueba_con_condiciones()
    {
        var esc = await EscenarioAsync(fx, SinActivar, activada: false);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        await DarRolAsync(http, admin, esc.Usuarios.Comite1, esc.RolDeRadicacion);
        var i = await DeudorAsync(http, admin, "Ignacio", conCentrales: false);

        var pedido = await OtorgarCupoAsync(http, esc.Usuarios.Comite1.Token, i, 60_000_000m, esc.Hoy);
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var cupo = await CarteraE2E.LeerAsync(pedido);
        cupo.GetProperty("status").GetString().Should().Be("PendingDecision");
        cupo.GetProperty("routing").GetProperty("levelCode").GetString().Should().Be("COMITE");
        var id = cupo.GetProperty("publicId").GetGuid();

        // Individual no: el nivel es colegiado y la pantalla ofrece llevarlo a sesión.
        var individual = await DecidirAsync(http, esc.Usuarios.Analista.Token, "credit-limits", id, "Approve", cupo.GetProperty("routing").GetProperty("contentSha256").GetString());
        var nivel = await InventarioE2E.FallaAsync(individual, "Lending.Attribution.LevelRequired");
        nivel.GetProperty("data").GetProperty("governingBody").ValueKind.Should().NotBe(JsonValueKind.Null);

        var (sesion, punto) = await SesionConPuntoAsync(http, esc, esc.Comite, esc.MiembrosDelComite.Values, LendingDecisionSubjects.CreditLimit, id);
        var secretaria = esc.Usuarios.Secretaria.Token;
        await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Put, $"{Sesiones}/{sesion}/items/{punto}", new
        {
            proposedOutcome = "ApproveWithConditions", conditions = JsonSerializer.Serialize(new { amount = 50_000_000m }), notes = "Monto menor por capacidad",
        });

        var excluido = await VotarAsync(http, esc.Usuarios.Comite1.Token, sesion, punto, esc.Usuarios.Comite1.Persona!.Value, "For", "OwnSession");
        var motivo = await InventarioE2E.FallaAsync(excluido, "Lending.Session.VoterExcluded");
        motivo.GetProperty("data").GetProperty("reason").ToString().Should().Contain("Filer", "quien pidió el cupo no vota");
        foreach (var u in new[] { esc.Usuarios.Comite2, esc.Usuarios.Comite3 })
        {
            var voto = await VotarAsync(http, u.Token, sesion, punto, u.Persona!.Value, "For", "OwnSession");
            voto.StatusCode.Should().Be(HttpStatusCode.Created, await voto.Content.ReadAsStringAsync());
        }

        var cerrada = await CerrarAsync(http, secretaria, sesion);
        cerrada.StatusCode.Should().Be(HttpStatusCode.OK, await cerrada.Content.ReadAsStringAsync());
        var acta = await CarteraE2E.LeerAsync(cerrada);
        acta.GetProperty("contentSha256").GetString().Should().MatchRegex("^[0-9a-f]{64}$");
        var minutas = await CarteraE2E.MandarGetAsync(http, secretaria, $"{Sesiones}/{sesion}/minutes.pdf");
        (await minutas.Content.ReadAsByteArrayAsync())[..4].Should().Equal("%PDF"u8.ToArray());

        var vigentes = await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{Cupos}?personPublicId={i}");
        vigentes.GetProperty("items").EnumerateArray().Should().Contain(c => c.GetProperty("status").GetString() == "Active"
            && c.GetProperty("amount").GetDecimal() == 50_000_000m, "el acta aprueba con el monto menor de sus condiciones");
    }

    [Fact]
    public async Task El_cupo_de_un_vinculado_va_al_consejo_y_el_interesado_no_vota()
    {
        var esc = await EscenarioAsync(fx, SinActivar, activada: false);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        esc.Consejeros.Should().HaveCount(5);
        var d = esc.Consejeros[3].Persona;
        await AutorizarAsync(http, admin, d, "CreditProcessing");
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/related-parties", new { personPublicId = d, basis = "BoardMember", validFrom = esc.Hoy });

        var pedido = await OtorgarCupoAsync(http, esc.Usuarios.Jefe.Token, d, 500_000m, esc.Hoy);
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var cupo = await CarteraE2E.LeerAsync(pedido);
        cupo.GetProperty("status").GetString().Should().Be("PendingDecision", "un vinculado va al consejo aunque el monto quepa");
        cupo.GetProperty("routing").GetProperty("levelCode").GetString().Should().Be("CONSEJO");
        cupo.GetProperty("routing").GetProperty("relatedParty").GetBoolean().Should().BeTrue();
        var id = cupo.GetProperty("publicId").GetGuid();

        var (sesion, punto) = await SesionConPuntoAsync(http, esc, esc.Consejo, esc.Consejeros.Select(c => c.Miembro), LendingDecisionSubjects.CreditLimit, id);
        var secretaria = esc.Usuarios.Secretaria.Token;
        var interesado = await VotarAsync(http, secretaria, sesion, punto, d, "For", "RecordedBySecretary");
        (await InventarioE2E.FallaAsync(interesado, "Lending.Session.VoterExcluded")).GetProperty("data").GetProperty("reason").ToString()
            .Should().Contain("Applicant", "el consejero D es el titular del cupo: no vota");
        foreach (var (persona, _) in esc.Consejeros.Where(c => c.Persona != d))
        {
            var voto = await VotarAsync(http, secretaria, sesion, punto, persona, "For", "RecordedBySecretary");
            voto.StatusCode.Should().Be(HttpStatusCode.Created, await voto.Content.ReadAsStringAsync());
        }
        var item = (await CarteraE2E.GetAsync(http, secretaria, $"{Sesiones}/{sesion}")).GetProperty("items").EnumerateArray().Single();
        item.GetProperty("tally").GetProperty("composition").GetInt32().Should().Be(5, "la base es la composición, aunque el interesado no vote");

        var cerrada = await CerrarAsync(http, secretaria, sesion);
        cerrada.StatusCode.Should().Be(HttpStatusCode.OK, await cerrada.Content.ReadAsStringAsync());
        (await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{Cupos}?personPublicId={d}")).GetProperty("items").EnumerateArray()
            .Should().Contain(c => c.GetProperty("status").GetString() == "Active", "cuatro de cinco favorables aprueban (4/5 de la composición)");
    }

    [Fact]
    public async Task La_coincidencia_en_listas_bloquea_el_cupo_y_solo_el_oficial_la_libera()
    {
        var esc = await EscenarioAsync(fx, SinActivar, activada: false);
        using var http = fx.CreateClient();
        await AutorizarAsync(http, esc.Coop.TokenAdmin, esc.PersonaEnLista, "CreditProcessing");

        var pedido = await OtorgarCupoAsync(http, esc.Usuarios.Jefe.Token, esc.PersonaEnLista, 500_000m, esc.Hoy);
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var cupo = await CarteraE2E.LeerAsync(pedido);
        var aviso = cupo.GetProperty("warnings").EnumerateArray().Single(w => w.GetProperty("code").GetString() == "Lending.Screening.Blocked");
        var resultado = aviso.GetProperty("screeningResultPublicId").GetGuid();
        (await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{Cupos}?personPublicId={esc.PersonaEnLista}")).GetProperty("items")
            .EnumerateArray().Should().Contain(c => c.GetProperty("status").GetString() == "Blocked");

        var asesor = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.AsesorO1.Token, HttpMethod.Post, $"{Listas}/{resultado}/release", new { reason = "Homónimo" });
        asesor.StatusCode.Should().Be(HttpStatusCode.NotFound, "sólo el oficial libera (nunca por atribución)");

        var oficial = esc.Usuarios.Oficial.Token;
        (await CarteraE2E.GetAsync(http, oficial, $"{Listas}?status=Blocked")).GetProperty("items").EnumerateArray()
            .Should().Contain(r => r.GetProperty("publicId").GetGuid() == resultado);
        var liberada = await CarteraE2E.ConClaveAsync(http, oficial, HttpMethod.Post, $"{Listas}/{resultado}/release",
            new { reason = "Homónimo verificado con el documento" });
        liberada.StatusCode.Should().Be(HttpStatusCode.OK, await liberada.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(liberada)).GetProperty("status").GetString().Should().Be("Released");
    }

    [Fact]
    public async Task La_concentracion_excedida_bloquea_con_limite_y_exceso_y_sin_patrimonio_tecnico_alerta()
    {
        var esc = await EscenarioAsync(fx, SinActivar, activada: false);
        using var http = fx.CreateClient();
        var j = await DeudorAsync(http, esc.Coop.TokenAdmin, "Julian", conCentrales: false);

        var excedida = await OtorgarCupoAsync(http, esc.Usuarios.Jefe.Token, j, 150_000_000m, esc.Hoy);
        var datos = (await InventarioE2E.FallaAsync(excedida, "Lending.Concentration.LimitExceeded")).GetProperty("data");
        datos.GetProperty("limit").GetDecimal().Should().Be(100_000_000m, "10 % de un patrimonio técnico de 1.000 millones");
        datos.GetProperty("excess").GetDecimal().Should().Be(50_000_000m);

        var antes = await AlertasAsync(esc, "Cartera.PatrimonioTecnicoNoDisponible");
        fx.Dobles.SinPatrimonioTecnico(esc.Coop.TenantPublicId);
        try
        {
            var sinBase = await OtorgarCupoAsync(http, esc.Usuarios.Jefe.Token, j, 1_000_000m, esc.Hoy);
            await InventarioE2E.FallaAsync(sinBase, "Lending.Concentration.TechnicalCapitalUnavailable");
            (await AlertasAsync(esc, "Cartera.PatrimonioTecnicoNoDisponible")).Should().BeGreaterThan(antes, "se alerta al contador aunque el cupo no se otorgue");
        }
        finally
        {
            fx.Dobles.PatrimonioTecnico(esc.Coop.TenantPublicId, PatrimonioTecnico, esc.Hoy.AddDays(-1));
        }
    }

    // --------------------------------------------------------------------------------------------- activada --

    [Fact]
    public async Task Sin_el_documento_obligatorio_no_se_radica_y_se_lista_lo_que_falta()
    {
        var esc = await EscenarioAsync(fx, Activada, activada: true);
        using var http = fx.CreateClient();
        var persona = await DeudorAsync(http, esc.Coop.TokenAdmin, "Documentada", conCentrales: false);
        var asesor = esc.Usuarios.AsesorO1.Token;

        var borrador = await CarteraE2E.ExitoAsync(http, asesor, HttpMethod.Post, Solicitudes, Solicitud(esc, persona, 3_000_000m, esc.ConDocumento));
        borrador.GetProperty("number").ValueKind.Should().Be(JsonValueKind.Null, "el borrador no consume número");
        var id = borrador.GetProperty("publicId").GetGuid();
        var radicar = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, $"{Solicitudes}/{id}/submit", new { rowVersion = borrador.GetProperty("rowVersion").GetString() });
        var faltan = (await InventarioE2E.FallaAsync(radicar, "Lending.Application.DocumentsMissing")).GetProperty("data").GetProperty("missing");
        faltan.EnumerateArray().Select(m => m.GetProperty("code").GetString()).Should().Contain("CEDULA");
        (await CarteraE2E.GetAsync(http, asesor, $"{Solicitudes}/{id}")).GetProperty("status").GetInt32().Should().Be(1, "sigue en borrador");
    }

    [Fact]
    public async Task Tres_solicitudes_van_al_analista_a_la_gerencia_y_al_comite()
    {
        var esc = await EscenarioAsync(fx, Activada, activada: true);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var asesor = esc.Usuarios.AsesorO1.Token;

        var tres = await RadicarAsync(http, asesor, Solicitud(esc, await DeudorAsync(http, admin, "Tres", false), 3_000_000m));
        var quince = await RadicarAsync(http, asesor, Solicitud(esc, await DeudorAsync(http, admin, "Quince", false), 15_000_000m));
        var sesenta = await RadicarAsync(http, asesor, Solicitud(esc, await DeudorAsync(http, admin, "Sesenta", false), 60_000_000m));
        tres.GetProperty("number").GetString().Should().StartWith("SOL-");
        tres.GetProperty("status").GetInt32().Should().Be(3, "PendingDecision");

        var (nivel3, huella3) = await EnrutamientoAsync(http, asesor, tres.GetProperty("publicId").GetGuid());
        var (nivel15, huella15) = await EnrutamientoAsync(http, asesor, quince.GetProperty("publicId").GetGuid());
        var (nivel60, huella60) = await EnrutamientoAsync(http, asesor, sesenta.GetProperty("publicId").GetGuid());
        (nivel3, nivel15, nivel60).Should().Be(("ANALISTA", "GERENCIA", "COMITE"));

        var analista = esc.Usuarios.Analista.Token;
        var pendientes = await CarteraE2E.GetAsync(http, analista, $"{Pendientes}?subject=CreditApplication");
        pendientes.GetProperty("items").EnumerateArray().Should().Contain(p => p.GetProperty("subjectPublicId").GetGuid() == tres.GetProperty("publicId").GetGuid()
            && p.GetProperty("canDecide").GetBoolean());

        var aprobada = await DecidirAsync(http, analista, "applications", tres.GetProperty("publicId").GetGuid(), "Approve", huella3);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        var decision = await CarteraE2E.LeerAsync(aprobada);
        decision.GetProperty("subjectStatus").GetString().Should().Be("Approved");
        decision.GetProperty("approvalExpiresAt").ValueKind.Should().Be(JsonValueKind.String, "la aprobación vence (FR-042)");

        var menor = await DecidirAsync(http, analista, "applications", quince.GetProperty("publicId").GetGuid(), "Approve", huella15);
        (await InventarioE2E.FallaAsync(menor, "Lending.Attribution.LevelRequired")).GetProperty("data").GetProperty("governingBody")
            .ValueKind.Should().Be(JsonValueKind.Null, "el analista tiene un nivel individual menor");
        var gerencia = await DecidirAsync(http, esc.Usuarios.Gerente.Token, "applications", quince.GetProperty("publicId").GetGuid(), "Approve", huella15);
        gerencia.StatusCode.Should().Be(HttpStatusCode.Created, await gerencia.Content.ReadAsStringAsync());

        var colegiado = await DecidirAsync(http, esc.Usuarios.Gerente.Token, "applications", sesenta.GetProperty("publicId").GetGuid(), "Approve", huella60);
        (await InventarioE2E.FallaAsync(colegiado, "Lending.Attribution.LevelRequired")).GetProperty("data").GetProperty("governingBody")
            .ValueKind.Should().NotBe(JsonValueKind.Null, "el comité decide en sesión");
    }

    [Fact]
    public async Task Si_cambia_el_consolidado_la_solicitud_se_reenruta_y_la_huella_vieja_no_decide()
    {
        var esc = await EscenarioAsync(fx, Activada, activada: true);
        using var http = fx.CreateClient();
        var k = await DeudorAsync(http, esc.Coop.TokenAdmin, "Karina", conCentrales: false);
        var asesor = esc.Usuarios.AsesorO1.Token;

        var pequena = await RadicarAsync(http, asesor, Solicitud(esc, k, 3_000_000m));
        var id = pequena.GetProperty("publicId").GetGuid();
        var (nivel, huella) = await EnrutamientoAsync(http, asesor, id);
        nivel.Should().Be("ANALISTA");

        var grande = await RadicarAsync(http, asesor, Solicitud(esc, k, 15_000_000m));
        var (_, huellaGrande) = await EnrutamientoAsync(http, asesor, grande.GetProperty("publicId").GetGuid());
        (await DecidirAsync(http, esc.Usuarios.Gerente.Token, "applications", grande.GetProperty("publicId").GetGuid(), "Approve", huellaGrande))
            .StatusCode.Should().Be(HttpStatusCode.Created, "la aprobada suma al consolidado de la persona");

        // Quien tiene el nivel nuevo decide con la huella que vio antes: el contenido cambió y la solicitud se reenruta.
        var vieja = await DecidirAsync(http, esc.Usuarios.Gerente.Token, "applications", id, "Approve", huella);
        var cambio = await InventarioE2E.FallaAsync(vieja, "Lending.Decision.ContentChanged");
        var actual = cambio.GetProperty("data").GetProperty("currentSha256").GetString();
        actual.Should().NotBe(huella);
        var (nuevo, huellaNueva) = await EnrutamientoAsync(http, asesor, id);
        nuevo.Should().Be("GERENCIA", "3 + 15 millones de consolidado ya no caben en el analista");
        huellaNueva.Should().Be(actual);
    }

    [Fact]
    public async Task Quien_radico_no_decide_y_la_coincidencia_en_listas_no_se_exceptua()
    {
        var esc = await EscenarioAsync(fx, Activada, activada: true);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        await DarRolAsync(http, admin, esc.Usuarios.Gerente, esc.RolDeRadicacion);
        var gerente = esc.Usuarios.Gerente.Token;

        var propia = await RadicarAsync(http, gerente, Solicitud(esc, await DeudorAsync(http, admin, "Lorena", false), 15_000_000m));
        var (_, huella) = await EnrutamientoAsync(http, gerente, propia.GetProperty("publicId").GetGuid());
        var conflicto = await DecidirAsync(http, gerente, "applications", propia.GetProperty("publicId").GetGuid(), "Approve", huella);
        await InventarioE2E.FallaAsync(conflicto, "Lending.Segregation.Conflict");

        await AutorizarAsync(http, admin, esc.PersonaEnLista, "CreditProcessing");
        var enLista = await RadicarAsync(http, esc.Usuarios.AsesorO1.Token, Solicitud(esc, esc.PersonaEnLista, 3_000_000m));
        enLista.GetProperty("blockedByRestrictiveLists").GetBoolean().Should().BeTrue("la coincidencia bloquea hasta que el oficial libere");
        var (_, huellaLista) = await EnrutamientoAsync(http, gerente, enLista.GetProperty("publicId").GetGuid());
        var negada = await DecidirAsync(http, esc.Usuarios.Analista.Token, "applications", enLista.GetProperty("publicId").GetGuid(), "Approve", huellaLista);
        await InventarioE2E.FallaAsync(negada, "Lending.Attribution.NotExceptionable");
    }

    // ----------------------------------------------------------------------------------------------- apoyo --

    private async Task<int> AlertasAsync(Escenario esc, string tipo)
    {
        var inv = new InventarioE2E.CooperativaAislada { TokenAdmin = esc.Coop.TokenAdmin, TenantPublicId = esc.Coop.TenantPublicId, SucursalPrincipal = esc.Coop.SucursalPrincipal };
        return Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, inv,
            $"""SELECT COUNT(*) FROM dbo."COR_Alerts" WHERE "TypeCode" = '{tipo}'""",
            $"SELECT COUNT(*) FROM [dbo].[COR_Alerts] WHERE [TypeCode] = N'{tipo}'"));
    }
}
