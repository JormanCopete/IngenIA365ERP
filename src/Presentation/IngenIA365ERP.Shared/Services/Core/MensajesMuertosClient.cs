using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>Una fila de mensajes muertos (feature 023; contracts/api.md §4). <c>Kind</c>: 1 documento, 2 seguimiento, 3 entrega. (nuevo)</summary>
public sealed record MensajeMuertoDto(int Kind, Guid PublicId, string Reference, string? Counterparty, decimal? Amount, DateTime DeadAt,
    int? AttemptLimit, int Attempts, string? LastError, string Module, string? Destination);

public sealed record IntentoDeMensajeMuertoDto(int Number, DateTime At, string Outcome, string? Code, string? Message);

public sealed record DetalleDeMensajeMuertoDto(MensajeMuertoDto Item, IReadOnlyList<IntentoDeMensajeMuertoDto> Attempts);

public sealed record PaginaDeMensajesMuertosDto(IReadOnlyList<MensajeMuertoDto> Items, int Page, int PageSize, long TotalCount);

public sealed record ReferenciaDeMensajeMuertoRequest(int Kind, Guid PublicId);

public sealed record ReprocesoDeMensajesMuertosRequest(IReadOnlyList<ReferenciaDeMensajeMuertoRequest> Items, string Reason);

public sealed record OmitidoDto(Guid PublicId, string Code);

public sealed record ResultadoDeReprocesoDto(int Reprocessed, IReadOnlyList<OmitidoDto> Skipped);

/// <summary>
/// Cliente tipado de los mensajes muertos (feature 023; contracts/api.md §4): documentos electrónicos, seguimientos y entregas de la bandeja
/// que agotaron su tope de intentos. Sin <c>Authorization</c> a mano; el reproceso lleva <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public sealed class MensajesMuertosClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/platform/dead-letters";

    public Task<ResultadoDeInventario<PaginaDeMensajesMuertosDto>> ListarAsync(int? clase, int pagina, int tamano, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeMensajesMuertosDto>(http, auth, HttpMethod.Get,
            ConQuery(Ruta, Query(("kind", clase?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("page", pagina.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("pageSize", tamano.ToString(System.Globalization.CultureInfo.InvariantCulture)))), null, null, ct);

    public Task<ResultadoDeInventario<DetalleDeMensajeMuertoDto>> DetalleAsync(int clase, Guid id, CancellationToken ct = default) =>
        EnviarAsync<DetalleDeMensajeMuertoDto>(http, auth, HttpMethod.Get, $"{Ruta}/{clase}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<ResultadoDeReprocesoDto>> ReprocesarAsync(ReprocesoDeMensajesMuertosRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeReprocesoDto>(http, auth, HttpMethod.Post, $"{Ruta}/reprocess", request, clave, ct);
}
