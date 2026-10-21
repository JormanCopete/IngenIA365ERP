using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0993 (feature 013, K5, US16; quickstart §7 pasos 5–7; Independent Test de la fase 21; colección «Cartera e2e», los dos motores por
/// <c>DB_PROVIDER</c>): el formato de cartera del SICSES y el archivo de una central de riesgo por las rutas de <c>/api/lending/regulatory</c>, sobre una
/// cooperativa aislada activada («sicses»), con la contabilidad iniciada y la matriz contable completa por la plantilla. Los layouts embebidos rigen
/// desde el 2026-12-01, así que el período del ensayo es el mayor entre diciembre de 2026 y dos meses después de hoy (con dos meses, la primera cuota
/// de los créditos de hoy ya está vencida al corte); los cierres mensuales se hacen mes a mes hasta él, con el reloj de la suite y el proceso conducido
/// a mano (<see cref="CierreE2E"/>).
/// <list type="bullet">
/// <item>Ana tiene documento de tipo «RC», que el SICSES no admite: validar no guarda nada y la incidencia bloqueante nombra su crédito y el campo;
/// generar responde 422 <c>Lending.Regulatory.BlockingIssues</c> con la versión 1 <c>Validated</c> sin archivo (su descarga,
/// <c>Lending.RegulatoryFile.NoFile</c>) (US16-1);</item>
/// <item>corregido el documento en el maestro y reabierto y vuelto a cerrar el mes (versión 2 del cierre), generar sin reconocer responde el aviso
/// <c>Lending.Regulatory.LayoutSinCotejar</c>; reconocido, 201 con la versión 2 <c>Generated</c>, su SHA-256 y la descarga por enlace firmado con los
/// mismos bytes; la versión 1 queda <c>Superseded</c>;</item>
/// <item>marcado cargado con el radicado, regenerar el período responde 422 <c>Lending.Regulatory.AlreadyUploaded</c>;</item>
/// <item>en el archivo de DATACREDITO, Beto (en mora, sin aviso previo con constancia) y Carla (sin autorización de reporte) quedan excluidos con su
/// motivo y listados en <c>GET /api/reports/lending/bureau-exclusions</c> (US16-2, SC-012);</item>
/// <item>sin <c>Lending.RegulatoryFiles.Read</c> la lista y la descarga responden el 404 genérico; el revisor fiscal lee y descarga pero no genera.</item>
/// </list>
/// Escrita el 2026-10-16 y <b>no ejecutada</b>: por instrucción del dueño las pruebas contra bases de datos se corren una sola vez, en la corrida
/// final de todas las fases. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class SicsesYCentralesTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "sicses";
    private const string Regulatorio = "/api/lending/regulatory";
    private const string Central = "DATACREDITO";

    /// <summary>El primer día con layout vigente (<c>validFrom</c> de los tres JSON embebidos).</summary>
    private static readonly DateOnly LayoutsDesde = new(2026, 12, 1);

    private static int _documento;

    [Fact]
    public async Task Validar_generar_regenerar_cargar_y_excluir_de_la_central()
    {
        var esc = await DesembolsoE2E.EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var tenant = esc.Coop.TenantPublicId;
        var u = esc.Usuarios;
        var despachador = new ConductorDelDespachador(fx);
        var hoy = esc.Hoy;
        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false);

        // El período del ensayo: el SICSES y la central del mismo mes.
        var mesDeHoy = new DateOnly(hoy.Year, hoy.Month, 1);
        var periodo = mesDeHoy.AddMonths(2) > LayoutsDesde ? mesDeHoy.AddMonths(2) : LayoutsDesde;
        var (anio, mes) = (periodo.Year, periodo.Month);
        var finDelPeriodo = new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes));

        // ------------------------------------------- la política y el suscriptor de la central; tres deudores con su crédito --
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
        {
            title = "Política de datos: CreditBureauReport", contentMarkdown = "Texto de la política de reporte a centrales para las e2e del SICSES.",
            effectiveFrom = DateTime.UtcNow.AddMinutes(-1), purpose = "CreditBureauReport",
        });
        var suscriptor = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/parameters/Centrales.CodigosDeSuscriptor/versions", new
        {
            scopeKind = 0, value = "DATACREDITO=123456;TRANSUNION=ABC123", validFrom = hoy.AddDays(-1), reason = "Suscriptor de las centrales del ensayo (e2e)",
        });
        suscriptor.IsSuccessStatusCode.Should().BeTrue($"el código de suscriptor: «{await suscriptor.Content.ReadAsStringAsync()}»");

        var (ana, documentoDeAna) = await DeudorAsync(http, admin, "Ana", tipoDeDocumento: "RC", reportaACentrales: true);
        var (beto, _) = await DeudorAsync(http, admin, "Beto", tipoDeDocumento: "C", reportaACentrales: true);
        var (carla, _) = await DeudorAsync(http, admin, "Carla", tipoDeDocumento: "C", reportaACentrales: false);
        var deAna = await CierreE2E.DesembolsadoAsync(http, esc, ana, 1_200_000m);
        var deBeto = await CierreE2E.DesembolsadoAsync(http, esc, beto, 1_000_000m);
        var deCarla = await CierreE2E.DesembolsadoAsync(http, esc, carla, 900_000m);
        var numeroDeAna = (await CarteraE2E.GetAsync(http, admin, $"{DesembolsoE2E.Creditos}/{deAna}")).GetProperty("number").GetString()!;

        // ------------------------------------------------------------- los cierres del mes, uno por uno hasta el período --
        await despachador.PasadasAsync(tenant, 3);
        Guid cierreDelPeriodo = default;
        for (var m = mesDeHoy; m <= periodo; m = m.AddMonths(1))
        {
            var fin = new DateOnly(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month));
            using (RelojEn(fx, fin.AddDays(1)))
            {
                var jefe = await IngresarAsync(http, u.Jefe);
                await CicloDeHoyAsync(fx, http, jefe, tenant);
                await despachador.PasadasAsync(tenant, 3);
                var corrida = await CierreE2E.OrdenarAsync(http, jefe, m.Year, m.Month);
                await CierreE2E.CorrerAsync(fx, http, jefe, tenant, corrida);
                if (m == periodo) cierreDelPeriodo = await CierreE2E.MensualAsync(http, jefe, m.Year, m.Month, version: 1);
            }
        }

        using (RelojEn(fx, finDelPeriodo.AddDays(1)))
        {
            var contadora = await IngresarAsync(http, u.Contadora);
            var jefe = await IngresarAsync(http, u.Jefe);

            // ----------------------------------------------------------------- los layouts: vigentes y sin cotejar (I7) --
            var layouts = (await CarteraE2E.GetAsync(http, contadora, $"{Regulatorio}/layouts")).EnumerateArray().ToList();
            layouts.Select(l => l.GetProperty("code").GetString()).Should().Contain(["SICSES-CARTERA-v1", "CENTRAL-DATACREDITO-v1"]);
            layouts.Should().OnlyContain(l => l.GetProperty("unverifiedFields").GetInt32() > 0, "los layouts se cargan con verified = false hasta cotejarlos");

            // ------------------------------------------------------ validar no guarda; la incidencia nombra crédito y campo --
            var validacion = await CarteraE2E.ExitoAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/sicses/{anio}/{mes}/validate");
            validacion.GetProperty("blocking").GetInt32().Should().BeGreaterThan(0, validacion.ToString());
            validacion.GetProperty("issues").EnumerateArray().Should().Contain(i =>
                i.GetProperty("severity").GetString() == "Blocking" && i.GetProperty("loan").GetString() == numeroDeAna
                && i.GetProperty("field").ValueKind == JsonValueKind.Number && i.GetProperty("field").GetInt32() == 1,"el tipo de documento «RC» de Ana no es un valor del SICSES (US16-1)");
            (await ArchivosAsync(http, contadora, "kind=1")).Should().BeEmpty("validar no guarda nada");

            // ------------------------------------------------- generar con bloqueantes: 422 y la versión 1 Validated sin archivo --
            var conBloqueantes = await CarteraE2E.ConClaveAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/sicses/{anio}/{mes}/generate",
                new { acknowledgeWarnings = true });
            var rechazo = await InventarioE2E.FallaAsync(conBloqueantes, "Lending.Regulatory.BlockingIssues");
            var datos = rechazo.GetProperty("data");
            datos.GetProperty("version").GetInt32().Should().Be(1);
            datos.GetProperty("issues").EnumerateArray().Should().Contain(i => i.GetProperty("loan").GetString() == numeroDeAna);
            var validada = datos.GetProperty("regulatoryFilePublicId").GetGuid();
            var v1 = await CarteraE2E.GetAsync(http, contadora, $"{Regulatorio}/files/{validada}");
            Estado(v1.GetProperty("file")).Should().Be("Validated");
            v1.GetProperty("file").GetProperty("downloadAvailable").GetBoolean().Should().BeFalse();
            await InventarioE2E.FallaAsync(await CarteraE2E.EnviarAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/files/{validada}/download-link", null),
                "Lending.RegulatoryFile.NoFile");

            // ----------------------------- corregido el maestro y vuelto a cerrar el mes (reapertura y Version + 1, T0989) --
            var correccion = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/core/people/{ana}", new
            {
                idType = "C", taxId = documentoDeAna, firstName = "Ana", lastName = "Sicses", mobile = "3001234560",
                email = $"ana.{documentoDeAna}@coop.sicses.test",
            });
            correccion.StatusCode.Should().Be(HttpStatusCode.NoContent, await correccion.Content.ReadAsStringAsync());
            var reapertura = await CierreE2E.ReabrirAsync(http, admin, cierreDelPeriodo, "Documento corregido antes del SICSES (e2e)");
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, reapertura);
            var otraVez = await CierreE2E.OrdenarAsync(http, jefe, anio, mes);
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, otraVez);
            await CierreE2E.MensualAsync(http, jefe, anio, mes, version: 2);

            // ------------------------------------------- sin reconocer: el aviso; reconocido: 201 Generated con SHA-256 --
            var sinReconocer = await CarteraE2E.ConClaveAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/sicses/{anio}/{mes}/generate",
                new { acknowledgeWarnings = false });
            sinReconocer.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await sinReconocer.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(sinReconocer)).Should().Be("Lending.Regulatory.LayoutSinCotejar");

            var generar = await CarteraE2E.ConClaveAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/sicses/{anio}/{mes}/generate",
                new { acknowledgeWarnings = true });
            generar.StatusCode.Should().Be(HttpStatusCode.Created, await generar.Content.ReadAsStringAsync());
            generar.Headers.Location.Should().NotBeNull();
            var generado = (await CarteraE2E.LeerAsync(generar)).GetProperty("file");
            Estado(generado).Should().Be("Generated");
            generado.GetProperty("version").GetInt32().Should().Be(2);
            var sha = generado.GetProperty("sha256").GetString()!;
            sha.Should().MatchRegex("^[0-9a-fA-F]{64}$");
            generado.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("code").GetString())
                .Should().Contain("Lending.Regulatory.LayoutSinCotejar", "la advertencia queda en la generación, nunca en el archivo");
            var archivo = generado.GetProperty("publicId").GetGuid();
            Estado((await CarteraE2E.GetAsync(http, contadora, $"{Regulatorio}/files/{validada}")).GetProperty("file"))
                .Should().Be("Superseded", "regenerar deja la versión anterior reemplazada");

            var enlace = await CarteraE2E.ExitoAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/files/{archivo}/download-link");
            var bytes = await DocumentosE2E.BajarAsync(http, enlace.GetProperty("url").GetString()!);
            Convert.ToHexString(SHA256.HashData(bytes)).Should().BeEquivalentTo(sha, "la descarga es el archivo que se generó");

            // ------------------------------------------- marcar cargado con el radicado; el período ya no se regenera --
            var cargado = await CarteraE2E.ConClaveAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/files/{archivo}/mark-uploaded", new
            {
                filingNumber = "SICSES-E2E-0001", uploadedAt = DateTime.UtcNow, filingDate = finDelPeriodo.AddDays(1),
            });
            cargado.StatusCode.Should().Be(HttpStatusCode.OK, await cargado.Content.ReadAsStringAsync());
            var marcado = await CarteraE2E.LeerAsync(cargado);
            Estado(marcado).Should().Be("Uploaded");
            marcado.GetProperty("uploaded").GetProperty("filingNumber").GetString().Should().Be("SICSES-E2E-0001");
            await InventarioE2E.FallaAsync(await CarteraE2E.ConClaveAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/sicses/{anio}/{mes}/generate",
                new { acknowledgeWarnings = true }), "Lending.Regulatory.AlreadyUploaded");

            // ------------------------------------------------- la central: excluidos con su motivo (US16-2, SC-012) --
            var deLaCentral = await CarteraE2E.ExitoAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/bureaus/{Central}/{anio}/{mes}/validate");
            var excluidos = deLaCentral.GetProperty("excluded").EnumerateArray()
                .ToDictionary(x => x.GetProperty("loanPublicId").GetGuid(), x => x.GetProperty("reason").GetString());
            excluidos.Should().Contain(deBeto, "NoPriorNoticeEvidence", "Beto está en mora sin aviso previo con constancia de su episodio");
            excluidos.Should().Contain(deCarla, "NoReportAuthorization", "Carla no autorizó el reporte a centrales");

            var central = await CarteraE2E.ConClaveAsync(http, contadora, HttpMethod.Post, $"{Regulatorio}/bureaus/{Central}/{anio}/{mes}/generate",
                new { acknowledgeWarnings = true });
            central.StatusCode.Should().Be(HttpStatusCode.Created, await central.Content.ReadAsStringAsync());
            var detalle = await CarteraE2E.LeerAsync(central);
            detalle.GetProperty("file").GetProperty("bureauCode").GetString().Should().Be(Central);
            detalle.GetProperty("excluded").EnumerateArray().Select(x => x.GetProperty("loanPublicId").GetGuid()).Should().Contain([deBeto, deCarla]);

            var informe = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, contadora,
                $"/api/reports/lending/bureau-exclusions?bureau={Central}&year={anio}&month={mes}"));
            var porCredito = informe.Filas.ToDictionary(f => Guid.Parse(informe.Oculta(f, "_credito")), f => informe.Texto(f, "Motivo"));
            porCredito.Should().ContainKey(deBeto).WhoseValue.Should().Contain("aviso previo");
            porCredito.Should().ContainKey(deCarla).WhoseValue.Should().Contain("autorización");

            // ------------------------------------------------ permisos: sin RegulatoryFiles.Read, 404; el revisor no genera --
            var lectura = await IngresarAsync(http, u.Lectura);
            (await CarteraE2E.MandarGetAsync(http, lectura, $"{Regulatorio}/files")).StatusCode.Should().Be(HttpStatusCode.NotFound,
                "la lista trae deudores: se abre con RegulatoryFiles.Read, nunca con un View");
            (await CarteraE2E.EnviarAsync(http, lectura, HttpMethod.Post, $"{Regulatorio}/files/{archivo}/download-link", null)).StatusCode
                .Should().Be(HttpStatusCode.NotFound);

            var revisor = await IngresarAsync(http, u.Revisor);
            (await ArchivosAsync(http, revisor, "kind=1")).Should().Contain(f => f.GetProperty("publicId").GetGuid() == archivo, "el revisor fiscal lee");
            (await CarteraE2E.EnviarAsync(http, revisor, HttpMethod.Post, $"{Regulatorio}/files/{archivo}/download-link", null)).StatusCode
                .Should().Be(HttpStatusCode.OK, "y descarga");
            (await CarteraE2E.ConClaveAsync(http, revisor, HttpMethod.Post, $"{Regulatorio}/bureaus/{Central}/{anio}/{mes}/generate",
                new { acknowledgeWarnings = true })).StatusCode.Should().Be(HttpStatusCode.NotFound, "pero no genera");
        }
    }

    /// <summary>
    /// Un asociado con el tipo de documento dado y su autorización de estudio (y, si <paramref name="reportaACentrales"/>, la de reporte a centrales con
    /// evidencia). Molde: <see cref="DesembolsoE2E.DeudorAsync"/>.
    /// </summary>
    private static async Task<(Guid Persona, string Documento)> DeudorAsync(HttpClient http, string admin, string nombre, string tipoDeDocumento,
        bool reportaACentrales)
    {
        var documento = (6_700_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = tipoDeDocumento, taxId = documento, firstName = nombre, lastName = "Sicses", mobile = "3001234560",
                email = $"{nombre.ToLowerInvariant()}.{documento}@coop.sicses.test",
            },
            associate = new { joinDate = new DateOnly(2024, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var persona = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "CreditProcessing");
        if (reportaACentrales) await OriginacionE2E.AutorizarAsync(http, admin, persona, "CreditBureauReport");
        return (persona, documento);
    }

    private static Task<string> IngresarAsync(HttpClient http, UsuariosDeCartera.Usuario usuario) => UsuariosDeCartera.IngresarAsync(http, usuario);

    private static async Task<List<JsonElement>> ArchivosAsync(HttpClient http, string token, string filtro) =>
        (await CarteraE2E.GetAsync(http, token, $"{Regulatorio}/files?{filtro}")).GetProperty("items").EnumerateArray().ToList();

    /// <summary>El estado de una generación por su nombre, venga el enum como número o como texto.</summary>
    private static string Estado(JsonElement archivo)
    {
        var estado = archivo.GetProperty("status");
        return estado.ValueKind == JsonValueKind.Number
            ? estado.GetInt32() switch { 1 => "Validated", 2 => "Generated", 3 => "Superseded", 4 => "Uploaded", var n => n.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            : estado.GetString()!;
    }
}
