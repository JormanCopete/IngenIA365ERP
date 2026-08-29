# Configuracion y Autenticacion

> ## ⚠️ Documento parcialmente superado — mirá este mapa antes de seguirlo
>
> Al 2026-08-25, **la mitad describe un sistema que ya no existe**. La
> constitución v2.0.0 lo señaló como el documento más desfasado del repositorio.
> No se reescribe entero porque las secciones de configuración siguen siendo
> útiles y correctas; se marca sección por sección para que nadie siga un paso
> que ya no funciona.
>
> | Sección | Estado |
> |---|---|
> | 1 · Modos del frontend (AppMode) | **Vigente** |
> | 2 · Multi-tenant y resolución | **Parcial.** El aislamiento ya no es por esquema sino **una base de datos por cooperativa** (Principio IV). La tabla de cooperativas y el middleware siguen como se describen. |
> | 3 · Tablas de Identity y Seed | **Superada.** Ver la corrección en la propia sección. |
> | 4 · Cadenas de conexión · 5 · DI · 8 · PaginationParams | **Vigentes** |
> | 6 · `AuthResponse` · 7 · `LoginRequest` | **Superadas.** Son el contrato de Fase 0. El vivo es [specs/002/contracts/auth.md](../specs/002-identidad-central-federada/contracts/auth.md): el cuerpo es `{email, password}`, sin tenant ni username, y el login puede devolver un *challenge* en vez de una sesión. |
> | 9 · Inicio rápido | **Superada.** Usá el de [README.md](../README.md). |
> | 10 · Problemas frecuentes | **Parcial**, referida a tablas de Fase 0. |
>
> Y dos cosas que este documento da por buenas y hoy no lo son: el
> administrador maestro **necesita segundo factor** para entrar, y los fallos de
> autenticación responden **401**, no 422.

Guia completa para levantar el sistema, cambiar entre modos (DEV-Mock / DEV-Api / QA / PDN) y entender el flujo de autenticacion multi-tenant.

---

## 1. Modos de ejecucion del frontend (AppMode)

El frontend (Web, Web.Client WASM, App MAUI) decide entre `MockAuthService` y `AuthService` real **leyendo configuracion**, no recompilando.

### Schema en `appsettings.json`

```json
"AppMode": {
  "Environment": "Development",
  "DataSource":  "Mock",
  "ApiBaseUrl":  "http://localhost:5100"
}
```

| Campo | Valores | Significado |
|-------|---------|-------------|
| `Environment` | `Development` \| `QA` \| `Production` | Solo etiqueta para logs/UI. No determina origen de datos. |
| `DataSource` | `Mock` \| `Api` | `Mock` -> `MockAuthService` (no toca red). `Api` -> `AuthService` real contra `ApiBaseUrl`. |
| `ApiBaseUrl` | URL | Base del HttpClient cuando `DataSource = Api`. |

> Importante: la propiedad es `DataSource` (no `DateSource`). Cualquier typo cae al default `Mock`.

### Implementacion

`src/Presentation/IngenIA365ERP.Shared/Configuration/AppMode.cs` expone:

- `AppMode.Environment`, `AppMode.DataSource`, `AppMode.ApiBaseUrl`, `AppMode.UseMock`, `AppMode.Tag`
- `AppMode.Configure(IConfiguration)` -> lee la seccion `AppMode` y sincroniza el legacy `AppSettings.UseMockServices`.

Cada host llama `AppMode.Configure(builder.Configuration)` antes de registrar servicios.

### Donde vive el config por host

| Host | Archivos | Comportamiento |
|------|----------|----------------|
| `IngenIA365ERP.Web.Client` (WASM) | `wwwroot/appsettings.json`, `appsettings.Development.json`, `appsettings.QA.json` | El runtime de Blazor WASM elige por `ASPNETCORE_ENVIRONMENT`. |
| `IngenIA365ERP.Web` (servidor) | `appsettings.json`, `appsettings.Development.json` | Standard ASP.NET Core. |
| `IngenIA365ERP.App` (MAUI) | `appsettings.json` y `appsettings.Development.json` como `EmbeddedResource` | El Development se carga solo en builds Debug (`#if DEBUG`). En Release solo se ve `appsettings.json`. |

Defaults entregados:

- `appsettings.json` -> `Production` + `Api`
- `appsettings.Development.json` -> `Development` + `Mock`

### Como cambiar de modo

