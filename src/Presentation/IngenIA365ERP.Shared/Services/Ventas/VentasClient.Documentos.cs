using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// Las ventas de oficina (contracts/api.md §18.1–§18.3, §20.3): la consulta de documentos de venta, la entrega y la reimpresión, y el
/// ciclo de las facturas y notas (<see cref="Rutas.Facturas"/>, <see cref="Rutas.Notas"/>). (nuevo)
/// </summary>
public sealed partial class VentasClient
{
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeVentaDto>>> ListarDocumentosAsync(FiltroDeVentas f, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ResumenDeVentaDto>>(HttpMethod.Get, Q(Rutas.Documentos,
            ("class", Texto(f.Class)), ("documentType", Texto(f.DocumentType)), ("status", Texto(f.Status)), ("from", Texto(f.From)), ("to", Texto(f.To)),
            ("pointOfSale", Texto(f.PointOfSale)), ("cashRegister", Texto(f.CashRegister)), ("cashSession", Texto(f.CashSession)), ("person", Texto(f.Person)),
            ("salesperson", Texto(f.Salesperson)), ("number", f.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("pendingDelivery", Texto(f.PendingDelivery)), ("pendingValidation", Texto(f.PendingValidation)),
            ("page", Texto(f.Page)), ("pageSize", Texto(f.PageSize))), null, null, ct);

    public Task<ResultadoDeInventario<DocumentoDeVentaDto>> ObtenerDocumentoAsync(Guid id, CancellationToken ct = default) =>
        Enviar<DocumentoDeVentaDto>(HttpMethod.Get, $"{Rutas.Documentos}/{id}", null, null, ct);

    /// <summary>La primera entrega, sin «COPIA»: la tirilla vuelve como modelo y la carta como PDF (§20.3).</summary>
    public Task<ResultadoDeInventario<EntregaDeVentaDto>> EntregarAsync(Guid id, EntregaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EntregaAsync($"{Rutas.Documentos}/{id}/deliver", id, request, clave, ct);

    /// <summary>La reimpresión con la marca «COPIA», auditada (<c>Inventory.Documents.Reprint</c>).</summary>
    public Task<ResultadoDeInventario<EntregaDeVentaDto>> ReimprimirAsync(Guid id, EntregaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EntregaAsync($"{Rutas.Reimpresion}/{id}/reprint", id, request, clave, ct);

    /// <summary>
    /// I4 (T758; §18.3.1): el comprador pide factura sobre un documento equivalente POS ya expedido. Una sola escritura: la nota de ajuste de
    /// anulación total y la factura con los mismos pagos, en una transacción; la factura se transmite después de la nota.
    /// </summary>
    public Task<ResultadoDeInventario<FacturaEnLugarDelDocumentoEquivalenteDto>> PedirFacturaAsync(Guid id, FacturaEnLugarRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<FacturaEnLugarDelDocumentoEquivalenteDto>(HttpMethod.Post, $"{Rutas.Documentos}/{id}/invoice-instead", request, clave, ct);

    // ------------------------------------------------------------------------- facturas y notas --

    /// <summary>
    /// Guarda el borrador de una factura o comprobante de oficina —también la factura desde un pedido o desde remisiones, con
    /// <c>originPublicIds</c> (I6)—: sin <paramref name="id"/> lo crea; con él lo reemplaza.
    /// </summary>
    public Task<ResultadoDeInventario<DocumentoDeVentaDto>> GuardarFacturaAsync(Guid? id, BorradorDeVentaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        GuardarBorradorAsync(Rutas.Facturas, id, request, clave, ct);

    /// <summary>
    /// I6 (T893; §18.4): guarda el borrador por la ruta de su clase —<see cref="Rutas.Cotizaciones"/>, <see cref="Rutas.Pedidos"/>,
    /// <see cref="Rutas.Remisiones"/>, <see cref="Rutas.NotasDebito"/> o <see cref="Rutas.Facturas"/>—; otra clase por esa ruta responde
    /// <c>Inventory.Document.TypeNotForRoute</c>.
    /// </summary>
    public Task<ResultadoDeInventario<DocumentoDeVentaDto>> GuardarBorradorAsync(string ruta, Guid? id, BorradorDeVentaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        id is { } existente
            ? Enviar<DocumentoDeVentaDto>(HttpMethod.Put, $"{ruta}/{existente}", request, clave, ct)
            : Enviar<DocumentoDeVentaDto>(HttpMethod.Post, ruta, request, clave, ct);

    /// <summary>I6 (§18.4): «Convertir en pedido» crea el borrador del pedido de una cotización confirmada y vigente.</summary>
    public Task<ResultadoDeInventario<DocumentoDeVentaDto>> ConvertirEnPedidoAsync(Guid cotizacion, Guid? tipoDelPedido, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DocumentoDeVentaDto>(HttpMethod.Post, $"{Rutas.Cotizaciones}/{cotizacion}/to-order", new ConvertirEnPedidoRequest(tipoDelPedido), clave, ct);

    public Task<ResultadoDeInventario<DocumentoDeVentaDto>> GuardarNotaAsync(Guid? id, BorradorDeNotaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        id is { } existente
            ? Enviar<DocumentoDeVentaDto>(HttpMethod.Put, $"{Rutas.Notas}/{existente}", request, clave, ct)
            : Enviar<DocumentoDeVentaDto>(HttpMethod.Post, Rutas.Notas, request, clave, ct);

    /// <summary>
    /// Confirmar una factura o una nota (<paramref name="ruta"/> es <see cref="Rutas.Facturas"/> o <see cref="Rutas.Notas"/>) con lo que la
    /// persona vio a pagar: si el calculado cambió, 422 <c>Inventory.Document.TotalChanged</c>.
    /// </summary>
    public Task<ResultadoDeInventario<ResultadoDeConfirmacionDto>> ConfirmarAsync(string ruta, Guid id, decimal? totalVisto, byte[]? rowVersion, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeConfirmacionDto>(HttpMethod.Post, $"{ruta}/{id}/confirm", new ConfirmarVentaRequest(totalVisto, rowVersion), clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeAnulacionDto>> AnularAsync(string ruta, Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ResultadoDeAnulacionDto>(HttpMethod.Post, $"{ruta}/{id}/void", new AnularVentaRequest(motivo, null, null), clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> DescartarAsync(string ruta, Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{ruta}/{id}/discard", new MotivoRequest(motivo), clave, ct);

    // ------------------------------------------------------------------------------------ crédito --

    /// <summary>
    /// Evaluar una venta a crédito al elegir un medio de crédito (§23.1): elegibilidad, condiciones del medio y la aprobación que exigirá.
    /// Es una consulta: no lleva <c>Idempotency-Key</c>. El servidor repite la evaluación al confirmar.
    /// </summary>
    public Task<ResultadoDeInventario<EvaluacionDeCreditoDto>> EvaluarCreditoAsync(EvaluacionDeCreditoRequest request, CancellationToken ct = default) =>
        Enviar<EvaluacionDeCreditoDto>(HttpMethod.Post, Rutas.EvaluacionesDeCredito, request, null, ct);

    /// <summary>El crédito de una venta (§23.2): pagos de crédito, aprobación, sello y mensajes a Cartera.</summary>
    public Task<ResultadoDeInventario<CreditoDeLaVentaDto>> ObtenerCreditoAsync(Guid id, CancellationToken ct = default) =>
        Enviar<CreditoDeLaVentaDto>(HttpMethod.Get, $"{Rutas.Documentos}/{id}/credit", null, null, ct);

    // ------------------------------------------------------------------------------------ entrega --

    private async Task<ResultadoDeInventario<EntregaDeVentaDto>> EntregaAsync(string url, Guid id, EntregaRequest request, ClaveDeOperacion clave, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<EntregaDeVentaDto>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(request) };
            clave.Aplicar(req, new { metodo = "POST", url, cuerpo = request });
            using var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode && resp.Content.Headers.ContentType?.MediaType == "application/pdf")
            {
                var archivo = await ArchivoAsync(resp, $"venta-{id:N}.pdf", ct);
                clave.Exito();
                var formato = TextosDeVentas.Numero(TextosDeVentas.NombresDeFormatoDeImpresion, request.Format) ?? 0;
                return new(true, new EntregaDeVentaDto(id, formato, false, null, archivo.Nombre, false) { Archivo = archivo }, null, null, (int)resp.StatusCode, null,
                    ClaveDeOperacion.FueRepeticion(resp));
            }
            var resultado = await ResultadoDeInventario<EntregaDeVentaDto>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave.Exito();
            // I4: la carta de un electrónico es un enlace firmado; el del almacén local de desarrollo es relativo a la API.
            if (resultado is { IsSuccess: true, Value: { Link: { Url: { } firmada } enlace } valor })
                resultado = resultado with { Value = valor with { Link = enlace with { Url = Adjuntos.EnlacesFirmados.Absoluta(http, firmada) } } };
            return resultado;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<EntregaDeVentaDto>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<EntregaDeVentaDto>.FormatoInesperado(ex.Message);
        }
    }
}
