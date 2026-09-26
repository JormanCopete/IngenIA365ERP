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
/// US5 (fases 13–14) agrega aquí el caso de <c>/pos/lookup</c>.
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