- **DEV-Mock** (sin API arriba): `appsettings.Development.json` -> `"DataSource": "Mock"`. F5.
- **DEV-Api** (contra API local): `appsettings.Development.json` -> `"DataSource": "Api"`, `"ApiBaseUrl": "http://localhost:5100"`. La API debe estar arriba.
- **QA**: `ASPNETCORE_ENVIRONMENT=QA` -> usa `appsettings.QA.json` con `"DataSource": "Api"` apuntando al API de QA.
- **PDN**: el `appsettings.json` por defecto es Production + Api. Solo distribuyes con la URL correcta.

### Log al arrancar

```
[Development][Mock] AuthService listo · ApiBaseUrl=http://localhost:5100
[Production][Api]  AuthService listo · ApiBaseUrl=https://api.ingenia365erp.com
```

(El log antiguo `[PROD]/[DEV]` era engañoso porque no separaba environment de origen de datos. Ya no se usa.)

---

## 2. Multi-tenant y resolucion de tenant

### Flujo basico

> **Flujo superado.** Ya no se elige cooperativa antes de entrar: la identidad
> es central y la cooperativa se resuelve DESPUÉS, a partir de las membresías de
> la persona. El de abajo es el de Fase 0 y se conserva como referencia.
>
> El flujo vivo: `POST /api/auth/login` con `{email, password}` → devuelve un
> *challenge* (`MfaRequired`, `MfaEnrollmentRequired`, `TenantSelection`,
> `NoActiveMembership`) o la sesión directamente si hay una sola membresía y sin
> MFA pendiente. El contrato está en
> [specs/002/contracts/auth.md](../specs/002-identidad-central-federada/contracts/auth.md)
> y el cliente es `CentralAuthClient`, no `AuthService`.

1. El usuario entra a `/login`. El formulario incluye un campo **Tenant**.
2. Frontend manda al API `POST /api/auth/login` con body `{ Email, Password, TenantId }`. El TenantId va **en el body** (todavia no hay sesion).
3. Antes de mandar el request, el `AuthService` llama `TenantService.SetTenantAsync(tenantId)`. A partir de ahi, el `TenantDelegatingHandler` agrega el header `X-Tenant-Id` a todos los requests siguientes.
4. La API responde con JWT. Cualquier request posterior incluye el header.

### Middleware de tenant

`src/Presentation/IngenIA365ERP.API/Middleware/TenantResolutionMiddleware.cs` lee el header `X-Tenant-Id` y resuelve el tenant en BD. Rutas exentas (no requieren tenant):

- `/api/auth/login` - tenant viene en el body
- `/api/auth/refresh` - tenant viene en el token
- `/api/admin/*` - administracion cross-tenant
- `/api/health` - health check
- `/swagger/*` - documentacion OpenAPI
- `/_framework/*`, `/_vs/*` - assets de hot-reload

Estrategias adicionales soportadas (mismo middleware):
- Subdominio: `tenant.ingenia365.app`
- Query param `?tenant=xxx` (solo Development)

### Tabla de tenants

Vive en BD `IngenIA365ERP_Admin`, tabla `dbo.ADM_Tenants`. El modelo EF (`ErpTenantInfo` + `TenantDbContext`) esta alineado a este schema real (no a `admin.Tenants` que era el schema imaginario inicial).

Campos relevantes:
- `Id` (int, PK auto-increment)
- `PublicId` (uniqueidentifier)
- `Identifier` (nvarchar(100), unique) — esto es lo que el frontend manda como `TenantId`
- `Name`, `SchemaName`, `LicenseType`, `IsActive`, `MaxUsers`, etc.

Tenant de desarrollo pre-existente:
```
Identifier = "dev_tenant"
SchemaName = "dev_tenant"
LicenseType = "Enterprise"
```

---

## 3. Tablas de Identity y Seed

> **Esto dejó de ser cierto.** Las migraciones EF **son** la fuente de verdad
> del esquema, en dos ensamblados por proveedor
> (`IngenIA365ERP.Persistence.Migrations.{SqlServer,PostgreSql}`), y las aplica
> el inicializador al arrancar. El corpus SQL de `database/schema/` quedó
> congelado como referencia histórica: no se aplica ni se mantiene. Lo que sigue
> se conserva sólo para entender de dónde venía el proyecto.

Las EF migrations no estan operativas (incompatibilidad de tools), asi que los schemas se aplican via SQL.

### Aplicar schema de Identity

```bash
sqlcmd -S localhost -E -d IngenIA365ERP -i tools/scripts/identity_tables.sql
```

Este script:
- Dropea constraints FK desde tablas auxiliares (`SEC_RefreshTokens`, `SEC_UserSessions`).
- Recrea las tablas que ASP.NET Core Identity necesita: `SEC_Users`, `SEC_Roles`, `SEC_UserRoles`, `SEC_UserClaims`, `SEC_UserLogins`, `SEC_UserTokens`, `SEC_RoleClaims`, `SEC_Permissions`, `SEC_RolePermissions`, `SEC_LoginAttempts`.
- Incluye `SET QUOTED_IDENTIFIER ON` (necesario para los indices filtrados de Identity).

