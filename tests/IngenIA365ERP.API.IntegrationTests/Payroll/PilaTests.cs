using System.Net;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Feature 010, US5 (T093; quickstart §3.5) por HTTP sobre la cooperativa compartida: catálogos con
/// código PILA, datos del aportante, empresa, dos empleados con fichas completas en un plan propio y
/// la nómina de marzo de 2027 aprobada → validar limpio (salvo las alertas del layout) → quitar la EPS
/// → bloqueante con enlace a la ficha → restaurar → generar → archivo de 358/693 posiciones por
/// renglón, CRLF, ASCII, con el cuadre balanceado → descargar → regenerar → versión 2 y la 1
/// reemplazada → sin permiso no se marca cargada (404) → marcar cargada → ya no se regenera.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class PilaTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/payroll/pila";

    [Fact]
    public async Task La_planilla_se_valida_genera_cuadra_descarga_versiona_y_se_marca_cargada()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // --- empresa (NIT para la cabecera), catálogos con código PILA y datos del aportante ---
        var empresa = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/companies",
            new { code = "COOP", name = "Cooperativa de prueba de nómina", shortName = "COOP", taxId = "900123456", taxIdCheckDigit = "7", city = "Cali" });
        empresa.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity]);
        var eps = await CatalogoAsync(http, admin, "/api/payroll/health-insurance", "EPSPILA", "EPS Sura (e2e)", "800088702", "EPS010");
        var afp = await CatalogoAsync(http, admin, "/api/payroll/pension-providers", "AFPPILA", "Porvenir (e2e)", "800144331", "230301");
        var arl = await CatalogoAsync(http, admin, "/api/payroll/work-risk", "ARLPILA", "ARL Sura (e2e)", "800256161", "14-23");
        var ccf = await CatalogoAsync(http, admin, "/api/payroll/family-compensation-funds", "CCFPILA", "Comfandi (e2e)", "890303093", "CCF24");
        var clases = (await NominaE2E.GetAsync(http, admin, "/api/payroll/work-risk-rates?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
        var claseI = clases.Single(c => c.GetProperty("code").GetInt32() == 1).GetProperty("publicId").GetGuid();

        var ajustes = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"{Ruta}/settings", new
        {
            contributorType = "1", contributorClass = "B", presentationForm = "U", arlPilaCode = "14-23", economicActivityCode = "1649501",
            divipolaDepartment = "76", divipolaMunicipality = "001", operatorCode = "24", operatorName = "Aportes en Línea",
        });
        ajustes.StatusCode.Should().Be(HttpStatusCode.OK, await ajustes.Content.ReadAsStringAsync());
        (await NominaE2E.LeerAsync(ajustes)).GetProperty("complete").GetBoolean().Should().BeTrue();
        (await NominaE2E.GetAsync(http, admin, $"{Ruta}/layouts")).EnumerateArray().Should().Contain(l => l.GetProperty("code").GetString() == "AT2-v30");

        // --- plan propio, dos empleados con ficha completa y la nómina de marzo de 2027 aprobada ---
        // El ejercicio contable de 2027: aprobar contabiliza el NM en el período del mes (otra prueba pudo abrirlo antes).
        var ejercicio2027 = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years", new { year = 2027 });
        ejercicio2027.StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity], await ejercicio2027.Content.ReadAsStringAsync());
        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "PILAE2E", name = "Plan PILA e2e", periodicity = "Monthly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        var planId = (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();
        var ana = await EmpleadoAsync(http, admin, "Anabel", "Arias", "Ávila", 2_000_000m, planId, eps, afp, arl, ccf, claseI);
        var gloria = await EmpleadoAsync(http, admin, "Gloria", "Gómez", "Giraldo", 8_000_000m, planId, eps, afp, arl, ccf, claseI);

        var periodo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods",
            new { planPublicId = planId, startDate = new DateTime(2027, 3, 1), endDate = new DateTime(2027, 3, 31), description = "Marzo 2027 (PILA e2e)", statusMessage = string.Empty });
        periodo.StatusCode.Should().Be(HttpStatusCode.Created, await periodo.Content.ReadAsStringAsync());
        var periodoId = (await NominaE2E.LeerAsync(periodo)).GetGuid();
        var calculada = await NominaE2E.CalcularAsync(http, admin, periodoId);
        await NominaE2E.AprobarAsync(http, admin, calculada.GetProperty("runPublicId").GetGuid());

        // ------------------------------------------------------------- validar --
        var fecha = await NominaE2E.GetAsync(http, admin, $"{Ruta}/2027/3/due-date");
        fecha.GetProperty("nitDigits").GetString().Should().Be("56");
        fecha.GetProperty("dueDate").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null, "la semilla trae PILA_PLAZO_PAGO_POR_NIT");

        var validacion = await Validar(http, admin);
        validacion.GetProperty("canGenerate").GetBoolean().Should().BeTrue(string.Join("; ", validacion.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("message").GetString())));
        validacion.GetProperty("contributors").GetInt32().Should().Be(2);
        validacion.GetProperty("issues").EnumerateArray().Should().Contain(i => i.GetProperty("code").GetString() == "Pila.LayoutSinCotejar");

        // Sin la EPS en la ficha: bloqueante con enlace a la ficha; se restaura y vuelve a validar limpio.
        await FichaAsync(http, admin, ana, 2_000_000m, null, afp, arl, ccf, claseI);
        var bloqueada = await Validar(http, admin);
        bloqueada.GetProperty("canGenerate").GetBoolean().Should().BeFalse();
        var sinEps = bloqueada.GetProperty("issues").EnumerateArray().Single(i => i.GetProperty("code").GetString() == "Pila.SinEps");
        sinEps.GetProperty("employeePublicId").GetGuid().Should().Be(ana);
        sinEps.GetProperty("link").GetString().Should().Be($"/nomina/empleados/{ana}");
        await FichaAsync(http, admin, ana, 2_000_000m, eps, afp, arl, ccf, claseI);
        (await Validar(http, admin)).GetProperty("canGenerate").GetBoolean().Should().BeTrue();
        (await NominaE2E.GetAsync(http, admin, $"{Ruta}?year=2027&month=3")).GetArrayLength().Should().Be(0, "validar no guarda");

        // -------------------------------------------------------------- generar --
        var sinReconocer = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/2027/3/generate", new { acknowledgeWarnings = false });
        sinReconocer.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(sinReconocer)).Should().Be("Payroll.Pila.WarningsNotAcknowledged");

        var generar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/2027/3/generate", new { acknowledgeWarnings = true });
        generar.StatusCode.Should().Be(HttpStatusCode.Created, await generar.Content.ReadAsStringAsync());
        var g1 = await NominaE2E.LeerAsync(generar);
        var gen1 = g1.GetProperty("generationPublicId").GetGuid();
        g1.GetProperty("version").GetInt32().Should().Be(1);
        g1.GetProperty("status").GetInt32().Should().Be(1, "Generated");
        g1.GetProperty("lines").GetInt32().Should().Be(2);
        g1.GetProperty("fileName").GetString().Should().Be("PILA_900123456_2027-03_v1.txt");
        // FR-027: la nómina redondea la ARL al múltiplo más cercano y la planilla al superior (Decreto 780/2016 art. 3.2.1.5):
        // la diferencia existe, se muestra por subsistema antes de descargar y descargar sin reconocerla se niega.
        var cuadre = g1.GetProperty("reconciliation");
        cuadre.GetProperty("balanced").GetBoolean().Should().BeFalse();
        var filasCuadre = cuadre.GetProperty("bySubsystem").EnumerateArray().ToList();
        filasCuadre.Should().OnlyContain(r => Math.Abs(r.GetProperty("difference").GetDecimal()) <= 100m, "la diferencia es sólo de redondeo");
        filasCuadre.Single(r => r.GetProperty("subsystem").GetString() == "Pension").GetProperty("difference").GetDecimal().Should().Be(0m);
        var totales = g1.GetProperty("totals");
        totales.GetProperty("fsp").GetDecimal().Should().Be(80_000m, "Gloria supera 4 SMMLV: 1 % de 8.000.000");
        totales.GetProperty("total").GetDecimal().Should().BeGreaterThan(0m);

        var detalle = await NominaE2E.GetAsync(http, admin, $"{Ruta}/{gen1}");
        var lineas = detalle.GetProperty("lines").EnumerateArray().ToList();
        lineas.Should().HaveCount(2);
        lineas.Select(l => l.GetProperty("employeePublicId").GetGuid()).Should().BeEquivalentTo([ana, gloria]);
        lineas.Should().OnlyContain(l => l.GetProperty("fields").GetProperty("33").GetString()!.StartsWith("EPS010"));
        var explicacion = await NominaE2E.GetAsync(http, admin, $"{Ruta}/{gen1}/lines/1/explanation");
        explicacion.GetProperty("recordText").GetString()!.Length.Should().Be(693);
        explicacion.GetProperty("fields").GetArrayLength().Should().Be(98);

        // ------------------------------------------------------------ descargar --
        // Feature 011 (§9): la planilla se baja con un enlace firmado, con las mismas reglas de antes.
        var sinReconocerDiferencia = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{gen1}/download-link", null);
        sinReconocerDiferencia.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(sinReconocerDiferencia)).Should().Be("Payroll.Pila.Unreconciled");
        var porLaApi = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"{Ruta}/{gen1}/file?acknowledgeDifference=true", null);
        porLaApi.StatusCode.Should().Be(HttpStatusCode.Conflict, "GET /file queda para las planillas del formato anterior");
        (await NominaE2E.CodigoDeErrorAsync(porLaApi)).Should().Be("Attachments.UseDownloadLink");
        using var descarga = await NominaE2E.BajarPorEnlaceAsync(http, admin, $"{Ruta}/{gen1}/download-link?acknowledgeDifference=true");
        descarga.StatusCode.Should().Be(HttpStatusCode.OK, await descarga.Content.ReadAsStringAsync());
        descarga.Content.Headers.ContentType!.CharSet.Should().Be("us-ascii");
        var bytes = await descarga.Content.ReadAsByteArrayAsync();
        bytes.Should().OnlyContain(b => b < 128);
        var renglones = Encoding.ASCII.GetString(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        renglones.Should().HaveCount(3);
        renglones[0].Should().HaveLength(358).And.StartWith("110001").And.Contain("2027-032027-04");
        renglones[1].Should().HaveLength(693).And.StartWith("200001");
        renglones[2].Should().HaveLength(693).And.StartWith("200002");
        (await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/pila-cuadre?generationId={gen1}")).GetProperty("filas").GetArrayLength().Should().Be(7);

        // ------------------------------------------ regenerar: versión 2, la 1 reemplazada --
        var generar2 = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/2027/3/generate", new { acknowledgeWarnings = true });
        generar2.StatusCode.Should().Be(HttpStatusCode.Created);
        var g2 = await NominaE2E.LeerAsync(generar2);
        g2.GetProperty("version").GetInt32().Should().Be(2);
        var gen2 = g2.GetProperty("generationPublicId").GetGuid();
        var versiones = (await NominaE2E.GetAsync(http, admin, $"{Ruta}?year=2027&month=3")).EnumerateArray().ToList();
        versiones.Single(v => v.GetProperty("generationPublicId").GetGuid() == gen1).GetProperty("status").GetInt32().Should().Be(3, "Superseded");
        (await NominaE2E.BajarPorEnlaceAsync(http, admin, $"{Ruta}/{gen1}/download-link?acknowledgeDifference=true")).StatusCode.Should().Be(HttpStatusCode.OK, "la versión reemplazada conserva su archivo");

        // ------------------------------------------------- sin permiso: 404 --
        var prohibido = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"{Ruta}/{gen2}/mark-uploaded", new { uploadedAt = new DateTime(2027, 4, 5), operatorReference = "X" });
        prohibido.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await NominaE2E.CodigoDeErrorAsync(prohibido)).Should().Be("Generic.NotFound");

        // ---------------------------------------------------------- marcar cargada --
        var cargada = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{gen2}/mark-uploaded", new { uploadedAt = new DateTime(2027, 4, 5, 9, 0, 0), operatorReference = "PL-2027-000456", paidAt = new DateOnly(2027, 4, 6) });
        cargada.StatusCode.Should().Be(HttpStatusCode.OK, await cargada.Content.ReadAsStringAsync());
        (await NominaE2E.GetAsync(http, admin, $"{Ruta}/{gen2}")).GetProperty("summary").GetProperty("operatorReference").GetString().Should().Be("PL-2027-000456");
        var otraVez = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/2027/3/generate", new { acknowledgeWarnings = true });
        otraVez.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Payroll.Pila.AlreadyUploaded");
    }

    // ------------------------------------------------------------------ helpers --

    private static async Task<System.Text.Json.JsonElement> Validar(HttpClient http, string token)
    {
        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, $"{Ruta}/2027/3/validate", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await NominaE2E.LeerAsync(resp);
    }

    private static async Task<Guid> CatalogoAsync(HttpClient http, string token, string ruta, string code, string name, string nit, string pilaCode)
    {
        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, ruta, new { code, name, shortName = code, taxId = nit, checkDigit = 1, pilaCode });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"{ruta}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(resp)).GetGuid();
    }

    private static async Task<Guid> EmpleadoAsync(HttpClient http, string token, string nombre, string apellido, string segundoApellido, decimal salario, Guid planId, Guid eps, Guid afp, Guid arl, Guid ccf, Guid clase)
    {
        var documento = (700_000_000 + Random.Shared.Next(1, 99_999_999)).ToString();
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = apellido, secondLastName = segundoApellido, email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();
        var empleado = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = salario, contractType = 1, hireDate = new DateTime(2026, 1, 15), payrollPlanPublicId = planId,
            healthInsurancePublicId = eps, pensionProviderPublicId = afp, workRiskProviderPublicId = arl, familyCompensationFundPublicId = ccf, workRiskRatePublicId = clase,
            pila = new { divipolaDepartment = "76", divipolaMunicipality = "001", economicActivityCode = "1649501" },
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado: «{await empleado.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(empleado)).GetGuid();
    }

    private static async Task FichaAsync(HttpClient http, string token, Guid empleadoId, decimal salario, Guid? eps, Guid afp, Guid arl, Guid ccf, Guid clase)
    {
        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Put, $"/api/payroll/employees/{empleadoId}", new
        {
            baseSalary = salario, contractType = 1, healthInsurancePublicId = eps, pensionProviderPublicId = afp, workRiskProviderPublicId = arl,
            familyCompensationFundPublicId = ccf, workRiskRatePublicId = clase, payrollBankAccountType = 1,
        });
        resp.IsSuccessStatusCode.Should().BeTrue(await resp.Content.ReadAsStringAsync());
    }
}
