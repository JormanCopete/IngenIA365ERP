using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0268 (feature 013, US17; quickstart §3.14 y el Independent Test de la fase 4; colección «Cartera e2e», los dos motores por
/// <c>DB_PROVIDER</c>, cooperativa aislada «seguridad» con perfil regulatorio de actividad financiera): la parte de K1 que no necesita
/// créditos.
/// <list type="bullet">
/// <item>T0238 (pendiente de la fase 3): el administrador ve en <c>/api/admin/permissions/mine</c> el catálogo <c>Lending.*</c> menos lo que
/// se le excluye (<c>Lending.Decisions.*</c>, <c>Lending.Sessions.Vote</c>; C86) y <c>/api/admin/roles/templates?module=Lending</c> devuelve
/// las trece plantillas.</item>
/// <item>US17-1 (sin desembolso): <c>lectura</c> recibe 404 en las escrituras de gobierno y queda <c>Lending.Access.Denied</c>.</item>
/// <item>US17-2: <c>revisor</c> lee matrices, órganos, actas y vinculados —la lectura del acta deja <c>Lending.Session.Viewed</c>— y toda
/// escritura le responde 404 (el voto, que se abre con <c>Sessions.Read</c>, 422 <c>Lending.Permission.Required</c>, api.md §1.4).</item>
/// <item>F9: un comando con un campo inválido queda <c>Lending.Request.Rejected</c> con campos y códigos, sin valores.</item>
/// <item>Órganos y actas: Comité de crédito con tres principales y Consejo con cinco principales y dos suplentes; sin quórum no se cierra;
/// un no miembro no vota; <c>secretaria</c> registra votos de otros y <c>comite.2</c> sólo el propio; voto de reemplazo; el PDF de una sesión
/// abierta es el borrador; la fracción de vinculados 3/4 en una cooperativa con actividad financiera → <c>Lending.Parameter.BelowLegalFloor</c>.</item>
/// <item>US17-3, SC-014: un evento <c>Lending.*</c> alterado y otro borrado en el Mongo del contenedor salen <c>Altered</c> y <c>Deleted</c>.</item>
/// <item>US1 (T0343): el alcance por línea. <c>asesor.o1</c> opera <c>CONLIB</c>, <c>CONCAJA</c> y <c>ROTATIVO</c>: la lista no le muestra
/// <c>VARIBR</c> y por URL recibe el 404 genérico, con <c>Lending.Access.OutOfScope</c> en la auditoría.</item>
/// </list>
/// Lo que necesita créditos (US17-1 por desembolso, US17-4 en el archivo de centrales, US17-5 con una obligación viva) lo suman a esta clase
/// las fases de US2, US3, US16 y del contrato de obligaciones, nunca con <c>Skip</c>. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class SeguridadYHabeasDataDeCarteraTests(CentralIdentityApiFixture fx)
{
    private const string Organos = "/api/lending/governing-bodies";
    private const string Sesiones = "/api/lending/sessions";
    private const string Matrices = "/api/lending/attribution-matrices";
    private const string Vinculados = "/api/lending/related-parties";
    private const string Verificar = "/api/audit/integrity/verify";
    private const string BaseDeAuditoria = "IngenIA365ERP_Audit_Test";

    // -------------------------------------------------------------------------------------- permisos (T0238) --

    [Fact]
    public async Task El_administrador_ve_los_permisos_de_Cartera_salvo_decidir_y_votar_y_las_trece_plantillas()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;

        var catalogo = (await CarteraE2E.GetAsync(http, admin, "/api/admin/permissions")).EnumerateArray()
            .Select(p => p.GetProperty("code").GetString()!).Where(c => c.StartsWith("Lending.", StringComparison.Ordinal)).ToList();
        catalogo.Should().HaveCount(CatalogoDeCartera, "LendingPermissionCatalogSeeder siembra los 123 Lending.* enteros desde K1");

        var mios = await CarteraE2E.GetAsync(http, admin, "/api/admin/permissions/mine");
        var deCartera = mios.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!)
            .Where(c => c.StartsWith("Lending.", StringComparison.Ordinal)).ToList();
        var excluidos = catalogo.Where(c => c.StartsWith("Lending.Decisions.", StringComparison.Ordinal) || c == "Lending.Sessions.Vote").ToList();
        excluidos.Should().HaveCount(5, "cuatro niveles de Lending.Decisions.* y el voto propio");
        deCartera.Should().BeEquivalentTo(catalogo.Except(excluidos),
            "CompanyAdmin recibe todo Lending.* menos lo que administrar no incluye: decidir crédito y votar (C86)");
        deCartera.Should().Contain(["Lending.Sessions.RecordVotes", "Lending.Sessions.Close", "Lending.Scope.AllOffices"]);

        var plantillas = (await CarteraE2E.GetAsync(http, admin, "/api/admin/roles/templates?module=Lending")).EnumerateArray()
            .Select(p => p.GetProperty("key").GetString()!).ToList();
        plantillas.Should().BeEquivalentTo(UsuariosDeCartera.PlantillasDeCartera);

        // Los roles sugeridos ya se crearon (UsuariosDeCartera): repetir no crea nada y los informa como existentes.
        var otraVez = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/activation/suggested-roles", new { });
        otraVez.GetProperty("created").GetArrayLength().Should().Be(0);
        otraVez.GetProperty("alreadyExisting").EnumerateArray().Select(k => k.GetString()).Should().BeEquivalentTo(UsuariosDeCartera.PlantillasDeCartera);
    }

    /// <summary>El tamaño del catálogo <c>Lending.*</c> de <c>LendingPermissionCatalogSeeder</c> (contracts/api.md §2.1).</summary>
    private const int CatalogoDeCartera = 123;

    // -------------------------------------------------------------------------------------- US17-1 y US17-2 --

    [Fact]
    public async Task Lectura_recibe_404_en_las_escrituras_de_gobierno_y_queda_Lending_Access_Denied()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var desde = DateTime.UtcNow.AddMinutes(-1);
        var lectura = esc.Usuarios.Lectura.Token;

        foreach (var (ruta, cuerpo) in EscriturasDeGobierno(esc))
        {
            var resp = await CarteraE2E.ConClaveAsync(http, lectura, HttpMethod.Post, ruta, cuerpo);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta}: «{await resp.Content.ReadAsStringAsync()}»");
            (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("Generic.NotFound");
        }

        var denegados = (await EventosAsync(http, esc, desde)).Where(e => Texto(e, "action") == "Lending.Access.Denied").ToList();
        denegados.Count(e => Texto(e, "userName")?.Contains("lectura", StringComparison.OrdinalIgnoreCase) == true)
            .Should().BeGreaterThanOrEqualTo(4, "cada intento sin permiso queda auditado antes del 404");
        denegados.Should().OnlyContain(e => Texto(e, "module") == "Lending");
    }

    [Fact]
    public async Task Revisor_lee_matrices_organos_actas_y_vinculados_y_ninguna_escritura_le_responde()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var desde = DateTime.UtcNow.AddMinutes(-1);
        var revisor = esc.Usuarios.Revisor.Token;

        (await CarteraE2E.MandarGetAsync(http, revisor, Matrices)).StatusCode.Should().Be(HttpStatusCode.OK);
        var organos = await CarteraE2E.GetAsync(http, revisor, Organos);
        organos.EnumerateArray().Select(o => o.GetProperty("code").GetString()).Should().Contain(["COMITE", "CONSEJO"]);
        (await CarteraE2E.GetAsync(http, revisor, $"{Organos}/{esc.Consejo}")).GetProperty("memberList").GetArrayLength().Should().Be(7);
        (await CarteraE2E.GetAsync(http, revisor, Sesiones)).ValueKind.Should().NotBe(JsonValueKind.Undefined);
        var sesion = await CarteraE2E.GetAsync(http, revisor, $"{Sesiones}/{esc.SesionDeLectura}");
        sesion.GetProperty("publicId").GetGuid().Should().Be(esc.SesionDeLectura);
        var vinculados = await CarteraE2E.GetAsync(http, revisor, Vinculados);
        vinculados.ToString().Should().Contain(esc.ConsejeroD.ToString(), "el consejero D y su cónyuge son vinculados");

        foreach (var (ruta, cuerpo) in EscriturasDeGobierno(esc))
        {
            var resp = await CarteraE2E.ConClaveAsync(http, revisor, HttpMethod.Post, ruta, cuerpo);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta}: «{await resp.Content.ReadAsStringAsync()}»");
        }
        var cerrar = await CarteraE2E.ConClaveAsync(http, revisor, HttpMethod.Post, $"{Sesiones}/{esc.SesionDeLectura}/close",
            new { expectedContentSha256 = new string('0', 64) });
        cerrar.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // El voto se abre con Sessions.Read y el handler exige RecordVotes o Vote (api.md §1.4): 422 con lo que falta, no 404.
        var voto = await CarteraE2E.ConClaveAsync(http, revisor, HttpMethod.Post, $"{Sesiones}/{esc.SesionDeLectura}/items/{esc.PuntoDeLectura}/votes", new
        {
            personPublicId = esc.Usuarios.Comite1.Persona, vote = "For", method = "RecordedBySecretary",
        });
        voto.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await voto.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(voto)).Should().Be("Lending.Permission.Required");

        var eventos = await EventosAsync(http, esc, desde);
        eventos.Should().Contain(e => Texto(e, "action") == AuditEventTypes.LendingSessionViewed
                                      && Texto(e, "userName")!.Contains("revisor", StringComparison.OrdinalIgnoreCase),
            "la lectura de un acta es una lectura sensible auditada");
        eventos.Should().Contain(e => Texto(e, "action") == AuditEventTypes.LendingRelatedPartyViewed, "la consulta de vinculados también");
    }

    // ------------------------------------------------------------------------------------------------- F9 --

    [Fact]
    public async Task Un_campo_invalido_queda_Lending_Request_Rejected_con_campos_y_codigos_sin_valores()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var desde = DateTime.UtcNow.AddMinutes(-1);
        const string Secreto = "NOMBRE SECRETO 7731";

        var resp = await CarteraE2E.ConClaveAsync(http, esc.Coop.TokenAdmin, HttpMethod.Post, Organos, new
        {
            code = "MAL CODIGO 7731", name = Secreto, kind = "RiskCommittee", decidesCredit = false, minutesPrefix = "CR-",
            rules = ReglasDeOrgano(esc.Hoy, null),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, await resp.Content.ReadAsStringAsync());

        var rechazo = (await EventosAsync(http, esc, desde)).Where(e => Texto(e, "action") == "Lending.Request.Rejected").ToList();
        rechazo.Should().NotBeEmpty("ValidationBehavior lo anota y el grupo de Cartera lo registra");
        var valores = Texto(rechazo[^1], "newValuesJson") ?? string.Empty;
        valores.Should().Contain("CreateGoverningBodyCommand").And.Contain("Code");
        valores.Should().NotContain("MAL CODIGO 7731").And.NotContain(Secreto, "la auditoría lleva campos y códigos, nunca los valores");
    }

    // ------------------------------------------------------------------------------------ órganos y actas --

    [Fact]
    public async Task Organos_sesiones_y_votos_respetan_quorum_miembros_permisos_y_reemplazos()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var admin = esc.Coop.TokenAdmin;

        // El comité con tres principales; el consejo con cinco principales y dos suplentes (quickstart §2.7).
        var comite = await CarteraE2E.GetAsync(http, admin, $"{Organos}/{esc.Comite}");
        comite.GetProperty("memberList").EnumerateArray().Count(m => m.GetProperty("isPrincipal").GetBoolean()).Should().Be(3);
        var consejo = await CarteraE2E.GetAsync(http, admin, $"{Organos}/{esc.Consejo}");
        consejo.GetProperty("memberList").EnumerateArray().Count(m => m.GetProperty("isPrincipal").GetBoolean()).Should().Be(5);
        consejo.GetProperty("memberList").EnumerateArray().Count(m => !m.GetProperty("isPrincipal").GetBoolean()).Should().Be(2);

        // Fracción de vinculados 3/4 con actividad financiera: menos que el límite nacional de 4/5.
        var tresCuartos = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Organos}/{esc.Consejo}/rules",
            ReglasDeOrgano(esc.Hoy.AddDays(1), (3, 4)));
        tresCuartos.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await tresCuartos.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(tresCuartos)).Should().Be("Lending.Parameter.BelowLegalFloor");
        (await CarteraE2E.LeerAsync(tresCuartos)).GetProperty("data").GetProperty("limitCode").GetString().Should().Be("VINCULADOS_FRACCION_CALIFICADA");

        // Sin quórum (un presente de tres) no se cierra.
        var sinQuorum = await SesionDelComiteAsync(http, esc, presentes: [u.Comite1]);
        var cerrar = await CerrarAsync(http, u.Secretaria.Token, sinQuorum.Sesion);
        cerrar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await cerrar.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(cerrar)).Should().Be("Lending.Session.QuorumNotMet");
        var datos = (await CarteraE2E.LeerAsync(cerrar)).GetProperty("data");
        datos.GetProperty("required").GetInt32().Should().Be(2);
        datos.GetProperty("present").GetInt32().Should().Be(1);

        // Con los tres presentes.
        var s = await SesionDelComiteAsync(http, esc, presentes: [u.Comite1, u.Comite2, u.Comite3]);
        var votos = $"{Sesiones}/{s.Sesion}/items/{s.Punto}/votes";

        // Un no miembro no vota (el gerente no es del comité).
        var ajeno = await CarteraE2E.ConClaveAsync(http, u.Secretaria.Token, HttpMethod.Post, votos, Voto(u.Gerente.Persona!.Value, "For", "RecordedBySecretary"));
        ajeno.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await ajeno.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(ajeno)).Should().Be("Lending.Session.VoterExcluded");
        (await CarteraE2E.LeerAsync(ajeno)).GetProperty("data").GetProperty("reason").GetString().Should().Be("NotMember");

        // comite.2 vota lo propio desde su sesión; el voto de otro no lo puede registrar.
        (await CarteraE2E.ConClaveAsync(http, u.Comite2.Token, HttpMethod.Post, votos, Voto(u.Comite2.Persona!.Value, "For", "OwnSession")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        foreach (var metodo in new[] { "OwnSession", "RecordedBySecretary" })
        {
            var otro = await CarteraE2E.ConClaveAsync(http, u.Comite2.Token, HttpMethod.Post, votos, Voto(u.Comite3.Persona!.Value, "For", metodo));
            otro.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await otro.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(otro)).Should().Be("Lending.Permission.Required");
            (await CarteraE2E.LeerAsync(otro)).GetProperty("data").GetProperty("permissionCodes").EnumerateArray().Select(p => p.GetString())
                .Should().Contain("Lending.Sessions.RecordVotes");
        }

        // La secretaría registra el de comite.1; un segundo voto sin motivo no entra; con motivo lo reemplaza y cuenta el vigente.
        var primero = await CarteraE2E.ConClaveAsync(http, u.Secretaria.Token, HttpMethod.Post, votos, Voto(u.Comite1.Persona!.Value, "Against", "RecordedBySecretary"));
        primero.StatusCode.Should().Be(HttpStatusCode.Created, await primero.Content.ReadAsStringAsync());
        var primeroId = (await CarteraE2E.LeerAsync(primero)).GetProperty("votePublicId").GetGuid();
        (await CarteraE2E.ConClaveAsync(http, u.Secretaria.Token, HttpMethod.Post, votos, Voto(u.Comite1.Persona!.Value, "For", "RecordedBySecretary")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "ya votó: el reemplazo exige motivo");
        var reemplazo = await CarteraE2E.ConClaveAsync(http, u.Secretaria.Token, HttpMethod.Post, votos,
            Voto(u.Comite1.Persona!.Value, "For", "RecordedBySecretary", "La secretaría anotó el voto equivocado"));
        reemplazo.StatusCode.Should().Be(HttpStatusCode.Created, await reemplazo.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(reemplazo)).GetProperty("replacesVotePublicId").GetGuid().Should().Be(primeroId);

        var abierta = await CarteraE2E.GetAsync(http, u.Secretaria.Token, $"{Sesiones}/{s.Sesion}");
        var punto = abierta.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("itemPublicId").GetGuid() == s.Punto);
        punto.GetProperty("votes").EnumerateArray().Single(v => v.GetProperty("votePublicId").GetGuid() == primeroId)
            .GetProperty("replacedAt").ValueKind.Should().NotBe(JsonValueKind.Null, "el voto reemplazado queda con su marca, no se borra");
        punto.GetProperty("tally").GetProperty("for").GetInt32().Should().Be(2, "cuenta el voto vigente de comite.1 y el de comite.2");
        punto.GetProperty("tally").GetProperty("against").GetInt32().Should().Be(0);

        // El PDF de una sesión abierta es el borrador sin número («SIN CERRAR»).
        var pdf = await CarteraE2E.MandarGetAsync(http, u.Secretaria.Token, $"{Sesiones}/{s.Sesion}/minutes.pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        (pdf.Content.Headers.ContentDisposition?.FileNameStar ?? pdf.Content.Headers.ContentDisposition?.FileName)
            .Should().Contain("acta-borrador-", "sin cerrar no tiene número: es el borrador marcado «SIN CERRAR»");

        // Con quórum y mayoría se cierra y queda numerada.
        var cerrada = await CerrarAsync(http, u.Secretaria.Token, s.Sesion);
        cerrada.StatusCode.Should().Be(HttpStatusCode.OK, await cerrada.Content.ReadAsStringAsync());
        var acta = await CarteraE2E.LeerAsync(cerrada);
        acta.GetProperty("status").GetInt32().Should().Be((int)SessionStatus.Closed);
        acta.GetProperty("number").GetString().Should().StartWith("CC-");
        acta.GetProperty("items").EnumerateArray().Single().GetProperty("outcome").GetInt32().Should().Be((int)CreditDecisionOutcome.Approve);
    }

    // ---------------------------------------------------------------------------------- alcance por línea (US1) --

    [Fact]
    public async Task Asesor_o1_no_ve_una_linea_fuera_de_su_alcance_y_por_URL_recibe_el_404_generico()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var lineas = await LineasE2E.LineasDelEnsayoAsync(fx, esc.Coop);
        var asesor = esc.Usuarios.AsesorO1;
        await UsuariosDeCartera.FijarLineasAsync(http, esc.Coop.TokenAdmin, asesor, lineas["CONLIB"], lineas["CONCAJA"], lineas["ROTATIVO"]);
        var desde = DateTime.UtcNow.AddMinutes(-1);

        var vistas = (await CarteraE2E.GetAsync(http, asesor.Token, LineasE2E.Lineas)).EnumerateArray()
            .Select(l => l.GetProperty("code").GetString()).ToList();
        vistas.Should().BeEquivalentTo(["CONLIB", "CONCAJA", "ROTATIVO"], "sólo las líneas de su alcance");
        (await CarteraE2E.GetAsync(http, asesor.Token, $"{LineasE2E.Lineas}/{lineas["CONLIB"]}")).GetProperty("code").GetString().Should().Be("CONLIB");

        var fuera = await CarteraE2E.MandarGetAsync(http, asesor.Token, $"{LineasE2E.Lineas}/{lineas["VARIBR"]}");
        fuera.StatusCode.Should().Be(HttpStatusCode.NotFound, await fuera.Content.ReadAsStringAsync());
        var inexistente = await CarteraE2E.MandarGetAsync(http, asesor.Token, $"{LineasE2E.Lineas}/{Guid.NewGuid()}");
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CarteraE2E.CodigoDeErrorAsync(fuera)).Should().Be(await CarteraE2E.CodigoDeErrorAsync(inexistente),
            "fuera del alcance es el mismo 404 que una línea que no existe (Lending.CreditLine.NotFound, api.md §3.1)");
        var usura = await CarteraE2E.EnviarAsync(http, asesor.Token, HttpMethod.Post, $"{LineasE2E.Lineas}/{lineas["VARIBR"]}/usury-check", new { });
        usura.StatusCode.Should().Be(HttpStatusCode.NotFound, "la consulta de usura tampoco la revela");

        var eventos = await EventosAsync(http, esc, desde);
        eventos.Should().Contain(e => Texto(e, "action") == "Lending.Access.OutOfScope"
                                      && Texto(e, "userName")!.Contains("asesor.o1", StringComparison.OrdinalIgnoreCase),
            "el intento fuera de alcance queda auditado aunque responda 404");

        // El administrador las ve todas.
        (await CarteraE2E.GetAsync(http, esc.Coop.TokenAdmin, LineasE2E.Lineas)).EnumerateArray().Select(l => l.GetProperty("code").GetString())
            .Should().Contain(["CONLIB", "CONCAJA", "ROTATIVO", "VARIBR"]);
    }

    /// <summary>
    /// US2 (bloque 6C, T0384; US17 Independent Test): en la cooperativa activada de la originación, <c>asesor.o2</c> abre por URL una solicitud
    /// de la oficina O1 y recibe el mismo 404 que una inexistente, con <c>Lending.Access.OutOfScope</c> en la auditoría; y el analista que
    /// decide por encima de su atribución recibe <c>Lending.Attribution.LevelRequired</c>.
    /// </summary>
    [Fact]
    public async Task Asesor_o2_no_ve_una_solicitud_de_O1_y_el_analista_no_decide_sobre_su_atribucion()
    {
        var esc = await OriginacionE2E.EscenarioAsync(fx, "originacion_activa", activada: true);
        using var http = fx.CreateClient();
        var desde = DateTime.UtcNow.AddMinutes(-1);
        var persona = await OriginacionE2E.DeudorAsync(http, esc.Coop.TokenAdmin, "Oficina", conCentrales: false);
        var radicada = await OriginacionE2E.RadicarAsync(http, esc.Usuarios.AsesorO1.Token, OriginacionE2E.Solicitud(esc, persona, 15_000_000m));
        var id = radicada.GetProperty("publicId").GetGuid();

        var ajena = await CarteraE2E.MandarGetAsync(http, esc.Usuarios.AsesorO2.Token, $"{OriginacionE2E.Solicitudes}/{id}");
        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CarteraE2E.CodigoDeErrorAsync(ajena)).Should().Be("Lending.Application.NotFound", "fuera de alcance es lo mismo que inexistente");
        var inexistente = await CarteraE2E.MandarGetAsync(http, esc.Usuarios.AsesorO2.Token, $"{OriginacionE2E.Solicitudes}/{Guid.NewGuid()}");
        (await CarteraE2E.CodigoDeErrorAsync(inexistente)).Should().Be("Lending.Application.NotFound");
        (await CarteraE2E.GetAsync(http, esc.Usuarios.AsesorO2.Token, OriginacionE2E.Solicitudes)).GetProperty("items").EnumerateArray()
            .Should().NotContain(s => s.GetProperty("publicId").GetGuid() == id, "las listas filtran en silencio");

        var (_, huella) = await OriginacionE2E.EnrutamientoAsync(http, esc.Usuarios.AsesorO1.Token, id);
        var encima = await OriginacionE2E.DecidirAsync(http, esc.Usuarios.Analista.Token, "applications", id, "Approve", huella);
        encima.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await encima.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(encima)).Should().Be("Lending.Attribution.LevelRequired");

        var eventos = await EventosAsync(http, esc.Coop, desde);
        eventos.Should().Contain(e => Texto(e, "action") == "Lending.Access.OutOfScope"
                                      && Texto(e, "userName")!.Contains("asesor.o2", StringComparison.OrdinalIgnoreCase),
            "el intento fuera de alcance queda auditado aunque responda 404");
    }

    // ------------------------------------------------------------------------------------- integridad --

    [Fact]
    public async Task Un_evento_de_Cartera_alterado_y_otro_borrado_en_Mongo_salen_en_la_verificacion()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var tenant = esc.Coop.TenantPublicId;

        // Tres intentos denegados nuevos, reenviados a Mongo sellados.
        foreach (var (ruta, cuerpo) in EscriturasDeGobierno(esc).Take(3))
            await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Lectura.Token, HttpMethod.Post, ruta, cuerpo);
        await fx.ReenviarAuditoriaAsync(tenant);
        var deCartera = (await OutboxAsync(tenant)).Where(f => f.Module == "Lending" && f.Seq != null).OrderBy(f => f.Seq).ToList();
        deCartera.Should().HaveCountGreaterThanOrEqualTo(3, "los eventos Lending.* van por COR_AuditOutbox y se sellan (T0189)");
        var (alterado, borrado) = (deCartera[^3], deCartera[^2]);

        // Uno más después, para que el borrado no sea el último de la cadena.
        await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Lectura.Token, HttpMethod.Post, Organos, EscriturasDeGobierno(esc)[0].Cuerpo);
        await fx.ReenviarAuditoriaAsync(tenant);

        var coleccion = fx.Factory.Services.GetRequiredService<IMongoClient>()
            .GetDatabase(AuditDatabaseNames.Para(BaseDeAuditoria, tenant.ToString("N")))
            .GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);
        (await coleccion.UpdateOneAsync(Filtro(alterado), Builders<BsonDocument>.Update.Set("userName", "otra persona"))).ModifiedCount.Should().Be(1);
        (await coleccion.DeleteOneAsync(Filtro(borrado))).DeletedCount.Should().Be(1);

        var verificacion = await CarteraE2E.EnviarAsync(http, esc.Usuarios.Auditor.Token, HttpMethod.Post, Verificar,
            new { from = DateTime.UtcNow.AddHours(-2), to = DateTime.UtcNow.AddHours(1) });
        verificacion.StatusCode.Should().Be(HttpStatusCode.OK, await verificacion.Content.ReadAsStringAsync());
        var incidentes = (await CarteraE2E.LeerAsync(verificacion)).GetProperty("incidents").EnumerateArray()
            .Select(i => (Kind: i.GetProperty("kind").GetString(), Seq: i.GetProperty("seq").GetInt64())).ToList();
        incidentes.Should().Contain(("Altered", alterado.Seq!.Value));
        incidentes.Should().Contain(("Deleted", borrado.Seq!.Value));
    }

    // ---------------------------------------------------------------------------------------- escenario --

    /// <summary>
    /// La cooperativa «seguridad» con sus usuarios (T0267), perfil regulatorio con actividad financiera, una carga de migración aplicada
    /// (el único asunto de acta que existe antes de US2: <c>ActivationDifferences</c>), el Comité de crédito con comite.1..3, el Consejo con
    /// cinco principales y dos suplentes y fracción de vinculados 4/5, el consejero D y su cónyuge como vinculados, y una sesión abierta del
    /// comité para las lecturas. Una por fixture.
    /// </summary>
    private sealed record Escenario(
        CarteraE2E.CooperativaAislada Coop,
        UsuariosDeCartera Usuarios,
        DateOnly Hoy,
        Guid Carga,
        Guid Comite,
        IReadOnlyDictionary<Guid, Guid> MiembrosDelComite,
        Guid Consejo,
        Guid ConsejeroD,
        Guid SesionDeLectura,
        Guid PuntoDeLectura);

    private static readonly Dictionary<CentralIdentityApiFixture, Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();

    private Task<Escenario> EscenarioAsync()
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue(fx, out var tarea))
            {
                tarea = PrepararAsync();
                Escenarios[fx] = tarea;
            }
            return tarea;
        }
    }

    private async Task<Escenario> PrepararAsync()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "seguridad", primerEjercicioContable: hoy.Year);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop);

        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/core/regulatory-profile/versions", new
        {
            organizationType = "COOP_AHORRO_CREDITO", financialActivity = true, supervisionLevel = 2, validFrom = hoy,
            source = "Certificado de la Supersolidaria (e2e)", reason = "Cooperativa con actividad financiera para el ensayo de gobierno",
        });

        var alta = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/migration/loads", new { cutoffDate = hoy.AddDays(-1), notes = "Asunto de las actas (e2e)" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var carga = (await CarteraE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/migration/loads/{carga}/apply", new { reason = "Carga vacía (e2e)" });

        // Comité de crédito: tres principales (comite.1..3).
        var comite = (await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, Organos, new
        {
            code = "COMITE", name = "Comité de crédito", kind = "CreditCommittee", decidesCredit = true, minutesPrefix = "CC-",
            rules = ReglasDeOrgano(hoy, null),
        })).GetProperty("publicId").GetGuid();
        var miembros = new Dictionary<Guid, Guid>();
        foreach (var u in new[] { usuarios.Comite1, usuarios.Comite2, usuarios.Comite3 })
            miembros[u.Persona!.Value] = CarteraE2E.MiembroDe(await NombrarAsync(http, admin, comite, u.Persona!.Value, hoy));

        // Consejo: cinco principales y dos suplentes, personas del maestro; fracción de vinculados 4/5.
        var consejo = (await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, Organos, new
        {
            code = "CONSEJO", name = "Consejo de administración", kind = "BoardOfDirectors", decidesCredit = true, minutesPrefix = "CA-",
            rules = ReglasDeOrgano(hoy, (4, 5)),
        })).GetProperty("publicId").GetGuid();
        var principales = new List<(Guid Persona, Guid Miembro)>();
        for (var i = 1; i <= 5; i++)
        {
            var persona = await CarteraE2E.PersonaAsync(http, admin, $"Consejero{(char)('A' + i - 1)}", "Ensayo");
            principales.Add((persona, CarteraE2E.MiembroDe(await NombrarAsync(http, admin, consejo, persona, hoy))));
        }
        for (var i = 1; i <= 2; i++)
        {
            var persona = await CarteraE2E.PersonaAsync(http, admin, $"Suplente{i}", "Ensayo");
            await NombrarAsync(http, admin, consejo, persona, hoy, suplenteDe: principales[i - 1].Miembro);
        }

        // Vinculados: el consejero D y su cónyuge.
        var consejeroD = principales[3].Persona;
        var conyuge = await CarteraE2E.PersonaAsync(http, admin, "ConyugeD", "Ensayo");
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, Vinculados, new { personPublicId = consejeroD, basis = "BoardMember", validFrom = hoy });
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, Vinculados, new
        {
            personPublicId = conyuge, basis = "Spouse", linkedToPersonPublicId = consejeroD, validFrom = hoy,
        });

        var esc = new Escenario(coop, usuarios, hoy, carga, comite, miembros, consejo, consejeroD, Guid.Empty, Guid.Empty);
        var lectura = await SesionDelComiteAsync(http, esc, presentes: [usuarios.Comite1, usuarios.Comite2, usuarios.Comite3]);
        return esc with { SesionDeLectura = lectura.Sesion, PuntoDeLectura = lectura.Punto };
    }

    private static Task<JsonElement> NombrarAsync(HttpClient http, string admin, Guid organo, Guid persona, DateOnly desde, Guid? suplenteDe = null) =>
        CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Organos}/{organo}/members", new
        {
            personPublicId = persona, position = suplenteDe is null ? "Member" : "Alternate", votes = true, validFrom = desde,
            alternateForMemberPublicId = suplenteDe, appointmentNotes = "Nombramiento del ensayo (quickstart §2.7)",
        });

    /// <summary>Una sesión del comité de hoy con esos presentes (los demás ausentes) y un punto <c>Decision</c> sobre la carga.</summary>
    private static async Task<(Guid Sesion, Guid Punto)> SesionDelComiteAsync(HttpClient http, Escenario esc, UsuariosDeCartera.Usuario[] presentes)
    {
        var secretaria = esc.Usuarios.Secretaria.Token;
        var sesion = (await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, Sesiones, new
        {
            governingBodyPublicId = esc.Comite, kind = "Ordinary", mode = "InPerson", heldOn = esc.Hoy, location = "Sala de juntas",
        })).GetProperty("publicId").GetGuid();
        var vienen = presentes.Select(p => p.Persona!.Value).ToHashSet();
        await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, $"{Sesiones}/{sesion}/attendance", new
        {
            attendees = esc.MiembrosDelComite.Select(m => new { memberPublicId = m.Value, present = vienen.Contains(m.Key) }).ToList(),
        });
        var punto = (await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, $"{Sesiones}/{sesion}/items", new
        {
            subject = LendingDecisionSubjects.ActivationDifferences, subjectPublicId = esc.Carga, kind = "Decision",
        })).GetProperty("itemPublicId").GetGuid();
        return (sesion, punto);
    }

    private static async Task<HttpResponseMessage> CerrarAsync(HttpClient http, string token, Guid sesion)
    {
        var huella = (await CarteraE2E.GetAsync(http, token, $"{Sesiones}/{sesion}")).GetProperty("currentContentSha256").GetString();
        return await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Sesiones}/{sesion}/close", new { expectedContentSha256 = huella });
    }

    private static object Voto(Guid persona, string valor, string metodo, string? motivo = null) =>
        new { personPublicId = persona, vote = valor, method = metodo, replacementReason = motivo };

    private sealed record Fraccion(int Numerator, int Denominator, string Comparison);

    private sealed record Reglas(Fraccion Quorum, Fraccion OrdinaryMajority, Fraccion? RelatedPartyFraction, DateOnly ValidFrom, string Reason, string Source);

    private static Reglas ReglasDeOrgano(DateOnly desde, (int N, int D)? vinculados) => new(
        new Fraccion(1, 2, "MoreThan"), new Fraccion(1, 2, "MoreThan"),
        vinculados is { } v ? new Fraccion(v.N, v.D, "AtLeast") : null,
        desde, "Reglas de los estatutos para el ensayo", "Estatutos de la cooperativa de prueba, art. 40");

    /// <summary>Una escritura bien formada por cada recurso de gobierno: llegan al filtro de permisos, que responde 404.</summary>
    private static List<(string Ruta, object Cuerpo)> EscriturasDeGobierno(Escenario esc) =>
    [
        (Organos, new { code = "INTRUSO", name = "Órgano intruso", kind = "RiskCommittee", decidesCredit = false, minutesPrefix = "IN-", rules = ReglasDeOrgano(esc.Hoy, null) }),
        (Sesiones, new { governingBodyPublicId = esc.Comite, kind = "Ordinary", mode = "InPerson", heldOn = esc.Hoy }),
        (Matrices, new { validFrom = esc.Hoy, reason = "Matriz intrusa", source = "Ninguna", levels = Array.Empty<object>(), rules = Array.Empty<object>() }),
        (Vinculados, new { personPublicId = esc.ConsejeroD, basis = "LegalRepresentative", validFrom = esc.Hoy }),
    ];

    // ----------------------------------------------------------------------------------------- auditoría --

    /// <summary>Lleva la auditoría de la cooperativa a Mongo y devuelve los eventos de Cartera desde <paramref name="desde"/>.</summary>
    private Task<List<JsonElement>> EventosAsync(HttpClient http, Escenario esc, DateTime desde) => EventosAsync(http, esc.Coop, desde);

    private async Task<List<JsonElement>> EventosAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, DateTime desde)
    {
        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);
        await fx.Factory.Services.GetRequiredService<IAuditService>().FlushAsync();
        var hasta = DateTime.UtcNow.AddMinutes(1);
        var eventos = new List<JsonElement>();
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var lote = await CarteraE2E.GetAsync(http, coop.TokenAdmin,
                $"/api/audit/logs?modules=Lending&from={desde:O}&to={hasta:O}&page={pagina}&pageSize=100");
            var items = lote.GetProperty("items").EnumerateArray().ToList();
            eventos.AddRange(items);
            if (items.Count < 100) break;
        }
        return eventos;
    }

    private static string? Texto(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private async Task<List<AuditOutboxEntry>> OutboxAsync(Guid tenantPublicId)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == tenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        return await ambito.Db.AuditOutbox.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
    }

    private static FilterDefinition<BsonDocument> Filtro(AuditOutboxEntry fila) => Builders<BsonDocument>.Filter.Eq("_id", fila.EventId.ToString("D"));
}
