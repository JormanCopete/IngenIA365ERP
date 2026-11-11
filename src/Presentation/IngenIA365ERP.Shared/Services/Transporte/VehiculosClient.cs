using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Transporte;

/// <summary>
/// Cliente tipado de los vehículos y los catálogos de Transporte (feature 020, US2; contracts/api.md §3). La cabecera
/// <c>Authorization</c> la pone el handler (<c>ElTokenDeSesionLoPoneElHandler</c>); las altas llevan la <c>Idempotency-Key</c> de la
/// operación de pantalla. Las plantillas las maneja <c>ImportarPlantilla</c> sobre <see cref="Ruta"/>.
/// </summary>
public sealed class VehiculosClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/transport/vehicles";
    public const string RutaDeCatalogos = "/api/transport/catalogs";

    public Task<ResultadoDeInventario<PaginaDeTransporteDto<VehiculoListadoDto>>> ListarAsync(string? q = null, int? estado = null, Guid? propietario = null,
        int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeTransporteDto<VehiculoListadoDto>>(http, auth, HttpMethod.Get, ConQuery(Ruta, Query(("q", q), ("status", Texto(estado)),
            ("owner", Texto(propietario)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<VehiculoDetalleDto>> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<VehiculoDetalleDto>(http, auth, HttpMethod.Get, $"{Ruta}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<VehiculoCreadoDto>> CrearAsync(VehiculoEntradaDto vehiculo, Guid propietario, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<VehiculoCreadoDto>(http, auth, HttpMethod.Post, Ruta, new { vehicle = vehiculo, ownerPersonPublicId = propietario }, clave, ct);

    public Task<ResultadoDeInventario<object>> ActualizarAsync(Guid id, VehiculoEntradaDto vehiculo, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Put, $"{Ruta}/{id}", vehiculo, null, ct);

    public Task<ResultadoDeInventario<object>> CambiarEstadoAsync(Guid id, int estado, DateOnly fecha, string motivo, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"{Ruta}/{id}/status", new { status = estado, effectiveDate = fecha, reason = motivo }, null, ct);

    public Task<ResultadoDeInventario<object>> RetirarAsync(Guid id, DateOnly fecha, string motivo, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"{Ruta}/{id}/retire", new { effectiveDate = fecha, reason = motivo }, null, ct);

    public Task<ResultadoDeInventario<DeudasDelPropietarioDto>> DeudasDelPropietarioAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<DeudasDelPropietarioDto>(http, auth, HttpMethod.Get, $"{Ruta}/{id}/owner-debts", null, null, ct);

    public Task<ResultadoDeInventario<object>> CambiarPropietarioAsync(Guid id, Guid nuevo, DateOnly fecha, string motivo, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Post, $"{Ruta}/{id}/owner", new { newOwnerPersonPublicId = nuevo, effectiveDate = fecha, reason = motivo }, null, ct);

    public Task<ResultadoDeInventario<object>> GuardarEquiposAsync(Guid id, IReadOnlyList<EquipoEntradaDto> equipos, CancellationToken ct = default) =>
        EnviarAsync<object>(http, auth, HttpMethod.Put, $"{Ruta}/{id}/equipment", equipos, null, ct);

    // ------------------------------------------------------------------------------------------------ catálogos --

    public Task<ResultadoDeInventario<IReadOnlyList<ValorDeCatalogoDto>>> CatalogoAsync(string catalogo, bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ValorDeCatalogoDto>>(http, auth, HttpMethod.Get,
            ConQuery($"{RutaDeCatalogos}/{catalogo}", Query(("includeInactive", Texto(incluirInactivos)))), null, null, ct);

    public Task<ResultadoDeInventario<ValorDeCatalogoDto>> CrearValorAsync(string catalogo, string codigo, string nombre, CancellationToken ct = default) =>
        EnviarAsync<ValorDeCatalogoDto>(http, auth, HttpMethod.Post, $"{RutaDeCatalogos}/{catalogo}", new { code = codigo, name = nombre }, null, ct);

    public Task<ResultadoDeInventario<ValorDeCatalogoDto>> CambiarValorAsync(string catalogo, Guid id, string nombre, bool activo, CancellationToken ct = default) =>
        EnviarAsync<ValorDeCatalogoDto>(http, auth, HttpMethod.Put, $"{RutaDeCatalogos}/{catalogo}/{id}", new { name = nombre, isActive = activo }, null, ct);

    /// <summary>Los nueve catálogos de Transporte: nombre de ruta y etiqueta.</summary>
    public static readonly IReadOnlyList<(string Ruta, string Etiqueta)> Catalogos =
    [
        ("marca", "Marcas"), ("clase", "Clases de vehículo"), ("carroceria", "Carrocerías"), ("combustible", "Combustibles"),
        ("modalidad", "Modalidades"), ("clase-de-servicio", "Clases de servicio"), ("organismo-de-transito", "Organismos de tránsito"),
        ("grupo-de-rodamiento", "Grupos de rodamiento"), ("tipo-de-equipo", "Tipos de equipo"),
    ];
}

public sealed record PaginaDeTransporteDto<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public sealed record VehiculoListadoDto(Guid PublicId, string Plate, string InternalNumber, string? RadioCode, int Status, DateOnly StatusSince,
    Guid OwnerPersonPublicId, string OwnerTaxId, string OwnerName, int Affiliation, string? Brand, string? Line, short? ModelYear);

public sealed record VehiculoCreadoDto(Guid VehiclePublicId, Guid OwnerPersonPublicId);

/// <summary>La ficha editable del vehículo (sin estado ni propietario).</summary>
public sealed class VehiculoEntradaDto
{
    public string Plate { get; set; } = string.Empty;
    public string InternalNumber { get; set; } = string.Empty;
    public string? RadioCode { get; set; }
    public Guid? BrandPublicId { get; set; }
    public string? Line { get; set; }
    public short? ModelYear { get; set; }
    public Guid? VehicleClassPublicId { get; set; }
    public Guid? BodyTypePublicId { get; set; }
    public string? Color { get; set; }
    public int? EngineDisplacementCc { get; set; }
    public Guid? FuelPublicId { get; set; }
    public short? PassengerCapacity { get; set; }
    public string? EngineNumber { get; set; }
    public string? ChassisNumber { get; set; }
    public string? Vin { get; set; }
    public string? TransitLicenseNumber { get; set; }
    public Guid? TransitAuthorityPublicId { get; set; }
    public DateOnly? RegistrationDate { get; set; }
    public Guid? ServiceClassPublicId { get; set; }
    public Guid? ModalityPublicId { get; set; }
    public DateOnly? JoinedOn { get; set; }
    public string? AffiliationContractNumber { get; set; }
    public DateOnly? AffiliationContractFrom { get; set; }
    public DateOnly? AffiliationContractTo { get; set; }
    public string? OperationCardNumber { get; set; }
    public Guid? RollingGroupPublicId { get; set; }
    public decimal? MinDailyRolling { get; set; }
    public decimal? MinMonthlyRolling { get; set; }
    public Guid? BranchPublicId { get; set; }
}

public sealed record RefDeCatalogoDto(Guid PublicId, string Code, string Name);

public sealed record PropietarioDto(Guid PersonPublicId, string IdType, string TaxId, string FullName, int Affiliation, DateOnly Since);

public sealed record EventoDeVehiculoDto(Guid PublicId, int Kind, DateOnly EffectiveDate, string? FromValue, string? ToValue, string? FromLabel, string? ToLabel,
    int? FromAffiliation, int? ToAffiliation, string? Reason, int Source, string? ChangedBy, DateTime RecordedAt);

public sealed record EquipoDto(Guid PublicId, RefDeCatalogoDto EquipmentType, string? Code, int Status);

public sealed record EquipoEntradaDto(Guid EquipmentTypePublicId, string? Code, int Status = 1);

public sealed class VehiculoDetalleDto
{
    public Guid PublicId { get; set; }
    public string Plate { get; set; } = string.Empty;
    public string InternalNumber { get; set; } = string.Empty;
    public string? RadioCode { get; set; }
    public int Status { get; set; }
    public DateOnly StatusSince { get; set; }
    public DateOnly? RetiredOn { get; set; }
    public string? RetirementReason { get; set; }
    public PropietarioDto Owner { get; set; } = null!;
    public VehiculoEntradaDto Data { get; set; } = new();
    public RefDeCatalogoDto? Brand { get; set; }
    public RefDeCatalogoDto? VehicleClass { get; set; }
    public RefDeCatalogoDto? BodyType { get; set; }
    public RefDeCatalogoDto? Fuel { get; set; }
    public RefDeCatalogoDto? TransitAuthority { get; set; }
    public RefDeCatalogoDto? ServiceClass { get; set; }
    public RefDeCatalogoDto? Modality { get; set; }
    public RefDeCatalogoDto? RollingGroup { get; set; }
    public List<EquipoDto> Equipment { get; set; } = [];
    public List<EventoDeVehiculoDto> History { get; set; } = [];
    public List<DocumentoDeVehiculoDto> Documents { get; set; } = [];
    public List<ConductorDelVehiculoDto> Drivers { get; set; } = [];
    public List<SuscripcionDto> Subscriptions { get; set; } = [];
}

public sealed record DeudaDto(string Source, string Reference, string Description, decimal OverdueAmount, int? DaysPastDue);

public sealed record DeudasDelPropietarioDto(Guid PersonPublicId, string FullName, decimal Total, IReadOnlyList<DeudaDto> Items, string? Note);

public sealed record ValorDeCatalogoDto(Guid PublicId, int Kind, string Code, string Name, bool IsActive, bool IsSystem);

public sealed record DocumentoDeVehiculoDto(Guid PublicId, Guid DocumentTypePublicId, string DocumentTypeCode, string DocumentTypeName, string Number,
    string? Issuer, DateOnly? IssuedOn, DateOnly? ValidFrom, DateOnly ExpiresOn, bool IsCurrent, string Situation, int DaysToExpire);

public sealed record ConductorDelVehiculoDto(Guid DriverPublicId, Guid AssignmentPublicId, Guid PersonPublicId, string TaxId, string FullName,
    DateOnly From, DateOnly? To, bool IsPrimary, bool IsCurrent);

public sealed record SuscripcionDto(Guid PublicId, Guid ServicePublicId, string ServiceName, string ProductCode, DateOnly From, DateOnly? To,
    decimal? AgreedPrice, bool IsCurrent);

/// <summary>Textos de Transporte que repiten varias pantallas.</summary>
public static class TextosDeTransporte
{
    public static string Estado(int estado) => estado switch { 1 => "Activo", 2 => "Inactivo", 3 => "Retirado", _ => estado.ToString() };

    public static string Vinculacion(int clase) => clase == 1 ? "Asociado" : "Externo";

    public static string Evento(int tipo) => tipo switch { 1 => "Registro", 2 => "Cambio de estado", 3 => "Cambio de propietario", 4 => "Reingreso", _ => tipo.ToString() };

    public static string Origen(int origen) => origen == 2 ? "Plantilla" : "Pantalla";
}
