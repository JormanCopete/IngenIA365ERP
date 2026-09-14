# Tasks: Alta de persona en un paso desde los módulos

**Input**: Design documents from `/specs/008-alta-persona-un-paso/` — [spec.md](spec.md),
[plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/api.md](contracts/api.md), [quickstart.md](quickstart.md)

**Tests**: incluidos. La spec exige pruebas automatizadas en SC-002 (regresión de banderas),
SC-003 y SC-004 (arquitectura), y el plan fija pruebas de Application y e2e. Se escriben antes
de la implementación de cada historia y deben fallar primero.

**Organization**: por historia de usuario. **Las historias no son independientes** en esta
feature y el orden de fases sigue las dependencias del código, no la prioridad nominal:
US3 (banderas) y US5 (permisos) son el suelo sobre el que se paran US4 (formulario compartido)
y, encima, US1 y US2 (alta en un paso). Cada fase termina en un checkpoint verificable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede ir en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: US1 empleado en un paso · US2 asociado en un paso · US3 banderas · US4 un solo
  formulario · US5 permisos efectivos
- Rutas exactas en cada tarea

## Path Conventions

- `A/` = `src/Core/IngenIA365ERP.Application/`
- `Id/` = `src/Infrastructure/IngenIA365ERP.Identity/`
- `Mig/` = `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/`
- `API/` = `src/Presentation/IngenIA365ERP.API/`
- `Sh/` = `src/Presentation/IngenIA365ERP.Shared/`
- `TA/` = `tests/IngenIA365ERP.Application.Tests/` · `TArq/` = `tests/IngenIA365ERP.Architecture.Tests/Principles/` ·
  `TE2E/` = `tests/IngenIA365ERP.API.IntegrationTests/`

---

## Phase 1: Setup

**Purpose**: línea base verificable antes de tocar código.

- [x] T001 Artefactos spec-kit en `specs/008-alta-persona-un-paso/` (spec, plan, research, data-model, contracts/api.md, quickstart, diagnostico-banderas.sql) y `.specify/feature.json`; bloque SPECKIT de `CLAUDE.md` apuntando a la 008
- [x] T002 Línea base en la rama: `dotnet build IngenIA365ERP.CI.slnf -c Release` sin errores y `dotnet test tests/IngenIA365ERP.Application.Tests` + `tests/IngenIA365ERP.Architecture.Tests` verdes; anotar los conteos para compararlos al cierre — **2026-09-13: 0 errores, Application 604, Architecture 56**

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: contratos y fábricas de Application, catálogo de permisos y cliente HTTP compartido.
Sin esto ninguna historia puede empezar: los compuestos necesitan las fábricas, los endpoints
protegidos necesitan los códigos en el catálogo, y las pantallas necesitan el cliente.

**⚠️ CRITICAL**: no arranca ninguna historia hasta terminar esta fase.

