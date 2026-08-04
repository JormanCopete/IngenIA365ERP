# Data Model — Soporte Multi-Motor de Base de Datos (Feature 004)

**Phase 1 output** · Branch `004-multi-motor-bd` · 2026-08-03

Este feature **no introduce entidades de dominio ni tablas de negocio nuevas**. Su "modelo de datos" son: (1) el modelo de configuración, (2) el contrato del framework de seeding, (3) el historial de migraciones por base/esquema, y (4) el inventario inicial de seeders.

---

## 1. Modelo de configuración — `DatabaseOptions` (sección `Database`)

```jsonc
{
  "Database": {
    "Provider": "PostgreSQL",              // "PostgreSQL" | "SqlServer" — case-insensitive. OBLIGATORIO.
    "ConnectionStrings": {
      "PostgreSQL": "Host=localhost;Port=5432;Database=ingenia365erp;Username=...;Password=...",
      "SqlServer": "Server=localhost;Database=IngenIA365ERP;Trusted_Connection=true;TrustServerCertificate=true"
      // Solo la del Provider activo es obligatoria (FR-004). Admin DB deriva con sufijo "_Admin"
      // salvo override explícito AdminConnectionStrings (misma estructura, opcional).
    },
    "AutoMigrate": true,                    // default: true en Development/QA, false en Production
    "Seed": {
      "RunParametricSeed": true,           // default: true en TODOS los ambientes
      "RunTestSeed": false                  // default: true en Development/QA, false en Production (opt-in explícito, FR-017)
    },
    "Startup": {
      "RetryWindowSeconds": 60,            // ventana total de espera de BD (D-05)
      "RetryIntervalSeconds": 5
    }
  }
}
```

### Reglas de validación (`IValidateOptions<DatabaseOptions>` + `ValidateOnStart`, D-01)

| Regla | Error (código namespaced, mensaje en español) |
|---|---|
| `Provider` ∈ {PostgreSQL, SqlServer} | `Database.InvalidProvider` — "Proveedor de base de datos '<x>' no soportado. Valores válidos: PostgreSQL, SqlServer." |
| Connection string presente y no vacía para el `Provider` activo | `Database.ConnectionStringMissing` — "Falta la cadena de conexión para el proveedor '<x>' (Database:ConnectionStrings:<x>)." |
| `RetryWindowSeconds` ≥ 0, `RetryIntervalSeconds` ≥ 1 | `Database.InvalidStartupOptions` |
| (runtime, no arranque) migraciones pendientes con `AutoMigrate=false` | `Database.MigrationsPending` — enumera migraciones (FR-011, fail-fast) |
| (runtime) seed sobre esquema desactualizado | `Database.Seed.SchemaOutdated` (FR-021) |

Precedencia: variables de entorno (`Database__Provider`, `Database__ConnectionStrings__PostgreSQL`, …) > `appsettings.{Environment}.json` > `appsettings.json`. Los defaults por ambiente se materializan en los `appsettings.{Development|QA|Production}.json` de ejemplo (contrato en `contracts/configuration.md`).

## 2. Contrato del framework de seeding (D-07)

```csharp
enum SeedCategory { Parametric, Test }
enum SeedScope    { Admin, Tenant }

interface IDataSeeder
{
    int Order { get; }                    // orden global de dependencias (menor = antes)
    SeedCategory Category { get; }
    SeedScope Scope { get; }
    // Solo para Category=Test: ambientes donde el default es "on" (Development, QA).
    // Parametric ignora esta lista (corre siempre que RunParametricSeed=true).
    string[] DefaultOnEnvironments { get; }
    Task SeedAsync(SeedContext context, CancellationToken ct);
}

// SeedContext: expone el DbContext del alcance en curso (AdminDbContext o
// ApplicationDbContext apuntado al esquema del tenant en curso), el TenantInfo
// (si Scope=Tenant), el ambiente y el logger.
```

### Invariantes

