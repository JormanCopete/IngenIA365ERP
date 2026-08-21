# IngenIA365ERP

ERP Financiero SaaS para cooperativas colombianas. Migrado desde SOLIDO (VB.NET/WinForms) a una arquitectura moderna Clean Architecture con .NET 10.

## Tecnologia

- **Backend:** .NET 10, Minimal APIs (Carter), EF Core 10, CQRS (MediatR)
- **Frontend:** Blazor Hybrid MAUI + Blazor Server + Blazor WebAssembly
- **UI:** SyncFusion Blazor 33.1.44 (tema fluent2-dark)
- **BD Transaccional:** SQL Server (schema-per-tenant)
- **BD Auditoria:** MongoDB (batching, TTL 5 anos)
- **Cache:** Redis
- **Auth:** JWT RS256, RBAC con 112 permisos, 8 roles
- **Reportes:** QuestPDF (15 reportes PDF)
- **Testing:** xUnit + FluentAssertions + ArchUnitNET

## Requisitos

- .NET 10 SDK
- Docker Desktop
- SQL Server 2022+ (o via Docker)

## Inicio rapido

```bash
# 1. Levantar infraestructura con Docker
docker-compose up -d sqlserver mongodb redis

# 2. Inicializar BD (requiere sqlcmd)
cd tools/scripts
pwsh init-dev.ps1

# 3. Ejecutar API (terminal 1)
cd src/Presentation/IngenIA365ERP.API
dotnet run

# 4. Ejecutar Web (terminal 2)
cd src/Presentation/IngenIA365ERP.Web
dotnet run

# 5. Abrir en navegador
# API:  http://localhost:5000/swagger
# Web:  http://localhost:5001
```

### Con Docker Compose completo

```bash
docker-compose up -d
# API:  http://localhost:5000
# Web:  http://localhost:5001
# RabbitMQ Management: http://localhost:15672 (ingenia365/ingenia365dev)
```

## Estructura del proyecto

```
IngenIA365ERP.slnx
|
+-- src/
|   +-- Core/
|   |   +-- IngenIA365ERP.Domain/           -> Entidades, Value Objects, Eventos
|   |   +-- IngenIA365ERP.Application/      -> CQRS, Validacion, Interfaces
|   |
|   +-- Infrastructure/
|   |   +-- IngenIA365ERP.Persistence/      -> EF Core + SQL Server
|   |   +-- IngenIA365ERP.Identity/         -> Auth + JWT + Permisos
|   |   +-- IngenIA365ERP.Audit/            -> MongoDB
|   |   +-- IngenIA365ERP.Caching/          -> Redis
|   |   +-- IngenIA365ERP.Legacy/           -> Puente a ERP.Core (temporal)
|   |
|   +-- Presentation/
|       +-- IngenIA365ERP.API/              -> Minimal APIs (backend)
|       +-- IngenIA365ERP.Web/              -> Blazor Server
|       +-- IngenIA365ERP.Web.Client/       -> Blazor WASM
|       +-- IngenIA365ERP.Shared/           -> Componentes Blazor compartidos
|       +-- IngenIA365ERP.App/              -> MAUI Hybrid (desktop/movil)
|
+-- tests/
|   +-- IngenIA365ERP.Domain.Tests/
|   +-- IngenIA365ERP.Application.Tests/
|   +-- IngenIA365ERP.API.IntegrationTests/
|   +-- IngenIA365ERP.Architecture.Tests/
|
+-- tools/
    +-- IngenIA365ERP.DataMigrator/         -> Migracion de datos SOLIDO -> nuevo
    +-- IngenIA365ERP.DbMigrator/           -> Crear schemas por tenant
    +-- scripts/                            -> Scripts SQL e inicializacion
```

## Modulos

| Modulo | Prefijo BD | Descripcion |
|--------|-----------|-------------|
| Core | COR_ | Personas, sucursales, ciudades, bancos, parametros |
| Contabilidad | ACC_ | Plan de cuentas, comprobantes, movimientos, saldos |
| Cartera Financiera | LND_ | Creditos, ahorros, aportes, recaudos, mora |
| Nomina | PAY_ | Empleados, liquidacion, novedades, prestaciones |
| Inventario | INV_ | Productos, bodegas, movimientos, facturacion |
| CDT | CDT_ | Certificados de deposito a termino |
| Tarjeta Debito | DEB_ | Tarjetas, transacciones |
| Tesoreria | TRS_ | Cheques, facturas, flujo de caja |
| Seguridad | SEC_ | Usuarios, roles, permisos |
| Auditoria | AUD_ | Logs de auditoria y acceso |

## Multi-tenancy

