using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de los recibos de lo que llega a Cartera (feature 013, US6, T0601; contracts/api.md §16.2): la lista con sus evaluaciones y la
/// respuesta enviada, el detalle con el contenido del mensaje y marcar no reprocesable un recibo rechazado (con motivo y su
/// <see cref="ClaveDeOperacion"/>). Sin <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed class MensajesRecibidosClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeRecibos = GobiernoDeCarteraClient.Base + "/inbound-messages";

    /// <summary>Los recibos por estado (<c>Processed</c>, <c>Rejected</c>, <c>ValidationFailed</c>…), tipo y fechas de recepción.</summary>
    public Task<ResultadoDeInventario<PaginaDeCarteraDto<MensajeRecibidoDto>>> ListarAsync(string? estado = null, string? tipo = null, DateOnly? desde = null,
        DateOnly? hasta = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<MensajeRecibidoDto>>(HttpMethod.Get, ConQuery(RutaDeRecibos, Query(
            ("status", estado), ("type", tipo), ("from", Texto(desde)), ("to", Texto(hasta)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))),
            null, null, ct);

    public Task<ResultadoDeInventario<MensajeRecibidoDetalleDto>> DetalleAsync(Guid mensaje, CancellationToken ct = default) =>
        Enviar<MensajeRecibidoDetalleDto>(HttpMethod.Get, $"{RutaDeRecibos}/{mensaje}", null, null, ct);

    /// <summary>
    /// Marca no reprocesable un recibo rechazado: Cartera emite en la misma transacción la <c>ObligacionRechazada</c> definitiva, que Comercial
    /// muestra en la venta.
    /// </summary>
    public Task<ResultadoDeInventario<MensajeMarcadoDto>> MarcarNoReprocesableAsync(Guid mensaje, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<MensajeMarcadoDto>(HttpMethod.Post, $"{RutaDeRecibos}/{mensaje}/mark-not-retryable", new MotivoDeCarteraRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
