using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de los archivos de desembolsos (feature 013, US3, T0458; contracts/api.md §9.3, FR-047, T78): los formatos del ámbito
/// <c>LoanDisbursement</c> (de <c>/api/core/bank-file-formats</c>), las cuentas de origen del plan, la vista previa (por POST y sin clave), generar,
/// la lista, el enlace firmado de 60 s (sin clave; repetirlo emite otro) y anular antes del primer resultado de Tesorería. Sin
/// <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed class ArchivosDeDesembolsoClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeArchivos = GobiernoDeCarteraClient.Base + "/disbursement-files";
    public const string RutaDeFormatos = "/api/core/bank-file-formats";
    public const string Ambito = "LoanDisbursement";

    /// <summary>Los formatos activos del ámbito de desembolsos (genéricos y por banco).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<FormatoBancarioResumenDto>>> FormatosAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<FormatoBancarioResumenDto>>(HttpMethod.Get, ConQuery(RutaDeFormatos, Query(("scope", Ambito), ("onlyActive", "true"))), null, null, ct);

    /// <summary>Las cuentas bancarias del plan que sirven de origen para el formato (las de su banco, o todas si es genérico).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<CuentaDeOrigenDto>>> CuentasDeOrigenAsync(Guid? formato, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CuentaDeOrigenDto>>(HttpMethod.Get, ConQuery(RutaDeArchivos + "/source-accounts", Query(("formatPublicId", Texto(formato)))), null,
            null, ct);

    public Task<ResultadoDeInventario<VistaPreviaDeArchivoDto>> VistaPreviaAsync(ArchivoDeDesembolsoRequest request, CancellationToken ct = default) =>
        Enviar<VistaPreviaDeArchivoDto>(HttpMethod.Post, RutaDeArchivos + "/preview", request, null, ct);

    public Task<ResultadoDeInventario<ArchivoGeneradoDto>> GenerarAsync(ArchivoDeDesembolsoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ArchivoGeneradoDto>(HttpMethod.Post, RutaDeArchivos, request, clave, ct);

    /// <summary>Los archivos (<c>status</c> Generated o Voided).</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ArchivoDeDesembolsoDto>>> ArchivosAsync(string? estado = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ArchivoDeDesembolsoDto>>(HttpMethod.Get, ConQuery(RutaDeArchivos, Query(("status", estado), ("page", Texto(pagina)))),
            null, null, ct);

    /// <summary>Un enlace firmado de 60 s (auditado). Va por POST y sin clave.</summary>
    public Task<ResultadoDeInventario<EnlaceDeCarteraDto>> EnlaceAsync(Guid archivo, CancellationToken ct = default) =>
        Enviar<EnlaceDeCarteraDto>(HttpMethod.Post, $"{RutaDeArchivos}/{archivo}/download-link", null, null, ct);

    /// <summary>Anula el archivo (sólo sin resultados de Tesorería); sus órdenes quedan libres para otro.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> AnularAsync(Guid archivo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeArchivos}/{archivo}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
