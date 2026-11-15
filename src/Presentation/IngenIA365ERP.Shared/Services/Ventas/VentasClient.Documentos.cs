using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
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
            ("page", Texto(f.Page)), ("pageSize", Texto(f.PageSize)),
            ("validity", string.IsNullOrWhiteSpace(f.Validity) ? null : f.Validity), ("invoicing", string.IsNullOrWhiteSpace(f.Invoicing) ? null : f.Invoicing)),
            null, null, ct);

    // ------------------------------------------------------------------------------- la cotización como base (2026-11-12) --

    /// <summary>
    /// «Cargar cotización» en la factura de oficina (<c>POST /sales/quotes/{id}/to-invoice</c>): copia cliente, vendedor, notas y líneas de una
    /// cotización confirmada al borrador <paramref name="borrador"/> (las agrega al final) o, sin él, a una factura nueva del
    /// <paramref name="tipo"/> y la <paramref name="bodega"/> dados. Vigente, respeta sus precios y descuentos; vencida, toma los del día y la
    /// respuesta lo dice (<see cref="CotizacionCargadaDto.Repriced"/>).
    /// </summary>
    public Task<ResultadoDeInventario<CotizacionCargadaDto>> CargarCotizacionEnFacturaAsync(Guid cotizacion, Guid? borrador, Guid? tipo, Guid? bodega,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CotizacionCargadaDto>(HttpMethod.Post, $"{Rutas.Cotizaciones}/{cotizacion}/to-invoice", new CargarCotizacionRequest(borrador, tipo, bodega), clave, ct);

    /// <summary>
    /// «Cargar cotización» en la venta del punto de venta (<c>POST /sales/quotes/{id}/to-pos</c>), para la pantalla del POS: agrega las líneas
    /// de una cotización confirmada a la venta en borrador <paramref name="borrador"/> (de la sesión abierta del usuario) o, sin ella, abre una
    /// venta nueva en <paramref name="sesionDeCaja"/>. Después se relee la venta con <c>GET /pos/drafts/{id}</c>
    /// (<see cref="CotizacionCargadaDto.DraftPublicId"/>). Las mismas reglas de vigencia que en la factura.
    /// </summary>
    public Task<ResultadoDeInventario<CotizacionCargadaDto>> CargarCotizacionEnBorradorAsync(Guid cotizacion, Guid? borrador, Guid? sesionDeCaja,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CotizacionCargadaDto>(HttpMethod.Post, $"{Rutas.Cotizaciones}/{cotizacion}/to-pos",
            new CargarCotizacionRequest(borrador, CashSessionPublicId: sesionDeCaja), clave, ct);

    /// <summary>La cotización en PDF para el cliente (<c>GET /sales/quotes/{id}/pdf</c>): para verla o imprimirla.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> CotizacionEnPdfAsync(Guid cotizacion, CancellationToken ct = default) =>
        Descargar($"{Rutas.Cotizaciones}/{cotizacion}/pdf", $"cotizacion-{cotizacion:N}.pdf", ct);

    /// <summary>Envía la cotización confirmada al cliente en PDF (<c>POST /sales/quotes/{id}/send</c>, <c>Inventory.Sales.SendQuote</c>).</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> EnviarCotizacionAsync(Guid cotizacion, string? correo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{Rutas.Cotizaciones}/{cotizacion}/send",
            new EnviarCotizacionRequest(string.IsNullOrWhiteSpace(correo) ? null : correo.Trim()), clave, ct);

    /// <summary>Los conceptos de corrección de la DIAN de una nota («Debit» o «Credit»), vigentes a hoy (<c>GET /sales/correction-concepts</c>).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ConceptoDeCorreccionDto>>> ConceptosDeCorreccionAsync(string nota, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ConceptoDeCorreccionDto>>(HttpMethod.Get, Q(Rutas.ConceptosDeCorreccion, ("note", nota)), null, null, ct);

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

    /// <summary>
    /// Decisión del dueño del 2026-11-12: pide la aprobación de una venta a crédito con mora (política «Aprobacion»). El supervisor la decide en
    /// persona con <see cref="DesafioDePresenciaAsync"/> y <see cref="DecidirEnPersonaAsync"/>.
    /// </summary>
    public Task<ResultadoDeInventario<AprobacionDeMoraDto>> PedirAprobacionDeMoraAsync(AprobacionDeMoraRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<AprobacionDeMoraDto>(HttpMethod.Post, Rutas.AprobacionesDeMora, request, clave, ct);

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