- [x] T003 `PersonInput` (record `init`, sin `IsEmployee/IsAssociate/IsSalesperson`) + `PersonInputValidator` (reglas hoy repetidas en Create/Update: `LastName`, `FirstName`, `TaxId`, `IdType` requeridos; `Email` formato; longitudes; `DateOfBirth` pasada) en `A/Core/People/Contracts/PersonInput.cs`
- [x] T004 [P] `EmployeeInput` (los 13 campos laborales/banca de `RegisterEmployeeCommand`) + `EmployeeInputValidator` (`BaseSalary > 0`, `HireDate`, `ContractType 0..10`, `PayrollBankAccountType 0..2`) en `A/Payroll/EmployeeManagement/Contracts/EmployeeInput.cs`
- [x] T005 [P] `AssociateInput` (los 23 campos de `RegisterAssociateCommand` salvo `PersonPublicId`) + `AssociateInputValidator` (`ContributionRate ≥ 0`, `ExternalSalary ≥ 0`) en `A/Core/Associates/Contracts/AssociateInput.cs`
- [x] T006 `PersonFactory.PrepareAsync(PersonInput, ct)` en `A/Core/People/Services/PersonFactory.cs`: busca `TaxId` con `IgnoreQueryFilters()`; viva → `Person.TaxIdDuplicate` («Ya existe {FullName} con ese documento.»); eliminada → `Person.TaxIdDeleted` («Ese documento pertenece a una persona eliminada el {DeletedAt:d}: {FullName}.»); `CityPublicId` → `Person.CityNotFound`; construye `Person` con auditoría y la **agrega sin guardar**
- [x] T007 [P] `EmployeeRegistrar.PrepareAsync(Person, EmployeeInput, ct)` en `A/Payroll/EmployeeManagement/Services/EmployeeRegistrar.cs`: `Employee.AlreadyExists` (sólo `Status != -1`), lookups por `PublicId`, `Payroll.PlanNotFound`, `Payroll.WorkRiskRateNotFound`, construye `Employee` (`Status = 1`, `RehireDate = MaxValue`) + `SalaryChange` inicial por navegación, `person.IsEmployee = true`, sin guardar
- [x] T008 [P] `AssociateRegistrar.PrepareAsync(Person, AssociateInput, ct)` en `A/Core/Associates/Services/AssociateRegistrar.cs`: `Associate.AlreadyExists`, lookups, construye `Associate`, `person.IsAssociate = true`, sin guardar
- [x] T009 Registrar las tres fábricas (`AddScoped`) en `A/DependencyInjection.cs`
- [x] T010 `CreatePersonCommand : PersonInput` y handler sobre `PersonFactory` + un `SaveChanges` en `A/Core/People/Commands/CreatePerson/CreatePersonCommand.cs` y `CreatePersonCommandHandler.cs` (el validador pasa a `Include(new PersonInputValidator())`); JSON plano compatible con `Personas.razor` y `NominaE2E` (`isEmployee` recibido se ignora); el handler (y los compuestos T041/T049) capturan la violación de `UK_COR_People_TaxId` en `SaveChanges` (`DbUpdateException` con `23505` en PostgreSQL / `2601`/`2627` en SQL Server), releen la persona por `TaxId` y devuelven `Person.TaxIdDuplicate` con el nombre — es lo que la spec promete cuando dos usuarios crean la misma persona a la vez; sin esto el segundo recibe 500
- [x] T011 [P] `RegisterEmployeeCommand : EmployeeInput { Guid PersonPublicId }` sobre `EmployeeRegistrar`, **un solo** `SaveChanges` (hoy dos, líneas 222/226) en `A/Payroll/EmployeeManagement/Commands/RegisterEmployee/RegisterEmployeeCommand.cs`
- [x] T012 [P] `RegisterAssociateCommand : AssociateInput { Guid PersonPublicId }` sobre `AssociateRegistrar` en `A/Core/Associates/Commands/RegisterAssociate/RegisterAssociateCommand.cs`
- [x] T013 Catálogo de permisos: nuevo `Id/Seed/CorePermissionCatalogSeeder.cs` (`Core.People.{View,Create,Update,Delete}`, `Core.Associates.{View,Create,Update}`, patrón de `PayrollPermissionCatalogSeeder`); `Id/Seed/PayrollPermissionCatalogSeeder.cs` += `Payroll.Employees.{View,Create,Update,Terminate}`; `Id/Seed/PhaseZeroSecuritySeeder.cs` llama al seeder nuevo
- [x] T014 `Id/Seed/BuiltInRolesSeeder.cs`: `Operator` += `Core.People.Create`, `Core.People.Update`, `Core.Associates.Create`, `Core.Associates.Update`, `Payroll.Employees.Create`, `Payroll.Employees.Update` (Delete/Terminate sólo CompanyAdmin); nuevo paso `ConcederLecturaDeMaestrosATodosLosRolesAsync` (todo rol de la cooperativa recibe los tres `*.View` si le faltan); al insertar vínculos invalidar `IPermissionClaimsCache.InvalidateAllForTenantAsync`
- [x] T015 [P] Cliente compartido: `Sh/Services/Core/CatalogosDePersona.cs` (tipos de documento, persona, género, estado civil, nivel educativo, estado — un solo sitio), `Sh/Services/Core/PersonasDtos.cs` (`PersonaDto`, `PersonaBusquedaDto`, `PersonaPorDocumentoDto`, `AltaEmpleadoConPersona`, `AltaAsociadoConPersona`), `Sh/Services/Core/PersonasClient.cs` (patrón `NominaClient`: `ObtenerAsync`, `CrearAsync`, `ActualizarAsync`, `BuscarAsync(q, rol)`, `PorDocumentoAsync`, `RestaurarAsync`, `RegistrarEmpleadoConPersonaAsync`, `RegistrarAsociadoConPersonaAsync`); registro en `src/Presentation/IngenIA365ERP.Web.Client/Program.cs` y `src/Presentation/IngenIA365ERP.Web/Program.cs`
- [x] T016 Pruebas de fundación en `TA/Core/People/PersonInputValidatorTests.cs`, `TA/Core/People/PersonFactoryTests.cs` (viva → `TaxIdDuplicate`; eliminada → `TaxIdDeleted`; nunca segunda fila; `CityNotFound`; no guarda; una violación de unicidad en el guardado se traduce a `TaxIdDuplicate` con el nombre), `TA/Security/RepartoDePermisosTests.cs` (Operator: Create/Update sí, Delete/Terminate no; ReadOnly y Auditor sólo View; `ConcederLectura` agrega View a un rol personalizado sin tocar lo demás)

