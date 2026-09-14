# Research: Alta de persona en un paso desde los módulos

**Feature**: 008 | **Date**: 2026-09-13

No quedó ningún `NEEDS CLARIFICATION` en el Technical Context: todo lo que hacía falta se
resolvió leyendo el código existente y con las seis decisiones del dueño. Cada punto sigue el
formato Decision / Rationale / Alternatives.

## R1. Atomicidad sin transacción explícita

- **Decision**: un comando compuesto (`RegisterEmployeeWithPersonCommand`,
  `RegisterAssociateWithPersonCommand`) que agrega `Person` → `Employee` → `SalaryChange` (o
  `Person` → `Associate`) al mismo `DbContext` por navegaciones EF y llama **un** solo
  `SaveChangesAsync`.
- **Rationale**: `IApplicationDbContext` no expone `BeginTransaction`; EF Core envuelve un
  `SaveChanges` en una transacción implícita, que es lo mismo que ya usa `ApprovePayrollRun` para
  corrida + comprobante. `Employee.Person` y `SalaryChange.Employee` son navegaciones existentes,
  así que las FK se resuelven en la inserción sin conocer los `Id` antes. `RegisterEmployeeCommand`
  hoy hace **dos** `SaveChanges` (líneas 222 y 226) sin necesidad; pasa a uno.
- **Alternatives considered**: (a) dos llamadas HTTP con compensación (`DELETE` si falla el
  segundo): deja persona huérfana si el navegador muere entre las dos y el borrado compensatorio
  aparece en auditoría como un borrado real; (b) `sender.Send(new CreatePersonCommand)` anidado
  dentro del handler de empleado: cada `Send` guarda y audita por separado, rompiendo FR-014;
  (c) exponer `BeginTransaction` en la interfaz: cambia un contrato transversal por un caso.

## R2. Fábricas compartidas en Application

- **Decision**: `PersonFactory.PrepareAsync(PersonInput)`, `EmployeeRegistrar.PrepareAsync(Person,
  EmployeeInput)` y `AssociateRegistrar.PrepareAsync(Person, AssociateInput)` — servicios
  `Scoped` de Application que validan reglas de negocio, construyen la entidad y la **agregan al
  contexto sin guardar**. `CreatePerson`, `RegisterEmployee`, `RegisterAssociate` y los dos
  compuestos las usan.
- **Rationale**: «cómo se crea una persona» (duplicado, ciudad, valores por defecto) hoy está
  escrito una vez pero pasaría a necesitarse en tres handlers; la fábrica lo deja en un solo
  sitio y hace trivial la prueba «el compuesto no persiste nada si falla el plan de nómina».
- **Alternatives considered**: métodos estáticos (no pueden consultar el contexto); herencia
  entre handlers (MediatR no lo favorece y mezcla pipelines).

## R3. Contratos de entrada: `PersonInput` sin banderas derivadas

- **Decision**: `PersonInput` (record, `init`) con identificación, contacto, demografía, las
  cinco banderas simples y `Status`; **no** tiene `IsEmployee/IsAssociate/IsSalesperson`.
  `CreatePersonCommand : PersonInput` y `UpdatePersonCommand : PersonInput` para que el JSON de
  `Personas.razor` y de `NominaE2E` siga siendo plano. Un `isEmployee=true` recibido se
  **ignora** (System.Text.Json descarta propiedades desconocidas), nunca se rechaza con 400.
- **Rationale**: el bug de integridad existe porque el contrato permite escribir lo que no
  debería; sacar las propiedades hace el bug imposible por tipo, no por disciplina.
  `NominaE2E.CrearEmpleadoAsync` manda `isEmployee=true` hoy y debe seguir pasando.
- **Alternatives considered**: `PATCH` parcial con `JsonPatch`; banderas opcionales `bool?` que el
  handler respeta si vienen — ambas dejan la puerta abierta.

## R4. Documento de una persona eliminada (decisión del dueño, opción B)

- **Decision**: `PersonFactory` busca el `TaxId` **incluyendo eliminadas** (`IgnoreQueryFilters`).
  Viva → `Person.TaxIdDuplicate` (nombre + `PublicId`). Eliminada → `Person.TaxIdDeleted`
  (nombre + `DeletedAt` + `PublicId`). Nuevo `RestorePersonCommand(PublicId)`:
  `IsDeleted=false`, limpia `DeletedAt/DeletedBy`, recalcula las tres derivadas desde tablas
  hijas, `UpdatedAt/By`, un `SaveChanges`; error `Person.NotDeleted` si no estaba eliminada.
  Endpoint `POST /api/core/people/{id}/restore` con `Core.People.Delete`.
