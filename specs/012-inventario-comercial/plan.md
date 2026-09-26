# Implementation Plan: Inventario comercial renovado — catálogo, existencias, compras, ventas y facturación

**Branch**: `012-inventario-comercial` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/012-inventario-comercial/spec.md`

## Summary

La feature reemplaza el traslado a medias de SOLIDO por un módulo comercial nuevo.

**Qué construye**:
- catálogo, bodegas con bodegas de tránsito, kardex inmutable y costeo;
- compras, ventas y POS con medios de pago configurables y cierre de caja por medio;
- documentos electrónicos DIAN, e integración con Contabilidad y Cartera **sólo por mensajes y
  consultas definidas** (R6).

**Cómo**:
- **Existencias**: el kardex es un hecho que nunca se reescribe. Existencias, saldos y capas son
  proyecciones que se reconstruyen desde él. La concurrencia se resuelve con un **cerrojo pesimista en
  orden fijo**, igual en PostgreSQL y SQL Server (T15), y la numeración va al final.
- **Mensajes**: nacen en la misma transacción del documento, en una **bandeja de salida por
  cooperativa**. Un despachador con **arrendamiento en la base de la cooperativa** los entrega por el
  camino común de MediatR, con un actor de sistema identificado («Proceso de integración») y el contexto
  de la cooperativa fijado. Nada se escribe en la auditoría global.
- **Contabilidad**: del lado contable, y como enmienda de la 009, se construyen:
  - la matriz de reglas, que incluye el medio de pago y el impuesto con su tarifa;
  - el consumidor, que produce un comprobante por documento, o resumido por lote;
  - la validación previa, que reutiliza el ensayo de `AccountingPoster`;
  - la conciliación.

  Las anulaciones y las notas son **comprobantes nuevos**, nunca la reversión total de la 009.
- **DIAN**: un puerto con un **modelo canónico propio**. El primer canal es un proveedor tecnológico
  externo, en un proyecto de infraestructura nuevo. El segundo, el servicio central de la 010.
- **Plataforma**: parámetros con vigencia, aprobaciones multinivel con montos por permiso, alcance por
  bodega y punto, alertas sobre las notificaciones existentes, y auditoría con origen, diferencias,
  entrega garantizada y cadena de sellos verificable.

El proceso con Cartera (IC) queda pendiente; mientras tanto rige el crédito provisional. Las fechas
las define el dueño.

Detalle de cada decisión en [research.md](research.md) (R1–R34). Los nombres canónicos y las
decisiones transversales T1–T52 están en [decisiones-transversales.md](decisiones-transversales.md),
que manda sobre cualquier otra redacción de nombres.

## Technical Context

**Language/Version**: C# / .NET 10.0.5 (API, Domain, Application, Infrastructure); Blazor (Shared, Web,
Web.Client y la app MAUI) con Syncfusion 33.2.8 por paquetes de componente.

**Primary Dependencies**: Carter, MediatR + FluentValidation, EF Core 10 (migraciones pares PostgreSQL
y SQL Server), QuestPDF (comprobantes, informes, representación gráfica), ClosedXML/OpenXML
(plantillas y exportación), QRCoder (QR de la representación gráfica, sólo en la API), `pg_trgm` en
PostgreSQL para la búsqueda de productos. El SDK o cliente HTTP del proveedor tecnológico vive **sólo**
en el proyecto nuevo `Infrastructure/IngenIA365ERP.ElectronicInvoicing`. No hay librerías nuevas en
Domain.

**Storage**:
- **Base de cada cooperativa**:
  - tablas `INV_*` nuevas;
  - `COR_*` de plataforma, impuestos, medios de pago y facturación electrónica;
  - `ACC_Inventory*` del lado contable;
  - `SEC_PermissionAmountLimits`.
- **Tablas existentes que cambian**: columnas nuevas en `COR_People`, `COR_Cities`, `COR_Branches` y
  `ACC_AccountTaxRates`.
- **MongoDB** por cooperativa para la auditoría, alimentado desde `COR_AuditOutbox`.
- **Almacén de objetos**: `IBlobStore` para imágenes, soportes y artefactos DIAN, que no se borran.
- **Credenciales del proveedor**: Secrets del clúster montados en archivo, nunca en la base ni en el
  repositorio.

**Testing**:
- xUnit + FluentAssertions + NSubstitute.
- Casos dorados JSON: costeo (promedio, PEPS, prorrateo y la excepción de arranque), impuestos y
  retenciones, precios y caja, aprobaciones.
- Application sobre datos de prueba.
- Arquitectura, nuevas y ampliadas (§2.18 de las decisiones transversales). Entre ellas:
  - `InventarioNoConoceContabilidadNiCartera`;
  - `LoDeInventarioNoSeReversa`;
  - `ElProveedorTecnologicoSoloLoConoceSuAdaptador`;
  - `LosPagosNoGuardanElNumeroDeTarjeta`.
- e2e con Testcontainers en los dos motores, con **simulador de la DIAN** (`CanalSimulado`).
- Rendimiento con `RUN_PERF_TESTS` (SC-009, SC-017, SC-020).

**Target Platform**: Linux (k3s) para API y Web; navegador y app de Windows/Android para las pantallas;
el POS corre en navegador en modo kiosco o en la app.

**Project Type**: web-service + SPA existentes, más **un proyecto de infraestructura nuevo**
(`IngenIA365ERP.ElectronicInvoicing`), sin servicio desplegable nuevo.

**Performance Goals**:
- venta de 5 ítems con lector en menos de 30 s, percentil 95, con 30 cajas; emisión electrónica p95 ≤ 5 s en el sandbox (SC-004, SC-019);
- búsqueda en menos de 1 s con 50.000 productos (SC-009);
- valorizado a una fecha pasada en menos de 30 s con 50 bodegas (SC-017);
- lote diario de 5.000 documentos en menos de 10 min (SC-020);
- 95 % de los mensajes en línea antes de 1 min (SC-011);
- cierre de caja de 300 ventas en menos de 5 min (SC-025).

**Constraints**:
- Inventario **nunca** lee ni escribe tablas contables ni de Cartera: lo vigila una prueba de
  arquitectura.
- Ningún valor legal en el código.
- Decimal exacto: cantidades a 4 decimales, costo unitario a 6, dinero a 2.
- Todo documento confirmado es inmutable, con la única excepción del caso (a) de FR-066.
- Una transacción explícita por comando.
- Los trabajos de fondo recorren las cooperativas una por una, con arrendamiento en la base de cada
  una.
- Fecha local `America/Bogota` (`HoyLocal`).
- No se guarda el número de tarjeta.
- Las credenciales DIAN nunca tocan la base.

**Scale/Scope**:
- 17 historias y 101 requisitos.
- Unas 70 tablas nuevas: `INV_*`, `COR_*` de plataforma, impuestos, medios de pago y DIAN, y
  `ACC_Inventory*`. Columnas nuevas en 4 tablas.
- Retiro de 23 tablas heredadas; se conserva `INV_Salespeople`.
- 8 migraciones pares: una destructiva con guarda y siete aditivas.
- Unos 95 permisos `Inventory.*` más los de Core, Contabilidad y facturación electrónica.
- Unas 40 pantallas y 33 vistas de informe.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec → clarify (dueño: fechas, Cartera pendiente, factura electrónica parametrizable, crédito provisional) → medios de pago (pedido del dueño al abrir el plan) → research (R1–R34) → este plan → tasks. La spec ya recibió las precisiones de la investigación: norma DIAN vigente, total a pagar, excepción de arranque, factura después del documento equivalente, conciliación con SOLIDO y 422 por permiso de cuerpo. Las piezas del lado contable quedan especificadas aquí como enmienda de la 009; lo que Cartera construya necesita su propia spec (D-02). |
| II   | Clean Architecture | PASS | Motores puros en Domain (costeo, tributario, precios, caja, aprobaciones, unidades) sin IO. Puertos en Application (`IContabilidadParaInventario`, `IConsultasDeCartera`, `ICanalDeEmisionElectronica`, `IEjecutorEnCooperativa`). El proveedor DIAN vive sólo en el proyecto de infraestructura nuevo: la API lo referencia únicamente como raíz de composición, y una prueba de arquitectura lo fija. |
| III  | CQRS + MediatR | PASS | Todo comando tiene su validador. Los consumidores de mensajes son comandos sin ruta, ejecutados por `ISender` con todos los comportamientos. `IdempotencyBehavior` entra al pipeline (Validation → Logging → Idempotency → Audit → Reintento → Performance). Los endpoints sólo reenvían. |
| IV   | Multi-tenancy | PASS | Toda tabla nueva vive en la base de la cooperativa. El despachador, el procesador DIAN y las tareas programadas recorren el directorio y abren cada cooperativa con `IEjecutorEnCooperativa`, con su contexto (base de datos y base de auditoría) fijado. Sin cooperativa resuelta fallan, y hay una guarda contra la base de auditoría global. Los arrendamientos están en la base de cada cooperativa. El servicio central de la 010 sigue sin estado. |
| V    | Person centralizada | PASS | Clientes, proveedores y vendedores son personas del maestro, que se escriben sólo desde el diálogo único (enmienda de la 008: marcas tributarias y autorización de datos). La copia fiscal inmutable de la contraparte en cada documento no es una tabla de rol; está justificada en la spec. El rol de vendedor se crea y se retira sólo por su operación. |
| VI   | PublicId externo | PASS | Rutas, DTOs y mensajes por `PublicId`. El corte de un lote viaja como `cutoffMessagePublicId`, nunca como el `Id` interno. |
| VII  | Soft-delete + auditoría | PASS | Todo es `AuditableEntity` con filtro. Los hechos (`IHechoInmutable`) y los documentos confirmados (`IInmutableTrasConfirmar`) no admiten cambios por ninguna vía de la aplicación; lo vigila una prueba. El mantenimiento técnico del Principio XI les aplica. |
| VIII | Validación dual | PASS | Validadores de FluentValidation. Las pantallas validan antes, incluidos el lector y los pagos, y el servidor manda. |
| IX   | Errores visibles | PASS | Bandeja de mensajes, lotes vencidos, validación fallida, rechazos DIAN y contingencias, todos con alertas (`COR_Alerts`) y destinatarios por permiso. Ningún `catch` vacío. |
| X    | Trazabilidad | PASS (con mejora) | La auditoría registra origen (IP y canal), motivo, diferencias antes y después y rechazos. Tiene entrega garantizada por `COR_AuditOutbox` y una cadena de sellos por cooperativa con anclas HMAC y verificación. El actor es explícito: una persona, o el proceso de integración con el usuario de origen como dato. Retención de 10 años. |
| XI   | Inmutabilidad | PASS | El kardex es un hecho y las correcciones son líneas o documentos nuevos. En Contabilidad, las anulaciones, notas y ajustes son comprobantes nuevos, y lo de Inventario no se reversa desde Contabilidad (`LoDeInventarioNoSeReversa`). |
| XII  | Migraciones | PASS | `RetiroDelInventarioHeredado` es **destructiva con guarda**: falla si hay filas, salvo aprobación por base con respaldo y segundo revisor, lleva el marcador `MIGRACION-DESTRUCTIVA-APROBADA` y va en su propio commit. Las otras siete son aditivas. Las semillas son idempotentes. Aplica a la base administrativa y a todas las de cooperativa. |
| UI   | Indicador de carga; sin colores literales | PASS | Pantallas nuevas con `IndicadorDeCarga` y `EstadoDeCarga` por zona. Los módulos Inventario, Compras, Ventas y POS entran a `ModulosMigrados`. El menú apunta sólo a páginas con `@page`. El POS tiene layout propio (`PosLayout`) sobre los mismos tokens. |

## Project Structure

### Documentation (this feature)

```text
specs/012-inventario-comercial/
├── plan.md                        # este archivo
├── spec.md                        # con las precisiones de la investigación ya aplicadas
├── decisiones-transversales.md    # nombres canónicos (§2) y decisiones T1–T52: manda sobre los nombres
├── research.md                    # Fase 0: hechos del código, R1–R34, lo que falta del dueño, riesgos
├── data-model.md                  # Fase 1: tablas, columnas, índices, estados, errores
├── quickstart.md                  # Fase 1: pruebas automáticas y ensayo por entrega
├── contracts/
│   ├── api.md           # permisos, convenciones y rutas de todo el módulo
│   ├── mensajes.md      # catálogo versionado de mensajes, sobre, orden, estados de entrega
│   ├── contabilidad.md  # matriz, consumidor, validación previa, lotes, conciliación, enmienda de la 009
│   ├── dian.md          # puerto, modelo canónico, estados, contingencias, rechazos, resoluciones
│   └── plantillas.md    # las 16 plantillas de importación en el orden de FR-095
├── checklists/requirements.md
└── tasks.md                       # Fase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Entities/Inventory/{Catalog,Warehousing,Documents,Transactions,Projections,Periods,GoLive,
│                       Purchasing,Pos,Pricing,Security}
├── Entities/{Integration,Approvals,Alerts,Parameters,ElectronicInvoicing}/  Entities/Core/{Taxes,Payments}
├── Entities/Accounting/Inventory/   Entities/Audit/{AuditOutboxEntry,AuditChainHead,AuditAnchor}
├── Inventory/{Costing,Units,Documents,Parameters,Purchasing}/   # motores puros y catálogos fijos
├── Taxes/  Sales/{Pricing,Payments,Cash,Promotions}/  ElectronicInvoicing/  Approvals/
└── Common/{IHechoInmutable,IInmutableTrasConfirmar,Parametros,Text/NormalizadorDeBusqueda}

