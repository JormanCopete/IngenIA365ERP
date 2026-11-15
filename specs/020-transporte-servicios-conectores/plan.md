# Implementation Plan: Transporte, servicios por vehículo y conectores externos

**Branch**: `020-transporte-servicios-conectores` | **Date**: 2026-11-08 | **Spec**: [spec.md](spec.md)

**Input**: la especificación de `specs/020-transporte-servicios-conectores/spec.md`, con las
decisiones del dueño del 2026-11-08: A–L, Q1, y las tres de planeación (FR-057, FR-058, FR-059).

## Summary

La feature agrega cuatro piezas, en este orden:

1. **Módulos por empresa.** El maestro habilita Transporte por cooperativa. Es la primera
   habilitación de módulos del ERP. Una tabla administrativa, un filtro en las rutas, el recorte de
   permisos y el menú cierran la puerta (R1).
2. **Transporte.** Vehículos con propietario (una persona; la clase asociado/externo se deduce),
   estados con historia, equipos, documentos con vencimiento y tablero, conductores básicos,
   catálogos por empresa y cinco plantillas masivas sobre la mecánica de la 016 (R3–R8).
3. **Servicios y facturación.**
   - Cada vehículo se suscribe a servicios, que son productos de Comercial con precio pactado
     opcional.
   - La facturación mensual hace **una factura por vehículo con una forma de pago por servicio**.
     Por la mecánica que ya tiene Comercial, eso da **una obligación de Cartera por servicio** sin
     tocar Cartera. Sin Cartera activa, la deuda queda como cuenta por cobrar comercial (R9–R11).
   - Transporte lleva su libro de cargos y asignaciones. Se entera de los pagos por **destinos
     opcionales** en la bandeja de integración, un cambio chico de plataforma (R12–R13).
4. **Conectores.**
   - El maestro define conectores HTTP/JSON como datos, versionados y replicados a cada cooperativa
     habilitada.
   - La cooperativa pone sus credenciales, cifradas en su base por decisión del dueño, y liga cada
     servicio.
   - Cuando un cargo queda pagado se emite **una sola** `SolicitudDeConector`. La entrega el
     consumidor `CON`: es el primer adaptador HTTP real del producto, con defensa contra SSRF,
     reintentos de la bandeja, tope de intentos, tablero y alertas (R14–R18).

Los documentos de apoyo son [research.md](research.md) (R1–R20 y riesgos),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md),
[contracts/mensajes.md](contracts/mensajes.md) y [quickstart.md](quickstart.md).

## Technical Context

**Language/Version**: C# / .NET 10.0.5; Blazor (Shared, Web, WASM, MAUI).

**Primary Dependencies**: las de siempre, sin paquetes nuevos: Carter, MediatR, FluentValidation,
EF Core (PostgreSQL y SQL Server), Syncfusion 33.2.8 (`Grid`, `Inputs`, `Buttons`, `Navigations`,
ya referenciados), DataProtection, ClosedXML, QuestPDF y `IHttpClientFactory` con
`SocketsHttpHandler`. No se agrega ningún componente de Syncfusion nuevo.

**Storage**:
- **Base administrativa**: 4 tablas `ADM_`.
- **Cada cooperativa**: 17 tablas `TRN_` y 5 `COR_`, más la columna `COR_People.IsDriver`.
- **Migraciones**: 3 pares, todas aditivas.

**Testing**: xUnit en Domain, Application, Architecture y Shared. Integración con Testcontainers y
un proveedor HTTP falso dentro de la fixture. Todo corre **una sola vez al final**.

**Target Platform**: la API en Linux sobre k3s (DEV, QA y PDN), y los clientes web y MAUI.

**Project Type**: web service + Blazor.

**Performance Goals** (de la spec):
- Facturar 1.000 vehículos en menos de 5 minutos, en segundo plano.
- Notificar el 99 % de los pagos en menos de 2 minutos: la bandeja despierta por señal o cada 5 s.
- Cargar 1.000 vehículos en menos de 10 minutos de pantalla.

**Constraints**:
- Entrega única antes del 01/12/2026.
- El repositorio es público: ningún dato de cooperativa.
- Compilar con `-m:1` y un máximo de dos sesiones compilando a la vez.
- Nunca dos asignaciones de crédito en el proveedor (identificador externo estable).
- Ninguna credencial visible.
- Producción sólo con autorización expresa, respaldo y diagnóstico.

