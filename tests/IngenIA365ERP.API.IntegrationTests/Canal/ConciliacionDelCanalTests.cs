using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.CanalDigital;

/// <summary>
/// Feature 021, T154 (FR-050, FR-051; research R4): el reporte de la pasarela y el aviso del canal, en los dos órdenes. Aviso primero: la línea del
/// archivo sale aplicada con el mismo recibo, sin otro. Archivo primero, con un canal que no tiene el aviso: la línea queda como partida con su motivo y el aviso
/// que llega después se aplica en línea (el ERP no aplica a ciegas lo que no puede verificar).
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ConciliacionDelCanalTests(CentralIdentityApiFixture fx)
{
    private static async Task<HttpResponseMessage> CargarAsync(HttpClient http, string token, Guid convenio, string csv)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/receipts/collection-agreements/{convenio}/files");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var multipart = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(bytes, "archivo", "PASARELA.csv");
        req.Content = multipart;
        return await http.SendAsync(req);
    }

    [Fact]
    public async Task En_los_dos_ordenes_nada_se_aplica_dos_veces()
    {
        using var http = fx.CreateClient();
        var e = await CanalE2E.PreparadoAsync(fx, http, "concilia");

        // Aviso primero, después el archivo.
        var primera = CanalE2E.Referencia(11, e.Hoy);
        var aviso = await RecaudoYTesoreriaE2E.LeerAsync(await CanalE2E.AvisarAsync(fx, http, e, CanalE2E.Aviso(e, primera, e.Caja.CreditoA, 40_000m)));
        var recibo = aviso.GetProperty("receiptPublicId").GetGuid();

        // Archivo primero (otra referencia), después el aviso.
        var segunda = CanalE2E.Referencia(12, e.Hoy);
        var carga = await CargarAsync(http, e.Token, e.Convenio,
            $"{e.Hoy:yyyy-MM-dd};40000.00;{primera};S1;APROBADA\r\n{e.Hoy:yyyy-MM-dd};30000.00;{segunda};S2;APROBADA");
        carga.StatusCode.Should().Be(HttpStatusCode.OK, await carga.Content.ReadAsStringAsync());
        var archivo = (await RecaudoYTesoreriaE2E.LeerAsync(carga)).GetProperty("file");
        archivo.GetProperty("applied").GetInt32().Should().Be(1, archivo.GetRawText());
        archivo.GetProperty("unidentified").GetInt32().Should().Be(1, "sin aviso, la segunda queda como partida");

        var tardio = await RecaudoYTesoreriaE2E.LeerAsync(await CanalE2E.AvisarAsync(fx, http, e, CanalE2E.Aviso(e, segunda, e.Caja.CreditoA, 30_000m)));
        tardio.GetProperty("receiptPublicId").GetGuid().Should().NotBe(recibo);

        var otraVez = await RecaudoYTesoreriaE2E.LeerAsync(await CanalE2E.AvisarAsync(fx, http, e, CanalE2E.Aviso(e, primera, e.Caja.CreditoA, 40_000m)));
        otraVez.GetProperty("receiptPublicId").GetGuid().Should().Be(recibo, "el aviso repetido después del archivo no aplica otra vez");
    }
}