**Checkpoint**: compila; `NominaE2E` sigue verde (POST people con `isEmployee=true` no rechaza); al arrancar la API local el log muestra los dos seeders sin error.

---

## Phase 3: User Story 3 — Las banderas de rol no se pisan entre módulos (Priority: P1)

**Goal**: las banderas Empleado/Asociado/Vendedor las escribe sólo el handler de la fila hija;
el reingreso crea ficha nueva y la consulta por persona devuelve la viva; las bases ya
sembradas se reconcilian.

**Independent Test**: registrar como empleada a una persona asociada, editar su correo por
`PUT /api/core/people/{id}`, terminar el contrato, reingresarla: en cada paso las banderas y
`by-person` responden lo que dice el spec (US3 escenarios 1-3, 5, 6). El escenario 4 (pestaña
Roles) se entrega con `PersonaCampos` en US4.

### Tests for User Story 3

- [x] T017 [P] [US3] `TA/Core/People/UpdatePersonCommandHandlerTests.cs`: asociada+empleada conserva ambas tras `Update` con las simples cambiadas; `IsCustomer` sí cambia
- [x] T018 [P] [US3] `TA/Payroll/EmployeeManagement/RegisterEmployeeCommandHandlerTests.cs`: conserva `IsAssociate`; un solo `SaveChanges`; tras `Terminate` un segundo `Register` crea ficha nueva y `GetEmployeeByPersonIdQuery` devuelve la viva

### Implementation for User Story 3

- [x] T019 [US3] `UpdatePersonCommand : PersonInput { Guid PublicId }` y handler que **no** escribe `IsAssociate/IsEmployee/IsSalesperson` (hoy líneas 108-115 sobrescriben las ocho) en `A/Core/People/Commands/UpdatePerson/UpdatePersonCommand.cs`
- [x] T020 [US3] `GetEmployeeByPersonIdQuery`: `Where(Status != -1).OrderByDescending(HireDate).FirstOrDefault()`; `EmployeeDetailDto` += `PersonPublicId` en `A/Payroll/EmployeeManagement/Queries/EmployeeQueries.cs` — **más** (hallazgo e2e): `UK_PAY_Employees_PersonId` era único sin filtro y el reingreso daba 500; `EmployeeConfiguration` lo filtra a fichas vivas y la migración en par `UnaSolaFichaVivaPorPersona` (reversible) lo aplica
- [x] T021 [US3] `DeleteSalespersonCommand`: al eliminar la ficha, `person.IsSalesperson = false` (`UpdatedAt/By`) en el mismo `SaveChanges`, en `A/Inventory/Salespeople/Commands/DeleteSalesperson/DeleteSalespersonCommand.cs`; prueba `TA/Inventory/Salespeople/DeleteSalespersonCommandHandlerTests.cs` (hoy sólo hace `IsDeleted = true`: viola el Principio V y desharía la reconciliación)
- [x] T022 [US3] Migración en par `ReconciliarBanderasDerivadasDePersona` (`tools/scripts/add-migration.ps1 -Name ReconciliarBanderasDerivadasDePersona -Context Application`; SQL a mano según data-model §5: seis `UPDATE` guardados por desalineación, `UpdatedBy = 'system:migration:008'`, sin `DELETE`; `Down` no-op con comentario: no hay estado previo que valga restaurar y volver a correr `Up` es idempotente) en `Mig/*_ReconciliarBanderasDerivadasDePersona.cs`
- [x] T023 [US3] Verificar `PrincipioXII_MigracionesDestructivas` y `Feature004_MigrationParity` verdes con la migración nueva; correr `specs/008-alta-persona-un-paso/diagnostico-banderas.sql` contra la base local antes y después de `AutoMigrate` (seis ceros después) — **paridad y XII verdes (56/56); el antes/después con la consulta se hace en QA por `kubectl exec` (T055), la base local no tiene `psql` sin credenciales en línea**

**Checkpoint**: US3 escenarios 1-3, 5 y 6 verificables por HTTP con `curl`/e2e existentes.

---

## Phase 4: User Story 5 — Permisos efectivos sobre personas, empleados y asociados (Priority: P2)

**Goal**: la API exige permiso por ruta (404 indistinguible), el cliente conoce sus permisos
en la cooperativa activa y `PermissionGate` deja de ser inerte.

**Independent Test**: con `ReadOnly`, `POST /api/payroll/employees` responde igual que
`POST /api/payroll/no-existe`; `/api/admin/permissions/mine` lista los códigos por rol; en la
pantalla un `PermissionGate` con un código que el usuario no tiene muestra el aviso y al cambiar
de cooperativa se recalcula sin F5.

### Tests for User Story 5

