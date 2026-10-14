using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Imports;
using Microsoft.Extensions.DependencyInjection;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Ayudantes de las e2e de la migración y la activación de Cartera (feature 013, US8, T0729–T0731), todo por HTTP: los archivos de cada parte en
/// <c>.csv</c> (encabezados de <see cref="DefinicionesDeMigracion"/> en la fila 1, separador «;», montos sin separador de miles), la subida de una tanda
/// por <c>/parts/{part}/import?mode=&amp;replace=</c> con su clave, la carga, la aplicación, la línea, la oficina vinculada a su agencia contable y
/// las personas del ensayo, y una consulta a la base de la cooperativa. (nuevo)
/// </summary>
public static class MigracionE2E
{
    public const string Cargas = "/api/lending/migration/loads";
    public const string Activacion = "/api/lending/activation";
    public const string Linea = "MIGCON";

    private static int _documento = 810_000_000;

    /// <summary>Un documento nuevo para una persona del ensayo.</summary>
    public static string Documento() => Interlocked.Increment(ref _documento).ToString(CultureInfo.InvariantCulture);

    public static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Monto(decimal valor) => valor.ToString("0", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------------------------------------------- archivos --

    /// <summary>Una fila: columna → valor (las que no vienen quedan vacías).</summary>
    public static Dictionary<string, string?> Fila(params (string Columna, string? Valor)[] celdas) => celdas.ToDictionary(c => c.Columna, c => c.Valor);

    /// <summary>El <c>.csv</c> de la parte con todos sus encabezados en la fila 1 y una fila por diccionario.</summary>
    public static byte[] Csv(string parte, IEnumerable<IReadOnlyDictionary<string, string?>> filas)
    {
        var columnas = DefinicionesDeMigracion.Buscar(parte)!.Definicion.Hojas[0].Columnas.Select(c => c.Nombre).ToList();
        var texto = new StringBuilder();
        texto.AppendLine(string.Join(';', columnas));
        foreach (var fila in filas)
            texto.AppendLine(string.Join(';', columnas.Select(c => fila.TryGetValue(c, out var v) ? v ?? string.Empty : string.Empty)));
        return Encoding.UTF8.GetBytes(texto.ToString());
    }

    /// <summary>Un crédito de la parte <c>loans</c>: tasa fija del 22 % EA, mora relativa 1,5, mensual, por caja.</summary>
    public static Dictionary<string, string?> Credito(string numero, string deudor, string oficina, DateOnly desembolso, decimal monto, int plazo,
        string? factura = null) => Fila(
        (C.Numero, numero), (C.Deudor, deudor), (C.Oficina, oficina), (C.Linea, Linea), (C.Factura, factura), (C.FechaDeDesembolso, Fecha(desembolso)),
        (C.MontoDesembolsado, Monto(monto)), (C.TipoDeTasa, "Fixed"), (C.TasaCorrienteEA, "22"), (C.TipoDeTasaDeMora, "RelativeToCurrent"),
        (C.FactorDeMora, "1,5"), (C.Periodicidad, "Monthly"), (C.Plazo, plazo.ToString(CultureInfo.InvariantCulture)), (C.FormaDePago, "Cashier"),
        (C.EnJuridico, "no"));

    /// <summary>Las cuotas pendientes de un crédito: <paramref name="cuotas"/> de <paramref name="capital"/>, la primera a los 15 días del corte.</summary>
    public static IEnumerable<Dictionary<string, string?>> Cuotas(string numero, DateOnly corte, int cuotas, decimal capital) =>
        Enumerable.Range(1, cuotas).Select(i => Fila((C.NumeroDeCredito, numero), (C.Cuota, i.ToString(CultureInfo.InvariantCulture)),
            (C.FechaDeVencimiento, Fecha(corte.AddDays(15).AddMonths(i - 1))), (C.Capital, Monto(capital))));

    public static Dictionary<string, string?> Saldo(string numero, string concepto, decimal valor) =>
        Fila((C.NumeroDeCredito, numero), (C.Concepto, concepto), (C.Valor, Monto(valor)));

    public static Dictionary<string, string?> Riesgo(string numero) =>
        Fila((C.NumeroDeCredito, numero), (C.DiasDeMora, "0"), (C.Calificacion, "A"), (C.DeterioroCapital, "0"), (C.EnSuspenso, "no"));

    // ----------------------------------------------------------------------------------------------------------- http --

    /// <summary>Crea la carga en borrador con su corte y devuelve su <c>PublicId</c>.</summary>
    public static async Task<Guid> CrearCargaAsync(HttpClient http, string token, DateOnly corte, string notas)
    {
        var alta = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Cargas, new { cutoffDate = corte, notes = notas });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"crear la carga: «{await alta.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();
    }

