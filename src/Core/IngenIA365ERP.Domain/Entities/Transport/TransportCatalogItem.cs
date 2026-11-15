using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Transport;

namespace IngenIA365ERP.Domain.Entities.Transport;

/// <summary>
/// Un valor de uno de los nueve catálogos de Transporte (<c>TRN_CatalogItems</c>; feature 020, R6; FR-024): marca, clase de
/// vehículo, carrocería, combustible, modalidad, clase de servicio, organismo de tránsito, grupo de rodamiento o tipo de equipo. El
/// código es la nomenclatura de la cooperativa (<c>CodigoDeCatalogo</c>), único por catálogo entre vivos. Se inactiva, no se borra.
/// </summary>
public class TransportCatalogItem : AuditableEntity
{
    public TransportCatalogKind Kind { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Lo sembró el sistema: se renombra o inactiva, no se borra.</summary>
    public bool IsSystem { get; set; }
}

/// <summary>
/// Un tipo de documento del vehículo (<c>TRN_DocumentTypes</c>; feature 020, R7; FR-020): SOAT, revisión técnico-mecánica, tarjeta de
/// operación, pólizas… Cada cooperativa decide si es obligatorio y con cuántos días avisa. Se inactiva, no se borra.
/// </summary>
public class TransportDocumentType : AuditableEntity
{
    public const int DiasDeAvisoMaximos = 365;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int WarningDays { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystem { get; set; }
}
