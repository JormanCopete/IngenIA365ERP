using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, T2, T0453 (US18 «Independent Test»; FR-124 a FR-127): por HTTP, la consulta de cuentas por pagar y el calendario de pagos responden con la cooperativa
/// preparada; una cuenta por pagar contable que Contabilidad no tiene no se convierte (<c>Treasury.Payable.NotFound</c>). El recorrido
/// completo de la factura de Comercial (confirmar a crédito → orden en la bandeja → lote → giro → <c>PagoDeFacturaDeProveedor</c> a Comercial) se arma en la corrida de cierre
/// de T2 sobre la cooperativa con Comercial activado. Escrita para la corrida de cierre de T2; sin correr. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class PagosAProveedoresTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Cuentas_por_pagar_calendario_y_conversion_que_no_procede()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "cxpagar");

        var cuentas = await InventarioE2E.GetAsync(http, t.Admin, "/api/treasury/payables");
        cuentas.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        var calendario = await InventarioE2E.GetAsync(http, t.Admin, "/api/treasury/payables/calendar");
        calendario.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);

        var inexistente = await CarteraE2E.ConClaveAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/payables/orders",
            new { personPublicId = t.Beneficiario, crossDocument = "CXP-NO-EXISTE", amount = 1000m });
        inexistente.StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(inexistente)).Should().Be("Treasury.Payable.NotFound");
    }
}
