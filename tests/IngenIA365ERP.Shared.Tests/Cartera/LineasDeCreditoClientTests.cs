using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0342 (feature 013, US1; contracts/api.md §1.5, §3.1–§3.3): <see cref="LineasDeCreditoClient"/>. Crear, versionar, cerrar, reabrir,
/// borrar, el puntaje, las equivalencias e importar llevan su <c>Idempotency-Key</c> —revisar y aplicar la plantilla son dos operaciones con
/// dos claves—; ninguna petición pone <c>Authorization</c>; las consultas y <c>usury-check</c> (una consulta por POST) van sin clave; un 422
/// de usura se explica en español con la usura, la modalidad y la diferencia. (nuevo)
/// </summary>
public class LineasDeCreditoClientTests
{
    private static readonly Guid Linea = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateOnly Desde = new(2026, 11, 1);

    private readonly ServidorDeCartera _servidor = new();
    private readonly LineasDeCreditoClient _cliente;

    public LineasDeCreditoClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new LineasDeCreditoClient(http, auth);
    }

    private static VersionDeLineaRequest Version() => new()
    {
        ValidFrom = Desde, Reason = "Tasa del comité", ModalityCode = "CONSUMO", SubsegmentCode = "SinLibranza",
        Rates = new TasasDeLineaRequest { Current = new TasaCorrienteRequest { Kind = 1, EffectiveAnnual = 0.22m } },
        Amounts = new RangoDeMontosRequest { Min = 500_000m, Max = 30_000_000m },
        Terms = new RangoDePlazosRequest { MinMonths = 1, MaxMonths = 60 },
    };

    /// <summary>Cada escritura con su método y su ruta esperados.</summary>
    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("crear", "POST", "/api/lending/credit-lines", (c, k) =>
            c.CrearAsync(new CrearLineaRequest("LIBRE", "Libre inversión", "Libre destino", Version()), k));
        yield return E("versionar", "POST", $"/api/lending/credit-lines/{Linea}/versions", (c, k) => c.NuevaVersionAsync(Linea, Version(), k));
        yield return E("cerrar", "POST", $"/api/lending/credit-lines/{Linea}/close", (c, k) => c.CerrarAsync(Linea, "Se retira del portafolio", k));
        yield return E("reabrir", "POST", $"/api/lending/credit-lines/{Linea}/reopen", (c, k) => c.ReabrirAsync(Linea, "Vuelve al portafolio", k));
        yield return E("borrar", "DELETE", $"/api/lending/credit-lines/{Linea}?reason=Creada%20por%20error", (c, k) => c.BorrarAsync(Linea, "Creada por error", k));
        yield return E("puntaje", "POST", $"/api/lending/credit-lines/{Linea}/scoring", (c, k) =>
            c.FijarPuntajeAsync(Linea, new PuntajeRequest(Desde, "Modelo nuevo", 600m, null,
                [new VariableDePuntajeDto("ANT", "Antigüedad", "AssociateSeniorityMonths", 1m, [new RangoDePuntajeDto(0m, null, 800m)])]), k));
        yield return E("equivalencias", "PUT", "/api/lending/commercial-term-equivalences", (c, k) =>
            c.FijarEquivalenciasAsync(new EquivalenciasDeComercioRequest([new EquivalenciaDeComercioRequest(1, "Monthly", 30)], Desde, "Ventas a crédito"), k));
        yield return E("revisar", "POST", "/api/lending/credit-lines/import?mode=review", (c, k) =>
            c.RevisarPlantillaAsync("lineas.xlsx", [1, 2, 3], "Carga inicial", k));
        yield return E("aplicar", "POST", "/api/lending/credit-lines/import?mode=apply", (c, k) =>
            c.AplicarPlantillaAsync("lineas.xlsx", [1, 2, 3], "Carga inicial", k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<LineasDeCreditoClient, ClaveDeOperacion, Task> accion) =>
        [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta,
        Func<LineasDeCreditoClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_cliente, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo);
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task Revisar_y_aplicar_son_dos_operaciones_con_dos_claves()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { template = "lending.credit-lines", valid = true, applied = false });
        var revision = new ClaveDeOperacion();
        var aplicacion = new ClaveDeOperacion();

        await _cliente.RevisarPlantillaAsync("lineas.xlsx", [1, 2, 3], "Carga inicial", revision);
        await _cliente.AplicarPlantillaAsync("lineas.xlsx", [1, 2, 3], "Carga inicial", aplicacion);

        var claves = _servidor.Vistas.Select(v => v.Clave).ToList();
        claves.Should().HaveCount(2).And.OnlyHaveUniqueItems("revisar no guarda; aplicar es otra operación");
    }

    [Fact]
    public async Task El_reintento_de_una_version_repite_la_clave_y_otro_contenido_trae_otra()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var clave = new ClaveDeOperacion();
        var version = Version();

        await _cliente.NuevaVersionAsync(Linea, version, clave);
        await _cliente.NuevaVersionAsync(Linea, version, clave);
        version.Rates.Current.EffectiveAnnual = 0.21m;
        await _cliente.NuevaVersionAsync(Linea, version, clave);

        var claves = _servidor.Vistas.Select(v => v.Clave).ToList();
        claves[1].Should().Be(claves[0], "el reintento con el mismo contenido conserva la clave");
        claves[2].Should().NotBe(claves[1], "otro contenido con la misma clave sería Operation.KeyReused");
    }

    [Fact]
    public async Task Las_consultas_y_usury_check_van_sin_clave()
    {
        _servidor.Responder = r =>
        {
            var ruta = r.RequestUri!.AbsolutePath;
            return ruta.EndsWith("/usury-check", StringComparison.Ordinal)
                ? ServidorDeCartera.Json(HttpStatusCode.OK, new
                {
                    date = "2026-10-10", versionPublicId = Guid.NewGuid(), modalityCode = "CONSUMO", usuryKey = "CONSUMO_Y_ORDINARIO", field = "rates.current",
                    rate = 0.22m, effectiveRate = 0.2231m, effectiveRateWithoutCharges = 0.22m, chargesSpread = 0.0031m, usuryRate = 0.2562m,
                    nationalValuePublicId = Guid.NewGuid(), headroom = 0.0331m, computableCharges = Array.Empty<object>(), explanation = new[] { "TIR diaria" },
                    passes = true,
                })
                : ruta.EndsWith("/modalities", StringComparison.Ordinal)
                    ? ServidorDeCartera.Json(HttpStatusCode.OK, new
                    {
                        documentPublicId = Guid.NewGuid(), code = "MODALIDADES", validFrom = "2026-01-01", sha256 = "x", verified = false,
                        modalities = Array.Empty<object>(),
                    })
                    : ServidorDeCartera.Json(HttpStatusCode.OK, Array.Empty<object>());
        };

        await _cliente.LineasAsync(new FiltrosDeLineas(Status: 2, IncludeClosed: true));
        var usura = await _cliente.ConsultarUsuraAsync(Linea, new ConsultaDeUsuraRequest(Amount: 5_000_000m));
        await _cliente.ModalidadesAsync(Desde);
        await _cliente.EquivalenciasAsync();

        usura.Value!.Passes.Should().BeTrue();
        usura.Value.Headroom.Should().Be(0.0331m);
        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("GET", "/api/lending/credit-lines?status=2&includeClosed=true"),
            ("POST", $"/api/lending/credit-lines/{Linea}/usury-check"),
            ("GET", "/api/lending/modalities?date=2026-11-01"),
            ("GET", "/api/lending/commercial-term-equivalences"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null, "ninguna escribe: usury-check es una consulta (api.md §1.5)");
        _servidor.Vistas.Should().OnlyContain(v => v.Authorization == null);
    }

    [Fact]
    public async Task Un_422_de_usura_se_explica_con_la_usura_la_modalidad_y_la_diferencia()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Lending.Usury.Exceeded",
            message = "La tasa supera la usura.",
            data = new
            {
                effectiveRate = 0.271m, usuryRate = 0.2562m, modalityKey = "CONSUMO_Y_ORDINARIO", date = "2026-11-01",
                errors = new[] { new { field = "rates.current.effectiveAnnual" } },
            },
            traceId = "t",
        });

        var r = await _cliente.CrearAsync(new CrearLineaRequest("LIBRE", "Libre inversión", null, Version()), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.StatusCode.Should().Be(422);
        var texto = LineasDeCreditoClient.ExplicarUsuraExcedida(r);
        texto.Should().Contain("27,1 %").And.Contain("25,62 %").And.Contain("CONSUMO_Y_ORDINARIO").And.Contain("1,48 puntos");
    }
}
