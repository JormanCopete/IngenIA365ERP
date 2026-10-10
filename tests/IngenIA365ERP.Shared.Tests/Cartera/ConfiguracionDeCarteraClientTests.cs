using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0266 (US17; contracts/api.md §1.4, §1.5, §3.4, §3.9, §3.10, §14, §22.2): <see cref="ConfiguracionDeCarteraClient"/> (parámetros, plantillas,
/// alertas, autorizaciones) y <see cref="MensajesAlDeudorClient"/> (cola y plantillas de mensaje). Toda escritura lleva la
/// <c>Idempotency-Key</c> —la misma en el reintento—; ninguna pone <c>Authorization</c>; las consultas y las vistas previas no llevan clave; un
/// 422 <c>Lending.Permission.Required</c> (la clave del parámetro tiene permiso propio) vuelve con sus permisos. (nuevo)
/// </summary>
public class ConfiguracionDeCarteraClientTests
{
    private static readonly Guid Id = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateOnly Fecha = new(2026, 11, 1);

    private readonly ServidorDeCartera _servidor = new();
    private readonly ConfiguracionDeCarteraClient _configuracion;
    private readonly MensajesAlDeudorClient _mensajes;

    public ConfiguracionDeCarteraClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _configuracion = new ConfiguracionDeCarteraClient(http, auth);
        _mensajes = new MensajesAlDeudorClient(http, auth);
    }

    /// <summary>Cada escritura, con su ruta esperada.</summary>
    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("vigencia", "/api/lending/parameters/Pagos.DiasMaximosFechaValor/versions", (c, _, k) =>
            c.NuevaVigenciaAsync("Pagos.DiasMaximosFechaValor", new NuevaVigenciaDeCarteraRequest(0, null, "5", Fecha, "Política nueva", null), k));
        yield return E("atender", $"/api/lending/alerts/{Id}/attend", (c, _, k) => c.AtenderAlertaAsync(Id, "Se llamó al deudor", k));
        yield return E("tipo de alerta", "/api/lending/alert-types/Cartera.MensajeAlDeudorFallido/versions", (c, _, k) =>
            c.NuevaVersionDeTipoDeAlertaAsync("Cartera.MensajeAlDeudorFallido",
                new VersionDeTipoDeAlertaRequest(["Lending.DebtorMessages.Read"], [1], null, Fecha, "Ensayo", true), k));
        yield return E("autorización", "/api/compliance/habeas-data/authorizations", (c, _, k) =>
            c.RegistrarAutorizacionAsync(new RegistrarAutorizacionRequest(Id, 3, null, null, null, 1, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 1, null), k));
        yield return E("envío", "/api/lending/debtor-messages", (_, m, k) =>
            m.EnviarMensajeAsync(new EnviarMensajeRequest(null, Id, 1, 3, "INF-ABONO", null, null), k));
        yield return E("cancelación", $"/api/lending/debtor-messages/{Id}/cancel", (_, m, k) => m.CancelarMensajeAsync(Id, "Ya pagó", k));
        yield return E("versión de plantilla", "/api/lending/message-templates/COB-MORA/versions", (_, m, k) =>
            m.NuevaVersionDePlantillaAsync("COB-MORA", new VersionDePlantillaDeMensajeRequest(1, 2, null, "Hola {{NombreDelTitular}}", null, null, Fecha, "Texto nuevo"), k));
    }

    private static object[] E(string nombre, string ruta, Func<ConfiguracionDeCarteraClient, MensajesAlDeudorClient, ClaveDeOperacion, Task> accion) =>
        [nombre, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string ruta,
        Func<ConfiguracionDeCarteraClient, MensajesAlDeudorClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_configuracion, _mensajes, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task El_reintento_de_una_vigencia_repite_la_clave_y_el_cambio_de_contenido_trae_otra()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var clave = new ClaveDeOperacion();
        var pedido = new NuevaVigenciaDeCarteraRequest(0, null, "5", Fecha, "Política nueva", null);

        await _configuracion.NuevaVigenciaAsync("Pagos.DiasMaximosFechaValor", pedido, clave);
        await _configuracion.NuevaVigenciaAsync("Pagos.DiasMaximosFechaValor", pedido, clave);
        await _configuracion.NuevaVigenciaAsync("Pagos.DiasMaximosFechaValor", pedido with { Value = "6" }, clave);

        var claves = _servidor.Vistas.Select(v => v.Clave).ToList();
        claves[1].Should().Be(claves[0], "el reintento con el mismo contenido conserva la clave");
        claves[2].Should().NotBe(claves[1], "otro contenido con la misma clave sería Operation.KeyReused");
    }

    [Fact]
    public async Task Un_422_de_permiso_requerido_vuelve_con_sus_permisos()
    {
        _servidor.Responder = _ => ServidorDeCartera.PermisoRequerido("Lending.Segregation.Manage");

        var r = await _configuracion.NuevaVigenciaAsync("Segregacion.Incompatibilidades",
            new NuevaVigenciaDeCarteraRequest(0, null, "[]", Fecha, "Ajuste", null), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.StatusCode.Should().Be(422);
        GobiernoDeCarteraClient.PermisosQueFaltan(r).Should().Equal("Lending.Segregation.Manage");
        _servidor.Vistas.Should().ContainSingle("un 422 no dispara renovación ni reintento");
    }

    [Fact]
    public async Task Las_consultas_y_la_vista_previa_no_llevan_clave()
    {
        _servidor.Responder = r => r.RequestUri!.AbsolutePath.EndsWith("/preview", StringComparison.Ordinal)
            ? ServidorDeCartera.Json(HttpStatusCode.OK, new { subject = (string?)null, body = "Hola", length = 4, segments = 1, warnings = Array.Empty<object>() })
            : r.RequestUri.AbsolutePath.EndsWith("/contact-window", StringComparison.Ordinal)
                ? ServidorDeCartera.Json(HttpStatusCode.OK, new { decision = "Permitido", reasons = Array.Empty<string>() })
                : r.RequestUri.AbsolutePath.EndsWith("/debtor-messages", StringComparison.Ordinal) || r.RequestUri.AbsolutePath.EndsWith("/alerts", StringComparison.Ordinal)
                    ? ServidorDeCartera.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 50, totalCount = 0 })
                    : ServidorDeCartera.Json(HttpStatusCode.OK, Array.Empty<object>());

        await _configuracion.ParametrosAsync();
        await _configuracion.HistorialDeParametroAsync("Cobranza.ContactosPorDia");
        await _configuracion.PlantillasAsync();
        await _configuracion.AlertasAsync(new FiltrosDeAlertas(Status: "Pending"));
        await _configuracion.AutorizacionesAsync(Id);
        await _mensajes.MensajesAsync(new FiltrosDeMensajes(Person: Id, Status: 2));
        var ventana = await _mensajes.VentanaDeContactoAsync(Id, 2, 1);
        var vista = await _mensajes.VistaPreviaAsync(new VistaPreviaDeMensajeRequest(2, null, "Hola", 1, null, null, null));

        ventana.Value!.Decision.Should().Be("Permitido");
        vista.Value!.Segments.Should().Be(1);
        _servidor.Vistas.Select(v => v.Ruta).Should().Equal(
            "/api/lending/parameters",
            "/api/lending/parameters/Cobranza.ContactosPorDia/history",
            "/api/lending/templates",
            "/api/lending/alerts?status=Pending&page=1&pageSize=50",
            $"/api/compliance/habeas-data/authorizations?personPublicId={Id}",
            $"/api/lending/debtor-messages?personPublicId={Id}&status=2&page=1&pageSize=50",
            $"/api/lending/debtor-messages/contact-window?person={Id}&channel=2&purpose=1",
            "/api/lending/message-templates/preview");
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }
}