- **Rationale**: `UK_COR_People_TaxId` es único **sin filtro** por `IsDeleted`
  (`PersonConfiguration.cs:103`); el check de duplicado actual excluye eliminadas
  (`CreatePersonCommandHandler.cs:20`), así que hoy la inserción viola el índice y responde 500.
  Restaurar la misma fila conserva todo lo que la referencia (cartera, asientos, corridas) y
  respeta soft-delete + auditoría (Principio VII). Ofrecer el botón donde ocurre el choque evita
  una pantalla nueva de «personas eliminadas».
- **Alternatives considered**: (a) sólo rechazar, restaurar por soporte — deja a la cooperativa
  bloqueada con ese documento; (c) reactivar en silencio con los datos digitados — sorprende y
  pisa datos; (d) índice filtrado `WHERE IsDeleted = false` — dos filas con el mismo documento,
  referencias históricas apuntando a la vieja, y una migración DDL en par por un caso raro.

## R5. Reingreso de un empleado retirado (decisión del dueño, opción A)

- **Decision**: ficha nueva. `Employee.AlreadyExists` sigue mirando sólo `Status != -1` (ya es
  así). `GetEmployeeByPersonIdQuery` pasa a filtrar `Status != -1` y ordenar por `HireDate desc`
  (por si hubiera datos legados con dos vivas, devuelve la más reciente). `RehireDate` sigue en
  `DateTime.MaxValue` como hoy; no se expone ni se usa.
- **Rationale**: las corridas ya liquidadas (`PAY_PayrollRunEmployees`) apuntan a la ficha
  retirada; reabrirla mezclaría dos vínculos laborales en un historial de salarios y violaría el
  Principio XI. Hoy `by-person` es `FirstOrDefault` sin filtro (`EmployeeQueries.cs:139`) y
  puede devolver la retirada. **Hallazgo al implementar (e2e):** `UK_PAY_Employees_PersonId` era
  único sin filtro, así que el reingreso —que la validación ya permitía— reventaba en la base con
  500; se convirtió en índice único filtrado a fichas vivas (migración `UnaSolaFichaVivaPorPersona`,
  reversible, filtro en T-SQL canónico traducido por `ProviderModelConventions`).
- **Alternatives considered**: reabrir la ficha con `RehireDate` (modelo SOLIDO); dejarlo fuera
  de alcance (pero la pantalla decide edición/registro por `IsEmployee`, que esta feature
  redefine, así que había que fijarlo).

## R6. Permisos: catálogo, roles y exposición al cliente

- **Decision**: `CorePermissionCatalogSeeder` nuevo (`Core.People.{View,Create,Update,Delete}`,
  `Core.Associates.{View,Create,Update}`) con el patrón de `PayrollPermissionCatalogSeeder`, que a
  su vez suma `Payroll.Employees.{View,Create,Update,Terminate}`. `BuiltInRolesSeeder`:
  `Operator` += `Core.People.Create/Update`, `Core.Associates.Create/Update`,
  `Payroll.Employees.Create/Update`; `Delete` y `Terminate` quedan sólo en `CompanyAdmin` (`*`).
  Nuevo paso `ConcederLecturaDeMaestrosATodosLosRolesAsync`: **todo** rol de la cooperativa
  (built-in o personalizado) recibe los tres `*.View` si no los tiene. Al insertar vínculos se
  invalida la caché Redis de la cooperativa (`IPermissionClaimsCache.InvalidateAllForTenantAsync`).
  Exposición: `GET /api/admin/permissions/mine` → `MyPermissionsDto(IsGlobalMasterAdmin,
  Permissions[])`, servido por `GetMyPermissionsQuery` sobre `ICurrentUserPermissions`
  (interfaz en Application, implementada por `API/Services/PermisosDelHandler`).
- **Rationale**: los patrones `*.View` de `Auditor/Operator/ReadOnly` ya cubren la lectura de los
  códigos nuevos para built-in; los roles **personalizados** no siguen patrones y por eso hace
  falta el paso explícito (FR-010). La caché de permisos tiene TTL 30 min: sin invalidar, el día
  del despliegue los usuarios verían 404 media hora. `/api/auth/me` es ruta exenta de cooperativa
  y no puede llevar permisos que dependen de ella.
- **Alternatives considered**: claims `perm` en el JWT (el token es central, la cooperativa se
  elige después); permisos en `MeResult` (ruta exenta); un endpoint por recurso (más viajes).

## R7. `PermissionGate` inerte y su traslado

