using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de la recogida de créditos y productos con el desembolso de un crédito nuevo (2026-11-27, pedido del dueño; feature 028, HU1, T018;
/// <c>/api/lending/payoffs</c>): los productos del deudor para recoger a la fecha proyectada de desembolso con el resumen de lo marcado, y la misma
/// simulación en PDF. Las dos son consultas por POST: sin clave de operación. Sin <c>Authorization</c> a mano: la pone el handler de la sesión. (nuevo)
/// </summary>
public sealed class RecogidasClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = GobiernoDeCarteraClient.Base + "/payoffs";

    /// <summary>Los productos del deudor liquidados a la fecha proyectada, si se pueden recoger y el resumen de lo marcado. No escribe.</summary>
    public Task<ResultadoDeInventario<ProductosParaRecogerDto>> ProductosAsync(ProductosParaRecogerRequest pedido, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<ProductosParaRecogerDto>(http, auth, HttpMethod.Post, $"{Ruta}/candidates", pedido, null, ct);

    /// <summary>La simulación en PDF con el membrete (POST: lleva lo marcado).</summary>
    public async Task<InvitationApiResult<ArchivoDescargado>> SimulacionEnPdfAsync(ProductosParaRecogerRequest pedido, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var resp = await http.PostAsJsonAsync($"{Ruta}/candidates/pdf", pedido, ct);
            if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            return InvitationApiResult<ArchivoDescargado>.Success(await EnvioDeVentas.ArchivoAsync(resp, "simulacion-de-recogida.pdf", ct));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }
}
