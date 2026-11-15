using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.CanalDigital;

/// <summary>
/// Feature 021, T153 (FR-044 a FR-046, FR-052): por HTTP contra Cartera y Recaudo reales, el aviso de un pago aprobado entra como recibo «en línea» y se
/// aplica al crédito; repetirlo da lo mismo; lo que excede queda a favor y el aviso sale en revisión; el reverso después del corte anula el recibo y
/// devuelve el dinero por Tesorería. En PostgreSQL y SQL Server (<c>DB_PROVIDER</c>).
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class AvisoDePagoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_aviso_se_aplica_una_vez_y_el_reverso_lo_devuelve_por_Tesoreria()
    {
        using var http = fx.CreateClient();
        var e = await CanalE2E.PreparadoAsync(fx, http, "avisouno");
        var referencia = CanalE2E.Referencia(1, e.Hoy);

        var r = await CanalE2E.AvisarAsync(fx, http, e, CanalE2E.Aviso(e, referencia, e.Caja.CreditoA, 50_000m));
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var aplicado = await RecaudoYTesoreriaE2E.LeerAsync(r);
        aplicado.GetProperty("receiptPublicId").GetGuid().Should().NotBeEmpty();
        var recibo = aplicado.GetProperty("receiptPublicId").GetGuid();

        var repetido = await RecaudoYTesoreriaE2E.LeerAsync(await CanalE2E.AvisarAsync(fx, http, e, CanalE2E.Aviso(e, referencia, e.Caja.CreditoA, 50_000m)));
        repetido.GetProperty("receiptPublicId").GetGuid().Should().Be(recibo, "el aviso es idempotente por la referencia");

        var reverso = await InventarioE2E.ExitoAsync(http, e.Token, HttpMethod.Post, $"/api/channel/payments/{referencia}/reverse",
            new { reason = "Pago doble del asociado" });
        reverso.GetProperty("mode").GetString().Should().Be("Treasury", "el corte del destino es la medianoche");
        reverso.GetProperty("refundOrders").GetArrayLength().Should().BeGreaterThan(0);
        reverso.GetProperty("channelUpdated").GetBoolean().Should().BeTrue("el canal marca el intento como devuelto por Tesorería");
        fx.CanalSatelite.Reversos.Should().Contain((e.Cooperativa, referencia, "treasury"));
        var otraVez = await InventarioE2E.ExitoAsync(http, e.Token, HttpMethod.Post, $"/api/channel/payments/{referencia}/reverse", new { reason = "Otra vez" });
        otraVez.GetProperty("voidingPublicId").GetGuid().Should().Be(reverso.GetProperty("voidingPublicId").GetGuid());
    }

    [Fact]
    public async Task Lo_que_excede_queda_a_favor_y_el_aviso_sale_en_revision()
    {
        using var http = fx.CreateClient();
        var e = await CanalE2E.PreparadoAsync(fx, http, "avisodos");

        var r = await CanalE2E.AvisarAsync(fx, http, e, CanalE2E.Aviso(e, CanalE2E.Referencia(2, e.Hoy), e.Caja.CreditoA, 9_000_000m));

        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var cuerpo = await RecaudoYTesoreriaE2E.LeerAsync(r);
        cuerpo.GetProperty("outcome").GetString().Should().Be("InReview", "nueve millones exceden lo que admite un crédito de dos");
        cuerpo.GetProperty("reason").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Un_total_que_no_cuadra_con_los_conceptos_no_aplica_nada()
    {
        using var http = fx.CreateClient();
        var e = await CanalE2E.PreparadoAsync(fx, http, "avisotres");
        var aviso = CanalE2E.Aviso(e, CanalE2E.Referencia(3, e.Hoy), e.Caja.CreditoA, 50_000m);
        var json = System.Text.Json.JsonSerializer.SerializeToNode(aviso)!;
        json["total"] = 60_000m;

        var r = await CanalE2E.ServicioAsync(fx, http, e.Cooperativa, HttpMethod.Post, $"/people/{e.Persona}/payment-notices", json);

        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(r)).Should().Be("Channel.Notice.TotalMismatch");
    }
}
