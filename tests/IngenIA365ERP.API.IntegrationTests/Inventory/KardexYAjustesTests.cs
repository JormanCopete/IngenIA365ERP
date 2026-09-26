using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T247 (quickstart §3.7; US1-2, US2-2 a US2-5; SC-002, SC-006, SC-013), en el motor de <c>DB_PROVIDER</c>, sobre el escenario
/// aislado «kardex»: la caja entra al kardex en unidades; un ajuste confirmado no se edita ni se descarta y se anula con un
/// documento contrario una sola vez; la misma <c>Idempotency-Key</c> tres veces da un solo número; la integridad detecta una
/// proyección alterada por SQL y la reconstrucción la deja en cero; un ajuste negativo sobre el umbral de su tipo espera
/// aprobación sin tocar la existencia y su creador no la decide; y quien no tiene alcance sobre PRIN recibe el 404.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class KardexYAjustesTests(CentralIdentityApiFixture fx)
{
    private const string Ajustes = "/api/inventory/adjustments";

    private Task<EscenarioDeInventario> EscenarioAsync() => EscenarioDeInventario.PrepararAsync(fx, "kardex");

    [Fact]
    public async Task Tres_cajas_entran_al_kardex_como_36_unidades_y_el_documento_sigue_en_cajas()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();

        var id = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 3, 1_300m * 12, EnCajas: true)]);
        var confirmado = await InventarioE2E.ConfirmarAsync(http, esc.Admin, Ajustes, id);
        confirmado.GetProperty("status").GetInt32().Should().Be(2, "Confirmed");

        var doc = await InventarioE2E.GetAsync(http, esc.Admin, $"{Ajustes}/{id}");
        var linea = doc.GetProperty("lines")[0];
        linea.GetProperty("quantity").GetDecimal().Should().Be(3m);
        linea.GetProperty("quantityBase").GetDecimal().Should().Be(36m);
        linea.GetProperty("unit").GetProperty("code").GetString().Should().Be("CAJA12");

        (await esc.FisicaAsync(http, esc.Admin, "P1", "PRIN")).Should().Be(36m);
        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        var kardex = await InventarioE2E.InformeAsync(http, esc.Admin, "kardex", $"product={esc.P("P1").Id}&warehouse={esc.Bodega("PRIN")}&from={hoy}&to={hoy}");
        kardex.Filas.Should().Contain(f => kardex.Numero(f, "Entrada") == 36m);
    }

    [Fact]
    public async Task Un_ajuste_confirmado_no_se_edita_y_se_anula_una_sola_vez_con_un_documento_contrario()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();

        var id = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P2", 10, 500m)]);
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, Ajustes, id);

        var doc = await InventarioE2E.GetAsync(http, esc.Admin, $"{Ajustes}/{id}");
        var acciones = doc.GetProperty("allowedActions").EnumerateArray().Select(a => a.ToString()).ToList();
        acciones.Should().NotContain(["Edit", "Discard", "1", "2"], "un confirmado no se edita ni se descarta (SC-013)");

        var editar = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Put, $"{Ajustes}/{id}", new
        {
            documentTypePublicId = esc.Tipos["AJP"], warehousePublicId = esc.Bodega("PRIN"), lines = esc.Lineas(new EscenarioDeInventario.Linea("P2", 99)),
        });
        await InventarioE2E.FallaAsync(editar, "Inventory.Document.NotDraft");
        var descartar = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Ajustes}/{id}/discard", new { reason = "no" });
        await InventarioE2E.FallaAsync(descartar, "Inventory.Document.NotDraft");

        var anular = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Ajustes}/{id}/void", new { reason = "Error de digitación" });
        anular.StatusCode.Should().Be(HttpStatusCode.Created, await anular.Content.ReadAsStringAsync());
        var anulacion = (await InventarioE2E.LeerAsync(anular)).GetProperty("voidingDocumentPublicId").GetGuid();

        var original = await InventarioE2E.GetAsync(http, esc.Admin, $"{Ajustes}/{id}");
        original.GetProperty("voidedBy").GetProperty("publicId").GetGuid().Should().Be(anulacion);
        (await esc.FisicaAsync(http, esc.Admin, "P2", "PRIN")).Should().Be(0m, "la existencia vuelve a la de antes");

        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        var kardex = await InventarioE2E.InformeAsync(http, esc.Admin, "kardex", $"product={esc.P("P2").Id}&warehouse={esc.Bodega("PRIN")}&from={hoy}&to={hoy}");
        kardex.Filas.Should().Contain(f => kardex.Numero(f, "Entrada") == 10m);
        kardex.Filas.Should().Contain(f => kardex.Numero(f, "Salida") == 10m, "la anulación deja su propia línea");

        var otraVez = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Ajustes}/{id}/void", new { reason = "Otra vez" });
        await InventarioE2E.FallaAsync(otraVez, "Inventory.Document.AlreadyVoided");
    }

    [Fact]
    public async Task La_misma_clave_tres_veces_da_un_solo_numero()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var id = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P4", 5, 1_000m)]);
        var clave = Guid.NewGuid();

        var respuestas = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++) respuestas.Add(await InventarioE2E.PedirConfirmarAsync(http, esc.Admin, Ajustes, id, clave));

        respuestas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        respuestas[0].Headers.Contains("Idempotent-Replayed").Should().BeFalse();
        respuestas.Skip(1).Should().OnlyContain(r => r.Headers.GetValues("Idempotent-Replayed").Single() == "true");
        var numeros = new List<long>();
        foreach (var r in respuestas) numeros.Add((await InventarioE2E.LeerAsync(r)).GetProperty("number").GetInt64());
        numeros.Distinct().Should().ContainSingle();
        (await esc.FisicaAsync(http, esc.Admin, "P4", "PRIN")).Should().Be(5m, "un solo efecto");
    }

    [Fact]
    public async Task La_integridad_detecta_una_proyeccion_alterada_y_la_reconstruccion_la_corrige()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P5", 8, 700m)]);
        var filtro = new { productPublicIds = new[] { esc.P("P5").Id } };

        var limpia = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/integrity/verify", filtro);
        limpia.GetProperty("incidents").GetArrayLength().Should().Be(0, "sin tocar nada, el kardex y las proyecciones coinciden (SC-006)");

        var alteradas = await InventarioE2E.SqlEnLaCooperativaAsync(fx, esc.Coop,
            """UPDATE dbo."INV_StockBalances" SET "Physical" = "Physical" + 7 WHERE "ProductId" = (SELECT "Id" FROM dbo."INV_Products" WHERE "Code" = 'P5')""",
            "UPDATE [dbo].[INV_StockBalances] SET [Physical] = [Physical] + 7 WHERE [ProductId] = (SELECT [Id] FROM [dbo].[INV_Products] WHERE [Code] = 'P5')");
        alteradas.Should().BeGreaterThan(0);

        var sucia = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/integrity/verify", filtro);
        var incidentes = sucia.GetProperty("incidents").EnumerateArray().ToList();
        incidentes.Should().NotBeEmpty();
        incidentes[0].GetProperty("product").GetProperty("code").GetString().Should().Be("P5");
        sucia.GetProperty("alertPublicId").ValueKind.Should().NotBe(JsonValueKind.Null, "el incidente levanta Inventario.IncidenteDeIntegridad");

        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/integrity/rebuild",
            new { productPublicIds = new[] { esc.P("P5").Id }, reason = "Proyección alterada en el ensayo" });
        var reconstruida = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/integrity/verify", filtro);
        reconstruida.GetProperty("incidents").GetArrayLength().Should().Be(0);
        (await esc.FisicaAsync(http, esc.Admin, "P5", "PRIN")).Should().Be(8m, "el kardex es la fuente");
    }

    [Fact]
    public async Task Un_ajuste_negativo_sobre_el_umbral_espera_aprobacion_y_su_creador_no_la_decide()
    {
        var esc = await EscenarioAsync();
        var usuarios = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        await InventarioE2E.PoliticaAsync(http, esc.Admin, esc.Tipos["AJN"], InventarioE2E.HoyEnColombia, (100_000m, "Inventory.Adjustments.Approve"));
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P3", 100, 2_000m)]);

        var id = await esc.AjusteAsync(http, esc.Admin, "AJN", "PRIN", [new("P3", 60)], causa: "MERMA");
        var resultado = await InventarioE2E.ConfirmarAsync(http, esc.Admin, Ajustes, id);
        resultado.GetProperty("status").GetInt32().Should().Be(1, "PendingApproval");
        resultado.TryGetProperty("number", out var numero).Should().BeTrue();
        numero.ValueKind.Should().Be(JsonValueKind.Null, "en aprobación no tiene número");
        (await esc.FisicaAsync(http, esc.Admin, "P3", "PRIN")).Should().Be(100m, "no toca la existencia hasta aprobarse");
        var solicitud = resultado.GetProperty("approval").GetProperty("requestPublicId").GetGuid();

        var propia = await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Admin, solicitud, aprobar: true);
        await InventarioE2E.FallaAsync(propia, "Approvals.SelfApprovalForbidden");

        var aprobada = await InventarioE2E.DecidirAsync(http, esc.Admin, usuarios.Aprobador.Token, solicitud, aprobar: true);
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
        (await InventarioE2E.GetAsync(http, esc.Admin, $"{Ajustes}/{id}")).GetProperty("status").GetInt32().Should().Be(2, "Confirmed");
        (await esc.FisicaAsync(http, esc.Admin, "P3", "PRIN")).Should().Be(40m);
    }

    [Fact]
    public async Task Sin_alcance_sobre_PRIN_la_existencia_el_detalle_y_la_confirmacion_son_404()
    {
        var esc = await EscenarioAsync();
        var usuarios = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        var id = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P6", 1, 100m)]);
        var b = usuarios.BodegaB.Token;

        (await InventarioE2E.MandarAsync(http, b, HttpMethod.Get, $"/api/inventory/stock?warehousePublicId={esc.Bodega("PRIN")}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await InventarioE2E.MandarAsync(http, b, HttpMethod.Get, $"{Ajustes}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await InventarioE2E.PedirConfirmarAsync(http, b, Ajustes, id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await InventarioE2E.MandarAsync(http, b, HttpMethod.Get, $"{Ajustes}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "lo inexistente responde lo mismo");
    }
}
