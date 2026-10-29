using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US2-9, T0237 (quickstart recorrido K; FR-186): la contingencia de la caja sobre Cartera real. Se registra el talonario prenumerado del punto (1 a 50);
/// durante la caída el cajero emitió a mano el recibo 0001; al volver se digita con su número, su fecha —hoy, dentro de la ventana del contrato: los créditos del
/// escenario se desembolsan hoy— y la imagen del papel. El recibo sale por el canal de contingencia, el estado del talonario y el informe «Contingencia por digitar» lo
/// muestran digitado con los 49 números sin usar, el mismo número no se digita dos veces y sin imagen no hay recibo. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ContingenciaDeCajaE2ETests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_recibo_del_talonario_se_digita_con_su_fecha_e_imagen_y_el_informe_lo_muestra()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "contingencia");
        var t = c.Token;
        var hoy = c.Escenario.Hoy;

        var talonario = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/receipts/contingency-books",
            new { collectionPointPublicId = c.Punto, fromNumber = 1, toNumber = 50 });
        var libro = talonario.GetProperty("publicId").GetGuid();

        var imagen = await AdjuntosE2E.SubirAsync(http, t, libro, Encoding.UTF8.GetBytes("%PDF-1.4 recibo 0001 del talonario (e2e)"), "recibo-0001.pdf",
            "application/pdf", "ReceiptsContingencyImage");
        imagen.EstadoDelAlmacen.Should().Be(System.Net.HttpStatusCode.NoContent, "la imagen del papel llega al almacén");

        object Digitar(int numero, Guid imagenDelPapel) => new
        {
            contingencyBookPublicId = libro, contingencyNumber = numero, payerPersonPublicId = c.Persona, valueDate = hoy,
            parts = new[] { new { target = "Loan", beneficiaryPersonPublicId = c.Persona, amount = 30_000m, productPublicId = c.CreditoA } },
            imageAttachmentPublicId = imagenDelPapel, meansClass = "Cash",
        };

        var sinImagen = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/contingency-receipts", Digitar(1, Guid.NewGuid()));
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinImagen)).Should().Be("Receipts.Contingency.ImageRequired");

        var recibo = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/receipts/contingency-receipts", Digitar(1, imagen.Adjunto));
        RecaudoYTesoreriaE2E.Enum<ReceiptChannel>(recibo.GetProperty("channel")).Should().Be(ReceiptChannel.Contingency);
        recibo.GetProperty("number").GetString().Should().NotBeNullOrWhiteSpace("el recibo digitado lleva también el consecutivo");
        recibo.GetProperty("valueDate").GetString().Should().Be(hoy.ToString("yyyy-MM-dd"));
        recibo.GetProperty("totalApplied").GetDecimal().Should().Be(30_000m);

        var repetido = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/contingency-receipts", Digitar(1, imagen.Adjunto));
        ((int)repetido.StatusCode).Should().Be(422, await repetido.Content.ReadAsStringAsync());

        var estado = await InventarioE2E.GetAsync(http, t, $"/api/receipts/contingency-books/{libro}/status");
        estado.GetProperty("typed").EnumerateArray().Select(n => n.GetInt32()).Should().Equal(1);
        estado.GetProperty("pending").GetArrayLength().Should().Be(0);
        estado.GetProperty("unused").GetInt32().Should().Be(49);

        var informe = new ContabilidadE2E.Tabla(await InventarioE2E.GetAsync(http, t, $"/api/reports/receipts/contingency-pending?point={c.Punto}"));
        var fila = informe.Filas.Where(f => informe.Oculta(f, "_talonario") == libro.ToString()).ToList();
        fila.Should().ContainSingle("el informe de contingencia trae el talonario");
        informe.Numero(fila[0], "Digitados").Should().Be(1m);
        informe.Numero(fila[0], "Sin usar").Should().Be(49m);
    }
}
