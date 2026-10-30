using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>
/// Cliente tipado de los lotes de pago y sus archivos (feature 015, US10, T0147; US26, T0168; contracts/api.md §10): armar, revisar, enviar a aprobación, aprobar o
/// rechazar, generar el archivo, descargarlo con un enlace firmado, marcarlo enviado o anularlo. (nuevo)
/// </summary>
public sealed class LotesClient(HttpClient http, CentralAuthClient auth)
{
    private const string Lotes = "/api/treasury/payment-batches";
    private const string Archivos = "/api/treasury/payment-files";

    public Task<ResultadoDeInventario<IReadOnlyList<LoteDePagoDto>>> LotesAsync(int? estado = null, Guid? cuenta = null, CancellationToken ct = default) =>
        Get<IReadOnlyList<LoteDePagoDto>>(EnvioDeVentas.ConQuery(Lotes, EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(estado)),
            ("bankAccount", EnvioDeVentas.Texto(cuenta)))), ct);

    public Task<ResultadoDeInventario<LoteDePagoDto>> LoteAsync(Guid lote, CancellationToken ct = default) => Get<LoteDePagoDto>($"{Lotes}/{lote}", ct);

    public Task<ResultadoDeInventario<LoteDePagoDto>> CrearAsync(CrearLoteRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<LoteDePagoDto>(http, auth, HttpMethod.Post, Lotes, r, clave, ct);

    public Task<ResultadoDeInventario<LoteDePagoDto>> CambiarOrdenesAsync(Guid lote, ItemsDeLoteRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<LoteDePagoDto>(http, auth, HttpMethod.Put, $"{Lotes}/{lote}/items", r, clave, ct);

    public Task<ResultadoDeInventario<VistaPreviaDeLoteDto>> VistaPreviaAsync(Guid lote, CancellationToken ct = default) =>
        Get<VistaPreviaDeLoteDto>($"{Lotes}/{lote}/preview", ct);

    public Task<ResultadoDeInventario<LoteDePagoDto>> EnviarAAprobacionAsync(Guid lote, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<LoteDePagoDto>(http, auth, HttpMethod.Post, $"{Lotes}/{lote}/submit", null, clave, ct);

    public Task<ResultadoDeInventario<LoteDePagoDto>> RevisarAsync(Guid lote, Guid item, string? nota, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<LoteDePagoDto>(http, auth, HttpMethod.Post, $"{Lotes}/{lote}/items/{item}/review", new MotivoRequest(nota), clave, ct);

    public Task<ResultadoDeInventario<LoteDePagoDto>> AprobarAsync(Guid lote, string? motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<LoteDePagoDto>(http, auth, HttpMethod.Post, $"{Lotes}/{lote}/approve", new MotivoRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<LoteDePagoDto>> RechazarAsync(Guid lote, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<LoteDePagoDto>(http, auth, HttpMethod.Post, $"{Lotes}/{lote}/reject", new MotivoRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<ArchivoDePagoDto>> GenerarArchivoAsync(Guid lote, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<ArchivoDePagoDto>(http, auth, HttpMethod.Post, $"{Lotes}/{lote}/file", null, clave, ct);

    public Task<ResultadoDeInventario<LoteDePagoDto>> EnviadoPorPortalAsync(Guid lote, EnvioAlBancoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<LoteDePagoDto>(http, auth, HttpMethod.Post, $"{Lotes}/{lote}/sent", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ArchivoDePagoDto>>> ArchivosAsync(Guid? lote = null, CancellationToken ct = default) =>
        Get<IReadOnlyList<ArchivoDePagoDto>>(EnvioDeVentas.ConQuery(Archivos, EnvioDeVentas.Query(("batch", EnvioDeVentas.Texto(lote)))), ct);

    public Task<ResultadoDeInventario<EnlaceDeArchivoDto>> DescargarAsync(Guid archivo, CancellationToken ct = default) =>
        Get<EnlaceDeArchivoDto>($"{Archivos}/{archivo}/download", ct);

    public Task<ResultadoDeInventario<ArchivoDePagoDto>> MarcarEnviadoAsync(Guid archivo, EnvioAlBancoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<ArchivoDePagoDto>(http, auth, HttpMethod.Post, $"{Archivos}/{archivo}/sent", r, clave, ct);

    public Task<ResultadoDeInventario<ArchivoDePagoDto>> AnularArchivoAsync(Guid archivo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<ArchivoDePagoDto>(http, auth, HttpMethod.Post, $"{Archivos}/{archivo}/void", new MotivoRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Get<T>(string url, CancellationToken ct) => EnvioDeVentas.EnviarAsync<T>(http, auth, HttpMethod.Get, url, null, null, ct);
}
