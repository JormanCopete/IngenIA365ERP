using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding;

public enum SeedCategory
{
    /// <summary>Datos maestros imprescindibles — corren en TODO ambiente (FR-015).</summary>
    Parametric,

    /// <summary>Datos demo — on por default en Development/QA, opt-in en Production (FR-017).</summary>
    Test
}

public enum SeedScope
{
    /// <summary>BD administrativa central (AdminDbContext).</summary>
    Admin,

    /// <summary>Esquema operativo de cada tenant (ApplicationDbContext por esquema).</summary>
    Tenant
}

/// <summary>
/// Contrato del framework de seeding (feature 004 — data-model §2).
/// Invariantes que TODO seeder debe respetar:
///  - Idempotencia por clave natural: insertar solo lo faltante; NUNCA
///    actualizar registros existentes (FR-016).
///  - Marca de auditoria: CreatedBy = "system:seed" (Parametric) /
///    "system:seed-demo" (Test — FR-020).
///  - Sin SQL crudo dependiente de motor (FR-005).
/// La transaccionalidad por seeder×alcance la garantiza el orquestador.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Orden global de dependencias (menor = antes).</summary>
    int Order { get; }

    SeedCategory Category { get; }

    SeedScope Scope { get; }

    /// <summary>Siembra lo faltante y devuelve cuantas filas inserto.</summary>
    Task<int> SeedAsync(SeedContext context, CancellationToken ct);
}

/// <summary>Contexto de ejecucion que el orquestador entrega a cada seeder.</summary>
public sealed class SeedContext
{
    /// <summary>Disponible cuando Scope = Admin.</summary>
    public AdminDbContext? Admin { get; init; }

    /// <summary>Disponible cuando Scope = Tenant — ya apuntado al esquema en curso.</summary>
    public ApplicationDbContext? TenantDb { get; init; }

    /// <summary>Tenant en curso (null para el esquema default "dbo").</summary>
    public ErpTenantInfo? Tenant { get; init; }

    public required string EnvironmentName { get; init; }

    public required ILogger Logger { get; init; }

    public const string ParametricCreatedBy = "system:seed";
    public const string DemoCreatedBy = "system:seed-demo";
}
