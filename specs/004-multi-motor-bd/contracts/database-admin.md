# Contract — Endpoints administrativos de base de datos (Feature 004)

Módulo Carter `DatabaseAdminModule` (`src/Presentation/IngenIA365ERP.API/Modules/DatabaseAdminModule.cs`).
Ambos endpoints: `[RequireMasterAdmin]` + `purpose == full`. Solo reenvían a `ISender` (principio III);
pasan por los 4 pipeline behaviors (auditoría incluida, principio X).

---

## POST /api/saas/database/seed

Ejecuta seeding bajo demanda (FR-019). Comando: `RunDatabaseSeedCommand`.

### Request

```jsonc
{
  "category": "Parametric",          // "Parametric" | "Test" — obligatorio
  "scope": "Tenant",                 // "Admin" | "Tenant" | "All" — obligatorio
  "tenantPublicId": "<guid|null>",   // opcional: limita a un tenant (solo scope Tenant)
  "confirmTestSeed": false           // obligatorio=true cuando category=Test en ambiente Production
}
```

### Validaciones (FluentValidation, códigos namespaced)

| Código | Regla |
|---|---|
| `Database.Seed.InvalidCategory` / `InvalidScope` | Valores fuera de enum |
| `Database.Seed.TenantNotFound` | `tenantPublicId` sin tenant activo |
| `Database.Seed.ConfirmationRequired` | `category=Test` en Production sin `confirmTestSeed=true` |
| `Database.Seed.SchemaOutdated` | Migraciones pendientes en el alcance pedido (FR-021) → 409 |

### Responses

| HTTP | Cuerpo |
|---|---|
| 200 | `{ "seedersRun": [ { "name", "scope", "tenants": n, "inserted": n } ], "durationMs": n }` |
| 400 | Error envelope con código de validación |
| 403 | No master / purpose inválido |
| 409 | `Database.Seed.SchemaOutdated` con migraciones pendientes enumeradas |

### Auditoría

Evento `Database.Seed.Executed` (Mongo, TTL 5 años): actor (centralUserId, email), categoría,
alcance, tenant(es) afectados, conteos, resultado, IP/UA.

---

## GET /api/saas/database/status

Consulta de estado para operación (soporta la verificación de FR-011/SC-004 sin acceso al servidor).
Query: `GetDatabaseStatusQuery` → `IDatabaseStatusReader`.

### Response 200

```jsonc
{
  "provider": "PostgreSQL",
  "autoMigrate": false,
  "admin":   { "migrationsApplied": 12, "migrationsPending": [] },
  "tenants": [
    { "tenantPublicId": "<guid>", "name": "Coop. Solidaria", "migrationsPending": [] }
  ],
  "seed": { "runParametricSeed": true, "runTestSeed": false }
}
```

Notas: nunca incluye connection strings ni credenciales; los tenants se listan desde el
directorio oficial (`TenantDirectory`), cada uno consultado por separado (principio IV).
