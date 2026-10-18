using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T080 (US2, escenarios 1 a 5 de la spec y el menor que cumple 18; quickstart §2; contracts/api.md §4 y §10): la apertura de cuentas por
/// HTTP en una cooperativa aislada con créditos, aportes y captaciones activados (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>):
/// <list type="number">
/// <item>La prueba independiente: una cuenta a la vista conjunta con dos titulares y beneficiarios al 60 % y 40 %, marcada exenta del GMF; una segunda
/// cuenta exenta del mismo titular responde <c>Lending.Gmf.AlreadyMarked</c> con la vigente (escenario 4).</item>
/// <item>Escenario 1: una persona que no es asociada no abre en una línea que exige asociado (<c>Lending.Savings.MembersOnly</c>, «vincular primero»).</item>
/// <item>Escenario 2: una coincidencia en listas restrictivas deja la cuenta en apertura con el aviso; quien la abrió no la aprueba, nadie la aprueba
/// antes de que el oficial libere la coincidencia, y después otra persona la activa.</item>
/// <item>Escenario 3: beneficiarios que suman 90 % responden <c>Lending.Savings.BeneficiariesMustSum100</c> con la suma.</item>
/// <item>Escenario 5: dos aperturas a la vez de la misma línea reciben números distintos y consecutivos.</item>
/// <item>El menor con su representante y el soporte: al cumplir 18 años el ciclo diario cierra la vigencia del representante la víspera y marca la cuenta
/// «actualizar datos», que se levanta con la autorización de datos del titular.</item>
/// </list>
/// Exige un despliegue con K8 (las líneas de captación se activan desde K8). <b>e2e en la corrida final</b> (decisión del dueño).
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class AperturaDeCuentasTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";
    private const string Exenciones = "/api/lending/gmf-exemptions";
    private const string Lineas = "/api/lending/lines";
    private const string Perfil = "/api/core/regulatory-profile";
    private const string Soportes = "LendingAccountPartySupport";

    private static int _documento;

    /// <summary>Lo que comparten las pruebas: la cooperativa, sus usuarios, la oficina, la línea a la vista y la de aportes.</summary>
    private sealed record Escenario(CaptacionesE2E.CooperativaDeCaptaciones Coop, UsuariosDeCartera Usuarios, Guid Oficina, Guid ALaVista, Guid Aportes, DateOnly Hoy)
    {
        public string Admin => Coop.TokenAdmin;
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();

    private Task<Escenario> EscenarioAsync(string nombre)
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = ArmarAsync(nombre);
                Escenarios[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private async Task<Escenario> ArmarAsync(string nombre)
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, nombre);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        // Quien abre debe tener persona (la segregación compara personas: quien abre no aprueba la apertura).
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        await PerfilQueCaptaAsync(http, admin);

        var vista = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = "AHVISTA", name = "Ahorro a la vista", depositClass = "Demand", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Tasa aprobada por el consejo (e2e)", requiresMembership = true, allowsCoHolders = true, allowsMinors = true,
                allowsPartialWithdrawals = true, rate = new { kind = "Fixed", effectiveAnnual = 0.01m }, balanceMethod = "DailyBalance",
                minimums = new { toOpen = 0m, toMaintain = 0m, toEarnInterest = 0m }, dayCount = "Actual365", rateEquivalence = "Compound",
                rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" }, settlementPeriodicity = "Monthly", tax = new { gmfApplies = true },
            },
        });
        vista.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea a la vista: «{await vista.Content.ReadAsStringAsync()}»");
        var aportes = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/contribution", new
        {
            code = "APORTES", name = "Aportes sociales", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Estatutos, art. 20 (e2e)", minimumNonReducible = new { kind = "FixedAmount", amount = 500_000m },
                installment = new { kind = "FixedAmount", amount = 50_000m, periodicity = "Monthly", dueDay = 5 }, refundTermDays = 60,
            },
        });
        aportes.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea de aportes: «{await aportes.Content.ReadAsStringAsync()}»");
        return new Escenario(coop, usuarios, usuarios.O1.OficinaPublicId, (await CarteraE2E.LeerAsync(vista)).GetProperty("publicId").GetGuid(),
            (await CarteraE2E.LeerAsync(aportes)).GetProperty("publicId").GetGuid(), hoy);
    }

    /// <summary>Corrige el perfil regulatorio vigente para que la cooperativa capte ahorro.</summary>
    private static async Task PerfilQueCaptaAsync(HttpClient http, string admin)
    {
        var vigente = (await CarteraE2E.GetAsync(http, admin, Perfil)).GetProperty("current");
        if (vigente.TryGetProperty("mayTakeDeposits", out var capta) && capta.ValueKind == JsonValueKind.True) return;
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Perfil}/versions", new
        {
            organizationType = vigente.GetProperty("organizationTypeCode").GetString(), financialActivity = true, supervisionLevel = 2,
            validFrom = DateOnly.Parse(vigente.GetProperty("validFrom").GetString()!, CultureInfo.InvariantCulture),
            source = "Certificado de la Supersolidaria (e2e)", reason = "La cooperativa capta ahorro (e2e)",
            supersedesPublicId = vigente.GetProperty("versionPublicId").GetGuid(), mayTakeDeposits = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"corregir el perfil: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Una persona del maestro con su fecha de nacimiento, asociada o no, con su autorización de tratamiento de datos (con evidencia).</summary>
    private static async Task<(Guid Persona, string Documento)> PersonaAsync(HttpClient http, string admin, string nombre, DateOnly nacimiento,
        bool asociada = true, bool autorizada = true)
    {
        var documento = (6_600_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(CultureInfo.InvariantCulture);
        var persona = new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Captaciones", personType = "01", dateOfBirth = nacimiento,
            email = $"{nombre.ToLowerInvariant()}.{documento}@captaciones.test",
        };
        Guid publicId;
        if (asociada)
        {
            var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
            {
                person = persona, associate = new { joinDate = new DateOnly(2024, 1, 15), contributionRate = 5m },
            });
            alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
            publicId = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        }
        else
        {
            var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/people", persona);
            alta.StatusCode.Should().Be(HttpStatusCode.Created, $"persona {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
            publicId = (await CarteraE2E.LeerAsync(alta)).GetGuid();
        }
        if (autorizada) await OriginacionE2E.AutorizarAsync(http, admin, publicId, "GeneralTreatment");
        return (publicId, documento);
    }

    private static object Apertura(Escenario esc, Guid linea, Guid titular, object[]? partes = null, object[]? beneficiarios = null,
        string manejo = "Indistinct", bool exenta = false) => new
    {
        linePublicId = linea, holderPersonPublicId = titular, officePublicId = esc.Oficina, handlingMode = manejo,
        parties = partes ?? [], beneficiaries = beneficiarios ?? [], markGmfExempt = exenta,
    };

    private static Task<HttpResponseMessage> AbrirAsync(HttpClient http, string token, object cuerpo) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Cuentas, cuerpo);

    private static async Task<JsonElement> AbiertaAsync(HttpClient http, string token, object cuerpo)
    {
        var resp = await AbrirAsync(http, token, cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la cuenta: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    private static DateOnly Adulto => new(1985, 3, 10);

    // ------------------------------------------------------------------------------------------- prueba independiente --

    [Fact]
    public async Task US2_conjunta_con_beneficiarios_60_40_exenta_del_GMF_y_la_segunda_exenta_se_rechaza()
    {
        var esc = await EscenarioAsync("apertura");
        using var http = fx.CreateClient();
        var (ana, _) = await PersonaAsync(http, esc.Admin, "Ana", Adulto);
        var (beto, _) = await PersonaAsync(http, esc.Admin, "Beto", Adulto);
        var (carla, _) = await PersonaAsync(http, esc.Admin, "Carla", new DateOnly(2010, 1, 5), asociada: false, autorizada: false);
        var (dario, _) = await PersonaAsync(http, esc.Admin, "Dario", new DateOnly(2012, 6, 9), asociada: false, autorizada: false);

        var cuenta = await AbiertaAsync(http, esc.Admin, Apertura(esc, esc.ALaVista, ana,
            [new { personPublicId = beto, role = "CoHolder" }],
            [new { personPublicId = carla, percent = 60m }, new { personPublicId = dario, percent = 40m }], manejo: "Joint", exenta: true));

        cuenta.GetProperty("status").GetInt32().Should().Be(2, "activa");
        cuenta.GetProperty("number").GetString().Should().StartWith("AHVISTA-");
        cuenta.GetProperty("handlingMode").GetInt32().Should().Be(2, "conjunto");
        cuenta.GetProperty("parties").EnumerateArray().Select(p => p.GetProperty("share").GetDecimal()).Should().BeEquivalentTo([0.5m, 0.5m]);
        cuenta.GetProperty("beneficiaries").EnumerateArray().Select(b => b.GetProperty("percent").GetDecimal()).Should().BeEquivalentTo([60m, 40m]);
        cuenta.GetProperty("gmfExempt").ValueKind.Should().Be(JsonValueKind.Object);

        var marcas = await CarteraE2E.GetAsync(http, esc.Admin, $"{Exenciones}?personPublicId={ana}&active=true");
        marcas.EnumerateArray().Should().ContainSingle().Which.GetProperty("accountNumber").GetString().Should().Be(cuenta.GetProperty("number").GetString());

        // Escenario 4: una segunda cuenta exenta del mismo titular se rechaza con la vigente.
        var segunda = await AbrirAsync(http, esc.Admin, Apertura(esc, esc.ALaVista, ana, exenta: true));
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(segunda)).Should().Be("Lending.Gmf.AlreadyMarked");

        // Sin marcarla, abre; marcarla después también se rechaza, salvo que reemplace la vigente.
        var otra = await AbiertaAsync(http, esc.Admin, Apertura(esc, esc.ALaVista, ana));
        var marcar = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, Exenciones,
            new { accountPublicId = otra.GetProperty("publicId").GetGuid(), reason = "La pide el titular (e2e)" });
        (await CarteraE2E.CodigoDeErrorAsync(marcar)).Should().Be("Lending.Gmf.AlreadyMarked");
        var reemplazo = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, Exenciones,
            new { accountPublicId = otra.GetProperty("publicId").GetGuid(), reason = "Cambio de cuenta exenta (e2e)", replaceCurrent = true });
        reemplazo.StatusCode.Should().Be(HttpStatusCode.Created, await reemplazo.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------------------------------------- escenarios --

    [Fact]
    public async Task Escenario_1_un_no_asociado_no_abre_donde_se_exige_asociado()
    {
        var esc = await EscenarioAsync("apertura");
        using var http = fx.CreateClient();
        var (externa, _) = await PersonaAsync(http, esc.Admin, "Elena", Adulto, asociada: false);

        var resp = await AbrirAsync(http, esc.Admin, Apertura(esc, esc.ALaVista, externa));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("Lending.Savings.MembersOnly");
        (await resp.Content.ReadAsStringAsync()).Should().Contain("Vincular primero");
        (await AbrirAsync(http, esc.Admin, Apertura(esc, esc.Aportes, externa))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "los aportes siempre son de un asociado");
    }

    [Fact]
    public async Task Escenario_2_la_coincidencia_en_listas_retiene_la_apertura_hasta_que_el_oficial_la_libera_y_otro_la_aprueba()
    {
        var esc = await EscenarioAsync("listas");
        using var http = fx.CreateClient();
        var (fabio, documento) = await PersonaAsync(http, esc.Admin, "Fabio", Adulto);
        var carga = await OriginacionE2E.ImportarListaAsync(http, esc.Admin, OriginacionE2E.LibroDeListas(esc.Hoy, (documento, "FABIO CAPTACIONES")), "apply");
        carga.StatusCode.Should().Be(HttpStatusCode.OK, $"cargar la lista propia: «{await carga.Content.ReadAsStringAsync()}»");

        var cuenta = await AbiertaAsync(http, esc.Admin, Apertura(esc, esc.ALaVista, fabio));
        cuenta.GetProperty("status").GetInt32().Should().Be(1, "en apertura");
        cuenta.GetProperty("openingBlockers").EnumerateArray().Select(b => b.GetString()).Should().Contain("ScreeningMatch");
        var aviso = cuenta.GetProperty("warnings").EnumerateArray().Single(w => w.GetProperty("code").GetString() == "Lending.Screening.Blocked");
        var resultado = aviso.GetProperty("screeningResultPublicId").GetGuid();
        var id = cuenta.GetProperty("publicId").GetGuid();

        var propia = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Cuentas}/{id}/approve-opening", new { reason = "Revisada (e2e)" });
        (await CarteraE2E.CodigoDeErrorAsync(propia)).Should().Be("Lending.Segregation.Conflict", "quien abre no aprueba");
        var otro = esc.Usuarios.Administrador.Token;
        var retenida = await CarteraE2E.ConClaveAsync(http, otro, HttpMethod.Post, $"{Cuentas}/{id}/approve-opening", new { reason = "Revisada (e2e)" });
        (await CarteraE2E.CodigoDeErrorAsync(retenida)).Should().Be("Lending.Screening.Blocked", "sólo el oficial libera la coincidencia");

        var liberada = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Oficial.Token, HttpMethod.Post, $"{OriginacionE2E.Listas}/{resultado}/release",
            new { reason = "Homónimo verificado con el documento (e2e)" });
        liberada.StatusCode.Should().Be(HttpStatusCode.OK, await liberada.Content.ReadAsStringAsync());

        var aprobada = await CarteraE2E.ConClaveAsync(http, otro, HttpMethod.Post, $"{Cuentas}/{id}/approve-opening", new { reason = "Revisada (e2e)" });
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(aprobada)).GetProperty("status").GetInt32().Should().Be(2, "activa");
    }

    [Fact]
    public async Task Escenario_3_beneficiarios_que_suman_90_se_rechazan_con_la_suma()
    {
        var esc = await EscenarioAsync("apertura");
        using var http = fx.CreateClient();
        var (gina, _) = await PersonaAsync(http, esc.Admin, "Gina", Adulto);
        var (hugo, _) = await PersonaAsync(http, esc.Admin, "Hugo", Adulto, asociada: false, autorizada: false);
        var (ines, _) = await PersonaAsync(http, esc.Admin, "Ines", Adulto, asociada: false, autorizada: false);

        var resp = await AbrirAsync(http, esc.Admin, Apertura(esc, esc.ALaVista, gina, beneficiarios:
            [new { personPublicId = hugo, percent = 50m }, new { personPublicId = ines, percent = 40m }]));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("Lending.Savings.BeneficiariesMustSum100");
        (await resp.Content.ReadAsStringAsync()).Should().Contain("\"sum\":90");
    }

    [Fact]
    public async Task Escenario_5_dos_aperturas_a_la_vez_reciben_numeros_distintos_y_consecutivos()
    {
        var esc = await EscenarioAsync("concurrencia");
        using var http1 = fx.CreateClient();
        using var http2 = fx.CreateClient();
        var (juan, _) = await PersonaAsync(http1, esc.Admin, "Juan", Adulto);
        var (karen, _) = await PersonaAsync(http1, esc.Admin, "Karen", Adulto);

        var respuestas = await Task.WhenAll(AbrirAsync(http1, esc.Admin, Apertura(esc, esc.ALaVista, juan)),
            AbrirAsync(http2, esc.Admin, Apertura(esc, esc.ALaVista, karen)));

        respuestas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var numeros = new List<long>();
        foreach (var r in respuestas)
            numeros.Add(long.Parse((await CarteraE2E.LeerAsync(r)).GetProperty("number").GetString()!["AHVISTA-".Length..], CultureInfo.InvariantCulture));
        numeros.Should().OnlyHaveUniqueItems();
        (numeros.Max() - numeros.Min()).Should().Be(1, "consecutivos, sin hueco por la concurrencia");
    }

    // ------------------------------------------------------------------------------------------------- mayoría de edad --

    [Fact]
    public async Task El_menor_que_cumple_18_pasa_a_manejar_su_cuenta_y_debe_actualizar_sus_datos()
    {
        var esc = await EscenarioAsync("menor");
        using var http = fx.CreateClient();
        var cumple = esc.Hoy.AddDays(3);
        var (menor, _) = await PersonaAsync(http, esc.Admin, "Sofia", cumple.AddYears(-18));
        var (madre, _) = await PersonaAsync(http, esc.Admin, "Lucia", Adulto);
        var registro = await ActasE2E.SoporteAsync(http, esc.Admin, menor, Soportes, "registro-civil.pdf");

        var sinRepresentante = await AbrirAsync(http, esc.Admin, Apertura(esc, esc.ALaVista, menor));
        (await CarteraE2E.CodigoDeErrorAsync(sinRepresentante)).Should().Be("Lending.Savings.RepresentativeRequired");

        var cuenta = await AbiertaAsync(http, esc.Admin, Apertura(esc, esc.ALaVista, menor,
            [new { personPublicId = madre, role = "LegalRepresentative", representationBasis = "Parent", supportAttachmentPublicId = registro }]));
        var id = cuenta.GetProperty("publicId").GetGuid();
        cuenta.GetProperty("holder").GetProperty("personPublicId").GetGuid().Should().Be(menor, "el menor es el titular");

        using (CausacionE2E.RelojEn(fx, cumple))
            await CausacionE2E.CicloDeHoyAsync(fx, http, esc.Admin, esc.Coop.TenantPublicId);

        var partes = await CarteraE2E.GetAsync(http, esc.Admin, $"{Cuentas}/{id}/parties?includeHistory=true");
        partes.GetProperty("requiresDataUpdate").GetBoolean().Should().BeTrue();
        partes.GetProperty("parties").EnumerateArray().Should().NotContain(p => p.GetProperty("role").GetInt32() == 4, "el representante deja de firmar");
        var representante = partes.GetProperty("history").EnumerateArray().Single(p => p.GetProperty("role").GetInt32() == 4);
        representante.GetProperty("validTo").GetString().Should().Be(cumple.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var confirmada = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Cuentas}/{id}/data-update",
            new { reason = "Actualizó su conocimiento y su autorización (e2e)" });
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(confirmada)).GetProperty("marks").GetProperty("requiresDataUpdate").GetBoolean().Should().BeFalse();
    }
}
