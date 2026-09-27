using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// El cliente del lado contable de la integración con Inventario (feature 012, US7, T532; contracts/api.md §26, enmienda de la
/// 009): la matriz de reglas, el mapeo de tipos de comprobante, la completitud, los lotes de contabilización con su vista previa
/// y su orden, y los recibos. (nuevo)
///
/// <list type="bullet">
/// <item>La matriz y el mapeo siguen la mecánica de la 009: <b>sin</b> clave de operación. Sólo ordenar un lote lleva
/// <c>Idempotency-Key</c> (<see cref="ClaveDeOperacion"/>, que la pantalla crea al abrir la orden y conserva en el reintento).
/// La plantilla 16 de la matriz se revisa y se aplica con el componente único <c>ImportarPlantilla</c> sobre
/// <see cref="RutaDeReglas"/>: esa importación sí lleva su clave.</item>
/// <item>Los filtros de enum viajan por <b>nombre</b> (la API los lee por nombre o por número) y las respuestas los traen como
/// número; las etiquetas las pone <see cref="TextosDeInventario"/>.</item>
/// <item>La cabecera <c>Authorization</c> la pone el handler de la sesión (<c>ElTokenDeSesionLoPoneElHandler</c>).</item>
/// <item>Las respuestas son <see cref="ResultadoDeInventario{T}"/>, que conserva el <c>data</c> del error
/// (<c>Accounting.InventoryBatch.AlreadyRunning</c> trae el lote en curso; <c>Import.Invalid</c>, la revisión).</item>
/// </list>
/// </summary>
public sealed class IntegracionContableClient(HttpClient http, CentralAuthClient auth)
{
    public const string Base = "/api/accounting/inventory";
    public const string RutaDeReglas = Base + "/rules";

    // -------------------------------------------------------------------------------------------- matriz --

