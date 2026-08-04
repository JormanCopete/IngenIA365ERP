# Corpus DDL congelado (feature 004-multi-motor-bd)

**Este directorio es REFERENCIA HISTÓRICA desde 2026-08.** Fue la fuente de
verdad del esquema SQL Server durante las Fases 0–1 (features 001–003).

Desde el feature **004-multi-motor-bd**, la fuente única de verdad del esquema
son las **migraciones EF Core por proveedor**:

- `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/`
- `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/`

Reglas:

1. **No agregar scripts nuevos aquí.** Todo cambio de esquema se hace en el
   modelo (entidades + `IEntityTypeConfiguration`) y se materializa con
   `.\tools\scripts\add-migration.ps1 -Name <X> -Context Application|Admin`
   (genera SIEMPRE el par SqlServer + PostgreSQL).
2. Los scripts para DBA se generan por release con
   `dotnet run --project tools/IngenIA365ERP.DbMigrator -- script` hacia
   [`generated/`](generated/) — idempotentes, con header estándar
   (principio constitucional XII).
3. Los archivos existentes documentan el esquema histórico y no deben
   ejecutarse sobre instalaciones nuevas (el inicializador de la API con
   `Database:AutoMigrate=true`, o `DbMigrator migrate`, aprovisionan todo).

Contexto completo: [`specs/004-multi-motor-bd/`](../../specs/004-multi-motor-bd/).
