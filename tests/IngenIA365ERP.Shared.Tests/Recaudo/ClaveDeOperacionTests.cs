using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Tests.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Recaudo;

/// <summary>
/// Feature 015, fase 47 (T0482; contracts/api.md §2.3; FR-170): la confirmación de una operación de caja se repite con <b>la misma</b> clave de operación tras
/// <c>DistributionChanged</c> o tras una aprobación presente (con <c>X-Approval-Request</c>), así el servidor ejecuta el recibo una sola vez; sólo un éxito renueva la
/// clave para la operación siguiente. Ninguna petición pone <c>Authorization</c>. Prueba escrita, sin correr.
/// </summary>
public class ClaveDeOperacionTests
{
    private static readonly Guid Operacion = Guid.Parse("15151515-0000-0000-0000-000000000001");
    private static readonly Guid Aprobacion = Guid.Parse("15151515-0000-0000-0000-000000000002");

    private readonly ServidorDeCartera _servidor = new();
    private readonly CajaClient _caja;
    private readonly List<string?> _aprobaciones = [];

    public ClaveDeOperacionTests()
    {
        var (http, auth) = _servidor.Sesion();
        _caja = new CajaClient(http, auth);
    }

    private void Responder(params HttpStatusCode[] estados)
    {
        var cola = new Queue<HttpStatusCode>(estados);
        _servidor.Responder = req =>
        {
            _aprobaciones.Add(req.Headers.TryGetValues(SesionesClient.CabeceraDeAprobacion, out var v) ? v.Single() : null);
            var estado = cola.Count > 0 ? cola.Dequeue() : HttpStatusCode.OK;
            return estado switch
            {
                HttpStatusCode.Conflict => ServidorDeCartera.Json(estado, new { code = "Receipts.Operation.DistributionChanged", message = "Cambió la distribución.", traceId = "t" }),
                HttpStatusCode.PreconditionRequired => ServidorDeCartera.Json(estado, new { code = "Approvals.Required", message = "Requiere aprobación.", traceId = "t" }),
                _ => ServidorDeCartera.Json(estado, new { publicId = Operacion, number = "RC-1", status = 2, channel = 1, valueDate = "2026-12-01" }),
            };
        };
    }

    [Fact]
    public async Task Tras_DistributionChanged_la_reconfirmacion_lleva_la_misma_clave()
    {
        Responder(HttpStatusCode.Conflict, HttpStatusCode.OK);
        var clave = new ClaveDeOperacion();

        (await _caja.ConfirmarAsync(Operacion, clave)).IsSuccess.Should().BeFalse();
        (await _caja.ConfirmarAsync(Operacion, clave)).IsSuccess.Should().BeTrue();

        _servidor.Vistas.Should().HaveCount(2);
        _servidor.Vistas[1].Clave.Should().Be(_servidor.Vistas[0].Clave, "el servidor tiene que reconocer la misma operación");
        _servidor.Vistas.Should().OnlyContain(v => v.Authorization == null);
    }

    [Fact]
    public async Task Tras_la_aprobacion_presente_repite_con_la_misma_clave_y_la_solicitud()
    {
        Responder(HttpStatusCode.PreconditionRequired, HttpStatusCode.OK);
        var clave = new ClaveDeOperacion();

        await _caja.ConfirmarAsync(Operacion, clave);
        var r = await _caja.ConfirmarAsync(Operacion, clave, Aprobacion);

        r.IsSuccess.Should().BeTrue();
        _servidor.Vistas[1].Clave.Should().Be(_servidor.Vistas[0].Clave);
        _aprobaciones.Should().Equal(null, Aprobacion.ToString());
    }

    [Fact]
    public async Task Solo_un_exito_renueva_la_clave()
    {
        Responder(HttpStatusCode.OK, HttpStatusCode.OK);
        var clave = new ClaveDeOperacion();
        var antes = clave.Valor;

        await _caja.ConfirmarAsync(Operacion, clave);

        clave.Valor.Should().NotBe(antes, "la siguiente es otra operación");
        _servidor.Vistas[0].Clave.Should().Be(antes.ToString());
    }
}
