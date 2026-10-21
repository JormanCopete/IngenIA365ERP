using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.NationalValues;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Ayudantes de las e2e de líneas de crédito (feature 013, US1, T0343, T0344): los valores nacionales que las líneas necesitan en la central
/// del host —la usura de consumo y de microcrédito y un IBR— y su réplica a la cooperativa, el cuerpo de una línea por la ruta unitaria
/// (api.md §3.1) y el libro de la plantilla <c>lending.credit-lines</c> (plantillas.md §1). Las tasas van como fracción en la ruta y en
/// puntos en la plantilla. (nuevo)
///
/// <para>
/// <b>La central es una por host</b> (§7.6): en «Cartera e2e» se registran una sola vez por fixture valores de vigencia abierta y
/// parecidos a los reales (consumo 25,62 % EA, microcrédito 55 % EA, IBR 3M 9,25 % NMV); lo que necesita una usura que cambia o un día sin
/// usura va en «Valores nacionales e2e» (<c>UsuraNuevaCierraLineasTests</c>).
/// </para>
/// </summary>
public static class LineasE2E
{
    public const string Lineas = "/api/lending/credit-lines";
    public const string Central = "/api/saas/national-values";

    /// <summary>La clave de usura de consumo, ordinario y comercial en el catálogo de modalidades sembrado (propuesta E4).</summary>
    public const string UsuraDeConsumo = "CONSUMO_Y_ORDINARIO";
    public const decimal TasaDeUsuraDeConsumo = 0.2562m;
    public const string UsuraDeMicrocredito = "MICROCREDITO";
    public const decimal TasaDeUsuraDeMicrocredito = 0.55m;
    public const string ClaveDelIbr = "3M";
    public const decimal Ibr = 0.0925m;

    private static readonly Dictionary<CentralIdentityApiFixture, Task> Registrados = [];
    private static readonly object Cerrojo = new();

    /// <summary>Registra en la central de este host (una vez por fixture) las dos usuras de vigencia abierta y el IBR de hoy.</summary>
    public static Task ValoresDeLaCentralAsync(CentralIdentityApiFixture fx)
    {
        lock (Cerrojo)
        {
            if (!Registrados.TryGetValue(fx, out var tarea))
            {
                tarea = RegistrarDeVerdadAsync(fx);
                Registrados[fx] = tarea;
            }
            return tarea;
        }
    }

