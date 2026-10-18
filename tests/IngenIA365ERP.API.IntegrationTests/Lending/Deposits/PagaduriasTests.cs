using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T095 (quickstart §3, pasos 1 y la parte del 2 que está construida; contracts/api.md §18.3; research E61): los grupos de archivo y las
/// equivalencias de concepto de un convenio por HTTP, sobre el escenario de libranza de la 013 (<see cref="LibranzaE2E.EscenarioAsync"/>, cooperativa
/// aislada «pagadurias»): fijar dos grupos (créditos; aportes y ahorros) registra una versión nueva con grupos; un código que agrupa la cuota de aportes y
/// la de ahorro permanente con reparto por prelación queda en su grupo; un ítem en dos códigos responde <c>.ConceptMappedTwice</c>; y generar el archivo
/// de un convenio con grupos responde <c>Lending.PayrollFile.FileGroupsPending</c> mientras la generación por grupo no exista (T091 parcial). Los pasos 3
/// y 4 (la respuesta con reparto y los «no aplicados») llegan con esa generación.
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class PagaduriasTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Grupos_y_equivalencias_registran_una_version_nueva_y_la_generacion_por_grupo_avisa_que_esta_pendiente()
    {
        var esc = await LibranzaE2E.EscenarioAsync(fx, "pagadurias");
        using var http = fx.CreateClient();
        var admin = await LibranzaE2E.AdministradorDeLaCooperativaAsync(http, esc.Nombre);
        var convenio = await CarteraE2E.GetAsync(http, admin, $"{LibranzaE2E.Convenios}/{esc.Convenio}");
        var version = convenio.GetProperty("versions").EnumerateArray().Last();
        var ida = version.GetProperty("outboundFormatPublicId").GetGuid();
        var vuelta = version.GetProperty("resultFormatPublicId").GetGuid();
        var desde = esc.Hoy.AddMonths(1);

        var grupos = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{LibranzaE2E.Convenios}/{esc.Convenio}/file-groups", new
        {
            validFrom = desde, reason = "La pagaduría separa los archivos (e2e)",
            groups = new object[]
            {
                new { code = "CRED", name = "Créditos", outboundFormatPublicId = ida, resultFormatPublicId = vuelta },
                new { code = "APORTES", name = "Aportes y ahorros", outboundFormatPublicId = ida, resultFormatPublicId = vuelta, cutoffDay = 15 },
            },
        });
        grupos.StatusCode.Should().Be(HttpStatusCode.OK, await grupos.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(grupos)).EnumerateArray().Select(g => g.GetProperty("code").GetString()).Should().Equal("CRED", "APORTES");

        var dosVeces = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{LibranzaE2E.Convenios}/{esc.Convenio}/concept-equivalences", new
        {
            validFrom = desde.AddDays(1), reason = "Equivalencias (e2e)",
            equivalences = new object[]
            {
                new { paymasterConceptCode = "101", description = "Cuota de crédito", split = "Priority", fileGroupCode = "CRED",
                    members = new[] { new { nature = "Placement", lineCode = "*", concept = "Installment", priority = 1 } } },
                new { paymasterConceptCode = "102", description = "Capital", split = "Priority", fileGroupCode = "CRED",
                    members = new[] { new { nature = "Placement", lineCode = "LIBRANZA", concept = "Principal", priority = 1 } } },
            },
        });
        (await CarteraE2E.CodigoDeErrorAsync(dosVeces)).Should().Be("Lending.PayrollAgreement.ConceptMappedTwice");

        var equivalencias = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{LibranzaE2E.Convenios}/{esc.Convenio}/concept-equivalences", new
        {
            validFrom = desde.AddDays(1), reason = "Equivalencias de la pagaduría (e2e)",
            equivalences = new object[]
            {
                new { paymasterConceptCode = "101", description = "Cuota de crédito", split = "Priority", fileGroupCode = "CRED",
                    members = new[] { new { nature = "Placement", lineCode = "*", concept = "Installment", priority = 1 } } },
                new { paymasterConceptCode = "201", description = "Aportes y ahorro permanente", split = "Priority", fileGroupCode = "APORTES",
                    members = new[]
                    {
                        new { nature = "Contribution", lineCode = "*", concept = "Installment", priority = 1 },
                        new { nature = "Deposit", lineCode = "AHPERM", concept = "Installment", priority = 2 },
                    } },
            },
        });
        equivalencias.StatusCode.Should().Be(HttpStatusCode.OK, await equivalencias.Content.ReadAsStringAsync());
        var leidas = await CarteraE2E.GetAsync(http, admin, $"{LibranzaE2E.Convenios}/{esc.Convenio}/concept-equivalences?date={desde.AddDays(1):yyyy-MM-dd}");
        leidas.EnumerateArray().Select(e => (e.GetProperty("paymasterConceptCode").GetString(), e.GetProperty("fileGroupCode").GetString()))
            .Should().Equal(("101", "CRED"), ("201", "APORTES"));

        var generar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, LibranzaE2E.Archivos,
            new { agreementPublicId = esc.Convenio, year = desde.AddMonths(1).Year, month = desde.AddMonths(1).Month });
        (await CarteraE2E.CodigoDeErrorAsync(generar)).Should().Be("Lending.PayrollFile.FileGroupsPending");
    }
}
