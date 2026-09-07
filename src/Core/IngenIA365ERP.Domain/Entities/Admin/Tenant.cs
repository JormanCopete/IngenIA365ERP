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

    /// <summary>
    /// Cadena de conexión propia, cuando la cooperativa vive fuera de la instancia
    /// por defecto. Null es lo normal: entonces se compone de la plantilla más
    /// <see cref="DatabaseName"/>.
    ///
    /// <para>
    /// Existe para que trasladar una cooperativa a su propio servidor sea un
    /// cambio de DATO y no de código, como exige el Principio IV.
    /// </para>
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Base de auditoría de esta cooperativa en MongoDB. El rastro regulatorio se
    /// aísla igual que los datos (Principio IV y Principio X).
    /// </summary>
    public string? AuditDatabaseName { get; set; }

    /// <summary>
    /// Base lógica de Redis asignada. Estable entre reinicios: se guarda, no se
    /// deriva, porque derivarla del Id la movería si la cooperativa se re-registra
    /// y el caché quedaría leyendo el espacio de otra.
    /// </summary>
    public int? RedisDbIndex { get; set; }

    /// <summary>
    /// Última migración aplicada a la base de esta cooperativa. El arranque la
    /// compara con el árbol del ensamblado para decidir si puede servirla.
    /// </summary>
    public string? MigrationsVersion { get; set; }

    /// <summary>
    /// Dónde está el aprovisionamiento: <c>Pending</c>, <c>Provisioning</c>,
    /// <c>Ready</c> o <c>Failed</c>.
    ///
    /// <para>
    /// Hace falta porque crear una base no es instantáneo ni infalible, y hasta
    /// ahora una cooperativa a medias era indistinguible de una lista: la fila
    /// existía en la consola y su base no. Con esto el fallo se ve.
    /// </para>
    /// </summary>
    public string ProvisioningState { get; set; } = "Pending";

    /// <summary>Por qué falló el aprovisionamiento, si falló. Para quien lo repare.</summary>
    public string? ProvisioningError { get; set; }

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
