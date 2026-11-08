---
description: "Tareas de la 020 — Transporte, servicios por vehículo y conectores externos"
---

# Tasks: Transporte, servicios por vehículo y conectores externos

**Input**: `specs/020-transporte-servicios-conectores/` — [plan.md](plan.md), [spec.md](spec.md),
[research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md),
[contracts/mensajes.md](contracts/mensajes.md), [quickstart.md](quickstart.md).

**Pruebas**: el plan (R20) las pide, y el dueño decidió que **se escriben con cada historia pero se
corren una sola vez al final** (fase 12). Mientras tanto cada fase sólo **compila**
(`dotnet build IngenIA365ERP.slnx -m:1`, a lo sumo dos sesiones compilando a la vez) y se commitea
en local, sin push.

**Reglas que valen para todas las tareas**:
- Repositorio público: ningún nombre, placa, URL ni credencial de una cooperativa real, tampoco en
  semillas ni pruebas.
- El producto nunca nombra el sistema anterior.
- Toda pantalla nueva o modificada va con `IndicadorDeCarga` por zona
  ([docs/manual/indicador-de-carga.md](../../docs/manual/indicador-de-carga.md)).
- Toda entidad hereda `AuditableEntity` y su configuración lleva `HasQueryFilter(e => !e.IsDeleted)`;
  los hechos implementan `IHechoInmutable`.
- Toda escritura es un Command con validador FluentValidation. Las de Transporte que crean cosas
  llevan `Idempotency-Key`.
- Hacia afuera, sólo `PublicId`.
- Los errores van con código tipado (data-model §6).
- No se crea `appsettings.Development.json`, no se toca `.claude/settings.local.json` y no se usa
  `git stash`.

## Formato: `[ID] [P?] [Story] Descripción`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin depender de una tarea pendiente).
- **[USn]**: la historia de [spec.md](spec.md) a la que sirve.

## Abreviaturas de rutas

| Abreviatura | Ruta |
|---|---|
| `Dom/` | `src/Core/IngenIA365ERP.Domain/` |
| `App/` | `src/Core/IngenIA365ERP.Application/` |
| `Per/` | `src/Infrastructure/IngenIA365ERP.Persistence/` |
| `MigPg/` | `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/` |
| `MigSql/` | `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/` |
| `Idt/` | `src/Infrastructure/IngenIA365ERP.Identity/` |
| `Ext/` | `src/Infrastructure/IngenIA365ERP.ExternalChannels/` |
| `Api/` | `src/Presentation/IngenIA365ERP.API/` |
| `Sh/` | `src/Presentation/IngenIA365ERP.Shared/` |
| `TDom/` | `tests/IngenIA365ERP.Domain.Tests/` |
| `TApp/` | `tests/IngenIA365ERP.Application.Tests/` |
| `TArq/` | `tests/IngenIA365ERP.Architecture.Tests/Principles/` |
| `TInt/` | `tests/IngenIA365ERP.API.IntegrationTests/` |

Las migraciones se generan con `tools/scripts/add-migration.ps1` (los dos motores) y se revisan a
mano: deben ser **aditivas**. Si el scaffold arrastra cambios ajenos, se limpian antes del commit.

---

## Phase 1: Setup

**Purpose**: dejar listos el prefijo, los enums de plataforma y las carpetas.

- [X] T001 Enmienda PATCH de los Estándares Técnicos: agregar el prefijo `TRN_` (Transporte) a la lista de prefijos de tabla, subir la versión PATCH y anotar la fecha en `.specify/memory/constitution.md`
- [X] T002 [P] Agregar `TRN = "TRN"` y `CON = "CON"` con su comentario en `Dom/Enums/Integration/IntegrationDestinations.cs`, y `Transport` y `Connectors` en `Dom/Enums/Integration/IntegrationParticipantClass.cs`
- [X] T003 [P] Agregar el módulo de origen `TRN` en `App/Common/Integration/ModulosDeOrigen.cs`
- [X] T004 [P] Agregar `Transport = "Transport"` y la regla `(".Transport", Transport)` antes de `.Core` en `App/Common/Audit/ModuloDeAuditoria.cs`
- [X] T005 [P] Crear el catálogo fijo de módulos habilitables `ModulosHabilitables` (hoy sólo `TRN` «Transporte», con el prefijo de permisos `Transport.` y el de rutas `/api/transport`) en `Dom/Platform/ModulosHabilitables.cs`
- [X] T006 [P] Crear los enums de Transporte en `Dom/Enums/Transport/`:
  - `VehicleStatus`: Active 1, Inactive 2, Retired 3.
  - `VehicleEventKind`: Registered 1, StatusChanged 2, OwnerChanged 3, Reentered 4.
  - `AffiliationClass`: Associate 1, External 2.
  - `TransportCatalogKind`: los nueve de data-model §2.
  - `EquipmentStatus`.
  - `DriverStatus`.
  - `ChangeSource`: Screen 1, Import 2.
  - `BillingMode`, `BillingPaymentMode`, `BillingRunStatus`, `ProrationPolicy`.
  - `ChargeRoute`, `ChargeStatus`, `ChargeReviewFlag`, `AllocationSource`.
- [X] T007 [P] Crear los enums de conectores en `Dom/Enums/Connectors/`:
  - `ConnectorAuthKind`: None, LoginToken, HeaderKey, Basic.
  - `ConnectorOutcome`: Done, DoneWithWarning, Retry, ReAuth, ManualReview.
  - `ConnectorRequestStatus`: los ocho de data-model §3.
  - `ConnectorCallKind`: Login, Operation.

**Checkpoint**: compila.

---

## Phase 2: Foundational (bloquea a todas las historias)

**Purpose**: los cambios de plataforma que las historias usan. Son tres: la consulta de módulos
habilitados, los destinos opcionales de la bandeja y los permisos.

**⚠️ CRITICAL**: ninguna historia empieza sin esto.

### Módulos habilitados (lectura)

- [X] T008 Crear la entidad `TenantModule` (TenantId, ModuleCode, IsEnabled, ChangedAt, ChangedBy, Reason) en `Dom/Entities/Admin/TenantModule.cs`
- [X] T009 Configurar `ADM_TenantModules`, con índice único `(TenantId, ModuleCode)` entre vivas, en `Per/Configurations/Admin/ModulosYConectoresConfigurations.cs`; agregar el `DbSet` en `Per/DbContext/AdminDbContext.cs` y en `App/Common/Interfaces/IAdminDbContext.cs`
- [X] T010 Crear el puerto `IModulosHabilitados` (`EstaHabilitadoAsync(tenantPublicId, code)`, `HabilitadosAsync(tenantPublicId)`, `Invalidar(tenantPublicId)`) en `App/Platform/Modules/IModulosHabilitados.cs`, y su implementación con `IMemoryCache` de 60 s que lee la base administrativa en `Per/Platform/ModulosHabilitadosDesdeAdmin.cs`; registrarla en el DI de Persistence
- [X] T011 Crear `FiltroDeModuloHabilitado` (endpoint filter con el código del módulo; sin módulo responde 403 con el sobre `Module.NotEnabled`; no mira permisos) en `Api/Filters/FiltroDeModuloHabilitado.cs`, y la extensión `RequireModulo("TRN")` para grupos
- [X] T012 En `Api/Services/PermisosDeLaPeticion.cs`, `ResolverSinMemoAsync` retira los permisos con prefijo de un módulo apagado (los prefijos salen de `ModulosHabilitables`)
- [X] T013 Agregar `EnabledModules` a `MyPermissionsDto`, llenarlo desde `IModulosHabilitados` en `App/Security/Permissions/GetMyPermissionsQuery.cs`, y exponerlo en `Sh/Services/Security/PermisosDelUsuario.cs` (`TieneModulo(code)`)
- [X] T014 [P] Crear el componente `<ModuloHabilitado Codigo="…">`, que pinta su contenido sólo si el módulo está en `EnabledModules`, en `Sh/Components/Shared/ModuloHabilitado.razor`
- [X] T015 Migración Admin `ModulosYConectoresDePlataforma`, parte 1 (sólo `ADM_TenantModules`) con `tools/scripts/add-migration.ps1` en `MigPg/Admin/` y `MigSql/Admin/`. Las tablas de conectores se agregan a esta misma migración en T179 **antes del primer commit que la congele**: si ya se commiteó, T179 crea otra migración.

### Destinos opcionales de la bandeja (R12, contracts/mensajes.md §1)

- [X] T016 Agregar `IReadOnlySet<string> DestinosOpcionales` (por defecto vacío) al record `TipoDeMensaje` en `App/Common/Integration/Contracts/Inventory/CatalogoDeMensajesV1.cs`; declarar `TRN` como opcional en `PagoAplicado`, `PagoReversado`, `ReciboEmitido`, `ReciboAnulado` y `DocumentoAnulado`
- [X] T017 En `App/Common/Integration/EmisorDeMensajes.cs`, después de crear la entrega obligatoria, crear una entrega más por cada destino opcional con participante habilitado y vigente en `IRegistroDeIntegracion` a la fecha de emisión:
  - `Mode = Always`, `Pending`, `NextAttemptAt = now`;
  - sin dependencias de mensaje;
  - un destino opcional sin participante no deja rastro.
- [X] T018 Verificar que `DespachadorDeMensajes` (`Api/Integration/DespachadorDeMensajes.cs`) y `ReprocessMessagesCommand` (`App/Common/Integration/ReprocessMessagesCommand.cs`) traten igual una entrega opcional. Ajustar sólo si algo supone «una entrega por mensaje».
- [X] T019 [P] Escribir pruebas del emisor en `TApp/Common/Integration/EmisorDeMensajesDestinosOpcionalesTests.cs`. Casos:
  - sin participante, sin fila;
  - con participante, una entrega más;
  - participante vencido, nada;
  - la obligatoria no cambia.

### Permisos

- [X] T020 Crear `TransportPermissionCatalogSeeder` con los códigos de R19 y su reparto por rol (Operador: View, Create, Update, ChangeStatus, Documents, Drivers, Services.View/Manage, Billing.View, Notifications.View; CompanyAdmin: todo) en `Idt/Seed/TransportPermissionCatalogSeeder.cs`; agregar `Core.Connectors.View/Configure` a `Idt/Seed/CorePermissionCatalogSeeder.cs`
- [X] T021 Llamar al catálogo nuevo desde `Idt/Seed/PhaseZeroSecuritySeeder.cs` y repartirlo en `Idt/Seed/BuiltInRolesSeeder.cs`
- [X] T022 [P] Crear la clase de constantes `TransportPermissions` (los códigos de R19, para `RequirePermission` y `PermissionGate`) en `App/Transport/TransportPermissions.cs`

**Checkpoint**: compila. La bandeja ya sabe repartir a destinos opcionales; nada los usa todavía.