### Seed automatico al arrancar

`Program.cs` del API en Development llama `IdentitySeedData.SeedAsync(app.Services)` que crea:

- 8 roles del sistema: `Administrator`, `Auditor`, `Accountant`, `LoanOfficer`, `Cashier`, `PayrollManager`, `InventoryManager`, `ReadOnly`.
- ~119 permisos cubriendo los 9 modulos.
- Asignaciones rol -> permiso segun `RolePermissionMap` (Administrator = `*`, etc.).
- Usuario admin:

| Campo | Valor |
|-------|-------|
| Email | `admin@ingenia365.com` |
| Password | `Admin@Temporal2024!` |
| TenantId | `dev_tenant` |
| Roles | `Administrator` |

> El admin se crea con `MustChangePassword = false` (asi se puede entrar directo en dev). En PDN se debe cambiar.

---

## 4. Cadenas de conexion

`appsettings.Development.json` del API usa **tres** cadenas distintas:

```jsonc
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=IngenIA365ERP;User=erp;Password=...;TrustServerCertificate=true",
  "TenantConnection":  "Server=localhost;Database=IngenIA365ERP_Admin;User=erp;Password=...;TrustServerCertificate=true",
  "SqlServer":         "Server=localhost;Database=IngenIA365ERP;Trusted_Connection=true;TrustServerCertificate=true",
  "MongoDB": "mongodb://localhost:27017/IngenIA365ERP_Audit",
  "Redis":   "localhost:6379,abortConnect=false"
}
```

| Cadena | Para que se usa | DbContext |
|--------|-----------------|-----------|
| `DefaultConnection` | BD transaccional principal | `ApplicationDbContext` |
| `TenantConnection` | BD admin (tenants, subscripciones) | `TenantDbContext` |
| `SqlServer` | BD de Identity (mismo SQL Server) | `ErpIdentityDbContext` |

Si Identity y la BD principal viven en el mismo SQL Server, `SqlServer` y `DefaultConnection` apuntan a la misma BD pero pueden diferir en credenciales (Trusted_Connection vs SQL auth).

---

## 5. Registracion de servicios DI (Program.cs del API)

Orden importante en `Program.cs` del API — los siguientes deben quedar **antes** de `AddIdentityServices` y `AddAuditServices` porque son consumidos por ellos:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentTenantService, TenantContextAccessor>();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeService, DateTimeService>();
```

Despues:

```csharp
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddAuditServices(builder.Configuration);
builder.Services.AddApplicationServices();      // MediatR + FluentValidation + Mapster
builder.Services.AddPersistenceServices(builder.Configuration);
```

Notas de scope:
- `MongoAuditService` esta registrado como **Singleton**, asi que sus dependencias (`ICurrentTenantService`, `ICurrentUserService`) tambien son Singleton para evitar errores de scope validation. `IHttpContextAccessor` maneja el contexto per-request internamente.
- `MemoryCacheService` es un `ConcurrentDictionary` thread-safe, registrado como Singleton (para uso local; en PDN se puede cambiar a Redis activando `AddCachingServices`).

### Implementaciones sencillas incluidas

- `MemoryCacheService` (en `Caching/Services/`): ICacheService en memoria. Implementa los 4 metodos del contrato incluyendo `RemoveByPrefixAsync`.
- `CurrentUserService` (en `API/Services/`): lee claims del `HttpContext.User` (uid, name, tenant_id, roles).
- `DateTimeService` (en `API/Services/`): `UtcNow` y `TodayUtc`.

---

## 6. Modelo `AuthResponse` (frontend ↔ backend)

El JSON que devuelve `POST /api/auth/login` y el modelo del cliente **deben coincidir** en nombres. El shape oficial es:

```json
{
  "userPublicId": "guid",
  "fullName":     "...",
  "email":        "...",
  "tenantId":     "...",
  "tenantName":   "...",
  "roles":        ["..."],
  "permissions":  ["..."],
  "tokens": {
    "accessToken":        "jwt...",
    "refreshToken":       "...",
    "accessTokenExpiry":  "2026-04-29T02:05:57Z",
    "refreshTokenExpiry": "2026-05-06T01:50:57Z"
  }
}
```

Esto vive en:
- API: `IngenIA365ERP.Identity/Models/AuthResponse.cs` (record)
- Cliente: `IngenIA365ERP.Shared/Models/AuthResponse.cs` (clase con setters)

~~`AuthService.LoginAsync`~~ **ya no existe**: se retiro el 2026-08-25 junto con
`/api/auth/dev/login`, el atajo que llamaba y que servia para saltarse el segundo
factor. Quien guarda la sesion hoy es `CentralAuthClient`.

> Si el login devuelve "Usuario o contrasena incorrectos" pero `curl` directo al API funciona con HTTP 200, casi siempre es desalineamiento de nombres entre `AuthResponse` cliente y API.

---

## 7. Modelo `LoginRequest` (frontend)

`IngenIA365ERP.Shared/Models/LoginRequest.cs`:

```csharp
public class LoginRequest
{
    [Required, EmailAddress]
    public string Email    { get; set; } = string.Empty;
    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
    public string TenantId { get; set; } = "dev_tenant";
}
```

La API espera `Email` (no `Username`). Anteriormente el frontend mandaba `Username`, lo cual deserializaba a null en el API y bloqueaba el login con credenciales aparentemente validas.

---

## 8. PaginationParams binding (queries con `[AsParameters]`)

`PaginationParams` aparece como sub-propiedad de muchos query records (`ListChecksQuery`, `ListTreasuryConceptsQuery`, etc.). ASP.NET Core no sabe bindear tipos complejos anidados desde query string en GET — los infiere como Body, lo cual rompe el arranque.

Solucion: `PaginationParams` tiene un metodo estatico `BindAsync(HttpContext, ParameterInfo)` que lee `PageNumber/PageSize/SortBy/IsDescending` directo del query string. RDF lo detecta y lo usa.

Trade-off: `IngenIA365ERP.Application` referencia `Microsoft.AspNetCore.App` (para `HttpContext`). Es una concesion arquitectonica menor que evita aplanar `Pagination` en ~75 query records.

---

## 9. Inicio rapido desde cero

```bash
# 1. SQL Server, MongoDB, Redis (Docker)
docker-compose up -d sqlserver mongodb redis

