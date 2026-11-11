using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Transporte;

/// <summary>Cliente tipado de los documentos del vehículo, sus tipos y el tablero de vencimientos (feature 020, US4).</summary>
public sealed class DocumentosDeVehiculoClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDelTablero = "/api/transport/documents";
    public const string RutaDeTipos = "/api/transport/document-types";

    public Task<ResultadoDeInventario<IReadOnlyList<TipoDeDocumentoDto>>> TiposAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<TipoDeDocumentoDto>>(http, auth, HttpMethod.Get, ConQuery(RutaDeTipos, Query(("includeInactive", Texto(incluirInactivos)))), null, null, ct);

    public Task<ResultadoDeInventario<TipoDeDocumentoDto>> CrearTipoAsync(string codigo, string nombre, bool obligatorio, int dias, CancellationToken ct = default) =>
        EnviarAsync<TipoDeDocumentoDto>(http, auth, HttpMethod.Post, RutaDeTipos, new { code = codigo, name = nombre, isRequired = obligatorio, warningDays = dias }, null, ct);

    public Task<ResultadoDeInventario<TipoDeDocumentoDto>> CambiarTipoAsync(Guid id, string nombre, bool obligatorio, int dias, bool activo, CancellationToken ct = default) =>
        EnviarAsync<TipoDeDocumentoDto>(http, auth, HttpMethod.Put, $"{RutaDeTipos}/{id}",
            new { name = nombre, isRequired = obligatorio, warningDays = dias, isActive = activo }, null, ct);

    public Task<ResultadoDeInventario<Guid>> RegistrarAsync(Guid vehiculo, DocumentoEntradaDto doc, CancellationToken ct = default) =>
        EnviarAsync<Guid>(http, auth, HttpMethod.Post, $"/api/transport/vehicles/{vehiculo}/documents", doc, null, ct);

    public Task<ResultadoDeInventario<object>> CorregirAsync(Guid vehiculo, Guid documento, DocumentoEntradaDto doc, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Put, $"/api/transport/vehicles/{vehiculo}/documents/{documento}", doc, null, ct);

    public Task<ResultadoDeInventario<TableroDeVencimientosDto>> TableroAsync(string? situacion = null, int? dias = null, CancellationToken ct = default) =>
        EnviarAsync<TableroDeVencimientosDto>(http, auth, HttpMethod.Get, ConQuery($"{RutaDelTablero}/expirations",
            Query(("status", situacion), ("days", Texto(dias)))), null, null, ct);
}

public sealed record TipoDeDocumentoDto(Guid PublicId, string Code, string Name, bool IsRequired, int WarningDays, bool IsActive, bool IsSystem);

public sealed class DocumentoEntradaDto
{
    public Guid DocumentTypePublicId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly ExpiresOn { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(1));
}

public sealed record VencimientoDto(Guid VehiclePublicId, string Plate, string InternalNumber, int VehicleStatus, Guid OwnerPersonPublicId, string OwnerTaxId,
    string OwnerName, string DocumentTypeCode, string DocumentTypeName, string Situation, string? Number, DateOnly? ExpiresOn, int? DaysToExpire);

public sealed record TableroDeVencimientosDto(int Expired, int Expiring, int Missing, IReadOnlyList<VencimientoDto> Items);