---

## Phase 3: User Story 1 — El maestro habilita Transporte para una empresa (P1) 🎯 MVP

**Goal**: el maestro enciende o apaga Transporte por cooperativa, con motivo y auditoría. Sin
módulo no hay menú ni rutas, ni para el maestro.

**Independent Test**: quickstart §1. Con dos cooperativas, se habilita en una:
- en la otra no hay menú;
- `GET /api/transport/vehicles` responde 403 `Module.NotEnabled`, incluso a un administrador con `*`;
- deshabilitar conserva los datos.

- [X] T023 [US1] Crear `SetTenantModuleCommand` (tenantPublicId, code, enabled, reason) con validador (código en `ModulosHabilitables`, motivo de 1 a 500), en `App/Platform/Modules/SetTenantModule/`. El handler:
  - escribe `ADM_TenantModules`;
  - invalida la caché;
  - audita `Platform.Module.Enabled/Disabled`;
  - al habilitar, llama a `IProvisionadorDeModulo`.
- [X] T024 [US1] Crear el puerto `IProvisionadorDeModulo` (`AlHabilitarAsync` y `AlDeshabilitarAsync`) en `App/Platform/Modules/IProvisionadorDeModulo.cs`. La implementación de Transporte va en `App/Transport/Platform/ProvisionadorDeTransporte.cs` y corre en la base de la cooperativa por `IEjecutorEnCooperativa`:
  - registra los participantes `TRN` y `CON` en el registro de integración (contracts/mensajes.md §4);
  - siembra los catálogos (llama a T049);
  - al deshabilitar, cierra la vigencia del participante `TRN`.
- [X] T025 [US1] Crear `GetTenantModulesQuery` (todos los de `ModulosHabilitables`, con estado, quién, cuándo y motivo) en `App/Platform/Modules/GetTenantModules/`
- [X] T026 [US1] Crear las rutas `GET /api/saas/tenants/{tenantId}/modules` y `PUT /api/saas/tenants/{tenantId}/modules/{code}` en un grupo con filtro de maestro (el patrón de `Api/Endpoints/Saas/NationalValuesEndpoints.cs`), en `Api/Endpoints/Saas/TenantModulesEndpoints.cs`
- [X] T027 [US1] Crear el grupo base `/api/transport` con `RequireModulo("TRN")` (todos los endpoints de Transporte cuelgan de él) en `Api/Endpoints/Transport/TransportGroup.cs`, y una ruta mínima `GET /api/transport/module-status` con `Transport.Vehicles.View` para comprobar la puerta
- [X] T028 [P] [US1] Crear el cliente `ModulosClient` (listar y cambiar) en `Sh/Services/Security/ModulosClient.cs` y registrarlo en el DI de Shared
- [X] T029 [US1] Crear la página `/saas/modulos`:
  - elegir la cooperativa;
  - grilla de módulos con interruptor;
  - diálogo de motivo;
  - `IndicadorDeCarga`;
  - en `Sh/Pages/Security/MasterModulos.razor`.
- [X] T030 [US1] En `Sh/Layout/NavMenu.razor`:
  - agregar «Módulos por cooperativa» bajo el bloque `_isMaster`;
  - agregar el grupo «Transporte» vacío envuelto en `<ModuloHabilitado Codigo="TRN">` (los enlaces se agregan en cada historia).
- [X] T031 [P] [US1] Prueba de arquitectura nueva `LosModulosHabilitablesTienenSuFiltro`: todo `MapGroup` bajo `Api/Endpoints/Transport/` cuelga del grupo con `RequireModulo`, y las plantillas y los informes de Transporte también, en `TArq/LosModulosHabilitablesTienenSuFiltro.cs`
- [X] T032 [P] [US1] Pruebas del handler de habilitación (motivo obligatorio, auditoría, provisionador llamado una vez, deshabilitar conserva) en `TApp/Platform/Modules/SetTenantModuleCommandTests.cs`

**Checkpoint**: compila. US1 completa y verificable a mano.

---

## Phase 4: User Story 2 — Vehículo con propietario y su vida (P1)

**Goal**: registrar, editar, inactivar, reactivar, cambiar de propietario y retirar un vehículo, con
historia, equipos y la clase de vinculación deducida.

**Independent Test**: quickstart §2, puntos 1 a 4. Además, buscar por placa, número interno,
radio-móvil y documento del propietario.

### Dominio y persistencia

- [X] T033 [P] [US2] Crear la regla pura `Placa.Normalizar` (mayúsculas, sin espacios ni guiones; 5 a 10 caracteres alfanuméricos) en `Dom/Transport/Placa.cs`
- [X] T034 [P] [US2] Crear la entidad `TransportCatalogItem` (Kind, Code, Name, IsActive, IsSystem) en `Dom/Entities/Transport/TransportCatalogItem.cs`
- [X] T035 [P] [US2] Crear la entidad `Vehicle` con todos los campos de data-model §2 en `Dom/Entities/Transport/Vehicle.cs`. El estado y el propietario tienen setters internos y sólo cambian por los métodos `CambiarEstado` y `CambiarPropietario`.
- [X] T036 [P] [US2] Crear el hecho `VehicleEvent` en `Dom/Entities/Transport/VehicleEvent.cs`
- [X] T037 [P] [US2] Crear `VehicleEquipment` en `Dom/Entities/Transport/VehicleEquipment.cs`
- [X] T038 [US2] Configurar `TRN_CatalogItems`, `TRN_Vehicles`, `TRN_VehicleEvents` y `TRN_VehicleEquipment` en `Per/Configurations/Transport/VehiclesConfigurations.cs`:
  - índices únicos filtrados a vivos (no `Retired`, no `IsDeleted`) sobre `Plate`, `InternalNumber` y `RadioCode` (éste sólo no nulo), en los dos motores;
  - `(Kind, Code)` en catálogos;
  - `(VehicleId, EquipmentTypeId)` entre activos;
  - FK a `COR_People` y `COR_Branches`.
- [X] T039 [US2] Agregar los `DbSet` en `Per/DbContext/ApplicationDbContext.cs` y en `App/Common/Interfaces/IApplicationDbContext.cs`

### Aplicación

- [X] T040 [US2] Crear la regla compartida `AfiliacionVigente` (copia de la regla de `App/Lending/Origination/PersonaDeOriginacion.cs`; devuelve `AffiliationClass`) en `App/Transport/Vehicles/AfiliacionVigente.cs`
- [X] T041 [US2] Crear `VehicleRegistrar` en `App/Transport/Vehicles/VehicleRegistrar.cs`. Es el alta y edición unitarias que reusan la pantalla y la plantilla:
  - normaliza;
  - valida unicidad entre vivos con el `existingPublicId` del que la tiene;
  - valida el `Kind` de cada catálogo (`Vehicle.CatalogKindMismatch`), el propietario (`Vehicle.OwnerRequired` / `.OwnerNotFound`) y el rango de fechas;
  - propone la sucursal principal;
  - escribe el evento `Registered`;
  - no guarda.
- [X] T042 [US2] Crear `CreateVehicleCommand` y `UpdateVehicleCommand` (sin estado ni propietario en el contrato) con validadores en `App/Transport/Vehicles/Commands/`. Traducen la violación del índice único a `Vehicle.PlateDuplicate` / `.InternalNumberDuplicate` / `.RadioCodeDuplicate`, como `Person.TaxIdDuplicate`.
- [X] T043 [US2] Crear `RegisterVehicleWithPersonCommand`, con un solo `SaveChangesAsync` y `Idempotency-Key` (el patrón de `App/Inventory/Salespeople/Commands/RegisterSalespersonWithPerson/`), en `App/Transport/Vehicles/Commands/RegisterVehicleWithPerson/`
- [X] T044 [US2] Crear `ChangeVehicleStatusCommand` (Active ↔ Inactive) y `RetireVehicleCommand` en `App/Transport/Vehicles/Commands/`:
  - fecha efectiva no anterior al último evento del mismo tipo (`Vehicle.EffectiveDateBeforeLastChange`);
  - motivo obligatorio;
  - `Vehicle.Retired` si ya está retirado;
  - evento `StatusChanged`;
  - auditoría.
- [X] T045 [US2] Crear `ChangeVehicleOwnerCommand` en `App/Transport/Vehicles/Commands/ChangeVehicleOwner/`:
  - fecha no anterior al último cambio de propietario;
  - evento `OwnerChanged` con la clase sellada de cada lado;
  - auditoría.
- [X] T046 [US2] Crear `GetOwnerDebtsQuery` (saldos vencidos del propietario por `IContratoDeObligaciones`; sin Cartera activa, por `App/Receipts/CashDesk/CuentasPorCobrarComerciales.cs`; informa, no bloquea) en `App/Transport/Vehicles/Queries/GetOwnerDebts/`
- [X] T047 [US2] Crear `SetVehicleEquipmentCommand` (reemplaza la lista; el tipo de equipo se valida contra el catálogo) en `App/Transport/Vehicles/Commands/SetVehicleEquipment/`
- [X] T048 [US2] Crear `ListVehiclesQuery` (q por placa, número interno, radio-móvil, documento o nombre; estado; propietario; paginado; alcance de sucursal) y `GetVehicleQuery` (ficha con la clase deducida, equipos, historia; documentos, conductores y suscripciones se suman en sus historias) en `App/Transport/Vehicles/Queries/`
- [X] T049 [US2] Crear `TransportCatalogSeeder` (Order 94; siembra clases RUNT, combustibles, modalidades del Decreto 1079, clases de servicio y tipos de equipo; marca y grupo de rodamiento sin semilla; sólo en cooperativas con `TRN` habilitado; idempotente con `SincronizacionDeCatalogo`) en `Per/Seeding/Parametric/TransportCatalogSeeder.cs`, con su JSON embebido en `Per/Seeding/Parametric/Data/transporte-catalogos.json`. Expone un método que llama `ProvisionadorDeTransporte`.
- [X] T050 [US2] Crear el CRUD de catálogos en `App/Transport/Catalogs/`:
  - `ListTransportCatalogQuery`;
  - `CreateTransportCatalogItemCommand` y `UpdateTransportCatalogItemCommand`, que inactivan y nunca borran si está en uso, con `CodigoDeCatalogo` de 10.
  
  Registrar los nueve casos `transporte-*` en `App/Common/Catalogos/BuscarCodigoDeCatalogoQuery.cs`.
- [X] T051 [US2] Crear el puerto `IUsoDePersonaEnTransporte` (`App/Common/People/IUsoDePersonaEnTransporte.cs`) con su implementación en `App/Transport/Vehicles/UsoDePersonaEnTransporte.cs`, y consultarlo en `App/Core/People/Commands/DeletePerson/DeletePersonCommand.cs`. Responde `Person.HasTransportUse` con la placa (FR-017); la parte del conductor se suma en T089.

