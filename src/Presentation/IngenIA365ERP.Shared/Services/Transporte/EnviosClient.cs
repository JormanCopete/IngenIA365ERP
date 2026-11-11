using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Transporte;

/// <summary>Cliente tipado del tablero de envíos a sistemas externos (feature 020, US9; contracts/api.md §3, <c>/api/transport/notifications</c>).</summary>
public sealed class EnviosClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/transport/notifications";

    public Task<ResultadoDeInventario<TableroDeEnviosDto>> ListarAsync(string? estado, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default) =>
        EnviarAsync<TableroDeEnviosDto>(http, auth, HttpMethod.Get,
            ConQuery(Base, Query(("status", estado), ("from", Texto(desde)), ("to", Texto(hasta)))), null, null, ct);

    public Task<ResultadoDeInventario<EnvioDetalleDto>> DetalleAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<EnvioDetalleDto>(http, auth, HttpMethod.Get, $"{Base}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<object>> ReintentarAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"{Base}/{id}/retry", new { }, clave, ct);

    public Task<ResultadoDeInventario<object>> CerrarAsync(Guid id, string motivo, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"{Base}/{id}/resolve", new { reason = motivo }, null, ct);
}

public sealed record EnvioDto(Guid PublicId, string Status, string Connector, Guid? VehiclePublicId, string? Plate, string? InternalNumber, string? Service,
    string? Period, string? InvoiceNumber, decimal? Amount, Guid ExternalId, int Attempts, int? LastStatusCode, string? LastResponseExcerpt,
    DateTime? LastAttemptAt, DateTime? SentAt, string? Warning, string? ReviewFlag, DateTime CreatedAt, string? ResolvedBy, string? ResolutionReason);

public sealed record TableroDeEnviosDto(IReadOnlyDictionary<string, int> Counts, IReadOnlyList<EnvioDto> Items, bool Truncated);

public sealed record IntentoDeEnvioDto(int AttemptNumber, string Kind, DateTime StartedAt, int DurationMs, int StatusCode, string Outcome, string? ResponseExcerpt,
    string? RequestBodySanitized, string? Url);

public sealed record EnvioDetalleDto(EnvioDto Envio, IReadOnlyList<IntentoDeEnvioDto> Attempts);

/// <summary>Los textos de los estados de un envío para la pantalla.</summary>
public static class EstadosDeEnvio
{
    public static readonly IReadOnlyList<(string Codigo, string Texto)> Todos =
    [
        ("Pending", "Pendiente"), ("Sent", "Enviado"), ("SentWithWarning", "Enviado con aviso"), ("Retrying", "Reintentando"),
        ("OnHold", "En espera"), ("Review", "Revisar"), ("PaymentVoided", "Pago anulado"), ("ResolvedManually", "Cerrado a mano"),
    ];

    public static string Texto(string codigo) => Todos.FirstOrDefault(e => e.Codigo == codigo).Texto ?? codigo;

    public static string Marca(string? marca) => marca switch
    {
        "PaymentVoidedAfterNotice" => "Pago anulado tras avisar: revise el sistema externo",
        "PaidAgainAfterVoid" => "Pagado otra vez tras anularse: no se avisó de nuevo",
        "NoConnector" => "El conector no tiene versión vigente",
        _ => string.Empty,
    };
}