- **Idempotencia por clave natural**: cada seeder consulta por su clave (código/nombre único) e inserta solo lo faltante. NUNCA actualiza registros existentes (respeta personalizaciones del cliente — FR-016).
- **Transaccionalidad**: una transacción por seeder × alcance (un seeder Tenant = una transacción por tenant). Interrupción ⇒ rollback de la unidad en curso; re-ejecución completa lo pendiente (FR-018).
- **Marcas de auditoría**: `CreatedBy = "system:seed"` (paramétrico) / `"system:seed-demo"` (demo — marca reconocible FR-020). Resto de campos `AuditableEntity` poblados normalmente.
- **Orden**: el orquestador ordena por `Order` global; los seeders Tenant corren después de que su tenant tenga esquema al día (guard FR-021).
- **Aislamiento multi-tenant**: el orquestador itera `TenantDirectory` y abre el contexto de CADA tenant explícitamente; un seeder jamás toca dos esquemas en una misma operación (principio IV).

## 3. Historial de migraciones

| Ámbito | Tabla | Ubicación |
|---|---|---|
| BD administrativa | `__EFMigrationsHistory` | BD `IngenIA365ERP_Admin` (esquema default) |
| Esquema de tenant | `__EFMigrationsHistory` | dentro del esquema del tenant (permite detectar tenants desactualizados por separado — D-03) |

El árbol de migraciones vive por proveedor en `IngenIA365ERP.Persistence.Migrations.{SqlServer|PostgreSql}` con subcarpetas `Admin/` y `Application/` (D-02). El inicializador compara `pending = migraciones del ensamblado − historial` por base y por esquema de tenant.

## 4. Inventario inicial de seeders

| Order | Seeder | Category | Scope | Contenido |
|---|---|---|---|---|
| 10 | `SystemParametersSeeder` | Parametric | Admin | Parámetros globales del SaaS |
| 20 | `RolesSeeder` | Parametric | Tenant | Los 8 roles del modelo de seguridad |
| 30 | `PermissionsSeeder` | Parametric | Tenant | Los 112 permisos (Resource, Action) + asignaciones rol-permiso base |
| 40 | `CurrenciesSeeder` | Parametric | Tenant | COP + monedas de referencia |
| 50 | `DocumentTypesSeeder` | Parametric | Tenant | Tipos de documento de identidad (CC, NIT, CE, …) |
| 60 | `ChartOfAccountsSeeder` | Parametric | Tenant | Plan de cuentas base (PUC cooperativo) |
| 70 | `TenantParametersSeeder` | Parametric | Tenant | Parámetros operativos por cooperativa |
| 900 | `DemoDataSeeder` | Test | Tenant | Personas/asociados, productos, facturas y movimientos de ejemplo (insertados vía entidades de dominio; marca `system:seed-demo`) |

> El inventario definitivo se refina en `/speckit-tasks` contrastando con los seeders
> Phase 0 existentes (`DomainSecuritySeedData` y afines), que se **migran** a este
> framework en lugar de duplicarse.

## 5. Máquina de estados del inicializador (referencia)

```text
Validar opciones (ValidateOnStart)  ── inválido ──▶ FAIL (Database.InvalidProvider / ConnectionStringMissing)
        │ ok
Esperar BD (retry ventana D-05)     ── agotado ──▶ FAIL (Database.Unreachable)
        │ ok
Adquirir MigrationLock (D-04)
        │
AutoMigrate=true ──▶ Migrar Admin ▸ luego cada esquema tenant  ── error ──▶ FAIL (Database.MigrationFailed)
AutoMigrate=false y pendientes ──▶ FAIL (Database.MigrationsPending, enumeradas)
        │ esquema al día
RunParametricSeed ──▶ SeedOrchestrator (Admin ▸ tenants)
RunTestSeed efectivo ──▶ DemoDataSeeder (+ evento auditable si Production)
        │
Liberar lock ──▶ App lista (health "ready" en verde)
```