**Scale/Scope**:
- ~70 rutas nuevas (≈ 10 del maestro, 3 de conectores de la cooperativa, ≈ 50 de Transporte y 6
  vistas de informe).
- 14 páginas:
  - `/saas/modulos`, `/saas/conectores`, `/saas/conectores/{id}`;
  - `/admin/conectores`;
  - `/transporte/vehiculos`, `/transporte/vehiculos/{id}`, `/transporte/conductores`,
    `/transporte/documentos`, `/transporte/catalogos`, `/transporte/servicios`,
    `/transporte/facturacion`, `/transporte/envios`, `/transporte/plantillas`, `/transporte/informes`.
- 5 plantillas, 2 consumidores de bandeja y 1 tarea programada (la facturación en lote).

## Constitution Check

*GATE: se revisa antes de la investigación y otra vez después del diseño.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec con 59 FR y sin aclaraciones abiertas; sigue `/speckit-tasks` |
| II   | Clean Architecture | PASS | reglas puras en Domain (`RepartoPorPrioridad`, `VencimientosDeDocumentos`, `RenderizadorDePlantilla`, `MapaDeRespuestas`, `DefinicionDeConector`); HTTP sólo en el adaptador de `ExternalChannels`, detrás del puerto `IEjecutorDeConector` |
| III  | CQRS + MediatR | PASS | cada escritura es un Command con validador; la facturación crea y confirma la factura por `ISender` hacia los comandos de Comercial, nunca escribiendo `INV_` directo; los consumidores escriben por comandos sin ruta (`LosComandosDeConsumoNoTienenRuta`) |
| IV   | Multi-tenancy | PASS | todo dato de negocio va en la base de la cooperativa; las definiciones del maestro se **replican** (R15) en vez de leer `ADM_` en una petición de cooperativa; el trabajo de fondo es el despachador, que ya recorre cooperativa por cooperativa; la tarea de facturación es `ITareaProgramada` por cooperativa |
| V    | Person centralizada | PASS | propietario y conductor son `COR_People`; `IsDriver` la escribe sólo `RolDeConductor`; la persona nueva sale de `PersonaDialog`; las plantillas no crean personas |
| VI   | PublicId | PASS | rutas y DTO con `PublicId`; los cargos guardan `PublicId` de factura, línea, pago y obligación |
| VII  | Soft-delete + auditoría | PASS | todo `AuditableEntity`; eventos, asignaciones, versiones e intentos son hechos inmutables; los catálogos se inactivan |
| VIII | Validación dual | PASS | validadores FluentValidation; las pantallas repiten las reglas (placa, códigos con `CampoCodigo`, fechas) |
| IX   | Errores visibles | PASS | los omitidos de la facturación tienen motivo; los rechazos del proveedor llegan al tablero con su texto; alertas por reintentos agotados y por pago anulado |
| X    | Trazabilidad | PASS | módulo de auditoría `Transport`; cada cambio de estado o propietario, corrida, envío y cierre manual se audita; las credenciales nunca se auditan (`[NoAuditar]`) |
| XI   | Inmutabilidad contable | PASS | Transporte no asienta nada: la contabilidad es de Comercial, Cartera y Recaudo, por sus contratos; anular es el flujo de Comercial y de Recaudo |
| XII  | Migraciones | PASS | 3 pares aditivos, sin datos; las semillas van por `IDataSeeder` (Order 94 catálogos, 95 réplica de conectores) |
| UI   | `IndicadorDeCarga` | PASS | `Transporte` entra en `ModulosMigrados`; la consola del maestro y `/admin/conectores` también llevan indicador; sin colores literales |

**Enmiendas menores**, sin cambiar principios:
- **Estándares Técnicos** (v2.0.x, PATCH): prefijo `TRN_` (Transporte).
- **Prueba de arquitectura `LasCredencialesDeCanalesExternosNoTocanLaBase`**: una excepción
  nominal, `ConnectorCredential.ProtectedPayload`, por decisión del dueño (FR-057). Va acompañada
  de dos pruebas que la acotan (R16).

