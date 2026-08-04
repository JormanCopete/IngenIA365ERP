# Quickstart — Verificación end-to-end del Multi-Motor (Feature 004)

Recorrido de los 5 user stories y los criterios SC-001…SC-008 sobre el entorno local.
Prerrequisito: rama `004-multi-motor-bd` implementada; Docker disponible.

## 0. Levantar la orquestación dual (US5 / FR-024)

```powershell
docker compose -f docker-compose.dev.yml up -d postgres sqlserver
docker compose -f docker-compose.dev.yml ps   # ambos "healthy"
```

## 1. US1 — Instalación limpia por configuración (SC-001, SC-002)

```powershell
# 1a. PostgreSQL (default de Development — no requiere override)
dotnet run --project src/Presentation/IngenIA365ERP.API
# Esperado en el log: provider=PostgreSQL · validación OK · migraciones aplicadas (lista)
#                     · seed paramétrico admin+tenants · "Now listening"
# Verificar: curl http://localhost:5100/health/ready → Healthy
#            login master (curl POST /api/auth/login) → challenge NoActiveMembership

# 1b. Misma build sobre SQL Server (solo configuración)
$env:Database__Provider = 'SqlServer'
dotnet run --project src/Presentation/IngenIA365ERP.API
# Esperado: misma secuencia contra SQL Server. Medir tiempo total 1a/1b < 10 min (SC-002).
Remove-Item Env:Database__Provider
```

## 2. Fail-fast de configuración (US1 / SC-004)

```powershell
$env:Database__Provider = 'Oracle'
dotnet run --project src/Presentation/IngenIA365ERP.API
# Esperado: exit != 0, mensaje Database.InvalidProvider con valores válidos; nada escuchando.

$env:Database__Provider = 'PostgreSQL'
$env:Database__ConnectionStrings__PostgreSQL = ''
dotnet run --project src/Presentation/IngenIA365ERP.API
# Esperado: Database.ConnectionStringMissing nombrando la clave exacta.
Remove-Item Env:Database__Provider, Env:Database__ConnectionStrings__PostgreSQL
```

## 3. US2 — Resiliencia y protección de producción (SC-007)

```powershell
# 3a. BD caída al arranque → reintentos → recuperación
docker compose -f docker-compose.dev.yml stop postgres
Start-Job { Start-Sleep 20; docker compose -f docker-compose.dev.yml start postgres }
dotnet run --project src/Presentation/IngenIA365ERP.API
# Esperado: Warnings de reintento cada 5s (credenciales enmascaradas) → arranque normal al volver la BD.

# 3b. AutoMigrate=false con migraciones pendientes → fail-fast enumerándolas
#     (sobre una BD recién creada sin migrar)
$env:Database__AutoMigrate = 'false'
dotnet run --project src/Presentation/IngenIA365ERP.API
# Esperado: Database.MigrationsPending con la lista y la sugerencia (scripts DBA o AutoMigrate).
Remove-Item Env:Database__AutoMigrate

# 3c. Scripts para DBA
dotnet run --project tools/IngenIA365ERP.DbMigrator -- script --provider PostgreSQL
# Esperado: SQL idempotente en database/schema/generated/PostgreSql/ con header estándar.
```

## 4. US3 — Seed paramétrico idempotente (SC-006)

```powershell
# Ejecutar 3 veces y comparar conteos de catálogos (roles=8, permisos=112, monedas, PUC)
1..3 | % { dotnet run --project tools/IngenIA365ERP.DbMigrator -- seed --category parametric --scope all }
# Esperado: ejecución 1 inserta; 2 y 3 reportan 0 inserciones; conteos idénticos.
# Borrar una moneda a mano y re-ejecutar → se restaura solo la faltante, sin tocar el resto.
```

## 5. US4 — Seed demo por ambiente

```powershell
# Development: demo on por defecto → existen clientes/facturas con CreatedBy='system:seed-demo'
# Simular producción:
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet run --project src/Presentation/IngenIA365ERP.API
# Esperado: log "seed de pruebas omitido por política de ambiente"; cero filas demo (SC-005).

$env:Database__Seed__RunTestSeed = 'true'
dotnet run --project src/Presentation/IngenIA365ERP.API
# Esperado: demo cargado + evento auditable Database.Seed.TestSeedEnabledInProduction en Mongo.
Remove-Item Env:ASPNETCORE_ENVIRONMENT, Env:Database__Seed__RunTestSeed
```

## 6. Endpoint master (FR-019) — con JWT de master admin

```powershell
curl -s http://localhost:5100/api/saas/database/status -H "Authorization: Bearer $master"
# Esperado: provider activo, migraciones aplicadas/pendientes por admin y por tenant, flags de seed.

curl -s -X POST http://localhost:5100/api/saas/database/seed `
  -H "Authorization: Bearer $master" -H "Content-Type: application/json" `
  -d '{"category":"Parametric","scope":"All"}'
# Esperado: 200 con seedersRun; evento Database.Seed.Executed en Mongo (actor=master).
```

## 7. US5 — Paridad (SC-003, SC-008)

```powershell
# Suites existentes en matriz de proveedor (Testcontainers)
$env:DB_PROVIDER='PostgreSql'; dotnet test tests/IngenIA365ERP.API.IntegrationTests
$env:DB_PROVIDER='SqlServer';  dotnet test tests/IngenIA365ERP.API.IntegrationTests
# Esperado: 100% verde en ambos. Incluye Provisioning_SmokeTests (todas las tablas por motor),
# Seed_IdempotencyTests y Startup_FailFastTests.

# Paridad de migraciones: renombrar/omitir una migración de un ensamblado → el check de CI falla.
```

## 8. Alta de tenant en runtime (FR-014, FR-019a)

Con el master: `POST /api/saas/tenants/with-admin` (flujo existente del 002) →
verificar que el esquema del tenant nuevo se crea en el motor activo, su
`__EFMigrationsHistory` queda al día y sus catálogos paramétricos sembrados;
la invitación del primer admin llega por SMTP como siempre.
