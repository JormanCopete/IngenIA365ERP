using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>El saldo de una cuenta a (D, R) (api.md §4, <c>GET /accounts/{id}/balance</c>). (nuevo)</summary>
public sealed record SaldoDeCuentaALaFechaDto(
    Guid AccountPublicId,
    string Number,
    DateOnly Date,
    DateTime RecordedAsOf,
    string Fingerprint,
    IReadOnlyDictionary<string, decimal> ByConcept,
    decimal HolderBalance,
    DisponibleDeCuentaDto Available,
    decimal? AccruedNotSettled);

/// <summary>Una línea del extracto en pantalla (api.md §4, <c>GET /accounts/{id}/statement</c>). (nuevo)</summary>
public sealed record LineaDeExtractoEnPantallaDto(DateOnly ValueDate, Guid? OperationPublicId, string? OperationNumber, string Kind, decimal Amount,
    decimal Withholding, decimal Gmf, decimal Balance);

/// <summary>El extracto en pantalla (no es un documento emitido). (nuevo)</summary>
public sealed record ExtractoEnPantallaDto(Guid AccountPublicId, string Number, int Nature, DateOnly From, DateOnly To, decimal OpeningBalance,
    IReadOnlyList<LineaDeExtractoEnPantallaDto> Lines, decimal InterestCredited, decimal Withholding, decimal Gmf, decimal ClosingBalance,
    DisponibleDeCuentaDto AvailableToday);

/// <summary>
/// Feature 014, US13 (T199; contracts/api.md §4 y §17): las lecturas del saldo a una fecha y del extracto en pantalla de una cuenta —las dos rutas de la fase
/// 11 que no tenían pantalla, auditadas como lecturas sensibles— y la emisión pendiente del título vigente de un CDAT. Los documentos se emiten por
/// <see cref="CarteraClient.EmitirDocumentoAsync"/>. Sin <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed partial class CaptacionesClient
{
    public Task<ResultadoDeInventario<SaldoDeCuentaALaFechaDto>> SaldoALaFechaAsync(Guid cuenta, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<SaldoDeCuentaALaFechaDto>(HttpMethod.Get, ConQuery($"{RutaDeCuentas}/{cuenta}/balance", Query(("date", Texto(fecha)))), null, null, ct);

    public Task<ResultadoDeInventario<ExtractoEnPantallaDto>> ExtractoEnPantallaAsync(Guid cuenta, DateOnly? desde, DateOnly? hasta,
        CancellationToken ct = default) =>
        Enviar<ExtractoEnPantallaDto>(HttpMethod.Get, ConQuery($"{RutaDeCuentas}/{cuenta}/statement", Query(("from", Texto(desde)), ("to", Texto(hasta)))),
            null, null, ct);

    /// <summary>Emite el título de la versión vigente de un CDAT que no lo tiene (sin firmante al constituir); si ya lo tiene, lo devuelve.</summary>
    public Task<ResultadoDeInventario<TituloDeCdatDto>> EmitirTituloDeCdatAsync(Guid cdat, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TituloDeCdatDto>(HttpMethod.Post, $"{RutaDeCdat}/{cdat}/titles", null, clave, ct);
}