# 2. Crear BDs base + tenant dev (ya existen tras init-dev.ps1)
cd tools/scripts
pwsh init-dev.ps1

# 3. Aplicar tablas de Identity
sqlcmd -S localhost -E -d IngenIA365ERP -i identity_tables.sql

# 4. Levantar API (terminal 1) — el seed de Identity corre automatico en Development
cd ../../src/Presentation/IngenIA365ERP.API
dotnet run

# 5. Levantar Web (terminal 2)
cd ../IngenIA365ERP.Web
dotnet run
```

### Probar login con curl

```bash
curl -k -X POST https://localhost:7100/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"Email":"admin@ingenia365.com","Password":"Admin@Temporal2024!","TenantId":"dev_tenant"}'
```

Respuesta esperada: HTTP 200 con JWT y arreglo de 119 permisos.

### URLs

- API: `https://localhost:7100` / `http://localhost:5100`
- Swagger: `https://localhost:7100/swagger`
- Health: `https://localhost:7100/api/health`

### Credenciales dev

| Campo | Valor |
|-------|-------|
| Email | `admin@ingenia365.com` |
| Password | `Admin@Temporal2024!` |
| Tenant | `dev_tenant` |

---

## 10. Resolucion de problemas frecuentes

| Sintoma | Causa probable | Solucion |
|---------|----------------|----------|
| `Tenant not specified. Use X-Tenant-Id header` en login | Middleware no reconoce la ruta como anonima | Verificar que `/api/auth/login` esta en la lista de skip del `TenantResolutionMiddleware` |
| Login devuelve "Usuario o contrasena incorrectos" pero curl funciona | Frontend `AuthResponse` desalineado | Verificar `Shared/Models/AuthResponse.cs` con shape de la API |
| `Cannot open database 'IngenIA365ERP_Dev'` | Cadena `SqlServer` apunta a BD inexistente | Cambiar a `Database=IngenIA365ERP` en `appsettings.Development.json` |
| `Invalid column name 'WasSuccessful'` | Codigo legacy que esperaba columna distinta a la del schema actual | El schema actual usa `Success` en `SEC_LoginAttempts`. `RecordLoginAttempt` ya esta corregido. |
| API arranca pero falla en `Body was inferred but the method does not allow inferred body parameters` | `PaginationParams` sin `BindAsync` | Verificar que `Shared/Models/PaginationParams.cs` lo expone |
| Log dice `[PROD]` aunque estes en local | Log antiguo, mal etiquetado | Ya no aparece. Ahora dice `[Environment][DataSource] ...` |
| Maui App build falla con `DirectoryNotFoundException` (path largo) | Limite Windows MAX_PATH=260 con OneDrive | Excluir App del solution build, o habilitar long paths en Windows |
