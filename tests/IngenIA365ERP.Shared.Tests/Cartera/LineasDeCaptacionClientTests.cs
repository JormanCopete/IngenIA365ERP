using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 014, T070 (US1; contracts/api.md §3; research E42): las líneas de captación y de aportes en <see cref="CaptacionesClient"/>. Crear, versionar,
/// cerrar y reabrir llevan su <c>Idempotency-Key</c> y van a sus rutas de <c>/api/lending/lines</c>; la lista filtra por naturaleza sin clave; ninguna
/// petición pone <c>Authorization</c>; el cuerpo lleva los nombres del contrato con las tasas como fracción; una versión leída vuelve como el pedido de
/// la siguiente; y los textos de la naturaleza de la ruta. (nuevo)
/// </summary>
public class LineasDeCaptacionClientTests
{
    private static readonly Guid Linea = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly ServidorDeCartera _servidor = new();
    private readonly CaptacionesClient _cliente;

    public LineasDeCaptacionClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new CaptacionesClient(http, auth);
    }

    private static VersionDeCaptacionRequest ALaVista() => new()
    {
        ValidFrom = new DateOnly(2026, 12, 1),
        Reason = "Tasa aprobada por el consejo",
        Rate = new TasaDeCaptacionRequest
        {
            Tiers = [new TramoDeCaptacionRequest { BalanceFrom = 0m, BalanceTo = 999_999.99m, EffectiveAnnual = 0.01m },
                new TramoDeCaptacionRequest { BalanceFrom = 1_000_000m, EffectiveAnnual = 0.03m }],
        },
    };

    private static VersionDeAportesRequest TresPorCiento() => new()
    {
        Reason = "Estatutos", MinimumNonReducible = new ValorDeAportesRequest { Amount = 500_000m },
        Installment = new CuotaDeAportesRequest { Kind = 2, Fraction = 0.03m }, RefundTermDays = 60,
    };

    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("captación", "/api/lending/lines/deposit", (c, k) =>
            c.CrearCaptacionAsync(new CrearLineaDeCaptacionRequest { Code = "AHVISTA", Name = "Ahorro a la vista", FirstVersion = ALaVista() }, k));
        yield return E("aportes", "/api/lending/lines/contribution", (c, k) =>
            c.CrearAportesAsync(new CrearLineaDeAportesRequest { Code = "APORTES", Name = "Aportes", FirstVersion = TresPorCiento() }, k));
        yield return E("versión de captación", $"/api/lending/lines/{Linea}/deposit-versions", (c, k) => c.NuevaVersionDeCaptacionAsync(Linea, ALaVista(), k));
        yield return E("versión de aportes", $"/api/lending/lines/{Linea}/contribution-versions", (c, k) => c.NuevaVersionDeAportesAsync(Linea, TresPorCiento(), k));
        yield return E("cerrar", $"/api/lending/lines/{Linea}/close", (c, k) => c.CerrarLineaAsync(Linea, "Fin del plan", k));
        yield return E("reabrir", $"/api/lending/lines/{Linea}/reopen", (c, k) => c.ReabrirLineaAsync(Linea, "Vuelve", k));
    }

    private static object[] E(string nombre, string ruta, Func<CaptacionesClient, ClaveDeOperacion, Task> accion) => [nombre, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string ruta, Func<CaptacionesClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_cliente, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task La_lista_y_el_detalle_van_sin_clave_con_su_filtro()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, Array.Empty<object>());
        await _cliente.LineasAsync(TextosDeCaptaciones.Captacion, incluirCerradas: true);
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { });
        await _cliente.LineaAsync(Linea, new DateOnly(2026, 12, 14));

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta, v.Clave)).Should().Equal(
            ("GET", "/api/lending/lines?nature=2&includeClosed=true", null),
            ("GET", $"/api/lending/lines/{Linea}?date=2026-12-14", null));
    }

    [Fact]
    public async Task El_cuerpo_lleva_los_nombres_del_contrato_y_las_tasas_como_fraccion()
    {
        string? cuerpo = null;
        _servidor.Responder = r =>
        {
            cuerpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return ServidorDeCartera.Json(HttpStatusCode.Created, new { });
        };

        await _cliente.CrearCaptacionAsync(new CrearLineaDeCaptacionRequest { Code = "AHVISTA", Name = "Ahorro", DepositClass = 1, FirstVersion = ALaVista() },
            new ClaveDeOperacion());

        using var json = JsonDocument.Parse(cuerpo!);
        var version = json.RootElement.GetProperty("firstVersion");
        json.RootElement.GetProperty("depositClass").GetInt32().Should().Be(1);
        version.GetProperty("rate").GetProperty("tiers")[1].GetProperty("effectiveAnnual").GetDecimal().Should().Be(0.03m);
        version.GetProperty("rate").GetProperty("kind").GetInt32().Should().Be(1);
        version.GetProperty("minimums").GetProperty("toEarnInterest").GetDecimal().Should().Be(0m);
        version.GetProperty("rounding").GetProperty("moment").GetInt32().Should().Be(3, "al peso por liquidación (D9)");
    }

    [Fact]
    public void Una_version_leida_vuelve_como_el_pedido_de_la_siguiente()
    {
        var leida = new VersionDeCaptacionDto(Guid.NewGuid(), 1, new DateOnly(2026, 12, 1), null, DateTime.UtcNow, "Alta", TextosDeCaptaciones.Cdat, [],
            true, false, false, false, false, 1, 0.09m, null, null, null, null, null, null, null,
            [new TramoDeCaptacionDto(1, null, null, 30, 179, null, null, 0.08m, null)], false, false, false, 0, 1, 2, 1, 1m, 1, 3, 0m, 0m, 0m, 0, null,
            "Retencion.TarifaCdat", false, null, null, null, null, 360, true, false, false,
            new CdatDeLaLineaDto(30, 540, [0, 30], [1], true, null, 2, false, 3, false, 2, Guid.NewGuid(), new RedencionAnticipadaDto(true, 1, 0.02m, null, 30)),
            null, null, null, new string('a', 64));

        var pedido = leida.ComoPedido();

        pedido.Rate.EffectiveAnnual.Should().Be(0.09m);
        pedido.Rate.Tiers.Should().ContainSingle().Which.TermDaysTo.Should().Be(179);
        pedido.TermDeposit!.InterestPeriodicities.Should().Equal(0, 30);
        pedido.TermDeposit.EarlyRedemption.PenaltyRateEa.Should().Be(0.02m);
        pedido.Tax.WithholdingRateParameterCode.Should().Be("Retencion.TarifaCdat");
        pedido.Reason.Should().BeEmpty("la versión nueva dice su propio motivo");
    }

    [Fact]
    public void Ajustar_a_la_clase_deja_solo_su_bloque()
    {
        var v = new VersionDeCaptacionRequest();

        v.AjustarALaClase(TextosDeCaptaciones.Cdat);
        v.TermDeposit.Should().NotBeNull();
        v.AllowsPartialWithdrawals.Should().BeFalse();
        v.AjustarALaClase(TextosDeCaptaciones.Contractual);
        v.TermDeposit.Should().BeNull();
        v.Contractual.Should().NotBeNull();
        v.AjustarALaClase(TextosDeCaptaciones.ALaVista);
        v.Contractual.Should().BeNull();
        v.Permanent.Should().BeNull();
    }

    [Theory]
    [InlineData("captacion", TextosDeCaptaciones.Captacion)]
    [InlineData("Aportes", TextosDeCaptaciones.Aportes)]
    [InlineData(null, TextosDeCaptaciones.Colocacion)]
    [InlineData("otra", TextosDeCaptaciones.Colocacion)]
    public void La_naturaleza_de_la_ruta(string? texto, int naturaleza)
    {
        TextosDeCaptaciones.NaturalezaDeLaRuta(texto).Should().Be(naturaleza);
        TextosDeCaptaciones.NaturalezaDeLaRuta(TextosDeCaptaciones.RutaDeLaNaturaleza(naturaleza)).Should().Be(naturaleza);
    }
}
