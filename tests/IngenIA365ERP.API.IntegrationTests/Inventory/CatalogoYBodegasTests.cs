using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T196, T238 (quickstart §3.2–§3.4; SC-014 en su parte de catálogo; contracts/plantillas.md §0–§7, §10–§13), en el motor de
/// <c>DB_PROVIDER</c>: las plantillas 2 a 7 se descargan con sólo encabezados en la fila 1 y se importan en el orden de §0.1
/// —revisar y aplicar con su propia <c>Idempotency-Key</c>—; el libro de productos de 500 filas con tres errores no se aplica y
/// corregido crea las 500; la lectura de un código de caja elige el empaque <c>CAJA12</c>; un producto citado en un borrador
/// no se borra (<c>Inventory.Product.HasHistory</c>); la primera bodega de una sucursal trae su tránsito y un alta de tipo
/// tránsito es 422; <c>GET /api/inventory/templates</c> dice qué puede hacer cada uno; las plantillas 10 a 13 se descargan
/// vacías y su importación no existe (404); sin permiso, el 404 genérico.
///
/// <para>
/// <b>Escrita, no corrida</b> hasta el par <c>InventarioComercialNucleo</c> (T440), que crea las tablas <c>INV_</c> del catálogo
/// y las bodegas (hoy excluidas por <c>NucleoComercialSinMigracion</c>). El borrador del caso de <c>HasHistory</c> usa la ruta
/// de ajustes de US2 (<c>/api/inventory/adjustments</c>). Se corre en T443. Cooperativa aislada «catalogo».
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CatalogoYBodegasTests(CentralIdentityApiFixture fx)
{
    private const string Coop = "catalogo";

    /// <summary>Las plantillas 2 a 7 en su orden de carga, con la ruta base y sus encabezados obligatorios.</summary>
    public static TheoryData<string, string> Plantillas => new()
    {
        { "/api/inventory/accounting-groups", "Datos" },
        { "/api/inventory/units", "Datos" },
        { "/api/inventory/brands", "Datos" },
        { "/api/inventory/product-categories", "Datos" },
        { "/api/inventory/products", "Productos" },
        { "/api/inventory/warehouses", "Bodegas" },
    };

    /// <summary>Las plantillas 10 a 13: en I1 sólo se descargan (T238).</summary>
    public static TheoryData<string, string> SoloDescarga => new()
    {
        { "/api/inventory/points-of-sale", "PuntosDeVenta" },
        { "/api/core/payment-means", "Franquicias" },
        { "/api/inventory/price-lists", "Listas" },
        { "/api/inventory/discount-caps", "Datos" },
    };

    [Theory]
    [MemberData(nameof(Plantillas))]
    public async Task Las_plantillas_2_a_7_se_descargan_con_solo_encabezados_en_la_fila_1(string ruta, string primeraHoja)
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, Coop);
        using var http = fx.CreateClient();

        var libro = await DescargarAsync(http, coop.TokenAdmin, $"{ruta}/template.xlsx");

        var hoja = libro.Worksheet(primeraHoja);
        hoja.Cell(1, 1).GetString().Should().NotBeNullOrWhiteSpace();
        hoja.Row(2).IsEmpty().Should().BeTrue("la plantilla vacía sólo trae encabezados");
        libro.Worksheets.Last().Name.Should().Be("Instrucciones");
    }

    [Theory]
    [MemberData(nameof(SoloDescarga))]
    public async Task Las_plantillas_10_a_13_se_descargan_vacias_y_no_se_importan_en_I1(string ruta, string primeraHoja)
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, Coop);
        using var http = fx.CreateClient();

        var libro = await DescargarAsync(http, coop.TokenAdmin, $"{ruta}/template.xlsx");
        libro.Worksheet(primeraHoja).Row(2).IsEmpty().Should().BeTrue();

        var importar = await ImportarAsync(http, coop.TokenAdmin, ruta, "review", Libro(("Datos", ["x"], [])));
        importar.StatusCode.Should().Be(HttpStatusCode.NotFound, "la importación de estas plantillas llega con I3");
        (await Peticion(http, coop.TokenAdmin, HttpMethod.Get, $"{ruta}/template.xlsx?withData=true")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task La_lista_de_plantillas_dice_que_puede_hacer_cada_uno()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, Coop);
        using var http = fx.CreateClient();

        var lista = (await InventarioE2E.GetAsync(http, coop.TokenAdmin, "/api/inventory/templates")).EnumerateArray().ToList();

        lista.Select(p => p.GetProperty("number").GetInt32()).Should().Equal(Enumerable.Range(1, 16));
        var productos = lista.Single(p => p.GetProperty("key").GetString() == "inventory.products");
        productos.GetProperty("canDownload").GetBoolean().Should().BeTrue();
        productos.GetProperty("canImport").GetBoolean().Should().BeTrue();
        var puntos = lista.Single(p => p.GetProperty("key").GetString() == "inventory.points-of-sale");
        puntos.GetProperty("canDownload").GetBoolean().Should().BeTrue();
        puntos.GetProperty("canImport").GetBoolean().Should().BeFalse();
        puntos.GetProperty("note").GetString().Should().Contain("I3");
    }

    [Fact]
    public async Task Las_plantillas_se_importan_en_orden_y_el_lector_elige_el_empaque()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, Coop);
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;

        await RevisarYAplicarAsync(http, t, "/api/inventory/accounting-groups",
            Libro(("Datos", ["codigo", "nombre"], [["ABARROTES", "Abarrotes"], ["SERVICIOS", "Servicios"]])));
        await RevisarYAplicarAsync(http, t, "/api/inventory/units",
            Libro(("Datos", ["codigo", "nombre", "decimales", "codigoDian"], [["CAJA12", "Caja x 12", "0", "XBX"]])));
        await RevisarYAplicarAsync(http, t, "/api/inventory/brands", Libro(("Datos", ["codigo", "nombre"], [["DIANA", "Arroz Diana"]])));
        await RevisarYAplicarAsync(http, t, "/api/inventory/product-categories",
            Libro(("Datos", ["codigo", "nombre", "padre"], [["ARROZ", "Arroz", "ALIM"], ["ALIM", "Alimentos", null]])));

        // Productos: 500 filas con tres errores (código de otro producto, unidad inexistente, tarifa inexistente).
        await RevisarYAplicarAsync(http, t, "/api/inventory/products", LibroDeProductos(1, 1, sinErrores: true));
        var conErrores = LibroDeProductos(2, 500, sinErrores: false);
        var revision = await InventarioE2E.LeerAsync(await ImportarAsync(http, t, "/api/inventory/products", "review", conErrores));
        revision.GetProperty("valid").GetBoolean().Should().BeFalse();
        var errores = revision.GetProperty("errors").EnumerateArray().ToList();
        errores.Should().HaveCount(3);
        errores.Should().Contain(e => e.GetProperty("code").GetString() == "Inventory.Barcode.Duplicate" && e.GetProperty("message").GetString()!.Contains("P001"));
        var aplicar = await ImportarAsync(http, t, "/api/inventory/products", "apply", conErrores);
        aplicar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(aplicar)).Should().Be("Import.Invalid");

        var corregido = LibroDeProductos(2, 500, sinErrores: true);
        var aplicado = await InventarioE2E.LeerAsync(await RevisarYAplicarAsync(http, t, "/api/inventory/products", corregido));
        aplicado.GetProperty("sheets").EnumerateArray().Single(h => h.GetProperty("sheet").GetString() == "Productos")
            .GetProperty("created").GetInt32().Should().Be(499);

        // La caja de P001 elige P001 con CAJA12 (US1-5).
        var busqueda = await InventarioE2E.GetAsync(http, t, "/api/inventory/products/search?q=CJ-0001");
        var exacta = busqueda.GetProperty("exact");
        exacta.GetProperty("code").GetString().Should().Be("P001");
        exacta.GetProperty("packUnit").GetProperty("unitCode").GetString().Should().Be("CAJA12");
        exacta.GetProperty("packUnit").GetProperty("factor").GetDecimal().Should().Be(12m);

        // Bodegas: la sucursal Principal no tiene código, así que la fila de tránsito fija el suyo.
        var bodegas = Libro(
            ("Bodegas", ["codigo", "nombre", "sucursal", "tipo"], [["B01", "Bodega principal", "Principal", "PRINCIPAL"], ["TRPPAL", "Tránsito Principal", "Principal", "TRANSITO"]]),
            ("Ubicaciones", ["bodega", "codigo", "nombre", "porDefecto"], [["B01", "A-01", "Pasillo A", "sí"]]));
        await RevisarYAplicarAsync(http, t, "/api/inventory/warehouses", bodegas);
        var lista = (await InventarioE2E.GetAsync(http, t, "/api/inventory/warehouses?includeTransit=true")).EnumerateArray().ToList();
        lista.Select(w => w.GetProperty("code").GetString()).Should().Contain(["B01", "TRPPAL"]);
        lista.Should().OnlyContain(w => w.GetProperty("activationStatus").GetInt32() == 0, "la plantilla nunca activa");
    }

    [Fact]
    public async Task La_primera_bodega_de_una_sucursal_trae_su_transito_y_el_transito_no_se_crea_a_mano()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, Coop);
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;

        var sucursal = await Peticion(http, t, HttpMethod.Post, "/api/core/branches", new { code = "FL", name = "Florida", shortName = "FLO" });
        sucursal.IsSuccessStatusCode.Should().BeTrue(await sucursal.Content.ReadAsStringAsync());
        var sucursales = await InventarioE2E.GetAsync(http, t, "/api/core/branches?PageNumber=1&PageSize=50");
        var florida = sucursales.GetProperty("items").EnumerateArray().Single(b => b.GetProperty("name").GetString() == "Florida").GetProperty("publicId").GetGuid();
        var tipos = (await InventarioE2E.GetAsync(http, t, "/api/inventory/warehouse-types")).EnumerateArray().ToList();
        Guid Tipo(string codigo) => tipos.Single(x => x.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();

        var prin = await Peticion(http, t, HttpMethod.Post, "/api/inventory/warehouses",
            new { code = "PRIN", name = "Principal Florida", branchPublicId = florida, warehouseTypePublicId = Tipo("PRINCIPAL") });
        prin.StatusCode.Should().Be(HttpStatusCode.Created, await prin.Content.ReadAsStringAsync());
        var creada = await InventarioE2E.LeerAsync(prin);
        creada.GetProperty("transitWarehouseCreated").GetProperty("code").GetString().Should().Be("TRFL");
        creada.GetProperty("warnings").EnumerateArray().Should().Contain(w => w.GetProperty("code").GetString() == "Inventory.Branch.MunicipalityMissing");

        var transito = await Peticion(http, t, HttpMethod.Post, "/api/inventory/warehouses",
            new { code = "TR2", name = "Otro tránsito", branchPublicId = florida, warehouseTypePublicId = Tipo("TRANSITO") });
        transito.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(transito)).Should().Be("Inventory.WarehouseType.TransitIsSystem");
    }

    [Fact]
    public async Task Un_producto_citado_en_un_borrador_no_se_borra()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, Coop);
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;

        await RevisarYAplicarAsync(http, t, "/api/inventory/accounting-groups", Libro(("Datos", ["codigo", "nombre"], [["BORRAR", "Para borrar"]])));
        await RevisarYAplicarAsync(http, t, "/api/inventory/product-categories", Libro(("Datos", ["codigo", "nombre"], [["VARIOS", "Varios"]])));
        await RevisarYAplicarAsync(http, t, "/api/inventory/products", Libro(("Productos", EncabezadosDeProductos,
            [["BORR-1", "Con historia", "Inventoriable", "VARIOS", null, "UND", "BORRAR", "Excluded", null, "COMPRAS"],
             ["BORR-2", "Sin historia", "Inventoriable", "VARIOS", null, "UND", "BORRAR", "Excluded", null, "COMPRAS"]])));
        await RevisarYAplicarAsync(http, t, "/api/inventory/warehouses",
            Libro(("Bodegas", ["codigo", "nombre", "sucursal", "tipo"], [["BH1", "Bodega historia", "Principal", "PRINCIPAL"], ["TRH", "Tránsito", "Principal", "TRANSITO"]])));

        var productos = (await InventarioE2E.GetAsync(http, t, "/api/inventory/products?search=BORR")).GetProperty("items").EnumerateArray().ToList();
        var conHistoria = productos.Single(p => p.GetProperty("code").GetString() == "BORR-1").GetProperty("publicId").GetGuid();
        var sinHistoria = productos.Single(p => p.GetProperty("code").GetString() == "BORR-2").GetProperty("publicId").GetGuid();
        var detalle = await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/{conHistoria}");
        var unidad = detalle.GetProperty("baseUnit").GetProperty("publicId").GetGuid();
        var bodega = (await InventarioE2E.GetAsync(http, t, "/api/inventory/warehouses")).EnumerateArray()
            .Single(w => w.GetProperty("code").GetString() == "BH1").GetProperty("publicId").GetGuid();
        var tipoAjuste = (await InventarioE2E.GetAsync(http, t, "/api/inventory/document-types?class=PositiveAdjustment")).EnumerateArray().First()
            .GetProperty("publicId").GetGuid();

        var borrador = await Peticion(http, t, HttpMethod.Post, "/api/inventory/adjustments", new
        {
            documentTypePublicId = tipoAjuste, warehousePublicId = bodega, reason = "Prueba de historia",
            lines = new[] { new { productPublicId = conHistoria, unitPublicId = unidad, quantity = 1m } },
        });
        borrador.StatusCode.Should().Be(HttpStatusCode.Created, await borrador.Content.ReadAsStringAsync());

        var borrar = await Peticion(http, t, HttpMethod.Delete, $"/api/inventory/products/{conHistoria}");
        borrar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(borrar)).Should().Be("Inventory.Product.HasHistory");
        var datos = (await InventarioE2E.LeerAsync(borrar)).GetProperty("data").GetProperty("alternatives").EnumerateArray().Select(a => a.GetString());
        datos.Should().BeEquivalentTo(["Inactive", "Blocked"]);

        (await Peticion(http, t, HttpMethod.Delete, $"/api/inventory/products/{sinHistoria}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Sin_permiso_las_rutas_del_catalogo_responden_el_404_generico()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, Coop);
        using var http = fx.CreateClient();
        var operador = await OperadorAsync(http, coop);

        (await Peticion(http, operador, HttpMethod.Get, "/api/inventory/products")).StatusCode.Should().Be(HttpStatusCode.OK, "Operator lee el catálogo");
        foreach (var (metodo, ruta) in new[]
                 {
                     (HttpMethod.Post, "/api/inventory/units"), (HttpMethod.Post, "/api/inventory/brands"),
                     (HttpMethod.Post, "/api/inventory/products"), (HttpMethod.Delete, $"/api/inventory/products/{Guid.NewGuid()}"),
                     (HttpMethod.Post, "/api/inventory/warehouses"), (HttpMethod.Put, "/api/inventory/reorder-policies"),
                 })
        {
            var r = await Peticion(http, operador, metodo, ruta, new { });
            r.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{metodo} {ruta} sin permiso es indistinguible de inexistente");
        }
        (await ImportarAsync(http, operador, "/api/inventory/products", "review", Libro(("Productos", EncabezadosDeProductos, []))))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static readonly string[] EncabezadosDeProductos =
        ["codigo", "nombre", "tipo", "categoria", "marca", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion"];

    /// <summary>Productos P{desde}..P{hasta}; P001 con su caja CJ-0001 (CAJA12). Con errores: fila 10 repite el código de P001, 20 sin unidad, 30 con tarifa inexistente.</summary>
    private static byte[] LibroDeProductos(int desde, int hasta, bool sinErrores)
    {
        var productos = new List<string?[]>();
        var codigos = new List<string?[]>();
        for (var i = desde; i <= hasta; i++)
        {
            var codigo = $"P{i:000}";
            var unidad = !sinErrores && i == 20 ? "NOEXISTE" : "UND";
            var tarifa = !sinErrores && i == 30 ? "IVA99" : (i % 2 == 0 ? "IVA19" : null);
            productos.Add([codigo, $"PRODUCTO {i}", "Inventoriable", "ARROZ", "DIANA", unidad, "ABARROTES", tarifa is null ? "Excluded" : "Taxed", tarifa, "COMPRAS"]);
            codigos.Add([codigo, !sinErrores && i == 10 ? "B-0001" : $"B-{i:0000}", null]);
        }
        var unidades = new List<string?[]>();
        if (desde == 1)
        {
            unidades.Add(["P001", "CAJA12", "12", "Both"]);
            codigos.Add(["P001", "CJ-0001", "CAJA12"]);
        }
        return Libro(("Productos", EncabezadosDeProductos, productos), ("CodigosDeBarras", ["producto", "codigoDeBarras", "unidad"], codigos),
            ("Unidades", ["producto", "unidad", "factor", "uso"], unidades));
    }

    /// <summary>Un libro con una hoja por sección: encabezados en la fila 1 y los datos debajo.</summary>
    private static byte[] Libro(params (string Hoja, string[] Encabezados, IReadOnlyList<string?[]> Filas)[] hojas)
    {
        using var libro = new XLWorkbook();
        foreach (var (nombre, encabezados, filas) in hojas)
        {
            var hoja = libro.Worksheets.Add(nombre);
            for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
            for (var f = 0; f < filas.Count; f++)
                for (var c = 0; c < filas[f].Length; c++)
                    if (filas[f][c] is { } v) hoja.Cell(f + 2, c + 1).SetValue(v);
        }
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    private static async Task<XLWorkbook> DescargarAsync(HttpClient http, string token, string url)
    {
        var resp = await Peticion(http, token, HttpMethod.Get, url);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, url);
        return new XLWorkbook(new MemoryStream(await resp.Content.ReadAsByteArrayAsync()));
    }

    /// <summary>Revisa (sin errores) y aplica el mismo libro, cada paso con su propia clave (§0.5).</summary>
    private static async Task<HttpResponseMessage> RevisarYAplicarAsync(HttpClient http, string token, string ruta, byte[] libro)
    {
        var revision = await ImportarAsync(http, token, ruta, "review", libro);
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var cuerpo = await InventarioE2E.LeerAsync(revision);
        cuerpo.GetProperty("valid").GetBoolean().Should().BeTrue($"{ruta}: {cuerpo}");
        var aplicado = await ImportarAsync(http, token, ruta, "apply", libro);
        aplicado.StatusCode.Should().Be(HttpStatusCode.OK, await aplicado.Content.ReadAsStringAsync());
        return aplicado;
    }

    private static Task<HttpResponseMessage> ImportarAsync(HttpClient http, string token, string ruta, string modo, byte[] contenido)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        formulario.Add(archivo, "file", "plantilla.xlsx");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"{ruta}/import?mode={modo}") { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }

    private static Task<HttpResponseMessage> Peticion(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo = null)
    {
        var peticion = new HttpRequestMessage(metodo, url);
        if (cuerpo is not null) peticion.Content = JsonContent.Create(cuerpo);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(metodo == HttpMethod.Get ? peticion : InventarioE2E.ConClave(peticion));
    }

    /// <summary>Un usuario con el rol integrado Operador (sólo <c>*.View</c> del comercio) en la cooperativa aislada.</summary>
    private async Task<string> OperadorAsync(HttpClient http, InventarioE2E.CooperativaAislada coop)
    {
        const string correo = "operador.catalogo@coop.inventario.test";
        var invitacion = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/tenants/{coop.TenantPublicId}/invitations", new { email = correo });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, await invitacion.Content.ReadAsStringAsync());
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        mensaje.Should().NotBeNull();
        var token = System.Text.RegularExpressions.Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        var aceptar = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, registration = new { password = "Operador-Catalogo-2026!" } });
        aceptar.StatusCode.Should().Be(HttpStatusCode.OK, await aceptar.Content.ReadAsStringAsync());
        var acceso = (await InventarioE2E.LeerAsync(aceptar)).GetProperty("accessToken").GetString()!;

        var roles = await InventarioE2E.GetAsync(http, coop.TokenAdmin, "/api/admin/roles?includeBuiltIn=true&pageSize=50");
        var rol = roles.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "Operator").GetProperty("publicId").GetGuid();
        var usuarios = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"/api/admin/users?search={Uri.EscapeDataString(correo)}&pageSize=50");
        var usuario = usuarios.GetProperty("items").EnumerateArray()
            .Single(u => string.Equals(u.GetProperty("email").GetString(), correo, StringComparison.OrdinalIgnoreCase)).GetProperty("publicId").GetGuid();
        var asignacion = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = rol });
        asignacion.IsSuccessStatusCode.Should().BeTrue(await asignacion.Content.ReadAsStringAsync());
        return acceso;
    }
}
