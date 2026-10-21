using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>
/// Cliente tipado de los valores nacionales (feature 013, fase 2, T0094; contracts/api.md §21.3 y §21.5): el registro central que lleva
/// el maestro con segundo factor (<c>/api/saas/national-values</c>, <c>/api/saas/regulatory-documents</c>) y la copia de solo lectura de
/// la cooperativa con su bitácora de réplicas y su revisión (<c>/api/core/national-values</c>, <c>/api/core/regulatory-documents</c>). La
/// cabecera <c>Authorization</c> la pone el handler (<c>ElTokenDeSesionLoPoneElHandler</c>); toda escritura lleva la
/// <c>Idempotency-Key</c> de la operación de pantalla. La plantilla <c>saas.national-values</c> la maneja <c>ImportarPlantilla</c> sobre
/// <see cref="RutaCentral"/>. (nuevo)
/// </summary>
public sealed class ValoresNacionalesClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaCentral = "/api/saas/national-values";
    public const string RutaDeDocumentosCentrales = "/api/saas/regulatory-documents";
    public const string RutaDeLaCopia = "/api/core/national-values";
    public const string RutaDeDocumentosDeLaCopia = "/api/core/regulatory-documents";

    // ------------------------------------------------------------------------------------ registro central (maestro) --

    public Task<ResultadoDeInventario<PaginaDePlataformaDto<ValorNacionalCentralDto>>> ListarCentralesAsync(string? codigo = null, string? clave = null,
        DateOnly? desde = null, DateOnly? hasta = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDePlataformaDto<ValorNacionalCentralDto>>(HttpMethod.Get, ConQuery(RutaCentral, Query(("code", codigo), ("key", clave), ("from", Texto(desde)),
            ("to", Texto(hasta)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<ValorNacionalRegistradoDto>> RegistrarAsync(RegistrarValorNacionalRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ValorNacionalRegistradoDto>(HttpMethod.Post, RutaCentral, request, clave, ct);

    public Task<ResultadoDeInventario<ValorNacionalRegistradoDto>> CorregirAsync(Guid valor, CorregirValorNacionalRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ValorNacionalRegistradoDto>(HttpMethod.Post, $"{RutaCentral}/{valor}/corrections", request, clave, ct);

    /// <summary>Los valores esperados sin vigencia para los próximos <paramref name="dias"/> días (C69).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ValorNacionalFaltanteDto>>> FaltantesAsync(int? dias = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ValorNacionalFaltanteDto>>(HttpMethod.Get, ConQuery($"{RutaCentral}/missing", Query(("days", Texto(dias)))), null, null, ct);

    /// <summary>El paquete <c>.json</c> para llevar los valores a otro ambiente (B5). Es una lectura: sin clave.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> ExportarAsync(DateOnly? desde = null, IReadOnlyList<string>? codigos = null, CancellationToken ct = default) =>
        DescargarAsync(http, auth, ConQuery($"{RutaCentral}/export", Query(("from", Texto(desde)),
            ("codes", codigos is { Count: > 0 } ? string.Join(',', codigos) : null))), "valores-nacionales.json", ct);

    public Task<ResultadoDeInventario<IReadOnlyList<DocumentoNormativoCentralDto>>> ListarDocumentosCentralesAsync(string? clase = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<DocumentoNormativoCentralDto>>(HttpMethod.Get, ConQuery(RutaDeDocumentosCentrales, Query(("kind", clase))), null, null, ct);

    public Task<ResultadoDeInventario<DocumentoNormativoRegistradoDto>> RegistrarDocumentoAsync(RegistrarDocumentoNormativoRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<DocumentoNormativoRegistradoDto>(HttpMethod.Post, RutaDeDocumentosCentrales, request, clave, ct);

    /// <summary>El contenido JSON de un documento central, para descargarlo.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> ContenidoCentralAsync(Guid documento, CancellationToken ct = default) =>
        DescargarAsync(http, auth, $"{RutaDeDocumentosCentrales}/{documento}/content", "documento-normativo.json", ct);

    // -------------------------------------------------------------------------------------- copia de la cooperativa --

    /// <summary>La copia; con <paramref name="fecha"/> (D) y <paramref name="informacionHasta"/> (R) aplica la regla bitemporal (C83).</summary>
    public Task<ResultadoDeInventario<PaginaDePlataformaDto<ValorNacionalDto>>> ListarCopiaAsync(string? codigo = null, string? clave = null, DateOnly? fecha = null,
        DateTime? informacionHasta = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDePlataformaDto<ValorNacionalDto>>(HttpMethod.Get, ConQuery(RutaDeLaCopia, Query(("code", codigo), ("key", clave), ("date", Texto(fecha)),
            ("recordedAsOf", informacionHasta?.ToUniversalTime().ToString("o")), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<PaginaDePlataformaDto<ReplicaDeValoresNacionalesDto>>> ReplicasAsync(int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDePlataformaDto<ReplicaDeValoresNacionalesDto>>(HttpMethod.Get, ConQuery($"{RutaDeLaCopia}/replications",
            Query(("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<RevisionRegistradaDto>> RevisarValorAsync(Guid valor, RevisionDeValorRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<RevisionRegistradaDto>(HttpMethod.Post, $"{RutaDeLaCopia}/{valor}/review", request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<DocumentoNormativoDto>>> ListarDocumentosAsync(string? clase = null, DateOnly? fecha = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<DocumentoNormativoDto>>(HttpMethod.Get, ConQuery(RutaDeDocumentosDeLaCopia, Query(("kind", clase), ("date", Texto(fecha)))),
            null, null, ct);

    public Task<ResultadoDeInventario<DocumentoNormativoDetalleDto>> DocumentoAsync(Guid documento, CancellationToken ct = default) =>
        Enviar<DocumentoNormativoDetalleDto>(HttpMethod.Get, $"{RutaDeDocumentosDeLaCopia}/{documento}", null, null, ct);

    public Task<ResultadoDeInventario<RevisionRegistradaDto>> RevisarDocumentoAsync(Guid documento, RevisionDeDocumentoRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<RevisionRegistradaDto>(HttpMethod.Post, $"{RutaDeDocumentosDeLaCopia}/{documento}/review", request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
