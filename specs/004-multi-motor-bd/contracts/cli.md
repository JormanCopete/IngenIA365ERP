# Contract — CLI y comandos de desarrollador (Feature 004)

## `tools/IngenIA365ERP.DbMigrator` (comandos nuevos)

El DbMigrator existente (hoy: `audit-bootstrap`) suma tres comandos. Todos leen la sección
`Database` (mismas reglas de validación y enmascaramiento que la API) y aceptan
`--provider` para override puntual.

```bash
# Aplicar migraciones pendientes (admin + todos los tenants, o filtrado)
dotnet run --project tools/IngenIA365ERP.DbMigrator -- migrate [--provider PostgreSQL|SqlServer] [--scope admin|tenants|all] [--tenant <publicId>]

# Ejecutar seeds bajo demanda
dotnet run --project tools/IngenIA365ERP.DbMigrator -- seed --category parametric|test [--scope admin|tenant|all] [--tenant <publicId>] [--confirm-test-seed]

# Generar scripts SQL idempotentes para DBA (por proveedor, hacia database/schema/generated/)
dotnet run --project tools/IngenIA365ERP.DbMigrator -- script --provider PostgreSQL|SqlServer [--from <migración>] [--output <ruta>]
```

Salidas: exit code 0 solo si todo OK; logs con migraciones/seeders ejecutados;
`Database.MigrationsPending`/`Database.Seed.SchemaOutdated` respetan los mismos códigos del runtime.

## Recetas `dotnet ef` por proveedor (desarrollo)

Las design-time factories leen `DB_PROVIDER` (default `PostgreSql`). **Toda migración se genera
para AMBOS proveedores** — la omisión la detecta el smoke de paridad (SC-008).

```bash
# Nueva migración (ejemplo: AddLoanRestructuring) — SIEMPRE en par:
DB_PROVIDER=PostgreSql dotnet ef migrations add AddLoanRestructuring \
  --context ApplicationDbContext \
  --project src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql \
  --startup-project src/Presentation/IngenIA365ERP.API \
  --output-dir Application

DB_PROVIDER=SqlServer dotnet ef migrations add AddLoanRestructuring \
  --context ApplicationDbContext \
  --project src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer \
  --startup-project src/Presentation/IngenIA365ERP.API \
  --output-dir Application
```

Mismo patrón con `--context AdminDbContext --output-dir Admin` para la BD administrativa.
`migrations remove`, `database update` y `migrations script --idempotent` siguen la misma
convención de proyecto + `DB_PROVIDER`. Se entrega un script PowerShell
(`tools/scripts/add-migration.ps1 -Name X -Context Application|Admin`) que ejecuta el par
completo para eliminar el error humano.

## CI (estrategia de versionado de migraciones)

- Gate de PR: build + suites en matriz `DB_PROVIDER={PostgreSql,SqlServer}` (Testcontainers).
- Verificación de paridad: por cada migración nueva en un ensamblado debe existir la homóloga
  (mismo nombre lógico) en el otro; diferencia ⇒ falla el pipeline (SC-008).
- Release: `script --idempotent` por proveedor publicado como artefacto + copiado a
  `database/schema/generated/{provider}/` con header estándar (principio XII).