- [x] T024 [P] [US5] `TA/Security/GetMyPermissionsQueryHandlerTests.cs`: devuelve la unión de permisos; maestro → `IsGlobalMasterAdmin=true` y lista vacía
- [x] T025 [P] [US5] `TArq/LosMaestrosDePersonaExigenPermiso.cs`: cada `Map*` de `API/Endpoints/Core/PeopleEndpoints.cs`, `PeopleDetailEndpoints.cs`, `AssociatesEndpoints.cs` y `API/Endpoints/Payroll/EmployeesEndpoints.cs` lleva `.RequirePermission(` (falla hoy)

### Implementation for User Story 5

- [x] T026 [US5] `ICurrentUserPermissions.ListAsync(ct)` en `A/Common/Interfaces/Security/ICurrentUserPermissions.cs`; `GetMyPermissionsQuery` → `MyPermissionsDto(bool IsGlobalMasterAdmin, IReadOnlyList<string> Permissions)` en `A/Security/Permissions/GetMyPermissionsQuery.cs`
- [x] T027 [US5] `API/Services/PermisosDelHandler.cs` implementa también `ICurrentUserPermissions` (sobre `PermisosDeLaPeticion`); registro en `API/Program.cs`; `GET /api/admin/permissions/mine` en `API/Endpoints/PermissionsModule.cs` (sesión + cooperativa; sin `RequirePermission` propio)
- [x] T028 [US5] `RequirePermission` en todas las rutas de `API/Endpoints/Core/PeopleEndpoints.cs` (View/Create/Update/Delete), `API/Endpoints/Core/PeopleDetailEndpoints.cs` (View), `API/Endpoints/Core/AssociatesEndpoints.cs` (View/Create/Update) y `API/Endpoints/Payroll/EmployeesEndpoints.cs` (View/Create/Update/Terminate) según contracts §1
- [x] T029 [US5] `Sh/Services/Security/PermisosDelUsuario.cs` (`Scoped`): `TieneAsync(codigo)`, `ObtenerAsync(forzar)`, evento `Cambiaron`; llama `/api/admin/permissions/mine` sólo con cooperativa activa; maestro → todo; se invalida en `CentralAuthClient.Authenticated`/`SignedOut` (el cambio de cooperativa dispara `Authenticated` al acuñar el token nuevo y ya navega fuera de la página: no hay diálogos que cerrar); **falla cerrado** en dos grados: sin sesión o sin cooperativa activa (prerender del Web, pantallas de ingreso) → **silencio**, `ObtenerAsync` devuelve vacío sin llamar a la API y el gate no rinde `ChildContent`; con sesión y cooperativa pero la llamada a `/mine` falla → vacío **y** aviso una sola vez (IX); DTO `PermisosMiosDto` en `Sh/Services/Security/PermisosDtos.cs`; prueba en `tests/IngenIA365ERP.Shared.Tests/Seguridad/PermisosDelUsuarioTests.cs` (sin cooperativa no hay petición ni aviso; con fallo HTTP hay aviso y ningún permiso); registro en `Web.Client/Program.cs` y `Web/Program.cs`
- [x] T030 [US5] Mover `PermissionGate` a `Sh/Components/Shared/PermissionGate.razor` sobre `PermisosDelUsuario` (rinde `ChildContent` sólo con permisos cargados; sin `Fallback` pinta «No tenés permiso para esta sección»); **borrar** `src/Presentation/IngenIA365ERP.Web.Client/Components/PermissionGate.razor`; revisar las 7 páginas que lo usan (`Sh/Pages/Administracion/AuditLogConsole.razor`, `Sh/Pages/Compliance/HabeasData/Consents/RecordConsent.razor`, `RecordRevocation.razor`, `Persons/History.razor`, `Policies/Index.razor`, `Policies/Publish.razor`, `Sh/Pages/Notifications/Center.razor`) para que el aviso sea el correcto ahora que el gate funciona
- [x] T031 [US5] `Sh/Services/NotificationServiceExtensions.cs`: 404 `Generic.NotFound` en POST/PUT/DELETE → «No tenés permiso para esta acción o el registro ya no existe.»

**Checkpoint**: T025 verde; `curl` con token `ReadOnly` a `POST /api/payroll/employees` → 404 `Generic.NotFound`; `/mine` responde por rol; e2e existentes de Nómina siguen verdes (sus usuarios son CompanyAdmin).

---

## Phase 5: User Story 4 — Un solo formulario de persona (Priority: P2)

**Goal**: `PersonaCampos`/`PersonaFormulario`/`PersonaDialog` alimentan Personas (y luego
Empleados y Asociados); la pestaña Roles muestra las derivadas como distintivo no editable con
«Registrar como…».