    private static async Task RegistrarDeVerdadAsync(CentralIdentityApiFixture fx)
    {
        using var http = fx.CreateClient();
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        var hoy = CarteraE2E.Hoy(fx);
        var inicioDelMes = new DateOnly(hoy.Year, hoy.Month, 1);
        await RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = UsuraDeConsumo, validFrom = inicioDelMes, value = TasaDeUsuraDeConsumo, unit = "EA",
            source = "Superintendencia Financiera, certificación de prueba (e2e)",
        });
        await RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = UsuraDeMicrocredito, validFrom = inicioDelMes, value = TasaDeUsuraDeMicrocredito, unit = "EA",
            source = "Superintendencia Financiera, certificación de prueba (e2e)",
        });
        await RegistrarAsync(http, maestro, new
        {
            code = "IBR", key = ClaveDelIbr, validFrom = hoy, referenceDate = hoy, value = Ibr, unit = "NominalMV", source = "Banco de la República (e2e)",
        });
    }

    /// <summary>Registra en la central; si ya estaba (otra prueba del host lo hizo), sigue.</summary>
    public static async Task<Guid?> RegistrarAsync(HttpClient http, string maestro, object cuerpo)
    {
        var resp = await InventarioE2E.MandarAsync(http, maestro, HttpMethod.Post, Central, cuerpo);
        if (resp.StatusCode == HttpStatusCode.Created) return (await CarteraE2E.LeerAsync(resp)).GetProperty("nationalValuePublicId").GetGuid();
        (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("NationalValues.Value.AlreadyRegistered",
            $"registrar {JsonSerializer.Serialize(cuerpo)}: «{await resp.Content.ReadAsStringAsync()}»");
        return null;
    }

    /// <summary>Los valores en la central y su copia en la cooperativa.</summary>
    public static async Task PrepararAsync(CentralIdentityApiFixture fx, CarteraE2E.CooperativaAislada coop)
    {
        await ValoresDeLaCentralAsync(fx);
        await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
    }

    // --------------------------------------------------------------------------------------- ruta unitaria --

    /// <summary>
    /// Una versión de tasa fija (api.md §3.1 <c>CreditLineVersionInput</c>): cuota fija, real/365, mensual, caja, montos de 500.000 a
    /// 30.000.000 y plazos de 1 a 60 meses, mora relativa 1,5. <paramref name="extra"/> agrega o reemplaza bloques.
    /// </summary>
    public static Dictionary<string, object?> VersionFija(DateOnly desde, string modalidad, string? subsegmento, decimal tasa, string motivo = "Versión del ensayo (e2e)")
        => new()
        {
            ["validFrom"] = desde,
            ["reason"] = motivo,
            ["modalityCode"] = modalidad,
            ["subsegmentCode"] = subsegmento,
            ["rates"] = new Dictionary<string, object?>
            {
                ["current"] = new { kind = "Fixed", effectiveAnnual = tasa },
                ["default"] = new { kind = "RelativeToCurrent", factor = 1.5m },
                ["bands"] = Array.Empty<object>(),
            },
            ["amounts"] = new { min = 500_000m, max = 30_000_000m },
            ["terms"] = new { minMonths = 1, maxMonths = 60 },
            ["periodicities"] = new[] { "Monthly" },
            ["dayCount"] = "Actual365",
            ["amortization"] = new { systems = new[] { "LevelPayment" } },
            ["paymentForms"] = new[] { "Cashier" },
        };

    /// <summary>La versión variable de <c>VARIBR</c>: IBR 3M + 8 puntos, piso 10 % y techo 24 %, cuota fija 30/360, reajuste mensual.</summary>
    public static Dictionary<string, object?> VersionVariable(DateOnly desde)
    {
        var v = VersionFija(desde, "CONSUMO", "SinLibranza", 0m, "IBR más margen (e2e)");
        v["rates"] = new Dictionary<string, object?>
        {
            ["current"] = new { kind = "Variable" },
            ["variable"] = new { indexCode = "IBR", indexKey = ClaveDelIbr, margin = 0.08m, floor = 0.10m, ceiling = 0.24m, resetPeriodicity = "Monthly" },
            ["default"] = new { kind = "RelativeToCurrent", factor = 1.5m },
            ["bands"] = Array.Empty<object>(),
        };
        v["dayCount"] = "Thirty360";
        return v;
    }

    /// <summary>El alta por la ruta unitaria.</summary>
    public static Task<HttpResponseMessage> CrearAsync(HttpClient http, string token, string codigo, string nombre, Dictionary<string, object?> version) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Lineas, new { code = codigo, name = nombre, purpose = "Destino del ensayo", firstVersion = version });

    /// <summary>Crea y exige el 201; devuelve el <c>PublicId</c>.</summary>
    public static async Task<Guid> CrearExitoAsync(HttpClient http, string token, string codigo, string nombre, Dictionary<string, object?> version)
    {
        var resp = await CrearAsync(http, token, codigo, nombre, version);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"crear la línea {codigo}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    /// <summary>
    /// Las líneas que piden la seguridad y el alcance por línea (quickstart §2.8): <c>CONLIB</c>, <c>CONCAJA</c>, <c>ROTATIVO</c> (origen
    /// comercial con su concepto tributario) y <c>VARIBR</c>. Si ya existen (la misma cooperativa en otra prueba) se leen.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, Guid>> LineasDelEnsayoAsync(CentralIdentityApiFixture fx, CarteraE2E.CooperativaAislada coop)
    {
        await PrepararAsync(fx, coop);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var existentes = (await CarteraE2E.GetAsync(http, admin, $"{Lineas}?includeClosed=true")).EnumerateArray()
            .ToDictionary(l => l.GetProperty("code").GetString()!, l => l.GetProperty("publicId").GetGuid(), StringComparer.Ordinal);

        async Task<Guid> UnaAsync(string codigo, string nombre, Func<Dictionary<string, object?>> version) =>
            existentes.TryGetValue(codigo, out var id) ? id : await CrearExitoAsync(http, admin, codigo, nombre, version());

        var rotativo = new Func<Dictionary<string, object?>>(() =>
        {
            var v = VersionFija(hoy, "CONSUMO", "SinLibranza", 0.23m);
            v["allowsCommercialOrigin"] = true;
            v["interestTaxConcept"] = new { taxed = false, supportAttachmentPublicId = Guid.NewGuid() };
            return v;
        });
        return new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["CONLIB"] = await UnaAsync("CONLIB", "Consumo con libranza", () => VersionFija(hoy, "CONSUMO", "ConLibranza", 0.24m)),
            ["CONCAJA"] = await UnaAsync("CONCAJA", "Consumo por caja", () => VersionFija(hoy, "CONSUMO", "SinLibranza", 0.22m)),
            ["ROTATIVO"] = await UnaAsync("ROTATIVO", "Cupo rotativo de almacén", rotativo),
            ["VARIBR"] = await UnaAsync("VARIBR", "Consumo IBR más margen", () => VersionVariable(hoy)),
        };
    }

    // ------------------------------------------------------------------------------------------- plantilla --

    /// <summary>Una fila de la hoja: columna → valor (texto o número).</summary>
    public sealed class Fila : Dictionary<string, object?>
    {
        public Fila() : base(StringComparer.Ordinal) { }
    }

    /// <summary>El libro con las hojas dadas (encabezados en la fila 1, en el orden de la primera aparición).</summary>
    public static byte[] Libro(IReadOnlyDictionary<string, IReadOnlyList<Fila>> hojas)
    {
        using var libro = new XLWorkbook();
        foreach (var (nombre, filas) in hojas)
        {
            var hoja = libro.AddWorksheet(nombre);
            var columnas = filas.SelectMany(f => f.Keys).Distinct(StringComparer.Ordinal).ToList();
            for (var c = 0; c < columnas.Count; c++) hoja.Cell(1, c + 1).Value = columnas[c];
            for (var r = 0; r < filas.Count; r++)
                for (var c = 0; c < columnas.Count; c++)
                    if (filas[r].TryGetValue(columnas[c], out var valor) && valor is not null)
                        hoja.Cell(r + 2, c + 1).Value = valor switch
                        {
                            decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            int n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            DateOnly f => f.ToString("yyyy-MM-dd"),
                            _ => valor.ToString(),
                        };
        }
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary><c>POST /credit-lines/import?mode=</c> con su clave y el motivo.</summary>
    public static Task<HttpResponseMessage> ImportarAsync(HttpClient http, string token, byte[] contenido, string modo, string motivo = "Carga inicial de líneas (e2e)")
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        formulario.Add(archivo, "file", "lineas.xlsx");
        formulario.Add(new StringContent(motivo), "reason");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"{Lineas}/import?mode={modo}") { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }
}