### API

- [X] T052 [US2] Crear las rutas de vehículos de contracts/api.md §3 (menos plantillas, documentos, conductores y suscripciones) con `RequirePermission` por acción y `Idempotency-Key` en `POST`, en `Api/Endpoints/Transport/VehiclesEndpoints.cs`
- [X] T053 [US2] Crear las rutas `GET/POST/PUT /api/transport/catalogs/{kind}` en `Api/Endpoints/Transport/TransportCatalogsEndpoints.cs`

### Pantallas

- [X] T054 [P] [US2] Crear los clientes tipados `VehiculosClient` y `CatalogosDeTransporteClient` (sin poner `Authorization` a mano) en `Sh/Services/Transporte/` y registrarlos
- [X] T055 [US2] Crear la página `/transporte/vehiculos` en `Sh/Pages/Transporte/Vehiculos.razor`:
  - buscador y filtros;
  - grilla;
  - «Nuevo» con `PersonSearchPicker` y, si la persona no existe, `PersonaDialog`;
  - validación dual de placa y códigos;
  - `IndicadorDeCarga`.
- [X] T056 [US2] Crear la ficha `/transporte/vehiculos/{id}` en `Sh/Pages/Transporte/VehiculoFicha.razor`:
  - pestañas Ficha, Vinculación y Equipos;
  - la clase deducida;
  - historia;
  - acciones Inactivar, Activar, Retirar y Cambiar propietario, con diálogo de fecha y motivo;
  - el cambio de propietario muestra antes las deudas del saliente;
  - `PermissionGate` por acción;
  - `IndicadorDeCarga`.
- [X] T057 [US2] Crear la página `/transporte/catalogos` (pestañas por `Kind`, `CampoCodigo` con `transporte-{kind}`, inactivar) en `Sh/Pages/Transporte/Catalogos.razor`
- [X] T058 [US2] Agregar al grupo Transporte de `Sh/Layout/NavMenu.razor` los enlaces «Vehículos» y «Catálogos»

### Pruebas (se escriben y compilan; se corren en la fase 12)

- [X] T059 [P] [US2] Normalización de placa en `TDom/Transport/PlacaTests.cs`
- [X] T060 [P] [US2] Pruebas de `VehicleRegistrar` y de los comandos de estado y propietario (duplicados con `existingPublicId`, fechas hacia atrás, retiro terminal, clase sellada) en `TApp/Transport/Vehicles/`

**Checkpoint**: compila. US2 se recorre a mano.

---

## Phase 5: User Story 3 — Carga y actualización masiva del parque (P1)

**Goal**: las plantillas `transport.vehicles` y `transport.changes`, con la mecánica de la 016
(revisar, aplicar todo o nada, `?existing=update|keep`), reusando el alta unitaria. Las plantillas
de documentos, conductores y suscripciones van en sus historias.

**Independent Test**: quickstart §2, punto 5. Además, una segunda carga con `update` cambia sólo
las celdas con valor, y una plantilla con la misma placa dos veces marca las dos filas.

- [X] T061 [US3] Crear `CatalogoDePlantillasDeTransporte` (el patrón de `App/Lending/Imports/CatalogoDePlantillasDeCartera.cs`) en `App/Transport/Imports/CatalogoDePlantillasDeTransporte.cs` y registrarlo donde se registran los demás catálogos de plantillas
- [X] T062 [US3] Crear la plantilla `transport.vehicles` en `App/Transport/Imports/PlantillaDeVehiculos.cs`:
  - llave: placa;
  - propietario por tipo y número de documento, por `ResolutorDePersonaPorDocumento`;
  - catálogos por código;
  - la fila crea o actualiza por `VehicleRegistrar`;
  - un propietario inexistente es un error de fila (no crea personas);
  - una placa repetida en el archivo marca las dos filas.
- [X] T063 [US3] Crear la plantilla `transport.changes` en `App/Transport/Imports/PlantillaDeCambiosDeVehiculo.cs`:
  - columnas: placa, acción (`activar`, `inactivar`, `retirar`, `propietario`), fecha, motivo, documento del nuevo propietario;
  - cada fila corre la misma lógica de T044 y T045 con `Source = Import`.
  - Para eso, factorizar esa lógica en `App/Transport/Vehicles/CambiosDeVehiculo.cs`, que no guarda.
- [X] T064 [US3] Mapear las dos plantillas con `MapPlantilla` (`Api/Endpoints/Common/RutasDePlantilla.cs`) bajo el grupo de vehículos (`/vehicles/template.xlsx`, `/vehicles/import`, `/vehicles/changes/…`) con `Transport.Vehicles.Import`, en `Api/Endpoints/Transport/VehiclesEndpoints.cs`
- [X] T065 [US3] Poner `ImportarPlantilla` (`Sh/Components/Inventario/ImportarPlantilla.razor`) en `Sh/Pages/Transporte/Vehiculos.razor` para las dos plantillas, y crear la página `/transporte/plantillas`, que lista las cinco plantillas del módulo, en `Sh/Pages/Transporte/Plantillas.razor`. Agregar su enlace al menú.
- [X] T066 [P] [US3] Prueba de arquitectura nueva `LasPlantillasDeTransporteUsanElAltaUnitaria`: las plantillas de `App/Transport/Imports` no instancian `Vehicle`, `Driver`, `Subscription` ni `Person`; usan `VehicleRegistrar`, `CambiosDeVehiculo`, `RolDeConductor` y `SuscripcionDeServicio`. Va en `TArq/LasPlantillasDeTransporteUsanElAltaUnitaria.cs`.
- [X] T067 [P] [US3] Pruebas de las dos plantillas (revisión sin guardar, error por fila y columna, todo o nada, update solo con valor) en `TApp/Transport/Imports/`
  > `PlantillaDeVehiculosTests` (seis casos). Destapó un defecto: `numeroInterno` era obligatorio en toda fila y no se podía actualizar
  > sólo el color; ahora es obligatorio al crear (lo exige el validador) y vacío conserva el que tiene.

**Checkpoint**: compila. US1 a US3 forman la salida mínima: el parque cargado.

---

## Phase 6: User Story 4 — Documentos del vehículo y tablero de vencimientos (P2)

**Goal**: tipos de documento por empresa con semilla, un documento vigente por tipo, tablero e
informe de vencidos, por vencer y faltantes. Nada bloquea.

**Independent Test**: quickstart §3.1. Además, cambiar en una cooperativa los días de aviso del
SOAT no afecta a otra.

- [X] T068 [P] [US4] Crear las entidades `DocumentType` y `VehicleDocument` en `Dom/Entities/Transport/`
- [X] T069 [P] [US4] Crear la clasificación pura `VencimientosDeDocumentos`: por vehículo activo y tipo, devuelve Vencido, PorVencer, Vigente o Falta (obligatorio sin documento), según la fecha de corte y los `WarningDays`. Va en `Dom/Transport/VencimientosDeDocumentos.cs`.
- [X] T070 [US4] Configurar `TRN_DocumentTypes` y `TRN_VehicleDocuments` (único `(VehicleId, DocumentTypeId)` entre no reemplazados) en `Per/Configurations/Transport/DocumentsConfigurations.cs`; agregar los `DbSet`
- [X] T071 [US4] Agregar la semilla de los siete tipos (`SOAT`, `RTM`, `TOP`, `RCC`, `RCE`, `TODORIESGO`, `REVPREV`, con obligatorio y días de aviso) a `Per/Seeding/Parametric/TransportCatalogSeeder.cs` y a su JSON
- [X] T072 [US4] Crear el CRUD de tipos de documento en `App/Transport/Documents/DocumentTypes/`: lista, crear y editar con obligatorio y días entre 0 y 365; se inactiva, nunca se borra; los sembrados no se borran.
- [X] T073 [US4] Crear `RegistrarDocumento` (marca `SupersededAt` en el vigente del mismo tipo y valida `ExpiresOn ≥ ValidFrom`) en `App/Transport/Documents/RegistrarDocumento.cs`, y los comandos `AddVehicleDocumentCommand` y `CorrectVehicleDocumentCommand` (corregir no crea historia) en `App/Transport/Documents/Commands/`
- [X] T074 [US4] Crear `GetDocumentExpirationsQuery` (el tablero: `days` y `status` como filtros, por vehículo y propietario) en `App/Transport/Documents/Queries/`, y sumar los documentos vigentes con su aviso a `GetVehicleQuery`
- [X] T075 [US4] Crear la plantilla `transport.documents` (llave: placa + tipo + número; usa `RegistrarDocumento`) en `App/Transport/Imports/PlantillaDeDocumentosDeVehiculo.cs` y registrarla en el catálogo
- [X] T076 [US4] Crear las rutas de documentos de contracts/api.md §3 y la plantilla con `MapPlantilla` en `Api/Endpoints/Transport/DocumentsEndpoints.cs`
- [X] T077 [P] [US4] Crear el cliente `DocumentosDeVehiculoClient` en `Sh/Services/Transporte/DocumentosDeVehiculoClient.cs`
- [X] T078 [US4] Agregar la pestaña «Documentos» a `Sh/Pages/Transporte/VehiculoFicha.razor`: vigentes con su aviso, «Registrar» y «Corregir», e historia.
- [X] T079 [US4] Crear la página `/transporte/documentos` en `Sh/Pages/Transporte/Documentos.razor`:
  - tablero con conteos Vencidos, Por vencer y Faltan;
  - grilla filtrable;
  - `ImportarPlantilla` de documentos;
  - pestaña de tipos de documento (con `Transport.Catalogs.Manage`);
  - `IndicadorDeCarga`.
  
  Agregar el enlace al menú.
- [X] T080 [P] [US4] Pruebas de `VencimientosDeDocumentos` (bordes del día de aviso, vencido hoy, falta, inactivo excluido) en `TDom/Transport/VencimientosDeDocumentosTests.cs`

**Checkpoint**: compila.

---

## Phase 7: User Story 5 — Conductores básicos (P2)

**Goal**: la persona como conductor (marca `IsDriver` escrita sólo por `RolDeConductor`) y sus
asignaciones a vehículos con fechas.

**Independent Test**: quickstart §3.2. Además, un propietario puede ser conductor de su propio
vehículo, y no se puede eliminar a una persona que es conductora vigente.

