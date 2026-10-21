using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de la integración contable vista desde Cartera (feature 013, US7, T0663; contracts/api.md §16.1, §16.3): los hechos contables
/// con su estado y su comprobante, el detalle de una parte con su lista por crédito, la conciliación con Contabilidad a una fecha y la bandeja de
/// los mensajes que Cartera emitió —lista con contadores y vista previa del envío posterior (<c>closure=true</c>), detalle, reproceso de
/// rechazados y envío posterior de «no aplica»—. Reprocesar y enviar después llevan motivo y la <see cref="ClaveDeOperacion"/> de la operación;
/// las consultas no. Sin <c>Authorization</c> a mano. Molde: <see cref="MensajesRecibidosClient"/>. (nuevo)
/// </summary>
public sealed class ConciliacionContableClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaContable = GobiernoDeCarteraClient.Base + "/accounting";
    public const string RutaDeMensajes = GobiernoDeCarteraClient.Base + "/messages";

    // ------------------------------------------------------------------------------------------- hechos --

    public Task<ResultadoDeInventario<PaginaDeCarteraDto<HechoContableDeCarteraDto>>> HechosAsync(FiltroDeHechosContables f, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<HechoContableDeCarteraDto>>(HttpMethod.Get, ConQuery($"{RutaContable}/facts", Query(
            ("type", f.Tipo), ("status", f.Estado), ("from", Texto(f.Desde)), ("to", Texto(f.Hasta)), ("branch", Texto(f.Sucursal)),
            ("loanPublicId", Texto(f.Credito)), ("page", Texto(f.Pagina)), ("pageSize", Texto(f.TamanoDePagina)))), null, null, ct);

    /// <summary>La parte con sus totales por combinación y una página de su lista por crédito.</summary>
    public Task<ResultadoDeInventario<ParteDeHechosContablesDto>> ParteAsync(Guid parte, int pagina = 1, int tamano = 100, CancellationToken ct = default) =>
        Enviar<ParteDeHechosContablesDto>(HttpMethod.Get, ConQuery($"{RutaContable}/fact-parts/{parte}", Query(
            ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    // ------------------------------------------------------------------------------------- conciliación --

    public Task<ResultadoDeInventario<ConciliacionContableDto>> ConciliacionAsync(DateOnly? fecha = null, Guid? sucursal = null, CancellationToken ct = default) =>
        Enviar<ConciliacionContableDto>(HttpMethod.Get, ConQuery($"{RutaContable}/reconciliation", Query(
            ("date", Texto(fecha)), ("branch", Texto(sucursal)))), null, null, ct);

    // ------------------------------------------------------------------------------------- tipos de hecho --

    /// <summary>El catálogo de tipos de hecho (<c>GET /api/lending/fact-types</c>, <c>Lending.Parameters.View</c>): el envío posterior los nombra por <c>PublicId</c>.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<TipoDeHechoDto>>> TiposDeHechoAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<TipoDeHechoDto>>(HttpMethod.Get, GobiernoDeCarteraClient.Base + "/fact-types", null, null, ct);

    // ----------------------------------------------------------------------------------------- emitidos --

    /// <summary>La bandeja de emitidos con sus contadores por estado; con <c>Clausura</c>, la vista previa del envío posterior y su corte.</summary>
    public Task<ResultadoDeInventario<BandejaDeMensajesDto>> EmitidosAsync(FiltroDeMensajesEmitidos f, CancellationToken ct = default) =>
        Enviar<BandejaDeMensajesDto>(HttpMethod.Get, ConQuery(RutaDeMensajes, Query(
            ("status", TextosDeInventario.Nombre(TextosDeInventario.NombresDeEstadoDeEntrega, f.Estado)),
            ("destination", f.Destino), ("type", f.Tipo), ("origin", Texto(f.Origen)), ("originNumber", f.NumeroDeOrigen), ("batch", Texto(f.Lote)),
            ("from", Texto(f.Desde)), ("to", Texto(f.Hasta)), ("closure", f.Clausura ? "true" : null),
            ("prevalidationOutcome", TextosDeInventario.Nombre(TextosDeInventario.NombresDeValidacionPrevia, f.ValidacionPrevia)),
            ("page", Texto(f.Pagina)), ("pageSize", Texto(f.TamanoDePagina)))), null, null, ct);

    /// <summary>El mensaje con su contenido inmutable, sus intentos y sus dependencias.</summary>
    public Task<ResultadoDeInventario<DetalleDeMensajeDto>> EmitidoAsync(Guid mensaje, string? destino = null, CancellationToken ct = default) =>
        Enviar<DetalleDeMensajeDto>(HttpMethod.Get, ConQuery($"{RutaDeMensajes}/{mensaje}", Query(("destination", destino))), null, null, ct);

    /// <summary>Reprocesa rechazados (202): un lote <c>Reprocess</c> que arrastra a los dependientes que esperaban.</summary>
    public Task<ResultadoDeInventario<LoteDeMensajesDto>> ReprocesarAsync(IReadOnlyList<Guid> mensajes, string motivo, ClaveDeOperacion clave,
        string? destino = null, CancellationToken ct = default) =>
        Enviar<LoteDeMensajesDto>(HttpMethod.Post, $"{RutaDeMensajes}/reprocess", new ReprocesoDeMensajesRequest(mensajes, motivo, destino), clave, ct);

    /// <summary>Envía después lo que no aplicaba (202), exactamente hasta el corte de la vista previa.</summary>
    public Task<ResultadoDeInventario<LoteDeMensajesDto>> EnviarNoAplicaAsync(EnvioPosteriorDeCarteraRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<LoteDeMensajesDto>(HttpMethod.Post, $"{RutaDeMensajes}/send-not-applicable", request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