- **Decision**: mover `PermissionGate.razor` de `Web.Client/Components` a
  `Shared/Components/Shared`, alimentado por `PermisosDelUsuario` (servicio `Scoped` de Shared
  que llama `/permissions/mine` una vez por cooperativa, se invalida en
  `CentralAuthClient.Authenticated/SignedOut`, y falla **cerrado**: sin datos no rinde
  `ChildContent`). Sin `Fallback` pinta «No tenés permiso para esta sección».
- **Rationale**: siete páginas de `Shared` lo usan hoy y Razor lo compila como etiqueta HTML
  desconocida porque `Shared` no referencia `Web.Client`: el gate nunca protegió nada. Al
  activarse de verdad, `Notifications.ManageOwn` no lo tienen `ReadOnly` ni `Auditor`: hay que
  revisar esas páginas para que el aviso sea el correcto y no una pantalla vacía.
- **Alternatives considered**: dejar el gate en Web.Client y exponerlo por `RenderFragment`
  inyectado (complejo y no sirve a MAUI); resolver permisos en cada página a mano.

## R8. Componente de formulario de persona

- **Decision**: `PersonaCampos` (una sección por instancia: Identificación / Contacto /
  Demografía / Roles), `PersonaFormulario` (`SfTab` con las cuatro, para el diálogo de Personas)
  y `PersonaDialog` (`SfDialog` + guardar + errores del servidor + `IndicadorDeCarga`). Empleados y
  Asociados componen **su propia** `SfTab` con `PersonaCampos` por pestaña y sus pestañas
  laborales/afiliación. Catálogos en `CatalogosDePersona` (un solo sitio); modelo
  `PersonaFormularioModelo` público con `DesdeDto`, `AInput()`, `Validar()`.
- **Rationale**: un hijo no puede emitir `TabItem` dentro del `TabItems` del padre de forma
  fiable en Syncfusion; por sección sí se compone. Hoy hay tres copias con catálogos
  divergentes (Personas 321-371, Empleados 489+, RegistroAsociado).
- **Alternatives considered**: un componente monolítico con parámetro «modo» (crece sin fin);
  `RenderFragment` por pestaña (más plumbing que valor).

## R9. `PersonSearchPicker` con «crear»

- **Decision**: parámetros `PermitirCrear`, `OnCrearPersona(termino)`, `FiltroRol`,
  `TextoSinResultados`; sin resultados muestra «Crear persona nueva con "{término}"» (si tiene
  `Core.People.Create`) o «Pedile a quien administra Maestros › Personas»; término numérico →
  documento prellenado; el `catch { _options = ... }` silencioso pasa a avisar; `IndicadorDeCarga
  Compacto` mientras busca; se retira el enlace en otra pestaña.
- **Rationale**: la constitución ya obliga a usar este componente para buscar personas; es el
  lugar natural del «no existe → crear». Los ocho buscadores ad-hoc (Cartera, Contabilidad,
  Tesorería, Tarjetas) se migran en fase 2 y quedan en la allowlist de la prueba de arquitectura.

## R10. Migración de datos `ReconciliarBanderasDerivadasDePersona`

- **Decision**: migración EF en par (PostgreSQL y SQL Server) con SQL a mano: tres `UPDATE`
  por bandera (encender donde hay fila hija viva y la bandera está apagada; apagar donde no la
  hay y está encendida), sin `DELETE`, idempotente; `Down` no-op documentado (no hay estado
  anterior que valga la pena restaurar). Antes de aplicarla en producción se corre
  `diagnostico-banderas.sql` (sólo lectura). Corrida el 2026-09-13 en PDN: 0 desalineadas.
- **Rationale**: FR-012; `PrincipioXII_MigracionesDestructivas` prohíbe `DELETE` sin marcador;
  la prueba de arquitectura ya vigila que la migración no toque DDL.

## R11. Compatibilidad de clientes existentes

- **Decision**: `POST /api/core/people` y `PUT /{id}` conservan ruta, verbo y forma del JSON
  (banderas derivadas ignoradas); `POST /api/payroll/employees` y `/api/core/associates` no
  cambian; los nuevos son rutas **adicionales** (`/with-person`, `/restore`,
  `/permissions/mine`). `PersonSearchDto` **suma** campos (`IsSalesperson`, `IsCustomer`,
  `IsSupplier`, `IsAdvisor`, `IsThirdParty`, `ReceivesInvoice`); `EmployeeDetailDto` suma
  `PersonPublicId`. Nada se retira.
- **Rationale**: MAUI y el Web se despliegan juntos pero los e2e y cualquier cliente viejo deben
  seguir pasando; sumar es compatible, quitar no.
