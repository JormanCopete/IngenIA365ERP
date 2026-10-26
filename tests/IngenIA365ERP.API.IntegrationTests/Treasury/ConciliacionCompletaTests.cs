using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, T2, T0446 (US17 «Independent Test»; FR-128 a FR-130): por HTTP, una cuenta con su extracto —abonos, cargos, el GMF y una comisión—, una regla de
/// emparejamiento por valor y fecha, la conciliación completa del mes con su cruce automático, la sugerencia de las notas del extracto y la nota del GMF que queda por aprobar
/// (quien la registró no la aprueba). Escrita para la corrida de cierre de T2; sin correr. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ConciliacionCompletaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Reglas_notas_y_conciliacion_completa_del_mes()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "concompleta");
        var hoy = t.Escenario.Hoy;
        var csv = string.Join("\r\n", "fecha;valor;referencia;documento;consignante;descripcion;saldo",
            $"{hoy:yyyy-MM-dd};-4000.00;;;;GMF 4X1000;",
            $"{hoy:yyyy-MM-dd};-11900.00;;;;COMISION CUOTA DE MANEJO;",
            $"{hoy:yyyy-MM-dd};250000.00;R-1;;;Abono;");
        (await ExtractoE2E.CargarAsync(http, t.Admin, t.CuentaDePagos, csv)).GetProperty("loaded").GetInt32().Should().Be(3);

        await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/reconciliation-rules",
            new { code = "VALFEC", name = "Valor y fecha", criterion = "AmountAndDate", toleranceDays = 2, priority = 1 });
        var sugerencias = await InventarioE2E.GetAsync(http, t.Admin, $"/api/treasury/bank-notes/suggestions?account={t.CuentaDePagos}");
        sugerencias.GetArrayLength().Should().Be(2, "el GMF y la comisión");
        var gmf = sugerencias.EnumerateArray().First(s => s.GetProperty("suggestedKind").GetInt32() == 1);
        var nota = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/bank-notes", new
        {
            bankAccountPublicId = t.CuentaDePagos, kind = "Gmf", amount = 4000m, statementLinePublicId = gmf.GetProperty("statementLinePublicId").GetGuid(),
        });
        nota.GetProperty("status").GetInt32().Should().Be(1, "por aprobar");
        var propia = await CarteraE2E.ConClaveAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/bank-notes/{nota.GetProperty("publicId").GetGuid()}/approve", null);
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(propia)).Should().Be("Treasury.BankNote.SelfApproval");

        var conciliacion = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/reconciliations",
            new { bankAccountPublicId = t.CuentaDePagos, year = hoy.Year, month = hoy.Month });
        var cruce = await CarteraE2E.ConClaveAsync(http, t.Admin, HttpMethod.Post,
            $"/api/treasury/reconciliations/{conciliacion.GetProperty("publicId").GetGuid()}/auto-match", null);
        cruce.StatusCode.Should().Be(HttpStatusCode.OK, await cruce.Content.ReadAsStringAsync());
    }
}

/// <summary>
/// Feature 015, T2, T0446 (SC-016): con <c>RUN_PERF_TESTS=1</c>, un extracto de 2.000 movimientos y el cruce automático de la conciliación completa con una regla debe
/// terminar en menos de una hora; el porcentaje emparejado solo (≥ 85 %) se mide con los registros reales de la corrida de cierre. Sin la variable, no hace nada. Escrita
/// para la corrida de cierre de T2; sin correr. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ConciliacionCompletaPerformanceTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Dos_mil_movimientos_se_cruzan_en_menos_de_una_hora()
    {
        if (Environment.GetEnvironmentVariable("RUN_PERF_TESTS") != "1") return;
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "conperf");
        var hoy = t.Escenario.Hoy;
        var sb = new StringBuilder("fecha;valor;referencia;documento;consignante;descripcion;saldo");
        for (var i = 1; i <= 2000; i++)
            sb.Append("\r\n").Append(string.Create(CultureInfo.InvariantCulture, $"{hoy:yyyy-MM-dd};{(i % 2 == 0 ? -1 : 1) * (1000 + i)}.00;P-{i};;;Movimiento {i};"));
        (await ExtractoE2E.CargarAsync(http, t.Admin, t.CuentaDePagos, sb.ToString())).GetProperty("loaded").GetInt32().Should().Be(2000);
        await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/reconciliation-rules",
            new { code = "REFVAL", name = "Referencia y valor", criterion = "ReferenceAndAmount", toleranceDays = 3, priority = 1 });
        var conciliacion = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/reconciliations",
            new { bankAccountPublicId = t.CuentaDePagos, year = hoy.Year, month = hoy.Month });

        var reloj = Stopwatch.StartNew();
        var cruce = await CarteraE2E.ConClaveAsync(http, t.Admin, HttpMethod.Post,
            $"/api/treasury/reconciliations/{conciliacion.GetProperty("publicId").GetGuid()}/auto-match", null);
        reloj.Stop();

        cruce.StatusCode.Should().Be(HttpStatusCode.OK, await cruce.Content.ReadAsStringAsync());
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromHours(1), "SC-016");
    }
}
