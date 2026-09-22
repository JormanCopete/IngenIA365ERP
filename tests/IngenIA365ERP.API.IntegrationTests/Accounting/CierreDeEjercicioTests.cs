using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// T112 — US6 (FR-021..FR-024) por HTTP, en una cooperativa propia cuyo primer ejercicio es 2025:
/// cerrar un mes con borrador rechaza y lista; cerrar el año exige los doce meses, el anterior y la
/// cuenta de resultado; el cierre deja un CI del 31/12 que el balance del 1 de enero siguiente ya no
/// ve como resultado y el estado de resultados del año sigue mostrando salvo <c>includeClosing</c>;
/// reabrir con motivo queda auditado y cerrar el siguiente con éste abierto rechaza.
/// </summary>
[Collection(ContabilidadCollection.Nombre)]
public class CierreDeEjercicioTests(CentralIdentityApiFixture fx)
{
    private const string Caja = "11050501";        // 110505 CAJA GENERAL
    private const string Ingreso = "41352001";     // 413520 ventas
    private const string Gasto = "51100501";       // 511005 SEGUROS
    private const string Excedente = "35050501";   // 350505 excedente del ejercicio (CUIF: 3505 EXCEDENTES DEL EJERCICIO)
    private const decimal Venta = 5_000_000m;
    private const decimal Seguro = 1_200_000m;

    [Fact]
    public async Task Cerrar_los_meses_y_el_ejercicio_deja_un_CI_que_los_informes_tratan_como_cierre()
    {
        var coop = await CooperativaAisladaAsync(fx, "cierre", 2025);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        foreach (var (codigo, nombre) in new[] { (Caja, "Caja"), (Ingreso, "Ventas"), (Gasto, "Seguros") })
            await CrearAuxiliarAsync(http, admin, codigo, nombre);
        // El CUIF no trae hijos bajo 3505 EXCEDENTES: la empresa crea la subcuenta y, debajo, la auxiliar.
        await CrearCuentaAsync(http, admin, "350505", "Excedente del ejercicio", "3505", ["CNT"]);
        await CrearAuxiliarAsync(http, admin, Excedente, "Excedente del ejercicio");

        // Movimientos de 2025: una venta y un seguro. Excedente esperado: 3.800.000.
        await ContabilizarAsync(http, admin, new DateOnly(2025, 3, 10), "Venta 2025", [Linea(Caja, null, Venta, 0m), Linea(Ingreso, null, 0m, Venta)]);
        await ContabilizarAsync(http, admin, new DateOnly(2025, 6, 15), "Seguro 2025", [Linea(Gasto, null, Seguro, 0m), Linea(Caja, null, 0m, Seguro)]);

        // (1) un borrador en junio impide cerrar junio y la respuesta lo lista
        var pendiente = await BorradorAsync(http, admin, new DateOnly(2025, 6, 20), "Pendiente de junio", [Linea(Gasto, null, 100m, 0m), Linea(Caja, null, 0m, 100m)]);
        var conBorrador = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/2025/6/close", null);
        conBorrador.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var cuerpo = await LeerAsync(conBorrador);
        cuerpo.GetProperty("code").GetString().Should().Be("Accounting.Period.HasDrafts");
        cuerpo.GetProperty("data").GetProperty("drafts").EnumerateArray().Should().ContainSingle(d => d.GetProperty("publicId").GetGuid() == pendiente);
        (await EnviarAsync(http, admin, HttpMethod.Delete, $"/api/accounting/documents/drafts/{pendiente}", null)).IsSuccessStatusCode.Should().BeTrue();

        // (2) el año no cierra con meses abiertos, y dice cuáles
        var mesesAbiertos = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2025/close", null);
        mesesAbiertos.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var abiertos = await LeerAsync(mesesAbiertos);
        abiertos.GetProperty("code").GetString().Should().Be("Accounting.FiscalYear.PeriodsOpen");
        abiertos.GetProperty("data").GetProperty("openMonths").GetArrayLength().Should().Be(12);

        for (var mes = 1; mes <= 12; mes++) await CerrarMesAsync(http, admin, 2025, mes);

        // (3) con los doce cerrados falta la cuenta de resultado
        var sinCuenta = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2025/close", null);
        sinCuenta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(sinCuenta)).Should().Be("Accounting.FiscalYear.ResultAccountMissing");

