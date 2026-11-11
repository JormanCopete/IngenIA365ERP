using IngenIA365ERP.Domain.Enums.Transport;

namespace IngenIA365ERP.Application.Transport.Vehicles;

/// <summary>
/// La ficha de un vehículo como la manda la pantalla, la API o una fila de plantilla (feature 020, FR-005 a FR-008; contracts/api.md §3).
/// <b>No trae estado ni propietario</b>: cambian sólo por su operación con historia (FR-011). Los catálogos van por <c>PublicId</c>.
/// </summary>
public sealed record VehicleInput
{
    public string Plate { get; init; } = string.Empty;
    public string InternalNumber { get; init; } = string.Empty;
    public string? RadioCode { get; init; }

    // Ficha técnica.
    public Guid? BrandPublicId { get; init; }
    public string? Line { get; init; }
    public short? ModelYear { get; init; }
    public Guid? VehicleClassPublicId { get; init; }
    public Guid? BodyTypePublicId { get; init; }
    public string? Color { get; init; }
    public int? EngineDisplacementCc { get; init; }
    public Guid? FuelPublicId { get; init; }
    public short? PassengerCapacity { get; init; }
    public string? EngineNumber { get; init; }
    public string? ChassisNumber { get; init; }
    public string? Vin { get; init; }
    public string? TransitLicenseNumber { get; init; }
    public Guid? TransitAuthorityPublicId { get; init; }
    public DateOnly? RegistrationDate { get; init; }
    public Guid? ServiceClassPublicId { get; init; }
    public Guid? ModalityPublicId { get; init; }

    // Vinculación.
    public DateOnly? JoinedOn { get; init; }
    public string? AffiliationContractNumber { get; init; }
    public DateOnly? AffiliationContractFrom { get; init; }
    public DateOnly? AffiliationContractTo { get; init; }
    public string? OperationCardNumber { get; init; }
    public Guid? RollingGroupPublicId { get; init; }
    public decimal? MinDailyRolling { get; init; }
    public decimal? MinMonthlyRolling { get; init; }

    /// <summary>La oficina (sucursal contable) del vehículo; sin ella se propone la principal.</summary>
    public Guid? BranchPublicId { get; init; }
}

/// <summary>Un equipo del vehículo (FR-008).</summary>
public sealed record EquipmentInput(Guid EquipmentTypePublicId, string? Code, EquipmentStatus Status = EquipmentStatus.Active);

/// <summary>Un valor de catálogo, como lo ve la ficha.</summary>
public sealed record CatalogRefDto(Guid PublicId, string Code, string Name);

/// <summary>El propietario como lo ve la ficha, con su clase de vinculación deducida (FR-016).</summary>
public sealed record OwnerDto(Guid PersonPublicId, string IdType, string TaxId, string FullName, AffiliationClass Affiliation, DateOnly Since);

/// <summary>Una fila del listado de vehículos (FR-012).</summary>
public sealed record VehicleListItemDto(
    Guid PublicId,
    string Plate,
    string InternalNumber,
    string? RadioCode,
    VehicleStatus Status,
    DateOnly StatusSince,
    Guid OwnerPersonPublicId,
    string OwnerTaxId,
    string OwnerName,
    AffiliationClass Affiliation,
    string? Brand,
    string? Line,
    short? ModelYear);

/// <summary>Un evento de la historia del vehículo.</summary>
public sealed record VehicleEventDto(
    Guid PublicId, VehicleEventKind Kind, DateOnly EffectiveDate, string? FromValue, string? ToValue, string? FromLabel, string? ToLabel,
    AffiliationClass? FromAffiliation, AffiliationClass? ToAffiliation, string? Reason, ChangeSource Source, string? ChangedBy, DateTime RecordedAt);

/// <summary>Un equipo del vehículo, como lo ve la ficha.</summary>
public sealed record VehicleEquipmentDto(Guid PublicId, CatalogRefDto EquipmentType, string? Code, EquipmentStatus Status);

/// <summary>
/// La ficha completa de un vehículo (contracts/api.md §3, <c>GET /vehicles/{id}</c>). Documentos, conductores y suscripciones se llenan en
/// sus historias (US4, US5, US6); hasta entonces van vacíos.
/// </summary>
public sealed record VehicleDetailDto
{
    public Guid PublicId { get; init; }
    public string Plate { get; init; } = string.Empty;
    public string InternalNumber { get; init; } = string.Empty;
    public string? RadioCode { get; init; }
    public VehicleStatus Status { get; init; }
    public DateOnly StatusSince { get; init; }
    public DateOnly? RetiredOn { get; init; }
    public string? RetirementReason { get; init; }
    public OwnerDto Owner { get; init; } = null!;
    public VehicleInput Data { get; init; } = new();
    public CatalogRefDto? Brand { get; init; }
    public CatalogRefDto? VehicleClass { get; init; }
    public CatalogRefDto? BodyType { get; init; }
    public CatalogRefDto? Fuel { get; init; }
    public CatalogRefDto? TransitAuthority { get; init; }
    public CatalogRefDto? ServiceClass { get; init; }
    public CatalogRefDto? Modality { get; init; }
    public CatalogRefDto? RollingGroup { get; init; }
    public IReadOnlyList<VehicleEquipmentDto> Equipment { get; init; } = [];
    public IReadOnlyList<VehicleEventDto> History { get; init; } = [];
    public IReadOnlyList<VehicleDocumentDto> Documents { get; init; } = [];
    public IReadOnlyList<VehicleDriverDto> Drivers { get; init; } = [];
    public IReadOnlyList<VehicleSubscriptionDto> Subscriptions { get; init; } = [];
}

/// <summary>Un documento del vehículo con su situación a hoy (US4): vigente, por vencer, vencido; los reemplazados van en la historia.</summary>
public sealed record VehicleDocumentDto(
    Guid PublicId, Guid DocumentTypePublicId, string DocumentTypeCode, string DocumentTypeName, string Number, string? Issuer,
    DateOnly? IssuedOn, DateOnly? ValidFrom, DateOnly ExpiresOn, bool IsCurrent, string Situation, int DaysToExpire);

/// <summary>Un conductor asignado al vehículo, vigente o en la historia (US5).</summary>
public sealed record VehicleDriverDto(
    Guid DriverPublicId, Guid AssignmentPublicId, Guid PersonPublicId, string TaxId, string FullName, DateOnly From, DateOnly? To, bool IsPrimary, bool IsCurrent);

/// <summary>Una suscripción del vehículo a un servicio, con su precio pactado vigente (US6).</summary>
public sealed record VehicleSubscriptionDto(
    Guid PublicId, Guid ServicePublicId, string ServiceName, string ProductCode, DateOnly From, DateOnly? To, decimal? AgreedPrice, bool IsCurrent);

/// <summary>Lo que devuelve el alta de un vehículo.</summary>
public sealed record VehicleCreatedDto(Guid VehiclePublicId, Guid OwnerPersonPublicId);

/// <summary>Una deuda vencida del propietario saliente (FR-015): sólo se informa.</summary>
public sealed record OwnerDebtDto(string Source, string Reference, string Description, decimal OverdueAmount, int? DaysPastDue);

/// <summary>Las deudas vencidas de un propietario, con su total.</summary>
public sealed record OwnerDebtsDto(Guid PersonPublicId, string FullName, decimal Total, IReadOnlyList<OwnerDebtDto> Items, string? Note);