- **Estrategia:** Schema-per-tenant en SQL Server
- **Resolucion:** Header `X-Tenant-Id` o subdominio
- **Admin:** BD separada `IngenIA365ERP_Admin` con tablas `ADM_*`
- **Herramienta:** `tools/IngenIA365ERP.DbMigrator` para crear/listar/eliminar tenants

## CI/CD

Pipeline en `.github/workflows/ci.yml`:
- Build + tests en cada push/PR
- Docker build + push a GHCR en merge a `main`

## Migracion desde SOLIDO

- **Fase 1:** 33 plugins VB.NET -> C# -> ERP.Core consolidado
- **Fase 2:** Rediseno BD (272 tablas), Clean Architecture, 382 archivos CQRS, 136 paginas Blazor, 15 reportes PDF
- **Fase 3:** Docker, CI/CD, scripts de inicializacion, tests de arquitectura
- **Documentacion:** `MAPEO-BD-VIEJO-NUEVO.md`, `ANALISIS-FORMULARIOS.md`
- **Migrador:** `tools/IngenIA365ERP.DataMigrator` para datos existentes

## Identidad central v2 (feature 002-identidad-central-federada)

A partir de v2 la autenticacion se rediseña: la credencial vive UNA SOLA VEZ en `IngenIA365ERP_Admin` (tablas `ADM_CentralUsers`, `ADM_TenantMemberships`, `ADM_Invitations`). El login ocurre en un dominio único sin combo box de cliente. Spec completa: [`specs/002-identidad-central-federada/`](specs/002-identidad-central-federada/).

### Variables de entorno requeridas

```bash
# Bootstrap del master admin (solo se aplica si ADM_CentralUsers esta vacia)
MASTER_ADMIN_EMAIL=master@tu-dominio.com
MASTER_ADMIN_PASSWORD=<password fuerte; minimo 12 chars>

# SMTP saliente — seccion "Smtp" de appsettings; estas variables la pisan.
# (La vieja seccion "EmailSender" no la lee nadie: fue eliminada.)
Smtp__Host=localhost                  # en dev: smtp4dev, que CAPTURA y no entrega
Smtp__Port=1025
Smtp__UseStartTls=false               # true contra el servidor real (puerto 587)
Smtp__Username=                       # vacio en dev; obligatorio contra el servidor real
Smtp__Password=                       # nunca en el repositorio: variable de entorno o Secret
Smtp__FromAddress=noresponder.ingenia365erp@notifica365.com
Smtp__FromName=No Responder IngenIA365 ERP

# Base de los enlaces que viajan DENTRO de esos correos (invitacion y reset).
# Si no se define, los enlaces salen apuntando a https://localhost:7200 desde
# cualquier ambiente, incluido produccion.
IdentityEmail__BaseUrl=http://localhost:5200

# Validación contra contraseñas comprometidas (HaveIBeenPwned)
PwnedPassword__Enabled=true
PwnedPassword__BaseUrl=https://api.pwnedpasswords.com
```

### Servicios Docker para desarrollo

`docker compose -f docker-compose.dev.yml up -d postgres redis smtp4dev` levanta el stack mínimo (el `docker-compose.yml` clásico trae MailHog en los mismos puertos).

> **En local el correo NO se entrega, y eso es lo correcto.** El destino por defecto en desarrollo es **smtp4dev**, cuya bandeja se ve en **http://localhost:8025**. smtp4dev *captura* los mensajes y no los reenvía a internet: si esperabas ver el correo de invitación en tu buzón real y no llegó, no hay nada roto — abrí `http://localhost:8025`. Para enviar de verdad hay que apuntar `Smtp__Host` al servidor real; ver [`docs/operaciones/correo-saliente.md`](docs/operaciones/correo-saliente.md).

### Bootstrap inicial

1. Aplicar DDL `database/schema/15a_*.sql` a `15e_*.sql` (orden) sobre BD virgen.
2. Aplicar `database/migration/16_Seed_Default_GlobalMasterAdmin.sql` (lee `MASTER_ADMIN_EMAIL` y `MASTER_ADMIN_PASSWORD`).
3. Bootstrap del audit log de MongoDB (idempotente):
   ```powershell
   $env:AUDIT_WRITER_PASSWORD='dev-writer'; $env:AUDIT_READER_PASSWORD='dev-reader'; dotnet run --project tools/IngenIA365ERP.DbMigrator -- audit-bootstrap --mongo-connection "mongodb://localhost:27017"
   ```
4. Arrancar la API. Login en `https://app.ingenia365.com` (o `https://localhost:5001` en dev) con las credenciales del master admin.

### Flujos disponibles

