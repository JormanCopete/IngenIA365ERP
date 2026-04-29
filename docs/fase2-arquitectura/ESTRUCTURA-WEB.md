# ESTRUCTURA-WEB.md - Solucion IngenIA365ERP

> Generado: 2026-03-21 | Build: EXITOSO (0 errores, 6 warnings)

## Arbol de directorios

```
IngenIA365ERP/
+-- IngenIA365ERP.slnx
|
+-- src/
|   +-- Core/
|   |   +-- IngenIA365ERP.Domain/                    (Class Library - sin dependencias)
|   |   |   +-- Common/
|   |   |   |   +-- AuditableEntity.cs
|   |   |   |   +-- AuditableEntityLong.cs
|   |   |   |   +-- BaseEntity.cs
|   |   |   |   +-- BaseEntityLong.cs
|   |   |   |   +-- IDomainEvent.cs
|   |   |   |   +-- ITenantEntity.cs
|   |   |   |   +-- ValueObject.cs
|   |   |   +-- Entities/
|   |   |       +-- Core/         (Person, Associate, Bank, Branch, City, Committee,
|   |   |       |                  Company, CostCenter, Beneficiary, PersonFinancial,
|   |   |       |                  Reference, Spouse)
|   |   |       +-- Accounting/   (ChartOfAccount, AccountBalance, JournalEntry, VoucherType)
|   |   |       +-- Lending/      (LoanPortfolio, LoanTransaction, PendingInstallment,
|   |   |       |                  CreditLineParameter)
|   |   |       +-- Payroll/      (Employee)
|   |   |       +-- Security/     (User, Role)
|   |   |
|   |   +-- IngenIA365ERP.Application/               (Class Library -> Domain)
|   |       +-- Common/
|   |       |   +-- Behaviors/    (ValidationBehavior, LoggingBehavior,
|   |       |   |                  PerformanceBehavior, AuditBehavior)
|   |       |   +-- Interfaces/   (IApplicationDbContext, IAuditService,
|   |       |   |                  IAuthenticationService, ICacheService,
|   |       |   |                  ICurrentTenantService, ICurrentUserService,
|   |       |   |                  IDateTimeService, ITokenService)
|   |       |   +-- Models/       (Result, Result<T>, Error, PagedList, PaginationParams)
|   |       +-- Core/People/
|   |       |   +-- Commands/CreatePerson/ (Command, Handler, Validator)
|   |       |   +-- Queries/
|   |       |       +-- GetPeople/           (Query, Handler)
|   |       |       +-- GetPersonByPublicId/ (Query, Handler)
|   |       +-- DependencyInjection.cs
|   |
|   +-- Infrastructure/
|   |   +-- IngenIA365ERP.Persistence/               (Class Library -> Application, Domain)
|   |   |   +-- Configurations/
|   |   |   |   +-- Core/        (Person, Associate, Bank, Branch, City, CostCenter)
|   |   |   |   +-- Accounting/  (AccountBalance, ChartOfAccount, JournalEntry)
|   |   |   |   +-- Lending/     (CreditLineParameter, LoanPortfolio, LoanTransaction)
|   |   |   |   +-- Security/    (Role, User)
|   |   |   +-- DbContext/       (ApplicationDbContext)
|   |   |   +-- Interceptors/    (AuditableEntityInterceptor, SoftDeleteInterceptor)
|   |   |   +-- DependencyInjection.cs
|   |   |
|   |   +-- IngenIA365ERP.Identity/                  (Class Library -> Application)
|   |   |   +-- Configuration/   (JwtSettings)
|   |   |   +-- Services/        (JwtTokenService, AuthenticationService)
|   |   |   +-- DependencyInjection.cs
|   |   |
|   |   +-- IngenIA365ERP.Audit/                     (Class Library -> Application)
|   |   |   +-- Configuration/   (MongoSettings)
|   |   |   +-- Models/          (AuditEntry)
|   |   |   +-- Services/        (MongoAuditService)
|   |   |   +-- DependencyInjection.cs
|   |   |
|   |   +-- IngenIA365ERP.Caching/                   (Class Library -> Application)
|   |   |   +-- Configuration/   (RedisSettings)
|   |   |   +-- Services/        (RedisCacheService)
|   |   |   +-- DependencyInjection.cs
|   |   |
|   |   +-- IngenIA365ERP.Legacy/                    (Class Library -> Application)
|   |       +-- Adapters/        (ILegacyAdapter, AccountingLegacyAdapter,
|   |                             LendingLegacyAdapter)
|   |
|   +-- Presentation/
|       +-- IngenIA365ERP.API/                       (Web API -> Application, Persistence,
|       |   |                                         Identity, Audit, Caching)
|       |   +-- Endpoints/       (HealthEndpoints, PeopleEndpoints)
|       |   +-- Program.cs       (Carter, Serilog, Swagger, CORS, HealthChecks)
|       |
|       +-- IngenIA365ERP.Web/                       (Blazor Server -> Shared, Web.Client)
|       |   +-- Components/      (App.razor, Error.razor)
|       |   +-- Services/        (FormFactor, SecureStorageService)
|       |   +-- Program.cs       (Syncfusion, Auth, Cookie)
|       |
|       +-- IngenIA365ERP.Web.Client/                (Blazor WASM -> Shared)
|       |   +-- Services/        (FormFactor, WebAssemblySecureStorage)
|       |   +-- Program.cs
|       |
|       +-- IngenIA365ERP.Shared/                    (Razor Class Library)
|       |   +-- Layout/          (MainLayout, MinimalLayout, NavMenu)
|       |   +-- Pages/           (Home, Dashboard, Login, NotFound)
|       |   +-- Components/      (RedirectToLogin)
|       |   +-- Services/        (IAuthService, AuthService, CustomAuthStateProvider,
|       |   |                     IFormFactor, ISecureStorage, Mock/MockAuthService)
|       |   +-- Models/          (LoginRequest, LoginResponse, LogoutRequest, AuthResponse)
|       |   +-- Configuration/   (AppSettings)
|       |   +-- Routes.razor
|       |
|       +-- IngenIA365ERP.App/                       (MAUI Blazor Hybrid -> Shared)
|           +-- Platforms/       (Android, iOS, MacCatalyst, Windows)
|           +-- Services/        (FormFactor, SecureStorageService)
|           +-- Resources/       (AppIcon, Splash, Images, Fonts, Raw)
|           +-- MauiProgram.cs
|
+-- tests/
|   +-- IngenIA365ERP.Domain.Tests/                  (xUnit -> Domain)
|   |   +-- Entities/PersonTests.cs
|   |   +-- Common/  (BaseEntityTests, ValueObjectTests)
|   |
|   +-- IngenIA365ERP.Application.Tests/             (xUnit -> Application, Domain)
|   |   +-- Common/ResultTests.cs
|   |
|   +-- IngenIA365ERP.API.IntegrationTests/          (xUnit + WebApplicationFactory -> API)
|   |   +-- HealthEndpointTests.cs
|   |
|   +-- IngenIA365ERP.Architecture.Tests/            (xUnit + ArchUnitNET -> Domain, Application)
|       +-- CleanArchitectureTests.cs
|
+-- tools/
    +-- IngenIA365ERP.DataMigrator/                  (Console App -> Persistence)
        +-- Program.cs
```

