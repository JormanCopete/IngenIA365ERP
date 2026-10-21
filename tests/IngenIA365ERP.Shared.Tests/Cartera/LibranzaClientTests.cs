using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0851 (feature 013, K2, US10; contracts/api.md §1.5, §13, §20): <see cref="LibranzaClient"/>. Crear un convenio, su versión, sus contactos,
/// generar y anular un archivo, resolver un no aplicado, trasladar una libranza y lo que hace el recaudador de prueba llevan su
/// <c>Idempotency-Key</c>; la vista previa del archivo, su enlace firmado y las listas no; ninguna pone <c>Authorization</c>. (nuevo)
/// </summary>
public class LibranzaClientTests
{
    private static readonly Guid Convenio = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Archivo = Guid.Parse("22222222-3333-4444-5555-666666666666");
    private static readonly Guid Credito = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private static readonly Guid Persona = Guid.Parse("44444444-5555-6666-7777-888888888888");

    private readonly ServidorDeCartera _servidor = new();
    private readonly LibranzaClient _libranza;

    public LibranzaClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _libranza = new LibranzaClient(http, auth);
    }

    private static VersionDeConvenioRequest Version => new()
    {
        ValidFrom = new DateOnly(2026, 11, 1), OutboundFormatPublicId = Archivo, ResultFormatPublicId = Credito, Reason = "Convenio firmado", SendDay = 25,
        DeductionCap = 0.4m, ResultCodes = [new CodigoDeRespuestaRequest("00", "Descontado", "Deducted")],
    };

    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("crear convenio", "POST", "/api/lending/payroll-agreements", (c, k) =>
            c.CrearAsync(new ConvenioRequest("PAGMUN01", "Secretaría", Persona, false, Version), k));
        yield return E("versión", "POST", $"/api/lending/payroll-agreements/{Convenio}/versions", (c, k) => c.VersionAsync(Convenio, Version, k));
        yield return E("contactos", "PUT", $"/api/lending/payroll-agreements/{Convenio}/contacts", (c, k) =>
            c.ContactosAsync(Convenio, [new ContactoDeConvenioRequest(Persona, "Nómina", true)], k));
        yield return E("generar", "POST", "/api/lending/payroll-deduction-files", (c, k) => c.GenerarAsync(new ArchivoDeDescuentosRequest(Convenio, 2027, 1), k));
        yield return E("anular", "POST", $"/api/lending/payroll-deduction-files/{Archivo}/void", (c, k) => c.AnularAsync(Archivo, "Período errado", k));
        yield return E("resolver", "POST", $"/api/lending/payroll-not-applied/{Archivo}/resolve", (c, k) =>
            c.ResolverAsync(Archivo, new ResolucionDeNoAplicadoRequest("MovedToCashPayment", "Pagará por caja"), k));
        yield return E("trasladar", "POST", $"/api/lending/loans/{Credito}/payroll-transfer", (c, k) =>
            c.TrasladarAsync(Credito, new TrasladoDeLibranzaRequest(null, null, null, null, null, "Retiro sin nuevo empleador"), k));
        yield return E("confirmar envío", "POST", $"/api/lending/test-doubles/collector/payroll-deduction-files/{Archivo}/confirm-sent", (c, k) =>
            c.ConfirmarEnvioDePruebaAsync(Archivo, new ConfirmacionDeEnvioDePruebaRequest("Portal", "RAD-1"), k));
        yield return E("respuesta", "POST", "/api/lending/test-doubles/collector/payroll-deduction-results", (c, k) =>
            c.ResultadoDePruebaAsync(Archivo, "respuesta.csv", "1;00;100.00;20270130\n"u8.ToArray(), k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<LibranzaClient, ClaveDeOperacion, Task> accion) => [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta, Func<LibranzaClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_libranza, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo);
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task La_vista_previa_el_enlace_y_las_listas_van_sin_clave()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 50, totalCount = 0 });

        await _libranza.VistaPreviaAsync(new ArchivoDeDescuentosRequest(Convenio, 2027, 1));
        await _libranza.EnlaceAsync(Archivo);
        await _libranza.ArchivosAsync(Convenio, 2027, 1, "Sent");
        await _libranza.NoAplicadosAsync(Convenio, "PaymasterResult", "Open");
        await _libranza.PendientesDeGiroAsync(Convenio, new DateOnly(2027, 2, 1));
        await _libranza.FormatosAsync(LibranzaClient.AmbitoDeVuelta);

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("POST", "/api/lending/payroll-deduction-files/preview"),
            ("POST", $"/api/lending/payroll-deduction-files/{Archivo}/download-link"),
            ("GET", $"/api/lending/payroll-deduction-files?agreementPublicId={Convenio}&year=2027&month=1&status=Sent&page=1"),
            ("GET", $"/api/lending/payroll-not-applied?agreementPublicId={Convenio}&origin=PaymasterResult&status=Open&page=1"),
            ("GET", $"/api/lending/payroll-agreements/{Convenio}/pending-transfers?asOf=2027-02-01"),
            ("GET", "/api/core/bank-file-formats?scope=PayrollDeductionResult&onlyActive=true"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }
}