| User story | Rutas UI | Endpoints clave |
|------------|----------|-----------------|
| US1 Onboarding por invitación | `/auth/accept-invitation?token=...` | `POST /api/invitations/accept`, `GET .../preview`, `POST /api/tenants/{id}/invitations` |
| US2 Login centralizado | `/security/login`, `/security/mfa-challenge`, `/security/no-membership` | `POST /api/auth/login`, `POST /api/auth/mfa/verify`, `POST /api/auth/refresh`, `GET /api/auth/me` |
| Phase 4b Profile & Recovery | `/profile/mfa`, `/profile/password`, `/auth/forgot-password`, `/auth/reset-password` | `POST /api/profile/mfa/{enroll,confirm,disable}`, `POST /api/profile/password`, `POST /api/auth/password/{forgot,reset}` |
| US3 Multi-empresa | `/security/select-tenant`, `/profile/default-tenant` | `GET /api/sessions/active-tenants`, `POST /api/sessions/{select,switch}-tenant`, `PUT /api/profile/default-tenant` |
| US4 Admin de empresa | `/admin/tenant/{id}/members`, `/admin/tenant/{id}/mfa-policy` | `GET/POST /api/tenants/{id}/members/*`, `GET/PUT /api/tenants/{id}/mfa-policy` |
| US5 Master admin | `/saas/register-tenant`, `/saas/force-mfa-reset` | `POST /api/saas/tenants/with-admin`, `POST /api/saas/users/{id}/force-mfa-reset` |

### Background jobs (Phase 8)

Registrados como `IHostedService` y corren automáticamente con la API:

- **InvitationExpiryJob** — cada 1h: marca `Invitation Pending → Expired` las que pasaron `ExpiresAt`, revoca en cascada `TenantMembership` huérfanas en estado `Invited`.
- **PasswordResetTokenCleanupJob** — cada 6h: purga tokens consumidos o expirados con > 30 días.

### Load testing

NBomber scenario configurado para validar SLO de login (100 req/s × 5 min, p95 < 800 ms):

```bash
LOADTEST_BASE_URL=http://localhost:5000 \
RUN_LOAD_TESTS=1 \
dotnet test tests/IngenIA365ERP.Load.Tests --filter "FullyQualifiedName~LoginThroughput"
```

## Base de datos multi-motor (feature 004-multi-motor-bd)

El ERP corre sobre **PostgreSQL o SQL Server** eligiendo el motor por
configuracion — misma build, cero recompilacion. PostgreSQL es el default de
desarrollo y el motor de la modalidad SaaS; SQL Server es opcion on-premise.

```jsonc
"Database": {
  "Provider": "PostgreSQL",            // "PostgreSQL" | "SqlServer"
  "ConnectionStrings":      { "PostgreSQL": "...", "SqlServer": "..." },
  "AdminConnectionStrings": { "PostgreSQL": "...", "SqlServer": "..." }, // opcional (deriva _Admin)
  "AutoMigrate": true,                  // default: on en Dev/QA, off en Production
  "Seed": { "RunParametricSeed": true, "RunTestSeed": null },  // demo: on Dev/QA, off Prod (opt-in)
  "Startup": { "RetryWindowSeconds": 60, "RetryIntervalSeconds": 5 }
}
```

- **Variables de entorno** (ganan a appsettings): `Database__Provider`,
  `Database__ConnectionStrings__PostgreSQL`, `Database__AutoMigrate`, etc.
  Solo la cadena del proveedor **activo** es obligatoria.
- **Fail-fast**: provider invalido, cadena faltante o migraciones pendientes con
  `AutoMigrate=false` impiden el arranque con un error accionable (`Database.*`).
- **Motor local**: `docker compose -f docker-compose.dev.yml up -d postgres`
  (puerto **5433** — la maquina dev tiene un PostgreSQL nativo en 5432).
  SQL Server sigue siendo el servicio nativo de Windows.
- **Migraciones**: viven por proveedor en
  `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{SqlServer|PostgreSql}`
  y SIEMPRE se generan en par: `.\tools\scripts\add-migration.ps1 -Name X -Context Application|Admin`.
  El corpus DDL de `database/schema|migration` quedo congelado como referencia.
- Contratos completos: [`specs/004-multi-motor-bd/contracts/`](specs/004-multi-motor-bd/contracts/).

## Documentacion adicional

- [`docs/CONFIGURACION-Y-AUTENTICACION.md`](docs/CONFIGURACION-Y-AUTENTICACION.md) — AppMode (Mock/Api), tenant resolution, tablas Identity, seed, cadenas de conexion, registracion DI, troubleshooting.