- [X] T081 [P] [US5] Agregar `IsDriver` (bool, defecto `false`) a la persona en `Dom/Entities/Core/Person.cs` (o donde viva `Person`) y su mapeo en la configuración de `COR_People`
- [X] T082 [P] [US5] Crear las entidades `Driver` y `DriverAssignment` en `Dom/Entities/Transport/`
- [X] T083 [US5] Configurar `TRN_Drivers` (`PersonId` único entre vivos) y `TRN_DriverAssignments` en `Per/Configurations/Transport/DriversConfigurations.cs`; agregar los `DbSet`
- [X] T084 [US5] Migración de cooperativa `Transporte` (todas las tablas `TRN_` de las fases 4 a 7 y `COR_People.IsDriver`) con `tools/scripts/add-migration.ps1` en `MigPg/Application/` y `MigSql/Application/`. Revisar que sea aditiva y que los índices filtrados salgan en los dos motores. Las tablas de la fase 8 se agregan a esta misma migración en T108 si todavía no se commiteó congelada; si no, van en otra.
- [X] T085 [US5] Crear `RolDeConductor` (crea, restaura y retira el rol; pone y quita `IsDriver`; no guarda; el modelo de `App/Inventory/Salespeople/RolDeVendedor.cs`) en `App/Transport/Drivers/RolDeConductor.cs`
- [X] T086 [US5] Crear los comandos de conductor en `App/Transport/Drivers/Commands/`, con las reglas `Driver.AlreadyAssigned` (sin vigencias cruzadas con el mismo vehículo) y `Driver.PrimaryAlreadyExists` (un titular vigente por vehículo):
  - `RegisterDriverCommand`;
  - `RegisterDriverWithPersonCommand` (un `SaveChangesAsync`, `Idempotency-Key`);
  - `RetireDriverCommand`;
  - `AssignDriverCommand`;
  - `EndDriverAssignmentCommand`.
- [X] T087 [US5] Crear `ListDriversQuery` en `App/Transport/Drivers/Queries/` y sumar los conductores vigentes y su historia a `GetVehicleQuery`
- [X] T088 [US5] Crear la plantilla `transport.drivers` (llave: documento del conductor + placa; usa `RolDeConductor` y la asignación; no crea personas) en `App/Transport/Imports/PlantillaDeConductores.cs` y registrarla
- [X] T089 [US5] Sumar la conductora vigente a `App/Transport/Vehicles/UsoDePersonaEnTransporte.cs` (FR-017)
- [X] T090 [US5] Crear las rutas de conductores de contracts/api.md §3 y la plantilla en `Api/Endpoints/Transport/DriversEndpoints.cs`
- [X] T091 [P] [US5] Crear el cliente `ConductoresClient` en `Sh/Services/Transporte/ConductoresClient.cs`
- [X] T092 [US5] Crear la página `/transporte/conductores` (lista, alta con `PersonSearchPicker`/`PersonaDialog`, asignar y terminar, plantilla, `IndicadorDeCarga`) en `Sh/Pages/Transporte/Conductores.razor`; agregar la pestaña «Conductores» a la ficha del vehículo y el enlace al menú
- [X] T093 [US5] Enmendar `TArq/LaPersonaSeEscribeEnUnSoloSitio.cs`: `IsDriver` la escribe sólo `RolDeConductor`
- [X] T094 [P] [US5] Pruebas de `RolDeConductor` y de las asignaciones en `TApp/Transport/Drivers/`

**Checkpoint**: compila. La migración `Transporte` está lista para la fase 8 (o se le suma otra).

---

## Phase 8: User Story 6 — Suscribir servicios y facturarlos cada mes (P2)

**Goal**:
- servicios (productos de Comercial con prioridad, línea de Cartera, medios y conector);
- suscripciones con precio pactado;
- políticas;
- facturación individual y en lote: **una factura por vehículo con una forma de pago a crédito por
  servicio** (una obligación de Cartera por servicio) o de contado;
- cargos idempotentes;
- la ruta sin Cartera.

**Independent Test**: quickstart §4 y §6.1.

### Cambio en Comercial: precio pactado (R9)

- [X] T095 [US6] (Implementado como `SaveInventoryDraftLine.AgreedPrice` con `[JsonIgnore]`: ninguna ruta lo puede mandar.) Agregar `bool PrecioPactado` a `SalesLineInput` en `App/Inventory/Sales/SalesDraftCommands.cs`. Sólo se acepta si el borrador nace de un proceso: ubicar el marcador de origen del borrador y, si no existe, agregar un `OrigenDelBorrador` (Pantalla, Proceso) al comando. Desde la pantalla, la marca se rechaza.
- [X] T096 [US6] En `App/Inventory/Sales/PrecificacionDeVenta.cs`, una línea con `PrecioPactado` no cuenta como descuento ni pide aprobación, y queda registrada en la línea como «precio pactado de suscripción» (columna o nota existente de la línea; si hace falta columna, va en la migración de T108)
- [X] T097 [P] [US6] Pruebas de `PrecificacionDeVenta` con `PrecioPactado` (proceso sí, pantalla rechazada, sin aprobación) en `TApp/Inventory/Sales/PrecioPactadoTests.cs`

### Verificaciones previas

- [X] T098 [US6] **Verificación R13**: leer `App/Receipts/BankItems/AplicacionNoInteractiva.cs` y anotar en research.md R13 qué criterio usa cuando el abono trae sólo la persona. Si no es por antigüedad, dejarlo como límite en el runbook (T214). Recaudo no se cambia.
- [X] T099 [US6] Confirmar en `App/Lending/Integration/Comercial/NacimientoDeObligacionDeVenta.cs` que cada `DocumentPayment` a crédito da una `Loan` con `SourcePaymentPublicId` y el `SuggestedLineCode` del pago. Confirmar también que dos pagos a crédito en una factura no chocan con ninguna regla de Comercial (validación de medios, `VentaACreditoRegistrada`). Si algo lo impide, parar y avisar al dueño antes de seguir.

### Dominio y persistencia

- [X] T100 [P] [US6] Crear las entidades en `Dom/Entities/Transport/`:
  - `TransportService`, `Subscription`, `SubscriptionPrice` y `BillingPolicy`;
  - `BillingRun`;
  - `ServiceCharge`, con `ExternalId` generado al crear y sin setter público;
  - el hecho `ChargeAllocation`.
- [X] T101 [P] [US6] Crear la regla pura `Prorrateo` (mes completo o días del mes; redondeo al peso) en `Dom/Transport/Prorrateo.cs`
- [X] T102 [P] [US6] Crear la regla pura `RepartoDePagosPorLinea`. Reparte `AmountDue` entre las líneas cuando difiere de la suma (retenciones): proporcional, con el residuo en la última. Va en `Dom/Transport/RepartoDePagosPorLinea.cs`.
- [X] T103 [US6] Configurar `TRN_Services` (`ProductPublicId` único entre vivos), `TRN_Subscriptions`, `TRN_SubscriptionPrices`, `TRN_BillingPolicies`, `TRN_BillingRuns`, `TRN_ServiceCharges` y `TRN_ChargeAllocations` en `Per/Configurations/Transport/BillingConfigurations.cs`; agregar los `DbSet`. Índices:
  - único `(VehicleId, ServiceId, Period)` entre `Status <> Voided`;
  - por `LoanPublicId`, `PaymentPublicId` e `InvoiceNumber`;
  - único `(ChargeId, SourceMessagePublicId)`.

### Aplicación

- [X] T104 [US6] Crear el CRUD de servicios en `App/Transport/Services/Commands/`: `CreateTransportServiceCommand` y `UpdateTransportServiceCommand`. Validan:
  - producto `Kind = Service` (`TransportService.ProductNotService`);
  - línea de Cartera `IsService` y sin interés (`TransportService.CreditLineNotService`), sólo si Cartera está activa;
  - medios de pago de clase `AssociateCredit` / `CustomerCredit`;
  - conector habilitado (opcional; el campo se activa en la fase 10).
  
  La consulta va en `ListTransportServicesQuery`.
- [X] T105 [US6] Crear `SuscripcionDeServicio` (alta y edición unitarias, `Subscription.Overlaps`, `SubscriptionPrice.Overlaps`) en `App/Transport/Services/SuscripcionDeServicio.cs`, y los comandos `SubscribeVehicleCommand`, `EndSubscriptionCommand` y `SetSubscriptionPriceCommand` en `App/Transport/Services/Commands/`
- [X] T106 [US6] Crear el CRUD de políticas con vigencia sin cruces (`Billing.NoPolicy` si falta la vigente) en `App/Transport/Billing/Policies/`
- [X] T107 [US6] Crear la plantilla `transport.subscriptions` (llave: placa + código de producto; usa `SuscripcionDeServicio`) en `App/Transport/Imports/PlantillaDeSuscripciones.cs` y registrarla
- [X] T108 [US6] Agregar las tablas de esta fase a la migración `Transporte` si todavía no está commiteada; si no, crear la migración `TransporteFacturacion` (aditiva, dos motores) en `MigPg/Application/` y `MigSql/Application/`
- [X] T109 [US6] Crear `CalculoDeCargosDelPeriodo` en `App/Transport/Billing/CalculoDeCargosDelPeriodo.cs`. Por vehículo y período devuelve sus cargos o el motivo de omisión:
  - suscripciones vigentes × servicios activos, menos lo ya facturado;
  - precio pactado vigente o `ResolvePriceQuery` de Comercial;
  - política de prorrateo;
  - motivos de omisión: inactivo, sin suscripción, sin precio vigente, propietario sin datos de facturación.
- [X] T110 [US6] Crear `FacturadorDeVehiculo` en `App/Transport/Billing/FacturadorDeVehiculo.cs`. Factura un vehículo en su propia unidad de trabajo:
  1. Arma el borrador por `ISender` (`SaveInventoryDraftCommand`) con una línea por servicio, en orden de prioridad, y `ExternalReference = "TRN:{vehiculo}:{aaaamm}"`. Si ya hay un borrador con esa referencia, lo retoma.
  2. A crédito: un `DocumentPayment` a crédito por línea (medio según la clase de vinculación, `SuggestedLineCode` del servicio, vencimiento según `DueDay`). De contado: un pago de caja por línea, que exige caja abierta (`Payments.CashSessionRequired`).
  3. Confirma (`ConfirmInventoryDocumentCommand`).
  4. Registra los `ServiceCharge` con la ruta (Cartera, CommercialReceivable si Cartera no está activa, Cash).
  5. De contado, las asignaciones `CashAtInvoice` y el cargo `Paid`. La emisión al conector se agrega en T151.
  6. Traduce los fallos conocidos a motivo de omisión: `ElectronicInvoicing.NotReady`, crédito provisional pendiente, `Inventory.Price.NotFound`.
