# CI multi-motor (feature 004)

Estrategia de verificación de paridad PostgreSQL / SQL Server (contracts/cli.md, SC-003/SC-008).

## Gates de PR

1. **Build** de la solución (`dotnet build IngenIA365ERP.slnx`).
2. **Paridad de migraciones**: `tools/scripts/check-migration-parity.ps1`
   (también corre como test en `Architecture.Tests/Feature004_MigrationParity`).
   Una migración generada para un solo proveedor rompe el gate.
3. **Suites unit/arquitectura**: `dotnet test tests/IngenIA365ERP.Application.Tests`
   + `tests/IngenIA365ERP.Architecture.Tests`.
4. **Matriz de integración** (Testcontainers, requiere Docker):

```yaml
strategy:
  matrix:
    db_provider: [PostgreSql, SqlServer]
steps:
  - run: dotnet test tests/IngenIA365ERP.API.IntegrationTests
    env:
      DB_PROVIDER: ${{ matrix.db_provider }}
```

## Release

- `dotnet run --project tools/IngenIA365ERP.DbMigrator -- script --provider PostgreSQL`
  y `--provider SqlServer` → artefactos idempotentes publicados y copiados a
  `database/schema/generated/{provider}/` (header con contexto/reversión/backup —
  principio XII). Son los scripts que aplica el DBA en producción
  (`Database:AutoMigrate=false`).

## Reglas para desarrolladores

- Toda migración nueva se genera EN PAR: `.\tools\scripts\add-migration.ps1 -Name X -Context Application|Admin`.
- `UseSqlServer`/`UseNpgsql` solo viven en `Persistence/Providers` (blindado por Architecture.Tests).
- Diferencias por motor → `ProviderModelConventions`, nunca en una configuration individual.