**Independent Test**: `Personas.razor` crea y edita con el diálogo compartido sin cambio
funcional visible; un campo agregado a `PersonaCampos` aparece en Personas sin tocarla; en
Roles, una empleada muestra «Empleado» como distintivo y «Registrar como asociado…».

### Tests for User Story 4

- [x] T032 [P] [US4] `tests/IngenIA365ERP.Shared.Tests/Personas/PersonaFormularioModeloTests.cs`: `DesdeDto` ↔ `AInput()` ida y vuelta; `Validar()` exige documento, nombres y correo válido; nunca expone las derivadas como editables

### Implementation for User Story 4

- [x] T033 [US4] `Sh/Models/Personas/PersonaFormularioModelo.cs` (el `PersonModel` de Personas, público, DataAnnotations, banderas simples editables, `EsEmpleado/EsAsociado/EsVendedor` de sólo lectura, `DesdeDto`, `AInput()`, `Validar()`)
- [x] T034 [US4] `Sh/Components/Personas/PersonaCampos.razor` (`Seccion = Identificacion|Contacto|Demografia|Roles`, `Modelo`, `Deshabilitado`, `Ciudades`, `MostrarEstado`, `OnRegistrarRol`; catálogos de `CatalogosDePersona`; en Roles 5 checks simples + distintivos derivados con botón «Registrar como {rol}…» sólo si `OnRegistrarRol` tiene delegado); `Sh/_Imports.razor` += `IngenIA365ERP.Shared.Components.Personas`
- [x] T035 [US4] `Sh/Components/Personas/PersonaFormulario.razor` (`SfTab` con las cuatro secciones) y `Sh/Components/Personas/PersonaDialog.razor` (`Visible/VisibleChanged`, `PersonaPublicId?` null = crear, `DocumentoInicial`, `RolSimpleInicial` reservado para fase 2, `OnGuardado(Guid)`, `OnRegistrarRol`; `IndicadorDeCarga` + `EstadoDeCarga` «Cargando…»/«Guardando…»; `_saving`; muestra el mensaje del servidor en `Person.TaxIdDuplicate`/`Person.TaxIdDeleted`; convención `ConfirmDialog`)
- [x] T036 [US4] `Sh/Pages/Maestros/Personas.razor` sobre `PersonaDialog`: quitar diálogo inline (67-289), `PersonModel` (684-747) y catálogos (321-371); distintivos en la grilla; «Registrar como empleado…» navega a `/nomina/empleados?persona={id}` y «…asociado…» a `/asociados/registro?persona={id}` (sólo con el permiso de crear de ese recurso, vía `PermisosDelUsuario`); botones Nueva/Editar/Eliminar dentro de `PermissionGate` (`Core.People.Create/Update/Delete`)

**Checkpoint**: Personas funciona igual que antes para el usuario, con ~300 líneas menos; T032 verde.

---

## Phase 6: User Story 1 — Registrar un empleado nuevo en un solo paso (Priority: P1) 🎯 MVP

**Goal**: «Nuevo empleado» → documento inexistente → «Crear persona nueva con "…"» → un diálogo
con persona editable + datos laborales → un «Registrar» atómico. Persona existente de sólo
lectura + «Editar datos de la persona». Duplicada → «Usar esa persona»; eliminada → «Restaurar
persona» (con permiso). Reingreso → ficha nueva.

**Independent Test**: quickstart §3 pasos 1-4 con Operador y CompanyAdmin; e2e
`AltaDePersonaEnUnPasoTests` (casos de empleado).

### Tests for User Story 1

- [x] T037 [P] [US1] `TA/Payroll/EmployeeManagement/RegisterEmployeeWithPersonCommandHandlerTests.cs`: crea `Person`+`Employee`+`SalaryChange` en un `SaveChanges`; `Payroll.PlanNotFound` y `Person.TaxIdDuplicate`/`TaxIdDeleted` no persisten nada; validador compuesto acumula errores de ambas partes
- [x] T038 [P] [US1] `TA/Core/People/RestorePersonCommandHandlerTests.cs`: misma fila, `IsDeleted=false`, `DeletedAt/By` en null, derivadas recalculadas desde tablas hijas, `Person.NotDeleted` si estaba viva, `Person.NotFound` si no existe
- [x] T039 [P] [US1] `TA/Core/People/SearchPeopleQueryHandlerTests.cs`: `Role` filtra por bandera; DTO trae las ocho; `GetPersonByDocumentQuery` encuentra eliminadas y `IsDeleted=true`
- [x] T040 [US1] `TE2E/Core/AltaDePersonaEnUnPasoTests.cs` (colección «Nomina e2e», helpers de `TE2E/Payroll/NominaE2E.cs`): `with-person` 201 + `isEmployee` + `by-person` con `personPublicId`; documento duplicado 422 sin registrar; documento de eliminada 422 `Person.TaxIdDeleted` sin fila nueva; `/restore` con Operador → 404 y con admin → 204 con banderas recalculadas; sólo lectura → 404 idéntico a ruta inexistente; asociado → empleado → `PUT` persona conserva ambas; `terminate` apaga sólo `IsEmployee`; `terminate` + reingreso → dos fichas y `by-person` devuelve la viva; `/permissions/mine` por rol; `/search?rol=`; `/by-document`; auditoría: tras `with-person` existe **un** evento `RegisterEmployeeWithPersonCommand` para ese documento y **ninguno** `CreatePersonCommand`/`RegisterEmployeeCommand` (leer Mongo como hace `TE2E/Audit/AuditAppendOnlyTests.cs`)

