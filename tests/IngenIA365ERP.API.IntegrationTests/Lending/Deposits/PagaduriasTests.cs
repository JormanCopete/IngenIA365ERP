using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T095 y T091 (quickstart §3; contracts/api.md §18.3; contrato-de-cuentas §6.4; mensajes.md §5.4 y §5.7; decisión del dueño del 2026-10-18,
/// D36): los grupos de archivo y las equivalencias de concepto de un convenio por HTTP, sobre el escenario de libranza de la 013
/// (<see cref="LibranzaE2E.EscenarioAsync"/>, cooperativa aislada «pagadurias»).
/// <list type="number">
/// <item>Fijar dos grupos (créditos; aportes y ahorros) registra una versión nueva con grupos; un código que agrupa la cuota de aportes y la de ahorro
/// permanente con reparto por prelación queda en su grupo; un ítem en dos códigos responde <c>.ConceptMappedTwice</c>.</item>
/// <item>Generar exige el grupo (<c>Lending.PayrollFile.FileGroupRequired</c>); el de créditos sale con el formato del grupo y el código de la pagaduría
/// en cada línea. El de aportes responde <c>Lending.PayrollFile.Empty</c>: el escenario de libranza no tiene asociados con cuentas de aportes cuya
/// empresa empleadora sea esta pagaduría (las cuotas de cuentas las prueban <c>ArchivoPorGrupoTests</c> y el reparto puro).</item>
/// <item>Una respuesta que descuenta menos por el código: lo cubierto se aplica por prelación y el resto queda exigible como no aplicado de la
/// pagaduría.</item>
/// <item>Una fila de un empleado que el archivo no tiene queda en «no aplicados» de la respuesta (<c>.EmployeeWithoutEquivalence</c>) y no bloquea lo
/// demás.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class PagaduriasTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Grupos_y_equivalencias_generan_un_archivo_por_grupo_y_la_respuesta_se_reparte_por_codigo()
    {
        var esc = await LibranzaE2E.EscenarioAsync(fx, "pagadurias");
        using var http = fx.CreateClient();
        var accesos = await LibranzaE2E.AccesosAsync(http, esc);
        var admin = accesos.Admin;
        var (_, numero, _, empleado) = await LibranzaE2E.CreditoPorLibranzaAsync(http, esc, accesos, "Deudora de pagadurías");
        var desde = esc.Hoy.AddMonths(1);

        // Los formatos del grupo: la ida lleva el empleado y el código de la pagaduría; la vuelta, lo descontado por empleado y por código.
        var ida = await LibranzaE2E.FormatoAsync(http, admin, "GRPIDA", "PayrollDeductionRequest", esc.Hoy,
            ("Empleado", "PayerEmployeeId", true), ("Codigo", "PaymasterConceptCode", true), ("Credito", "LoanNumber", false),
            ("Cuenta", "AccountNumber", false), ("Valor", "Amount", true), ("Novedad", "NoveltyKind", true));
        var vuelta = await LibranzaE2E.FormatoAsync(http, admin, "GRPVUE", "PayrollDeductionResult", esc.Hoy,
            ("Empleado", "PayerEmployeeId", true), ("Codigo", "PaymasterConceptCode", true), ("Descontado", "DeductedAmount", true),
            ("Fecha", "DeductionDate", false));

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

        // Paso 2: un archivo por grupo.
        var periodo = desde.AddMonths(1);
        var sinGrupo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, LibranzaE2E.Archivos,
            new { agreementPublicId = esc.Convenio, year = periodo.Year, month = periodo.Month });
        (await CarteraE2E.CodigoDeErrorAsync(sinGrupo)).Should().Be("Lending.PayrollFile.FileGroupRequired");
        var deAportes = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, LibranzaE2E.Archivos,
            new { agreementPublicId = esc.Convenio, year = periodo.Year, month = periodo.Month, fileGroupCode = "APORTES" });
        (await CarteraE2E.CodigoDeErrorAsync(deAportes)).Should().Be("Lending.PayrollFile.Empty");
        var deCreditos = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, LibranzaE2E.Archivos,
            new { agreementPublicId = esc.Convenio, year = periodo.Year, month = periodo.Month, fileGroupCode = "CRED" });
        deCreditos.StatusCode.Should().Be(HttpStatusCode.Created, await deCreditos.Content.ReadAsStringAsync());
        var archivo = (await CarteraE2E.LeerAsync(deCreditos)).GetProperty("publicId").GetGuid();
        var (cabecera, lineas) = await LibranzaE2E.ArchivoAsync(http, admin, archivo);
        cabecera.GetProperty("fileGroupCode").GetString().Should().Be("CRED");
        var linea = lineas.Single(l => l.GetProperty("loanNumber").GetString() == numero);
        linea.GetProperty("paymasterConceptCode").GetString().Should().Be("101");
        var total = linea.GetProperty("total").GetDecimal();

        // Pasos 3 y 4: la pagaduría descuenta menos por el código 101 y nombra a un empleado que el archivo no tiene.
        var parcial = Math.Round(total / 2m, 2);
        await ResponderPorCodigoAsync(http, esc, archivo, (empleado, "101", parcial, periodo), ("EMP-NOEXISTE", "101", 10_000m, periodo));

        var noAplicados = (await CarteraE2E.GetAsync(http, admin,
                $"{LibranzaE2E.NoAplicados}?agreementPublicId={esc.Convenio}&origin=PaymasterResult&status=Open"))
            .GetProperty("items").EnumerateArray().ToList();
        noAplicados.Should().Contain(n => n.GetProperty("loanNumber").GetString() == numero, "lo no cubierto queda exigible como no aplicado de la pagaduría");
        var sinEquivalencia = (await CarteraE2E.GetAsync(http, admin, $"{LibranzaE2E.Archivos}/{archivo}/unmatched")).EnumerateArray().ToList();
        sinEquivalencia.Should().ContainSingle().Which.GetProperty("reasonCode").GetString().Should().Be("Lending.PayrollFile.EmployeeWithoutEquivalence");
    }

    /// <summary>
    /// La respuesta de la pagaduría en el formato de vuelta del grupo (empleado;código;descontado;fecha), cargada en Recaudo real, que la emite como v2 (feature 015,
    /// T0300; antes la emitía el recaudador de prueba).
    /// </summary>
    private async Task ResponderPorCodigoAsync(HttpClient http, LibranzaE2E.Escenario esc, Guid archivo,
        params (string Empleado, string Codigo, decimal Descontado, DateOnly Fecha)[] filas)
    {
        var texto = string.Join("\n", filas.Select(f =>
            $"{f.Empleado};{f.Codigo};{f.Descontado.ToString("0.00", CultureInfo.InvariantCulture)};{f.Fecha.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}")) + "\n";
        await LibranzaE2E.ResponderEnRecaudoAsync(fx, http, esc, archivo, texto, "respuesta-por-codigo.csv", "GRPVUE");
    }
}
