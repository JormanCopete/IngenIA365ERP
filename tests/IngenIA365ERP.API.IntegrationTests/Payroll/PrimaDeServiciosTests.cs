using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Feature 010, US1 (T046; quickstart §3.1) por HTTP sobre la cooperativa compartida de nómina: la
/// prima del <b>primer</b> semestre de 2026 —el segundo cierra el 31-12 y el comprobante se fecha al
/// corte, que no puede ser posterior a hoy— con los empleados A (2.000.000 fijo) y B (ingreso el 15-03,
/// 1.750.905 hasta el 30-04 y 2.000.000 desde el 01-05: los mismos tramos del caso dorado 02 corridos
/// un semestre). Calcular → aviso de saldo inicial → duplicado → recalcular → el Operador no aprueba
/// (404) ni por la ruta de la ordinaria (422) → aprobar → comprobante <c>ServiceBonusRun</c> visible en
/// Contabilidad con la provisión cancelada (SC-003 por el cuadre de la corrida y las líneas del
/// comprobante) → relación de pago propia → comprobante del empleado con la etiqueta → exportación de
/// las dos vistas → reversar y liquidar de nuevo.
///
/// <para>
/// La cooperativa redondea al peso (política por defecto): 1.124.547,50 sale 1.124.548 y 630.404,72
/// sale 630.405. Los excluidos con razón (integral, aprendiz lectivo, pasante, retirado con definitiva)
/// se prueban en Application: la API de la ficha no expone <c>EmployeeClass</c> ni existe todavía la
/// definitiva por HTTP en esta rama, así que aquí sólo se comprueba que la lista viaja.
/// </para>
/// </summary>
[Collection(NominaCollection.Nombre)]
public class PrimaDeServiciosTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/payroll/settlements/service-bonus";
    private const string CorreoOperador = "nomina.operador@coop.nomina.test";
    private const string ClaveOperador = "Nomina-Operador-2026!";
    private static readonly DateOnly Corte = new(2026, 6, 30);

    [Fact]
    public async Task La_prima_del_semestre_se_calcula_aprueba_paga_exporta_y_reversa_por_su_propia_ruta()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // --- políticas que la prueba necesita vigentes al corte: quien calcula aprueba (con segunda
        //     confirmación) y la nómina «arrancó» el 01-01-2026, para que A avise su saldo inicial ---
        await AsegurarPoliticaAsync(http, admin, "AllowSameUserApproval", "true", new DateOnly(2026, 1, 1), null);
        await AsegurarPoliticaAsync(http, admin, "ArranqueNominaFecha", "2026-01-01", new DateOnly(2026, 6, 1), Corte);

        var faltantes = await NominaE2E.GetAsync(http, admin, "/api/payroll/legal-parameters/missing?process=Settlements");
        faltantes.GetProperty("missing").GetArrayLength().Should().Be(0, "la cooperativa recién sembrada tiene todos los parámetros de las liquidaciones");

        // --- un plan propio para que la provisión de junio exista sin pisar los períodos de las otras pruebas ---
        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "PRIMA", name = "Plan prima e2e", periodicity = "Monthly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        var planId = (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();

        var (a, _) = await CrearEmpleadoEnPlanAsync(http, admin, "Alba", 2_000_000m, new DateTime(2020, 3, 1), planId);
        var (b, _) = await CrearEmpleadoEnPlanAsync(http, admin, "Bruno", 1_750_905m, new DateTime(2026, 3, 15), planId);
        var cambio = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/employees/{b}/salary-changes",
            new { newSalary = 2_000_000m, effectiveFrom = new DateTime(2026, 5, 1), reason = "Ajuste tras período de prueba" });
        cambio.StatusCode.Should().Be(HttpStatusCode.Created, await cambio.Content.ReadAsStringAsync());

        // Junio aprobado en el plan de la prima: provisiona PROV_PRIMA para A y B (lo que la prima cancela).
        var junio = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods",
            new { planPublicId = planId, startDate = new DateTime(2026, 6, 1), endDate = new DateTime(2026, 6, 30), description = "Junio 2026 (prima e2e)", statusMessage = string.Empty });
        junio.StatusCode.Should().Be(HttpStatusCode.Created, await junio.Content.ReadAsStringAsync());
        var junioId = (await NominaE2E.LeerAsync(junio)).GetGuid();
        var ordinaria = await NominaE2E.CalcularAsync(http, admin, junioId);
        await NominaE2E.AprobarAsync(http, admin, ordinaria.GetProperty("runPublicId").GetGuid());
        ordinaria.GetProperty("employeeCount").GetInt32().Should().Be(2, "sólo A y B están en el plan de la prima");

        // ------------------------------------------------------------------ calcular --
        var calculo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Ruta, new { year = 2026, semester = 1 });
        calculo.StatusCode.Should().Be(HttpStatusCode.Created, await calculo.Content.ReadAsStringAsync());
        var v1 = await NominaE2E.LeerAsync(calculo);
        var runId = v1.GetProperty("runPublicId").GetGuid();
        v1.GetProperty("kind").GetString().Should().Be("ServiceBonus");
        v1.GetProperty("version").GetInt32().Should().Be(1);
        v1.GetProperty("cutoffDate").GetString().Should().Be("2026-06-30");
        v1.GetProperty("excluded").ValueKind.Should().Be(JsonValueKind.Array);
        v1.GetProperty("blockers").GetArrayLength().Should().Be(0);
        var avisoSaldo = v1.GetProperty("warnings").EnumerateArray().Single(w => w.GetProperty("code").GetString() == "Payroll.Settlement.OpeningBalanceMissing");
        avisoSaldo.GetProperty("data").GetProperty("employeePublicIds").EnumerateArray().Select(x => x.GetGuid()).Should().Contain(a, "A ingresó antes del arranque y no tiene saldo inicial (FR-007)")
            .And.NotContain(b, "B ingresó después del arranque");

        var detalleA = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/employees/{a}");
        var primaA = Linea(detalleA, "PRIMA");
        primaA.GetProperty("amount").GetDecimal().Should().Be(1_124_548m, "(2.000.000 + 249.095) × 180 / 360 = 1.124.547,50 al peso");
        primaA.GetProperty("quantity").GetDecimal().Should().Be(180);
        primaA.GetProperty("baseAmount").GetDecimal().Should().Be(2_249_095m);
        primaA.GetProperty("explanation").GetProperty("parameter").GetProperty("code").GetString().Should().Be("PRIMA_DIAS_ANIO");
        Linea(detalleA, "RETEFTE_PRIMA").GetProperty("amount").GetDecimal().Should().Be(0m);
        // El ajuste explica contra qué provisión se cruzó: baseAmount es la acumulada de junio del empleado.
        var ajusteA = Linea(detalleA, "PRIMA_AJUSTE_PROV");
        var provisionA = ajusteA.GetProperty("baseAmount").GetDecimal();
        provisionA.Should().BeGreaterThan(0m, "junio aprobado provisionó prima");
        ajusteA.GetProperty("amount").GetDecimal().Should().Be(1_124_548m - provisionA, "la diferencia contra la provisión de junio");

        var detalleB = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/employees/{b}");
        var primaB = Linea(detalleB, "PRIMA");
        primaB.GetProperty("amount").GetDecimal().Should().Be(630_405m, "46 días a 2.000.000 + 60 días a 2.249.095 = 630.404,72 al peso");
        primaB.GetProperty("quantity").GetDecimal().Should().Be(106);
        detalleB.GetProperty("salaryTranches").GetArrayLength().Should().Be(2, "la explicación va tramo por tramo");

        var resumen = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}");
        resumen.GetProperty("kind").GetString().Should().Be("ServiceBonus");
        resumen.GetProperty("year").GetInt32().Should().Be(2026);
        resumen.GetProperty("semester").GetInt32().Should().Be(1);
        resumen.GetProperty("periodPublicId").ValueKind.Should().Be(JsonValueKind.Null, "una liquidación especial no tiene período");
        resumen.GetProperty("warnings").EnumerateArray().Should().Contain(w => w.GetProperty("code").GetString() == "Payroll.Settlement.OpeningBalanceMissing");

        var excluidos = await NominaE2E.GetAsync(http, admin, $"{Ruta}/{runId}/excluded");
        excluidos.ValueKind.Should().Be(JsonValueKind.Array);
        excluidos.EnumerateArray().Select(x => x.GetProperty("employeePublicId").GetGuid()).Should().NotContain(a).And.NotContain(b);

        // ------------------------------------------------------ duplicado y recálculo --
        var duplicado = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Ruta, new { year = 2026, semester = 1 });
        duplicado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(duplicado)).Should().Be("Payroll.Settlement.Duplicate");

        var recalculo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/recalculate", new { });
        recalculo.StatusCode.Should().Be(HttpStatusCode.Created, await recalculo.Content.ReadAsStringAsync());
        var v2 = await NominaE2E.LeerAsync(recalculo);
        v2.GetProperty("version").GetInt32().Should().Be(2);
        var runV2 = v2.GetProperty("runPublicId").GetGuid();
        var lista = await NominaE2E.GetAsync(http, admin, $"{Ruta}?year=2026");
        lista.EnumerateArray().Select(x => (x.GetProperty("version").GetInt32(), x.GetProperty("status").GetString()))
            .Should().Contain((1, "Superseded")).And.Contain((2, "Draft"));
        runId = runV2;

        // ----------------------------------------- el Operador no aprueba, ni por otra ruta --
        var operador = await CrearOperadorAsync(http, admin, ctx.TenantPublicId);
        var operadorAprueba = await NominaE2E.EnviarAsync(http, operador, HttpMethod.Post, $"{Ruta}/{runId}/approve", new { confirm = true });
        operadorAprueba.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin Payroll.ServiceBonus.Approve la ruta no existe");
        (await NominaE2E.CodigoDeErrorAsync(operadorAprueba)).Should().Be("Generic.NotFound");
        var operadorLista = await NominaE2E.EnviarAsync(http, operador, HttpMethod.Get, $"{Ruta}?year=2026", null);
        operadorLista.StatusCode.Should().Be(HttpStatusCode.OK, "el Operador ve y calcula");
        var porLaOrdinaria = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/approve", new { confirm = true, confirmWithoutSegregation = true });
        porLaOrdinaria.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(porLaOrdinaria)).Should().Be("Payroll.Settlement.UseSettlementRoute");

        // ------------------------------------------------------------------- aprobar --
        var sinSegunda = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/approve", new { confirm = true });
        sinSegunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "quien calculó aprueba sólo con la segunda confirmación");
        (await NominaE2E.CodigoDeErrorAsync(sinSegunda)).Should().Be("Payroll.Settlement.ConfirmationRequired");

        var fueraDeRango = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/approve", new { confirm = true, confirmWithoutSegregation = true, postingDate = "2026-06-15" });
        (await NominaE2E.CodigoDeErrorAsync(fueraDeRango)).Should().Be("Payroll.Settlement.PostingDateInvalid");

        var aprobacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/approve", new { confirm = true, confirmWithoutSegregation = true });
        aprobacion.StatusCode.Should().Be(HttpStatusCode.OK, await aprobacion.Content.ReadAsStringAsync());
        var aprobada = await NominaE2E.LeerAsync(aprobacion);
        aprobada.GetProperty("postingDate").GetString().Should().Be("2026-06-30", "D-04: el comprobante se fecha al corte");
        aprobada.GetProperty("approvedWithoutSegregation").GetBoolean().Should().BeTrue();
        var documentoId = aprobada.GetProperty("documentPublicId").GetGuid();
        aprobada.GetProperty("number").GetString().Should().StartWith("NM-");

        // El comprobante existe en Contabilidad con el origen de la prima y cuadra.
        var comprobante = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/by-source?module=NOM&sourcePublicId={runId}");
        comprobante.GetProperty("publicId").GetGuid().Should().Be(documentoId);
        comprobante.GetProperty("origin").GetProperty("sourceType").GetString().Should().Be("ServiceBonusRun");
        comprobante.GetProperty("date").GetString().Should().Be("2026-06-30");
        comprobante.GetProperty("status").GetString().Should().Be("Posted");
        comprobante.GetProperty("totalDebit").GetDecimal().Should().Be(comprobante.GetProperty("totalCredit").GetDecimal()).And.BeGreaterThan(0m);
        var lineasDelComprobante = comprobante.GetProperty("lines").EnumerateArray().ToList();
        lineasDelComprobante.Should().Contain(l => l.GetProperty("personPublicId").ValueKind == JsonValueKind.String, "la cuenta por pagar lleva al empleado como tercero");

        // SC-003: el cuadre de la corrida dice cuánta provisión se consumió y qué diferencia fue al gasto,
        // y el comprobante mueve exactamente eso (la provisión de junio de A y B queda cancelada).
        var cuadre = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/balance-check");
        cuadre.GetProperty("accountingDocumentBalanced").GetBoolean().Should().BeTrue();
        var provisionPrima = cuadre.GetProperty("provision").EnumerateArray().Single(p => p.GetProperty("provisionCode").GetString() == "PROV_PRIMA");
        var acumulada = provisionPrima.GetProperty("accrued").GetDecimal();
        var consumida = provisionPrima.GetProperty("consumed").GetDecimal();
        var diferencia = provisionPrima.GetProperty("difference").GetDecimal();
        var liberada = provisionPrima.GetProperty("released").GetDecimal();
        acumulada.Should().BeGreaterThan(0m, "junio provisionó prima para A y B");
        var devengado = (await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}")).GetProperty("totals").GetProperty("earnings").GetDecimal();
        consumida.Should().Be(devengado, "la prima liquidada se cruza entera contra la provisión");
        (acumulada + diferencia - liberada).Should().Be(consumida, "SC-003: provisión acumulada + diferencia al gasto = prima pagada; nada queda pendiente en el libro");
        var provisionEnElComprobante = lineasDelComprobante.Sum(l => l.GetProperty("debit").GetDecimal());
        provisionEnElComprobante.Should().BeGreaterThanOrEqualTo(consumida, "el comprobante debita al menos lo que la prima cancela");

        // El período ordinario de junio no cambia.
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{junioId}")).GetProperty("status").GetString().Should().Be("Approved");

        var listaAprobada = await NominaE2E.GetAsync(http, admin, $"{Ruta}?year=2026&status=Approved");
        var fila = listaAprobada.EnumerateArray().Single(x => x.GetProperty("runPublicId").GetGuid() == runId);
        fila.GetProperty("postedDocumentPublicId").GetGuid().Should().Be(documentoId);
        fila.GetProperty("paidCount").GetInt32().Should().Be(0);

        // ------------------------------------------- relación de pago propia y comprobante --
        var relacion = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/payments");
        relacion.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("employeePublicId").GetGuid()).Should().Contain(a).And.Contain(b);
        relacion.GetProperty("paidCount").GetInt32().Should().Be(0);

        var pdf = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/payroll/runs/{runId}/payslips/{a}/pdf", null);
        pdf.StatusCode.Should().Be(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal(0x25, 0x50, 0x44, 0x46);

        var etiquetado = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/comprobante?runId={runId}&employeeId={a}");
        etiquetado.GetProperty("titulo").GetString().Should().Contain("Alba");

        // ---------------------------------------------------- las dos vistas del centro --
        foreach (var vista in new[] { "liquidacion-especial-resumen", "liquidacion-especial-detalle" })
        {
            var tabla = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/{vista}?runId={runId}");
            tabla.GetProperty("titulo").GetString().Should().Contain("Prima de servicios", vista);
            tabla.GetProperty("subtitulo").GetString().Should().Contain("Prima de servicios 2026-I").And.Contain(NominaE2E.CorreoAdmin, "encabezado con la liquidación y quien la pide");
            tabla.GetProperty("columnas")[0].GetProperty("tipo").ValueKind.Should().Be(JsonValueKind.String, vista);
            tabla.GetProperty("filas").GetArrayLength().Should().BeGreaterThan(0, vista);
            var xlsx = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/reports/payroll/{vista}?runId={runId}&format=xlsx", null);
            xlsx.StatusCode.Should().Be(HttpStatusCode.OK, $"{vista}: «{await xlsx.Content.ReadAsStringAsync()}»");
            xlsx.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            var pdfVista = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/reports/payroll/{vista}?runId={runId}&format=pdf", null);
            pdfVista.StatusCode.Should().Be(HttpStatusCode.OK, vista);
            (await pdfVista.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(500);
        }
        var resumenA = (await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/liquidacion-especial-resumen?runId={runId}"))
            .GetProperty("filas").EnumerateArray().Single(f => f.GetProperty("valores")[0].GetString() == "Alba Prueba");
        resumenA.GetProperty("valores")[3].GetInt32().Should().Be(180, "días del semestre");
        var soloLectura = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, $"/api/reports/payroll/liquidacion-especial-resumen?runId={runId}", null);
        soloLectura.StatusCode.Should().Be(HttpStatusCode.OK, "ReadOnly tiene Payroll.ServiceBonus.View");

        // ------------------------------------------------------------------ reversar --
        var operadorReversa = await NominaE2E.EnviarAsync(http, operador, HttpMethod.Post, $"{Ruta}/{runId}/reverse", new { reason = "x" });
        operadorReversa.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var reversion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{runId}/reverse", new { reason = "Faltó una comisión de mayo" });
        reversion.StatusCode.Should().Be(HttpStatusCode.OK, await reversion.Content.ReadAsStringAsync());
        var reversada = await NominaE2E.LeerAsync(reversion);
        reversada.GetProperty("reversalNumber").GetString().Should().StartWith("NM-");
        var espejo = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/{reversada.GetProperty("reversalDocumentPublicId").GetGuid()}");
        espejo.GetProperty("kind").GetString().Should().Be("Reversal");
        espejo.GetProperty("totalDebit").GetDecimal().Should().Be(comprobante.GetProperty("totalDebit").GetDecimal(), "espejo del original");
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}")).GetProperty("status").GetString().Should().Be("Reversed");

        // Tras reversar, la provisión vuelve a estar pendiente en el cuadre y el semestre se liquida de nuevo.
        var otra = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Ruta, new { year = 2026, semester = 1 });
        otra.StatusCode.Should().Be(HttpStatusCode.Created, await otra.Content.ReadAsStringAsync());
        var v3 = await NominaE2E.LeerAsync(otra);
        v3.GetProperty("version").GetInt32().Should().Be(3);
        var cuadreV3 = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{v3.GetProperty("runPublicId").GetGuid()}/balance-check");
        cuadreV3.GetProperty("provision").EnumerateArray().Single(p => p.GetProperty("provisionCode").GetString() == "PROV_PRIMA")
            .GetProperty("accrued").GetDecimal().Should().Be(acumulada, "la reversión devolvió la provisión: la prima vuelve a estar pendiente");

        // Descartar el borrador nuevo deja el semestre limpio para las demás pruebas.
        var descarte = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{v3.GetProperty("runPublicId").GetGuid()}/discard", new { reason = "Fin de la prueba" });
        descarte.StatusCode.Should().Be(HttpStatusCode.OK, await descarte.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------- ayudas --

    private static JsonElement Linea(JsonElement detalle, string code) =>
        detalle.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("conceptCode").GetString() == code);

    private static async Task<(Guid EmpleadoId, string Documento)> CrearEmpleadoEnPlanAsync(HttpClient http, string token, string nombre, decimal salario, DateTime ingreso, Guid planId)
    {
        var documento = (800_000_000 + Random.Shared.Next(1, 99_999_999)).ToString();
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Prueba", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();
        var empleado = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = salario, contractType = 1, hireDate = ingreso, payrollPlanPublicId = planId,
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado: «{await empleado.Content.ReadAsStringAsync()}»");
        return ((await NominaE2E.LeerAsync(empleado)).GetGuid(), documento);
    }

    /// <summary>Deja una vigencia de la política con ese valor cubriendo el corte; si ya la hay, no toca nada (otra prueba pudo dejarla).</summary>
    private static async Task AsegurarPoliticaAsync(HttpClient http, string token, string clave, string valor, DateOnly desde, DateOnly? hasta)
    {
        var versiones = await NominaE2E.GetAsync(http, token, $"/api/payroll/company-policies/{clave}/versions");
        var vigente = versiones.EnumerateArray().FirstOrDefault(v =>
            DateOnly.Parse(v.GetProperty("validFrom").GetString()!) <= Corte
            && (v.GetProperty("validTo").ValueKind == JsonValueKind.Null || DateOnly.Parse(v.GetProperty("validTo").GetString()!) >= Corte));
        if (vigente.ValueKind == JsonValueKind.Object && vigente.GetProperty("value").GetString() == valor) return;

        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/payroll/company-policies/{clave}/versions",
            new { value = valor, validFrom = desde, validTo = hasta, reason = "Prueba e2e de la prima de servicios", closePrevious = true });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"política {clave}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Un usuario con el rol Operador: invitado por el administrador, acepta y recibe el rol built-in.</summary>
    private async Task<string> CrearOperadorAsync(HttpClient http, string admin, Guid tenantId)
    {
        var invitacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/tenants/{tenantId}/invitations", new { email = CorreoOperador });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, $"invitación del operador: «{await invitacion.Content.ReadAsStringAsync()}»");
        var token = await AceptarInvitacionAsync(http, CorreoOperador, ClaveOperador);

        var roles = await NominaE2E.GetAsync(http, admin, "/api/admin/roles?includeBuiltIn=true&pageSize=50");
        var operador = roles.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "Operator").GetProperty("publicId").GetGuid();
        var usuarios = await NominaE2E.GetAsync(http, admin, $"/api/admin/users?search={Uri.EscapeDataString(CorreoOperador)}&pageSize=50");
        var usuario = usuarios.GetProperty("items").EnumerateArray().Single(u => string.Equals(u.GetProperty("email").GetString(), CorreoOperador, StringComparison.OrdinalIgnoreCase)).GetProperty("publicId").GetGuid();
        var asignacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = operador });
        asignacion.IsSuccessStatusCode.Should().BeTrue($"rol Operador: «{await asignacion.Content.ReadAsStringAsync()}»");

        var permisos = await NominaE2E.GetAsync(http, token, "/api/admin/permissions/mine");
        var codigos = permisos.GetProperty("permissions").EnumerateArray().Select(c => c.GetString()).ToList();
        codigos.Should().Contain("Payroll.ServiceBonus.Calculate").And.NotContain("Payroll.ServiceBonus.Approve");
        return token;
    }

    private async Task<string> AceptarInvitacionAsync(HttpClient http, string correo, string clave)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        mensaje.Should().NotBeNull($"la invitación de {correo} tiene que haber salido por correo");
        var token = Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        token.Success.Should().BeTrue("el correo de invitación trae token=");
        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, registration = new { password = clave } });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aceptar la invitación de {correo}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(resp)).GetProperty("accessToken").GetString()!;
    }
}