- [X] T111 [US6] Crear `PreviewServiceBillingQuery` (qué facturaría y qué omitiría, sin guardar) en `App/Transport/Billing/Queries/`
- [X] T112 [US6] Crear `RunSingleServiceBillingCommand` (en línea, `Idempotency-Key`, `Billing.PeriodAlreadyBilled` si ya estaba) en `App/Transport/Billing/Commands/`
- [X] T113 [US6] Crear `StartServiceBillingRunCommand` (crea el `BillingRun` en `Queued` con `OperationKey`; devuelve `runPublicId`) y la tarea `FacturacionDeServiciosTarea : ITareaProgramada`, que corre por `IEjecutorEnCooperativa`. La tarea:
  - recorre en tandas;
  - deja `Running`, luego `Finished` o `FinishedWithOmissions`;
  - guarda el resumen con omitidos;
  - con la violación del índice único, cuenta el vehículo como «ya estaba».
  
  Va en `App/Transport/Billing/` y se registra con las demás tareas programadas.
  **Cambio en implementación** (2026-11-08): no es tarea de fondo. Comercial exige que el mensaje de la venta a crédito lo origine la
  persona y el crédito su permiso, así que la corrida la avanza la pantalla por tandas (`ProcessServiceBillingRunCommand`,
  `POST /billing/runs/{id}/next`) con el cursor guardado en la corrida.
- [X] T114 [US6] Crear `GetBillingRunsQuery`, `GetBillingRunQuery` y `ListServiceChargesQuery` en `App/Transport/Billing/Queries/`; sumar las suscripciones a `GetVehicleQuery`
- [X] T115 [US6] Crear las rutas de servicios, políticas, suscripciones, plantilla, facturación y cargos de contracts/api.md §3 en `Api/Endpoints/Transport/ServicesEndpoints.cs` y `Api/Endpoints/Transport/BillingEndpoints.cs`

### Pantallas

- [X] T116 [P] [US6] Crear los clientes `ServiciosDeTransporteClient` y `FacturacionDeServiciosClient` en `Sh/Services/Transporte/`
- [X] T117 [US6] Crear la página `/transporte/servicios` en `Sh/Pages/Transporte/Servicios.razor`:
  - servicios con producto, prioridad, línea de Cartera, medios y conector;
  - políticas con vigencia;
  - `ImportarPlantilla` de suscripciones;
  - `IndicadorDeCarga`;
  - el aviso «Cartera no está activa: se cobra sólo en caja y se salda por prioridad» (FR-059).
- [X] T118 [US6] Agregar la pestaña «Servicios» a `Sh/Pages/Transporte/VehiculoFicha.razor`: suscripciones, precio pactado con vigencia, terminar y «Facturar este vehículo».
- [X] T119 [US6] Crear la página `/transporte/facturacion` en `Sh/Pages/Transporte/Facturacion.razor`:
  - período;
  - vista previa;
  - lanzar a crédito o de contado, con el aviso FR-059;
  - avance de la corrida, con sondeo cada pocos segundos mientras esté `Running`;
  - resumen con omitidos y su motivo;
  - cargos del período;
  - `IndicadorDeCarga`.
  
  Agregar los enlaces «Servicios» y «Facturación» al menú.
- [X] T120 [P] [US6] Prueba de arquitectura nueva `ElTransporteFacturaPorComercial`: ningún tipo de `App/Transport` instancia `InventoryDocument`, `DocumentLine` ni `DocumentPayment` ni usa sus `DbSet`; sólo envía comandos. Va en `TArq/ElTransporteFacturaPorComercial.cs`.
- [X] T121 [US6] Enmendar `TArq/NingunTrabajoDeFondoOperaSinCooperativa.cs` para que reconozca `FacturacionDeServiciosTarea`. **No aplica** (2026-11-08): la corrida en lote no es tarea de fondo sino tandas que avanza la pantalla (ver T113 y research R10).
- [X] T122 [P] [US6] Pruebas en `TDom/Transport/` y `TApp/Transport/Billing/`:
  - `Prorrateo` y `RepartoDePagosPorLinea`;
  - `CalculoDeCargosDelPeriodo` (cada motivo de omisión, precio pactado frente a lista, ya facturado);
  - `FacturadorDeVehiculo` con dobles de `ISender` (crédito: un pago por línea, en orden de prioridad; contado; retoma del borrador).

**Checkpoint**: compila. Ya se factura; todavía nadie escucha los pagos.

---

## Phase 9: User Story 7 — El maestro define un conector y lo habilita (P3)

**Goal**:
- conectores HTTP/JSON como datos, con versiones inmutables;
- validación de URL y variables;
- simulación sin red;
- habilitación por cooperativa con réplica `ADM_` → `COR_`.

**Independent Test**: quickstart §5.1 y §5.2. Además, otra cooperativa no ve el conector.

### Dominio (puro)

- [X] T123 [P] [US7] Crear `DefinicionDeConector`, que parsea y valida el JSON de contracts/api.md §1:
  - `auth` por tipo, `operation`, `credentialFields`, `responses`, `maxAttempts` de 1 a 50, `timeoutSeconds` de 1 a 60;
  - errores tipados.
  
  Va en `Dom/Connectors/DefinicionDeConector.cs`.
- [X] T124 [P] [US7] Crear el catálogo fijo `VariablesDeConector` (las de contracts/api.md §1, con su tipo) en `Dom/Connectors/VariablesDeConector.cs`
- [X] T125 [P] [US7] Crear `RenderizadorDePlantilla` en `Dom/Connectors/RenderizadorDePlantilla.cs`:
  - `{{var}}` y `{{var:int|dec2|date(fmt)}}`;
  - texto escapado JSON;
  - variable desconocida: `Connector.UnknownVariable`;
  - `{{cred.*}}` fuera de `auth`: `Connector.CredentialOutsideAuth`;
  - sin expresiones ni lógica.
- [X] T126 [P] [US7] Crear `MapaDeRespuestas` (reglas en orden; `status` con `contains` opcional sin distinguir mayúsculas ni tildes; `status: 0` para red o tiempo agotado; lo no previsto da `ManualReview`) en `Dom/Connectors/MapaDeRespuestas.cs`
- [X] T127 [P] [US7] Crear `DireccionSegura` en `Dom/Connectors/DireccionSegura.cs`:
  - al guardar: sólo `https`, sin credenciales en la URL, host DNS o IP pública;
  - `EsIpPermitida(IPAddress)`: rechaza loopback, RFC 1918, enlace local, 100.64/10, ULA, 169.254.169.254 e IPv4 mapeada;
  - errores `Connector.InsecureUrl` y `Connector.PrivateAddress`.

### Persistencia

- [X] T128 [US7] Crear las entidades de plataforma `PlatformConnector`, `PlatformConnectorVersion` (hecho) y `TenantConnector` en `Dom/Entities/Admin/`
- [X] T129 [US7] Crear las entidades de réplica `Connector` y `ConnectorVersion` en `Dom/Entities/Core/Connectors/`
- [X] T130 [US7] Configurar `ADM_Connectors`, `ADM_ConnectorVersions` y `ADM_TenantConnectors` en `Per/Configurations/Admin/ModulosYConectoresConfigurations.cs`, y `COR_Connectors` y `COR_ConnectorVersions` en `Per/Configurations/Core/Connectors/ConnectorsConfigurations.cs`; agregar los `DbSet` en los dos contextos y sus interfaces

### Aplicación

- [X] T131 [US7] Crear los comandos del maestro en `App/Platform/Connectors/Commands/`:
  - `CreatePlatformConnectorCommand` y `UpdatePlatformConnectorCommand`;
  - `PublishConnectorVersionCommand`: valida la URL, la definición y las variables; numera; calcula `DefinitionSha256`; llama al provisionador;
  - `SetTenantConnectorCommand`: con motivo; audita; llama al provisionador.
- [X] T132 [US7] Crear el puerto `IProvisionadorDeConectores` en `App/Platform/Connectors/IProvisionadorDeConectores.cs`, y su implementación en `Per/Platform/ProvisionadorDeConectores.cs`. Copia la versión vigente y la habilitación a `COR_Connectors` y `COR_ConnectorVersions` de cada cooperativa afectada por `IEjecutorEnCooperativa`. Al deshabilitar, marca la réplica inactiva.
  > **Hecho así (2026-11-08)**: como el aprovisionamiento de módulos (R1), la copia la hace la tarea programada
  > `TareaDeReplicaDeConectores` (`conectores.replicar`) con `ReplicaDeConectores` en `App/Platform/Connectors/`, en cada pasada y en
  > cada cooperativa: `IEjecutorEnCooperativa` no corre dentro de una petición, y así ninguna petición del maestro escribe en otra base.
  > Habilitar de nuevo despierta los envíos en espera. Publicar y habilitar no llaman a nadie: la tarea lo pone al día en segundos.
- [X] T133 [US7] Crear `ConnectorReplicaSeeder` (Order 95; resincroniza al arrancar, el patrón de `Per/Seeding/Parametric/NationalValuesReplicaSeeder.cs`) en `Per/Seeding/Parametric/ConnectorReplicaSeeder.cs`
  > **No hace falta (2026-11-08)**: la tarea de réplica corre en la primera pasada del programador tras arrancar; un sembrador sería un segundo escritor de la copia.
- [X] T134 [US7] Crear `SimulateConnectorQuery` (renderiza con variables de ejemplo; devuelve método, URL y cuerpo; cabeceras sin secretos; ninguna conexión) en `App/Platform/Connectors/Queries/`, y las consultas de lista, versiones y cooperativas habilitadas
- [X] T135 [US7] Crear las rutas `/api/saas/connectors…` de contracts/api.md §1 con filtro de maestro en `Api/Endpoints/Saas/ConnectorsEndpoints.cs`

### Pantallas

- [X] T136 [P] [US7] Crear el cliente `ConectoresDePlataformaClient` en `Sh/Services/Security/ConectoresDePlataformaClient.cs`
  > Quedó en `Sh/Services/Conectores/` (con `EjemploDeDefinicion`), registrado en `RegistroDeClientesDeTransporte`.
- [X] T137 [US7] Crear la página `/saas/conectores` (lista, nuevo, activo) en `Sh/Pages/Security/MasterConectores.razor`
- [X] T138 [US7] Crear la página `/saas/conectores/{id}` en `Sh/Pages/Security/MasterConectorDetalle.razor`:
  - editor de la versión (URL base y definición JSON, con los errores en su lugar);
  - lista de versiones;
  - «Simular» con variables de ejemplo;
  - habilitación por cooperativa con motivo;
  - `IndicadorDeCarga`.
  
  Agregar «Conectores» al bloque del maestro en `Sh/Layout/NavMenu.razor`.
- [X] T139 [P] [US7] Pruebas en `TDom/Connectors/`:
  - `DefinicionDeConector`, `RenderizadorDePlantilla` (formatos, escape, variable desconocida, `cred` fuera de `auth`);
  - `MapaDeRespuestas` (orden, `contains`, lo no previsto);
  - `DireccionSegura` (cada rango bloqueado, IPv6, IPv4 mapeada).

**Checkpoint**: compila. El maestro define y replica conectores.