        var excedente = await CuentaAsync(http, admin, Excedente);
        var setup = await GetAsync(http, admin, "/api/accounting/setup");
        var configurar = await EnviarAsync(http, admin, HttpMethod.Put, "/api/accounting/setup", new
        {
            resultAccountPublicId = excedente, mainBranchPublicId = coop.SucursalPrincipal, fourEyes = false,
            reconciliationDayTolerance = setup.GetProperty("reconciliationDayTolerance").GetInt32(), taxTolerance = setup.GetProperty("taxTolerance").GetDecimal(),
        });
        configurar.IsSuccessStatusCode.Should().BeTrue($"cuenta de resultado: «{await configurar.Content.ReadAsStringAsync()}»");

        // (4) el cierre: un CI del 31/12/2025 con el excedente
        var cierre = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2025/close", null);
        cierre.StatusCode.Should().Be(HttpStatusCode.OK, $"cerrar 2025: «{await cierre.Content.ReadAsStringAsync()}»");
        var cerrado = await LeerAsync(cierre);
        cerrado.GetProperty("result").GetDecimal().Should().Be(Venta - Seguro);
        var ci = cerrado.GetProperty("closingDocumentPublicId").GetGuid();
        var documento = await GetAsync(http, admin, $"/api/accounting/documents/{ci}");
        documento.GetProperty("voucherTypeCode").GetString().Should().Be("CI");
        documento.GetProperty("kind").GetString().Should().Be("Closing");
        documento.GetProperty("status").GetString().Should().Be("Posted");
        documento.GetProperty("date").GetString().Should().Be("2025-12-31");
        documento.GetProperty("totalDebit").GetDecimal().Should().Be(documento.GetProperty("totalCredit").GetDecimal());
        var lineas = documento.GetProperty("lines").EnumerateArray().ToList();
        lineas.Single(l => l.GetProperty("accountCode").GetString() == Ingreso).GetProperty("debit").GetDecimal().Should().Be(Venta);
        lineas.Single(l => l.GetProperty("accountCode").GetString() == Gasto).GetProperty("credit").GetDecimal().Should().Be(Seguro);
        lineas.Single(l => l.GetProperty("accountCode").GetString() == Excedente).GetProperty("credit").GetDecimal().Should().Be(Venta - Seguro);