    /// <summary><c>POST …/parts/{part}/import?mode=&amp;replace=</c>: una tanda (un archivo) con su clave y el motivo.</summary>
    public static Task<HttpResponseMessage> ImportarAsync(HttpClient http, string token, Guid carga, string parte, byte[] contenido, string modo = "apply",
        bool reemplazar = false, string? archivo = null)
    {
        var formulario = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(contenido);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        formulario.Add(bytes, "file", archivo ?? $"{parte}.csv");
        formulario.Add(new StringContent("Carga de la cartera vigente (e2e)"), "reason");
        var url = $"{Cargas}/{carga}/parts/{parte}/import?mode={modo}{(reemplazar ? "&replace=true" : string.Empty)}";
        var peticion = new HttpRequestMessage(HttpMethod.Post, url) { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }

    /// <summary>Importa y exige el 200 aplicado; devuelve la revisión.</summary>
    public static async Task<JsonElement> CargarAsync(HttpClient http, string token, Guid carga, string parte, IEnumerable<IReadOnlyDictionary<string, string?>> filas,
        bool reemplazar = false, string? archivo = null)
    {
        var resp = await ImportarAsync(http, token, carga, parte, Csv(parte, filas), "apply", reemplazar, archivo);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"importar {parte}: «{await resp.Content.ReadAsStringAsync()}»");
        var r = await CarteraE2E.LeerAsync(resp);
        r.GetProperty("applied").GetBoolean().Should().BeTrue($"importar {parte}: {r}");
        return r;
    }

    public static Task<HttpResponseMessage> AplicarAsync(HttpClient http, string token, Guid carga, string motivo = "Aplicar la carga (e2e)") =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Cargas}/{carga}/apply", new { reason = motivo });

    /// <summary>Una persona del maestro con su documento; devuelve el documento y la persona.</summary>
    public static async Task<(string Documento, Guid Persona)> PersonaAsync(HttpClient http, string token, string nombre, string apellido = "Migrada")
    {
        var documento = Documento();
        var alta = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = apellido, email = $"{nombre.ToLowerInvariant()}.{documento}@coop.migracion.test",
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"persona {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        return (documento, (await CarteraE2E.LeerAsync(alta)).GetGuid());
    }

    /// <summary>La oficina (<c>/api/admin/branches</c>) y su agencia contable vinculada; devuelve el nombre de la agencia (lo que lleva la plantilla).</summary>
    public static async Task<string> OficinaAsync(HttpClient http, string token, Guid tenantPublicId, string codigo)
    {
        var alta = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/admin/branches", new
        {
            tenantPublicId, code = codigo, name = $"Oficina {codigo}", isHeadquarters = false,
        });
        alta.IsSuccessStatusCode.Should().BeTrue($"oficina {codigo}: «{await alta.Content.ReadAsStringAsync()}»");
        var cuerpo = await CarteraE2E.LeerAsync(alta);
        var oficina = cuerpo.ValueKind == JsonValueKind.String ? cuerpo.GetGuid() : cuerpo.GetProperty("publicId").GetGuid();
        var nombre = $"Agencia {codigo}";
        var agencia = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/branches", new { name = nombre, shortName = codigo, tenantBranchPublicId = oficina });
        agencia.StatusCode.Should().Be(HttpStatusCode.Created, $"agencia {codigo}: «{await agencia.Content.ReadAsStringAsync()}»");
        return nombre;
    }

    /// <summary>
    /// La línea <c>MIGCON</c> de consumo (22 % EA bajo la usura del ensayo) con versión desde <paramref name="desde"/>: admite origen comercial y es la
    /// de origen comercial por defecto para asociados (la obligación en cero de una venta cancelada antes del corte nace en ella).
    /// </summary>
    public static async Task LineaAsync(CentralIdentityApiFixture fx, CarteraE2E.CooperativaAislada coop, DateOnly desde)
    {
        await LineasE2E.PrepararAsync(fx, coop);
        using var http = fx.CreateClient();
        var version = LineasE2E.VersionFija(desde, "CONSUMO", "SinLibranza", 0.22m, "Línea de la cartera migrada (e2e)");
        version["allowsCommercialOrigin"] = true;
        version["commercialDefaultFor"] = "Associate";
        version["interestTaxConcept"] = new { taxed = false, supportAttachmentPublicId = Guid.NewGuid() };
        version["amounts"] = new { min = 100_000m, max = 30_000_000m };
        version["periodicities"] = new[] { "Monthly", "SinglePayment" };
        await LineasE2E.CrearExitoAsync(http, coop.TokenAdmin, Linea, "Consumo migrado", version);
    }

    /// <summary>Una consulta tipada sobre la base de la cooperativa.</summary>
    public static async Task<T> EnLaBaseAsync<T>(CentralIdentityApiFixture fx, Guid tenantPublicId, Func<IApplicationDbContext, Task<T>> consulta)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None)).Single(t => t.PublicId == tenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        return await consulta(ambito.Db);
    }

    /// <summary>Un error de <c>data.errors[]</c> (importación o entre partes).</summary>
    public static IReadOnlyList<JsonElement> Errores(JsonElement cuerpo) =>
        cuerpo.TryGetProperty("data", out var datos) && datos.ValueKind == JsonValueKind.Object && datos.TryGetProperty("errors", out var e)
            ? [.. e.EnumerateArray()]
            : cuerpo.TryGetProperty("errors", out var directos) && directos.ValueKind == JsonValueKind.Array ? [.. directos.EnumerateArray()] : [];
}
