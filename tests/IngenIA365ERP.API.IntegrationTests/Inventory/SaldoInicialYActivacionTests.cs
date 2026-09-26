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

    /// <summary>
    /// T475 (I2; quickstart §4.2; US4-2, US4-3, US4-5; FR-090): con la contabilidad iniciada y la matriz cargada, la activación de
    /// PRIN compara por conjunto de cuentas el saldo de los libros al corte con el valorizado de PRIN más el de PV1 y PV2 —que siguen en
    /// SOLIDO y comparten cuentas, con sus cifras al corte—; la diferencia de 250.000 del conjunto sólo la acepta quien tiene
    /// <c>Inventory.Warehouses.AcceptActivationDifference</c>, con motivo; y <c>SaldoInicialCargado</c> llega a Contabilidad como
    /// recibo sin comprobante.
    /// </summary>
    [Fact]
    public async Task Con_contabilidad_la_activacion_compara_por_conjunto_con_los_libros()
    {
        var esc = await Accounting.ContabilidadDeInventarioE2E.PrepararAsync(fx, "saldocontable", activar: false);
        var usuarios = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        var corte = esc.Corte.ToString("yyyy-MM-dd");

        // Saldo inicial de PRIN: ASEO 20.000 (P1) y ABARROTES 5.000 (P4); PV1 y PV2 siguen en SOLIDO con 6.000 de ASEO y 1.000 de ABARROTES.
        var aplicada = await InventarioE2E.RevisarYAplicarAsync(http, esc.Admin, Saldos, InventarioE2E.Libro(("Datos", Encabezados,
        [
            ["PRIN", corte, "P1", null, "20", "1000"],
            ["PRIN", corte, "P4", null, "10", "500"],
        ])));
        var documento = aplicada.GetProperty("extra").GetProperty("documents").EnumerateArray().Single().GetProperty("documentPublicId").GetGuid();
        await ConfirmarSaldoAsync(http, esc, usuarios, documento);
        await InventarioE2E.RevisarYAplicarAsync(http, esc.Admin, "/api/inventory/legacy-figures", InventarioE2E.Libro(("Datos",
            ["fecha", "bodega", "producto", "cantidad", "valor", "grupoContable"],
            [[corte, "PV1", "P1", "5", "6000", null], [corte, "PV2", "P4", "2", "1000", null]])));

        // Los libros al corte: 282.000 en la cuenta de inventario (el conjunto de los dos grupos), 250.000 más que el valorizado.
        await Accounting.ContabilidadE2E.ContabilizarAsync(http, esc.Admin, esc.Corte, "Saldo de inventario traído de SOLIDO", new object[]
        {
            Accounting.ContabilidadE2E.Linea(Accounting.ContabilidadDeInventarioE2E.Inventario, esc.S1, 282_000m, 0m),
            Accounting.ContabilidadE2E.Linea(Accounting.ContabilidadDeInventarioE2E.SobranteDeInventario, esc.S1, 0m, 282_000m),
        });

        var ruta = $"/api/inventory/warehouses/{esc.Bodega("PRIN")}/activation";
        var previa = await InventarioE2E.GetAsync(http, esc.Admin, $"{ruta}?cutoffDate={corte}");
        var conjunto = previa.GetProperty("sets").EnumerateArray().Should().ContainSingle($"un conjunto de cuentas: {previa}").Subject;
        conjunto.GetProperty("ledgerBalance").GetDecimal().Should().Be(282_000m);
        conjunto.GetProperty("valuation").GetProperty("thisWarehouse").GetDecimal().Should().Be(25_000m);
        conjunto.GetProperty("valuation").GetProperty("legacyWarehouses").EnumerateArray()
            .Should().Contain(w => w.GetProperty("warehouseCode").GetString() == "PV1" && w.GetProperty("value").GetDecimal() == 6_000m,
                "PV1 suma con sus cifras de SOLIDO y se muestra aparte");
        conjunto.GetProperty("valuation").GetProperty("total").GetDecimal().Should().Be(32_000m);
        Math.Abs(conjunto.GetProperty("difference").GetDecimal()).Should().Be(250_000m);
        previa.GetProperty("requiresAcceptance").GetBoolean().Should().BeTrue();

        // Sin aceptar, 422; quien activa sin poder aceptar la diferencia, 422; el administrador con motivo, activa.
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, ruta, new { cutoffDate = corte }),
            "Inventory.Activation.Difference");
        await InventarioE2E.RolAsync(http, esc.Coop, "ACTIVADOR", null, extra: ["Inventory.Warehouses.View", "Inventory.Warehouses.Activate"]);
        var activador = await InventarioE2E.UsuarioAsync(fx, http, esc.Coop, "activador.saldocontable@coop.inventario.test", "ACTIVADOR");
        await InventarioE2E.AlcanceAsync(http, esc.Admin, activador.UsuarioPublicId, esc.Bodega("PRIN"));
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, activador.Token, HttpMethod.Post, ruta,
            new { cutoffDate = corte, acceptDifference = true, reason = "Sin permiso de aceptar" }), "Inventory.Activation.AcceptDifferenceNotAllowed");
        var activada = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, ruta,
            new { cutoffDate = corte, acceptDifference = true, reason = "ABARROTES difiere en 250.000 contra SOLIDO; se ajusta en Contabilidad" });
        activada.GetProperty("differenceAccepted").GetBoolean().Should().BeTrue();

        // SaldoInicialCargado: recibo sin comprobante (informativo).
        await fx.Despachador.PasadaAsync(esc.Coop.TenantPublicId);
        (await Accounting.ContabilidadDeInventarioE2E.EnteroAsync(fx, esc,
            """SELECT COUNT(*) FROM dbo."ACC_InventoryPostings" WHERE "MessageType" = 'SaldoInicialCargado' AND "AccountingDocumentId" IS NULL""",
            "SELECT COUNT(*) FROM [dbo].[ACC_InventoryPostings] WHERE [MessageType] = 'SaldoInicialCargado' AND [AccountingDocumentId] IS NULL"))
            .Should().Be(1, "el saldo inicial queda en Contabilidad como recibo sin comprobante (US4-5)");
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