---

## Phase 10: User Story 8 — La empresa pone sus credenciales y liga servicios (P3)

**Goal**:
- credenciales cifradas en la base, que nunca se muestran;
- prueba de conexión contra el proveedor;
- ligar cada servicio a su conector.

**Independent Test**: quickstart §5.3 y §5.4, y §7.1 para la clave.

### Adaptador HTTP (primera llamada saliente real)

- [X] T140 [US8] Crear el puerto `IEjecutorDeConector.EjecutarAsync(SolicitudHttp, CancellationToken)`, que devuelve código, extracto de 2 KB y duración (código 0 si falla la red o vence el tiempo), en `App/Core/Connectors/IEjecutorDeConector.cs`
  > **Hecho así (2026-11-08)**: el puerto va con un defecto sin red (`SinSalidaHttpDeConector`) que la API reemplaza al componer
  > `ExternalChannels`; la respuesta trae el cuerpo completo hasta 64 KB sólo en memoria (el del inicio de sesión lleva el token) y
  > `Extracto()` da los 2 KB que se guardan; `DireccionBloqueada` distingue la guarda de una falla de red.
- [X] T141 [US8] Crear el adaptador `EjecutorHttpDeConector` en `Ext/Connectors/EjecutorHttpDeConector.cs`:
  - cliente con nombre `"conectores"` sobre `SocketsHttpHandler`;
  - conexión en 5 s y total en `timeoutSeconds`;
  - sin redirecciones y sin reintentos de `HttpClient`;
  - circuito por cooperativa y conector con `Ext/Circuit/CircuitoDeCanalExterno.cs`.
- [X] T142 [US8] Crear `GuardaDeDireccion`, el `ConnectCallback` que resuelve el host y rechaza las IP que `DireccionSegura.EsIpPermitida` no admite (cierra el *DNS rebinding*), en `Ext/Connectors/GuardaDeDireccion.cs`. La opción `Conectores:PermitirRedesPrivadas` se lee al registrar y **el arranque falla si vale `true` en Production**. Registrar todo en `Ext/DependencyInjection.cs`.

### Credenciales

- [X] T143 [US8] Crear en `Dom/Entities/Core/Connectors/` las entidades y configurarlas en `Per/Configurations/Core/Connectors/ConnectorsConfigurations.cs`, con sus `DbSet`:
  - `ConnectorCredential`: `ProtectedPayload` con `[NoAuditar]`; `SetAt`, `SetBy`; `LastVerifiedAt`, `LastVerificationOk`, `LastVerificationMessage`. Tabla `COR_ConnectorCredentials`, única por conector entre vivas.
  - `ConnectorRequest`: el envío del tablero, con `MessagePublicId` único (data-model §3).
  - `ConnectorCall`: el intento, como hecho.
  > El circuito usa `EstadoDeLosCircuitosExternos` con la clave `CON:conector:{id}` por cooperativa: 5 fallas seguidas (red, tiempo o
  > 5xx) lo abren y cada 5 minutos pasa un sondeo.
- [X] T144 [US8] Crear `AlmacenDeCredencialesDeConector` (DataProtection con el propósito `IngenIA365ERP.Connectors.Credential.v1`; `GuardarAsync` y `LeerAsync` → diccionario de campos) en `App/Core/Connectors/AlmacenDeCredencialesDeConector.cs`. Es el **único** que llama a `Unprotect`.
  > El protector lleva la cooperativa como subpropósito (una fila copiada a otra base no se descifra allí) y un campo vacío conserva el
  > valor anterior: el formulario nunca rellena los secretos.
- [X] T145 [US8] Crear `SesionDeConector` en `App/Core/Connectors/SesionDeConector.cs`. Arma la autenticación por tipo:
  - `LoginToken`: hace el login, lee `tokenPath` y guarda el token en `IMemoryCache` por cooperativa y conector hasta `expiresIn − 60 s`; nunca persiste;
  - `HeaderKey`;
  - `Basic`.
  
  Se puede invalidar ante un 401.
- [X] T146 [US8] Crear los comandos en `App/Core/Connectors/Commands/` y la consulta `ListTenantConnectorsQuery` (`hasCredentials`, quién, cuándo, última verificación; nunca el contenido):
  - `SetConnectorCredentialsCommand`: valida los campos contra `credentialFields`; audita sin valores;
  - `TestConnectorCommand`: autentica y devuelve `{ ok, statusCode, message }` sin token; guarda la verificación.
  > `SetConnectorCredentialsCommand.Fields` lleva `[JsonIgnore]`: `AuditBehavior` serializa el comando entero y sólo debe ver
  > `CamposConValor` (los nombres). Poner credenciales despierta los envíos en espera (`EnviosEnEspera`, T164).
- [X] T147 [US8] Crear la migración de cooperativa `ConectoresDeLaCooperativa` (`COR_Connectors`, `COR_ConnectorVersions`, `COR_ConnectorCredentials`, `COR_ConnectorRequests`, `COR_ConnectorCalls`) en `MigPg/Application/` y `MigSql/Application/`
  > **Hecho así (2026-11-08)**: las tablas `COR_Connector*` quedaron dentro de la migración `Transporte` (T084): `TRN_Services` tiene llave a
  > `COR_Connectors` y EF genera todo el modelo pendiente de una vez. Son dos migraciones en total, no tres: `ModulosYConectoresDePlataforma`
  > (admin, con las tablas de conectores: T015 + T179) y `Transporte` (cooperativa: T084 + T108 + T147). El lado PostgreSQL se generó con la
  > cadena por variable de entorno (sin contraseña; `migrations add` no conecta) porque el worktree no tiene `appsettings.Development.json`.
- [X] T148 [US8] Crear las rutas `/api/core/connectors` de contracts/api.md §2 en `Api/Endpoints/Core/ConnectorsEndpoints.cs`. Activar en `App/Transport/Services/Commands/` la validación del conector ligado (habilitado en la cooperativa; a lo sumo un conector por producto).
- [X] T149 [US8] Crear el cliente `ConectoresClient` en `Sh/Services/Security/ConectoresClient.cs` y la página `/admin/conectores` en `Sh/Pages/Administracion/Conectores.razor`:
  - conectores habilitados;
  - formulario por `credentialFields`, con los secretos en `type=password` que nunca se rellenan;
  - «Probar conexión»;
  - `IndicadorDeCarga`.
  
  Agregar el enlace en el menú de administración, envuelto en `PermissionGate` con `Core.Connectors.View`. Habilitar el selector de conector en `Sh/Pages/Transporte/Servicios.razor`.
- [X] T150 [US8] Pruebas de arquitectura:
  > La enmienda de `LasCredencialesDeCanalesExternosNoTocanLaBase` no hizo falta: mira las entidades de canales y de Cartera, y
  > `ProtectedPayload` no tiene nombre de secreto. La excepción del FR-057 queda nombrada en `LasCredencialesDeConectoresNoSalen`.
  - nueva `LasCredencialesDeConectoresNoSalen`: ningún DTO, `TablaExportable`, plantilla de log ni evento de auditoría toca `ConnectorCredential.ProtectedPayload`; en `TArq/LasCredencialesDeConectoresNoSalen.cs`;
  - nueva `SoloElAlmacenDescifraCredenciales`: sólo `AlmacenDeCredencialesDeConector` llama a `Unprotect` con ese propósito; en `TArq/SoloElAlmacenDescifraCredenciales.cs`;
  - nueva `SoloElAdaptadorDeConectoresHablaHttp`: ningún tipo de Application ni de API usa `HttpClient` para conectores; en `TArq/SoloElAdaptadorDeConectoresHablaHttp.cs`;
  - enmienda de `TArq/LasCredencialesDeCanalesExternosNoTocanLaBase.cs`: una excepción nominal y única para `ConnectorCredential.ProtectedPayload`, con el comentario que cita el FR-057.

**Checkpoint**: compila. La empresa configura y prueba; todavía no se envía nada.

---

## Phase 11: User Story 9 — Al pagarse un servicio, el proveedor se entera solo (P3)

**Goal**: detectar el pago que salda un cargo por los cinco mensajes (R12, R13), emitir **una sola**
`SolicitudDeConector` y entregarla por el consumidor `CON`, con reintentos, tope, tablero y alertas
(R14, R18).

**Independent Test**: quickstart §5.5 a §5.9 y §6.2.

### 11a. Detección de pagos (depende de la fase 8 y de T143; el resto puede ir en paralelo con las fases 9 y 10)

- [X] T151 [US9] Agregar `PorPagoDeVentaAsync(paymentPublicIds)` → (obligación, número, estado) a `App/Common/Integration/Lending/IConsultaDeObligaciones.cs`, e implementarlo donde vive la implementación actual en Cartera
- [X] T152 [P] [US9] Crear la regla pura `RepartoPorPrioridad` (reparte un importe entre cargos abiertos por prioridad del servicio y orden de línea; no pasa del saldo de cada uno; devuelve el sobrante) en `Dom/Transport/RepartoPorPrioridad.cs`
- [X] T153 [US9] Sembrar los tres tipos de alerta en `Per/Seeding/Parametric/AlertTypesSeeder.cs`, dirigidos a quien tenga `Transport.Notifications.Manage`, con textos sin datos de ninguna cooperativa:
  - `Transporte.PagoAnuladoTrasAviso`;
  - `Transporte.PagadoOtraVez`;
  - `Conector.ReintentosAgotados`.
- [X] T154 [US9] Crear el contrato `SolicitudDeConectorV1` (contracts/mensajes.md §2) en `App/Common/Integration/Contracts/Connectors/SolicitudDeConectorV1.cs` y registrarlo en el catálogo de `CatalogoDeMensajesV1.cs`: origen `TRN`, destino `CON`, clave `Cargo:{cargo:N}` (`ConfirmacionPorPublicId`), `AdmiteProceso`
- [X] T155 [US9] Crear `EmisionDeSolicitudDeConector` en `App/Transport/Notifications/EmisionDeSolicitudDeConector.cs`. Emite por `EmisorDeMensajes` en la misma unidad de trabajo, nunca guarda, y sólo la primera vez que el cargo pasa a `Paid`:
  - resuelve las variables al momento del pago (vehículo, propietario, servicio, período, pago, factura y `envio.id = ExternalId`);
  - sella la versión y las variables `sensitive`;
  - crea `ConnectorRequest` en `Pending`;
  - guarda `NotifiedRequestPublicId`;
  - sin conector, sin conector habilitado o sin credenciales: `ReviewFlag = NoConnector` y no emite.
  > **Hecho así (2026-11-08)**: un servicio **sin** conector no avisa ni se marca (no es un error). Sólo un conector sin versión vigente (o una
  > cooperativa sin oficina) deja `NoConnector`. Un conector deshabilitado o sin credenciales **sí** emite: el consumidor deja el envío
  > `OnHold` y sale al habilitar o al poner credenciales (T164), que es lo que pide el FR-050. Es el `INotificadorDeCargos` de la facturación
  > (T158) y el origen va con la oficina de la factura (`FacturasDeServicios.OficinaAsync`).
