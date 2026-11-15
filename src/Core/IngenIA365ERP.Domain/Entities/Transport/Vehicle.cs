using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Transport;

namespace IngenIA365ERP.Domain.Entities.Transport;

/// <summary>
/// Un vehículo del parque de la cooperativa (<c>TRN_Vehicles</c>; feature 020, R3; FR-005 a FR-012; data-model §2). Placa, número
/// interno y código radio-móvil son únicos entre vivos (no retirados ni borrados). <b>El estado y el propietario sólo cambian por sus
/// métodos</b> (<see cref="CambiarEstado"/>, <see cref="Retirar"/>, <see cref="CambiarPropietario"/>), que llaman sus comandos con
/// historia; la edición de la ficha no los toca (FR-011). Retirado es terminal: el reingreso es un vehículo nuevo (FR-010).
/// </summary>
public class Vehicle : AuditableEntity
{
    // === Identificación ===
    public string Plate { get; set; } = string.Empty;
    public string InternalNumber { get; set; } = string.Empty;

    /// <summary>El número que identifica al vehículo ante el sistema externo (el móvil); opcional.</summary>
    public string? RadioCode { get; set; }

    // === Propietario y estado (sólo por sus métodos) ===
    public int OwnerPersonId { get; private set; }
    public Person? OwnerPerson { get; private set; }
    public DateOnly OwnerSince { get; private set; }
    public VehicleStatus Status { get; private set; } = VehicleStatus.Active;
    public DateOnly StatusSince { get; private set; }
    public DateOnly? RetiredOn { get; private set; }
    public string? RetirementReason { get; private set; }

    // === Ficha técnica ===
    public int? BrandId { get; set; }
    public TransportCatalogItem? Brand { get; set; }
    public string? Line { get; set; }
    public short? ModelYear { get; set; }
    public int? VehicleClassId { get; set; }
    public TransportCatalogItem? VehicleClass { get; set; }
    public int? BodyTypeId { get; set; }
    public TransportCatalogItem? BodyType { get; set; }
    public string? Color { get; set; }
    public int? EngineDisplacementCc { get; set; }
    public int? FuelId { get; set; }
    public TransportCatalogItem? Fuel { get; set; }
    public short? PassengerCapacity { get; set; }
    public string? EngineNumber { get; set; }
    public string? ChassisNumber { get; set; }
    public string? Vin { get; set; }
    public string? TransitLicenseNumber { get; set; }
    public int? TransitAuthorityId { get; set; }
    public TransportCatalogItem? TransitAuthority { get; set; }
    public DateOnly? RegistrationDate { get; set; }
    public int? ServiceClassId { get; set; }
    public TransportCatalogItem? ServiceClass { get; set; }
    public int? ModalityId { get; set; }
    public TransportCatalogItem? Modality { get; set; }

    // === Vinculación ===
    public DateOnly JoinedOn { get; set; }
    public string? AffiliationContractNumber { get; set; }
    public DateOnly? AffiliationContractFrom { get; set; }
    public DateOnly? AffiliationContractTo { get; set; }
    public string? OperationCardNumber { get; set; }
    public int? RollingGroupId { get; set; }
    public TransportCatalogItem? RollingGroup { get; set; }

    /// <summary>Mínimo de rodamiento diario: dato informativo, sin control ni cálculo (FR-007).</summary>
    public decimal? MinDailyRolling { get; set; }

    /// <summary>Mínimo de rodamiento mensual: dato informativo, sin control ni cálculo (FR-007).</summary>
    public decimal? MinMonthlyRolling { get; set; }

    /// <summary>La oficina (sucursal contable) del vehículo, para el alcance de sucursal; la propuesta es la principal.</summary>
    public int? BranchId { get; set; }

    public ICollection<VehicleEvent> Events { get; init; } = [];
    public ICollection<VehicleEquipment> Equipment { get; init; } = [];

