using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// T062 — US4 por HTTP (quickstart §3.4): saldo derivado de 22,5 a los 540 días; vista previa de
/// hábiles 11 (lunes a sábado) y 9 (lunes a viernes) para el caso fijo de la spec; compensación por
/// encima del máximo rechazada con el máximo; registrar el disfrute crea el movimiento y la corrida
/// en una acción; aprobar contabiliza contra la provisión (SC-003) y deja la ausencia en los dos
/// períodos; la ordinaria de esos períodos no paga esos días como salario pero cotiza completo; un
/// disfrute sobre un período aprobado responde 422 con el destino del retroactivo; reversar deja el
/// espejo y anula la novedad del período aún abierto. Los períodos son de 2027 para no chocar con
/// los que las otras pruebas de la colección crean en 2026.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class VacacionesTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Saldo_vista_previa_registro_aprobacion_novedad_en_la_ordinaria_tope_retroactivo_y_reversa()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // La semilla de la 010 copió Payroll.AllowSameUserApproval (false) a PAY_CompanyPolicies al crear la cooperativa, y desde
        // entonces el parámetro del sistema que NominaE2E pone en true ya no manda: la política con vigencia sí. Aquí quien calcula
        // aprueba, así que la política se habilita por su propia ruta.
        var mismoUsuario = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/company-policies/AllowSameUserApproval/versions", new
        {
            key = "AllowSameUserApproval", value = "true", validFrom = "2020-01-01", reason = "Prueba e2e: quien calcula aprueba con segunda confirmación",
        });
        mismoUsuario.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity], $"política de segregación: «{await mismoUsuario.Content.ReadAsStringAsync()}»");

        // --- Inés: 18 meses al 27-10-2026 (540 días comerciales desde el 28-04-2025), 2.400.000, con EPS y fondo para que la liquidación no se bloquee ---
        var epsId = await CatalogoAsync(http, admin, "/api/payroll/health-insurance", "EPSVAC", "EPS Vacaciones E2E", "800111222");
        var afpId = await CatalogoAsync(http, admin, "/api/payroll/pension-providers", "AFPVAC", "AFP Vacaciones E2E", "800333444");
        var (empleadoId, _) = await CrearEmpleadoAfiliadoAsync(http, admin, "Ines", 2_400_000m, new DateTime(2025, 4, 28), epsId, afpId);

        var saldo = await NominaE2E.GetAsync(http, admin, $"/api/payroll/vacations/employees/{empleadoId}/balance?asOf=2026-10-27");
        saldo.GetProperty("balance").GetProperty("workedDays").GetInt32().Should().Be(540);
        saldo.GetProperty("balance").GetProperty("accruedDays").GetDecimal().Should().Be(22.5m, "540 × 15 / 360 con el parámetro de la semilla");
        saldo.GetProperty("balance").GetProperty("pendingDays").GetDecimal().Should().Be(22.5m);
        saldo.GetProperty("explanation").GetArrayLength().Should().BeGreaterThan(1, "el saldo se explica paso a paso");
        saldo.GetProperty("maxCompensableDays").GetDecimal().Should().Be(11.25m);

        var lista = await NominaE2E.GetAsync(http, admin, "/api/payroll/vacations/balances?asOf=2026-10-27");
        lista.EnumerateArray().Single(s => s.GetProperty("employeePublicId").GetGuid() == empleadoId).GetProperty("pendingDays").GetDecimal().Should().Be(22.5m);

        // --- vista previa obligatoria: el caso fijo de la spec, 28-10 a 10-11-2026 ---
        var lunesASabado = await VistaPreviaAsync(http, admin, "2026-10-28", "2026-11-10");
        lunesASabado.GetProperty("workingDays").GetInt32().Should().Be(11);
        lunesASabado.GetProperty("calendarDays").GetInt32().Should().Be(14);
        var saltados = lunesASabado.GetProperty("skipped").EnumerateArray().Select(s => (s.GetProperty("date").GetString(), s.GetProperty("reason").GetString()!)).ToList();
        saltados.Should().Contain(("2026-11-01", "Sunday")).And.Contain(("2026-11-08", "Sunday"));
        saltados.Should().Contain(s => s.Item1 == "2026-11-02" && s.Item2.StartsWith("Holiday:"), "Todos los Santos se traslada al lunes (Ley 51)");

        // La semana laboral es una política con vigencia: de lunes a viernes sólo entre octubre y noviembre de 2026.
        var politica = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/company-policies/SemanaLaboral/versions", new
        {
            key = "SemanaLaboral", value = "LunesAViernes", validFrom = "2026-10-01", validTo = "2026-11-30", reason = "Prueba e2e: jornada de lunes a viernes en el último trimestre",
        });
        politica.StatusCode.Should().Be(HttpStatusCode.Created, $"política: «{await politica.Content.ReadAsStringAsync()}»");
        var lunesAViernes = await VistaPreviaAsync(http, admin, "2026-10-28", "2026-11-10");
        lunesAViernes.GetProperty("workingDays").GetInt32().Should().Be(9);
        lunesAViernes.GetProperty("workWeek").GetInt32().Should().Be(1);
        lunesAViernes.GetProperty("skipped").EnumerateArray().Count(s => s.GetProperty("reason").GetString() == "Saturday").Should().Be(2, "31-10 y 07-11");

        // --- compensación por encima del máximo: 422 con el máximo permitido (FR-016) ---
        var excedida = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/settlements/vacations", new { employeePublicId = empleadoId, kind = "Compensation", compensationDays = 12 });
        excedida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, $"«{await excedida.Content.ReadAsStringAsync()}»");
        var sobre = await NominaE2E.LeerAsync(excedida);
        sobre.GetProperty("code").GetString().Should().Be("Payroll.Vacation.CompensationOverMax");
        var datos = sobre.GetProperty("data");
        datos.GetProperty("requestedDays").GetDecimal().Should().Be(12m);
        datos.GetProperty("policyCode").GetString().Should().Be("VACACIONES_COMPENSABLE_PCT");
        datos.GetProperty("maxDays").GetDecimal().Should().Be(datos.GetProperty("accruedDays").GetDecimal() / 2m, "50 % de lo causado al corte (hoy), VACACIONES_COMPENSABLE_PCT");
        datos.GetProperty("maxDays").GetDecimal().Should().BeLessThan(12m);

        // --- registrar el disfrute: movimiento + corrida Vacation en borrador, en una acción ---
        // 27-01 a 09-02-2027: 14 calendario, 12 hábiles de lunes a sábado (saltan los domingos 31-01 y 07-02), cubre enero y febrero.
        // Se pagan 13 días: los del calendario comercial (27 al 30 de enero = 4, más 9 de febrero), los mismos que la ordinaria descuenta (D-29).
        var ejercicio2027 = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years", new { year = 2027 });
        ejercicio2027.StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity, HttpStatusCode.Conflict], $"ejercicio 2027: «{await ejercicio2027.Content.ReadAsStringAsync()}»");
        var enero = await NominaE2E.CrearPeriodoAsync(http, admin, new DateTime(2027, 1, 1), new DateTime(2027, 1, 31));
        var febrero = await NominaE2E.CrearPeriodoAsync(http, admin, new DateTime(2027, 2, 1), new DateTime(2027, 2, 28));

        var registro = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/settlements/vacations",
            new { employeePublicId = empleadoId, kind = "Enjoyment", from = "2027-01-27", to = "2027-02-09", notes = "Vacaciones de comienzo de año" });
        registro.StatusCode.Should().Be(HttpStatusCode.Created, $"registrar: «{await registro.Content.ReadAsStringAsync()}»");
        var calculo = await NominaE2E.LeerAsync(registro);
        var runId = calculo.GetProperty("runPublicId").GetGuid();
        var movimientoId = calculo.GetProperty("movementPublicId").GetGuid();
        calculo.GetProperty("workingDays").GetDecimal().Should().Be(12m);
        calculo.GetProperty("calendarDays").GetInt32().Should().Be(14);
        calculo.GetProperty("amount").GetDecimal().Should().Be(13m * 80_000m, "D-01 y D-29: la liquidación paga los 13 días comerciales del descanso a 2.400.000 / 30 (4 de enero + 9 de febrero, los que la ordinaria descuenta)");
        calculo.GetProperty("blockers").GetArrayLength().Should().Be(0);
        var novedades = calculo.GetProperty("novelties").EnumerateArray().ToList();
        novedades.Should().HaveCount(2);
        novedades.Single(n => n.GetProperty("periodPublicId").GetGuid() == enero).GetProperty("days").GetInt32().Should().Be(4);
        novedades.Single(n => n.GetProperty("periodPublicId").GetGuid() == febrero).GetProperty("days").GetInt32().Should().Be(9);
        novedades.Should().OnlyContain(n => n.GetProperty("conceptCode").GetString() == "AUSENCIA_VACACIONES" && !n.GetProperty("retroactive").GetBoolean());

        var resumen = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}");
        resumen.GetProperty("kind").GetString().Should().Be("Vacation");
        resumen.GetProperty("status").GetString().Should().Be("Draft");
        resumen.GetProperty("employeePublicId").GetGuid().Should().Be(empleadoId);
        if (resumen.TryGetProperty("periodPublicId", out var periodo)) periodo.ValueKind.Should().Be(JsonValueKind.Null, "una liquidación especial no tiene período");

        var detalle = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/employees/{empleadoId}");
        var lineas = detalle.GetProperty("lines").EnumerateArray().ToList();
        var codigos = lineas.Select(l => l.GetProperty("conceptCode").GetString()).ToList();
        codigos.Should().Contain("VACACIONES_LIQ");
        // Inés no tiene nóminas aprobadas: su provisión acumulada es cero de verdad, así que el ajuste
        // lleva TODA la liquidación al gasto (nada queda debitado en la provisión; SC-003). Hasta el
        // 2026-09-21 el lector no informaba la provisión en cero y el motor omitía el ajuste.
        var liquidado = lineas.Single(l => l.GetProperty("conceptCode").GetString() == "VACACIONES_LIQ").GetProperty("amount").GetDecimal();
        var ajuste = lineas.Should().ContainSingle(l => l.GetProperty("conceptCode").GetString() == "VACACIONES_AJUSTE_PROV").Subject;
        ajuste.GetProperty("amount").GetDecimal().Should().Be(liquidado, "sin provisión acumulada, la diferencia al gasto es todo lo liquidado");

        var movimientos = await NominaE2E.GetAsync(http, admin, $"/api/payroll/vacations/employees/{empleadoId}/movements");
        var movimiento = movimientos.EnumerateArray().Single(m => m.GetProperty("movementPublicId").GetGuid() == movimientoId);
        movimiento.GetProperty("status").GetInt32().Should().Be(0, "Registered");
        movimiento.GetProperty("runPublicId").GetGuid().Should().Be(runId);
        movimiento.GetProperty("weekPolicyUsed").GetString().Should().Be("LunesASabado", "la vigencia de lunes a viernes terminó en noviembre de 2026");

        // La ruta de la ordinaria no aprueba una liquidación especial (contracts/api.md §2).
        var porLaOrdinaria = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/approve", new { confirm = true, exceptions = Array.Empty<object>(), confirmWithoutSegregation = true });
        porLaOrdinaria.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(porLaOrdinaria)).Should().Be("Payroll.Settlement.UseSettlementRoute");

        // --- aprobar: comprobante VacationRun, movimiento liquidado, ausencia en los dos períodos ---
        var aprobar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/settlements/vacations/{runId}/approve", new { confirm = true, confirmWithoutSegregation = true });
        aprobar.StatusCode.Should().Be(HttpStatusCode.OK, $"aprobar: «{await aprobar.Content.ReadAsStringAsync()}»");
        var aprobada = await NominaE2E.LeerAsync(aprobar);
        aprobada.GetProperty("number").GetString().Should().StartWith("NM");
        aprobada.GetProperty("total").GetDecimal().Should().BeGreaterThan(0m);

        var cuadre = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/balance-check");
        var provision = cuadre.GetProperty("provision").EnumerateArray().Single(p => p.GetProperty("provisionCode").GetString() == "PROV_VACACIONES");
        provision.GetProperty("consumed").GetDecimal().Should().Be(13m * 80_000m, "SC-003: lo liquidado consume PROV_VACACIONES");

        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/vacations/employees/{empleadoId}/movements")).EnumerateArray()
            .Single(m => m.GetProperty("movementPublicId").GetGuid() == movimientoId).GetProperty("status").GetInt32().Should().Be(1, "Liquidated");

        var novedadesEnero = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{enero}/novelties");
        var ausenciaEnero = novedadesEnero.EnumerateArray().Single(n => n.GetProperty("conceptCode").GetString() == "AUSENCIA_VACACIONES" && n.GetProperty("employeePublicId").GetGuid() == empleadoId);
        ausenciaEnero.GetProperty("origin").GetString().Should().Be("VacationLeave");
        var novedadesFebrero = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{febrero}/novelties");
        novedadesFebrero.EnumerateArray().Should().Contain(n => n.GetProperty("conceptCode").GetString() == "AUSENCIA_VACACIONES" && n.GetProperty("employeePublicId").GetGuid() == empleadoId);

        var saldoTrasAprobar = await NominaE2E.GetAsync(http, admin, $"/api/payroll/vacations/employees/{empleadoId}/balance?asOf=2027-01-26");
        saldoTrasAprobar.GetProperty("balance").GetProperty("enjoyedDays").GetDecimal().Should().Be(12m);

        // --- la ordinaria de enero no paga esos días como salario (D-01) y cotiza completo sobre el salario ---
        var ordinariaEnero = await NominaE2E.CalcularAsync(http, admin, enero);
        var runEnero = ordinariaEnero.GetProperty("runPublicId").GetGuid();
        var detalleEnero = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runEnero}/employees/{empleadoId}");
        var lineasEnero = detalleEnero.GetProperty("lines").EnumerateArray().ToList();
        var salario = lineasEnero.Single(l => l.GetProperty("conceptCode").GetString() == "SALARIO");
        salario.GetProperty("quantity").GetDecimal().Should().Be(26m, "30 − 4 días de vacaciones del 27 al 31 de enero");
        salario.GetProperty("amount").GetDecimal().Should().Be(2_400_000m * 26m / 30m);
        lineasEnero.Should().Contain(l => l.GetProperty("conceptCode").GetString() == "AUSENCIA_VACACIONES");
        lineasEnero.Single(l => l.GetProperty("conceptCode").GetString() == "SALUD_EMP").GetProperty("amount").GetDecimal().Should().Be(96_000m, "4 % sobre el IBC completo de 2.400.000: los días de vacaciones cotizan");
        lineasEnero.Should().NotContain(l => l.GetProperty("conceptCode").GetString() == "VACACIONES", "la liquidación ya pagó los días; la ordinaria no vuelve a pagarlos");

        // Aprobar enero: el período queda inmutable. Un disfrute que lo cruza se rechaza y ofrece febrero como retroactivo.
        await NominaE2E.AprobarAsync(http, admin, runEnero);
        var sobreAprobado = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/settlements/vacations",
            new { employeePublicId = empleadoId, kind = "Enjoyment", from = "2027-01-11", to = "2027-01-15" });
        sobreAprobado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, $"«{await sobreAprobado.Content.ReadAsStringAsync()}»");
        var sobreRetro = await NominaE2E.LeerAsync(sobreAprobado);
        sobreRetro.GetProperty("code").GetString().Should().Be("Payroll.Vacation.PeriodApproved");
        sobreRetro.GetProperty("data").GetProperty("periodPublicId").GetGuid().Should().Be(enero);
        sobreRetro.GetProperty("data").GetProperty("retroactiveTargetPeriodPublicId").GetGuid().Should().Be(febrero);

        // --- reversar: espejo, movimiento de vuelta a registrado, la novedad de febrero (abierto) se anula y la de enero (aprobado) queda ---
        var reversar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/settlements/vacations/{runId}/reverse", new { reason = "Prueba e2e: las fechas cambian" });
        reversar.StatusCode.Should().Be(HttpStatusCode.OK, $"reversar: «{await reversar.Content.ReadAsStringAsync()}»");
        (await NominaE2E.LeerAsync(reversar)).GetProperty("reversalNumber").GetString().Should().StartWith("NM");
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}")).GetProperty("status").GetString().Should().Be("Reversed");
        var tras = (await NominaE2E.GetAsync(http, admin, $"/api/payroll/vacations/employees/{empleadoId}/movements")).EnumerateArray()
            .Single(m => m.GetProperty("movementPublicId").GetGuid() == movimientoId);
        tras.GetProperty("status").GetInt32().Should().Be(0, "Registered");
        var febreroTras = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{febrero}/novelties");
        febreroTras.EnumerateArray().Where(n => n.GetProperty("conceptCode").GetString() == "AUSENCIA_VACACIONES" && n.GetProperty("employeePublicId").GetGuid() == empleadoId)
            .Should().NotBeEmpty().And.OnlyContain(n => n.GetProperty("status").GetString() == "Cancelled", "febrero seguía abierto");

        var liquidaciones = await NominaE2E.GetAsync(http, admin, $"/api/payroll/settlements/vacations?employeeId={empleadoId}");
        liquidaciones.EnumerateArray().Single(l => l.GetProperty("runPublicId").GetGuid() == runId).GetProperty("status").GetString().Should().Be("Reversed");

        // --- centro de reportes y permisos: el auditor ve, no registra ---
        var reporte = await NominaE2E.GetAsync(http, admin, "/api/reports/payroll/saldos-vacaciones?asOf=2026-10-27&format=json");
        reporte.GetProperty("titulo").GetString().Should().Be("Saldos de vacaciones");
        var excel = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, "/api/reports/payroll/movimientos-vacaciones?desde=2027-01-01&hasta=2027-02-28&format=xlsx", null);
        excel.StatusCode.Should().Be(HttpStatusCode.OK, $"«{await excel.Content.ReadAsStringAsync()}»");
        excel.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheet");

        var lectura = ctx.TokenSoloLectura;
        (await NominaE2E.EnviarAsync(http, lectura, HttpMethod.Get, "/api/payroll/vacations/balances", null)).StatusCode.Should().Be(HttpStatusCode.OK, "ReadOnly tiene Payroll.Vacations.View");
        var sinPermiso = await NominaE2E.EnviarAsync(http, lectura, HttpMethod.Post, "/api/payroll/vacations/working-days", new { from = "2026-10-28", to = "2026-11-10" });
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin permiso la ruta no existe para quien no lo tiene");
    }

    private static async Task<JsonElement> VistaPreviaAsync(HttpClient http, string token, string desde, string hasta)
    {
        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/vacations/working-days", new { from = desde, to = hasta });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"vista previa: «{await resp.Content.ReadAsStringAsync()}»");
        return await NominaE2E.LeerAsync(resp);
    }

    /// <summary>Crea la EPS o el fondo (o lo reutiliza si otra corrida ya lo dejó) y devuelve su PublicId.</summary>
    private static async Task<Guid> CatalogoAsync(HttpClient http, string token, string ruta, string codigo, string nombre, string nit)
    {
        var alta = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, ruta, new { code = codigo, name = nombre, shortName = codigo, taxId = nit, checkDigit = 1 });
        if (alta.StatusCode == HttpStatusCode.Created) return (await NominaE2E.LeerAsync(alta)).GetGuid();
        var lista = await NominaE2E.GetAsync(http, token, $"{ruta}?pageSize=500");
        var items = lista.ValueKind == JsonValueKind.Array ? lista : lista.GetProperty("items");
        return items.EnumerateArray().Single(x => x.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
    }

    private static async Task<(Guid EmpleadoId, string Documento)> CrearEmpleadoAfiliadoAsync(HttpClient http, string token, string nombre, decimal salario, DateTime ingreso, Guid epsId, Guid afpId)
    {
        var documento = Random.Shared.Next(710_000_000, 719_999_999).ToString();
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Prueba", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();
        var empleado = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = salario, contractType = 1, hireDate = ingreso, healthInsurancePublicId = epsId, pensionProviderPublicId = afpId,
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado: «{await empleado.Content.ReadAsStringAsync()}»");
        return ((await NominaE2E.LeerAsync(empleado)).GetGuid(), documento);
    }
}
