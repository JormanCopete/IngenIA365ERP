using System.IO.Compression;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Feature 010 US2 (T053; quickstart §3.2): las cesantías e intereses del año por HTTP, sobre la
/// cooperativa compartida de la colección —plan de nómina propio para no cruzarse con los períodos
/// de las otras pruebas—: dos fondos con persona, tres empleados, un mes ordinario aprobado que deja
/// provisiones, calcular → duplicado rechazado → relación por fondo en borrador → aprobar con
/// <c>NM</c> <c>SeveranceRun</c> cuyo tercero de <c>CESANTIAS</c> es la persona del fondo y el de
/// <c>INT_CESANTIAS</c> el empleado → SC-003 sobre las provisiones (cuadre de la corrida) → relación
/// de pago de los intereses → consignación por fondo exportada en tres formatos con los mismos
/// totales y una hoja por fondo en Excel → marcar consignado (dos veces, 422) → los intereses pagados
/// bloquean la reversión → retirada la marca, se reversa y la consignación queda como historial.
/// El corte es el 31-08-2026, no el 31-12: el comprobante se fecha al corte y no puede ser posterior a
/// hoy (D-04), y la prueba corre el día que corre.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class CesantiasAnualesTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/payroll/settlements/severance";
    private static readonly DateOnly Corte = new(2026, 8, 31);

    private static readonly (string Formato, string TipoContenido, byte[] Firma)[] Formatos =
    [
        ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B]),
        ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B]),
        ("pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]),
    ];

    [Fact]
    public async Task Calcular_aprobar_con_el_fondo_como_tercero_consignar_por_fondo_pagar_intereses_y_reversar()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // --- la política por empresa (feature 010) manda sobre el parámetro heredado que fija NominaE2E: quien calcula puede aprobar con segunda confirmación ---
        var politica = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/company-policies/AllowSameUserApproval/versions", new
        {
            value = "true", validFrom = "2026-01-01", validTo = (string?)null, reason = "Pruebas e2e: una sola persona calcula y aprueba", closePrevious = true,
        });
        politica.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity], $"política: «{await politica.Content.ReadAsStringAsync()}»");

        // --- plan propio, dos fondos con persona, tres empleados ---
        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "CESANT", name = "Cesantías e2e", periodicity = "Monthly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, $"plan: «{await plan.Content.ReadAsStringAsync()}»");
        var planId = (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();

        var (porvenirId, porvenirPersona) = await CrearFondoAsync(http, admin, "PORVENIR", "Porvenir S.A.", "800144331");
        var (proteccionId, proteccionPersona) = await CrearFondoAsync(http, admin, "PROTECCION", "Protección S.A.", "800138188");

        var a = await CrearEmpleadoAsync(http, admin, "Alba", 2_500_000m, new DateTime(2024, 1, 1), planId, porvenirId);
        var f = await CrearEmpleadoAsync(http, admin, "Fabio", 2_000_000m, new DateTime(2024, 1, 1), planId, proteccionId);
        var g = await CrearEmpleadoAsync(http, admin, "Gema", 1_750_905m, new DateTime(2026, 6, 1), planId, porvenirId);
        var cambio = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/employees/{f}/salary-changes", new
        {
            employeePublicId = f, newSalary = 2_400_000m, effectiveFrom = "2026-07-01T00:00:00", reason = "Aumento de julio",
        });
        cambio.StatusCode.Should().Be(HttpStatusCode.Created, $"cambio de salario: «{await cambio.Content.ReadAsStringAsync()}»");

        // --- un mes ordinario aprobado del plan (junio) deja provisiones de cesantías e intereses de dónde cancelar (SC-003) ---
        var periodo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods", new
        {
            planPublicId = planId, startDate = "2026-06-01T00:00:00", endDate = "2026-06-30T00:00:00", description = "Cesantías e2e junio 2026", statusMessage = string.Empty,
        });
        periodo.StatusCode.Should().Be(HttpStatusCode.Created, $"período: «{await periodo.Content.ReadAsStringAsync()}»");
        var periodoId = (await NominaE2E.LeerAsync(periodo)).GetGuid();
        var ordinaria = await NominaE2E.CalcularAsync(http, admin, periodoId);
        var ordinariaId = ordinaria.GetProperty("runPublicId").GetGuid();
        ordinaria.GetProperty("employeeCount").GetInt32().Should().Be(3, "los tres del plan propio");
        await NominaE2E.AprobarAsync(http, admin, ordinariaId);

        // --- calcular las cesantías del año (corte anticipado ≤ hoy) ---
        var calculo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Ruta, new { year = 2026, cutoffDate = Corte.ToString("yyyy-MM-dd"), employeePublicIds = new[] { a, f, g } });
        calculo.StatusCode.Should().Be(HttpStatusCode.Created, $"calcular: «{await calculo.Content.ReadAsStringAsync()}»");
        var calculado = await NominaE2E.LeerAsync(calculo);
        var runId = calculado.GetProperty("runPublicId").GetGuid();
        calculado.GetProperty("kind").GetString().Should().Be("Severance");
        calculado.GetProperty("version").GetInt32().Should().Be(1);
        calculado.GetProperty("employees").GetInt32().Should().Be(3);
        calculado.GetProperty("blockers").GetArrayLength().Should().Be(0);
        calculado.GetProperty("excluded").GetArrayLength().Should().Be(0);
        var totalLiquidado = calculado.GetProperty("totals").GetProperty("net").GetDecimal();
        totalLiquidado.Should().BeGreaterThan(0m);

        var duplicado = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Ruta, new { year = 2026, cutoffDate = Corte.ToString("yyyy-MM-dd") });
        duplicado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(duplicado)).Should().Be("Payroll.Settlement.Duplicate");

        // --- la corrida se lee por las rutas de la 005 con su tipo, y cada línea trae su explicación ---
        var resumen = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}");
        resumen.GetProperty("kind").GetString().Should().Be("Severance");
        resumen.GetProperty("cutoffDate").GetString().Should().Be(Corte.ToString("yyyy-MM-dd"));
        resumen.GetProperty("year").GetInt32().Should().Be(2026);
        resumen.GetProperty("periodPublicId").ValueKind.Should().Be(JsonValueKind.Null, "una liquidación especial no tiene período");

        var detalleA = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/employees/{a}");
        var lineasA = detalleA.GetProperty("lines").EnumerateArray().ToList();
        var cesantiasA = lineasA.Single(l => l.GetProperty("conceptCode").GetString() == "CESANTIAS");
        var interesesA = lineasA.Single(l => l.GetProperty("conceptCode").GetString() == "INT_CESANTIAS");
        cesantiasA.GetProperty("amount").GetDecimal().Should().BeGreaterThan(0m);
        cesantiasA.GetProperty("quantity").GetDecimal().Should().Be(240m, "01-01 a 31-08 son 240 días comerciales");
        cesantiasA.GetProperty("explanation").GetProperty("steps").GetArrayLength().Should().BeGreaterThan(2, "la explicación va paso a paso");
        interesesA.GetProperty("amount").GetDecimal().Should().BeGreaterThan(0m);
        lineasA.Should().Contain(l => l.GetProperty("conceptCode").GetString() == "CESANTIAS_AJUSTE_PROV", "junio dejó provisión: la liquidación la cancela con el ajuste");

        var detalleF = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/employees/{f}");
        var cesantiasF = detalleF.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("conceptCode").GetString() == "CESANTIAS");
        cesantiasF.GetProperty("explanation").ToString().Should().Contain("promedio", "cambió de salario dentro de la ventana de estabilidad: se promedia el año");

        // --- la relación por fondo ya se revisa en borrador: dos fondos, totales = suma de las líneas CESANTIAS ---
        var relacion = await NominaE2E.GetAsync(http, admin, $"{Ruta}/{runId}/deposit-schedule");
        var fondos = relacion.GetProperty("funds").EnumerateArray().ToList();
        fondos.Should().HaveCount(2);
        var porvenir = fondos.Single(x => x.GetProperty("fundPublicId").GetGuid() == porvenirId);
        var proteccion = fondos.Single(x => x.GetProperty("fundPublicId").GetGuid() == proteccionId);
        porvenir.GetProperty("employees").GetInt32().Should().Be(2, "Alba y Gema");
        porvenir.GetProperty("fundNit").GetString().Should().Be("800144331", "el NIT de la persona del fondo");
        proteccion.GetProperty("employees").GetInt32().Should().Be(1);
        proteccion.GetProperty("total").GetDecimal().Should().Be(cesantiasF.GetProperty("amount").GetDecimal());
        var totalCesantias = fondos.Sum(x => x.GetProperty("total").GetDecimal());
        relacion.GetProperty("grandTotal").GetDecimal().Should().Be(totalCesantias);
        relacion.GetProperty("dueDate").GetString().Should().Be("2027-02-14", "CESANTIAS_FECHA_LIMITE_CONSIGNACION llevado al año siguiente");
        relacion.GetProperty("interestDueDate").GetString().Should().Be("2027-01-31");
        porvenir.GetProperty("depositedAt").ValueKind.Should().Be(JsonValueKind.Null);

        // --- marcar consignado antes de aprobar no cabe ---
        var prematuro = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/funds/{porvenirId}/mark-deposited", new { depositedAt = "2026-09-05", reference = "X" });
        prematuro.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(prematuro)).Should().Be("Payroll.Severance.NotApproved");

        // --- aprobar: comprobante NM SeveranceRun fechado al corte, CxP al fondo y al empleado ---
        var sinConfirmar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/approve", new { confirm = false });
        sinConfirmar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(sinConfirmar)).Should().Be("Payroll.Settlement.ConfirmationRequired");

        var aprobacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/approve", new
        {
            confirm = true, postingDate = (string?)null, payDate = "2026-09-15", confirmEmpty = false, confirmWithoutSegregation = true,
        });
        aprobacion.StatusCode.Should().Be(HttpStatusCode.OK, $"aprobar: «{await aprobacion.Content.ReadAsStringAsync()}»");
        var aprobada = await NominaE2E.LeerAsync(aprobacion);
        aprobada.GetProperty("number").GetString().Should().StartWith("NM-");
        aprobada.GetProperty("postingDate").GetString().Should().Be(Corte.ToString("yyyy-MM-dd"), "D-04: la fecha del comprobante es la del corte");
        var documentoId = aprobada.GetProperty("documentPublicId").GetGuid();

        var comprobante = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/{documentoId}");
        comprobante.GetProperty("origin").GetProperty("sourceType").GetString().Should().Be("SeveranceRun");
        comprobante.GetProperty("origin").GetProperty("sourcePublicId").GetGuid().Should().Be(runId);
        comprobante.GetProperty("status").GetString().Should().Be("Posted");
        comprobante.GetProperty("totalDebit").GetDecimal().Should().Be(comprobante.GetProperty("totalCredit").GetDecimal());
        var lineasNm = comprobante.GetProperty("lines").EnumerateArray().ToList();
        var cxpCesantias = lineasNm.Where(l => l.GetProperty("detail").GetString()!.StartsWith("Nómina CESANTIAS ") && l.GetProperty("credit").GetDecimal() > 0m).ToList();
        cxpCesantias.Should().HaveCount(2, "una cuenta por pagar por fondo");
        cxpCesantias.Select(l => l.GetProperty("personPublicId").GetGuid()).Should().BeEquivalentTo([porvenirPersona, proteccionPersona], "el tercero de las cesantías es la persona del fondo (FR-011, FR-088)");
        cxpCesantias.Sum(l => l.GetProperty("credit").GetDecimal()).Should().Be(totalCesantias, "la CxP a los fondos es el total de la relación");
        var cxpIntereses = lineasNm.Where(l => l.GetProperty("detail").GetString() == "Nómina INT_CESANTIAS" && l.GetProperty("credit").GetDecimal() > 0m).ToList();
        cxpIntereses.Select(l => l.GetProperty("personPublicId").GetGuid()).Should().BeEquivalentTo(await PersonasDeAsync(http, admin, a, f, g), "los intereses se deben a cada empleado");

        // --- SC-003: el cuadre de la corrida dice qué provisión se consumió y que el comprobante cuadra con las líneas ---
        var cuadre = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/balance-check");
        cuadre.GetProperty("accountingDocumentBalanced").GetBoolean().Should().BeTrue();
        var provisiones = cuadre.GetProperty("provision").EnumerateArray().ToList();
        var provCesantias = provisiones.Single(p => p.GetProperty("provisionCode").GetString() == "PROV_CESANTIAS");
        provCesantias.GetProperty("accrued").GetDecimal().Should().BeGreaterThan(0m, "junio provisionó cesantías");
        provCesantias.GetProperty("consumed").GetDecimal().Should().Be(totalCesantias, "la liquidación consume toda la provisión acumulada y lleva la diferencia al gasto");
        (provCesantias.GetProperty("accrued").GetDecimal() + provCesantias.GetProperty("difference").GetDecimal() - provCesantias.GetProperty("released").GetDecimal())
            .Should().Be(provCesantias.GetProperty("consumed").GetDecimal(), "acumulado + diferencia − liberado = consumido: el saldo de la provisión queda en cero");
        provisiones.Should().Contain(p => p.GetProperty("provisionCode").GetString() == "PROV_INT_CESANTIAS");

        // --- la lista del año: totales de cesantías e intereses, el comprobante y los fondos por consignar ---
        var lista = await NominaE2E.GetAsync(http, admin, $"{Ruta}?year=2026");
        var item = lista.EnumerateArray().Single(x => x.GetProperty("runPublicId").GetGuid() == runId);
        item.GetProperty("status").GetString().Should().Be("Approved");
        item.GetProperty("severanceTotal").GetDecimal().Should().Be(totalCesantias);
        item.GetProperty("interestTotal").GetDecimal().Should().Be(fondos.Sum(x => x.GetProperty("interestTotal").GetDecimal()));
        item.GetProperty("payDate").GetString().Should().Be("2026-09-15", "la fecha de pago de los intereses es propia");
        item.GetProperty("postedDocumentNumber").GetString().Should().StartWith("NM-");
        item.GetProperty("funds").EnumerateArray().Should().OnlyContain(x => x.GetProperty("depositedAt").ValueKind == JsonValueKind.Null);

        // --- relación de pago de los INTERESES: es la de la corrida; el neto es intereses menos retención ---
        var pagos = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/payments");
        pagos.GetProperty("employees").GetInt32().Should().Be(3);
        pagos.GetProperty("paidCount").GetInt32().Should().Be(0);
        pagos.GetProperty("totalNet").GetDecimal().Should().Be(resumen.GetProperty("totals").GetProperty("net").GetDecimal());

        // --- consignación por fondo en JSON y en tres formatos con los mismos totales; en Excel, una hoja por fondo ---
        var tabla = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/consignacion-cesantias?runId={runId}");
        var columnas = tabla.GetProperty("columnas").EnumerateArray().Select(c => c.GetProperty("nombre").GetString()).ToList();
        var indice = columnas.IndexOf("Cesantías a consignar");
        indice.Should().BeGreaterThan(0);
        tabla.GetProperty("totales").GetProperty("valores")[indice].GetDecimal().Should().Be(totalCesantias, "el reporte suma lo mismo que la relación y que la CxP");
        tabla.GetProperty("filas").EnumerateArray().Select(x => x.GetProperty("seccion").GetString()).Distinct().Should().HaveCount(2, "un bloque por fondo");
        tabla.GetProperty("notas").EnumerateArray().Should().Contain(n => n.GetString()!.Contains("14/02/2027"));
        foreach (var (formato, tipo, firma) in Formatos)
        {
            var resp = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/reports/payroll/consignacion-cesantias?runId={runId}&format={formato}", null);
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"{formato}: «{await resp.Content.ReadAsStringAsync()}»");
            resp.Content.Headers.ContentType!.MediaType.Should().Be(tipo);
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            bytes.Length.Should().BeGreaterThan(500);
            bytes.Take(firma.Length).Should().Equal(firma, $"{formato} es un archivo del tipo declarado");
            if (formato == "xlsx")
            {
                using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                zip.Entries.Count(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal)).Should().Be(3, "la relación completa más una hoja por fondo");
            }
        }
        var soloPorvenir = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/consignacion-cesantias?runId={runId}&fundId={porvenirId}");
        soloPorvenir.GetProperty("totales").GetProperty("valores")[indice].GetDecimal().Should().Be(porvenir.GetProperty("total").GetDecimal());

        // --- el archivo plano del fondo todavía no existe (N4): 422 con el código del contrato ---
        var archivo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"{Ruta}/{runId}/deposit-schedule/{porvenirId}/file", null);
        archivo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(archivo)).Should().Be("Payroll.Severance.FundFormatMissing");

        // --- marcar consignado: una vez por fondo; la segunda es 422 ---
        var marca = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/funds/{porvenirId}/mark-deposited", new { depositedAt = "2026-09-10", reference = "PLANILLA-2026-08" });
        marca.StatusCode.Should().Be(HttpStatusCode.OK, $"marcar consignado: «{await marca.Content.ReadAsStringAsync()}»");
        var consignacion = await NominaE2E.LeerAsync(marca);
        consignacion.GetProperty("amount").GetDecimal().Should().Be(porvenir.GetProperty("total").GetDecimal());
        consignacion.GetProperty("depositedBy").GetString().Should().Be(NominaE2E.CorreoAdmin);

        var repetida = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/funds/{porvenirId}/mark-deposited", new { depositedAt = "2026-09-11", reference = (string?)null });
        repetida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(repetida)).Should().Be("Payroll.Severance.AlreadyDeposited");

        relacion = await NominaE2E.GetAsync(http, admin, $"{Ruta}/{runId}/deposit-schedule");
        relacion.GetProperty("funds").EnumerateArray().Single(x => x.GetProperty("fundPublicId").GetGuid() == porvenirId).GetProperty("depositedAt").GetString().Should().Be("2026-09-10");
        relacion.GetProperty("funds").EnumerateArray().Single(x => x.GetProperty("fundPublicId").GetGuid() == proteccionId).GetProperty("depositedAt").ValueKind.Should().Be(JsonValueKind.Null);
        var tablaMarcada = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/consignacion-cesantias?runId={runId}&fundId={porvenirId}");
        tablaMarcada.GetProperty("filas")[0].GetProperty("valores")[columnas.IndexOf("Estado")].GetString().Should().Be("Consignado el 10-09-2026, ref. PLANILLA-2026-08");

        // --- los intereses pagados bloquean la reversión; retirada la marca, se reversa y la consignación queda como historial ---
        var pago = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/payments", new
        {
            employeePublicIds = new[] { a }, paidAt = "2026-09-15T00:00:00", method = "Transfer", reference = "INT-2026",
        });
        pago.StatusCode.Should().Be(HttpStatusCode.OK, $"pagar intereses: «{await pago.Content.ReadAsStringAsync()}»");

        var bloqueada = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/reverse", new { reason = "Salario mal registrado" });
        bloqueada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(bloqueada)).Should().Be("Payroll.PaymentBlocksReversal");

        var retiro = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/payments/{a}/revert", new { reason = "Transferencia devuelta" });
        retiro.IsSuccessStatusCode.Should().BeTrue($"retirar marca: «{await retiro.Content.ReadAsStringAsync()}»");

        var reversion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/reverse", new { reason = "Salario mal registrado" });
        reversion.StatusCode.Should().Be(HttpStatusCode.OK, $"reversar: «{await reversion.Content.ReadAsStringAsync()}»");
        (await NominaE2E.LeerAsync(reversion)).GetProperty("reversalNumber").GetString().Should().StartWith("NM-");

        lista = await NominaE2E.GetAsync(http, admin, $"{Ruta}?year=2026");
        item = lista.EnumerateArray().Single(x => x.GetProperty("runPublicId").GetGuid() == runId);
        item.GetProperty("status").GetString().Should().Be("Reversed");
        item.GetProperty("funds").EnumerateArray().Single(x => x.GetProperty("fundPublicId").GetGuid() == porvenirId).GetProperty("depositedAt").GetString()
            .Should().Be("2026-09-10", "la consignación ocurrió aunque el asiento se reverse: queda como historial (data-model §2.7a)");

        // --- tras reversar se admite liquidar de nuevo, como versión 2 ---
        var otraVez = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Ruta, new { year = 2026, cutoffDate = Corte.ToString("yyyy-MM-dd"), employeePublicIds = new[] { a, f, g } });
        otraVez.StatusCode.Should().Be(HttpStatusCode.Created, $"recalcular tras reversar: «{await otraVez.Content.ReadAsStringAsync()}»");
        (await NominaE2E.LeerAsync(otraVez)).GetProperty("version").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Sin_permiso_de_calcular_la_ruta_no_existe_pero_la_lista_se_ve()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();

        var lectura = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, $"{Ruta}?year=2026", null);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK, "sólo lectura tiene Payroll.Severance.View");

        var calculo = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, Ruta, new { year = 2025 });
        calculo.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin Payroll.Severance.Calculate la ruta no existe (Generic.NotFound)");

        var marca = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"{Ruta}/{Guid.NewGuid()}/funds/{Guid.NewGuid()}/mark-deposited", new { depositedAt = "2026-09-10", reference = "X" });
        marca.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin Payroll.Severance.MarkDeposited tampoco");
    }

    // ------------------------------------------------------------------- datos --

    private static int _documento = 800_100_000;

    private static async Task<(Guid FundId, Guid PersonId)> CrearFondoAsync(HttpClient http, string token, string codigo, string nombre, string nit)
    {
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "N", taxId = nit, firstName = nombre, lastName = "S.A.", businessName = nombre, personType = "J", isThirdParty = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona del fondo {nombre}: «{await persona.Content.ReadAsStringAsync()}»");
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();

        var fondo = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/severance-providers", new
        {
            code = codigo, name = nombre, shortName = codigo, taxId = nit, checkDigit = 0, personPublicId = personaId,
        });
        fondo.StatusCode.Should().Be(HttpStatusCode.Created, $"fondo {nombre}: «{await fondo.Content.ReadAsStringAsync()}»");
        return ((await NominaE2E.LeerAsync(fondo)).GetGuid(), personaId);
    }

    /// <summary>Persona + ficha en el plan propio y con su fondo de cesantías (<c>EmployeeInput.SeveranceProviderPublicId</c>, <c>PayrollPlanPublicId</c>).</summary>
    private static async Task<Guid> CrearEmpleadoAsync(HttpClient http, string token, string nombre, decimal salario, DateTime ingreso, Guid planId, Guid fondoId)
    {
        var documento = Interlocked.Increment(ref _documento).ToString();
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Cesantias", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test",
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();

        var empleado = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = salario, contractType = 1, hireDate = ingreso, payrollPlanPublicId = planId, severanceProviderPublicId = fondoId,
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado {nombre}: «{await empleado.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(empleado)).GetGuid();
    }

    private static async Task<List<Guid>> PersonasDeAsync(HttpClient http, string token, params Guid[] empleados)
    {
        var personas = new List<Guid>();
        foreach (var e in empleados)
            personas.Add((await NominaE2E.GetAsync(http, token, $"/api/payroll/employees/{e}")).GetProperty("personPublicId").GetGuid());
        return personas;
    }
}
