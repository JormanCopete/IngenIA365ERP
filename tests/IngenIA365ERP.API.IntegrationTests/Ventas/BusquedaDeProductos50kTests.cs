using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// Una prueba de rendimiento que sólo corre con <c>RUN_PERF_TESTS=1</c> y, sin ella, se reporta <b>omitida</b> con su motivo
/// —nunca un <c>return</c> al principio que la cuente como aprobada sin haber medido nada (T197; CLAUDE.md, «el verde tapa
/// siete métodos»)—. (nuevo)
/// </summary>
public sealed class FactDeRendimientoAttribute : FactAttribute
{
    public const string Variable = "RUN_PERF_TESTS";

    public FactDeRendimientoAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
            Skip = $"Prueba de rendimiento: corre sólo con {Variable}=1.";
    }
}

/// <summary>
/// T197 (SC-009, FR-020, T43; decisiones-transversales §2.18, nombre fijo), en el motor de <c>DB_PROVIDER</c>: con 50.000
/// productos con código de barras, <c>GET /api/inventory/products/search</c> responde por código, código de barras, nombre y
/// referencia con p95 menor a un segundo. Los productos se siembran por la plantilla 6 (una sola aplicación de 50.000 filas,
/// el mismo camino que la cooperativa). Sin <c>RUN_PERF_TESTS=1</c> se reporta omitida (<see cref="FactDeRendimientoAttribute"/>).
/// US5 (T568) agrega aquí el caso de <c>GET /api/inventory/pos/lookup</c>: la lectura exacta por código de barras en el POS.
///
/// <para>
/// Cooperativa aislada «busqueda50k».
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class BusquedaDeProductos50kTests(CentralIdentityApiFixture fx)
{
    private const int Productos = 50_000;
    private const int Consultas = 100;

    [FactDeRendimiento]
    public async Task La_busqueda_sobre_50000_productos_responde_con_p95_menor_a_un_segundo()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "busqueda50k");
        using var http = fx.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(20);
        var t = coop.TokenAdmin;

        await ImportarAsync(http, t, "/api/inventory/accounting-groups", Libro("Datos", ["codigo", "nombre"], [["ABARROTES", "Abarrotes"]]));
        await ImportarAsync(http, t, "/api/inventory/product-categories", Libro("Datos", ["codigo", "nombre"], [["GENERAL", "General"]]));
        await ImportarAsync(http, t, "/api/inventory/products", LibroDeProductos());

        var aleatorio = new Random(20260925);
        var casos = new (string Nombre, Func<int, string> Consulta)[]
        {
            ("código", i => $"PRD{i:00000}"),
            ("código de barras", i => $"770{i:0000000000}"),
            ("nombre", i => $"ARTICULO {i:00000}"),
            ("referencia", i => $"REF-{i:00000}"),
        };

        foreach (var (nombre, consulta) in casos)
        {
            var tiempos = new List<double>(Consultas);
            for (var n = 0; n < Consultas; n++)
            {
                var q = Uri.EscapeDataString(consulta(aleatorio.Next(1, Productos + 1)));
                var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/inventory/products/search?q={q}");
                peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
                var reloj = Stopwatch.StartNew();
                var resp = await http.SendAsync(peticion);
                reloj.Stop();
                resp.StatusCode.Should().Be(HttpStatusCode.OK);
                tiempos.Add(reloj.Elapsed.TotalMilliseconds);
            }
            tiempos.Sort();
            var p95 = tiempos[(int)Math.Ceiling(tiempos.Count * 0.95) - 1];
            p95.Should().BeLessThan(1000, $"búsqueda por {nombre}: p95 {p95:N0} ms sobre {Productos:N0} productos (SC-009)");
        }
    }

    /// <summary>
    /// T568 (US5, SC-009, T43): con los mismos 50.000 productos, la lectura exacta del lector en el POS
    /// (<c>GET /api/inventory/pos/lookup</c>) responde con p95 menor a un segundo. Cien productos al azar llevan precio en la lista
    /// general (la lectura trae el precio); la búsqueda recorre el índice de los 50.000 códigos. Cooperativa aislada «lookup50k».
    /// </summary>
    [FactDeRendimiento]
    public async Task La_lectura_del_POS_sobre_50000_productos_responde_con_p95_menor_a_un_segundo()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "lookup50k");
        using var http = fx.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(20);
        var t = coop.TokenAdmin;

        await ImportarAsync(http, t, "/api/inventory/accounting-groups", Libro("Datos", ["codigo", "nombre"], [["ABARROTES", "Abarrotes"]]));
        await ImportarAsync(http, t, "/api/inventory/product-categories", Libro("Datos", ["codigo", "nombre"], [["GENERAL", "General"]]));
        await ImportarAsync(http, t, "/api/inventory/products", LibroDeProductos());

        // Un punto con su caja y una sesión abierta del administrador; la bodega activa desde el mes antepasado.
        var tipos = (await InventarioE2E.GetAsync(http, t, "/api/inventory/warehouse-types")).EnumerateArray().ToList();
        var bodega = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/warehouses", new
        {
            code = "PV1", name = "Punto de venta 1", branchPublicId = coop.SucursalPrincipal,
            warehouseTypePublicId = tipos.Single(w => w.GetProperty("code").GetString() == "PUNTOVENTA").GetProperty("publicId").GetGuid(),
            transitWarehouse = new { code = "TR1", name = "Tránsito" },
        })).GetProperty("warehouse").GetProperty("publicId").GetGuid();
        var corte = EscenarioDeInventario.CortePorDefecto;
        await EscenarioDeInventario.ActivarAsync(http, t, bodega, corte);
        var desde = corte.AddDays(1);
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/EINV/Dian.ObligadaAFacturar/versions", new
        {
            scopeKind = "None", value = "false", validFrom = desde.ToString("yyyy-MM-dd"), reason = "Ensayo sin facturación electrónica",
        });
        var rv = await EscenarioDeVentas.TipoAsync(http, t, "RV", "Comprobante de venta", "NonElectronicSalesReceipt", desde);
        var canal = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/sales-channels", new { code = "MOSTRADOR", name = "Mostrador" }))
            .GetProperty("publicId").GetGuid();
        var punto = await EscenarioDeVentas.PuntoAsync(http, t, "PV1", "Punto uno", coop.SucursalPrincipal, canal, bodega, posHabilitado: true);
        var caja = await EscenarioDeVentas.CajaAsync(http, t, punto, "CJ1", "Caja 1", bodega, null, [("PosSale", rv)]);
        var sesion = await EscenarioDeVentas.AbrirAsync(http, t, caja);

        var aleatorio = new Random(20260927);
        var conPrecio = Enumerable.Range(0, Consultas).Select(_ => aleatorio.Next(1, Productos + 1)).Distinct().ToList();
        var productos = new List<object>();
        foreach (var i in conPrecio)
        {
            var id = (await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/search?q=PRD{i:00000}")).GetProperty("exact").GetProperty("publicId").GetGuid();
            var detalle = await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/{id}");
            productos.Add(new { productPublicId = id, unitPublicId = detalle.GetProperty("baseUnit").GetProperty("publicId").GetGuid(), price = 1_000m + i });
        }
        var lista = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/price-lists", new
        {
            code = "GENERAL", name = "Lista general", includesTaxes = true, scope = new { }, validFrom = desde.ToString("yyyy-MM-dd"), reason = "SC-009",
        })).GetProperty("priceListPublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{lista}/items", new { items = productos, reason = "SC-009" });

        var tiempos = new List<double>(conPrecio.Count);
        foreach (var i in conPrecio)
        {
            var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/inventory/pos/lookup?code=770{i:0000000000}&cashSession={sesion}");
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
            var reloj = Stopwatch.StartNew();
            var resp = await http.SendAsync(peticion);
            reloj.Stop();
            resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
            tiempos.Add(reloj.Elapsed.TotalMilliseconds);
        }
        tiempos.Sort();
        var p95 = tiempos[(int)Math.Ceiling(tiempos.Count * 0.95) - 1];
        p95.Should().BeLessThan(1000, $"lectura del POS: p95 {p95:N0} ms sobre {Productos:N0} productos (SC-009)");
    }

    private static byte[] LibroDeProductos()
    {
        using var libro = new XLWorkbook();
        var productos = libro.Worksheets.Add("Productos");
        string[] enc = ["codigo", "nombre", "tipo", "categoria", "unidadBase", "grupoContable", "tratamientoIva", "conceptoRetencion", "referencia"];
        for (var c = 0; c < enc.Length; c++) productos.Cell(1, c + 1).Value = enc[c];
        var codigos = libro.Worksheets.Add("CodigosDeBarras");
        codigos.Cell(1, 1).Value = "producto";
        codigos.Cell(1, 2).Value = "codigoDeBarras";
        for (var i = 1; i <= Productos; i++)
        {
            var fila = i + 1;
            var codigo = $"PRD{i:00000}";
            object[] valores = [codigo, $"ARTICULO {i:00000}", "Inventoriable", "GENERAL", "UND", "ABARROTES", "Excluded", "COMPRAS", $"REF-{i:00000}"];
            for (var c = 0; c < valores.Length; c++) productos.Cell(fila, c + 1).SetValue(valores[c].ToString());
            codigos.Cell(fila, 1).SetValue(codigo);
            codigos.Cell(fila, 2).SetValue($"770{i:0000000000}");
        }
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] Libro(string hoja, string[] encabezados, string[][] filas)
    {
        using var libro = new XLWorkbook();
        var h = libro.Worksheets.Add(hoja);
        for (var c = 0; c < encabezados.Length; c++) h.Cell(1, c + 1).Value = encabezados[c];
        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++) h.Cell(f + 2, c + 1).SetValue(filas[f][c]);
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    private static async Task ImportarAsync(HttpClient http, string token, string ruta, byte[] contenido)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        formulario.Add(archivo, "file", "carga.xlsx");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"{ruta}/import?mode=apply") { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await http.SendAsync(InventarioE2E.ConClave(peticion));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"{ruta}: {await resp.Content.ReadAsStringAsync()}");
    }
}
