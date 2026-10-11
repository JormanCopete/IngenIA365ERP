using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0507 (feature 013, US4; contracts/api.md §1.5, §8.1, §16.4, §16.5): <see cref="CreditosClient"/> y <see cref="CorridasDeCarteraClient"/>. Las
/// consultas del crédito, la liquidación a (D, R), las corridas y la verificación del libro van sin clave; ordenar y reintentar una corrida la
/// llevan; ninguna pone <c>Authorization</c>. R viaja como instante UTC con su zona, para que la API no lo lea como hora local. (nuevo)
/// </summary>
public class CreditosClientTests
{
    private static readonly Guid Credito = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private static readonly Guid Operacion = Guid.Parse("44444444-5555-6666-7777-888888888888");
    private static readonly Guid Corrida = Guid.Parse("55555555-6666-7777-8888-999999999999");

    private readonly ServidorDeCartera _servidor = new();
    private readonly CreditosClient _creditos;
    private readonly CorridasDeCarteraClient _corridas;

    public CreditosClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _creditos = new CreditosClient(http, auth);
        _corridas = new CorridasDeCarteraClient(http, auth);
    }

    [Fact]
    public async Task Las_consultas_del_credito_van_sin_clave_y_la_liquidacion_lleva_D_y_R_en_UTC()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0 });
        var d = new DateOnly(2026, 12, 15);
        var r = new DateTime(2026, 12, 15, 15, 2, 11, DateTimeKind.Utc);

        await _creditos.ListarAsync("CR-0001", "Active", moraMinima: 30);
        await _creditos.CreditoAsync(Credito);
        await _creditos.EstadoDeCuentaAsync(Credito, d);
        await _creditos.LiquidacionAsync(Credito, d, r);
        await _creditos.PlanAsync(Credito, 2);
        await _creditos.VersionesDelPlanAsync(Credito);
        await _creditos.OperacionesAsync(Credito);
        await _creditos.OperacionAsync(Credito, Operacion);
        await _creditos.LibroAsync(Credito, d.AddDays(-30), d, "CurrentInterest");

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("GET", "/api/lending/loans?search=CR-0001&status=Active&minDaysPastDue=30&page=1&pageSize=20"),
            ("GET", $"/api/lending/loans/{Credito}"),
            ("GET", $"/api/lending/loans/{Credito}/statement?date=2026-12-15"),
            ("GET", $"/api/lending/loans/{Credito}/settlement?date=2026-12-15&recordedAsOf=2026-12-15T15%3A02%3A11.0000000Z"),
            ("GET", $"/api/lending/loans/{Credito}/schedule?version=2"),
            ("GET", $"/api/lending/loans/{Credito}/schedule/versions"),
            ("GET", $"/api/lending/loans/{Credito}/operations?page=1&pageSize=20"),
            ("GET", $"/api/lending/loans/{Credito}/operations/{Operacion}"),
            ("GET", $"/api/lending/loans/{Credito}/transactions?from=2026-11-15&to=2026-12-15&concept=CurrentInterest&page=1&pageSize=50"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task Ordenar_y_reintentar_una_corrida_llevan_su_clave_y_verificar_el_libro_no()
    {
        await _corridas.OrdenarCicloAsync(new DateOnly(2026, 10, 11), new ClaveDeOperacion());
        await _corridas.ReintentarAsync(Corrida, "El índice ya se replicó", new ClaveDeOperacion());
        await _corridas.ListarAsync("DailyCycle", "Failed");
        await _corridas.CorridaAsync(Corrida);
        await _corridas.VerificarLibroAsync(new VerificacionDelLibroRequest(SampleSize: 20));

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta, v.Clave is not null)).Should().Equal(
            ("POST", "/api/lending/process-runs", true),
            ("POST", $"/api/lending/process-runs/{Corrida}/retry", true),
            ("GET", "/api/lending/process-runs?kind=DailyCycle&status=Failed&page=1&pageSize=20", false),
            ("GET", $"/api/lending/process-runs/{Corrida}?page=1", false),
            ("POST", "/api/lending/integrity/verify", false));
        _servidor.Vistas.Should().OnlyContain(v => v.Authorization == null, "la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public void R_sin_zona_se_toma_como_UTC_y_la_hora_local_se_convierte()
    {
        CreditosClient.Instante(null).Should().BeNull();
        CreditosClient.Instante(new DateTime(2026, 10, 11, 8, 30, 0, DateTimeKind.Unspecified)).Should().Be("2026-10-11T08:30:00.0000000Z");
        var local = new DateTime(2026, 10, 11, 8, 30, 0, DateTimeKind.Local);
        CreditosClient.Instante(local).Should().Be(local.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture));
    }
}
