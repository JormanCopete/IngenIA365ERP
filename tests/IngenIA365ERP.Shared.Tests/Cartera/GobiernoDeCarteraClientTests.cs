using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0266 (US17; contracts/api.md §1.4, §1.5, §3.5–§3.8, §5): <see cref="GobiernoDeCarteraClient"/>. Toda escritura (matriz, montos, órganos,
/// miembros, sesiones, asistencia, puntos, desafío, voto, cierre, vinculados, alcance) lleva la <c>Idempotency-Key</c> de la acción de la
/// persona —la misma en el reintento, otra tras el éxito—; ninguna pone <c>Authorization</c> a mano (<c>ElTokenDeSesionLoPoneElHandler</c>);
/// las consultas y la vista previa (consulta por POST) no llevan clave; y un 422 <c>Lending.Permission.Required</c> vuelve como error de
/// negocio con sus permisos, sin tocar la sesión. (nuevo)
/// </summary>
public class GobiernoDeCarteraClientTests
{
    private static readonly Guid Id = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Otro = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private static readonly DateOnly Fecha = new(2026, 11, 1);

    private readonly ServidorDeCartera _servidor = new();
    private readonly GobiernoDeCarteraClient _cliente;
    private readonly Services.Security.CentralAuthClient _auth;

    public GobiernoDeCarteraClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _auth = auth;
        _cliente = new GobiernoDeCarteraClient(http, auth);
    }

    private static ReglasDeOrganoRequest Reglas() =>
        new(new FraccionRequest(1, 2, 2), new FraccionRequest(1, 2, 2), null, Fecha, "Estatutos", "Acta 12 de la asamblea", 1);

    /// <summary>Cada escritura del cliente, con su método y su ruta esperados.</summary>
    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("matriz", "POST", "/api/lending/attribution-matrices", (c, k) => c.PublicarMatrizAsync(new PublicarMatrizRequest(Fecha, "Ajuste anual", null,
            [new NivelDeAtribucionRequest("GER", "Gerencia", 1, 1, "Lending.Decisions.Manager", null, 10_000_000m, false, false)],
            [new ReglaDeAtribucionRequest(1, "CreditApplication", "GER", null)]), k));
        yield return E("monto", "POST", "/api/lending/amount-limits",
            (c, k) => c.FijarMontoAsync(new FijarMontoMaximoRequest(Id, "Lending.Disbursements.Execute", 5_000_000m, Fecha, "Ensayo"), k));
        yield return E("órgano", "POST", "/api/lending/governing-bodies",
            (c, k) => c.CrearOrganoAsync(new CrearOrganoRequest("CCR", "Comité de crédito", 3, true, "CC", Reglas()), k));
        yield return E("reglas", "POST", $"/api/lending/governing-bodies/{Id}/rules", (c, k) => c.NuevasReglasAsync(Id, Reglas(), k));
        yield return E("miembro", "POST", $"/api/lending/governing-bodies/{Id}/members",
            (c, k) => c.NombrarMiembroAsync(Id, new NombrarMiembroRequest(Otro, 3, true, Fecha, null, null, null), k));
        yield return E("fin de miembro", "POST", $"/api/lending/governing-bodies/{Id}/members/{Otro}/end",
            (c, k) => c.TerminarMiembroAsync(Id, Otro, new FinDeVigenciaRequest(Fecha, "Renunció"), k));
        yield return E("inactivar órgano", "POST", $"/api/lending/governing-bodies/{Id}/deactivate", (c, k) => c.InactivarOrganoAsync(Id, "Se fusionó", k));
        yield return E("convocar", "POST", "/api/lending/sessions",
            (c, k) => c.ConvocarAsync(new ConvocarSesionRequest(Id, 1, 1, Fecha, "Sede", null), k));
        yield return E("asistencia", "POST", $"/api/lending/sessions/{Id}/attendance",
            (c, k) => c.RegistrarAsistenciaAsync(Id, new AsistenciaRequest([new AsistenteRequest(Otro, true)], null), k));
        yield return E("punto", "POST", $"/api/lending/sessions/{Id}/items",
            (c, k) => c.AgregarPuntoAsync(Id, new PuntoDeSesionRequest("CreditApplication", Otro, 1, null, null), k));
        yield return E("corregir punto", "PUT", $"/api/lending/sessions/{Id}/items/{Otro}",
            (c, k) => c.CorregirPuntoAsync(Id, Otro, new CorreccionDePuntoRequest(1, null, "Ajuste"), k));
        yield return E("retirar punto", "DELETE", $"/api/lending/sessions/{Id}/items/{Otro}", (c, k) => c.RetirarPuntoAsync(Id, Otro, "Duplicado", k));
        yield return E("desafío", "POST", $"/api/lending/sessions/{Id}/presence-challenge", (c, k) => c.DesafioDePresenciaAsync(Id, Otro, k));
        yield return E("voto", "POST", $"/api/lending/sessions/{Id}/items/{Otro}/votes",
            (c, k) => c.VotarAsync(Id, Otro, new VotoRequest(Otro, 1, 1, null, null), k));
        yield return E("cierre", "POST", $"/api/lending/sessions/{Id}/close", (c, k) => c.CerrarAsync(Id, "abc", k));
        yield return E("vínculo", "POST", "/api/lending/related-parties",
            (c, k) => c.RegistrarVinculoAsync(new RegistrarVinculoRequest(Otro, 1, Fecha, null, null, null, null, null, "Acta de elección", null), k));
        yield return E("fin de vínculo", "POST", $"/api/lending/related-parties/{Id}/end",
            (c, k) => c.TerminarVinculoAsync(Id, new FinDeVigenciaRequest(Fecha, "Terminó su periodo"), k));
        yield return E("alcance", "PUT", $"/api/lending/scopes/users/{Id}",
            (c, k) => c.FijarLineasAsync(Id, new FijarLineasRequest([new LineaAsignadaRequest(Otro, true)]), k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<GobiernoDeCarteraClient, ClaveDeOperacion, Task> accion) =>
        [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta,
        Func<GobiernoDeCarteraClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_cliente, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo);
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task El_reintento_de_un_voto_repite_la_clave_y_la_siguiente_accion_trae_otra()
    {
        var respuestas = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            ServidorDeCartera.Json(HttpStatusCode.Created, new { votePublicId = Guid.NewGuid() }),
            ServidorDeCartera.Json(HttpStatusCode.Created, new { votePublicId = Guid.NewGuid() }),
        ]);
        _servidor.Responder = _ => respuestas.Dequeue();
        var clave = new ClaveDeOperacion();
        var voto = new VotoRequest(Otro, 1, 3, new PresenciaDeVotoRequest(Id, "{\"id\":\"abc\"}", null), null);

        (await _cliente.VotarAsync(Id, Otro, voto, clave)).IsSuccess.Should().BeFalse();
        (await _cliente.VotarAsync(Id, Otro, voto, clave)).IsSuccess.Should().BeTrue();
        (await _cliente.VotarAsync(Id, Otro, voto, clave)).IsSuccess.Should().BeTrue();

        var claves = _servidor.Vistas.Select(v => v.Clave).ToList();
        claves[1].Should().Be(claves[0], "el reintento del mismo voto conserva la clave");
        claves[2].Should().NotBe(claves[1], "tras el éxito, otra acción de la persona es otra operación");
    }

    [Fact]
    public async Task Un_422_de_permiso_requerido_es_un_error_de_negocio_con_sus_permisos()
    {
        _servidor.Responder = _ => ServidorDeCartera.PermisoRequerido("Lending.Sessions.RecordVotes", "Lending.Sessions.Vote");

        var r = await _cliente.VotarAsync(Id, Otro, new VotoRequest(Otro, 1, 1, null, null), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.StatusCode.Should().Be(422);
        r.ErrorCode.Should().Be(GobiernoDeCarteraClient.PermisoRequerido);
        GobiernoDeCarteraClient.PermisosQueFaltan(r).Should().Equal("Lending.Sessions.RecordVotes", "Lending.Sessions.Vote");
        _servidor.Vistas.Should().ContainSingle("no hay reintento ni renovación: sólo el voto");
        _servidor.Vistas.Should().NotContain(v => v.Ruta.Contains("/auth/"), "un 422 no es un problema de sesión");
        _auth.CurrentAccessToken.Should().NotBeNull("la sesión sigue igual");
    }

    [Fact]
    public async Task Las_consultas_y_la_vista_previa_no_llevan_clave()
    {
        _servidor.Responder = r => r.RequestUri!.AbsolutePath.EndsWith("/preview", StringComparison.Ordinal) || r.RequestUri.AbsolutePath.EndsWith("matrices", StringComparison.Ordinal)
            ? ServidorDeCartera.Json(HttpStatusCode.OK, Array.Empty<object>())
            : ServidorDeCartera.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 50, totalCount = 0 });

        await _cliente.MatricesAsync(conHistoria: true);
        await _cliente.VistaPreviaDeAtribucionAsync(new VistaPreviaDeAtribucionRequest("CreditApplication", 1_000_000m));
        await _cliente.SesionesAsync(new FiltrosDeSesiones(Body: Id, Status: 3));
        await _cliente.VinculadosAsync(persona: Otro);
        await _cliente.ReclamosAsync(estado: 1);

        _servidor.Vistas.Select(v => v.Ruta).Should().Equal(
            "/api/lending/attribution-matrices?includeHistory=true",
            "/api/lending/attribution-matrices/preview",
            $"/api/lending/sessions?body={Id}&status=3&page=1&pageSize=50",
            $"/api/lending/related-parties?personPublicId={Otro}&page=1&pageSize=50",
            "/api/lending/credit-report-claims?status=1&page=1&pageSize=50");
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task El_acta_en_PDF_es_una_lectura_sin_clave()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) { Headers = { ContentType = new("application/pdf") } },
        };

        var r = await _cliente.ActaEnPdfAsync(Id);

        r.IsSuccess.Should().BeTrue();
        r.Value!.TipoContenido.Should().Be("application/pdf");
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/lending/sessions/{Id}/minutes.pdf");
        vista.Clave.Should().BeNull();
        vista.Authorization.Should().BeNull();
    }
}
