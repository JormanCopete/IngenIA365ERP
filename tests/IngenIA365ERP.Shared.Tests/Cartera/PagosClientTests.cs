using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0561 (feature 013, US5; contracts/api.md §8.3, §10, §20): <see cref="PagosClient"/> y el recaudador de <see cref="DoblesDePruebaClient"/>.
/// Reversar, cambiar la preferencia, aplicar pagos de prueba, reversarlos por su clave y resolver saldos llevan su <c>Idempotency-Key</c>; simular,
/// la vista previa del reverso, leer la preferencia, listar obligaciones, simular por el contrato y los reversos recibidos no; ninguna pone
/// <c>Authorization</c>. Los enums del contrato llegan como número y se leen. (nuevo)
/// </summary>
public class PagosClientTests
{
    private static readonly Guid Credito = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private static readonly Guid Operacion = Guid.Parse("44444444-5555-6666-7777-888888888888");
    private static readonly Guid Persona = Guid.Parse("55555555-6666-7777-8888-999999999999");
    private const string ClavePago = "TST:0123456789abcdef0123456789abcdef";

    private readonly ServidorDeCartera _servidor = new();
    private readonly PagosClient _pagos;
    private readonly DoblesDePruebaClient _dobles;

    public PagosClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _pagos = new PagosClient(http, auth);
        _dobles = new DoblesDePruebaClient(http, auth);
    }

    public sealed record Clientes(PagosClient P, DoblesDePruebaClient T);

    private Clientes Todos => new(_pagos, _dobles);

    private static PagoDePruebaRequest Pago() => new(ClavePago, Credito, "CycleInstallment", 250_000m, new DateOnly(2026, 10, 11), "Cash", Persona,
        new ReciboDePruebaRequest("TST-1", new DateOnly(2026, 10, 11)));

    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("reversar", "POST", $"/api/lending/loans/{Credito}/operations/{Operacion}/reverse", (c, k) =>
            c.P.ReversarAsync(Credito, Operacion, new ReversoDePagoRequest("Cheque devuelto", "sha256:abc"), k));
        yield return E("preferencia", "POST", $"/api/lending/loans/{Credito}/prepayment-preference", (c, k) =>
            c.P.CambiarPreferenciaAsync(Credito, new CambioDePreferenciaRequest("ReduceTerm", "Instrucción escrita del deudor"), k));
        yield return E("aplicar", "POST", "/api/lending/test-doubles/collector/payments", (c, k) =>
            c.T.AplicarPagosAsync(new LoteDePagosDePruebaRequest([Pago()], Interactive: false), k));
        yield return E("reversar por clave", "POST", "/api/lending/test-doubles/collector/payments/TST%3A0123456789abcdef0123456789abcdef/reverse", (c, k) =>
            c.T.ReversarPagoAsync(ClavePago, "Prueba", k));
        yield return E("resolver", "POST", "/api/lending/test-doubles/collector/credit-balances/resolve", (c, k) =>
            c.T.ResolverSaldoAsync(new ResolucionDePruebaRequest(Credito, "RefundThroughCollector", ClavePago), k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<Clientes, ClaveDeOperacion, Task> accion) => [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta, Func<Clientes, ClaveDeOperacion, Task> accion)
    {
        await accion(Todos, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo);
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task Simular_ver_y_listar_van_sin_clave()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { });

        await _pagos.SimularAsync(Credito, new SimulacionDePagoRequest("FullPayoff", null, new DateOnly(2026, 10, 11)));
        await _pagos.VistaPreviaDelReversoAsync(Credito, Operacion, "Cheque devuelto");
        await _pagos.PreferenciaAsync(Credito);
        await _dobles.ObligacionesAsync(Persona, comoCodeudor: true);
        await _dobles.SimularPagoAsync(new SimulacionDelRecaudadorRequest(Credito, "FreeAmount", 100_000m, null));
        await _dobles.ReversosRecibidosAsync();

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("POST", $"/api/lending/loans/{Credito}/payment-simulations"),
            ("POST", $"/api/lending/loans/{Credito}/operations/{Operacion}/reversal-preview"),
            ("GET", $"/api/lending/loans/{Credito}/prepayment-preference"),
            ("GET", $"/api/lending/test-doubles/collector/obligations?personPublicId={Persona}&includeCoDebtor=true"),
            ("POST", "/api/lending/test-doubles/collector/simulations"),
            ("GET", "/api/lending/test-doubles/collector/reversals"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task Los_enums_del_contrato_llegan_como_numero()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new
        {
            result = 4,
            code = "Lending.Contract.Answered",
            message = "Atendida.",
            value = new
            {
                lendingStatus = 2,
                personPublicId = Persona,
                date = "2026-10-11",
                obligations = new[]
                {
                    new
                    {
                        obligationPublicId = Credito, number = "CR-1", creditLineCode = "LIB", creditLineName = "Libre inversión", role = 1, status = 2,
                        daysPastDue = 0, dueToday = 0m, cycleInstallment = 250_000m, pastDue = 0m, payoffAmount = 5_000_000m, advance = 0m,
                        creditBalance = 0m, admittedModalities = new[] { 1, 3, 5 }, blockReasons = Array.Empty<string>(), hasPaymentAgreement = false,
                        validationFailed = false,
                    },
                },
            },
        });

        var r = await _dobles.ObligacionesAsync(Persona);

        r.IsSuccess.Should().BeTrue();
        var obligacion = r.Value!.Value!.Obligations.Should().ContainSingle().Subject;
        TextosDePagos.Rol(obligacion.Role).Should().Be("Deudor");
        TextosDePagos.EstadoDeObligacion(obligacion.Status).Should().Be("Vigente");
        obligacion.AdmittedModalities.Select(TextosDePagos.Modalidad).Should().Equal("Cancelación total", "Cuota del ciclo", "Abono extraordinario a capital");
        TextosDePagos.Resultado(r.Value.Result).Should().Be("Atendida");
    }

    [Fact]
    public void Nomina_y_la_recogida_no_se_reversan_desde_Cartera()
    {
        TextosDePagos.QuienReversa("Payroll").Should().StartWith("Sólo Nómina lo reversa");
        TextosDePagos.QuienReversa("OwnLoanPayoff").Should().NotBeNull();
        TextosDePagos.QuienReversa("Contributions").Should().NotBeNull();
        TextosDePagos.QuienReversa("Receipts").Should().BeNull();
        TextosDePagos.QuienReversa("Test").Should().BeNull();
        DoblesDePruebaClient.ClaveNueva().Should().MatchRegex("^TST:[0-9a-f]{32}$");
    }
}
