using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, US10, T0133 (SC-028; FR-207): un lote desde la cuenta del fondo de liquidez se niega sin un uso aprobado; con el uso pedido y aprobado por otra persona
/// por el monto, se arma. Se corre en la fase 22.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class FondoDeLiquidezRestringidoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Sin_uso_aprobado_no_hay_lote_desde_el_fondo()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "fondo");
        var fondo = await TesoreriaE2E.CuentaPropiaAsync(http, t.Admin, t.Escenario, "FONDOLQ", 16, null);
        var orden = await TesoreriaE2E.OrdenDeDesembolsoAsync(fx, http, t, 1_500_000m);

        var sinUso = await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/payment-batches", new
        {
            bankAccountPublicId = fondo, form = 1, payDate = DateOnly.FromDateTime(DateTime.Today), orderPublicIds = new[] { orden.GetProperty("publicId").GetGuid() },
        });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinUso)).Should().BeOneOf("Treasury.Batch.SourceIsLiquidityFund", "Treasury.BankAccount.NotLinked");

        var uso = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/liquidity-fund-uses", new
        {
            bankAccountPublicId = fondo, cause = 1, amount = 2_000_000m, reason = "atender retiros", supportAttachmentPublicId = Guid.NewGuid(),
        });
        uso.GetProperty("status").GetInt32().Should().Be(1);
        var propia = await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/liquidity-fund-uses/{uso.GetProperty("publicId").GetGuid()}/approve", new { });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(propia)).Should().Be("Treasury.LiquidityFund.SelfApproval");
        var aprobado = await InventarioE2E.ExitoAsync(http, t.Escenario.Usuarios.Jefe.Token, HttpMethod.Post,
            $"/api/treasury/liquidity-fund-uses/{uso.GetProperty("publicId").GetGuid()}/approve", new { });
        aprobado.GetProperty("status").GetInt32().Should().Be(2);
    }
}