**Re-check tras el diseño**: PASS. El único punto delicado es el Principio IV con las definiciones
del maestro, y se resuelve con la réplica.

## Project Structure

### Documentation (this feature)

```text
specs/020-transporte-servicios-conectores/
├── spec.md
├── plan.md              # este archivo
├── research.md          # R1–R20 y riesgos
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api.md
│   └── mensajes.md
├── checklists/requirements.md
└── tasks.md             # lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Entities/Admin/            TenantModule, PlatformConnector, PlatformConnectorVersion, TenantConnector
├── Entities/Transport/        Vehicle, VehicleEvent, VehicleEquipment, VehicleDocument, DocumentType,
│                              TransportCatalogItem, Driver, DriverAssignment, TransportService,
│                              Subscription, SubscriptionPrice, BillingPolicy, BillingRun,
│                              ServiceCharge, ChargeAllocation
├── Entities/Core/Connectors/  Connector, ConnectorVersion, ConnectorCredential, ConnectorRequest, ConnectorCall
├── Transport/                 Placa, VencimientosDeDocumentos, RepartoPorPrioridad, Prorrateo
└── Connectors/                DefinicionDeConector, RenderizadorDePlantilla, MapaDeRespuestas, DireccionSegura

src/Core/IngenIA365ERP.Application/
├── Platform/Modules/          IModulosHabilitados, SetTenantModuleCommand, IProvisionadorDeModulo
├── Platform/Connectors/       comandos del maestro, IProvisionadorDeConectores, simulación
├── Core/Connectors/           credenciales, prueba de conexión, DestinoConectores, RegisterConnectorCallCommand,
│                              IEjecutorDeConector (puerto), AlmacenDeCredencialesDeConector
├── Transport/Vehicles|Documents|Drivers|Catalogs|Services|Billing|Charges|Notifications|Imports|Reports
│   └── Integration/           DestinoTransporte, ApplyChargeAllocationsCommand
└── Common/Integration/        DestinosOpcionales en TipoDeMensaje y en EmisorDeMensajes; SolicitudDeConectorV1

src/Infrastructure/
├── IngenIA365ERP.Persistence/Configurations/{Admin,Transport,Core}/…
├── IngenIA365ERP.Persistence/Seeding/Parametric/TransportCatalogSeeder (94), ConnectorReplicaSeeder (95)
├── IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/{Admin,Application}/…
├── IngenIA365ERP.Identity/Seed/TransportPermissionCatalogSeeder
└── IngenIA365ERP.ExternalChannels/Connectors/   EjecutorHttpDeConector, GuardaDeDireccion (ConnectCallback)

src/Presentation/
├── IngenIA365ERP.API/Endpoints/Saas/{TenantModulesEndpoints,ConnectorsEndpoints}.cs
├── IngenIA365ERP.API/Endpoints/Core/ConnectorsEndpoints.cs
├── IngenIA365ERP.API/Endpoints/Transport/*.cs   (grupo con FiltroDeModuloHabilitado)
├── IngenIA365ERP.API/Endpoints/Reports/TransportReportsEndpoints.cs
├── IngenIA365ERP.API/Filters/FiltroDeModuloHabilitado.cs
└── IngenIA365ERP.Shared/
    ├── Pages/Transporte/*.razor, Pages/Security/MasterModulos.razor, MasterConectores*.razor,
    │   Pages/Administracion/Conectores.razor
    ├── Components/Shared/ModuloHabilitado.razor
    ├── Services/Transporte/*Client.cs, Services/Security/ConectoresClient.cs
    └── Layout/NavMenu.razor   (grupo Transporte y enlaces de la consola del maestro)

tests/
├── IngenIA365ERP.Domain.Tests/Transport|Connectors/
├── IngenIA365ERP.Application.Tests/Transport|Connectors|Platform/
├── IngenIA365ERP.Architecture.Tests/Principles/   (nuevas y enmendadas, ver abajo)
└── IngenIA365ERP.API.IntegrationTests/Transport/  (colección «Transporte e2e», ProveedorFalso)
```

**Structure Decision**: las capas y carpetas de siempre. Transporte es un módulo nuevo con su
prefijo. Los conectores son de plataforma y de Core, no de Transporte, para que mañana los use otro
módulo.

