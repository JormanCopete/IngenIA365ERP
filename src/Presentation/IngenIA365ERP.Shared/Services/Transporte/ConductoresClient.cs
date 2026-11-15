using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Transporte;

/// <summary>Cliente tipado de los conductores y sus asignaciones (feature 020, US5).</summary>
public sealed class ConductoresClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/transport/drivers";

    public Task<ResultadoDeInventario<IReadOnlyList<ConductorDto>>> ListarAsync(string? q = null, bool incluirRetirados = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ConductorDto>>(http, auth, HttpMethod.Get, ConQuery(Ruta, Query(("q", q), ("includeRetired", Texto(incluirRetirados)))), null, null, ct);

    public Task<ResultadoDeInventario<ConductorCreadoDto>> RegistrarAsync(Guid persona, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ConductorCreadoDto>(http, auth, HttpMethod.Post, Ruta, new { personPublicId = persona }, clave, ct);

    public Task<ResultadoDeInventario<object>> RetirarAsync(Guid conductor, DateOnly fecha, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"{Ruta}/{conductor}/retire", new { date = fecha }, null, ct);

    public Task<ResultadoDeInventario<Guid>> AsignarAsync(Guid conductor, Guid vehiculo, DateOnly desde, bool titular, CancellationToken ct = default) =>
        EnviarAsync<Guid>(http, auth, HttpMethod.Post, $"{Ruta}/{conductor}/assignments", new { vehiclePublicId = vehiculo, from = desde, isPrimary = titular }, null, ct);

    public Task<ResultadoDeInventario<object>> TerminarAsync(Guid conductor, Guid asignacion, DateOnly hasta, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"{Ruta}/{conductor}/assignments/{asignacion}/end", new { to = hasta }, null, ct);
}

public sealed record ConductorCreadoDto(Guid DriverPublicId, Guid PersonPublicId);

public sealed record AsignacionDeConductorDto(Guid PublicId, Guid VehiclePublicId, string Plate, string InternalNumber, DateOnly From, DateOnly? To, bool IsPrimary, bool IsCurrent);

public sealed record ConductorDto(Guid PublicId, Guid PersonPublicId, string IdType, string TaxId, string FullName, int Status, DateOnly Since, DateOnly? RetiredOn,
    IReadOnlyList<AsignacionDeConductorDto> Assignments);