### Implementation for User Story 1

- [x] T041 [US1] `RegisterEmployeeWithPersonCommand(PersonInput Person, EmployeeInput Employee)` → `Result<RegisterEmployeeWithPersonResult(Guid PersonPublicId, Guid EmployeePublicId)>`; validador con `SetValidator` de ambos; handler: `PersonFactory` → `EmployeeRegistrar` → **un** `SaveChanges` en `A/Payroll/EmployeeManagement/Commands/RegisterEmployeeWithPerson/RegisterEmployeeWithPersonCommand.cs`
- [x] T042 [P] [US1] `RestorePersonCommand(Guid PublicId)` + validador + handler (`IgnoreQueryFilters`, misma fila, recalcula `IsEmployee/IsAssociate/IsSalesperson`, `Person.NotDeleted`, `Person.NotFound`) en `A/Core/People/Commands/RestorePerson/RestorePersonCommand.cs`
- [x] T043 [P] [US1] `SearchPeopleQuery(string SearchTerm, string? Role)` con `PersonSearchDto` += `IsSalesperson, IsCustomer, IsSupplier, IsAdvisor, IsThirdParty, ReceivesInvoice` en `A/Core/People/Queries/SearchPeopleQuery.cs`; `GetPersonByDocumentQuery(string TaxId)` → `PersonByDocumentDto` (eliminadas incluidas) en `A/Core/People/Queries/GetPersonByDocumentQuery.cs`
- [x] T044 [US1] Endpoints: `POST /api/payroll/employees/with-person` (`RequirePermission("Payroll.Employees.Create").RequirePermission("Core.People.Create")`, `ErrorEnvelopeFilter`, 201 con el resultado) en `API/Endpoints/Payroll/EmployeesEndpoints.cs`; `POST /api/core/people/{id}/restore` (`Core.People.Delete`, 204) en `API/Endpoints/Core/PeopleEndpoints.cs`; `GET /api/core/people/by-document?taxId=` (`Core.People.View`; en query string, no en la ruta: `UseSerilogRequestLogging` registra `RequestPath` y no la query, igual que `/search?q=`) y `search?q=&rol=` en `API/Endpoints/Core/PeopleDetailEndpoints.cs`
- [x] T045 [US1] `Sh/Components/Shared/PersonSearchPicker.razor`: `PermitirCrear`, `OnCrearPersona(termino)`, `FiltroRol`, `TextoSinResultados`; sin resultados: botón «Crear persona nueva con "{término}"» (si `PermitirCrear` y `Core.People.Create`) o texto «Pedile a quien administra Maestros › Personas»; término numérico sin resultados consulta `/by-document` y, si es eliminada, muestra el aviso con «Restaurar persona» (`Core.People.Delete`); el `catch { _options = ... }` (línea 144) pasa a avisar con `Notification`; `IndicadorDeCarga Compacto` mientras busca; se retira el enlace en otra pestaña (72-78); `PersonSearchItem` se muda a `PersonasDtos` (alias temporal para las llamadas existentes)
- [x] T046 [US1] `Sh/Pages/Nomina/Empleados.razor`: `ModoAlta` (persona nueva editable | existente sólo lectura); `SfTab` propia con `PersonaCampos` (Identificación/Contacto/Demografía, `Deshabilitado` según modo, sin Roles) + pestañas laborales/banca; botón «Editar datos de la persona» → `PersonaDialog` (con `Core.People.Update`) y refresco al guardar; «Registrar» llama `PersonasClient.RegistrarEmpleadoConPersonaAsync` o `POST /api/payroll/employees` según modo; **quitar** `_personModel`, catálogos locales (489+) y el `PUT` de persona (749-768); `OpenEditFromRow` usa `EmployeeDetailDto.PersonPublicId` (no busca por documento, 689-691); `[SupplyParameterFromQuery] Guid? Persona` abre el diálogo; 422 `Person.TaxIdDuplicate` → «Usar esa persona» (via `/by-document`); `Person.TaxIdDeleted` → «Restaurar persona»/texto; `IndicadorDeCarga` en diálogo y grilla; conservar el flag `_saving`; botones Nuevo/Editar/Terminar dentro de `PermissionGate`

