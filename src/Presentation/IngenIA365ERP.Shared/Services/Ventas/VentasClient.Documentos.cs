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

    // ------------------------------------------------------------------------- facturas y notas --

    /// <summary>Guarda el borrador de una factura o comprobante de oficina: sin <paramref name="id"/> lo crea; con él lo reemplaza.</summary>
    public Task<ResultadoDeInventario<DocumentoDeVentaDto>> GuardarFacturaAsync(Guid? id, BorradorDeVentaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        id is { } existente
            ? Enviar<DocumentoDeVentaDto>(HttpMethod.Put, $"{Rutas.Facturas}/{existente}", request, clave, ct)
            : Enviar<DocumentoDeVentaDto>(HttpMethod.Post, Rutas.Facturas, request, clave, ct);

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
