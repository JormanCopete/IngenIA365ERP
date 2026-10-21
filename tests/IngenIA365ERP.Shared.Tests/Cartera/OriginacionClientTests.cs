using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0383 (feature 013, US2; contracts/api.md §1.5, §4.1–§4.5): <see cref="OriginacionClient"/>. Toda escritura (enviar la simulación, el
/// borrador, sus documentos, radicar, analizar, volver a validar, anular, decidir, consultar a centrales, liberar) lleva su
/// <c>Idempotency-Key</c> —la misma en el reintento, otra tras el éxito—; la simulación, su PDF, el enrutamiento y el enlace al informe van sin
/// clave aunque algunos vayan por POST; ninguna petición pone <c>Authorization</c>. (nuevo)
/// </summary>
public class OriginacionClientTests
{
    private static readonly Guid Linea = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Solicitud = Guid.Parse("22222222-3333-4444-5555-666666666666");
    private static readonly Guid Cupo = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private static readonly Guid Persona = Guid.Parse("44444444-5555-6666-7777-888888888888");
    private static readonly Guid Otro = Guid.Parse("55555555-6666-7777-8888-999999999999");

    private readonly ServidorDeCartera _servidor = new();
    private readonly OriginacionClient _cliente;

    public OriginacionClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new OriginacionClient(http, auth);
    }

    private static SimulacionRequest Simulacion() => new(Linea, 12_000_000m, 36, Periodicity: 30, PersonPublicId: Persona);

    private static SolicitudRequest Borrador() => new()
    {
        ApplicantPersonPublicId = Persona, OfficePublicId = Otro, CreditLinePublicId = Linea, Amount = 12_000_000m, Installments = 36,
        PrepaymentPreference = 1,
    };

    private static DecisionRequest Aprobar() => new(1, null, null, "abc");

    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("enviar", "POST", "/api/lending/simulations/schedule/send", (c, k) => c.EnviarSimulacionAsync(new EnviarSimulacionRequest(Simulacion(), Persona), k));
        yield return E("crear", "POST", "/api/lending/applications", (c, k) => c.CrearAsync(Borrador(), k));
        yield return E("actualizar", "PUT", $"/api/lending/applications/{Solicitud}", (c, k) => c.ActualizarAsync(Solicitud, Borrador(), [1, 2], k));
        yield return E("adjuntar", "POST", $"/api/lending/applications/{Solicitud}/documents", (c, k) =>
            c.AdjuntarDocumentoAsync(Solicitud, new DocumentoDeSolicitudRequest("CED", Otro), k));
        yield return E("quitar", "DELETE", $"/api/lending/applications/{Solicitud}/documents/{Otro}", (c, k) => c.QuitarDocumentoAsync(Solicitud, Otro, k));
        yield return E("radicar", "POST", $"/api/lending/applications/{Solicitud}/submit", (c, k) => c.RadicarAsync(Solicitud, [1], k));
        yield return E("analizar", "POST", $"/api/lending/applications/{Solicitud}/analysis", (c, k) =>
            c.AnalizarAsync(Solicitud, new AnalisisDeSolicitudRequest(3_000_000m, 1_000_000m, 0m), k));
        yield return E("revalidar", "POST", $"/api/lending/applications/{Solicitud}/checks/rerun", (c, k) =>
            c.RepetirValidacionesAsync(Solicitud, new RepetirValidacionesRequest(["Screening"]), k));
        yield return E("cancelar", "POST", $"/api/lending/applications/{Solicitud}/cancel", (c, k) =>
            c.CancelarAsync(Solicitud, new CancelarSolicitudRequest("Withdrawal", "El asociado desiste"), k));
        yield return E("decidir solicitud", "POST", $"/api/lending/applications/{Solicitud}/decisions", (c, k) => c.DecidirSolicitudAsync(Solicitud, Aprobar(), k));
        yield return E("decidir cupo", "POST", $"/api/lending/credit-limits/{Cupo}/decisions", (c, k) => c.DecidirCupoAsync(Cupo, Aprobar(), k));
        yield return E("central", "POST", "/api/lending/bureau-queries", (c, k) =>
            c.ConsultarCentralAsync(new ConsultaACentralRequest(Persona, "SIMULADO", "CreditApplication", Solicitud), k));
        yield return E("liberar", "POST", $"/api/lending/screening-results/{Otro}/release", (c, k) => c.LiberarCoincidenciaAsync(Otro, "Homónimo", k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<OriginacionClient, ClaveDeOperacion, Task> accion) =>
        [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta,
        Func<OriginacionClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_cliente, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo);
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task El_reintento_repite_la_clave_y_tras_el_exito_la_siguiente_es_otra()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var clave = new ClaveDeOperacion();

        await _cliente.RadicarAsync(Solicitud, [1], clave);
        await _cliente.RadicarAsync(Solicitud, [1], clave);
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { });
        await _cliente.RadicarAsync(Solicitud, [1], clave);
        await _cliente.RadicarAsync(Solicitud, [1], clave);

        var claves = _servidor.Vistas.Select(v => v.Clave).ToList();
        claves[1].Should().Be(claves[0], "el reintento con el mismo contenido conserva la clave");
        claves[2].Should().Be(claves[1]);
        claves[3].Should().NotBe(claves[2], "tras el éxito la operación siguiente es otra");
    }

    [Fact]
    public async Task El_borrador_viaja_con_su_rowVersion_al_lado()
    {
        string? cuerpo = null;
        _servidor.Responder = r =>
        {
            cuerpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return ServidorDeCartera.Json(HttpStatusCode.OK, new { });
        };

        await _cliente.ActualizarAsync(Solicitud, Borrador(), [1, 2, 3], new ClaveDeOperacion());

        using var doc = JsonDocument.Parse(cuerpo!);
        doc.RootElement.GetProperty("rowVersion").GetString().Should().Be(Convert.ToBase64String([1, 2, 3]));
        doc.RootElement.GetProperty("creditLinePublicId").GetGuid().Should().Be(Linea);
        doc.RootElement.GetProperty("amount").GetDecimal().Should().Be(12_000_000m);
    }

    [Fact]
    public async Task Simular_el_pdf_el_enrutamiento_las_listas_y_el_enlace_van_sin_clave()
    {
        _servidor.Responder = r => r.RequestUri!.AbsolutePath.EndsWith("/pdf", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([37, 80, 68, 70]) }
            : ServidorDeCartera.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0 });

        await _cliente.SimularAsync(Simulacion());
        var pdf = await _cliente.SimulacionEnPdfAsync(Simulacion());
        await _cliente.EnrutamientoAsync(Solicitud);
        await _cliente.SolicitudesAsync(new FiltrosDeSolicitudes(Status: 3, Mine: true));
        await _cliente.PendientesAsync("CreditLimit");
        await _cliente.ConsultasACentralesAsync(Persona);
        await _cliente.EnlaceDelInformeAsync(Otro);
        await _cliente.ResultadosDeListasAsync("Blocked");

        pdf.IsSuccess.Should().BeTrue();
        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("POST", "/api/lending/simulations/schedule"),
            ("POST", "/api/lending/simulations/schedule/pdf"),
            ("GET", $"/api/lending/applications/{Solicitud}/routing"),
            ("GET", "/api/lending/applications?status=3&mine=true&page=1&pageSize=20"),
            ("GET", "/api/lending/pending-decisions?subject=CreditLimit&page=1"),
            ("GET", $"/api/lending/bureau-queries?personPublicId={Persona}&page=1"),
            ("POST", $"/api/lending/bureau-queries/{Otro}/download-link"),
            ("GET", "/api/lending/screening-results?status=Blocked&page=1"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null, "ninguna escribe: son consultas, aunque vayan por POST (api.md §1.5)");
        _servidor.Vistas.Should().OnlyContain(v => v.Authorization == null);
    }
}