**Checkpoint**: quickstart §3 pasos 1-4 con Operador y CompanyAdmin; T037-T040 verdes; `NominaE2E` verde.

---

## Phase 7: User Story 2 — Registrar un asociado nuevo en un solo paso (Priority: P1)

**Goal**: mismo flujo para «Nuevo asociado»; `RegistroAsociado` deja de tragar excepciones y
entra al indicador de carga.

**Independent Test**: registrar un asociado con documento inexistente en un diálogo y verlo en
Personas con «Asociado»; persona ya asociada → edición; e2e (casos de asociado).

### Tests for User Story 2

- [x] T047 [P] [US2] `TA/Core/Associates/RegisterAssociateWithPersonCommandHandlerTests.cs`: `Person`+`Associate` en un `SaveChanges`; duplicada/eliminada no persisten nada; conserva `IsEmployee` si la persona existente ya era empleada
- [x] T048 [US2] `TE2E/Core/AltaDePersonaEnUnPasoTests.cs` += `associates/with-person` 201 + `isAssociate` + `by-person`; `Associate.AlreadyExists` 422; ReadOnly → 404

### Implementation for User Story 2

- [x] T049 [US2] `RegisterAssociateWithPersonCommand(PersonInput Person, AssociateInput Associate)` → `Result<RegisterAssociateWithPersonResult(Guid PersonPublicId, Guid AssociatePublicId)>` + validador compuesto + handler (un `SaveChanges`) en `A/Core/Associates/Commands/RegisterAssociateWithPerson/RegisterAssociateWithPersonCommand.cs`
- [x] T050 [US2] `POST /api/core/associates/with-person` (`Core.Associates.Create` **y** `Core.People.Create`, `ErrorEnvelopeFilter`, 201) en `API/Endpoints/Core/AssociatesEndpoints.cs`
- [x] T051 [US2] `Sh/Pages/Asociados/RegistroAsociado.razor`: mismo patrón que T046 (`ModoAlta`, `PersonaCampos`, «Editar datos de la persona», `RegistrarAsociadoConPersonaAsync`, quitar modelo/catálogos/`PUT` 581-592, `?persona=`, duplicada/eliminada); `IndicadorDeCarga` + `EstadoDeCarga` en grilla y diálogo; sus `catch` silenciosos pasan a `Notification.ErrorAsync`; conservar el flag `_saving`; botones dentro de `PermissionGate` (`Core.Associates.Create/Update`)

**Checkpoint**: quickstart §3 paso 1 (asociado) y US2 escenario 2; T047-T048 verdes.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: cerrar las pruebas de arquitectura que sólo pueden pasar con las tres pantallas
migradas, documentar, desplegar.

- [x] T052 [P] `TArq/LaPersonaSeEscribeEnUnSoloSitio.cs`: ningún `.razor` fuera de `Sh/Components/Personas/` y `Sh/Pages/Maestros/Personas.razor` hace POST/PUT a `/api/core/people` ni declara `class PersonModel`; sólo `PersonSearchPicker` llama `/api/core/people/search`, con allowlist explícita de los buscadores ad-hoc que vacía la fase 2 (`Pages/CarteraFinanciera/Recaudos.razor`, `SolicitudCredito.razor` y los demás que el escaneo encuentre)
- [x] T053 [P] `TArq/LasPantallasDicenQueEstanCargando.cs`: `ModulosMigrados` += `"Asociados"`; `TArq/PrincipioIX_NoEmptyCatch.cs` cubre `catch { _options =` y `catch { /* fall through`
- [x] T054 [P] `docs/manual/alta-de-persona-desde-modulos.md` (receta para la fase 2: `PersonaDialog` con `RolSimpleInicial`, picker con `PermitirCrear`, migrar buscadores ad-hoc; regla de banderas; códigos de permiso), `CLAUDE.md` (sección Core: regla de banderas derivadas, restaurar persona, reingreso, permisos de maestros, `PermissionGate` en Shared), `docs/operaciones/estado-y-pendientes.md` (feature 008 y fase 2 pendiente), `README.md` si menciona el flujo viejo
- [x] T055 Correr `quickstart.md` §1-§3 completo en local; conteos de pruebas comparados con T002 (`CLAUDE.md` «Totales» si cambian) — **§1 y §2 hechos (938 sin contenedores: +157 sobre la línea base; e2e 152/153 con la omitida de siempre); §3 (recorridos a mano en el navegador por rol) queda para QA con el dueño, que es quien tiene la sesión**
- [ ] T056 Merge a `develop` (dos PR: **componente + Personas + servidor** primero, **Empleados + Asociados** después), CI verde (~9 min), Argo DEV/QA; verificación en QA con Operador / sólo lectura / rol personalizado / cambio de cooperativa (quickstart §4) — **2026-09-13: rama confirmada (`4015f24`) y mergeada a `develop` en un solo merge (`db9f1da`) por pedido del dueño, incluida la corrección de P14 (`MotivoDeRetiroComoTexto`); CI en curso; la verificación por rol en QA queda pendiente**
- [ ] T057 Promoción a producción con **autorización expresa**: `pg_dump` de `ingenia365erp` y `cooflopal`, `diagnostico-banderas.sql` antes y después (seis ceros), release notes con los códigos de permiso nuevos

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)**: bloquea todo lo demás.
- **US3 (Phase 3)**: necesita T003-T012 (contratos, fábricas, comandos sobre fábricas).
- **US5 (Phase 4)**: necesita T013-T014 (códigos en el catálogo; sin ellos `RequirePermission`
  daría 404 a todo el mundo) y T015 (cliente).