    public Task<ResultadoDeInventario<CatalogoDeReglasDeInventarioDto>> CatalogoAsync(CancellationToken ct = default) =>
        EnviarAsync<CatalogoDeReglasDeInventarioDto>(HttpMethod.Get, $"{RutaDeReglas}/catalog", null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ReglaDeInventarioDto>>> ReglasAsync(FiltroDeReglasDeInventario f, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ReglaDeInventarioDto>>(HttpMethod.Get, InventarioClient.ConQuery(RutaDeReglas, InventarioClient.Query(
            ("operation", f.Operacion), ("role", f.Rol), ("asOf", f.ALaFecha?.ToString("yyyy-MM-dd")), ("accountingGroupCode", f.GrupoContable),
            ("warehouseCode", f.Bodega), ("pointOfSaleCode", f.PuntoDeVenta), ("paymentMeansCode", f.MedioDePago), ("taxRateCode", f.Tarifa),
            ("reasonCode", f.Causa), ("account", f.Cuenta), ("onlyCurrent", f.SoloVigentes ? "true" : null),
            ("page", f.Pagina.ToString()), ("pageSize", f.TamanoDePagina.ToString()))), null, null, ct);

    public Task<ResultadoDeInventario<ReglaDeInventarioDto>> ReglaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<ReglaDeInventarioDto>(HttpMethod.Get, $"{RutaDeReglas}/{id}", null, null, ct);

    /// <summary>Las versiones de la misma clave, de la más nueva a la más vieja.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ReglaDeInventarioDto>>> VersionesDeReglaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ReglaDeInventarioDto>>(HttpMethod.Get, $"{RutaDeReglas}/{id}/versions", null, null, ct);

    public Task<ResultadoDeInventario<ReglaGuardadaDeInventarioDto>> CrearReglaAsync(CrearReglaDeInventarioRequest request, CancellationToken ct = default) =>
        EnviarAsync<ReglaGuardadaDeInventarioDto>(HttpMethod.Post, RutaDeReglas, request, null, ct);

    /// <summary>Una versión nueva de la misma clave: cierra la vigente la víspera de <c>ValidFrom</c>.</summary>
    public Task<ResultadoDeInventario<ReglaGuardadaDeInventarioDto>> NuevaVersionDeReglaAsync(Guid id, VersionDeReglaDeInventarioRequest request,
        CancellationToken ct = default) =>
        EnviarAsync<ReglaGuardadaDeInventarioDto>(HttpMethod.Post, $"{RutaDeReglas}/{id}/versions", request, null, ct);

    /// <summary>Cierra la vigencia con motivo; nunca borra.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> DesactivarReglaAsync(Guid id, DesactivarReglaDeInventarioRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{RutaDeReglas}/{id}/deactivate", request, null, ct);

    // ---------------------------------------------------------------------------------- tipos de comprobante --

    public Task<ResultadoDeInventario<IReadOnlyList<MapeoDeComprobanteDeInventarioDto>>> MapeosAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<MapeoDeComprobanteDeInventarioDto>>(HttpMethod.Get, $"{Base}/voucher-mappings", null, null, ct);

    /// <summary>Agrega o reemplaza la fila de (operación, tipo de documento); el tipo de comprobante tiene que ser de Inventario.</summary>
    public Task<ResultadoDeInventario<MapeoFijadoDeInventarioDto>> FijarMapeoAsync(MapeoDeComprobanteDeInventarioRequest request, CancellationToken ct = default) =>
        EnviarAsync<MapeoFijadoDeInventarioDto>(HttpMethod.Put, $"{Base}/voucher-mappings", request, null, ct);

    // ------------------------------------------------------------------------------------------ completitud --

    public Task<ResultadoDeInventario<CompletitudDeInventarioDto>> CompletitudAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        EnviarAsync<CompletitudDeInventarioDto>(HttpMethod.Get,
            InventarioClient.ConQuery($"{Base}/completeness", InventarioClient.Query(("date", fecha?.ToString("yyyy-MM-dd")))), null, null, ct);

    // ------------------------------------------------------------------------------------------------ lotes --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<LoteDeIntegracionDto>>> LotesAsync(FiltroDeLotesDeIntegracion f, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<LoteDeIntegracionDto>>(HttpMethod.Get, InventarioClient.ConQuery($"{Base}/batches", InventarioClient.Query(
            ("from", f.Desde?.ToString("yyyy-MM-dd")), ("to", f.Hasta?.ToString("yyyy-MM-dd")),
            ("status", TextosDeInventario.Nombre(TextosDeInventario.NombresDeEstadoDeLote, f.Estado)),
            ("trigger", TextosDeInventario.Nombre(TextosDeInventario.NombresDeDisparador, f.Disparador)),
            ("destination", f.Destino), ("page", f.Pagina.ToString()), ("pageSize", f.TamanoDePagina.ToString()))), null, null, ct);

    public Task<ResultadoDeInventario<DetalleDeLoteDeIntegracionDto>> LoteAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<DetalleDeLoteDeIntegracionDto>(HttpMethod.Get, $"{Base}/batches/{id}", null, null, ct);

    /// <summary>Lo que haría un lote manual del rango, sin numerar ni guardar; trae el corte que se manda al ordenarlo.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeLoteDeIntegracionDto>> VistaPreviaDeLoteAsync(VistaPreviaDeLoteDeIntegracionRequest request,
        CancellationToken ct = default) =>
        EnviarAsync<VistaPreviaDeLoteDeIntegracionDto>(HttpMethod.Post, $"{Base}/batches/preview", request, null, ct);

    /// <summary>Ordena el lote manual (202): corre en segundo plano con la persona como actor. Con la clave de la operación.</summary>
    public Task<ResultadoDeInventario<LoteOrdenadoDeIntegracionDto>> OrdenarLoteAsync(OrdenDeLoteDeIntegracionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<LoteOrdenadoDeIntegracionDto>(HttpMethod.Post, $"{Base}/batches", request, clave, ct);

    // ---------------------------------------------------------------------------------------------- recibos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ReciboDeContabilizacionDto>>> RecibosAsync(Guid? documento = null, Guid? mensaje = null,
        Guid? lote = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ReciboDeContabilizacionDto>>(HttpMethod.Get, InventarioClient.ConQuery($"{Base}/postings", InventarioClient.Query(
            ("document", documento?.ToString()), ("message", mensaje?.ToString()), ("batch", lote?.ToString()),
            ("page", pagina.ToString()), ("pageSize", tamano.ToString()))), null, null, ct);

    // ------------------------------------------------------------------------------------------------ envío --

    private async Task<ResultadoDeInventario<T>> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
            clave?.Aplicar(req, new { metodo = metodo.Method, url, cuerpo });
            using var resp = await http.SendAsync(req, ct);
            var resultado = await ResultadoDeInventario<T>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave?.Exito();
            return resultado;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<T>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<T>.FormatoInesperado(ex.Message);
        }
    }
}