## Paquetes NuGet por proyecto

### Core

| Proyecto | Paquete | Version |
|----------|---------|---------|
| **Domain** | (ninguno) | - |
| **Application** | MediatR | 12.5.0 |
| | FluentValidation | 11.12.0 |
| | FluentValidation.DependencyInjectionExtensions | 11.12.0 |
| | Mapster | 10.0.3 |
| | Mapster.DependencyInjection | 10.0.0 |
| | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 |
| | Microsoft.Extensions.Logging.Abstractions | 10.0.5 |
| | Microsoft.EntityFrameworkCore | 10.0.5 |

### Infrastructure

| Proyecto | Paquete | Version |
|----------|---------|---------|
| **Persistence** | Microsoft.EntityFrameworkCore | 10.0.5 |
| | Microsoft.EntityFrameworkCore.SqlServer | 10.0.5 |
| | Microsoft.EntityFrameworkCore.Tools | 10.0.5 |
| | Dapper | 2.1.72 |
| | Finbuckle.MultiTenant | 7.0.2 |
| | Finbuckle.MultiTenant.EntityFrameworkCore | 7.0.2 |
| **Identity** | Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.5 |
| | BCrypt.Net-Next | 4.1.0 |
| | Microsoft.IdentityModel.Tokens | 8.16.0 |
| | System.IdentityModel.Tokens.Jwt | 8.16.0 |
| **Audit** | MongoDB.Driver | 3.7.1 |
| | Microsoft.Extensions.Configuration.Abstractions | 10.0.5 |
| | Microsoft.Extensions.Configuration.Binder | 10.0.5 |
| | Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.5 |
| **Caching** | Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.5 |
| | StackExchange.Redis | 2.12.4 |
| | Microsoft.Extensions.Configuration.Abstractions | 10.0.5 |
| | Microsoft.Extensions.Configuration.Binder | 10.0.5 |
| | Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.5 |
| **Legacy** | (ninguno -- pendiente ODBC) | - |