        var ejercicio = await GetAsync(http, admin, "/api/accounting/periods?year=2025");
        ejercicio.GetProperty("status").GetString().Should().Be("Closed");
        ejercicio.GetProperty("closingDocumentPublicId").GetGuid().Should().Be(ci);
        (await CodigoDeErrorAsync(await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2025/close", null))).Should().Be("Accounting.FiscalYear.AlreadyClosed");

        // (5) el cierre no se reversa desde Comprobantes: se reabre el ejercicio
        var reversarCi = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{ci}/reverse", new { reason = "no" });
        reversarCi.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(reversarCi)).Should().Be("Accounting.Document.IsClosing");

        // (6) el balance del 1 de enero de 2026 arranca sin resultados y con el excedente en el patrimonio
        await AbrirEjercicioAsync(http, admin, 2026);
        var balance2026 = await InformeAsync(http, admin, "trial-balance?from=2026-01-01&to=2026-01-31");
        Saldo(balance2026, Ingreso, "Saldo inicial").Should().Be(0m, "el cierre canceló el ingreso");
        Saldo(balance2026, Gasto, "Saldo inicial").Should().Be(0m);
        Math.Abs(Saldo(balance2026, Excedente, "Saldo inicial")).Should().Be(Venta - Seguro, "el excedente quedó en la cuenta de resultado");
        Math.Abs(Saldo(balance2026, Caja, "Saldo inicial")).Should().Be(Venta - Seguro, "la caja no la toca el cierre");

        // (7) el estado de resultados de 2025 sigue mostrando el año; con includeClosing los resultados quedan en cero
        var eri = await InformeAsync(http, admin, "income-statement?from=2025-01-01&to=2025-12-31");
        eri.Raiz.ToString().Should().Contain(Venta.ToString("0"), "sin el cierre, el ingreso del año se ve");
        var eriConCierre = await InformeAsync(http, admin, "income-statement?from=2025-01-01&to=2025-12-31&includeClosing=true");
        eriConCierre.Notas.Should().Contain(n => n.Contains("cierre", StringComparison.OrdinalIgnoreCase));
        var balance2025ConCierre = await InformeAsync(http, admin, "trial-balance?from=2025-01-01&to=2025-12-31&includeClosing=true");
        Saldo(balance2025ConCierre, Ingreso, "Saldo final").Should().Be(0m);
        var balance2025 = await InformeAsync(http, admin, "trial-balance?from=2025-01-01&to=2025-12-31");
        Math.Abs(Saldo(balance2025, Ingreso, "Saldo final")).Should().Be(Venta, "el cierre del ejercicio consultado queda fuera por defecto");

        // (8) reabrir exige motivo, reversa el CI en su fecha y queda auditado; los meses siguen cerrados
        var sinMotivo = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2025/reopen", new { reason = "" });
        sinMotivo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reabrir = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2025/reopen", new { reason = "Faltó una provisión de diciembre" });
        reabrir.StatusCode.Should().Be(HttpStatusCode.OK, $"reabrir 2025: «{await reabrir.Content.ReadAsStringAsync()}»");
        var reverso = (await LeerAsync(reabrir)).GetProperty("closingDocumentPublicId").GetGuid();
        var reversion = await GetAsync(http, admin, $"/api/accounting/documents/{reverso}");
        reversion.GetProperty("kind").GetString().Should().Be("Closing");
        reversion.GetProperty("date").GetString().Should().Be("2025-12-31");
        (await GetAsync(http, admin, $"/api/accounting/documents/{ci}")).GetProperty("status").GetString().Should().Be("Reversed");
        ejercicio = await GetAsync(http, admin, "/api/accounting/periods?year=2025");
        ejercicio.GetProperty("status").GetString().Should().Be("Open");
        ejercicio.GetProperty("reopenReason").GetString().Should().Be("Faltó una provisión de diciembre");
        ejercicio.GetProperty("periods").EnumerateArray().Should().OnlyContain(p => p.GetProperty("status").GetString() == "Closed", "reabrir el año no reabre los meses");
        var eventos = await EventosDeAuditoriaAsync(http, admin, "Accounting", "Accounting.FiscalYear.Reopened");
        eventos.Should().NotBeEmpty("la reapertura del ejercicio queda en la auditoría de la cooperativa");
        JsonDocument.Parse(eventos[0].GetProperty("newValuesJson").GetString()!).RootElement.GetProperty("reason").GetString().Should().Be("Faltó una provisión de diciembre");
        Math.Abs(Saldo(await InformeAsync(http, admin, "trial-balance?from=2026-01-01&to=2026-01-31"), Ingreso, "Saldo inicial")).Should().Be(Venta, "reabierto, el ingreso vuelve al saldo inicial de 2026");

        // (9) cerrar 2026 con 2025 abierto rechaza; con 2025 cerrado de nuevo, 2026 (sin resultados) cierra sin comprobante
        for (var mes = 1; mes <= 12; mes++) await CerrarMesAsync(http, admin, 2026, mes);
        (await CodigoDeErrorAsync(await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2026/close", null))).Should().Be("Accounting.FiscalYear.PreviousOpen");
        (await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2025/close", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var cierre2026 = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2026/close", null);
        cierre2026.StatusCode.Should().Be(HttpStatusCode.OK, $"cerrar 2026: «{await cierre2026.Content.ReadAsStringAsync()}»");
        (await LeerAsync(cierre2026)).GetProperty("closingDocumentPublicId").ValueKind.Should().Be(JsonValueKind.Null, "2026 no tuvo resultados");
        (await CodigoDeErrorAsync(await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2025/reopen", new { reason = "x" }))).Should().Be("Accounting.FiscalYear.NotLast");
        (await CodigoDeErrorAsync(await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years/2027/close", null))).Should().Be("Accounting.FiscalYear.NotFound");
    }

    /// <summary>La columna pedida de la fila de esa cuenta en el balance de prueba, o cero si la cuenta no aparece.</summary>
    private static decimal Saldo(Tabla balance, string cuenta, string columna)
    {
        var fila = balance.PorCodigo(cuenta);
        return fila is null ? 0m : balance.Numero(fila.Value, columna);
    }
}