- **US4 (Phase 5)**: necesita US5 (`PermisosDelUsuario`/`PermissionGate` para los botones y el
  «Registrar como…») y T015 (`CatalogosDePersona`, `PersonasClient`).
- **US1 (Phase 6)**: necesita US3 (`by-person` sólo ficha viva, `UpdatePerson` sin derivadas),
  US4 (`PersonaCampos`, `PersonaDialog`) y US5 (gate, `/mine`).
- **US2 (Phase 7)**: necesita lo mismo que US1 y reutiliza T042-T045.
- **Polish (Phase 8)**: T052 sólo puede pasar con T036, T046 y T051 hechos.

### Within Each User Story

- Pruebas primero y en rojo; modelos → servicios → endpoints → pantalla.
- Tareas sobre el mismo archivo van en serie (T044 y T050 tocan endpoints distintos; T028 y
  T044 tocan `PeopleEndpoints.cs` y `EmployeesEndpoints.cs`: T028 antes).

### Parallel Opportunities

- Phase 2: T004 ∥ T005 tras T003; T007 ∥ T008 tras T006; T011 ∥ T012 tras T010; T015 ∥ todo el
  bloque de Application; T013 → T014 en serie.
- Phase 3: T017 ∥ T018. Phase 4: T024 ∥ T025. Phase 6: T037 ∥ T038 ∥ T039; T042 ∥ T043 tras T041.
- Phase 8: T052 ∥ T053 ∥ T054.

---

## Parallel Example: Phase 6 (US1)

```bash
# Pruebas de la historia, juntas y en rojo:
Task: "RegisterEmployeeWithPersonCommandHandlerTests en TA/Payroll/EmployeeManagement/"
Task: "RestorePersonCommandHandlerTests en TA/Core/People/"
Task: "SearchPeopleQueryHandlerTests en TA/Core/People/"

# Application, tras el compuesto:
Task: "RestorePersonCommand en A/Core/People/Commands/RestorePerson/"
Task: "SearchPeopleQuery(Role) + GetPersonByDocumentQuery en A/Core/People/Queries/"
```

---

## Implementation Strategy

### MVP First (US1 con su suelo)

1. Phase 1-2 (línea base, contratos, fábricas, catálogo de permisos, cliente).
2. Phase 3 (US3) y Phase 4 (US5): el servidor queda íntegro y protegido aunque las pantallas
   sigan como hoy — **desplegable por sí solo** (cierra el bug de banderas y los permisos).
3. Phase 5 (US4): Personas sobre el componente, sin cambio funcional visible — **PR 1**.
4. Phase 6 (US1): empleado en un paso — **demo del caso que motivó la feature**.
5. **STOP and VALIDATE**: quickstart §3.

### Incremental Delivery

- PR 1 = Phases 1-5 (servidor + componente + Personas). PR 2 = Phases 6-8 (Empleados,
  Asociados, arquitectura, docs). Cada PR pasa CI y se promueve a DEV/QA antes del siguiente.
- Producción sólo al cierre, con respaldo y autorización expresa (T057).

---

## Notes

- Los códigos de error nuevos: `Person.TaxIdDeleted`, `Person.NotDeleted`,
  `Payroll.WorkRiskRateNotFound`; los existentes no cambian de texto salvo `Person.TaxIdDuplicate`,
  que ahora nombra a la persona.
- `RehireDate` no se toca ni se expone (R5). `IsSalesperson` sigue el mismo modelo pero la
  pantalla de Vendedores no cambia (fase 2).
- Nada de contraseñas ni secretos en scripts ni en el repositorio; el `pg_dump` y las consultas a
  PDN van por `k3s kubectl exec` sin credenciales en línea de comandos.
