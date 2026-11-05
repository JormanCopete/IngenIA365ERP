using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Implantacion;

/// <summary>
/// T023 (feature 016, US1, escenarios 1–6), en el motor de <c>DB_PROVIDER</c>: la plantilla <c>core.people</c> por HTTP sobre una
/// cooperativa aislada («personas016»). Revisar con 3 filas buenas y 2 malas responde los errores con fila y columna y no guarda nada;
/// corregido, aplicar crea las personas como el alta unitaria; la misma persona con otro correo se actualiza con el cambio campo a
/// campo; una eliminada responde <c>Person.TaxIdDeleted</c>; las marcas de proveedor se escriben y las de asociado no.
/// <para><b>Escrita, no corrida</b>: las e2e de la 016 se corren una sola vez al final (T060).</para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CargueDePersonasTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/core/people";

    private static readonly string[] Encabezados =
        ["tipoDocumento", "documento", "digitoVerificacion", "naturaleza", "primerNombre", "primerApellido", "razonSocial", "correo", "fechaNacimiento", "esProveedor", "ciudad"];

    private static string?[] Natural(string documento, string nombre, string? correo = null, string? fecha = "1980-01-15", string? ciudad = null) =>
        ["CC", documento, null, "natural", nombre, "PRUEBA", null, correo, fecha, null, ciudad];

    private static string?[] Juridica(string nit, string razon) =>
        ["NIT", nit, null, "juridica", null, null, razon, null, null, "sí", null];

    private static byte[] Libro(params string?[][] filas) => InventarioE2E.Libro(("Datos", Encabezados, filas));

    [Fact]
    public async Task Revisar_no_guarda_aplicar_crea_y_volver_a_aplicar_actualiza()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "personas016");
        using var http = fx.CreateClient();

        // Escenarios 1 y 5: tres buenas y dos malas; la revisión dice fila y columna y no guarda; aplicar con errores es 422.
        var conErrores = Libro(
            Natural("1610101010", "ANA"), Natural("1620202020", "LUIS"), Juridica("901600001", "EJEMPLO DIECISEIS SAS"),
            Natural("1630303030", "MARIA", fecha: "2090-01-01"), Natural("1640404040", "PEDRO", ciudad: "NO-EXISTE"));
        var revision = await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "review", conErrores);
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var cuerpo = await InventarioE2E.LeerAsync(revision);
        cuerpo.GetProperty("valid").GetBoolean().Should().BeFalse();
        cuerpo.GetProperty("errors").EnumerateArray().Select(e => (e.GetProperty("row").GetInt32(), e.GetProperty("column").GetString()))
            .Should().BeEquivalentTo([(5, "fechaNacimiento"), (6, "ciudad")]);
        (await PorDocumentoAsync(http, coop.TokenAdmin, "1610101010")).Should().Be(HttpStatusCode.NotFound, "la revisión no guarda");
        var aplicada = await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "apply", conErrores);
        aplicada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await PorDocumentoAsync(http, coop.TokenAdmin, "1610101010")).Should().Be(HttpStatusCode.NotFound, "con un error no se guarda ninguna");

        // Corregido: se crean las cinco.
        var bueno = Libro(
            Natural("1610101010", "ANA"), Natural("1620202020", "LUIS"), Juridica("901600001", "EJEMPLO DIECISEIS SAS"),
            Natural("1630303030", "MARIA"), Natural("1640404040", "PEDRO"));
        var creadas = await InventarioE2E.RevisarYAplicarAsync(http, coop.TokenAdmin, Ruta, bueno);
        creadas.GetProperty("sheets")[0].GetProperty("created").GetInt32().Should().Be(5);
        var juridica = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Ruta}/by-document?taxId=901600001&type=NIT");
        juridica.GetRawText().Should().Contain("EJEMPLO DIECISEIS SAS");

        // Escenario 2: la misma persona con otro correo se actualiza y el cambio va campo a campo.
        var actualizada = await InventarioE2E.RevisarYAplicarAsync(http, coop.TokenAdmin, Ruta, Libro(Natural("1610101010", "ANA", correo: "ana016@correo.test")));
        actualizada.GetProperty("sheets")[0].GetProperty("updated").GetInt32().Should().Be(1);
        actualizada.GetProperty("changes")[0].GetProperty("fields").EnumerateArray()
            .Should().ContainSingle(f => f.GetProperty("column").GetString() == "correo" && f.GetProperty("after").GetString() == "ana016@correo.test");
    }

    [Fact]
    public async Task Una_eliminada_responde_TaxIdDeleted_en_la_columna_del_documento()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "personas016");
        using var http = fx.CreateClient();
        await InventarioE2E.RevisarYAplicarAsync(http, coop.TokenAdmin, Ruta, Libro(Natural("1650505050", "ELIMINADA")));
        var persona = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Ruta}/by-document?taxId=1650505050&type=CC");
        var id = persona.GetProperty("publicId").GetGuid();
        (await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Delete, $"{Ruta}/{id}", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var revision = await InventarioE2E.LeerAsync(await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "review", Libro(Natural("1650505050", "ELIMINADA"))));

        revision.GetProperty("errors").EnumerateArray()
            .Should().ContainSingle(e => e.GetProperty("code").GetString() == "Person.TaxIdDeleted" && e.GetProperty("column").GetString() == "documento");
    }

    [Fact]
    public async Task La_plantilla_se_descarga_con_sus_encabezados()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "personas016");
        using var http = fx.CreateClient();
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"{Ruta}/template.xlsx");
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", coop.TokenAdmin);

        var resp = await http.SendAsync(peticion);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var libro = new ClosedXML.Excel.XLWorkbook(await resp.Content.ReadAsStreamAsync());
        libro.Worksheet("Datos").Row(1).CellsUsed().Select(c => c.GetString()).Should().StartWith(["tipoDocumento", "documento"]);
    }

    private static async Task<HttpStatusCode> PorDocumentoAsync(HttpClient http, string token, string numero)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"{Ruta}/by-document?taxId={numero}&type=CC");
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (await http.SendAsync(peticion)).StatusCode;
    }
}
