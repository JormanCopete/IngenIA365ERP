using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, segunda ronda del 2026-10-27 (D-l, T0503; FR-234): el retiro de un firmante registra la fecha desde la que no firma; lo aprueba otra persona y la vigencia se
/// cierra la víspera de esa fecha. Un lote de una cuenta sin firmantes informa que no exige combinación de firmas.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class FirmanteRetiradoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_firmante_no_firma_desde_su_fecha_de_retiro_aprobada_por_otra_persona()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "firmanteretiro");
        var esc = t.Escenario;
        await TesoreriaE2E.AprobadorDeTesoreriaAsync(http, esc);
        var jefe = esc.Usuarios.Jefe.Token;
        var hoy = esc.Hoy;

        var alta = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/bank-accounts/{t.CuentaDePagos}/signers", new
        {
            personPublicId = t.Beneficiario, signatureClass = "A", maxAmountAlone = 5_000_000m, canSignJointly = true, role = "Gerente",
            minutesAttachmentPublicId = Guid.NewGuid(), validFrom = hoy,
        });
        var firmante = alta.GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"/api/treasury/bank-accounts/{t.CuentaDePagos}/signers/{firmante}/approve");

        var antesDelAlta = await InventarioE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/bank-accounts/{t.CuentaDePagos}/signers/{firmante}/retire",
            new { reason = "Renunció (e2e)", minutesAttachmentPublicId = Guid.NewGuid(), retiredOn = hoy.AddDays(-1) });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(antesDelAlta)).Should().Be("Treasury.Signer.RetiredBeforeValidFrom");

        var pedido = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/bank-accounts/{t.CuentaDePagos}/signers/{firmante}/retire",
            new { reason = "Renunció (e2e)", minutesAttachmentPublicId = Guid.NewGuid(), retiredOn = hoy.AddDays(3) });
        pedido.GetProperty("retiredOn").GetString().Should().Be(hoy.AddDays(3).ToString("yyyy-MM-dd"));
        pedido.GetProperty("status").GetInt32().Should().Be((int)BankAccountAuthorityState.PendingRetirement);

        var autoaprobado = await InventarioE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/bank-accounts/{t.CuentaDePagos}/signers/{firmante}/approve");
        autoaprobado.IsSuccessStatusCode.Should().BeFalse("quien pidió el retiro no lo aprueba");
        await InventarioE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"/api/treasury/bank-accounts/{t.CuentaDePagos}/signers/{firmante}/approve");

        var lista = await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/bank-accounts/{t.CuentaDePagos}/signers?includeRetired=true");
        var fila = lista.EnumerateArray().Single(x => x.GetProperty("publicId").GetGuid() == firmante);
        fila.GetProperty("status").GetInt32().Should().Be((int)BankAccountAuthorityState.Retired);
        fila.GetProperty("validTo").GetString().Should().Be(hoy.AddDays(2).ToString("yyyy-MM-dd"), "la vigencia se cierra la víspera de la fecha de retiro");
    }
}