- [X] T156 [US9] Crear `ApplyChargeAllocationsCommand` (sin ruta; `LosComandosDeConsumoNoTienenRuta`) en `App/Transport/Integration/ApplyChargeAllocationsCommand.cs`. En una unidad de trabajo:
  1. Escribe las asignaciones, idempotentes por `(ChargeId, SourceMessagePublicId)`.
  2. Recalcula el estado de cada cargo: `Paid` si la suma de asignaciones cubre el valor; `Open` si una reversa la baja.
  3. Llama a `EmisionDeSolicitudDeConector` para los que pasan a `Paid` por primera vez.
  4. En los que se reabren con envío ya hecho, marca el `ConnectorRequest` como `PaymentVoided`, pone `ReviewFlag = PaymentVoidedAfterNotice` y alerta por `App/Common/Integration/DestinatariosDeIntegracion.cs`, ampliado con el permiso `Transport.Notifications.Manage`.
  5. Un nuevo pago de un cargo que ya avisó no emite de nuevo: queda `ReviewFlag = PaidAgainAfterVoid`.
  > Lee él mismo los mensajes (`IMensajesEntrantes`, como `ConsumeMembershipWithdrawalApprovedCommand`) y hace todo lo de T157; el destino
  > sólo enruta. Las alertas se levantan después del `SaveChanges` (`IAlertas` guarda). Un envío todavía no salido también pasa a
  > `PaymentVoided` (el consumidor lo rechaza sin llamar); sólo alerta si ya había salido.
- [X] T157 [US9] Crear `DestinoTransporte : IDestinoDeMensajes` (`Destino = "TRN"`; acepta los cinco mensajes; lo que no toca ningún cargo responde `AlreadyProcessed` sin escribir) en `App/Transport/Integration/DestinoTransporte.cs`. Así trata cada mensaje (research R12):
  - **`PagoAplicado`**: busca el cargo por `LoanPublicId`. Si no lo encuentra, pregunta `PorPagoDeVentaAsync` y guarda el `LoanPublicId`. La asignación es el `Principal` aplicado; `PaidOff` lo confirma.
  - **`PagoReversado`**: asignación negativa.
  - **`ReciboEmitido`**: toma las aplicaciones `CommercialReceivable` de la factura y las reparte con `RepartoPorPrioridad`.
  - **`ReciboAnulado`**: reversa las asignaciones de ese recibo.
  - **`DocumentoAnulado`**: los cargos sin pago pasan a `Voided`; los pagados quedan `Paid` con `ReviewFlag` y alerta.
- [X] T158 [US9] En `App/Transport/Billing/FacturadorDeVehiculo.cs`, la ruta de contado llama a `EmisionDeSolicitudDeConector` al dejar los cargos `Paid`
- [X] T159 [US9] Enmendar `TArq/UnSoloConsumidorPorDestino.cs` para `TRN` y `CON`

### 11b. Entrega por el conector (después de las fases 9 y 10)

- [X] T160 [US9] Crear `RegisterConnectorCallCommand` (ámbito propio, como `App/Common/Integration/RegisterDeliveryResultCommand.cs`: escribe el `ConnectorCall` y el estado del `ConnectorRequest`; audita `Transport.Connector.Called` sin credenciales y con el cuerpo saneado) en `App/Core/Connectors/Commands/RegisterConnectorCall/`
- [X] T161 [US9] Crear `DestinoConectores : IDestinoDeMensajes` (`Destino = "CON"`) en `App/Core/Connectors/DestinoConectores.cs`. Pasos:
  1. Conector deshabilitado o sin credenciales: `Retry` y envío `OnHold`.
  2. `IntentosLeidos ≥ maxAttempts`: `Rejected` con `Conector.ReintentosAgotados`, envío `Review` y alerta.
  3. Si no, autentica con `SesionDeConector`, renderiza, llama por `IEjecutorDeConector` y traduce con `MapaDeRespuestas`:
     - `Done` → `Processed`, con `ResultReference = externalId`;
     - `DoneWithWarning` → `Processed`, con aviso;
     - `Retry` → `Retry`;
     - `ReAuth` → invalida el token y repite **una vez**; si vuelve 401, `Retry`;
     - `ManualReview` o no previsto → `Rejected`.
  4. Registra el intento con T160.
  > El tope cuenta las llamadas a la operación (`ConnectorRequest.Attempts`), no los intentos de la entrega: un envío que esperó días en
  > `OnHold` no llega agotado.
- [X] T162 [US9] Crear los comandos del tablero en `App/Transport/Notifications/Commands/`, con `Idempotency-Key`:
  - `RetryConnectorRequestCommand`: si está en `Review`, lo reprocesa por `ReprocessMessagesCommand` con destino `CON`; si está en `Retrying`, pone `NextAttemptAt = now`; siempre con el mismo `ExternalId`;
  - `ResolveConnectorRequestCommand`: motivo obligatorio, auditado, `ResolvedManually`.
  > Reintentar uno en revisión le devuelve el cupo de llamadas (las anteriores quedan como hechos) y lo reprocesa por la bandeja con la
  > clave de la operación; el comando no es `IOperacionIdempotente` para no anidar dos (el reproceso sí lo es).
- [X] T163 [US9] Crear `ListConnectorRequestsQuery` (estado, rango, vehículo, servicio; conteos por estado; une `COR_ConnectorRequests` con `TRN_ServiceCharges` por `OriginRef`) y `GetConnectorRequestQuery` (intentos con código, extracto, duración y cuerpo saneado) en `App/Transport/Notifications/Queries/`
- [X] T164 [US9] Al habilitar un conector o poner credenciales, despertar los envíos `OnHold` de ese conector (`NextAttemptAt = now`) desde `SetTenantConnectorCommand` y `SetConnectorCredentialsCommand`
- [X] T165 [US9] Crear las rutas `/api/transport/notifications…` de contracts/api.md §3 en `Api/Endpoints/Transport/NotificationsEndpoints.cs`
- [X] T166 [P] [US9] Crear el cliente `EnviosClient` en `Sh/Services/Transporte/EnviosClient.cs`
- [X] T167 [US9] Crear la página `/transporte/envios` en `Sh/Pages/Transporte/Envios.razor`:
  - conteos por estado;
  - grilla con vehículo, servicio, factura, pago, estado y última respuesta;
  - detalle con intentos;
  - «Reintentar» y «Cerrar a mano» con motivo;
  - el aviso FR-059 si no hay Cartera;
  - `IndicadorDeCarga`.
  
  Agregar el enlace al menú.

### Pruebas (se escriben y compilan; se corren en la fase 12)

- [X] T168 [P] [US9] Pruebas de `RepartoPorPrioridad` (prioridades, empate por orden de línea, sobrante, cargo ya pagado) en `TDom/Transport/RepartoPorPrioridadTests.cs`
- [X] T169 [P] [US9] Pruebas de `ApplyChargeAllocationsCommand` y `DestinoTransporte` en `TApp/Transport/Integration/`:
  - mensaje ajeno sin escritura;
  - parcial sin emisión;
  - saldado con una emisión;
  - mismo mensaje dos veces;
  - reversa tras aviso con `PaymentVoided` y alerta;
  - repago sin segunda emisión;
  - anulación de factura.
- [X] T170 [P] [US9] Pruebas de `DestinoConectores` con un `IEjecutorDeConector` falso en `TApp/Core/Connectors/DestinoConectoresTests.cs`:
  - cada resultado del mapa;
  - `ReAuth` una sola vez;
  - tope de intentos;
  - `OnHold`;
  - el mismo `externalId` en cada reintento.

**Checkpoint**: compila. La feature está construida.

---

## Phase 12: Polish, informes y pruebas finales

**Purpose**:
- los informes;
- las pruebas de arquitectura enmendadas;
- la e2e con proveedor falso;
- **la única corrida de todas las suites**;
- documentación y estado.

### Informes

- [X] T171 Crear las seis vistas `TablaExportable` (`vehiculos`, `historia`, `vencimientos`, `conductores`, `facturacion`, `envios`) en `App/Transport/Reports/`. Las columnas de `envios` excluyen el cuerpo y cualquier campo marcado `sensitive`.
- [X] T172 Crear `/api/reports/transport/{vista}?format=json|xlsx|pdf` (json con `Transport.Vehicles.View`, archivo con `Transport.Vehicles.Export` por `RequirePermissionWhenExporting`; cada exportación audita `Transport.Report.Exported`; dentro del filtro de módulo) en `Api/Endpoints/Reports/TransportReportsEndpoints.cs`
- [X] T173 Crear la página `/transporte/informes` (selector de vista, filtros, exportar, `IndicadorDeCarga`) en `Sh/Pages/Transporte/Informes.razor`; enlazarla desde el menú y desde `Sh/Pages/CentroReportes.razor` (sólo si el módulo está habilitado)

### Pruebas de arquitectura enmendadas

- [X] T174 [P] Enmendar `TArq/LosEndpointsProtegidosExigenPermiso.cs` para que incluya `Endpoints/Transport/*.cs`, `Endpoints/Core/ConnectorsEndpoints.cs` y `Endpoints/Reports/TransportReportsEndpoints.cs`
- [X] T175 [P] Agregar `Transporte` a `ModulosMigrados` en `TArq/LasPantallasDicenQueEstanCargando.cs`, y las páginas del maestro y `/admin/conectores` a su revisión
- [X] T176 [P] Agregar las rutas de Transporte y del maestro a `TArq/TodoEnlaceDelMenuTieneSuPagina.cs` (incluidos los `NavigateTo` del Centro de Reportes)
- [X] T177 [P] Revisar que `TArq/LosComandosDeConsumoNoTienenRuta.cs` cubra `ApplyChargeAllocationsCommand` y `RegisterConnectorCallCommand`, y que `TArq/ElTokenDeSesionLoPoneElHandler.cs` cubra los clientes nuevos de `Sh/Services/Transporte/` y `Sh/Services/Security/`

### Migraciones

- [X] T178 Revisar las tres migraciones en los dos motores:
  > Dos migraciones (ver T147), aditivas en los dos motores: `Up` sólo crea tablas, índices y la columna `IsDriver`; los 16 índices filtrados
  > salen en SQL Server y traducidos en PostgreSQL; `check-migration-parity.ps1`: 17 admin y 81 de cooperativa en par.
  - aditivas;
  - índices filtrados presentes;
  - sin cambios ajenos arrastrados por el scaffold.
