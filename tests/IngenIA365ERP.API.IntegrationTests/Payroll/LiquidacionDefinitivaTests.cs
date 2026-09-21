using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Payroll.NominaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Feature 010, US3 (quickstart §3.3, T072): la liquidación definitiva recorrida por HTTP sobre la
/// cooperativa compartida de nómina. Un plan quincenal propio para no cruzarse con los períodos
/// mensuales de las demás pruebas; el empleado H con una libranza con cuotas causadas sin
/// descontar (no hay endpoint HTTP para parametrizar líneas de crédito ni desembolsar en Cartera, así
/// que el recaudo por <c>ProcessPaymentCommand</c> lo fijan las pruebas de Application):
/// FR-021 con una quincena aprobada → registrar → propuesta con desglose → bajar con motivo (subir →
/// 422) → aprobar (comprobante <c>SettlementRun</c>, ficha retirada, PDF) → la quincena siguiente no lo
/// incluye → reversar reabre la ficha → la ruta vieja responde 404 → auditoría → reportes → permisos.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class LiquidacionDefinitivaTests(CentralIdentityApiFixture fx)
{
    private const string Terminaciones = "/api/payroll/settlements/terminations";
    private static int _sufijo = 100;

    [Fact]
    public async Task Ciclo_completo_de_la_definitiva_por_HTTP()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // --- un plan quincenal propio y el empleado H en él ---
        var sufijo = Interlocked.Increment(ref _sufijo);
        var plan = await EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = $"DEF{sufijo}", name = $"Definitiva {sufijo}", periodicity = "Biweekly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        var planId = (await LeerAsync(plan)).GetProperty("publicId").GetGuid();

        var documento = (900_000_000 + sufijo).ToString();
        var persona = await EnviarAsync(http, admin, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = "Hernán", lastName = "Definitiva", email = $"hernan.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, await persona.Content.ReadAsStringAsync());
        var personaId = (await LeerAsync(persona)).GetGuid();
        // Con EPS y fondo de pensiones: la definitiva liquida aportes de ley y sin afiliación el motor se niega (bloqueo, no aviso).
        var eps = await EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/health-insurance", new { code = $"EPS{sufijo}", name = $"EPS de prueba {sufijo}", taxId = $"8001{sufijo:00000}", checkDigit = 1 });
        eps.StatusCode.Should().Be(HttpStatusCode.Created, await eps.Content.ReadAsStringAsync());
        var afp = await EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pension-providers", new { code = $"AFP{sufijo}", name = $"Fondo de prueba {sufijo}", taxId = $"8002{sufijo:00000}", checkDigit = 2 });
        afp.StatusCode.Should().Be(HttpStatusCode.Created, await afp.Content.ReadAsStringAsync());
        var empleado = await EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = 2_400_000m, contractType = 1, hireDate = new DateTime(2024, 10, 1), payrollPlanPublicId = planId,
            healthInsurancePublicId = (await LeerAsync(eps)).GetGuid(), pensionProviderPublicId = (await LeerAsync(afp)).GetGuid(),
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, await empleado.Content.ReadAsStringAsync());
        var empleadoId = (await LeerAsync(empleado)).GetGuid();

        // --- tres quincenas del plan: la última de agosto (se aprueba), la primera y la segunda de septiembre ---
        var q0 = await PeriodoAsync(http, admin, planId, new DateTime(2026, 8, 16), new DateTime(2026, 8, 31));
        var q1 = await PeriodoAsync(http, admin, planId, new DateTime(2026, 9, 1), new DateTime(2026, 9, 15));
        var q2 = await PeriodoAsync(http, admin, planId, new DateTime(2026, 9, 16), new DateTime(2026, 9, 30));
        var corridaQ0 = await CalcularAsync(http, admin, q0);
        await AprobarAsync(http, admin, corridaQ0.GetProperty("runPublicId").GetGuid());

        // --- libranza con un tercero: cuota de 50.000 desde la quincena aprobada; dos cuotas causadas, ninguna descontada ---
        var libranza = await EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/recurring-novelties", new
        {
            employeePublicId = empleadoId, conceptCode = "LIBRANZA", amount = 50_000m, startDate = new DateTime(2026, 8, 16), totalInstallments = 12, notes = "Coopcentral",
        });
        libranza.StatusCode.Should().Be(HttpStatusCode.Created, await libranza.Content.ReadAsStringAsync());

        // --- FR-021: una fecha dentro de la quincena aprobada se rechaza y dice cuál es el período abierto ---
        var enAprobada = await EnviarAsync(http, admin, HttpMethod.Post, Terminaciones, new
        {
            employeePublicId = empleadoId, terminationDate = new DateOnly(2026, 8, 20), reasonCode = "RENUNCIA", contractType = "Indefinite",
        });
        enAprobada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await enAprobada.Content.ReadAsStringAsync());
        var errorFr021 = await LeerAsync(enAprobada);
        errorFr021.GetProperty("code").GetString().Should().Be("Payroll.Termination.PeriodApproved");
        errorFr021.GetProperty("data").GetProperty("periodPublicId").GetGuid().Should().Be(q0);
        errorFr021.GetProperty("data").GetProperty("openPeriodPublicId").GetGuid().Should().Be(q1);

        // --- registrar la terminación: despido sin justa causa el 15-09-2026, contrato indefinido ---
        var registro = await EnviarAsync(http, admin, HttpMethod.Post, Terminaciones, new
        {
            employeePublicId = empleadoId, terminationDate = new DateOnly(2026, 9, 15), reasonCode = "DESP_SINJC", contractType = "Indefinite", notes = "Prueba e2e",
        });
        registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
        var registrada = await LeerAsync(registro);
        var runId = registrada.GetProperty("runPublicId").GetGuid();
        var terminacionId = registrada.GetProperty("terminationPublicId").GetGuid();
        var lineas = registrada.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("conceptCode").GetString()!, l => l.GetProperty("amount").GetDecimal());
        lineas["SALARIO_PENDIENTE"].Should().Be(1_200_000m, "del 1 al 15 de septiembre sobre 2.400.000");
        // 705 días comerciales del 01-10-2024 al 15-09-2026: 30 del primer año + 345 × 20 / 360 = 49,1667 días × 80.000 = 3.933.333.
        lineas["INDEMNIZACION"].Should().Be(3_933_333m);
        lineas.Should().ContainKey("PRIMA").And.ContainKey("CESANTIAS").And.ContainKey("INT_CESANTIAS").And.ContainKey("VACACIONES_COMP");
        var ficha = await GetAsync(http, admin, $"/api/payroll/employees/{empleadoId}");
        ficha.GetProperty("status").GetInt32().Should().Be(1, "la ficha sigue vigente hasta aprobar");
        ficha.GetProperty("termination").GetProperty("status").GetInt32().Should().Be(0);

        // --- la propuesta de descuentos con su desglose ---
        var descuentos = await GetAsync(http, admin, $"{Terminaciones}/{runId}/deductions");
        var item = descuentos.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("kind").GetInt32().Should().Be(2, "libranza con tercero");
        item.GetProperty("proposed").GetDecimal().Should().Be(100_000m, "dos cuotas causadas (las quincenas del 16-08 y del 01-09) y ninguna descontada");
        item.GetProperty("applied").GetDecimal().Should().Be(100_000m);
        item.GetProperty("causedNotDeducted").GetInt32().Should().Be(2);
        descuentos.GetProperty("netAfterDeductions").GetDecimal().Should().Be(descuentos.GetProperty("net").GetDecimal() - 100_000m);
        var obligacionId = item.GetProperty("obligationPublicId").GetGuid();

        // --- subir se rechaza; bajar exige motivo y queda auditado ---
        var subir = await EnviarAsync(http, admin, HttpMethod.Put, $"{Terminaciones}/{runId}/deductions/{obligacionId}", new { applied = 150_000m, reason = "más" });
        subir.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(subir)).Should().Be("Payroll.Settlement.DeductionAboveProposed");
        var sinMotivo = await EnviarAsync(http, admin, HttpMethod.Put, $"{Terminaciones}/{runId}/deductions/{obligacionId}", new { applied = 50_000m, reason = "" });
        (await CodigoDeErrorAsync(sinMotivo)).Should().Be("Payroll.Settlement.DeductionReasonRequired");
        var bajar = await EnviarAsync(http, admin, HttpMethod.Put, $"{Terminaciones}/{runId}/deductions/{obligacionId}", new { applied = 50_000m, reason = "Una cuota la paga directo al tercero" });
        bajar.StatusCode.Should().Be(HttpStatusCode.OK, await bajar.Content.ReadAsStringAsync());
        (await LeerAsync(bajar)).GetProperty("applied").GetDecimal().Should().Be(50_000m);
        var resumen = await GetAsync(http, admin, $"/api/payroll/runs/{runId}");
        resumen.GetProperty("version").GetInt32().Should().Be(1, "el ajuste no crea versión");
        resumen.GetProperty("totals").GetProperty("net").GetDecimal().Should().Be(registrada.GetProperty("totals").GetProperty("net").GetDecimal() + 50_000m);
        resumen.GetProperty("kind").GetString().Should().Be("Settlement");

        // --- el documento para firma sale como borrador antes de aprobar ---
        var borrador = await EnviarAsync(http, admin, HttpMethod.Get, $"{Terminaciones}/{runId}/document", null);
        borrador.StatusCode.Should().Be(HttpStatusCode.OK, await borrador.Content.ReadAsStringAsync());
        borrador.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        borrador.Content.Headers.ContentDisposition!.FileName!.Should().Contain("borrador");

        // --- sanción moratoria informativa: existe, nunca es línea ---
        var sancion = await GetAsync(http, admin, $"{Terminaciones}/{runId}/late-payment-penalty");
        sancion.GetProperty("capMonths").GetInt32().Should().BePositive();
        sancion.GetProperty("dailyRate").GetDecimal().Should().Be(80_000m);
        lineas.Keys.Should().NotContain(k => k.Contains("SANCION"));

        // --- la ruta ordinaria no aprueba una definitiva ---
        var porLaOrdinaria = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/approve", new { confirm = true, exceptions = Array.Empty<object>(), confirmWithoutSegregation = true });
        (await CodigoDeErrorAsync(porLaOrdinaria)).Should().Be("Payroll.Settlement.UseSettlementRoute");

        // --- aprobar: comprobante SettlementRun, ficha retirada, persona sin la bandera, PDF definitivo ---
        var aprobar = await EnviarAsync(http, admin, HttpMethod.Post, $"{Terminaciones}/{runId}/approve", new { confirm = true, confirmWithoutSegregation = true });
        aprobar.StatusCode.Should().Be(HttpStatusCode.OK, await aprobar.Content.ReadAsStringAsync());
        var aprobada = await LeerAsync(aprobar);
        aprobada.GetProperty("number").GetString().Should().StartWith("NM-");
        aprobada.GetProperty("portfolioPayments").GetArrayLength().Should().Be(0, "la libranza no pasa por Cartera");
        aprobada.TryGetProperty("settlementDocumentAttachmentPublicId", out _).Should().BeTrue();

        resumen = await GetAsync(http, admin, $"/api/payroll/runs/{runId}");
        resumen.GetProperty("status").GetString().Should().Be("Approved");
        resumen.GetProperty("accountingDocumentNumber").GetString().Should().Be(aprobada.GetProperty("number").GetString());
        var comprobante = await GetAsync(http, admin, $"/api/accounting/documents/{resumen.GetProperty("accountingDocumentPublicId").GetGuid()}");
        comprobante.GetProperty("origin").GetProperty("sourceType").GetString().Should().Be("SettlementRun");
        var cuadre = await GetAsync(http, admin, $"/api/payroll/runs/{runId}/balance-check");
        cuadre.GetProperty("accountingDocumentBalanced").GetBoolean().Should().BeTrue("SC-003: toda liquidación aprobada tiene comprobante que cuadra");

        ficha = await GetAsync(http, admin, $"/api/payroll/employees/{empleadoId}");
        ficha.GetProperty("status").GetInt32().Should().Be(-1);
        ficha.GetProperty("terminationDate").GetDateTime().Date.Should().Be(new DateTime(2026, 9, 15));
        ficha.GetProperty("terminationCause").GetString().Should().Be("Despido sin justa causa");
        ficha.GetProperty("termination").GetProperty("status").GetInt32().Should().Be(1);
        (await GetAsync(http, admin, $"/api/core/people/{personaId}")).GetProperty("isEmployee").GetBoolean().Should().BeFalse("terminar apaga sólo «Empleado»");
        (await EnviarAsync(http, admin, HttpMethod.Get, $"/api/payroll/employees/by-person/{personaId}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "sin ficha viva");

        var pdf = await EnviarAsync(http, admin, HttpMethod.Get, $"{Terminaciones}/{runId}/document", null);
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentDisposition!.FileName!.Should().NotContain("borrador");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        // --- la lista lo muestra liquidado y la quincena siguiente no lo incluye ---
        var lista = await GetAsync(http, admin, $"{Terminaciones}?year=2026&employeeId={empleadoId}");
        var fila = lista.EnumerateArray().Should().ContainSingle().Subject;
        fila.GetProperty("status").GetInt32().Should().Be(1);
        fila.GetProperty("runPublicId").GetGuid().Should().Be(runId);
        var corridaQ2 = await CalcularAsync(http, admin, q2);
        var empleadosQ2 = await GetAsync(http, admin, $"/api/payroll/runs/{corridaQ2.GetProperty("runPublicId").GetGuid()}/employees");
        empleadosQ2.EnumerateArray().Should().NotContain(e => e.GetProperty("employeePublicId").GetGuid() == empleadoId, "retirado con definitiva aprobada antes del período");

        // --- una definitiva aprobada no se recalcula ni se registra otra terminación ---
        (await CodigoDeErrorAsync(await EnviarAsync(http, admin, HttpMethod.Post, $"{Terminaciones}/{runId}/recalculate", null))).Should().Be("Payroll.Settlement.NotDraft");
        var otra = await EnviarAsync(http, admin, HttpMethod.Post, Terminaciones, new { employeePublicId = empleadoId, terminationDate = new DateOnly(2026, 9, 15), reasonCode = "RENUNCIA", contractType = "Indefinite" });
        (await CodigoDeErrorAsync(otra)).Should().Be("Payroll.Termination.EmployeeAlreadyTerminated");

        // --- reversar: espejo, ficha reabierta, recaudos de Cartera listados (aquí ninguno) ---
        var reversar = await EnviarAsync(http, admin, HttpMethod.Post, $"{Terminaciones}/{runId}/reverse", new { reason = "Se equivocó la fecha de retiro" });
        reversar.StatusCode.Should().Be(HttpStatusCode.OK, await reversar.Content.ReadAsStringAsync());
        var reversada = await LeerAsync(reversar);
        reversada.GetProperty("reversalNumber").GetString().Should().StartWith("NM-");
        reversada.GetProperty("portfolioPayments").GetArrayLength().Should().Be(0);
        reversada.GetProperty("message").GetString().Should().Contain("vigente");
        ficha = await GetAsync(http, admin, $"/api/payroll/employees/{empleadoId}");
        ficha.GetProperty("status").GetInt32().Should().Be(1);
        ficha.GetProperty("termination").ValueKind.Should().Be(JsonValueKind.Null, "la terminación quedó reintegrada: ya no es la viva");
        (await GetAsync(http, admin, $"/api/core/people/{personaId}")).GetProperty("isEmployee").GetBoolean().Should().BeTrue();
        (await GetAsync(http, admin, $"/api/payroll/runs/{runId}")).GetProperty("status").GetString().Should().Be("Reversed");

        // --- la ruta vieja se retiró sin alias ---
        var vieja = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/employees/{empleadoId}/terminate", new { terminationDate = new DateTime(2026, 9, 15), terminationCause = "Renuncia" });
        vieja.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // --- auditoría: ajuste del descuento, retiro y reintegro ---
        (await EsperarEventoAsync(http, admin, "Payroll.Settlement.DeductionAdjusted", obligacionId.ToString())).Should().BeTrue("el ajuste del descuento queda en la auditoría");
        (await EsperarEventoAsync(http, admin, "Payroll.Employee.Terminated", terminacionId.ToString())).Should().BeTrue();
        (await EsperarEventoAsync(http, admin, "Payroll.Employee.Reinstated", runId.ToString())).Should().BeTrue();

        // --- centro de reportes: terminaciones del año y saldos iniciales ---
        var reporte = await GetAsync(http, admin, "/api/reports/payroll/terminaciones?desde=2026-01-01&hasta=2026-12-31");
        reporte.GetProperty("filas").EnumerateArray().Should().Contain(f => f.GetProperty("valores")[1].GetString() == documento);
        var excel = await EnviarAsync(http, admin, HttpMethod.Get, "/api/reports/payroll/terminaciones?desde=2026-01-01&hasta=2026-12-31&format=xlsx", null);
        excel.StatusCode.Should().Be(HttpStatusCode.OK, await excel.Content.ReadAsStringAsync());
        (await GetAsync(http, admin, "/api/reports/payroll/saldos-iniciales-prestaciones")).GetProperty("columnas").GetArrayLength().Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task El_catalogo_de_motivos_protege_las_marcas_legales_y_admite_motivos_propios()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var motivos = await GetAsync(http, admin, $"{Terminaciones}/reasons");
        var despido = motivos.EnumerateArray().Single(m => m.GetProperty("code").GetString() == "DESP_SINJC");
        despido.GetProperty("generatesSeverancePay").GetBoolean().Should().BeTrue();
        despido.GetProperty("isSeeded").GetBoolean().Should().BeTrue();
        var despidoId = despido.GetProperty("publicId").GetGuid();

        var quitarMarca = await EnviarAsync(http, admin, HttpMethod.Put, $"{Terminaciones}/reasons/{despidoId}", new
        {
            publicId = despidoId, code = "DESP_SINJC", name = "Despido sin justa causa", generatesSeverancePay = false, requiresContractEndDate = true, legalBasis = "CST art. 64",
        });
        (await CodigoDeErrorAsync(quitarMarca)).Should().Be("Payroll.Termination.ReasonSeeded");

        var codigo = $"PRUEBA{Interlocked.Increment(ref _sufijo)}";
        var propio = await EnviarAsync(http, admin, HttpMethod.Post, $"{Terminaciones}/reasons", new { code = codigo.ToLowerInvariant(), name = "Motivo propio de la cooperativa", generatesSeverancePay = false, requiresContractEndDate = false });
        propio.StatusCode.Should().Be(HttpStatusCode.Created, await propio.Content.ReadAsStringAsync());
        var conIndemnizacion = await EnviarAsync(http, admin, HttpMethod.Post, $"{Terminaciones}/reasons", new { code = $"X{codigo}", name = "No puede indemnizar", generatesSeverancePay = true });
        conIndemnizacion.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "es una regla de negocio con código propio, no una validación de forma (revisión de N1)");
        (await CodigoDeErrorAsync(conIndemnizacion)).Should().Be("Payroll.Termination.ReasonSeverancePayReserved");
        var creados = await GetAsync(http, admin, $"{Terminaciones}/reasons");
        creados.EnumerateArray().Should().Contain(m => m.GetProperty("code").GetString() == codigo && !m.GetProperty("isSeeded").GetBoolean());
    }

    [Fact]
    public async Task Sin_permiso_registrar_o_aprobar_responde_como_una_ruta_inexistente_y_ver_si_se_puede()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();

        var registrar = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, Terminaciones, new { employeePublicId = Guid.NewGuid(), terminationDate = new DateOnly(2026, 9, 15), reasonCode = "RENUNCIA" });
        registrar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoDeErrorAsync(registrar)).Should().Be("Generic.NotFound");
        var aprobar = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"{Terminaciones}/{Guid.NewGuid()}/approve", new { confirm = true });
        aprobar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var ajustar = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Put, $"{Terminaciones}/{Guid.NewGuid()}/deductions/{Guid.NewGuid()}", new { applied = 1m, reason = "x" });
        ajustar.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ver = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, $"{Terminaciones}?year=2026", null);
        ver.StatusCode.Should().Be(HttpStatusCode.OK, "sólo lectura tiene Payroll.Settlements.View");
        var inexistente = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Get, $"{Terminaciones}/{Guid.NewGuid()}/deductions", null);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoDeErrorAsync(inexistente)).Should().Be("Payroll.Run.NotFound");
    }

    // ------------------------------------------------------------------ ayudas --

    private static async Task<Guid> PeriodoAsync(HttpClient http, string token, Guid planId, DateTime desde, DateTime hasta)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/pay-periods", new
        {
            planPublicId = planId, startDate = desde, endDate = hasta, description = $"Quincena {desde:dd/MM} – {hasta:dd/MM/yyyy}", statusMessage = string.Empty,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"período {desde:dd/MM}–{hasta:dd/MM}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await LeerAsync(resp)).GetGuid();
    }

    /// <summary>La auditoría explícita de nómina lleva el PublicId de la entidad y sus valores; se espera a que Mongo la tenga.</summary>
    private static async Task<bool> EsperarEventoAsync(HttpClient http, string token, string accion, string texto)
    {
        var limite = DateTime.UtcNow.AddSeconds(25);
        JsonElement pagina = default;
        do
        {
            pagina = await GetAsync(http, token, $"/api/audit/logs?Action={Uri.EscapeDataString(accion)}&PageSize=200");
            var hay = pagina.GetProperty("items").EnumerateArray().Any(e =>
                (e.TryGetProperty("newValuesJson", out var nuevo) && nuevo.ValueKind == JsonValueKind.String && nuevo.GetString()!.Contains(texto, StringComparison.OrdinalIgnoreCase))
                || (e.TryGetProperty("entityId", out var id) && id.ValueKind == JsonValueKind.String && id.GetString()!.Equals(texto, StringComparison.OrdinalIgnoreCase)));
            if (hay) return true;
            await Task.Delay(500);
        } while (DateTime.UtcNow < limite);
        var todo = await GetAsync(http, token, "/api/audit/logs?PageSize=50&Module=Payroll");
        throw new Xunit.Sdk.XunitException($"No llegó el evento {accion} con «{texto}». Última página filtrada: {pagina.GetRawText()[..Math.Min(1500, pagina.GetRawText().Length)]} · Nómina: {todo.GetRawText()[..Math.Min(2500, todo.GetRawText().Length)]}");
    }
}