### Presentation

| Proyecto | Paquete | Version |
|----------|---------|---------|
| **API** | Carter | 8.2.1 |
| | Swashbuckle.AspNetCore | 7.3.2 |
| | Serilog.AspNetCore | 9.0.0 |
| | AspNetCoreRateLimit | 5.0.0 |
| **Web** | (ASP.NET Core implicit) | 10.0.x |
| **Web.Client** | Microsoft.AspNetCore.Components.WebAssembly | 10.0.0 |
| **Shared** | Syncfusion.Blazor | 33.1.44 |
| | Syncfusion.Blazor.Themes | 33.1.44 |
| | Microsoft.AspNetCore.Components.Authorization | 10.0.5 |
| | Microsoft.Extensions.Http | 10.0.5 |
| **App** | Microsoft.Maui.Controls | 10.0.20 |
| | Microsoft.AspNetCore.Components.WebView.Maui | 10.0.20 |
| | Syncfusion.Blazor | 33.1.44 |

### Tests

| Proyecto | Paquete | Version |
|----------|---------|---------|
| **Domain.Tests** | xunit | 2.9.3 |
| | FluentAssertions | 7.2.2 |
| | NSubstitute | 5.3.0 |
| | Microsoft.NET.Test.Sdk | 17.14.1 |
| **Application.Tests** | (mismos que Domain.Tests) | - |
| **API.IntegrationTests** | Microsoft.AspNetCore.Mvc.Testing | 10.0.5 |
| | FluentAssertions | 7.2.2 |
| | xunit | 2.9.3 |
| **Architecture.Tests** | TngTech.ArchUnitNET.xUnit | 0.13.3 |
| | xunit | 2.9.3 |

## Diagrama de dependencias entre proyectos

```
                    +------------------+
                    |     Domain       |  (sin dependencias externas)
                    +--------+---------+
                             |
                    +--------v---------+
                    |   Application    |  MediatR, FluentValidation, Mapster
                    +--+----+----+--+--+
                       |    |    |  |
         +-------------+    |    |  +--------------+
         |                  |    |                 |
   +-----v-----+  +--------v-+  +v--------+ +-----v-----+
   |Persistence |  | Identity |  |  Audit  | |  Caching  |
   | (EF+SQL)   |  |  (JWT)   |  | (Mongo) | |  (Redis)  |
   +-----+------+  +-----+----+  +----+----+ +-----+-----+
         |               |            |             |
   +-----v---------------v------------v-------------v------+
   |                         API                            |
   |      Carter + Serilog + Swagger + HealthChecks         |
   +--------------------------------------------------------+

   +------------+     +----------------+     +-----------------+
   |   Shared   | <---+      Web       |     |      App        |
   |  (Blazor   | <---+   (Server)     |     |    (MAUI)       |
   |   Razor)   |     +----------------+     +-----------------+
   +------+-----+            ^
          |                  |
   +------v----------+      |
   |   Web.Client    +------+
   |  (Blazor WASM)  |
   +------------------+

   +-----------------+
   |  DataMigrator   +----> Persistence
   |   (Console)     |
   +-----------------+

   Tests:
   Domain.Tests ----------> Domain
   Application.Tests ------> Application, Domain
   API.IntegrationTests ---> API (WebApplicationFactory)
   Architecture.Tests -----> Domain, Application (ArchUnitNET)
```