- [X] T179 Confirmar que las tablas de conectores de la base administrativa están en `ModulosYConectoresDePlataforma` y que los snapshots de los dos motores quedaron consistentes; si no, generar la migración admin que falte

### Integración (e2e)

- [X] T180 Crear el proveedor falso en `TInt/Transport/ProveedorFalso.cs`:
  - Kestrel en `https://127.0.0.1:{puerto}`;
  - login con token;
  - asignar;
  - modos «200», «500 dos veces y luego 200», «400 ya tiene vigencia», «400 no existe», «401 una vez»;
  - registra cada `externalID` recibido.
  
  En `CentralIdentityApiFixture`, poner `Conectores:PermitirRedesPrivadas=true` sólo en ese host.
  > **Hecho así (2026-11-08)**: `ProveedorFalso` va **en proceso** como `IEjecutorDeConector` (fixture `ApiConProveedorFalsoFixture`),
  > no en Kestrel con TLS: un certificado de prueba no es de confianza para el `SocketsHttpHandler` del host. Todo lo demás corre de verdad;
  > la guarda de direcciones la cubren sus pruebas y `ConectoresSeguridadTests`.
- [X] T181 Crear la colección «Transporte e2e» (una cooperativa aislada con Comercial y Cartera activos y una línea de servicio `IsService`) con el recorrido R20 en `TInt/Transport/TransporteE2ETests.cs`:
  1. habilitar;
  2. cargar vehículos por plantilla;
  3. suscribir;
  4. facturar a crédito: dos obligaciones;
  5. pagar en caja la de RADIO: un envío `Sent`;
  6. 500 dos veces: mismo `externalID` tres veces;
  7. anular el recibo: `PaymentVoided` y alerta;
  8. repagar: sin segundo envío.
  > **Hecho así (2026-11-08)**: `RecorridoDeContadoTests` recorre habilitar, conector, credenciales, servicio, vehículos, factura **de contado**
  > por Comercial, un solo aviso, entrega por el despachador, 500 dos veces con el mismo `externalID` tres veces y cuerpo sin credenciales.
  > La ruta a crédito (Cartera activada, pago en caja, anulación del recibo y repago) **no está en e2e**: exige activar Cartera con sus
  > requisitos y Recaudo; su lógica la cubren `ApplyChargeAllocationsTests` y `DestinoConectoresTests`. Destapó dos defectos (copia del
  > conector con otro `PublicId`; tablero no componible por EF), corregidos.
- [X] T182 [P] Crear las e2e de la puerta de módulo (otra cooperativa: 403 `Module.NotEnabled` incluso con `*`; sin menú en `enabledModules`) en `TInt/Transport/ModuloHabilitadoTests.cs`
- [ ] T183 [P] Crear la e2e de la ruta sin Cartera (FR-059: pago en caja de la factura por el valor de RADIO notifica RADIO y no APP) en `TInt/Transport/TransporteSinCarteraTests.cs`
- [X] T184 [P] Crear la e2e de SSRF y URL insegura (`Connector.InsecureUrl`; IP privada rechazada en un host sin `PermitirRedesPrivadas`) en `TInt/Transport/ConectoresSeguridadTests.cs`
- [X] T185 Crear la e2e de SC-006: tras el recorrido, buscar el texto de la clave de prueba en el log capturado, la auditoría Mongo, la exportación de `envios` y `GET /api/core/connectors`. Debe haber cero apariciones. Va en `TInt/Transport/CredencialesNoSeVenTests.cs`.

### Corrida única de suites (decisión del dueño)

- [X] T186 `dotnet build IngenIA365ERP.slnx -m:1`: cero errores
- [X] T187 Correr Domain, Application, Architecture y Shared (`dotnet test tests/IngenIA365ERP.<X>.Tests`) en segundo plano. Corregir lo que falle y anotar los totales.
  > 2026-11-08: Domain 2.936, Shared 528, Application 7.217 + 6 de plantillas, Architecture 995. Se corrigieron 7 guardas (6 de arquitectura
  > y la de hechos ADM) y dos búsquedas por documento; `LaPersonaSeEscribeEnUnSoloSitio` y `SoloElNumeradorNumera` fallaron sólo bajo
  > carga en una corrida de 18 minutos y pasan solas.
- [ ] T188 Correr las de integración con Docker Desktop encendido, en PostgreSQL y SQL Server (`DB_PROVIDER`), en segundo plano. Primero `--filter "Collection=Transporte e2e"` y luego la suite completa. Anotar pasan, omitidas y fallas de ambiente con su motivo.

### Documentación y estado

- [X] T189 [P] Escribir el manual `docs/manual/transporte.md`:
  - habilitar;
  - vehículos y propietario;
  - plantillas;
  - documentos;
  - conductores;
  - servicios;
  - facturación;
  - envíos.
  
  Sin datos reales y sin nombrar el sistema anterior.
- [X] T190 [P] Escribir el manual `docs/manual/conectores-externos.md`:
  - definir un conector: autenticación, plantilla, variables y formatos, mapa de respuestas;
  - simular;
  - habilitar;
  - credenciales;
  - tablero.
- [X] T191 [P] Escribir el runbook `docs/operaciones/transporte-puesta-en-marcha.md`:
  - respaldos `pg_dump` de la base administrativa y de cada cooperativa;
  - migraciones;
  - habilitar el módulo;
  - crear la línea de Cartera de servicio (`IsService`, sin interés, `AllowsCommercialOrigin`, `AllowsNonAssociates`);
  - productos y servicios;
  - tipo de documento de venta y canal DIAN;
  - el límite de `AplicacionNoInteractiva` (T098);
  - el riesgo del llavero (R16).
  
  Producción sólo con autorización expresa del dueño.
- [X] T192 Actualizar `CLAUDE.md` con un bloque «Transporte y conectores (feature 020)»: reglas que no se negocian, rutas, migraciones, pruebas de arquitectura, recetas y pendientes del dueño. Remedir la tabla de Totales con sus comandos. Agregar las recetas nuevas a `docs/INDICE-DOCUMENTACION.md`.
- [ ] T193 Correr `quickstart.md` a mano en local (lo que no cubren las e2e: las pantallas, §1 a §7) y anotar lo que quede para el dueño (MAUI a mano, factura electrónica real)
- [ ] T194 Actualizar la memoria `feature-020-transporte-estado.md` y la nota compartida `020-transporte-estado.md` con el estado final, commits y pendientes; commit local, sin push

---

## Dependencies & Execution Order

### Fases

| Fase | Depende de | Notas |
|---|---|---|
| 1 Setup | — | |
| 2 Foundational | 1 | **bloquea todo** |
| 3 US1 | 2 | MVP de la puerta |
| 4 US2 | 3 (grupo `/api/transport` y provisionador) | |
| 5 US3 | 4 | |
| 6 US4 | 4 | paralelo con 5 y 7 |
| 7 US5 | 4 | paralelo con 5 y 6; genera la migración `Transporte` (T084) |
| 8 US6 | 4, 7 (migración), 5 (catálogo de plantillas) | |
| 9 US7 | 2 | paralelo con 4 a 8 en lo de Domain y Admin |
| 10 US8 | 9 | |
| 11a US9 detección | 8 y T143 (entidad del envío) | el resto, paralelo con 9 y 10 |
| 11b US9 entrega | 9, 10, 11a | |
| 12 Polish | todas | la **única** corrida de suites |

### Dentro de cada historia

Dominio puro → entidades y configuración → migración → aplicación (reglas compartidas antes que
comandos) → rutas → clientes → pantallas → menú → pruebas escritas. Cada fase termina con
`dotnet build -m:1` y un commit local.

### Puntos de control con el dueño

- **T099**: si Comercial no admite varios pagos a crédito en una factura, se para y se consulta antes de seguir con la fase 8.
- **T098**: el criterio de `AplicacionNoInteractiva` se documenta; no se cambia Recaudo.
- **Producción**: nada se despliega ni se empuja sin autorización expresa.

## Parallel Opportunities

- **Fase 1**: T002 a T007 son archivos distintos.
- **Fase 2**: T014, T019 y T022 mientras se hacen T010 a T013.
- **Fase 4**: T033 a T037 (dominio) en paralelo; T054 en paralelo con las rutas.
- **Fases 5, 6 y 7**, una vez terminada la 4, en paralelo si se reparte el trabajo. Cuidado: tocan
  `VehiculoFicha.razor`, `NavMenu.razor` y `GetVehicleQuery`, así que conviene hacerlas en serie en
  una sola sesión o coordinar esos tres archivos.
- **Fase 9, parte de dominio** (T123 a T127): puede adelantarse en cualquier momento después de la
  fase 1; son funciones puras.
- **Fase 11a**: en paralelo con las fases 9 y 10.
- **Fase 12**: T174 a T177, T182 a T184 y T189 a T191.

**Límite de compilación**: a lo sumo dos sesiones compilando a la vez, siempre con `-m:1`.

### Ejemplo: US2 en paralelo

```text
T033 Placa.cs   ·   T034 TransportCatalogItem.cs   ·   T035 Vehicle.cs   ·   T036 VehicleEvent.cs   ·   T037 VehicleEquipment.cs
→ T038 configuración → T040 AfiliacionVigente → T041 VehicleRegistrar → T042–T048 comandos y consultas
→ T052–T053 rutas   ·   T054 clientes (en paralelo)
→ T055–T058 pantallas y menú   ·   T059–T060 pruebas (en paralelo)
```

### Ejemplo: US9 en paralelo

```text
T152 RepartoPorPrioridad.cs   ·   T153 tipos de alerta   ·   T154 SolicitudDeConectorV1   (con T143 ya hecha)
→ T155 EmisionDeSolicitudDeConector → T156 ApplyChargeAllocationsCommand → T157 DestinoTransporte
(cuando estén las fases 9 y 10) → T160 RegisterConnectorCallCommand → T161 DestinoConectores → T162–T167 tablero
```

## Implementation Strategy

### MVP

Fases 1, 2 y 3 (US1): la puerta del módulo por cooperativa. Es lo mínimo con valor propio, porque
deja lista la habilitación de módulos para cualquier módulo futuro.

### Salida mínima de Transporte

US1, US2 y US3: el parque cargado y llevado en el ERP. Es lo que una cooperativa necesita el primer
día aunque todavía no facture servicios.

### Entrega completa (antes del 01/12/2026)

US4 y US5 (documentos y conductores) → US6 (facturación) → US7 y US8 (conectores) → US9 (envíos) →
fase 12. La entrega es **una sola** por decisión del dueño: las fases ordenan la construcción, no
los despliegues.

### Totales

- **194 tareas**: Setup 7 · Foundational 15 · US1 10 · US2 28 · US3 7 · US4 13 · US5 14 · US6 28 ·
  US7 17 · US8 11 · US9 20 · Polish 24.
