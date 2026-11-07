using System.Diagnostics;
using System.Globalization;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Implantacion;

/// <summary>
/// El 2026-11-06 los dos pods de la API cayeron por <c>OOMKilled</c> (1 GiB) mientras se aplicaban 11.949 personas por
/// plantilla; con tandas de 2.000 pasó. Esto sube ese mismo volumen por HTTP, con las 34 columnas llenas, contra la pila
/// real, y muestrea la memoria del proceso mientras dura cada petición: revisión, revisión en Excel y aplicación.
///
/// <para>
/// El pico de heap bajo Server GC no dice cuánto hace falta: sin presión, la basura no se recoge. Para ver lo vivo:
/// <c>DOTNET_gcServer=0</c>, <c>DOTNET_gcConcurrent=0</c>, <c>DOTNET_GCgen0size=1000000</c>; lo exacto, un <c>dotnet-gcdump</c> en plena
/// aplicación (el 2026-11-06 dio ~200 MB vivos sobre la base). Las cifras y qué se concluyó, en
/// <c>docs/operaciones/memoria-de-la-api.md</c>.
/// </para>
/// <para>
/// Sólo corre con <c>RUN_PERF_TESTS=1</c>, como las demás de rendimiento. Con <c>PERF_MEMORIA_SALIDA</c> deja las cifras en
/// ese archivo. Sin la variable hace <c>return</c> y se cuenta como pasada sin medir.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class MemoriaDeImportacionTests(CentralIdentityApiFixture fx)
{
    private const int Filas = 11_949;

    private static readonly Dictionary<string, string> Resumenes = [];

    private static readonly string[] Encabezados =
    [
        "tipoDocumento", "documento", "naturaleza", "primerNombre", "otrosNombres", "primerApellido", "segundoApellido",
        "lugarExpedicion", "fechaExpedicion", "correo", "telefono", "telefono2", "celular", "direccion", "fechaNacimiento",
        "genero", "estadoCivil", "esProveedor", "esCliente", "responsableIva", "autorretenedor", "agenteRetencionIva",
        "regimenSimple", "declaranteRenta", "obligadoFacturar", "obligadoContabilidad", "granContribuyente",
        "exentoRetencion", "exentoReteIca", "ciiu",
    ];

    private static string?[] Persona(int i)
    {
        var doc = (31_000_000 + i).ToString(CultureInfo.InvariantCulture);
        return
        [
            "CC", doc, "natural", $"NOMBRE{i}", "SEGUNDO", $"APELLIDO{i}", $"OTRO{i % 500}",
            "CALI", "2005-03-01", $"persona{i}@correo.test", "6023334455", "6023334456", "3001234567",
            $"CALLE {i % 100} # {i % 50} - {i % 30} BARRIO CENTRO", "1985-06-15",
            "M", "S", "no", "no", "no", "no", "no", "no", "no", "no", "no", "no", "no", "no", "0010",
        ];
    }

    private sealed record Pico(string Etapa, double Segundos, long Status, long HeapBaseMb, long HeapPicoMb, long VivoPicoMb, long WsBaseMb, long WsPicoMb, long AsignadoMb);

    /// <summary>Muestrea cada 50 ms mientras corre <paramref name="accion"/>.</summary>
    private static async Task<Pico> MedirAsync(string etapa, Func<Task<HttpResponseMessage>> accion)
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        using var proceso = Process.GetCurrentProcess();
        proceso.Refresh();
        var heapBase = GC.GetTotalMemory(false);
        var wsBase = proceso.WorkingSet64;
        var asignadoBase = GC.GetTotalAllocatedBytes(false);
        long heapPico = heapBase, wsPico = wsBase, vivoPico = 0;

        using var fin = new CancellationTokenSource();
        var muestreo = Task.Run(async () =>
        {
            while (!fin.IsCancellationRequested)
            {
                proceso.Refresh();
                heapPico = Math.Max(heapPico, GC.GetTotalMemory(false));
                // Lo que quedó después de la última recolección: lo vivo, sin la basura que Server GC todavía no recoge.
                vivoPico = Math.Max(vivoPico, GC.GetGCMemoryInfo(GCKind.Any).HeapSizeBytes);
                wsPico = Math.Max(wsPico, proceso.WorkingSet64);
                try { await Task.Delay(50, fin.Token); } catch (OperationCanceledException) { }
            }
        });

        using var asignaciones = new AsignacionesPorTipo();
        var reloj = Stopwatch.StartNew();
        using var respuesta = await accion();
        await respuesta.Content.LoadIntoBufferAsync();

        reloj.Stop();
        Resumenes[etapa] = asignaciones.Resumen();
        // La auditoría sale después de responder: se le da tiempo a que aparezca en el pico.
        await Task.Delay(TimeSpan.FromSeconds(15));
        await fin.CancelAsync();
        await muestreo;

        const long Mb = 1024 * 1024;
        return new Pico(etapa, Math.Round(reloj.Elapsed.TotalSeconds, 1), (long)respuesta.StatusCode, heapBase / Mb, heapPico / Mb, vivoPico / Mb, wsBase / Mb, wsPico / Mb,
            (GC.GetTotalAllocatedBytes(false) - asignadoBase) / Mb);
    }

    [Fact]
    public async Task Once_mil_personas_por_plantilla()
    {
        if (Environment.GetEnvironmentVariable("RUN_PERF_TESTS") != "1") return;

        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "memoria016");
        using var http = fx.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(15);

        var libro = InventarioE2E.Libro(("Datos", Encabezados, Enumerable.Range(0, Filas).Select(Persona).ToArray()));
        var lector = new IngenIA365ERP.API.Reports.Importadores.ClosedXmlTabularFileReader();
        var picos = new List<Pico>
        {
            await MedirAsync("solo-lector", async () =>
            {
                var tabla = await lector.LeerHojaAsync(libro, "personas.xlsx", null);
                tabla.Value.Filas.Count.Should().Be(Filas);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            }),
            await MedirAsync("revision", () => InventarioE2E.ImportarAsync(http, coop.TokenAdmin, "/api/core/people", "review", libro)),
            await MedirAsync("revision-xlsx", () => InventarioE2E.ImportarAsync(http, coop.TokenAdmin, "/api/core/people", "review&format=xlsx", libro)),
            await MedirAsync("aplicacion", () => InventarioE2E.ImportarAsync(http, coop.TokenAdmin, "/api/core/people", "apply", libro)),
        };

        var salida = new StringBuilder($"archivo {libro.Length / 1024} KiB, {Filas} filas, {Encabezados.Length} columnas, servidor GC {System.Runtime.GCSettings.IsServerGC}\n");
        foreach (var p in picos) salida.AppendLine(p.ToString()).AppendLine(Resumenes.GetValueOrDefault(p.Etapa));
        Console.WriteLine(salida);
        if (Environment.GetEnvironmentVariable("PERF_MEMORIA_SALIDA") is { Length: > 0 } ruta) await File.AppendAllTextAsync(ruta, salida.ToString());

        picos.Should().OnlyContain(p => p.Status == 200, salida.ToString());
    }
}
