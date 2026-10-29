using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Tests.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Recaudo;

/// <summary>
/// Feature 015, fase 47 (T0482): el paso de la caja por el cliente —crear la operación, pedir los ítems, poner los medios, declarar, quien efectúa, preseleccionar,
/// confirmar, abandonar— va a sus rutas y cada escritura lleva su clave; lo que sólo lee no la lleva. <b>Desviación</b>: la tarea pedía probar un «estado de la caja» de
/// Shared con sus atajos, pero ese estado vive en la pantalla (<c>Pages/Recaudo/Caja.razor</c>), no en una clase que se pueda probar sola; se prueba el cliente que la
/// pantalla recorre en sus tres pasos. Prueba escrita, sin correr.
/// </summary>
public class CajaEstadoTests
{
    private static readonly Guid Sesion = Guid.Parse("15151515-0000-0000-0000-00000000000a");
    private static readonly Guid Persona = Guid.Parse("15151515-0000-0000-0000-00000000000b");
    private static readonly Guid Operacion = Guid.Parse("15151515-0000-0000-0000-00000000000c");
    private static readonly Guid Medio = Guid.Parse("15151515-0000-0000-0000-00000000000d");

    private readonly ServidorDeCartera _servidor = new();
    private readonly CajaClient _caja;

    public CajaEstadoTests()
    {
        var (http, auth) = _servidor.Sesion();
        _caja = new CajaClient(http, auth);
    }

    public static IEnumerable<object[]> Pasos()
    {
        yield return P("crear", "POST", "/api/receipts/cash-operations", (c, k) =>
            c.CrearAsync(new CrearOperacionRequest(Sesion, Persona, null, null, null, null, null), k));
        yield return P("ítems", "PUT", $"/api/receipts/cash-operations/{Operacion}/items", (c, k) =>
            c.ItemsAsync(Operacion, [new ItemPedidoRequest(1, 100_000m)], k));
        yield return P("medios", "PUT", $"/api/receipts/cash-operations/{Operacion}/means", (c, k) =>
            c.MediosAsync(Operacion, [new MedioDeCajaRequest(Medio, 100_000m)], null, k));
        yield return P("quien efectúa", "PUT", $"/api/receipts/cash-operations/{Operacion}/delivered-by", (c, k) => c.QuienEfectuaAsync(Operacion, Persona, k));
        yield return P("preseleccionar", "POST", $"/api/receipts/cash-operations/{Operacion}/preselect?includeCoDebtor=false", (c, k) =>
            c.PreseleccionarAsync(Operacion, false, k));
        yield return P("confirmar", "POST", $"/api/receipts/cash-operations/{Operacion}/confirm", (c, k) => c.ConfirmarAsync(Operacion, k));
        yield return P("abandonar", "POST", $"/api/receipts/cash-operations/{Operacion}/abandon", (c, k) => c.AbandonarAsync(Operacion, k));
    }

    private static object[] P(string nombre, string metodo, string ruta, Func<CajaClient, ClaveDeOperacion, Task> accion) => [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Pasos))]
    public async Task Cada_paso_va_a_su_ruta_con_su_clave_y_sin_Authorization(string nombre, string metodo, string ruta, Func<CajaClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_caja, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo, nombre);
        vista.Ruta.Should().Be(ruta, nombre);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Lo_que_puede_pagar_y_los_recibos_se_leen_sin_clave()
    {
        await _caja.LoQuePuedePagarAsync(Persona, null);
        await _caja.RecibosAsync(null, null, null);
        await _caja.ReciboAsync(Operacion);

        _servidor.Vistas.Should().HaveCount(3).And.OnlyContain(v => v.Clave == null && v.Metodo == "GET");
    }
}