    /// <summary>Un vehículo nuevo, Activo desde su ingreso, con su propietario.</summary>
    public static Vehicle Registrar(int ownerPersonId, DateOnly joinedOn)
    {
        if (ownerPersonId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerPersonId), "Todo vehículo vivo tiene propietario.");
        return new Vehicle
        {
            OwnerPersonId = ownerPersonId,
            OwnerSince = joinedOn,
            Status = VehicleStatus.Active,
            StatusSince = joinedOn,
            JoinedOn = joinedOn,
        };
    }

    /// <summary>Un vehículo nuevo con un propietario que todavía no se guarda (alta con persona nueva en un paso).</summary>
    public static Vehicle Registrar(Person propietario, DateOnly joinedOn)
    {
        ArgumentNullException.ThrowIfNull(propietario);
        return new Vehicle
        {
            OwnerPerson = propietario,
            OwnerPersonId = propietario.Id,
            OwnerSince = joinedOn,
            Status = VehicleStatus.Active,
            StatusSince = joinedOn,
            JoinedOn = joinedOn,
        };
    }

    public bool EstaRetirado => Status == VehicleStatus.Retired;

    /// <summary>Activa o inactiva (FR-009). No retira: para eso está <see cref="Retirar"/>.</summary>
    public void CambiarEstado(VehicleStatus nuevo, DateOnly desde)
    {
        if (EstaRetirado) throw new InvalidOperationException("Un vehículo retirado no cambia de estado; el reingreso es un vehículo nuevo.");
        if (nuevo is not (VehicleStatus.Active or VehicleStatus.Inactive))
            throw new ArgumentOutOfRangeException(nameof(nuevo), "Activar o inactivar; retirar es otra operación.");
        Status = nuevo;
        StatusSince = desde;
    }

    /// <summary>Retira (desvincula) el vehículo: libera placa, número interno y radio-móvil (FR-010).</summary>
    public void Retirar(DateOnly desde, string motivo)
    {
        if (EstaRetirado) throw new InvalidOperationException("El vehículo ya está retirado.");
        Status = VehicleStatus.Retired;
        StatusSince = desde;
        RetiredOn = desde;
        RetirementReason = motivo.Trim();
    }

    /// <summary>Cambia el propietario desde una fecha efectiva (FR-014).</summary>
    public void CambiarPropietario(int nuevoPropietarioId, DateOnly desde)
    {
        if (EstaRetirado) throw new InvalidOperationException("Un vehículo retirado no cambia de propietario.");
        if (nuevoPropietarioId <= 0) throw new ArgumentOutOfRangeException(nameof(nuevoPropietarioId));
        OwnerPersonId = nuevoPropietarioId;
        OwnerPerson = null;
        OwnerSince = desde;
    }
}

/// <summary>
/// Un hecho de la historia del vehículo (<c>TRN_VehicleEvents</c>; feature 020, R3; FR-009, FR-014): registro, cambio de estado o de
/// propietario, con fecha efectiva, valores anterior y nuevo, motivo y de dónde vino. En el cambio de propietario sella la clase de
/// vinculación de cada lado en esa fecha. No se modifica ni se borra.
/// </summary>
public class VehicleEvent : AuditableEntity, IHechoInmutable
{
    public int VehicleId { get; init; }
    public Vehicle? Vehicle { get; init; }
    public VehicleEventKind Kind { get; init; }
    public DateOnly EffectiveDate { get; init; }

    /// <summary>El estado anterior, o el <c>PublicId</c> del propietario anterior.</summary>
    public string? FromValue { get; init; }

    /// <summary>El estado nuevo, o el <c>PublicId</c> del propietario nuevo.</summary>
    public string? ToValue { get; init; }

    public AffiliationClass? FromAffiliation { get; init; }
    public AffiliationClass? ToAffiliation { get; init; }
    public string? Reason { get; init; }
    public ChangeSource Source { get; init; } = ChangeSource.Screen;

    /// <summary>Quién hizo el cambio (nombre visible).</summary>
    public string? ChangedByName { get; init; }
}

/// <summary>
/// Un equipo instalado en el vehículo (<c>TRN_VehicleEquipment</c>; FR-008): taxímetro o registradora, radio, GPS, usuario de app u
/// otro tipo del catálogo, con su código (serie, usuario…) y estado.
/// </summary>
public class VehicleEquipment : AuditableEntity
{
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int EquipmentTypeId { get; set; }
    public TransportCatalogItem? EquipmentType { get; set; }
    public string? Code { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Active;
}
