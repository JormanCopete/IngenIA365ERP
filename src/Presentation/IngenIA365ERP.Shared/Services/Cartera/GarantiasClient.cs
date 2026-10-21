using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de las garantías y el pagaré (feature 013, US3, T0458; contracts/api.md §6): garantías de una solicitud o crédito (registrar,
/// editar, cambiar de estado, liberar) y el pagaré físico o desmaterializado (crear, actualizar el estado con el depósito —por POST y sin clave:
/// sólo trae lo que el depósito ya sabe—, custodia del físico, endosar, anular). Sus documentos se suben directo al almacén con el dueño
/// <see cref="DuenoDeDocumentos"/>. Sin <c>Authorization</c> a mano; toda escritura con su <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class GarantiasClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeGarantias = GobiernoDeCarteraClient.Base + "/collaterals";
    public const string RutaDePagares = GobiernoDeCarteraClient.Base + "/promissory-notes";

    /// <summary>El dueño de los adjuntos de una garantía o un pagaré (<c>AdjuntosDeModulo</c>, <c>LendingGuaranteeDocument</c>).</summary>
    public const string DuenoDeDocumentos = "LendingGuaranteeDocument";

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<GarantiaDto>>> GarantiasAsync(Guid? solicitud = null, Guid? credito = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<GarantiaDto>>(HttpMethod.Get, ConQuery(RutaDeGarantias, Query(
            ("applicationPublicId", Texto(solicitud)), ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<GarantiaDto>> RegistrarGarantiaAsync(GarantiaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<GarantiaDto>(HttpMethod.Post, RutaDeGarantias, request, clave, ct);

    public Task<ResultadoDeInventario<GarantiaDto>> EditarGarantiaAsync(Guid garantia, GarantiaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<GarantiaDto>(HttpMethod.Put, $"{RutaDeGarantias}/{garantia}", request, clave, ct);

    public Task<ResultadoDeInventario<GarantiaDto>> CambiarEstadoAsync(Guid garantia, EstadoDeGarantiaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<GarantiaDto>(HttpMethod.Post, $"{RutaDeGarantias}/{garantia}/status", request, clave, ct);

    public Task<ResultadoDeInventario<GarantiaDto>> LiberarAsync(Guid garantia, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<GarantiaDto>(HttpMethod.Post, $"{RutaDeGarantias}/{garantia}/release", new MotivoDeCarteraRequest(motivo), clave, ct);

    // ------------------------------------------------------------------------------------------------- pagaré --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<PagareDto>>> PagaresAsync(Guid? solicitud = null, Guid? credito = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<PagareDto>>(HttpMethod.Get, ConQuery(RutaDePagares, Query(
            ("applicationPublicId", Texto(solicitud)), ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    /// <summary>Crea el pagaré: el desmaterializado lo pide al depósito vigente (queda sellado) y espera la firma.</summary>
    public Task<ResultadoDeInventario<PagareDto>> CrearPagareAsync(PagareRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PagareDto>(HttpMethod.Post, RutaDePagares, request, clave, ct);

    /// <summary>Consulta el estado al depósito (sin clave).</summary>
    public Task<ResultadoDeInventario<PagareDto>> ActualizarPagareAsync(Guid pagare, CancellationToken ct = default) =>
        Enviar<PagareDto>(HttpMethod.Post, $"{RutaDePagares}/{pagare}/refresh", null, null, ct);

    public Task<ResultadoDeInventario<PagareDto>> CustodiaAsync(Guid pagare, CustodiaDePagareRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<PagareDto>(HttpMethod.Post, $"{RutaDePagares}/{pagare}/physical-custody", request, clave, ct);

    public Task<ResultadoDeInventario<PagareDto>> AnularPagareAsync(Guid pagare, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PagareDto>(HttpMethod.Post, $"{RutaDePagares}/{pagare}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
