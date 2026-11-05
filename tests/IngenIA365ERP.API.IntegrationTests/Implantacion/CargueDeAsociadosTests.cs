using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Implantacion;

/// <summary>
/// T030 (feature 016, US3), en el motor de <c>DB_PROVIDER</c>: la plantilla <c>core.associates</c> por HTTP sobre una cooperativa aislada
/// («asociados016»). Personas creadas por su plantilla; un documento inexistente impide aplicar; corregido, quedan con su marca y su fila
/// como las deja el alta unitaria; otro grupo actualiza sin duplicar; un retirado nace retirado y un vigente no se retira por archivo.
/// <para><b>Escrita, no corrida</b>: las e2e de la 016 se corren una sola vez al final (T060).</para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CargueDeAsociadosTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/core/associates";

    private static readonly string[] Encabezados =
        ["tipoDocumento", "documento", "fechaAfiliacion", "oficina", "estado", "grupo", "clase", "fechaRetiro", "motivoRetiro"];

    private static string?[] Fila(string documento, string oficina, string estado = "vigente", string? grupo = null, string fecha = "2015-03-01",
        string? retiro = null, string? motivo = null) =>
        ["CC", documento, fecha, oficina, estado, grupo, "afiliado", retiro, motivo];

    private static byte[] Libro(params string?[][] filas) => InventarioE2E.Libro(("Datos", Encabezados, filas));

    /// <summary>Tres personas por la plantilla, una oficina, dos grupos y un motivo de retiro.</summary>
    private static async Task<(string Oficina, string GrupoUno, string GrupoDos, string Motivo)> PrepararAsync(HttpClient http, string token, string prefijo)
    {
        string?[] Persona(string numero) => ["CC", numero, "natural", "PERSONA", "PRUEBA"];
        await InventarioE2E.RevisarYAplicarAsync(http, token, "/api/core/people", InventarioE2E.Libro(("Datos",
            ["tipoDocumento", "documento", "naturaleza", "primerNombre", "primerApellido"],
            [Persona($"{prefijo}1"), Persona($"{prefijo}2"), Persona($"{prefijo}3")])));

        var oficina = $"OF{prefijo}";
        var grupoUno = $"GA{prefijo}";
        var grupoDos = $"GB{prefijo}";
        var motivo = $"MR{prefijo}";
        (await InventarioE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/branches", new { code = oficina, name = oficina })).IsSuccessStatusCode.Should().BeTrue();
        await CrearGrupoAsync(http, token, grupoUno);
        await CrearGrupoAsync(http, token, grupoDos);
        (await InventarioE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/withdrawal-reasons", new { code = motivo, name = motivo })).IsSuccessStatusCode.Should().BeTrue();
        return (oficina, grupoUno, grupoDos, motivo);
    }

    private static async Task CrearGrupoAsync(HttpClient http, string token, string codigo)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/core/associate-groups")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { code = codigo, name = codigo }),
        };
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        (await http.SendAsync(InventarioE2E.ConClave(peticion))).IsSuccessStatusCode.Should().BeTrue();
    }

    private static async Task<JsonElement> AsociadoAsync(HttpClient http, string token, string documento)
    {
        var persona = await InventarioE2E.GetAsync(http, token, $"/api/core/people/by-document?taxId={documento}&type=CC");
        return await InventarioE2E.GetAsync(http, token, $"{Ruta}/by-person/{persona.GetProperty("publicId").GetGuid()}");
    }

    [Fact]
    public async Task Un_documento_inexistente_impide_aplicar_y_corregido_quedan_con_su_marca()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "asociados016");
        using var http = fx.CreateClient();
        var c = await PrepararAsync(http, coop.TokenAdmin, "16101");

        var conError = Libro(Fila("161011", c.Oficina, grupo: c.GrupoUno), Fila("161012", c.Oficina), Fila("161099", c.Oficina));
        var revision = await InventarioE2E.LeerAsync(await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "review", conError));
        revision.GetProperty("errors").EnumerateArray().Should().ContainSingle(e =>
            e.GetProperty("code").GetString() == "Import.Cell.NotFound" && e.GetProperty("column").GetString() == "documento"
            && e.GetProperty("message").GetString()!.Contains("esta plantilla no crea personas"));
        (await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "apply", conError)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var creados = await InventarioE2E.RevisarYAplicarAsync(http, coop.TokenAdmin, Ruta,
            Libro(Fila("161011", c.Oficina, grupo: c.GrupoUno), Fila("161012", c.Oficina), Fila("161013", c.Oficina)));
        creados.GetProperty("sheets")[0].GetProperty("created").GetInt32().Should().Be(3);
        var persona = await InventarioE2E.GetAsync(http, coop.TokenAdmin, "/api/core/people/by-document?taxId=161011&type=CC");
        persona.GetRawText().Should().Contain("\"isAssociate\":true");

        // Escenario 2: la misma persona con otro grupo se actualiza y no se duplica.
        var actualizado = await InventarioE2E.RevisarYAplicarAsync(http, coop.TokenAdmin, Ruta, Libro(Fila("161011", c.Oficina, grupo: c.GrupoDos)));
        actualizado.GetProperty("sheets")[0].GetProperty("updated").GetInt32().Should().Be(1);
        (await AsociadoAsync(http, coop.TokenAdmin, "161011")).GetRawText().Should().Contain("\"status\":\"A\"");
    }

    [Fact]
    public async Task Un_retirado_nace_retirado_y_un_vigente_no_se_retira_por_archivo()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "asociados016");
        using var http = fx.CreateClient();
        var c = await PrepararAsync(http, coop.TokenAdmin, "16102");

        await InventarioE2E.RevisarYAplicarAsync(http, coop.TokenAdmin, Ruta, Libro(
            Fila("161021", c.Oficina, estado: "retirado", retiro: "2024-06-30", motivo: c.Motivo),
            Fila("161022", c.Oficina)));
        (await AsociadoAsync(http, coop.TokenAdmin, "161021")).GetRawText().Should().Contain("\"status\":\"R\"");

        var revision = await InventarioE2E.LeerAsync(await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "review",
            Libro(Fila("161022", c.Oficina, estado: "retirado", retiro: "2024-06-30", motivo: c.Motivo))));
        revision.GetProperty("errors").EnumerateArray().Should().ContainSingle(e =>
            e.GetProperty("code").GetString() == "Associate.WithdrawalOwnedByLending" && e.GetProperty("column").GetString() == "estado");
    }

    [Fact]
    public async Task Una_afiliacion_futura_da_error_en_su_columna()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "asociados016");
        using var http = fx.CreateClient();
        var c = await PrepararAsync(http, coop.TokenAdmin, "16103");

        var revision = await InventarioE2E.LeerAsync(await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "review",
            Libro(Fila("161031", c.Oficina, fecha: "2090-01-01"))));

        revision.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("column").GetString()).Should().Equal("fechaAfiliacion");
    }

    [Fact]
    public async Task La_plantilla_se_descarga_con_sus_encabezados()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "asociados016");
        using var http = fx.CreateClient();
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"{Ruta}/template.xlsx");
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", coop.TokenAdmin);

        var resp = await http.SendAsync(peticion);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var libro = new ClosedXML.Excel.XLWorkbook(await resp.Content.ReadAsStreamAsync());
        libro.Worksheet("Datos").Row(1).CellsUsed().Select(c => c.GetString()).Should().StartWith(["tipoDocumento", "documento", "fechaAfiliacion"]);
    }
}