## Archivos creados en esta sesion

### Nuevos (creados desde cero)
- `tests/IngenIA365ERP.Domain.Tests/` -- Proyecto de test + 3 tests (Person, BaseEntity, ValueObject)
- `tests/IngenIA365ERP.Application.Tests/` -- Proyecto de test + ResultTests
- `tests/IngenIA365ERP.API.IntegrationTests/` -- Proyecto de test + HealthEndpointTests
- `tests/IngenIA365ERP.Architecture.Tests/` -- Proyecto de test + CleanArchitectureTests
- `tests/*/GlobalUsings.cs` -- Global using para Xunit en cada proyecto de test
- `src/Core/IngenIA365ERP.Application/Common/Behaviors/*.cs` -- 4 pipeline behaviors (MediatR)
- `src/Presentation/IngenIA365ERP.App/Resources/AppIcon/*.svg` -- Placeholders MAUI
- `src/Presentation/IngenIA365ERP.App/Resources/Splash/splash.svg` -- Placeholder MAUI
- `tools/IngenIA365ERP.DataMigrator/` -- Proyecto consola para migracion de datos

### Modificados (correcciones de compilacion)
- `src/Core/IngenIA365ERP.Application/IngenIA365ERP.Application.csproj` -- Mapster 7->10, MapsterMapper->Mapster.DependencyInjection
- `src/Core/IngenIA365ERP.Application/Common/Interfaces/IAuditService.cs` -- Firma alineada con MongoAuditService + AuditLogEntry record
- `src/Core/IngenIA365ERP.Domain/Entities/Lending/CreditLineParameter.cs` -- Agregado LegacyCode
- `src/Infrastructure/IngenIA365ERP.Audit/IngenIA365ERP.Audit.csproj` -- Agregados Configuration.Binder/Options packages
- `src/Infrastructure/IngenIA365ERP.Audit/Services/MongoAuditService.cs` -- UserId int? -> ToString()
- `src/Infrastructure/IngenIA365ERP.Caching/IngenIA365ERP.Caching.csproj` -- Agregados Configuration.Binder/Options packages
- `src/Infrastructure/IngenIA365ERP.Persistence/DbContext/ApplicationDbContext.cs` -- Agregado DbSet<Reference>
- `src/Infrastructure/IngenIA365ERP.Persistence/Interceptors/AuditableEntityInterceptor.cs` -- Username -> UserName
- `src/Infrastructure/IngenIA365ERP.Persistence/Interceptors/SoftDeleteInterceptor.cs` -- Username -> UserName
- `src/Presentation/IngenIA365ERP.API/Program.cs` -- Agregado `public partial class Program;`

### Archivos expandidos automaticamente (pre-existentes, expandidos por linter/scaffolding)
La mayoria de los proyectos de infraestructura y presentacion fueron expandidos
automaticamente con codigo funcional durante la sesion (DI, configuraciones EF Core,
servicios de Identity/JWT, servicios de Caching/Redis, endpoints Carter, MAUI app,
Blazor layouts y paginas, etc.).

## Estado del build

```
Compilacion correcta.
    6 Advertencia(s)    -- todas de MAUI Windows (PRI249 qualifier warnings, cosmeticos)
    0 Errores

Framework: .NET 10.0 (SDK 10.0.201)
Proyectos compilados: 17
  - 2 Core (Domain, Application)
  - 5 Infrastructure (Persistence, Identity, Audit, Caching, Legacy)
  - 5 Presentation (API, Web, Web.Client, Shared, App)
  - 4 Tests (Domain.Tests, Application.Tests, API.IntegrationTests, Architecture.Tests)
  - 1 Tools (DataMigrator)
```

## Formato de solucion (.slnx)

El archivo `IngenIA365ERP.slnx` usa el formato XML simplificado de .NET 10+,
basado en el patron observado en FlitApp.slnx. Los proyectos se organizan en
carpetas logicas: `/Core/`, `/Infrastructure/`, `/Presentation/`, `/Tests/`, `/Tools/`.
