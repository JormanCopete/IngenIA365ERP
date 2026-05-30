using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>Maps to [dbo].[ADM_Tenants].</summary>
public class Tenant : AuditableEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SchemaName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Subdomain { get; set; }

    [MaxLength(50)]
    public string PlanType { get; set; } = "Basic";

    // T034 — Identidad legal de la cooperativa (NIT y razón social colombiana).
    // Obligatorios para la facturación y los reportes SARLAFT que el módulo
    // de auditoría deja firmados; opcionales mientras la fila no esté
    // activada para preservar idempotencia en backfills.
    [MaxLength(20)]
    public string? Nit { get; set; }

    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(300)]
    public string? LegalAddress { get; set; }

    // Régimen tributario (Común, Simple, RégimenSimplificado, Especial, etc.).
    // Valores libres por ahora; un catálogo (`COR_TaxRegimes`) se introducirá en módulo Contabilidad.
    [MaxLength(50)]
    public string? TaxRegime { get; set; }

    public bool IsActive { get; set; } = true;
    public int MaxUsers { get; set; } = 10;
    public long StorageLimitMb { get; set; } = 5120;

    [MaxLength(100)]
    public string? DatabaseName { get; set; }

    [MaxLength(200)]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? ContactPhone { get; set; }

    public DateTime? ActivatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }

    // Navigation
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<TenantSetting> Settings { get; set; } = [];
}
