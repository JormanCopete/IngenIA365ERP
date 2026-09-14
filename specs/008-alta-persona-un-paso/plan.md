# Implementation Plan: Alta de persona en un paso desde los módulos

**Branch**: `008-alta-persona-un-paso` | **Date**: 2026-09-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-alta-persona-un-paso/spec.md` (6 decisiones
del dueño registradas en Clarifications, sesión 2026-09-13).

## Summary

Un solo diálogo y un solo «Registrar» para empleado y asociado con persona nueva (comando
compuesto atómico en el servidor); los datos personales de una persona existente se editan sólo
desde el diálogo compartido de Personas; las banderas derivadas (Empleado, Asociado, Vendedor)
las escribe únicamente el handler de la fila hija; un componente de formulario de persona para
las tres pantallas; permisos `Core.People.*`, `Core.Associates.*`, `Payroll.Employees.*` en el
catálogo, exigidos en la API y expuestos al cliente para la cooperativa activa. Dos decisiones
de datos cierran huecos que hoy rompen: el documento de una persona **eliminada** se rechaza con
aviso y se ofrece **restaurar la misma fila**; el **reingreso** de un empleado retirado crea una
**ficha nueva** y la consulta por persona devuelve sólo la viva.

## Technical Context

**Language/Version**: .NET 10 (global.json 10.0.3xx), C# 14; Blazor Web App
InteractiveWebAssembly (Web + Web.Client) y MAUI Hybrid; Syncfusion 33.2.8 por componente
**Primary Dependencies**: MediatR + FluentValidation (Application), EF Core 10 (PostgreSQL /
SQL Server en par), Redis (caché de permisos, TTL 30 min), MongoDB (auditoría vía `AuditBehavior`),
Carter (endpoints), `PermissionAuthorizationFilter` (feature 003)
**Storage**: `COR_People`, `PAY_Employees`, `PAY_SalaryChanges`, `COR_Associates`,
`SEC_Permissions`/`SEC_RolePermissions` — **sin columnas nuevas**; una migración de datos en par
(`ReconciliarBanderasDerivadasDePersona`) y **una de índice** (`UnaSolaFichaVivaPorPersona`:
`UK_PAY_Employees_PersonId` pasa a único sólo entre fichas vivas, `Status <> -1 AND IsDeleted = 0`,
porque sin filtro el reingreso —decidido como ficha nueva— reventaba en la base); `UK_COR_People_TaxId`
sigue **sin filtro** (una eliminada se restaura, no se duplica)
**Testing**: Application.Tests (InMemory + NSubstitute), Architecture.Tests (escaneo de fuente),
API.IntegrationTests (`CentralIdentityApiFixture`, colección «Nomina e2e», Docker), verificación
manual en QA con tres roles
**Target Platform**: API en contenedor linux-x64 (k3s, Argo CD); cliente WebAssembly en
navegador detrás de Cloudflare; MAUI comparte `Shared`
**Project Type**: web-service (Minimal APIs) + web-app (Blazor) en un solo repositorio, Clean
Architecture de cuatro capas
**Performance Goals**: el alta compuesta responde en una sola petición HTTP y un solo
`SaveChangesAsync`; los permisos del cliente se cargan **una vez por cooperativa** (una petición a
`/api/admin/permissions/mine`), no por pantalla ni por botón; sin regresión en el tiempo de
apertura de Personas/Empleados (hoy dominado por catálogos, que pasan a cargarse con `WhenAll`)
**Constraints**: sin `BeginTransaction` expuesto en `IApplicationDbContext` → atomicidad por un
solo `SaveChangesAsync` con navegaciones EF; permisos por cooperativa resueltos por petición
(`PermisosDeLaPeticion`); sin permiso → 404 `Generic.NotFound` indistinguible (FR-017 de la
feature 003); `Shared` no referencia `Application` (los DTOs se duplican en `PersonasDtos`);
ningún valor legal ni de negocio en código
**Scale/Scope**: 3 pantallas refactorizadas (~2.400 líneas → ~1.500), 3 componentes nuevos,
2 comandos compuestos + 1 de restauración, 5 rutas nuevas, 11 códigos de permiso nuevos,
1 migración de datos; volumen de datos hoy mínimo (2 personas en producción) pero el diseño
sirve a cooperativas con miles de personas (búsqueda paginada ya existente)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec (clarificada) → plan → tasks en esta carpeta; rama propia |
| II   | Clean Architecture | PASS | contratos, fábricas y `ICurrentUserPermissions` en Application; la implementación en API; `Shared` no referencia Application |
| III  | CQRS + MediatR | PASS | `RegisterEmployeeWithPersonCommand`, `RegisterAssociateWithPersonCommand`, `RestorePersonCommand` con validador hermano; endpoints sólo reenvían |
| IV   | Multi-tenancy | PASS | todo bajo la conexión de la cooperativa; permisos por cooperativa; la caché del cliente se vacía al cambiar; `/permissions/mine` exige cooperativa resuelta |
| V    | Person centralizada | PASS | corazón de la feature: una sola escritura de la persona; banderas derivadas sólo por el handler hijo (texto literal del principio); ninguna tabla hija recibe datos personales |
| VI   | PublicId | PASS | `PersonPublicId`, `EmployeeDetailDto.PersonPublicId`, `Person.TaxIdDeleted` trae `PublicId`; ningún Id interno cruza |
| VII  | Soft-delete + auditoría | PASS | sin borrados físicos; restaurar revierte el soft-delete de la **misma** fila; la migración sólo actualiza banderas |
| VIII | Validación dual | PASS | `PersonInputValidator`/`EmployeeInputValidator`/`AssociateInputValidator` en servidor (compuestos con `SetValidator`); `PersonaFormularioModelo.Validar()` + DataAnnotations en cliente |
| IX   | Errores visibles | PASS | `PersonSearchPicker` y `RegistroAsociado` dejan de tragar excepciones; `PermisosDelUsuario` falla cerrado: en silencio sin sesión (prerender), con aviso si la llamada falla con sesión; `PrincipioIX_NoEmptyCatch` cubre los dos `catch` hoy vacíos |
| X    | Trazabilidad | PASS | cada compuesto y la restauración son **un** Command → un evento de auditoría con el request completo |
| XI   | Inmutabilidad contable | PASS | el reingreso crea ficha nueva justamente para no tocar la retirada, a la que apuntan corridas liquidadas |
| XII  | Migraciones | PASS | `ReconciliarBanderasDerivadasDePersona` sólo DML, idempotente (recalcula desde tablas hijas), sin `DELETE`, `Down` no-op documentado; `UnaSolaFichaVivaPorPersona` sólo cambia el filtro de un índice y su `Down` lo restaura; ambas en par PostgreSQL/SQL Server; diagnóstico previo en `diagnostico-banderas.sql` |
| UI   | Indicador de carga | PASS | `PersonaDialog`, picker y `RegistroAsociado` con `IndicadorDeCarga` + `EstadoDeCarga`; `Asociados` entra a `ModulosMigrados`; sin colores literales ni `<style>` |

**Post-design re-check (Phase 1)**: sin cambios; el diseño de `data-model.md` no agrega columnas
ni tablas, `contracts/api.md` no expone ningún `int Id`, y la única ruta nueva sin
`RequirePermission` propio (`/permissions/mine`) exige sesión y cooperativa resuelta.

## Decisiones

| Decisión | Alternativa descartada | Por qué |
|---|---|---|
| Un diálogo y un Guardar (3.2) para roles con tabla hija; 3.1 para roles-marca | 3.1 para todo; ambos según elija el usuario | 3.1 duplica guardados y auditoría y deja persona con bandera si se cancela el segundo paso; ofrecer ambos multiplica caminos |
| Comando compuesto con un solo `SaveChangesAsync` y fábricas compartidas (`PersonFactory`, `EmployeeRegistrar`, `AssociateRegistrar`) | dos llamadas HTTP con compensación; `Send` anidado de `CreatePersonCommand` | la compensación deja persona huérfana y un borrado en auditoría; el `Send` anidado guarda y audita por separado |
| Banderas derivadas fuera de `Create/UpdatePersonCommand` | actualización parcial de banderas | la constitución ya dice quién las escribe; sacarlas del contrato hace imposible el bug |
| Persona existente de sólo lectura en módulos + «Editar datos de la persona» | edición en línea con PUT parcial | un solo sitio escribe la persona (decisión del dueño) |
| `PersonaCampos` por sección + `SfTab` propia en cada módulo | componente que emite `TabItem` | un hijo no puede emitir `TabItem` dentro del `TabItems` del padre de forma fiable |
| `GET /api/admin/permissions/mine` | permisos en `MeResult` | `/api/auth/me` es ruta exenta de cooperativa; los permisos son por cooperativa |
| Operador crea/edita, no da de baja; `*.View` para todo rol existente | cerrar lectura; Operador con todo | decisiones del dueño; statu quo de lectura evita bloqueos el día del despliegue |
| Duplicado sólo por `TaxId` | `(IdType, TaxId)` | el NIT de una natural es su cédula: distinguir por tipo permitiría duplicar |
| Documento de persona eliminada → `Person.TaxIdDeleted` (422 con nombre, fecha y `PublicId`) + `RestorePersonCommand` (`POST /api/core/people/{id}/restore`, `Core.People.Delete`) sobre la misma fila | ignorar eliminadas (hoy: viola `UK_COR_People_TaxId` y da 500); índice filtrado; reactivar sin preguntar | el índice único no distingue eliminadas; restaurar la misma fila conserva cartera/asientos/corridas que la referencian y queda auditado |
| Reingreso = ficha nueva; `GetEmployeeByPersonIdQuery` y `by-person` devuelven sólo `Status != -1`; `RehireDate` sigue en `MaxValue`; `UK_PAY_Employees_PersonId` filtrado a fichas vivas (migración `UnaSolaFichaVivaPorPersona`, reversible) | reabrir la ficha retirada con `RehireDate` | las corridas liquidadas apuntan a la ficha retirada (Principio XI); hoy `FirstOrDefault` sin filtro devolvía cualquiera de las dos, y el índice único sin filtro hacía del reingreso un 500 |

Detalle y alternativas en [research.md](research.md).

## Project Structure

### Documentation (this feature)

```text
specs/008-alta-persona-un-paso/
├── spec.md                    # especificación clarificada (6 decisiones)
├── plan.md                    # este archivo
├── research.md                # Phase 0: decisiones con alternativas
├── data-model.md              # Phase 1: entidades, banderas, estados, migración
├── quickstart.md              # Phase 1: cómo probarlo de punta a punta
├── contracts/api.md           # Phase 1: endpoints, permisos, comandos, errores
├── diagnostico-banderas.sql   # consulta de sólo lectura previa a la migración
├── checklists/requirements.md
└── tasks.md                   # Phase 2 (ya generado; 57 tareas)
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Application/
  Core/People/Contracts/PersonInput.cs                     # + PersonInputValidator
  Core/People/Services/PersonFactory.cs                    # duplicado (viva/eliminada), ciudad; agrega sin guardar
  Core/People/Commands/CreatePerson/*, UpdatePerson/*      # sobre PersonInput y PersonFactory
  Core/People/Commands/RestorePerson/RestorePersonCommand.cs # misma fila; recalcula derivadas
  Core/People/Queries/SearchPeopleQuery.cs                 # banderas + Role
  Core/People/Queries/GetPersonByDocumentQuery.cs          # por documento (query string, no ruta: no queda en el log), eliminadas incluidas
  Core/Associates/Contracts/AssociateInput.cs
  Core/Associates/Services/AssociateRegistrar.cs
  Core/Associates/Commands/RegisterAssociate/*, RegisterAssociateWithPerson/*
  Payroll/EmployeeManagement/Contracts/EmployeeInput.cs
  Payroll/EmployeeManagement/Services/EmployeeRegistrar.cs
  Payroll/EmployeeManagement/Commands/RegisterEmployee/*, RegisterEmployeeWithPerson/*
  Payroll/EmployeeManagement/Queries/EmployeeQueries.cs    # PersonPublicId; by-person sólo ficha viva
  Common/Interfaces/Security/ICurrentUserPermissions.cs
  Security/Permissions/GetMyPermissionsQuery.cs
  DependencyInjection.cs
src/Infrastructure/IngenIA365ERP.Identity/Seed/
  CorePermissionCatalogSeeder.cs, PayrollPermissionCatalogSeeder.cs, BuiltInRolesSeeder.cs, PhaseZeroSecuritySeeder.cs
src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/
  *_ReconciliarBanderasDerivadasDePersona.cs
src/Presentation/IngenIA365ERP.API/
  Endpoints/Core/PeopleEndpoints.cs, PeopleDetailEndpoints.cs, AssociatesEndpoints.cs
  Endpoints/Payroll/EmployeesEndpoints.cs, Endpoints/PermissionsModule.cs
  Services/PermisosDelHandler.cs, Program.cs
src/Presentation/IngenIA365ERP.Shared/
  Services/Core/CatalogosDePersona.cs, PersonasClient.cs, PersonasDtos.cs
  Models/Personas/PersonaFormularioModelo.cs
  Services/Security/PermisosDelUsuario.cs
  Components/Shared/PermissionGate.razor (movido desde Web.Client), PersonSearchPicker.razor
  Components/Personas/PersonaCampos.razor, PersonaFormulario.razor, PersonaDialog.razor
  Pages/Maestros/Personas.razor, Pages/Nomina/Empleados.razor, Pages/Asociados/RegistroAsociado.razor
tests/
  IngenIA365ERP.Application.Tests/Core/People/, Payroll/EmployeeManagement/, Security/
  IngenIA365ERP.Architecture.Tests/Principles/   # LaPersonaSeEscribeEnUnSoloSitio, LosMaestrosDePersonaExigenPermiso
  IngenIA365ERP.API.IntegrationTests/Core/AltaDePersonaEnUnPasoTests.cs
```

**Structure Decision**: se respeta la estructura existente de cuatro capas; no hay proyecto
nuevo. Lo único que **cambia de sitio** es `PermissionGate.razor` (de `Web.Client/Components` a
`Shared/Components/Shared`) porque las páginas que lo usan viven en `Shared` y hoy lo compilan
como etiqueta HTML desconocida.

## Complexity Tracking

Sin violaciones que justificar: ninguna compuerta está en `FAIL`.
