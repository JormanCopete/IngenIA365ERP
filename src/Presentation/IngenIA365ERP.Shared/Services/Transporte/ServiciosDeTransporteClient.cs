using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Transporte;

/// <summary>Cliente tipado de los servicios por vehículo, suscripciones, políticas y facturación (feature 020, US6).</summary>
public sealed class ServiciosDeTransporteClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeSuscripciones = "/api/transport/subscriptions";

    public Task<ResultadoDeInventario<IReadOnlyList<ServicioDeTransporteDto>>> ServiciosAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ServicioDeTransporteDto>>(http, auth, HttpMethod.Get, ConQuery("/api/transport/services", Query(("includeInactive", Texto(incluirInactivos)))), null, null, ct);

    public Task<ResultadoDeInventario<ServicioDeTransporteDto>> CrearServicioAsync(ServicioEntradaDto s, CancellationToken ct = default) =>
        EnviarAsync<ServicioDeTransporteDto>(http, auth, HttpMethod.Post, "/api/transport/services", s, null, ct);

    public Task<ResultadoDeInventario<ServicioDeTransporteDto>> CambiarServicioAsync(Guid id, ServicioEntradaDto s, CancellationToken ct = default) =>
        EnviarAsync<ServicioDeTransporteDto>(http, auth, HttpMethod.Put, $"/api/transport/services/{id}", s, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<PoliticaDeFacturacionDto>>> PoliticasAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<PoliticaDeFacturacionDto>>(http, auth, HttpMethod.Get, "/api/transport/billing-policies", null, null, ct);

    public Task<ResultadoDeInventario<Guid>> PonerPoliticaAsync(PoliticaEntradaDto p, CancellationToken ct = default) =>
        EnviarAsync<Guid>(http, auth, HttpMethod.Post, "/api/transport/billing-policies", p, null, ct);

    public Task<ResultadoDeInventario<Guid>> SuscribirAsync(Guid vehiculo, Guid servicio, DateOnly desde, DateOnly? hasta, decimal? precio, CancellationToken ct = default) =>
        EnviarAsync<Guid>(http, auth, HttpMethod.Post, $"/api/transport/vehicles/{vehiculo}/subscriptions",
            new { servicePublicId = servicio, from = desde, to = hasta, price = precio }, null, ct);

    public Task<ResultadoDeInventario<object>> TerminarAsync(Guid vehiculo, Guid suscripcion, DateOnly hasta, string? motivo, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"/api/transport/vehicles/{vehiculo}/subscriptions/{suscripcion}/end", new { to = hasta, reason = motivo }, null, ct);

    public Task<ResultadoDeInventario<object>> PactarPrecioAsync(Guid vehiculo, Guid suscripcion, decimal precio, DateOnly desde, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"/api/transport/vehicles/{vehiculo}/subscriptions/{suscripcion}/prices", new { price = precio, from = desde }, null, ct);

    // --------------------------------------------------------------------------------------------- facturación --

    public Task<ResultadoDeInventario<VistaPreviaDeFacturacionDto>> VistaPreviaAsync(string periodo, CancellationToken ct = default) =>
        EnviarAsync<VistaPreviaDeFacturacionDto>(http, auth, HttpMethod.Get, ConQuery("/api/transport/billing/preview", Query(("period", periodo))), null, null, ct);

    public Task<ResultadoDeInventario<FacturaIndividualDto>> FacturarVehiculoAsync(Guid vehiculo, string periodo, int modo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<FacturaIndividualDto>(http, auth, HttpMethod.Post, "/api/transport/billing/single", new { vehiclePublicId = vehiculo, period = periodo, paymentMode = modo }, clave, ct);

    public Task<ResultadoDeInventario<CorridaDeFacturacionDto>> AbrirCorridaAsync(string periodo, int modo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<CorridaDeFacturacionDto>(http, auth, HttpMethod.Post, "/api/transport/billing/runs", new { period = periodo, paymentMode = modo }, clave, ct);

    public Task<ResultadoDeInventario<CorridaDeFacturacionDto>> AvanzarCorridaAsync(Guid corrida, CancellationToken ct = default) =>
        EnviarAsync<CorridaDeFacturacionDto>(http, auth, HttpMethod.Post, $"/api/transport/billing/runs/{corrida}/next", null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CorridaDeFacturacionDto>>> CorridasAsync(string? periodo = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CorridaDeFacturacionDto>>(http, auth, HttpMethod.Get, ConQuery("/api/transport/billing/runs", Query(("period", periodo))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CargoDeServicioDto>>> CargosAsync(string? periodo = null, Guid? vehiculo = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CargoDeServicioDto>>(http, auth, HttpMethod.Get, ConQuery("/api/transport/charges", Query(("period", periodo), ("vehicle", Texto(vehiculo)))), null, null, ct);
}

public sealed record ServicioDeTransporteDto(Guid PublicId, Guid ProductPublicId, string ProductCode, string Name, int Priority, string? CreditLineCode,
    string? AssociateCreditMeansCode, string? CustomerCreditMeansCode, Guid? ConnectorPublicId, string? ConnectorName, bool IsActive);

public sealed class ServicioEntradaDto
{
    public Guid ProductPublicId { get; set; }
    public int Priority { get; set; } = 1;
    public string? CreditLineCode { get; set; }
    public string? AssociateCreditMeansCode { get; set; }
    public string? CustomerCreditMeansCode { get; set; }
    public Guid? ConnectorPublicId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record PoliticaDeFacturacionDto(Guid PublicId, DateOnly ValidFrom, DateOnly? ValidTo, int Proration, string SalesDocumentTypeCode, string? WarehouseCode,
    string? SalesPointCode, short DueDay);

public sealed class PoliticaEntradaDto
{
    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int Proration { get; set; } = 1;
    public string SalesDocumentTypeCode { get; set; } = string.Empty;
    public string? WarehouseCode { get; set; }
    public string? SalesPointCode { get; set; }
    public short DueDay { get; set; }
}

public sealed record OmisionDto(Guid VehiclePublicId, string Plate, string InternalNumber, string Code, string Reason);

public sealed record LineaDeVistaPreviaDto(string Service, decimal Quantity, decimal? UnitPrice, bool AgreedPrice);

public sealed record VehiculoDeVistaPreviaDto(Guid VehiclePublicId, string Plate, string InternalNumber, string OwnerName, IReadOnlyList<LineaDeVistaPreviaDto> Lines, decimal Total);

public sealed record VistaPreviaDeFacturacionDto(string Period, int Vehicles, decimal Total, IReadOnlyList<VehiculoDeVistaPreviaDto> Items, IReadOnlyList<OmisionDto> Omitted,
    int AlreadyBilled, string? Warning);

public sealed record FacturaIndividualDto(Guid? InvoicePublicId, string? InvoiceNumber, decimal Total, bool AlreadyBilled, string? OmissionCode, string? OmissionReason,
    IReadOnlyList<Guid> Charges);

public sealed record CorridaDeFacturacionDto(Guid PublicId, string Period, int Mode, int PaymentMode, int Status, DateTime? StartedAt, DateTime? FinishedAt, string StartedBy,
    int InvoicesCount, decimal TotalAmount, int AlreadyBilledCount, int OmittedCount, IReadOnlyList<OmisionDto> Omitted, int Pending);

public sealed record CargoDeServicioDto(Guid PublicId, Guid VehiclePublicId, string Plate, string InternalNumber, string Service, string Period, string InvoiceNumber,
    decimal Amount, decimal Paid, int Route, int Status, DateTime? PaidAt, int ReviewFlag, Guid ExternalId);