### Pruebas de arquitectura

**Nuevas**:
- `LosModulosHabilitablesTienenSuFiltro`: todo grupo de `Endpoints/Transport` lleva
  `FiltroDeModuloHabilitado`.
- `ElTransporteFacturaPorComercial`: ningún tipo de `Application.Transport` instancia
  `InventoryDocument` ni toca `INV_`; sólo envía comandos.
- `LasPlantillasDeTransporteUsanElAltaUnitaria`.
- `SoloElAdaptadorDeConectoresHablaHttp`.
- `LasCredencialesDeConectoresNoSalen`.
- `SoloElAlmacenDescifraCredenciales`.

**Enmendadas**:
- `LasCredencialesDeCanalesExternosNoTocanLaBase`: la excepción nominal.
- `LosEndpointsProtegidosExigenPermiso`: patrones de Transporte y conectores.
- `LasPantallasDicenQueEstanCargando`: `Transporte`.
- `TodoEnlaceDelMenuTieneSuPagina`: rutas de Transporte.
- `NingunTrabajoDeFondoOperaSinCooperativa`: la tarea de facturación.
- `UnSoloConsumidorPorDestino`: `TRN` y `CON`.

## Entregas y orden de construcción

Una sola entrega antes del 01/12/2026, construida por fases. Cada fase compila y se commitea; las
suites corren al final.

| Fase | Contenido | Historias |
|---|---|---|
| **T0 Plataforma** | `ADM_TenantModules`, filtro, recorte de permisos, `enabledModules`, menú, `/saas/modulos`; destinos opcionales en la bandeja | US1 |
| **T1 Vehículos** | catálogos y semilla, vehículos, historia, propietario y deudas, equipos, persona: uso en Transporte | US2 |
| **T2 Masivo y documentos** | tipos y documentos, vencimientos y tablero; conductores y `IsDriver`; 4 plantillas (vehículos, cambios, documentos, conductores); informes | US3, US4, US5 |
| **T3 Servicios y facturación** | servicios, suscripciones, precio pactado en Comercial, políticas, facturación individual y en lote (tarea), cargos, contado; plantilla de suscripciones | US6 |
| **T4 Detección de pagos** | `DestinoTransporte`, asignaciones, reparto por prioridad, reversas, anulación de factura | US9 (parte) |
| **T5 Conectores** | `ADM_` y réplica, consola del maestro con simulación, credenciales cifradas y prueba, adaptador HTTP con SSRF, `DestinoConectores`, tablero de envíos, alertas | US7, US8, US9 |
| **T6 Cierre** | suites completas, e2e con proveedor falso, documentación (manual, runbook de implantación y operación, CLAUDE.md), enmienda de la constitución | todas |

## Riesgos y dependencias del despliegue

1. **Comercial activo y con su tipo de documento de venta listo** en la cooperativa. Si es factura
   electrónica, con su canal DIAN. Hoy el canal real no está construido (T732) y Comercial no está
   activo en la cooperativa de referencia.
2. **Línea de Cartera de servicio** creada y marcada `IsService` antes de facturar a crédito con
   Cartera.
3. **Producción**:
   - Antes: `pg_dump` de la base administrativa y de cada cooperativa.
   - Despliegue por la vía habitual.
   - Habilitación del módulo por el maestro.
   - **Nada de esto se hace sin autorización expresa del dueño.**
4. **Credenciales del proveedor**: las pone el administrador de la cooperativa en pantalla. Nunca
   pasan por el repositorio ni por el chat.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Excepción a «credenciales externas fuera de la base» | el dueño decidió que las pone el administrador en pantalla (FR-057) | archivo secreto del clúster: el administrador no podría ponerlas ni cambiarlas; lo descartó el dueño |
| Destinos opcionales en el emisor (cambio de plataforma) | Transporte debe enterarse de pagos que hoy sólo van a Contabilidad | mensajes nuevos de Cartera hacia `TRN` (Cartera conocería a Transporte); sondeo periódico (no cumple el SC-004 y barre cooperativas) |
| Réplica de las definiciones del maestro | Principio IV: el consumidor no debe leer la base administrativa en contexto de cooperativa | leer `ADM_` en cada envío |
