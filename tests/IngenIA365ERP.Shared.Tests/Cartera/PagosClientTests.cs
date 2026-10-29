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

    private readonly ServidorDeCartera _servidor = new();
    private readonly PagosClient _pagos;

    public PagosClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _pagos = new PagosClient(http, auth);
    }

    public sealed record Clientes(PagosClient P);

    private Clientes Todos => new(_pagos);

    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("reversar", "POST", $"/api/lending/loans/{Credito}/operations/{Operacion}/reverse", (c, k) =>
            c.P.ReversarAsync(Credito, Operacion, new ReversoDePagoRequest("Cheque devuelto", "sha256:abc"), k));
        yield return E("preferencia", "POST", $"/api/lending/loans/{Credito}/prepayment-preference", (c, k) =>
            c.P.CambiarPreferenciaAsync(Credito, new CambioDePreferenciaRequest("ReduceTerm", "Instrucción escrita del deudor"), k));
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

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("POST", $"/api/lending/loans/{Credito}/payment-simulations"),
            ("POST", $"/api/lending/loans/{Credito}/operations/{Operacion}/reversal-preview"),
            ("GET", $"/api/lending/loans/{Credito}/prepayment-preference"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public void Los_enums_del_contrato_se_traducen_desde_su_numero()
    {
        // Feature 015, T0307: hasta el retiro de los dobles se leían por el listado del recaudador de prueba; los textos siguen sirviendo a las pantallas.
        TextosDePagos.Rol(1).Should().Be("Deudor");
        TextosDePagos.EstadoDeObligacion(2).Should().Be("Vigente");
        new[] { 1, 3, 5 }.Select(TextosDePagos.Modalidad).Should().Equal("Cancelación total", "Cuota del ciclo", "Abono extraordinario a capital");
        TextosDePagos.Resultado(4).Should().Be("Atendida");
    }

    [Fact]
    public void Nomina_y_la_recogida_no_se_reversan_desde_Cartera()
    {
        TextosDePagos.QuienReversa("Payroll").Should().StartWith("Sólo Nómina lo reversa");
        TextosDePagos.QuienReversa("OwnLoanPayoff").Should().NotBeNull();
        TextosDePagos.QuienReversa("Contributions").Should().NotBeNull();
        TextosDePagos.QuienReversa("Receipts").Should().BeNull();
        TextosDePagos.QuienReversa("Test").Should().BeNull();
    }
}
