using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Auditoria;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Tests.Seguridad;
using IngenIA365ERP.Shared.Tests.Sesion;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// T413 (US12; §15.2, §16, §7, §31, §29): la parte de seguridad de <see cref="InventarioClient"/>. Toda escritura (decidir,
/// retirar, fijar alcance, montos, políticas, vigencias, alertas, vendedores) lleva la <c>Idempotency-Key</c> de la acción
/// de la persona —la misma en el reintento, otra tras el éxito—; ninguna pone <c>Authorization</c> a mano
/// (<c>ElTokenDeSesionLoPoneElHandler</c>); y un 422 <c>Approvals.Presence.Invalid</c> es un error de negocio que vuelve
/// con su código y no dispara ninguna renovación de sesión.
/// </summary>
public class InventarioClientSeguridadTests
{
    private static readonly DateTime Ahora = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly Servidor _servidor = new();
    private readonly HttpClient _http;
    private readonly CentralAuthClient _auth;
    private readonly InventarioClient _cliente;

    public InventarioClientSeguridadTests()
    {
        var almacen = new AlmacenEnMemoria();
        var sesion = new RenovadorDeSesion(almacen, new FabricaDeUnSoloServidor(_servidor), new RelojFijo(Ahora));
        // Sin AuthBearerHandler en la cadena, a propósito: si el cliente pusiera Authorization, se vería aquí.
        _http = new HttpClient(_servidor, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        _auth = new CentralAuthClient(_http, sesion, new EstadoDeAutenticacionFalso());
        _auth.AdoptSessionAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12)).GetAwaiter().GetResult();
        _servidor.Vistas.Clear();
        _cliente = new InventarioClient(_http, _auth);
    }

    /// <summary>Cada escritura de la parte de seguridad, con su ruta esperada.</summary>
    public static IEnumerable<object[]> Escrituras()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        yield return E("decide", $"/api/inventory/approvals/{id}/decide",
            (c, k) => c.DecidirAprobacionAsync(id, new DecidirAprobacionRequest(1, null, 1, "abc"), k));
        yield return E("withdraw", $"/api/inventory/approvals/{id}/withdraw", (c, k) => c.RetirarAprobacionAsync(id, "Ya no aplica", k));
        yield return E("scope", $"/api/inventory/scopes/users/{id}",
            (c, k) => c.FijarAlcanceAsync(id, new FijarAlcanceRequest([new BodegaPedidaDto(id, true)]), k));
        yield return E("limit", "/api/inventory/amount-limits",
            (c, k) => c.FijarMontoMaximoAsync(new FijarMontoMaximoRequest(id, "Inventory.Purchases.Confirm", 5_000_000m, new DateOnly(2026, 10, 1), "Ensayo"), k));
        yield return E("policy", "/api/inventory/approval-policies",
            (c, k) => c.GuardarPoliticaDeAprobacionAsync(new GuardarPoliticaDeAprobacionRequest("DocumentConfirmation", id, new DateOnly(2026, 10, 1), "Ensayo",
                [new NivelDeAprobacionDto(1, 0, "Inventory.Adjustments.Approve")]), k));
        yield return E("parameter", "/api/inventory/parameters/INV/Existencias.StockNegativoPermitido/versions",
            (c, k) => c.NuevaVigenciaAsync("INV", "Existencias.StockNegativoPermitido",
                new NuevaVigenciaDeParametroRequest(0, null, null, "true", new DateOnly(2026, 12, 1), "Ensayo", null, null), k));
        yield return E("attend", $"/api/inventory/alerts/{id}/attend", (c, k) => c.AtenderAlertaAsync(id, "Pedido en camino", k));
        yield return E("alert-type", "/api/inventory/alert-types/Inventario.Quiebre/versions",
            (c, k) => c.NuevaVersionDeTipoDeAlertaAsync("Inventario.Quiebre",
                new VersionDeTipoDeAlertaRequest(["Inventory.Purchases.Create"], [1], null, new DateOnly(2026, 11, 1), "Ensayo", true), k));
        yield return E("salesperson", "/api/inventory/salespeople",
            (c, k) => c.CrearVendedorAsync(new CrearVendedorRequest(id, 1, false, null), k));
        yield return E("retire", $"/api/inventory/salespeople/{id}/retire", (c, k) => c.RetirarVendedorAsync(id, "Dejó de vender", k));
    }

    private static object[] E(string nombre, string ruta, Func<InventarioClient, ClaveDeOperacion, Task> accion) => [nombre, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string ruta, Func<InventarioClient, ClaveDeOperacion, Task> accion)
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.OK, new { });

        await accion(_cliente, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task El_reintento_de_una_decision_repite_la_clave_y_la_siguiente_accion_trae_otra()
    {
        var respuestas = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            Servidor.Json(HttpStatusCode.OK, new { requestPublicId = Guid.NewGuid(), status = "Approved", currentLevel = 1, source = new { publicId = Guid.NewGuid() } }),
            Servidor.Json(HttpStatusCode.OK, new { requestPublicId = Guid.NewGuid(), status = "Approved", currentLevel = 2, source = new { publicId = Guid.NewGuid() } }),
        ]);
        _servidor.Responder = _ => respuestas.Dequeue();
        var clave = new ClaveDeOperacion();
        var id = Guid.NewGuid();
        var decision = new DecidirAprobacionRequest(1, null, 1, "abc");

        (await _cliente.DecidirAprobacionAsync(id, decision, clave)).IsSuccess.Should().BeFalse();
        (await _cliente.DecidirAprobacionAsync(id, decision, clave)).IsSuccess.Should().BeTrue();
        (await _cliente.DecidirAprobacionAsync(id, decision, clave)).IsSuccess.Should().BeTrue();

        var claves = _servidor.Vistas.Select(v => v.Clave).ToList();
        claves[1].Should().Be(claves[0], "el reintento del mismo intento conserva la clave");
        claves[2].Should().NotBe(claves[1], "tras el éxito, otra acción de la persona es otra operación");
    }

    [Fact]
    public async Task Un_422_de_presencia_es_un_error_de_negocio_y_no_renueva_la_sesion()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.UnprocessableEntity,
            new { code = "Approvals.Presence.Invalid", message = "La verificación de presencia no es válida.", traceId = "t" });

        var r = await _cliente.DecidirAprobacionAsync(Guid.NewGuid(), new DecidirAprobacionRequest(1, null, 2, "abc"), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("Approvals.Presence.Invalid");
        r.StatusCode.Should().Be(422);
        _servidor.Vistas.Should().ContainSingle("no hay reintento ni renovación: sólo la decisión");
        _servidor.Vistas.Should().NotContain(v => v.Ruta.Contains("/auth/"), "un 422 no es un problema de sesión");
        _auth.CurrentAccessToken.Should().NotBeNull("la sesión sigue igual");
    }

    [Fact]
    public async Task Las_consultas_no_llevan_clave()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 50, totalCount = 0 });

        await _cliente.AprobacionesAsync(mias: true);
        await _cliente.AlertasAsync(new FiltrosDeAlertas(Status: "Pending", PageSize: 1));
        await _cliente.VendedoresAsync("16000", conRetirados: true);

        _servidor.Vistas.Select(v => v.Ruta).Should().Equal(
            "/api/inventory/approvals?mine=true&page=1&pageSize=50",
            "/api/inventory/alerts?status=Pending&page=1&pageSize=1",
            "/api/inventory/salespeople?search=16000&includeRetired=true&page=1&pageSize=50");
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task La_verificacion_de_integridad_es_una_consulta_sin_clave()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.OK, new
        {
            stream = "x:10y", fromSeq = 1, toSeq = 3, @checked = 3, anchorsChecked = 0,
            incidents = new[] { new { kind = "Altered", seq = 2, eventId = "e-2", occurredAt = Ahora } },
        });
        var integridad = new IntegridadDeAuditoriaClient(_http, _auth);

        var r = await integridad.VerificarAsync(Ahora.AddDays(-1), Ahora);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Incidents.Should().ContainSingle().Which.Kind.Should().Be("Altered");
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/audit/integrity/verify");
        vista.Clave.Should().BeNull();
        vista.Authorization.Should().BeNull();
    }

    [Theory]
    [InlineData("Creator", "creó")]
    [InlineData("PreviousLevel", "otro nivel")]
    [InlineData("Requester", "pidió")]
    public void El_motivo_de_exclusion_se_explica_en_espanol(string motivo, string contiene)
    {
        TextosDeSeguridad.Exclusion(motivo).Should().Contain(contiene);
    }

    /// <summary>El servidor, con las cabeceras que el cliente pone (la clave y la autorización).</summary>
    private sealed class Servidor : HttpMessageHandler
    {
        public List<Vista> Vistas { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Vistas.Add(new Vista(
                request.RequestUri!.PathAndQuery,
                request.Headers.TryGetValues(ClaveDeOperacion.Cabecera, out var claves) ? claves.Single() : null,
                request.Headers.Authorization?.ToString()));
            return Task.FromResult(Responder(request));
        }

        public static HttpResponseMessage Json(HttpStatusCode status, object cuerpo) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json"),
        };
    }

    private sealed record Vista(string Ruta, string? Clave, string? Authorization);
}
