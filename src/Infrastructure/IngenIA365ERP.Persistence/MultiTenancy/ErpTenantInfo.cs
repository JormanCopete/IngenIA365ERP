using System.ComponentModel.DataAnnotations.Schema;
using Finbuckle.MultiTenant.Abstractions;

namespace IngenIA365ERP.Persistence.MultiTenancy;

public class ErpTenantInfo : ITenantInfo
{
    public int InternalId { get; set; }
    public Guid PublicId { get; set; }
    public string? Identifier { get; set; }
    public string? Name { get; set; }
    public string? ConnectionString { get; set; }
    public string SchemaName { get; set; } = "dbo";

    /// <summary>
    /// Base física de la cooperativa (Principio IV). Puede venir null en filas
    /// anteriores al modelo de base por cooperativa; entonces manda SchemaName.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// "Provisioning" | "Ready" | "Failed" (ver EstadoDeAprovisionamiento).
    /// Una cooperativa registrada no es todavía una cooperativa usable: entre el
    /// alta y el final del aprovisionamiento su base existe sin tablas, y un
    /// trabajo de fondo que la recorra revienta con «relation does not exist».
    /// </summary>
    public string? ProvisioningState { get; set; }

    public string LicenseType { get; set; } = "Basic";
    public bool IsActive { get; set; } = true;
    public int MaxUsers { get; set; } = 10;
    public DateTime? ExpirationDate { get; set; }
    public DateTime CreatedAt { get; set; }

    // Legacy/Finbuckle compatibility shims — not stored, exposed for ITenantInfo
    // and code that still references Schema/PlanType.
    private string? _idOverride;

    [NotMapped]
    public string? Id
    {
        get => _idOverride ?? Identifier;
        set => _idOverride = value;
    }

    [NotMapped]
    public string Schema
    {
        get => SchemaName;
        set => SchemaName = value;
    }

    [NotMapped]
    public string? PlanType
    {
        get => LicenseType;
        set => LicenseType = value ?? "Basic";
    }
}
