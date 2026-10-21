using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0383 (feature 013, US2; contracts/api.md §1.5, §7.1, §7.2, §7.4): <see cref="CuposClient"/>. Otorgar, terminar, revalidar, la preferencia,
/// bloquear, levantar, los grupos conectados y la plantilla de aportes llevan su <c>Idempotency-Key</c> —revisar y aplicar son dos operaciones
/// con dos claves—; el disponible y las listas no; ninguna petición pone <c>Authorization</c>. (nuevo)
/// </summary>
public class CuposClientTests
{
    private static readonly Guid Persona = Guid.Parse("44444444-5555-6666-7777-888888888888");
    private static readonly Guid Cupo = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private static readonly Guid Linea = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Otro = Guid.Parse("55555555-6666-7777-8888-999999999999");
    private static readonly DateOnly Hoy = new(2026, 10, 10);

    private readonly ServidorDeCartera _servidor = new();
    private readonly CuposClient _cliente;

    public CuposClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new CuposClient(http, auth);
    }

    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("otorgar", "POST", "/api/lending/credit-limits", (c, k) =>
            c.OtorgarAsync(new OtorgarCupoRequest(Persona, 3, null, 2, 500_000m, Hoy, null, 1, "Cupo rotativo"), k));
        yield return E("terminar", "POST", $"/api/lending/credit-limits/{Cupo}/end", (c, k) => c.TerminarAsync(Cupo, Hoy, "Retiro", k));
        yield return E("revalidar", "POST", $"/api/lending/credit-limits/{Cupo}/revalidate", (c, k) => c.RevalidarAsync(Cupo, "Terminó el vínculo", k));
        yield return E("preferencia", "POST", $"/api/lending/credit-limits/{Cupo}/prepayment-preference", (c, k) =>
            c.CambiarPreferenciaAsync(Cupo, new PreferenciaDeAbonoRequest(2, "Pide reducir cuota"), k));
        yield return E("bloquear", "POST", "/api/lending/blocks", (c, k) => c.BloquearAsync(new BloqueoRequest(Persona, 9, "Oficio del juzgado"), k));
        yield return E("levantar", "POST", $"/api/lending/blocks/{Otro}/lift", (c, k) => c.LevantarAsync(Otro, "Levantamiento del embargo", k));
        yield return E("grupo", "POST", "/api/lending/connected-groups", (c, k) =>
            c.RegistrarGrupoAsync(new RegistrarGrupoRequest("Familia Pérez", 1, "Negocio común", [Persona, Otro]), k));
        yield return E("miembro", "POST", $"/api/lending/connected-groups/{Otro}/members", (c, k) =>
            c.AgregarMiembroAsync(Otro, new MiembroDeGrupoRequest(Persona, "Socio del negocio"), k));
        yield return E("retirar", "POST", $"/api/lending/connected-groups/{Otro}/members/{Persona}/end", (c, k) => c.RetirarMiembroAsync(Otro, Persona, "Vendió", k));
        yield return E("terminar grupo", "POST", $"/api/lending/connected-groups/{Otro}/end", (c, k) => c.TerminarGrupoAsync(Otro, "Se disolvió", k));
        yield return E("revisar", "POST", "/api/lending/contribution-balances/import?mode=review", (c, k) => c.RevisarAportesAsync("aportes.xlsx", [1, 2], "Corte", k));
        yield return E("aplicar", "POST", "/api/lending/contribution-balances/import?mode=apply", (c, k) => c.AplicarAportesAsync("aportes.xlsx", [1, 2], "Corte", k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<CuposClient, ClaveDeOperacion, Task> accion) => [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta, Func<CuposClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_cliente, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo);
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task Revisar_y_aplicar_los_aportes_son_dos_operaciones_con_dos_claves()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { template = "lending.contribution-balances", valid = true, applied = false });

        await _cliente.RevisarAportesAsync("aportes.xlsx", [1, 2], "Corte", new ClaveDeOperacion());
        await _cliente.AplicarAportesAsync("aportes.xlsx", [1, 2], "Corte", new ClaveDeOperacion());

        _servidor.Vistas.Select(v => v.Clave).Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task El_reintento_de_otorgar_repite_la_clave_y_otro_monto_trae_otra()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var clave = new ClaveDeOperacion();
        var pedido = new OtorgarCupoRequest(Persona, 1, null, 2, 5_000_000m, Hoy, null, null, "Cupo consolidado");

        await _cliente.OtorgarAsync(pedido, clave);
        await _cliente.OtorgarAsync(pedido, clave);
        await _cliente.OtorgarAsync(pedido with { Amount = 4_000_000m }, clave);

        var claves = _servidor.Vistas.Select(v => v.Clave).ToList();
        claves[1].Should().Be(claves[0]);
        claves[2].Should().NotBe(claves[1], "otro contenido con la misma clave sería Operation.KeyReused");
    }

    [Fact]
    public async Task Las_consultas_van_sin_clave()
    {
        _servidor.Responder = r => r.RequestUri!.AbsolutePath.EndsWith("/connected-groups", StringComparison.Ordinal)
            ? ServidorDeCartera.Json(HttpStatusCode.OK, Array.Empty<object>())
            : ServidorDeCartera.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0 });

        await _cliente.DisponibleAsync(Persona, Linea, Hoy);
        await _cliente.CuposAsync(new FiltrosDeCupos(Persona, Kind: 3));
        await _cliente.BloqueosAsync(new FiltrosDeBloqueos(Persona, Active: true));
        await _cliente.SaldosDeAportesAsync(Persona, Hoy);
        await _cliente.GruposAsync(Persona, incluirTerminados: true);

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("GET", $"/api/lending/credit-limits/availability?personPublicId={Persona}&creditLinePublicId={Linea}&date=2026-10-10"),
            ("GET", $"/api/lending/credit-limits?personPublicId={Persona}&kind=3&page=1&pageSize=20"),
            ("GET", $"/api/lending/blocks?personPublicId={Persona}&active=true&page=1&pageSize=20"),
            ("GET", $"/api/lending/contribution-balances?personPublicId={Persona}&asOf=2026-10-10&page=1"),
            ("GET", $"/api/lending/connected-groups?personPublicId={Persona}&includeEnded=true"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }
}