src/Core/IngenIA365ERP.Application/
├── Inventory/{Catalog,Warehouses,Documents,Kardex,Costing,Transfers,Counts,Purchasing,Sales,Pos,Cash,
│              Pricing,Periods,GoLive,Salespeople,Imports,Reports,Security,Integration,Common}
├── Accounting/Inventory/{Reglas,Contabilizacion,Lotes,Consultas}       # enmienda de la 009
├── ElectronicInvoicing/{Channels,Canonical,Catalogs,Numeracion,Documents,Contingencies,Settings}
├── Core/{Taxes,PaymentMeans}
└── Common/{Execution,Integration,Approvals,Alerts,Parameters,Taxation,Persistence,Imports}
    + Behaviors/IdempotencyBehavior.cs, Interfaces/Security/{IActorActual,IAlcanceDeInventario,ILimitesPorPermiso}

src/Infrastructure/
├── IngenIA365ERP.Persistence/  Configurations/{Inventory,Integration,Approvals,Alerts,Parameters,
│                               ElectronicInvoicing,Core,Accounting,Security,Audit}, Inventory/CerrojoDeInventario.cs,
│                               Interceptors/AuditableEntityInterceptor.cs (ampliado), Seeding/Parametric/*
├── IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/   # 8 pares (research R33)
├── IngenIA365ERP.Audit/  Services/AuditOutboxForwarder.cs, Integrity/SelloDeIntegridad.cs
├── IngenIA365ERP.ElectronicInvoicing/   # PROYECTO NUEVO (I4): Channels/{Simulado,<Proveedor>,ServicioCentral},
│                                        # Credentials, Processor, Circuit/CircuitoDeCanal
├── IngenIA365ERP.Identity/  Seed/{InventoryPermissionCatalogSeeder,ElectronicInvoicingPermissionCatalogSeeder}
└── IngenIA365ERP.Storage/   NotificationEmailDispatcher con arrendamiento por cooperativa

src/Presentation/
├── IngenIA365ERP.API/  Endpoints/{Inventory/*,Core/{Taxes,PaymentMeans}Endpoints,ElectronicInvoicing/*,
│                       Accounting/InventoryIntegrationEndpoints,Reports/InventoryReportsEndpoints}
│                       Integration/{DespachadorDeMensajes,EjecutorEnCooperativa,ProgramadorDeTareas}
│                       Services/{ActorDeLaPeticion,OrigenDeLaPeticion,AlcanceDeInventarioDeLaPeticion,…}
│                       Reports/{RepresentacionGraficaReport,CashCountReport,CashMovementReceiptReport,SalesDocumentReport}
└── IngenIA365ERP.Shared/  Pages/{Inventario,Compras,Ventas,Pos}/*, Pages/Maestros/{Impuestos,MediosDePago,
                           ResolucionesDian}, Pages/Contabilidad/Inventario/*, Pages/Admin/FacturacionElectronica,
                           Layout/PosLayout.razor, wwwroot/js/pos.js, Services/{Inventario,Ventas,Compras,Core,
                           FacturacionElectronica}/*Client*.cs

tests/
├── IngenIA365ERP.Domain.Tests/{Inventory/Costing/Casos,Taxes/Casos,Sales,Approvals}/
├── IngenIA365ERP.Application.Tests/{Inventory,Accounting/Inventory,ElectronicInvoicing,Common}/
├── IngenIA365ERP.Architecture.Tests/Principles/   # las de decisiones-transversales §2.18
└── IngenIA365ERP.API.IntegrationTests/{Inventory,Integration,ElectronicInvoicing}/   # e2e por entrega
```

**Structure Decision**: se extienden los proyectos existentes siguiendo el molde de las features 009,
010 y 011. Se agrega **un solo** proyecto, `Infrastructure/IngenIA365ERP.ElectronicInvoicing`, porque es
el único lugar donde puede vivir el cliente del proveedor tecnológico sin que Domain, Application ni
el resto de la API lo conozcan. No hay servicio desplegable nuevo: el modo «software propio» reutiliza
el servicio central de la 010 cuando exista.

Lo compartido va a **Core** para que Tesorería y Cartera lo reutilicen:
- impuestos y retenciones;
- medios de pago, tarjetas y datáfonos;
- facturación electrónica;
- parámetros con vigencia, aprobaciones y alertas.

Lo comercial va a `INV_`, y lo contable a `ACC_Inventory*`. No se crean prefijos nuevos (T2).

## Complexity Tracking

Sin violaciones de la constitución. Tres puntos piden justificación:

| Punto | Por qué | Alternativa descartada |
|---|---|---|
| Proyecto nuevo `IngenIA365ERP.ElectronicInvoicing` | El proveedor es intercambiable (Q3), y su cliente no debe contaminar Application ni la API. | Ponerlo en la API: una prueba de arquitectura no podría aislar el SDK y el cambio de proveedor tocaría el núcleo. |
| Piezas de plataforma nuevas (bandeja de salida, arrendamientos, idempotencia, parámetros, aprobaciones, alertas, cadena de auditoría) | R3 a R7 de la spec las exigen y hoy no existen (D-03). Se construyen reutilizables y en Core. | Resolverlas dentro de Inventario: la 009, Nómina y Tesorería tendrían que duplicarlas. |
| Migración destructiva de 23 tablas | El dueño decidió retirar el módulo actual sin migrar (Q1). | Convivir con las tablas viejas: dejaría dos fuentes de existencias. |

## Decisiones

Se adoptan las decisiones T1–T52 de [decisiones-transversales.md](decisiones-transversales.md) y R1–R34
de [research.md](research.md). Resumen por tema:

| Tema | Decisión del plan |
|---|---|
| Nombres y módulo (T1–T4) | Identificadores en inglés; motores y puertos en español; mensajes sin tildes. Prefijos de la constitución. Un módulo de código `Inventory` con rutas `/api/inventory/*`; facturación electrónica `/api/electronic-invoicing/*`; catálogos compartidos `/api/core/*`; lado contable `/api/accounting/inventory/*`. |
| Ejecución por cooperativa (T5, T6, T10, T47) | `IEjecutorEnCooperativa` con contexto ambiental. `IActorActual` nuevo (no se toca `ICurrentUserService.UserId`: pregunta B1). Arrendamientos en `COR_BackgroundLeases` de cada base. Guarda en `MongoAuditService`. |
| Mensajes (T7–T12) | Bandeja `COR_IntegrationMessages` con sus entregas por destino y dependencias, emitida desde I1. Orden por documento y relacionados. Un comprobante por documento; los lotes son de plataforma, con resumen por fecha, tipo y sucursal. El recibo único es `ACC_InventoryPostings`. El resultado lo registra un comando aparte. |
| Idempotencia y transacción (T13, T14) | `COR_OperationKeys` con la cabecera `Idempotency-Key` y retención indefinida; `TransaccionExplicita` única. |
| Existencias y costo (T15–T19) | Cerrojo pesimista en orden fijo; numeración al final. Documento genérico `INV_Documents` con clases fijas. Kardex como hecho y proyecciones reconstruibles. Promedio ponderado por cooperativa o por bodega; PEPS en I5. Excepción de arranque y ajustes de conteo fuera del parámetro de retroactivos, desde I1. Decimales 4, 6 y 2. |
| Fecha y parámetros (T20, T21) | `HoyLocal` `America/Bogota`. `COR_ParameterVersions` con catálogos de claves por módulo y valores por defecto seguros. |
| Impuestos (T22–T24, T26) | Catálogo en Core (`COR_Tax*`, `COR_WithholdingConcepts`) con motor tributario puro; tarifas como fracción a 6 decimales; UVT leída de `PAY_LegalParameters` por un solo lector. Marcas tributarias en `COR_People` y DIVIPOLA en ciudades y sucursales. Total a pagar = total − retenciones del comprador. |
| Medios de pago y caja (T25, T50) | `COR_PaymentMeans` con clase fija, `COR_CardNetworks`, `COR_CardAcquirers`, `COR_CardTerminals` y `COR_CashDenominations` en Core; disponibilidad por punto en Inventario. Sesiones, movimientos, arqueo por medio con denominaciones y referencias, diferencias con aprobación, cierre del día. Sin número de tarjeta. |
| Contabilidad (T27–T31) | Matriz `ACC_InventoryPostingRules` por operación y rol, con dimensiones por código y resolución por especificidad. Tipos `FV`, `EI` y `SI` más `NV`, `CP`, `TR`, `AC` y `CJ`. Anulaciones como comprobantes nuevos. Validación previa sobre `ValidarAsync`. Frontera en `Application/Common/Integration`. |
| Cartera (T32) | Destino `Lending` acumulado sin reintentos mientras IC está pendiente; crédito provisional con aprobación; cuenta por cobrar desde `VentaFacturada`. |
| Seguridad (T33–T35, T48) | Motor de aprobaciones de plataforma (`COR_Approval*`) con segregación. `SEC_PermissionAmountLimits` (manda el mayor). Alcance por bodega y punto, que falla cerrado. Unos 95 permisos y perfiles sugeridos como plantillas. |
| Auditoría y alertas (T36–T39) | Origen, motivo, rechazos y diferencias. `COR_AuditOutbox` con cadena y anclas HMAC para Inventario, su plataforma y Navegación. Alertas sobre `COR_Notifications`. |
| DIAN (T40–T42, T52) | Puerto `ICanalDeEmisionElectronica` y modelo canónico v1. `COR_ElectronicDocuments` con versiones, transmisiones, resoluciones con clave técnica y canal, y configuración con vigencia. `GuardiaDeEmisionFiscal` según la obligación. Contingencias 03 y 04 con circuito: la demora del POS cuenta como falla. Casos a, b y c. Eventos RADIAN registrados en I1 y emitidos en I5. |
| Catálogo, búsqueda e importación (T43, T49) | `SearchText` normalizado; búsqueda «contiene» por cada término (`LIKE '%término%'` escapado, todos los términos). PostgreSQL `pg_trgm` + GIN; SQL Server recorre el índice no agrupado cubriente sobre `SearchText`, aceptable a 50.000 productos; si la medición de SC-009 no cumple en SQL Server, se activa un índice de texto completo como alternativa. Plantillas con encabezados en la fila 1, todo o nada, errores por fila y columna, en el orden de `contracts/plantillas.md`. |
| Informes y pantallas (T44, T45) | 33 vistas `TablaExportable` en `/api/reports/inventory/{vista}`. Pantallas bajo `/inventario`, `/compras`, `/ventas` y `/pos`, y maestros de Core. POS con layout propio, lector en modo teclado y atajos. |
| Personas y datos (T46, T52) | Autorización de datos al dar de alta desde el diálogo único; consumidor final sembrado; copia fiscal inmutable por documento. |

## Entregas

Sin fechas: las define el dueño cuando la aplicación esté lista. Ruta crítica de COOFLOPAL: **I1 → I2 →
I3 → I4**, con I4 construida en paralelo sobre el simulador. El ensayo exige I1 a I4.

| Entrega | Contenido | Migración |
|---|---|---|
| **I1 · Núcleo y plataforma** | Plantillas primero. Retiro del módulo actual; plataforma completa (T5–T21, T33–T39); catálogo tributario en Core; catálogo, bodegas, kardex, costeo promedio con la excepción de arranque, documentos de inventario, compra directa, traslados, conteos, períodos, saldo inicial, cifras de SOLIDO, vendedores, alcance, alertas, auditoría verificable. | `RetiroDelInventarioHeredado` (destructiva con guarda), `PlataformaParaInventario`, `InventarioComercialNucleo` |
| **I2 · Integración contable** | Despachador y lotes; enmienda de la 009 (matriz, consumidor, validación previa, conciliación, aviso de cierre); bandeja; activación de bodegas con cuadre. | `IntegracionContableDeInventario` |
| **I3 · Ventas y POS** | Medios de pago en Core; puntos, cajas, sesiones, arqueo y cierre por medio; POS; ventas y notas (las electrónicas confirman con I4); comprobante no electrónico; precios y topes; crédito provisional. | `VentasYPuntoDeVenta` |
| **I4 · Documentos electrónicos** | Proyecto nuevo, simulador y adaptador del proveedor; numeración fiscal, contingencias, casos a, b y c, factura después del documento equivalente; documento soporte; representación gráfica y correo. | `DocumentosElectronicos` |
| **IC · Crédito con Cartera (pendiente)** | Consultas reales, entrega de lo acumulado y validación. Espera la spec de Cartera (D-02). | — |
| **I5 · Compras completas y costeo avanzado** | Solicitud, orden, recepción parcial, cruce a tres vías, prorrateo, emisión RADIAN, PEPS, retroactivos generales, cambio de método. | `ComprasYCosteoAvanzado` |
| **I6 · Comercio ampliado y analítica** | Cotización, pedido con reserva, remisión, nota débito, promociones, variantes, combos y ensamble, lote, serie y vencimiento, conteo ABC, reposición, informes avanzados y tablero. | `ComercioAmpliado` |

## Riesgos y lo que falta del dueño

El detalle está en research.md, en «Lo que falta del dueño» (78 preguntas en nueve grupos, cada una
con su propuesta por defecto) y en «Riesgos». Si el dueño no responde, **rige la propuesta**. Sólo unas
pocas preguntas bloquean algo:

- **Antes de publicar las plantillas (I1)**:
  - el largo del código de producto (propuesta: 20);
  - si alguna cooperativa ya tiene filas en las tablas heredadas: el segundo revisor y la aprobación
    del retiro.
- **Antes de I1 en producción**: verificar en los clústeres el usuario de Mongo de la API y la clave de
  firma de auditoría.
- **Para la salida (I4)**:
  - el proveedor tecnológico (lista corta propuesta: The Factory HKA / Dataico, con prueba en sandbox);
  - las resoluciones y los prefijos que COOFLOPAL usa hoy;
  - el buzón de correo para entregar los documentos.
- **Para el ensayo**: la contadora valida los tipos de comprobante, la semilla tributaria, los códigos
  DIAN de los medios de pago y las cuentas de la matriz.
- **Para IC**: la especificación de Cartera, con la lista de lo que debe cubrir (pregunta H4).

Riesgos principales:
- el tamaño de I1, que carga toda la plataforma;
- la dependencia de un proveedor tecnológico todavía no elegido;
- el volumen de documentos del POS ante la DIAN (unos 150.000 al mes);
- la calidad de los datos que llegan de SOLIDO;
- la migración destructiva en ambientes donde alguien ya haya parametrizado el módulo actual.

## Re-evaluación de la constitución tras el diseño

Sin cambios en las compuertas. La revisión de los artefactos de la Fase 1 corrigió dos riesgos
concretos:
- **Principio VI**: el corte de los lotes exponía el `Id` interno de los mensajes, y ahora viaja como
  `PublicId`.
- **R6**: la bandeja de Inventario iba a leer el número del comprobante contable, y ahora lo toma del
  resultado que registra la entrega.

La migración destructiva conserva su guarda y su marcador. El servicio central de la 010 sigue sin
estado.
