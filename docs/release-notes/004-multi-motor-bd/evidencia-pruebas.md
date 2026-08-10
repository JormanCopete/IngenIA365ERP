# Evidencia de pruebas — Feature 004: Multi-Motor de Base de Datos

**Fecha**: 2026-08-04 · **Rama**: `004-multi-motor-bd` · **Entorno**: máquina dev
(Windows 11, Docker Desktop; PostgreSQL 17 en contenedor puerto 5433, SQL Server
nativo, Testcontainers para suites).

## SC-001 / SC-002 — Misma build, ambos motores, instalación desde cero

Ejecutado con el MISMO binario (`dotnet run --no-build`), cambiando solo
variables de entorno:

| Paso | PostgreSQL 17 (virgen) | SQL Server (BD virgen) |
|---|---|---|
| Validación de config (log con cadenas enmascaradas) | ✅ | ✅ (origen: variable de entorno) |
| Migración BD admin (16 tablas + historial) | ✅ `20260804125001_InitialSchema` | ✅ `20260804124946_InitialSchema` |
| Migración BD operativa (**286 tablas**) | ✅ | ✅ |
| Seed paramétrico (master admin + Phase 0 + catálogos) | ✅ | ✅ |
| `/health/ready` → Healthy (db-init + database + mongo + redis) | ✅ | ✅ |
| Login `master@ingenia.dev` → JWT RS256 | ✅ | ✅ |

Tiempo total instalación desde cero: **< 1 minuto** por motor (SC-002 ≪ 10 min).

## SC-004 — Fail-fast

- `Database__Provider=Oracle` → host no arranca, `Database.InvalidProvider` con valores válidos (test automatizado).
- Connection string faltante del provider activo → `Database.ConnectionStringMissing` nombrando la clave (test automatizado); la del provider NO activo nunca es obligatoria (FR-004, test).
- `AutoMigrate=false` + BD virgen → `Database.MigrationsPending` enumerando `admin: …InitialSchema | operativa: …InitialSchema` y sugiriendo scripts DBA (verificado en vivo, boot 7).

## SC-005 / SC-006 — Seeds

- Arranque Development: SystemParameters=5, Monedas=3, TiposDoc=6, PUC nivel 1=9, Demo=4 personas (`CreatedBy='system:seed-demo'`).
- `POST /api/saas/database/seed` (master JWT) re-ejecutado → **0 inserciones en todos los seeders** (idempotencia SC-006).
- Fixture de integración con `RunTestSeed=false` → **0 registros demo** (SC-005); test automatizado `Catalogos_parametricos_sembrados_y_demo_ausente`.

## SC-008 — Paridad

- `tools/scripts/check-migration-parity.ps1` → OK Admin (1 par), OK Application (1 par).
- `Architecture.Tests/Feature004_MigrationParity` (2 tests) + `Feature004_MultiProvider` (3 tests) en verde — 39/39 la suite Architecture.
- `Provisioning_SmokeTests` (esquema ≥286 tablas + catálogos + idempotencia) en verde con `DB_PROVIDER=PostgreSql` **y** `DB_PROVIDER=SqlServer` (Testcontainers, host real con inicializador).

## FR-012 — Scripts DBA

`DbMigrator script` generó los idempotentes con header estándar:
`database/schema/generated/PostgreSQL/{Admin,Application}_idempotent.sql` (25 KB / 592 KB)
y `.../SqlServer/...` (25 KB / 572 KB).

## Suites (sweep 2026-08-04)

- Domain.Tests: **80/80** · Application.Tests: **229/229** · Architecture.Tests: **39/39**.
- API.IntegrationTests, matriz `DB_PROVIDER` (Testcontainers, serial):
  - **PostgreSql: 54/65** · **SqlServer: 54/65** — los ~10 fallos son **IDÉNTICOS
    en ambos motores** ⇒ ninguno es específico de motor (**veredicto de paridad
    SC-003: cumplido para el alcance del 004**).
  - Los fallos comunes son deuda preexistente de la suite (T128 del 002 quedó
    abierta): (a) 5 tests legacy Fase 0 que esperan el contrato de login
    pre-cutover (LoginFlow, RefreshRotation, PermissionEnforcement ×2, perf
    p95 de auditoría); (b) tests de identidad multi-tenant que pasan AISLADOS
    (verificado con `--filter`) pero fallan en corrida completa por estado
    estático compartido entre hosts de test del mismo proceso (sospecha: la
    suscripción pub/sub estática de la caché de membresías). Registrado como
    tarea de saneamiento independiente.
  - Los tests NUEVOS del 004 (Provisioning_SmokeTests ×3, Startup_FailFastTests ×4)
    pasan en ambos motores.

## Pendientes al cierre de esta evidencia

- T033: test de integración del provisioning de tenant runtime por motor (cubierto
  funcionalmente por el flujo de MasterRegisterTenant en la matriz; test dedicado pendiente).
- T034: escenario manual de reintento con BD caída (quickstart §3a) — el caso
  Unreachable está cubierto por test automatizado.
- Saneamiento de la suite legacy (fuera del alcance 004 — tarea aparte).
