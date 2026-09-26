using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T303 (quickstart §3.5 y la parte de I1 de §4.2; US4-1, US4-3 a US4-6; FR-089 a FR-091; SC-016), en el motor de
/// <c>DB_PROVIDER</c>, sobre el escenario aislado «saldoinicial» (bodegas sin activar): la plantilla con un producto y una
/// bodega inexistentes no guarda nada y lista los dos errores; corregida, un borrador por bodega que confirma con aprobación de
/// otra persona y emite <c>SaldoInicialCargado</c> informativo; un ajuste en la bodega no activa responde
/// <c>Inventory.Warehouse.NotActive</c>; las cifras de SOLIDO y el comparativo de valorizado muestran la diferencia; la
/// activación sin comparación contable (host fuera de <c>Production</c>) exige aceptar la diferencia con motivo, y sin
/// <c>Inventory.Warehouses.Activate</c> es 404; la segunda bodega, activada después de salidas en la primera (ámbito
/// cooperativa), confirma su saldo a su corte y deja <c>AjusteDeCostoReconocido</c> por el documento afectado (caso dorado 17).
/// El volumen de SC-016 (20.000 líneas) sólo corre con <c>RUN_PERF_TESTS=1</c>. US7 (I2) agrega aquí la comparación contra
/// los libros (quickstart §4.2).
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class SaldoInicialYActivacionTests(CentralIdentityApiFixture fx)
{
    private const string Saldos = "/api/inventory/opening-balances";
    private static readonly string[] Encabezados = ["bodega", "fechaDeCorte", "producto", "ubicacion", "cantidad", "costoUnitario"];

    [Fact]
    public async Task Del_saldo_inicial_a_la_segunda_bodega_activada()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "saldoinicial", activar: false);
        var usuarios = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        var corte = esc.Corte.ToString("yyyy-MM-dd");

        // (1) Dos errores: nada guardado.
        var conErrores = InventarioE2E.Libro(("Datos", Encabezados,
        [
            ["PRIN", corte, "P1", null, "20", "1000"],
            ["PRIN", corte, "NOEXISTE", null, "1", "10"],
            ["BODX", corte, "P2", null, "1", "10"],
        ]));
        var revision = await InventarioE2E.LeerAsync(await InventarioE2E.ImportarAsync(http, esc.Admin, Saldos, "review", conErrores));
        revision.GetProperty("valid").GetBoolean().Should().BeFalse();
        revision.GetProperty("errors").GetArrayLength().Should().Be(2);
        await InventarioE2E.FallaAsync(await InventarioE2E.ImportarAsync(http, esc.Admin, Saldos, "apply", conErrores), "Import.Invalid");
        (await InventarioE2E.GetAsync(http, esc.Admin, Saldos)).GetProperty("items").EnumerateArray().Should().BeEmpty("todo o nada");

        // (2) Corregida: un borrador por bodega.
        var corregida = InventarioE2E.Libro(("Datos", Encabezados,
        [
            ["PRIN", corte, "P1", null, "20", "1000"],
            ["PRIN", corte, "P2", null, "10", "500"],
            ["PV1", corte, "P1", null, "5", "1200"],
        ]));
        var aplicada = await InventarioE2E.RevisarYAplicarAsync(http, esc.Admin, Saldos, corregida);
        var documentos = aplicada.GetProperty("extra").GetProperty("documents").EnumerateArray().ToList();
        documentos.Should().HaveCount(2);
        Guid DocDe(string bodega) => documentos.Single(d => d.GetProperty("warehouse").GetProperty("code").GetString() == bodega)
            .GetProperty("documentPublicId").GetGuid();

        // (3) La bodega no activa sólo admite su saldo inicial.
        var ajuste = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P3", 1, 100m)]);
        await InventarioE2E.FallaAsync(await InventarioE2E.PedirConfirmarAsync(http, esc.Admin, "/api/inventory/adjustments", ajuste), "Inventory.Warehouse.NotActive");

        // (4) Confirmar pasa por la aprobación de otra persona; el mensaje es informativo.
        await ConfirmarSaldoAsync(http, esc, usuarios, DocDe("PRIN"));
        var informativos = await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            """SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = 'SaldoInicialCargado' AND "Kind" = 2""",
            "SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = 'SaldoInicialCargado' AND [Kind] = 2");
        Convert.ToInt32(informativos).Should().Be(1);

        // (5) Cifras de SOLIDO y el comparativo de valorizado.
        await InventarioE2E.RevisarYAplicarAsync(http, esc.Admin, "/api/inventory/legacy-figures", InventarioE2E.Libro(("Datos",
            ["fecha", "bodega", "producto", "cantidad", "valor", "grupoContable"],
            [[corte, "PRIN", "P1", "20", "20500", null], [corte, "PRIN", "P2", "10", "5000", null]])));
        var comparativo = await InventarioE2E.InformeAsync(http, esc.Admin, "legacy-comparison-valuation", $"asOf={corte}&warehouse={esc.Bodega("PRIN")}");
        var p1 = comparativo.Filas.Single(f => Accounting.ContabilidadE2E.Tabla.Texto(f, comparativo.Indice("Producto")).Contains("P1"));
        Math.Abs(comparativo.Numero(p1, "Diferencia (valor)")).Should().Be(500m);
        comparativo.Numero(p1, "Diferencia (cantidad)").Should().Be(0m);

        // (6) Activar: sin el permiso, 404; sin aceptar la diferencia, 422; aceptándola con motivo, activa.
        var ruta = $"/api/inventory/warehouses/{esc.Bodega("PRIN")}/activation";
        (await InventarioE2E.MandarAsync(http, usuarios.Aprobador.Token, HttpMethod.Post, ruta, new { cutoffDate = corte, acceptDifference = true, reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, ruta, new { cutoffDate = corte }),
            "Inventory.Activation.Difference");
        var activada = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, ruta,
            new { cutoffDate = corte, acceptDifference = true, reason = "Ensayo de I1 sin comparación contable" });
        activada.GetProperty("differenceAccepted").GetBoolean().Should().BeTrue();

        // (7) Salidas en la primera y, después, el saldo de la segunda a su corte (caso dorado 17, ámbito cooperativa).
        var salida = await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJN", "PRIN", [new("P1", 4)]);
        await ConfirmarSaldoAsync(http, esc, usuarios, DocDe("PV1"));
        var ajustesDeCosto = await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            """SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = 'AjusteDeCostoReconocido'""",
            "SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = 'AjusteDeCostoReconocido'");
        Convert.ToInt32(ajustesDeCosto).Should().BeGreaterThan(0, $"la salida {salida.GetProperty("displayNumber")} cambió de costo al entrar el saldo de PV1");
        (await esc.ExistenciaAsync(http, esc.Admin, "P1")).GetProperty("costState").GetProperty("averageCost").GetDecimal()
            .Should().Be(1_040m, "(20 × 1.000 + 5 × 1.200) / 25");
        await EscenarioDeInventario.ActivarAsync(http, esc.Admin, esc.Bodega("PV1"), esc.Corte);
    }

    [FactDeRendimiento]
    public async Task Veinte_mil_lineas_con_dos_errores_no_guardan_nada()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "saldo20k", activar: false);
        using var http = fx.CreateClient();
        var corte = esc.Corte.ToString("yyyy-MM-dd");
        var filas = new List<string?[]>();
        for (var i = 0; i < 20_000; i++) filas.Add(["PRIN", corte, $"P{i % 7 + 1}", $"U{i}", "1", "100"]);
        filas[10] = ["PRIN", corte, "NOEXISTE", null, "1", "100"];
        filas[20] = ["BODX", corte, "P1", null, "1", "100"];
        var revision = await InventarioE2E.LeerAsync(await InventarioE2E.ImportarAsync(http, esc.Admin, Saldos, "review",
            InventarioE2E.Libro(("Datos", Encabezados, filas))));
        revision.GetProperty("valid").GetBoolean().Should().BeFalse();
        revision.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("row").GetInt32()).Should().Contain([12, 22]);
    }

    private static async Task ConfirmarSaldoAsync(HttpClient http, EscenarioDeInventario esc, UsuariosDelEnsayo usuarios, Guid documento)
    {
        var enviado = await InventarioE2E.ConfirmarAsync(http, esc.Admin, Saldos, documento);
        enviado.GetProperty("status").GetInt32().Should().Be(1, "el tipo de saldo inicial se aprueba siempre (PendingApproval)");
        var solicitud = enviado.GetProperty("approval").GetProperty("requestPublicId").GetGuid();
        var aprobada = await InventarioE2E.DecidirAsync(http, esc.Admin, usuarios.Aprobador.Token, solicitud, aprobar: true);
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
        (await InventarioE2E.GetAsync(http, esc.Admin, $"{Saldos}/{documento}")).GetProperty("status").GetInt32().Should().Be(2);
    }
}
