using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Transport;

namespace IngenIA365ERP.Domain.Entities.Transport;

/// <summary>
/// Un documento del vehículo (<c>TRN_VehicleDocuments</c>; feature 020, R7; FR-021): tipo, número, emisor o aseguradora, fechas y
/// vencimiento. Hay uno vigente por tipo: registrar otro del mismo tipo marca éste con <see cref="SupersededAt"/> y queda en la
/// historia. Se edita sólo para corregir; corregir no crea historia. Un vencimiento no bloquea nada (FR-023).
/// </summary>
public class VehicleDocument : AuditableEntity
{
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int DocumentTypeId { get; set; }
    public TransportDocumentType? DocumentType { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly ExpiresOn { get; set; }

    /// <summary>Cuándo lo reemplazó otro del mismo tipo; nulo = vigente.</summary>
    [EscrituraUnica]
    public DateTime? SupersededAt { get; set; }

    public bool EsVigente => SupersededAt is null;
}

/// <summary>
/// Una persona registrada como conductor (<c>TRN_Drivers</c>; feature 020, R5; FR-018, FR-019). Su marca <c>Person.IsDriver</c> la
/// escribe sólo <c>RolDeConductor</c>, nunca una casilla de la persona. Una persona tiene a lo sumo un registro vivo.
/// </summary>
public class Driver : AuditableEntity
{
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public DriverStatus Status { get; set; } = DriverStatus.Active;
    public DateOnly Since { get; set; }
    public DateOnly? RetiredOn { get; set; }

    public ICollection<DriverAssignment> Assignments { get; init; } = [];
}

/// <summary>
/// La asignación de un conductor a un vehículo con fechas (<c>TRN_DriverAssignments</c>; FR-018). Un vehículo puede tener varios
/// conductores vigentes y a lo sumo un titular; un conductor no se asigna dos veces al mismo vehículo con vigencias cruzadas.
/// </summary>
public class DriverAssignment : AuditableEntity
{
    public int DriverId { get; set; }
    public Driver? Driver { get; set; }
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public DateOnly From { get; set; }
    public DateOnly? To { get; set; }
    public bool IsPrimary { get; set; }

    public bool VigenteEn(DateOnly fecha) => From <= fecha && (To is null || To >= fecha);
}
