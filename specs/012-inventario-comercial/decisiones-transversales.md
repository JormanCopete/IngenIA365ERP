# Decisiones transversales — 012 Inventario comercial

**Fecha**: 2026-09-24 · **Estado**: armonización de la fase 0 (siete temas investigados por separado).
**Fuente de verdad** para `research.md`, `data-model.md`, `contracts/*`, `quickstart.md`, `plan.md` y
`tasks.md` de la feature 012. Quien redacte esos artefactos **reutiliza literalmente** los nombres de la
§2 y respeta las decisiones T1..T52 de la §3. Si un borrador contradice este documento, gana este
documento; si este documento contradice la especificación o la constitución, ganan ellas y se corrige
aquí.

Se publica en `specs/012-inventario-comercial/decisiones-transversales.md`; todo artefacto de la feature
lo cita por ese nombre. `data-model.md` va de la §0 a la §27 y `contracts/api.md` de la §1 a la §32.

Insumos leídos: `spec.md` (101 FR, 25 SC, 17 historias), la constitución (12 principios) y los 14
archivos de investigación de la fase 0, uno de decisiones y otro de hechos por tema (contabilidad,
mensajería, núcleo, ventas y POS, DIAN, seguridad, compras-impuestos-cartera). Esos archivos no se
publican: su contenido quedó consolidado en `research.md` (R1–R34), y las menciones de este documento a
«núcleo 12», «ventas 6» o «seguridad 13» sólo dicen de qué tema vino cada decisión.

Decisiones del dueño que este documento da por fijas y no reabre: Q1 A (COOFLOPAL sale con I1–I4;
obligada a facturar electrónicamente; la obligación es un parámetro por cooperativa; ninguna fecha
fija en los artefactos), Q2 B (mensajes asíncronos con validación previa y modo de paso en línea /
por lotes / no pasa; enmienda la 009 para Inventario), Q3 C (proveedor tecnológico externo primero y
software propio por el servicio central de la 010 después, detrás del mismo adaptador), IC pendiente
con crédito provisional, medios de pago dinámicos (sección K) y las 20 decisiones por defecto de los
Supuestos.

---

## 1. Arquitectura en una página

### 1.1 Mapa de proyectos y carpetas

```
src/Presentation
  IngenIA365ERP.API
    Endpoints/Inventory/*.cs                 módulo comercial (catálogo, bodegas, documentos, compras,
                                             ventas, POS, caja, precios, períodos, puesta en marcha,
                                             parámetros, aprobaciones, alertas, alcance, bandeja)
    Endpoints/Core/{Taxes,PaymentMeans}Endpoints.cs          catálogos compartidos de Core
    Endpoints/ElectronicInvoicing/*.cs       facturación electrónica (plataforma)
    Endpoints/Accounting/InventoryIntegrationEndpoints.cs    lado contable (enmienda 009)
    Endpoints/Reports/InventoryReportsEndpoints.cs           reescrito: una ruta por vista
    Integration/  DespachadorDeMensajes, EjecutorEnCooperativa, ProgramadorDeTareas,
                  SenalDeMensajes, IntegrationOptions        (trabajos de fondo; sólo la API los registra)
    Services/     ActorDeLaPeticion, OrigenDeLaPeticion, AlcanceDeInventarioDeLaPeticion,
                  LimitesPorPermiso, DestinatariosPorPermiso
    Reports/      RepresentacionGraficaReport (I4), CashMovementReceiptReport, CashCountReport,
                  SalesDocumentReport (I3)
  IngenIA365ERP.Shared
    Pages/{Inventario,Compras,Ventas,Pos}/*.razor           Pages/Maestros/{Impuestos,MediosDePago,
    ResolucionesDian}.razor   Pages/Contabilidad/Inventario/*.razor   Pages/Admin/FacturacionElectronica.razor
    Layout/PosLayout.razor    Components/{Inventario,Pos}/*   wwwroot/js/pos.js
    Services/{Inventario,Ventas,Compras,Core,FacturacionElectronica}/*Client*.cs
    Services/Http/CanalDeOrigenHandler.cs    Services/IImpresionDeDocumentos.cs
  IngenIA365ERP.Web / .Web.Client / .App     registran clientes y handlers (los tres anfitriones)

src/Core
  IngenIA365ERP.Domain
    Entities/Inventory/{Catalog,Warehousing,Documents,Transactions,Projections,Periods,GoLive,
                        Purchasing,Pos,Pricing,Security}
    Entities/Integration (+ /Transactions)   Entities/Approvals (+ /Transactions)   Entities/Alerts
    Entities/Parameters   Entities/ElectronicInvoicing (+ /Transactions)
    Entities/Core/Taxes   Entities/Core/Payments   Entities/Audit/{AuditOutboxEntry,AuditChainHead,AuditAnchor}
    Entities/Accounting/Inventory (+ Entities/Accounting/Transactions/InventoryPosting)
    Entities/Security/PermissionAmountLimit
    Enums/{Inventory,Integration,Approvals,Alerts,Parameters,ElectronicInvoicing,Dian,Core}
    Inventory/{Costing,Units,Documents,Parameters,Purchasing}      motores puros y catálogos fijos
    Taxes/   Sales/{Pricing,Payments,Cash,Promotions}   ElectronicInvoicing/   Approvals/
    Common/{IHechoInmutable, IInmutableTrasConfirmar, SinDiffDeAuditoriaAttribute, NoAuditarAttribute,
            Parametros/DefinicionDeParametro, Text/NormalizadorDeBusqueda}
  IngenIA365ERP.Application
    Inventory/{Catalog,Warehouses,Documents,Kardex,Costing,Transfers,Counts,Purchasing,Sales,Pos,
               Cash,Pricing,Periods,GoLive,Salespeople,Imports,Reports,Security,Integration,Common}
    ElectronicInvoicing/{Channels,Canonical,Catalogs,Numeracion,Documents,Contingencies,Settings}
    Accounting/Inventory/{Reglas,Contabilizacion,Lotes,Consultas}
    Core/{Taxes,PaymentMeans}
    Common/{Execution,Integration,Approvals,Alerts,Parameters,Taxation,Persistence,Imports}
    Common/Behaviors/IdempotencyBehavior.cs  Common/Interfaces/Security/{IActorActual,IAlcanceDeInventario,
            ILimitesPorPermiso}   Common/Interfaces/IOrigenDeLaPeticion.cs
    Security/Roles/Plantillas/PerfilesSugeridos.cs   Compliance/HabeasData/IAutorizacionDeDatos.cs

src/Infrastructure
  IngenIA365ERP.Persistence   Configurations/{Inventory,Integration,Approvals,Alerts,Parameters,
                              ElectronicInvoicing,Core,Accounting,Security,Audit}
                              Inventory/CerrojoDeInventario.cs  Interceptors/AuditableEntityInterceptor.cs (ampliado)
                              Seeding/Parametric/* (semillas nuevas, §2.14)
  IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}   pares por entrega (§2.15)
  IngenIA365ERP.Audit         Services/AuditOutboxForwarder.cs  Integrity/SelloDeIntegridad.cs
                              Indexes/AuditRetention.cs (ampliado)
  IngenIA365ERP.ElectronicInvoicing  (PROYECTO NUEVO, I4) Channels/{Simulado,<Proveedor>,ServicioCentral},
                              Credentials/CredencialesEnArchivo, Processor/ProcesadorDeDocumentosElectronicos,
                              Circuit/CircuitoDeCanal
  IngenIA365ERP.Identity      Seed/{InventoryPermissionCatalogSeeder, ElectronicInvoicingPermissionCatalogSeeder}
                              (+ códigos nuevos en Core/Accounting/DomainPermissionCatalogSeeder)
  IngenIA365ERP.Storage       NotificationEmailDispatcher pasa a arrendamiento por cooperativa (T47)
```

### 1.2 Qué vive dónde

| Pieza | Dónde | Tablas | Quién la usa |
|---|---|---|---|
| Contexto de ejecución por cooperativa fuera de HTTP | `Application/Common/Execution` + `API/Integration/EjecutorEnCooperativa` | — | despachador, procesador DIAN, tareas programadas |
| Actor y origen | `IActorActual`, `IOrigenDeLaPeticion` (Application) → `API/Services` | — | todo el módulo, aprobaciones, auditoría |
| Bandeja de salida y despacho | `Application/Common/Integration` + `API/Integration` | `COR_Integration*` | Inventario emite; Contabilidad (I2) y Cartera (IC) consumen |
| Idempotencia de pantalla | `Common/Behaviors/IdempotencyBehavior` | `COR_OperationKeys` | todo comando `IOperacionIdempotente` |
| Arrendamientos de trabajos de fondo | `Application/Common/Execution/IArrendamientos` | `COR_BackgroundLeases` | despachador, reenviador de auditoría, procesador DIAN, tareas, correo |
| Parámetros con vigencia | `Application/Common/Parameters` + catálogos de claves por módulo | `COR_ParameterVersions` | Inventario, tributario, facturación electrónica |
| Aprobaciones multinivel | `Domain/Approvals` + `Application/Common/Approvals` | `COR_Approval*` | documentos, descuentos, arqueo, crédito provisional, cruce |
| Montos máximos por permiso | `ILimitesPorPermiso` | `SEC_PermissionAmountLimits` | confirmar con valor (FR-009) |
| Alcance por bodega y punto | `IAlcanceDeInventario` | `INV_UserWarehouseScopes`, `INV_UserPointOfSaleScopes` | toda consulta y comando del módulo |
| Alertas | `Application/Common/Alerts` | `COR_AlertTypes`, `COR_Alerts` (+ `COR_Notifications`) | todo «alerta» de la spec |
| Auditoría garantizada y sello | `Infrastructure/Audit` + `ApplicationDbContext` | `COR_AuditOutbox`, `COR_AuditChainHeads`, `COR_AuditAnchors` | módulos encadenados (T38) |
| Catálogo de impuestos y motor tributario | Core: `Application/Core/Taxes`, `Domain/Taxes` | `COR_Tax*`, `COR_WithholdingConcepts` | compras, ventas, DIAN |
| Medios de pago y tarjetas | Core: `Application/Core/PaymentMeans` | `COR_PaymentMeans`, `COR_Card*`, `COR_CashDenominations` | ventas, caja; mañana Cartera y Tesorería |
| Módulo comercial | `*/Inventory/*` | `INV_*` | — |
| Lado contable (enmienda 009) | `Application/Accounting/Inventory` | `ACC_Inventory*` | consumidor de mensajes, matriz, validación previa |
| Facturación electrónica | `Application/ElectronicInvoicing` + proyecto `Infrastructure/IngenIA365ERP.ElectronicInvoicing` | `COR_Electronic*`, `COR_Dian*` | ventas y compras (DS); reutilizable por otros módulos |
| Frontera con Contabilidad y Cartera | `Application/Common/Integration/{Accounting,Lending,Contracts}` | — | lo único de otros módulos que Inventario puede referenciar |

### 1.3 Flujo canónico de confirmación (todas las clases)

1. `IdempotencyBehavior` abre `TransaccionExplicita` e inserta la fila de `COR_OperationKeys` (T13, T14).
2. El handler (`ConfirmInventoryDocumentCommand`) relee el borrador, verifica clase ↔ ruta
   (`DocumentClassGroup`), alcance (`IAlcanceDeInventario`), bodega activa, período
   (`INV_Setup.LastClosedDate`) y reglas de la clase (`ClasesDeDocumento`).
3. Motor de aprobaciones (`IMotorDeAprobaciones.EvaluarAsync` + `ILimitesPorPermiso`): con niveles,
   el documento pasa a `PendingApproval` y termina aquí (sin número). La última aprobación vuelve a
   entrar por el paso 2 en la transacción del aprobador.
4. `GuardiaDeEmisionFiscal.Evaluar` si la clase es fiscal (`Electronic` / `NonElectronic` / `Blocked`).
5. Si el modo que se sellará ≠ `NotPosted`: validación previa por `IContabilidadParaInventario.EvaluarAsync`
   **fuera del cerrojo**, con tiempo máximo `Contabilidad.ValidacionPreviaSegundos`; «no responde» →
   política `Contabilidad.PoliticaSinRespuesta` (T30). Crédito: `IConsultasDeCartera` (mientras IC
   pendiente, `CarteraNoHabilitada` → crédito provisional con aprobación, T32).
6. Cerrojo en orden canónico (`ICerrojoDeInventario`): `INV_Setup` (compartido) → `INV_Warehouses`
   (compartido, por Id) → `INV_CostStates` → `INV_StockBalances` → `INV_StockDetails` (exclusivos, por Id)
   → fila de numeración (`INV_DocumentSequences` o `COR_DianNumberingResolutions`) al final (T15, T16).
7. `MotorDeCosteo` + `RegistroDeKardex` (kardex y proyecciones, sin guardar); disponibilidad ≥ 0 salvo
   parámetro (T18).
8. `Numerador` / `NumeradorFiscal` asigna el número.
9. `EmisorDeMensajes` agrega mensajes, entregas (modo sellado o heredado) y dependencias (T7–T9).
10. Si es fiscal electrónico: fila `COR_ElectronicDocuments` en `Pending` + versión 1 con el SHA-256
    del canónico (el artefacto se sube después del commit, T40).
11. Un `SaveChanges` (el `AuditableEntityInterceptor` agrega las diferencias a `COR_AuditOutbox` y
    `AuditBehavior` el evento del comando, T37) y commit.
12. Después del commit: `ISenalDeMensajes` despierta al despachador; en el POS, intento en línea de
    `EmitElectronicDocumentCommand` hasta `Dian.EsperaMaximaPosSegundos`.

### 1.4 Flujo de un mensaje

`EmisorDeMensajes` (en la transacción del documento) → `COR_IntegrationMessages` +
`COR_IntegrationMessageDeliveries` (por destino) + `COR_IntegrationMessageDependencies` →
`DespachadorDeMensajes` (arrendamiento `integration.dispatch` en la base de la cooperativa) → elige
entregas elegibles por Id → `IEjecutorEnCooperativa` (ámbito DI nuevo, contexto ambiental, actor
«Proceso de integración») → comando del destino (`PostInventoryMessagesCommand` en Contabilidad) que
escribe el comprobante y `ACC_InventoryPostings` (recibo único por `MessagePublicId`) en un
`SaveChanges` → nuevo ámbito → `RegisterDeliveryResultCommand` (estado de la entrega +
`COR_IntegrationDeliveryAttempts`).

---

## 2. Nombres canónicos

### 2.1 Reglas de nombres (T1, T2, T3)

- **Inglés**: entidades (PascalCase singular), tablas, columnas, enums (tipo y valores), comandos y
  consultas (verbo + entidad), DTO (sufijo `Dto`), rutas (`/api/{módulo}/{plural-kebab}`), permisos
  (`Módulo.Recurso.Acción`), códigos de error (`Área.Recurso.Motivo`), carpetas y espacios de nombres
  nuevos.
- **Español permitido** (precedente del código: `MovimientosContables`, `AccountLineRules`/
  `CuentaParaReglas`, `GuardiaDeMetodos`): motores puros, servicios, puertos entre módulos, lectores,
  guardias y pruebas de arquitectura; los **tipos de mensaje** (español PascalCase, como la spec); los
  **códigos de dato** que llena la cooperativa (operaciones y roles de la matriz, claves de parámetros,
  tipos de alerta). Todos están nombrados en esta sección: nadie inventa otro.
- **Sin tildes en identificadores**: «DevoluciónRegistrada» de la spec es el tipo `DevolucionRegistrada`;
  «NotaCréditoEmitida» es `NotaCreditoEmitida`; «NotaDébitoEmitida» es `NotaDebitoEmitida`. La pantalla
  muestra el nombre con tilde.
- **Prefijos de tabla**: sólo los que lista la constitución. `INV_` (módulo comercial completo: catálogo,
  bodegas, documentos, compras, ventas, POS, caja, precios), `COR_` (plataforma y catálogos compartidos,
  incluida la facturación electrónica), `ACC_` (lado contable), `SEC_` (seguridad). **No** se crean
  `INT_`, `SLS_` ni `FEL_`.
- **Numeradores**: la propiedad nunca se llama `NextNumber` (lo vigila
  `NingunModuloEscribeMovimientosFueraDelContrato`): `NextValue` en consecutivos del módulo y en el
  contador de lotes, `LastIssuedNumber` en resoluciones DIAN.
- Toda entidad nueva hereda `AuditableEntity` o `AuditableEntityLong` (Principio VII), tiene `PublicId`
  (Principio VI) y su configuración declara el filtro de borrado lógico.

### 2.2 Tablas nuevas (base de cada cooperativa)

`E` = entrega en que nace la tabla. `H` = hecho inmutable (`IHechoInmutable`, sólo inserción).

**Plataforma — ejecución, idempotencia, parámetros** (`Domain/Entities/{Parameters,Core,Integration}`)

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `COR_ParameterVersions` | `ParameterVersion` | I1 | Valor con vigencia por (`Module`, `Key`, `ScopeKind`, `ScopeId`, `ValidFrom`); `Value` texto, `Reason` obligatorio, `LegalSource` opcional; único filtrado por no borrados. |
| `COR_OperationKeys` | `OperationKey` | I1 | Clave de idempotencia de una operación de pantalla: `Key` (único), `Operation`, `RequestSha256`, `CentralUserId`, `ActorName`, `ResultJson`. Nunca se borra. |
| `COR_BackgroundLeases` | `BackgroundLease` | I1 | Arrendamiento de un trabajo de fondo en la base de la cooperativa: `Name` (único), `Owner`, `LeaseUntil`, `RowVersion`. |

**Plataforma — mensajería** (`Domain/Entities/Integration`, inmutables en `/Transactions`)

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `COR_IntegrationMessages` | `IntegrationMessage` (H) | I1 | Mensaje de negocio emitido: `PublicId` = MessageId, `Type`, `Version`, `Kind`, origen, relacionado, raíz de cadena, fecha de operación, sucursal, persona, moneda y tasa, `PayloadJson`, `PayloadSha256`, `PrevalidationOutcome`, usuario de origen. Id `bigint` = orden (interno: nunca sale en JSON). UK `(OriginPublicId, Type, OriginEventKey)`; valores de `OriginEventKey` en §2.6. |
| `COR_IntegrationMessageDependencies` | `IntegrationMessageDependency` (H) | I1 | Arista «este mensaje espera a aquél». |
| `COR_IntegrationMessageDeliveries` | `IntegrationMessageDelivery` | I1 | Estado por destino (`Destination`, `Mode`, `Status`, `ScheduleKey`, `BatchScopeKey`, `BatchId`, `Attempts`, `NextAttemptAt`, último error, `ProcessedAt`, `ResultReference`, `ResultVoucherTypeCode` (10), `ResultVoucherNumber` (30), `RowVersion`). `ScheduleKey` = `{DocumentTypeCode raíz}\|{DisparadorDeLote}\|{HoraDeLote}\|{Granularidad}` sellados al confirmar; `BatchScopeKey` = `CashSession:{publicId}` (CierreDeTurno) · `Period:{aaaa-mm}` (CierreDePeriodo). El resultado (PublicId, tipo y número del comprobante) lo devuelve el consumidor y lo escribe `RegisterDeliveryResultCommand`: la bandeja de Inventario lo lee de aquí, nunca de tablas `ACC_`. UK `(MessageId, Destination)`. |
| `COR_IntegrationDeliveryAttempts` | `IntegrationDeliveryAttempt` (H) | I2 | Bitácora de cada intento: inicio, fin, resultado, código, actor, lote, instancia. |
| `COR_IntegrationBatches` | `IntegrationBatch` | I2 | Orden y registro de lote, reproceso o envío posterior: `Number`, `Destination`, `ScheduleKey`, `Trigger`, `ScheduledFor`, `CashSessionPublicId`, período, `CutoffMessageId` (`bigint` interno; por HTTP llega como `cutoffMessagePublicId` y el handler lo resuelve), rango de fechas, `Granularity`, `Status`, solicitante (persona o proceso), totales, resultado. UK filtrado `(ScheduleKey, ScheduledFor)` para `Trigger = Scheduled`. |
| `COR_IntegrationBatchCounters` | `IntegrationBatchCounter` | I2 | Fila única con `NextValue` del número de lote. |

**Plataforma — aprobaciones, alertas, auditoría; seguridad**

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `COR_ApprovalPolicies` | `ApprovalPolicy` | I1 | Política por (`Module`, `Subject`, `DocumentTypePublicId` nulo = todos los tipos del sujeto) con `PolicyKey`, `Version`, vigencia sin cruces y motivo. `Module = "Inventory"` (el mismo valor en la API y en la base); `Subject` ∈ `ApprovalSubjects` (§2.5). |
| `COR_ApprovalPolicyLevels` | `ApprovalPolicyLevel` | I1 | Nivel: `Order`, `Threshold`, `PermissionCode`. |
| `COR_ApprovalRequests` | `ApprovalRequest` | I1 | Solicitud: módulo, `Subject`, `SourceType` (`InventoryDocument`, `DocumentLineDiscount`, `DocumentPayment`, `TransferDiscrepancy`, `PurchaseMatchLine`), `SourcePublicId`, `Amount`, `Currency`, política sellada, creador, solicitante, excluidos, `Status`, `CurrentLevel`, `ContentSha256` (huella de lo aprobado). |
| `COR_ApprovalDecisions` | `ApprovalDecision` (H) | I1 | Decisión por nivel: quién (`SEC_Users.Id`), cuándo, aprobar/rechazar, motivo, `Method`. UK `(RequestId, Level)` entre aprobaciones. |
| `COR_AlertTypes` | `AlertType` | I1 | Configuración por cooperativa y vigencia de cada tipo de alerta: `TypeCode`, `RecipientPermissions`, `Channels`, umbrales JSON, motivo. |
| `COR_Alerts` | `Alert` | I1 | Alerta levantada: tipo, módulo, severidad, asunto, cuerpo, entidad (`EntityType` + `EntityPublicId`), `DedupKey` único mientras esté pendiente, `Status`, atendida por y nota, `WithoutRecipient`. |
| `COR_AuditOutbox` | `AuditOutboxEntry` | I1 | Evento de auditoría escrito en la misma transacción del cambio: `EventId`, `Stream`, `OccurredAt`, `PayloadJson`, `Forwarded`, `ForwardedAt`, `Seq`, `Hash`. Tras reenviar se vacía `PayloadJson` y queda la fila delgada (nunca DELETE). |
| `COR_AuditChainHeads` | `AuditChainHead` | I1 | Cabeza de cada cadena (`Stream` único, `LastSeq`, `LastHash`, `RowVersion`): impide bifurcar la cadena. |
| `COR_AuditAnchors` | `AuditAnchor` (H) | I1 | Ancla cada N eventos y diaria: `Stream`, `Seq`, `Hash`, `Hmac`, `KeyVersion`. |
| `SEC_PermissionAmountLimits` | `PermissionAmountLimit` | I1 | Monto máximo por (`RoleId`, `PermissionCode`) con vigencia, moneda y motivo. |

**Core — catálogos compartidos** (`Domain/Entities/Core/{Taxes,Payments}`)

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `COR_TaxDefinitions` | `TaxDefinition` | I1 | Impuesto o retención: `Code`, `Name`, `Kind`, `CalculationForm`, `TaxedOnDefinitionId` (ReteIVA), `IsWithholding`, `DianTaxCode`. |
| `COR_TaxRates` | `TaxRate` | I1 | Tarifa con vigencia: `Rate` (fracción, 9,6) o `AmountPerUnit`, concepto de retención, municipio DANE y CIIU (o `*`), base mínima en UVT o pesos, condiciones tipadas sobre las partes, `AppliesTo`, `Priority`, `LegalSource`. |
| `COR_WithholdingConcepts` | `WithholdingConcept` | I1 | Concepto de retención (compras, servicios, honorarios, arrendamientos, transporte, otros). |
| `COR_PaymentMeans` | `PaymentMeans` | I3 | Medio de pago configurable (clase fija, tarjeta, banco destino como dato, reglas de captura y arqueo, tolerancia, comisión esperada, `DianPaymentMeansCode`, vigencia). Modificarlo exige motivo (`IConMotivo`); tolerancia y comisión esperada se **copian** en cada `INV_CashCountLines` y en cada pago al usarse, así que un cambio sólo afecta lo que se cierre o cobre después. |
| `COR_CardNetworks` | `CardNetwork` | I3 | Franquicia o red (`CardKind`). |
| `COR_CardAcquirers` | `CardAcquirer` | I3 | Adquirente, con `PersonId` para el tercero. |
| `COR_CardTerminals` | `CardTerminal` | I3 | Datáfono de cobro (no confundir con `DEB_PosTerminals`). |
| `COR_CashDenominations` | `CashDenomination` | I3 | Billetes y monedas vigentes (dato sembrado); `Kind` es `CashDenominationKind` (§2.5). |

**Facturación electrónica — plataforma** (`Domain/Entities/ElectronicInvoicing`, I4)

| Tabla | Entidad | Propósito |
|---|---|---|
| `COR_ElectronicEmissionSettings` | `ElectronicEmissionSetting` | Configuración con vigencia: `Mode`, `ChannelCode`, `Environment`, `SoftwareId`, `TestSetId`, `CredentialKey` (sólo referencia), `CredentialVerifiedAt`, `EmailDeliveryBy`, `IsEnabled`, `Reason`. `ChannelCode` es la clave del adaptador en mayúsculas (`SIMULADO`, la del proveedor, `SERVICIO-CENTRAL`), la de `ICanalDeEmisionElectronica.ChannelCode`: se sella en cada documento y forma la clave del Secret. |
| `COR_DianNumberingResolutions` | `DianNumberingResolution` | Resolución: `Kind`, `BacksUpKind`, número y fecha, `Prefix` (≤ 4), `RangeFrom`, `RangeTo`, vigencia, `Environment`, `LastIssuedNumber`, `RowVersion`. |
| `COR_DianResolutionChannels` | `DianResolutionChannel` | Asociación prefijo ↔ canal o software desde una fecha; `TechnicalKey` sólo en factura (`[NoAuditar]`, enmascarada). |
| `COR_ElectronicDocuments` | `ElectronicDocument` | Uno por número fiscal: `SourceModule` + `SourceDocumentPublicId`, `Kind`, `DianDocumentTypeCode`, `ResolutionId`, `Prefix`, `Consecutive`, `Number`, `Environment`, canal sellado, `UniqueCode`/`UniqueCodeKind`, `QrContent`, `Status`, `ContingencyType`, `ContingencyEventId`, fechas, `CurrentVersionId`, `CorrectsDocumentId`, `RejectedBy`, `AttemptCount`, `NextAttemptAt`, `LeaseUntil`, `LeaseOwner`, `TransmissionDeadline`. UK `(Environment, Prefix, Consecutive)` **sin filtro**. |
| `COR_ElectronicDocumentVersions` | `ElectronicDocumentVersion` (H) | Versión: `VersionNumber`, `SourceDocumentPublicId`, `Reason` (`Initial`/`CaseA`/`CaseB`), canónico (adjunto + SHA-256), XML firmado, AttachedDocument, PDF. |
| `COR_ElectronicDocumentTransmissions` | `ElectronicDocumentTransmission` (H) | Un intento contra el canal: operación, canal, solicitante, duración, clave de idempotencia, resultado, códigos, mensajes crudos y traducidos, referencia externa, ApplicationResponse. |
| `COR_DianContingencyEvents` | `DianContingencyEvent` | Evento de contingencia 03 o 04: canal, inicio, fin, detectado por, motivo, estado, plazo, evidencias (adjuntos del dueño `DianContingencyEvent`, T41). |

**Contabilidad — enmienda de la 009** (`Domain/Entities/Accounting/Inventory`, I2)

| Tabla | Entidad | Propósito |
|---|---|---|
| `ACC_InventoryPostingRules` | `InventoryPostingRule` | Matriz: `Operation`, `Role`, dimensiones por código (`AccountingGroupCode`, `WarehouseCode`, `PointOfSaleCode`, `PaymentMeansCode`, `TaxRateCode` + `TaxRate`, `ReasonCode`) y por FK de Core (`BranchId`, `CostCenterId`), `AccountId`, vigencia, `Notes`, `DimensionKey` (UK filtrado con `ValidFrom`). |
| `ACC_InventoryVoucherMappings` | `InventoryVoucherMapping` | Operación (+ código de tipo de documento de inventario opcional) → `VoucherTypeId` (Module/INV) y `CrossDocumentTypeId`. |
| `ACC_InventoryPostings` | `InventoryPosting` (H, `Entities/Accounting/Transactions`) | Recibo único por mensaje procesado (UK `MessagePublicId`): tipo y versión, documento origen y relacionado, fecha de operación, `AccountingDocumentId` (nulo en informativos o valor cero), `BatchPublicId`, usuario de origen, actor. |

**Inventario — catálogo** (`Domain/Entities/Inventory/Catalog`)

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `INV_UnitsOfMeasure` | `UnitOfMeasure` | I1 | Unidad con `AllowedDecimals` (0..4) y `DianUnitCode` (Rec. 20). |
| `INV_ProductCategories` | `ProductCategory` | I1 | Categoría jerárquica (`ParentId`, `Level` ≤ 5). |
| `INV_Brands` | `Brand` | I1 | Marca. |
| `INV_AccountingGroups` | `AccountingGroup` | I1 | Grupo contable (lo traduce la matriz). |
| `INV_Products` | `Product` | I1 | Producto: `Code` (20), `Name`, `Kind`, categoría, marca, unidad base, grupo contable, `TracksLot`/`TracksSerial`/`TracksExpiry`, `Status`, `VatSaleTreatment`, `WithholdingConceptId`, `Reference`, `Weight`, `Volume`, `SearchText`. |
| `INV_ProductUnits` | `ProductUnit` | I1 | Unidad alterna con `Factor` (18,6) y uso (compra/venta). |
| `INV_ProductBarcodes` | `ProductBarcode` | I1 | Código de barras único en la cooperativa, con `ProductUnitId` del empaque. |
| `INV_ProductTaxes` | `ProductTax` | I1 | Producto → `TaxDefinition`/`TaxRate`, tratamiento y `TaxableUnitsPerBaseUnit`. |
| `INV_ProductAccountingGroupChanges` | `ProductAccountingGroupChange` | I1 | Historial del cambio de grupo (fecha efectiva, cantidad y valor): origen de «GrupoContableReclasificado». |
| `INV_ProductComponents` | `ProductComponent` | I6 | Componentes de combo o kit. |
| `INV_VariantAttributes` · `INV_VariantAttributeValues` · `INV_ProductVariantValues` | `VariantAttribute` · `VariantAttributeValue` · `ProductVariantValue` | I6 | Plantillas y variantes. |
| `INV_Lots` · `INV_Serials` | `Lot` · `Serial` | I6 | Lote con vencimiento; serie única en existencia. |

**Inventario — bodegas, existencias y kardex** (`Catalog`/`Warehousing`/`Transactions`/`Projections`)

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `INV_WarehouseTypes` | `WarehouseType` | I1 | Tipo parametrizable con comportamiento fijo `WarehouseBehavior`. |
| `INV_Warehouses` | `Warehouse` | I1 | Bodega de una sucursal (`BranchId` → `COR_Branches`), tipo, `ActivationStatus`, `CutoffDate`, `ActivatedAt/By`. La de tránsito se crea con la primera bodega operativa de la sucursal: su código lo propone el sistema (`TR` + código de la sucursal) y lo puede fijar quien crea (`transitWarehouse { code, name }` opcional en `POST /warehouses`, o una fila de tipo tránsito en la plantilla); no se crea sola fuera de ese momento, y un alta manual de tipo tránsito responde `Inventory.WarehouseType.TransitIsSystem`. |
| `INV_WarehouseLocations` | `WarehouseLocation` | I1 | Ubicación interna; una por defecto en cada bodega. |
| `INV_ReorderPolicies` | `ReorderPolicy` | I1 | Mínimo, máximo y punto de reorden por producto y bodega. |
| `INV_SalesChannels` | `SalesChannel` | I1 | Canal de venta (lo usan tipos de documento, puntos y listas). |
| `INV_KardexEntries` | `KardexEntry` (H, long) | I1 | Hecho de kardex: documento y línea, producto, bodega, ubicación, lote/serie, `OperationDate`, `RegisteredAt`, `Kind`, `Reason`, `QuantityBase` (con signo), `UnitCost`, `TotalCost`, `CostScopeWarehouseId` (0 = cooperativa), `CostMethod`, `ReversesEntryId`, `AffectsEntryId`. |
| `INV_StockBalances` | `StockBalance` | I1 | Proyección: físico y reservado por producto y bodega (UK sin filtro). |
| `INV_StockDetails` | `StockDetail` | I1 | Proyección por producto, bodega, ubicación y lote. |
| `INV_CostStates` | `CostState` | I1 | Proyección de costo por (producto, ámbito): cantidad, valor, promedio, último costo. |
| `INV_CostLayers` | `CostLayer` | I5 | Capas PEPS (proyección). |
| `INV_LayerConsumptions` | `LayerConsumption` (H) | I5 | Consumo de capa por salida. |
| `INV_Reservations` | `Reservation` | I6 | Reserva de un pedido vigente: `QuantityBase`, `ConsumedQuantityBase`, `Status` (`ReservationStatus`), `ReleasedByDocumentId`, `ReleaseReason`. `INV_StockBalances.Reserved` = Σ (`QuantityBase` − `ConsumedQuantityBase`) de las `Active`. Una sola definición: la de la parte 2 de `data-model.md`. |

**Inventario — documentos** (`Documents`; `Purchasing`; `Pos`; `Pricing`)

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `INV_DocumentTypes` | `InventoryDocumentType` | I1 | Tipo parametrizable de una clase: `Code`, `Name`, `Class`, campos obligatorios, `SalesChannelId`, `IsTaxableWithdrawal`, `VatNonDeductible`, `AllowsFutureDate`, `IsActive`. |
| `INV_DocumentTypeWarehouses` | `DocumentTypeWarehouse` | I1 | Bodegas permitidas del tipo. |
| `INV_DocumentSequences` | `DocumentSequence` | I1 | Consecutivo no fiscal por (`DocumentTypeId`, `Prefix`) con vigencia y `NextValue`. |
| `INV_AdjustmentCauses` | `AdjustmentCause` | I1 | Causas de baja y ajuste (semilla: merma, daño, vencimiento, hurto, diferencia de conteo, destrucción, reclamación al transportador). |
| `INV_Documents` | `InventoryDocument` | I1 | Cabecera genérica (T17). UK `(DocumentTypeId, Prefix, Number)` filtrado `[Number] IS NOT NULL AND [FiscalNumberReleased] = 0`. |
| `INV_DocumentLines` | `InventoryDocumentLine` | I1 | Línea genérica: producto, unidad, `Quantity`, `Factor`, `QuantityBase`, `RoundingQuantity`, `UnitPrice`, `ListPrice`, `PriceListId`, descuentos, `UnitCost`, lote/serie, ubicación origen y destino. |
| `INV_DocumentLinks` | `DocumentLink` | I1 | Vínculo entre documentos (`Kind`). |
| `INV_DocumentLineLinks` | `DocumentLineLink` | I1 | Vínculo entre líneas con `QuantityBase` (pendientes se calculan, no se guardan). |
| `INV_DocumentPartySnapshots` | `DocumentPartySnapshot` (H) | I1 | Copia fiscal de la contraparte por versión (la vigente = mayor `Version`; el caso a de FR-066 agrega otra). |
| `INV_DocumentTaxLines` | `DocumentTaxLine` (H) | I1 | Foto del motor tributario: por línea o por documento, tarifa aplicada, base, valor, `Treatment`, explicación JSON. |
| `INV_SupplierInvoiceDetails` | `SupplierInvoiceDetail` | I1 | 1:1 con factura o nota del proveedor: prefijo, número, CUFE, emisión, vencimiento, forma de pago, electrónica. UK filtrados entre no anuladas. |
| `INV_SupplierInvoiceEvents` | `SupplierInvoiceEvent` | I1 | Estado de 030 y 032 por factura a crédito: `EventCode`, `Status`, fecha, quién, fuente, CUDE, `ElectronicDocumentPublicId` (I5). |
| `INV_TransferDiscrepancies` | `TransferDiscrepancy` | I1 | Faltante o sobrante pendiente de un traslado y su resolución. |
| `INV_CountSnapshotLines` · `INV_CountCaptures` | `CountSnapshotLine` · `CountCapture` | I1 | Foto teórica del conteo y capturas por contador. |
| `INV_PurchaseMatchLines` | `PurchaseMatchLine` | I5 | Cruce a tres vías por línea; retenidas para aprobación. Lleva `Status` (`PurchaseMatchStatus`) y `Reasons` (nuevo). |
| `INV_LandedCostAllocations` | `LandedCostAllocation` | I5 | Reparto de costos adicionales por línea recibida. |
| `INV_DocumentLineDiscounts` | `DocumentLineDiscount` | I3 | Descuento por línea: `Source`, porcentaje, valor, aprobación, `PromotionId` (I6). |
| `INV_DocumentPayments` | `DocumentPayment` | I3 | Pago por medio: `PaymentMeansId`, `Amount`, `Direction`, entregado y vueltas, referencia, autorización, datáfono, lote, copias del medio, `Last4`, `CashSessionId`, condiciones de crédito, `PendingValidation`. |
| `INV_VoucherRedemptions` | `VoucherRedemption` | I3 | Uso de bono; UK filtrado `(PaymentMeansId, NormalizedNumber)` con `Status = Active`. |
| `INV_CashMovementDetails` | `CashMovementDetail` | I3 | 1:1 con el documento de movimiento de caja: sesión, `Kind`, medio origen, destino, caja destino, valor, denominaciones. |
| `INV_CashDocumentLines` | `CashDocumentLine` | I3 | Líneas por medio de pago de «Arqueo con diferencia» (signo, valor, `Treatment`, cajero). |

Columnas clave de `INV_Documents` (las demás las define `data-model.md`): `Class`, `DocumentTypeId`,
`Prefix`, `Number` (nulo en borrador), `Status`, `OperationDate` (fecha local), `ConfirmedAt`,
`CreatedByUserId` y `ConfirmedByUserId` (`SEC_Users.Id`, T6), `WarehouseId`, `DestinationWarehouseId`,
`TransitWarehouseId`, `BranchId`, `CostCenterId`, `CounterpartyPersonId`, `SalespersonId`,
`SalesChannelId`, `PointOfSaleId`, `CashRegisterId`, `CashSessionId`, `IsSuspended`/`SuspendedAt`/
`SuspendedLabel` (borrador POS), `ExternalReference`, `Currency`, `ExchangeRate`, `PostingMode` (sellado),
`Reason`, `VoidsDocumentId`, `VoidedByDocumentId`, `FiscalNumberReleased`,
`OperationMunicipalityDaneCode` (compras, T24), totales `Subtotal`, `DiscountTotal`, `TaxTotal`,
`WithholdingTotal`, `Total`, `AmountDue` (T26). Las columnas de venta `ValidUntil`, `DueDate`,
`ReturnsGoods`, `IsFullReversal` y `CorrectionConceptCode` (y `ListPriceIncludesTaxes` en
`INV_DocumentLines`) **nacen con la tabla en I1** (`InventarioComercialNucleo`), aunque se usen desde I3
o I6. No lleva clave de idempotencia (T13) ni copia fiscal en columnas (T52). El cierre del saldo de una
orden de compra agrega en I5 `BalanceClosedAt`, `BalanceClosedByUserId` y `BalanceClosedReason` (nuevo;
data-model §9.8).

**Inventario — POS, caja, precios** (`Pos`, `Pricing`)

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `INV_PointsOfSale` | `PointOfSale` | I3 | Punto: sucursal, canal, `PosEnabled`, bodega por defecto. |
| `INV_CashRegisters` | `CashRegister` | I3 | Caja del punto con su bodega e impresión. |
| `INV_CashRegisterDocumentTypes` | `CashRegisterDocumentType` | I3 | Tipo de documento de la caja por `Role` (`CashRegisterDocumentRole`, seis roles, §2.5); UK `(CashRegisterId, Role)`. `PosSaleContingency` exige una resolución con `BacksUpKind = PosEquivalent`; `InvoiceContingency`, una con `BacksUpKind = Invoice`. |
| `INV_PaymentMeansPointsOfSale` · `INV_PaymentMeansChannels` · `INV_PaymentMeansDocumentTypes` | `PaymentMeansPointOfSale` · `PaymentMeansChannel` · `PaymentMeansDocumentType` | I3 | Dónde se ofrece cada medio (con «todos» en el medio). |
| `INV_CashSessions` | `CashSession` | I3 | Sesión (turno): caja, cajero (`SEC_Users.Id` + copia de persona), `OperatingDate` local, base, estado. UK filtrados por caja y por cajero con `Status = Open`. |
| `INV_CashCounts` · `INV_CashCountLines` · `INV_CashCountDenominations` · `INV_CashCountTerminalBatches` · `INV_CashCountReferenceChecks` | `CashCount` · `CashCountLine` · `CashCountDenomination` · `CashCountTerminalBatch` · `CashCountReferenceCheck` | I3 | Arqueo por sesión y medio: esperado, contado, diferencia, tolerancia, motivo, tratamiento; denominaciones, lotes de datáfono, cotejo de referencias. |
| `INV_DayCloses` · `INV_DayCloseLines` | `DayClose` · `DayCloseLine` | I3 | Cierre del día por punto (UK `(PointOfSaleId, OperatingDate)`), con detalle por medio, adquirente y datáfono. |
| `INV_PriceLists` · `INV_PriceListItems` | `PriceList` · `PriceListItem` | I3 | Lista con ámbito (persona, segmento, canal, sucursal), `IncludesTaxes`, vigencia y `ScopeKey`; precio por (lista, producto, unidad): UK `(PriceListId, ProductId, UnitId)`. |
| `INV_DiscountCaps` | `DiscountCap` | I3 | Tope por rol (línea y documento) con vigencia. |
| `INV_Promotions` · `INV_PromotionScopes` · `INV_PromotionTiers` | `Promotion` · `PromotionScope` · `PromotionTier` | I6 | Promociones. |

**Inventario — períodos, puesta en marcha, seguridad** (`Periods`, `GoLive`, `Security`)

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `INV_Setup` | `InventorySetup` | I1 | Fila única: `StartDate` (primer día del mes del primer corte), `LastClosedDate` (bloqueo compartido al confirmar, exclusivo al cerrar). La crea el primer registro de una fecha de corte o `ImportOpeningBalanceCommand` si aún no existe. |
| `INV_Periods` | `InventoryPeriod` | I1 | Mes: estado, cierre, reapertura, motivo, remisiones sin facturar aceptadas. |
| `INV_PeriodClosingBalances` | `PeriodClosingBalance` | I1 | Valorizado fijado por producto × bodega con grupo; versión y `Superseded` al reabrir. |
| `INV_WarehouseActivations` | `WarehouseActivation` | I1 (usa I2) | Comparación de la activación por grupo y conjunto de cuentas, diferencia, aceptación y motivo. |
| `INV_LegacyFigures` | `LegacyFigure` | I1 | Cifras de SOLIDO importadas (fecha, códigos tal como vienen, Ids resueltos, cantidad, valor, lote de importación). |
| `INV_UserWarehouseScopes` | `UserWarehouseScope` | I1 | Alcance usuario → bodega (`IsDefault`). |
| `INV_UserPointOfSaleScopes` | `UserPointOfSaleScope` | I3 | Alcance usuario → punto de venta. |
| `INV_Salespeople` | `Salesperson` (se conserva) | — | Rol vendedor; el UK pasa a filtrado `[IsDeleted] = 0` (I1). |

### 2.3 Columnas nuevas en tablas existentes

| Tabla | Columnas | E |
|---|---|---|
| `COR_People` | `IsVatResponsible`, `IsSelfWithholder`, `IsVatWithholdingAgent`, `IsSimpleTaxRegime`, `IsIncomeTaxFiler`, `IsObligatedToInvoice` (bool, aditivas; se reutilizan `IsLargeContributor`, `WithholdingExempt`, `IcaWithholdingExempt`, `CiiuCode`) | I1 |
| `COR_Cities` | `DaneCode` (5, único filtrado) + semilla DIVIPOLA | I1 |
| `COR_Branches` | `MunicipalityDaneCode` (5) | I1 |
| `ACC_AccountTaxRates` | `Rate` pasa de (9,4) a (9,6) (ampliación, no destructiva) | I2 |
| `HabeasDataConsent.Action` | nuevo valor de texto `Declined` (sin migración) | I1 |

### 2.4 Tablas retiradas, conservadas y no tocadas

- **Retiradas** (migración destructiva `RetiroDelInventarioHeredado`, I1): `INV_CommissionParameters`,
  `INV_CommissionPriceParams`, `INV_DiscountTypes`, `INV_Discounts`, `INV_Documents`, `INV_Invoices`,
  `INV_Transactions`, `INV_TransactionTypes`, `INV_Locations`, `INV_OrderDocuments`,
  `INV_OrderTransactions`, `INV_PhysicalInventory`, `INV_Prices`, `INV_PriceListTypes`,
  `INV_PrimaryGroups`, `INV_ProductAccounts`, `INV_Products`, `INV_ProductGroups`, `INV_SalesPoints`,
  `INV_SecondaryGroups`, `INV_Shifts`, `INV_VatAccounts`, `INV_Warehouses` (23). Con ellas salen sus 23
  entidades, 65 archivos de `Application/Inventory` (salvo `Salespeople`), 17 archivos de
  `Endpoints/Inventory`, `InventoryReportsEndpoints` actual, `InventoryValuationReport`, 20 páginas,
  `NavMenu` 94-111, tarjetas de `CentroReportes`, temas de `ManualCatalogo` y el uso en
  `IdentitySeedData`. `AccountReferenceFinder` deja de mirar `ProductAccounts`/`VatAccounts` en I1.
- **Nombres reutilizados después del retiro** (nuevas tablas, otro esquema): `INV_Documents`,
  `INV_Products`, `INV_Warehouses`. El retiro va en su propio commit y su propia migración, antes
  (T4).
- **Conservada**: `INV_Salespeople`.
- **No se tocan ni se usan** (nombres que confunden): `COR_PaymentMethods` (`sys_forpago`),
  `COR_Sequences` (`sys_consecu`), `DEB_PosTerminals`, `COR_Companies.Dian*`. Su retiro no es de esta
  feature.

### 2.5 Enumeraciones (tipo en inglés; valores en inglés; la pantalla traduce)

En la base toda enumeración se guarda como `int` con su valor numérico (una sola regla en las dos partes
de `data-model.md`; ninguna columna de enum es `tinyint`). La FK a la unidad de medida se llama siempre
`UnitId`.

`Domain/Enums/Inventory/InventoryEnums.cs`
- `ProductKind` { Inventoriable=1, Service=2, Combo=3, Kit=4, Template=5, Variant=6 }
- `ProductStatus` { Active=1, Inactive=2, Blocked=3 }
- `WarehouseBehavior` { Operational=1, Transit=2 }
- `WarehouseActivationStatus` { NotActivated=0, Active=1 }
- `ProductUnitUsage` { Purchase=1, Sale=2, Both=3 } **(nuevo, T198)**: enum del cuerpo de `/products/{id}/units` y de la
  plantilla (contracts/api.md §17.1); en la base son las marcas `UsedForPurchase`/`UsedForSale` de `INV_ProductUnits`.
- `DocumentClass` { PurchaseRequest=1, PurchaseOrder=2, PurchaseReceipt=3, SupplierInvoice=4,
  SupplierNote=5, SupportDocument=6, SupportDocumentAdjustmentNote=7, LandedCost=8, SupplierReturn=9,
  PositiveAdjustment=10, NegativeAdjustment=11, InternalConsumption=12, WriteOff=13, Assembly=14,
  OpeningBalance=15, TransferDispatch=16, TransferReceipt=17, LocationMove=18, PhysicalCount=19,
  CostAdjustment=20, CashMovement=21, CashCountDifference=22, SalesQuote=23, SalesOrder=24,
  Shipment=25, SalesInvoice=26, SalesInvoiceFromShipments=27, PosEquivalentDocument=28,
  NonElectronicSalesReceipt=29, NonElectronicSalesNote=30, CreditNote=31, PosAdjustmentNote=32,
  DebitNote=33, Voiding=34 }
- `DocumentClassGroup` { Purchases=1, Adjustments=2, Transfers=3, Counts=4, Sales=5, Cash=6,
  OpeningBalance=7, Costing=8 } — decide ruta y familia de permisos (T17).
- `DocumentStatus` { Draft=0, PendingApproval=1, Confirmed=2, Voided=3, Discarded=4 }
- `PostingMode` { Online=1, Batch=2, NotPosted=3 }
- `PostingGranularity` { PerDocument=1, Summarized=2 }
- `BatchScheduleKind` { Daily=1, CashSessionClose=2, PeriodClose=3 }
- `PostingChain` { None=0, Purchases=1, Sales=2, Transfers=3 }
- `KardexEntryKind` { Entry=1, Exit=2, CostAdjustment=3 }
- `KardexReason` { Normal=1, Retroactive=2, PriceDifference=3, LandedCost=4, NegativeRegularization=5,
  VoidDifference=6, RoundingResidue=7, MethodChange=8 }
- `CostMethod` { WeightedAverage=1, Fifo=2 } · `CostScope` { Cooperative=1, Warehouse=2 }
- `InventoryPeriodStatus` { Open=1, Closed=2 }
- `CountKind` { Total=1, Cyclic=2 } · `CountScope` { All=0, Category=1, Location=2, Selection=3, AbcClass=4 }
- `TransferDiscrepancyKind` { Shortage=1, Surplus=2 } · `TransferDiscrepancyResolution` { ReturnToOrigin=1,
  WriteOffFromTransit=2, LateReceipt=3, SurplusAdjustment=4 }
- `DocumentLinkKind` { Voids=1, FromOrder=2, FromShipment=3, NoteOf=4, ReceiptOf=5, InvoiceOfReceipt=6,
  ReturnOf=7, DispatchOf=8, CountAdjustmentOf=9, ReplacementOf=10, LandedCostOf=11 } — cerrado: pedido
  ← cotización es `FromOrder`, remisión ← pedido es `DispatchOf`; no existe `FromQuote`.
- `CashSessionStatus` { Open=1, Closed=2 }
- `CashMovementKind` { WithdrawalToSafe=1, WithdrawalToRegister=2, WithdrawalForDeposit=3, BaseIncome=4,
  ReclassificationBetweenMeans=5 } · `CashMovementDestination` { Safe=1, Register=2, Deposit=3 }
- `CashDifferenceTreatment` { Surplus=1, ShortageToCashier=2, ShortageToExpense=3 }
- `CashRegisterDocumentRole` { PosSale=1, InvoiceOnRequest=2, PosAdjustmentNote=3, InvoiceCreditNote=4,
  PosSaleContingency=5, InvoiceContingency=6 } — las notas no electrónicas van en `PosAdjustmentNote`;
  cada rol de venta tiene su rol de contingencia (FR-058, FR-067).
- `CashDenominationKind` { Bill=1, Coin=2 } · `ReservationStatus` { Active=1, Consumed=2, Released=3, Expired=4 }
- `PaymentDirection` { Received=1, Refunded=2 } · `DiscountSource` { Manual=1, Promotion=2 }
- `VoucherRedemptionStatus` { Active=1, Released=2 }
- `PromotionKind` { Percent=1, Amount=2, BuyNPayM=3, QuantityPrice=4, BundlePrice=5 }
- `SupplierInvoiceEventCode` { Receipt030=30, GoodsReceived032=32 } · `SupplierInvoiceEventStatus`
  { Pending=0, RegisteredExternally=1, Emitted=2, Rejected=3, NotApplicable=4 }
- `PurchaseMatchStatus` { Held=1, Approved=2, Rejected=3 } (I5, nuevo; data-model §9.6; lo agrega US13, T776)
- `CreditOrigin` { ProvisionalCredit=1, LendingNoResponse=2, Validated=3 } · `PromotionScopeKind` { Product=1,
  Category=2, Segment=3, Channel=4 } · `DayCloseStatus` { Closed=1, Reopened=2 } **(nuevos en §2.5; data-model §26;
  creados en T025)**

`Domain/Enums/Core/TaxEnums.cs` y `PaymentEnums.cs`
- `TaxKind` { Iva=1, Inc=2, ReteFuente=3, ReteIva=4, ReteIca=5, Ica=6, Other=99 }
- `TaxCalculationForm` { PercentOfBase=1, PercentOfTax=2, AmountPerUnit=3 }
- `TaxTreatment` { Generated=1, Deductible=2, AddedToCost=3, WithholdingApplied=4, WithholdingSuffered=5 }
- `TaxAppliesTo` { Purchases=1, Sales=2, Both=3 } · `VatSaleTreatment` { Taxed=1, Exempt=2, Excluded=3 }
- `PaymentMeansClass` { Cash=1, CreditCard=2, DebitCard=3, AssociateCredit=4, CustomerCredit=5,
  BankDeposit=6, Transfer=7, Voucher=8, Check=9, Other=99 }
- `CashCountMethod` { PhysicalCount=1, VoucherTotal=2, ByReference=3, None=4 }
- `PaymentReferenceKind` { Approval=1, Receipt=2, Deposit=3, VoucherNumber=4, CheckNumber=5, Other=99 }
- `CardKind` { Credit=1, Debit=2, Both=3 }

`Domain/Enums/Integration/IntegrationEnums.cs`
- `IntegrationMessageKind` { Business=1, Informational=2 }
- `DeliveryStatus` { Pending=0, InBatch=1, Processed=2, Rejected=3, NotApplicable=4, ValidationFailed=5 }
  — `Rejected` → `ReprocessMessagesCommand` → `InBatch` (lote `Reprocess`); `NotApplicable` →
  `SendNotApplicableMessagesCommand` → `InBatch` (lote `SendNotApplicable`); `Processed` (Lending) →
  validación negativa → `ValidationFailed`.
- `DeliveryMode` { Online=1, Batch=2, NotPosted=3, Always=4 }
- `BatchTrigger` { Scheduled=1, CashSessionClose=2, PeriodClose=3, Manual=4, Reprocess=5, SendNotApplicable=6 }
- `BatchStatus` { Requested=0, Running=1, Completed=2, CompletedWithRejections=3, Empty=4 }
- `ActorKind` { Person=1, Process=2 } · `ExecutionChannel` { Web=1, App=2, Pos=3, Process=4 }
- `PrevalidationOutcome` { Postable=1, NoResponse=2, NotApplicable=3 }
- `MessageOriginKind` { Document=1, Operation=2 } · `DeliveryAttemptOutcome` { Processed=1, AlreadyProcessed=2,
  Rejected=3, Retry=4 } **(nuevos en §2.5; data-model §26; creados en T039)**
- Destinos (constantes, no enum): `IntegrationDestinations.Accounting = "Accounting"`,
  `IntegrationDestinations.Lending = "Lending"`.

`Domain/Enums/ElectronicInvoicing/ElectronicInvoicingEnums.cs` (+ `Domain/Enums/Dian/DianEnvironment.cs`,
trasladado desde `Enums/Payroll` con los mismos valores `Production=1, Testing=2`)
- `EmissionMode` { TechnologyProvider=1, OwnSoftware=2 }
- `ElectronicDocumentKind` { Invoice=1, CreditNote=2, DebitNote=3, PosEquivalent=4, PosAdjustmentNote=5,
  SupportDocument=6, SupportDocumentAdjustmentNote=7, RadianEvent030=8, RadianEvent032=9 }
- `ElectronicDocumentStatus` { Pending=0, Sent=1, Validated=2, ValidatedWithNotices=3, Rejected=4,
  IssuerContingency=5, DianContingency=6, CancelledWithoutReplacement=7 }
- `ContingencyType` { Issuer03=3, Dian04=4 } · `RejectedBy` { Dian=1, Channel=2 }
- `ResolutionKind` { Invoice=1, PosEquivalent=2, SupportDocument=3, Contingency=4 }
- `ChannelOutcome` { Validated=1, ValidatedWithNotices=2, Rejected=3, InProcess=4, NotFound=5,
  DianUnavailable=6, ChannelUnavailable=7, InvalidData=8 }
- `TransmissionOperation` { Emit=1, TransmitContingency=2, QueryStatus=3, Event=4, Download=5 }
- `DocumentVersionReason` { Initial=1, CaseA=2, CaseB=3 } · `EmailDeliveryBy` { Erp=1, Channel=2 }

`Domain/Enums/Approvals`, `Alerts`, `Parameters`
- `ApprovalRequestStatus` { Pending=0, Approved=1, Rejected=2, Cancelled=3 }
- `ApprovalDecisionKind` { Approve=1, Reject=2 } · `ApprovalMethod` { OwnSession=1, InPersonPasskey=2, InPersonTotp=3 }
- `ApprovalSubjects` (constantes de texto, no enum): `DocumentConfirmation`, `DiscountOverCap`,
  `ProvisionalCredit`, `TransferDiscrepancy`, `PurchaseMatchException` (I5)
- `AlertStatus` { Pending=0, Attended=1 } · `AlertSeverity` { Info=1, Warning=2, Critical=3 }
- `AlertChannels` [Flags] { InApp=1, Email=2 }
- `ParameterScopeKind` { None=0, Warehouse=1, DocumentType=2, PointOfSale=3, CashRegister=4, ThirdPartyKind=5 }

### 2.6 Mensajes (T7, T8)

Todos nacen en versión 1. `Type` es la cadena guardada; el record vive en
`Application/Common/Integration/Contracts/Inventory/<Type>V1.cs`. Sobre común (`IntegrationEnvelopeV1`):
`MessageId`, `Type`, `Version`, `Kind`, `OriginModule = "INV"`, `Origin` { `Kind` (Document|Operation),
`DocumentClass`, `DocumentTypeCode`, `Number`, `PublicId`, `OperationDate`, `FiscalUniqueCode?` },
`Related?` { `PublicId`, `DocumentClass`, `Number` }, `ChainRootPublicId`, `BranchPublicId`,
`CostCenterPublicId?`, `WarehouseCode?`, `PersonPublicId?`, `Currency = "COP"`, `ExchangeRate = 1`,
`OriginUser` { `CentralUserId`, `Name` }, `EmittedAt` (UTC), `originEventKey`. Propiedades en inglés, JSON
camelCase, enums como texto, decimales exactos, tarifas como **fracción**.

`originEventKey` (columna `OriginEventKey`; con `OriginPublicId` y `Type` forma el único del mensaje y la
unidad de contabilización de T11) toma estos valores y ningún otro (detalle en `contracts/mensajes.md`
§10.1): `Confirmation` (todo mensaje de un documento); `Confirmation:{affectedDocumentPublicId:N}` (cada
parte de `AjusteDeCostoReconocido`); `Confirmation:{paymentPublicId:N}` (`VentaACreditoRegistrada` y
`AjusteDeVentaACredito`); `Close:{closingVersion}` · `Reopen:{closingVersion}` (período);
`Reclassification` (`GrupoContableReclasificado`).

| `Type` | Clase(s) que lo emiten (FR-036) | Destino · `Kind` | Contenido | Operación(es) de la matriz | Comprobante por defecto |
|---|---|---|---|---|---|
| `VentaFacturada` | SalesInvoice, SalesInvoiceFromShipments, PosEquivalentDocument, NonElectronicSalesReceipt | Accounting · Business | base, descuentos, impuestos y retenciones por impuesto y tarifa, pagos por medio | `Venta` | FV |
| `CostoDeVentaReconocido` | SalesInvoice (directa o desde pedido), PosEquivalentDocument, NonElectronicSalesReceipt, Shipment | Accounting · Business | cantidades y costo por grupo y bodega | `CostoDeVenta` | FV (SI por tipo si es remisión) |
| `CompraRecibida` | PurchaseReceipt | Accounting · Business | cantidades y costo | `Compra` | EI |
| `FacturaProveedorRegistrada` | SupplierInvoice, SupplierNote (con signo), SupportDocument, SupportDocumentAdjustmentNote | Accounting · Business | base, descuentos, IVA descontable y al costo, retenciones | `FacturaProveedor` | CP |
| `AjusteInventarioAprobado` | PositiveAdjustment, NegativeAdjustment, InternalConsumption (retiro gravado: + base e IVA), WriteOff, Assembly, sobrante de traslado | Accounting · Business | cantidades y costo (+ base e IVA) | `AjustePositivo`, `AjusteNegativo`, `ConsumoInterno`, `RetiroGravado`, `Baja`, `Ensamble` | EI / SI |
| `TrasladoDespachado` | TransferDispatch | Accounting · Business | cantidades y costo, bodega origen y tránsito | `DespachoTraslado` | TR |
| `TrasladoRecibido` | TransferReceipt | Accounting · Business | cantidades y costo, tránsito y destino | `RecepcionTraslado` | TR |
| `DevolucionRegistrada` | SupplierReturn; CreditNote, PosAdjustmentNote o NonElectronicSalesNote con devolución | Accounting · Business | cantidades y costo | `DevolucionAProveedor` / `DevolucionDeCliente` | SI / NV |
| `DocumentoAnulado` | Voiding (FR-006, FR-066 b y c) | Accounting · el `Kind` del original | contenido del original con signo contrario | las del original, con reglas a la fecha del original | el del original |
| `AjusteDeCostoReconocido` | CostAdjustment, SupplierInvoice/SupplierNote (diferencia de precio), LandedCost, SupplierReturn y Voiding de entradas ya promediadas; **uno por documento afectado** | Accounting · Business | sólo diferencia de costo, dividida en existencia y vendido | `AjusteDeCosto` | AC |
| `NotaCreditoEmitida` | CreditNote, PosAdjustmentNote, NonElectronicSalesNote | Accounting · Business | base, descuentos, impuestos, retenciones, reintegros por medio; marca de anulación total | `NotaCredito` | NV |
| `NotaDebitoEmitida` | DebitNote (I6) | Accounting · Business | base, descuentos, impuestos, retenciones | `NotaDebito` | NV |
| `GrupoContableReclasificado` | operación `ChangeProductAccountingGroupCommand` (FR-027) | Accounting · Business (modo general) | cantidad y valor que pasan de grupo, por bodega | `Reclasificacion` | AC |
| `MovimientoDeCajaRegistrado` | CashMovement | Accounting · Business | tipo, medio origen, destino, punto, sesión, valor | `MovimientoDeCaja` | CJ |
| `DiferenciaDeArqueoAprobada` | CashCountDifference | Accounting · Business | una línea por medio: signo, valor, tratamiento, cajero (persona), aprobador | `DiferenciaDeArqueo` | CJ |
| `SaldoInicialCargado` | OpeningBalance | Accounting · Informational | cantidad y valor por grupo y bodega | — (sin comprobante) | — |
| `PeriodoInventarioCerrado` | operación `CloseInventoryPeriodCommand` | Accounting · Informational | valorizado fijado por grupo y bodega | — | — |
| `PeriodoInventarioReabierto` | operación `ReopenInventoryPeriodCommand` | Accounting · Informational | período y motivo | — | — |
| `VentaACreditoRegistrada` | toda venta con pago de clase `AssociateCredit` o `CustomerCredit` | Lending · Business (`DeliveryMode.Always`) | lo de FR-062: persona, tipo de tercero, pago de crédito, medio, valor, plazo, cuotas, periodicidad, vencimientos, línea sugerida, `PendingValidation`, `Origin` (ProvisionalCredit / LendingNoResponse / Validated), aprobación, `AccountsReceivableRecordedBy` sellado | — | — |
| `AjusteDeVentaACredito` | notas, devoluciones y anulaciones (incluidos casos b y c) de una venta a crédito | Lending · Business (`Always`) | `AdjustmentClass` { CreditNote, DebitNote, Return, Voiding, VoidingByDianRejection, Replacement }, valor con signo, `OriginalMessageId`, documentos original y del ajuste, mismo sello | — | — |

Reglas: la anulación del saldo inicial emite `DocumentoAnulado` **informativo** (hereda el `Kind`). Un
documento fiscal validado nunca emite `DocumentoAnulado` (FR-066). `CostoDeVentaReconocido` y
`DevolucionRegistrada` no llevan base ni impuestos; los de venta no llevan costo (FR-036).

### 2.7 Catálogos fijos de la matriz (dato, en `Application/Accounting/Inventory/Reglas`)

- `OperacionesDeInventario`: `Venta`, `CostoDeVenta`, `Compra`, `FacturaProveedor`,
  `DevolucionAProveedor`, `DevolucionDeCliente`, `NotaCredito`, `NotaDebito`, `AjustePositivo`,
  `AjusteNegativo`, `ConsumoInterno`, `RetiroGravado`, `Baja`, `Ensamble`, `DespachoTraslado`,
  `RecepcionTraslado`, `AjusteDeCosto`, `Reclasificacion`, `MovimientoDeCaja`, `DiferenciaDeArqueo`.
- `RolesDeCuenta`: `Inventario`, `Transito`, `Costo`, `Ingreso`, `Descuento`, `Devolucion`,
  `Impuesto`, `Retencion`, `MedioDePago`, `MercanciaPorFacturar`, `CuentaPorPagar`, `Contrapartida`,
  `CajaDestino`, `Sobrante`, `Faltante`, `GastoDeArqueo`, `Redondeo`.
- Qué rol exige qué dimensión: `Impuesto`/`Retencion` → `TaxRateCode` + `TaxRate`; `MedioDePago` →
  `PaymentMeansCode` (opcional `PointOfSaleCode`); `Inventario`/`Costo`/`Ingreso`/`Transito` →
  `AccountingGroupCode` (opcional `WarehouseCode`); `Sobrante`/`Faltante`/`GastoDeArqueo` → `ReasonCode`
  (tratamiento). Especificidad por pesos: bodega o punto 16, centro 8, sucursal 4, grupo 2.
- `ReasonCode` (`nvarchar(40)`) en `DiferenciaDeArqueo`: `Surplus`, `ShortageToCashier`,
  `ShortageToExpense`; en `Baja`/`AjusteNegativo`: el código de `INV_AdjustmentCauses`; rol `CajaDestino`
  de `MovimientoDeCaja`: la otra punta (`CashMovementDestination`: `Safe`, `Deposit`); rol `Contrapartida`
  en `AjusteDeCosto`: el nombre de `KardexReason` (p. ej. `NegativeRegularization`).

### 2.8 Parámetros con vigencia (`COR_ParameterVersions`; T21)

`Module` · `Key` · valores · **defecto seguro** · ámbitos · entrega. Un valor no admitido es un error
nombrado (`Parameters.ValueNotAllowed`), nunca el defecto. «Sellado» = se fija en el documento al
confirmar.

| Module | Key | Valores | Defecto | Ámbitos | E |
|---|---|---|---|---|---|
| INV | `Costeo.Metodo` | `PromedioPonderado`, `Peps` | PromedioPonderado | None (sólo al inicio de un período sin movimientos posteriores) | I1 (Peps I5) |
| INV | `Costeo.Ambito` | `Cooperativa`, `Bodega` | Cooperativa | None (misma regla) | I1 |
| INV | `Costeo.RetroactivosPermitidos` | bool | false (no rige para el saldo inicial de una bodega `NotActivated` ni para los ajustes de un conteo aprobado: T18) | None | I5 |
| INV | `Costeo.RetroactivosDiasMaximos` | int | 0 | None | I5 |
| INV | `Existencias.StockNegativoPermitido` | bool | false | None, Warehouse | I1 |
| INV | `Redondeo.Montos` | `Centavo`, `Peso` | Centavo | None | I1 |
| INV | `Redondeo.Residuo` | `MayorValor`, `UltimaLinea` | MayorValor | None | I1 |
| INV | `Conteo.BloquearMovimientos` | bool | true | None, Warehouse | I1 |
| INV | `Conteo.FechaDelAjuste` | `Foto`, `Aprobacion` | Foto | None | I1 |
| INV | `Conteo.ToleranciaReconteoPorcentaje` · `Conteo.ToleranciaReconteoUnidades` | decimal | 0 · 0 | None, Warehouse | I1 |
| INV | `Compras.DiasAlertaEventosRadian` | int | 3 | None | I1 |
| INV | `Compras.ToleranciaCantidadPorcentaje` · `…CantidadValor` · `…PrecioPorcentaje` · `…PrecioValor` | decimal | 0 | None | I5 |
| INV | `Compras.ReglaDeTolerancia` | `AmbasCondiciones`, `CualquieraDeLas` | AmbasCondiciones | None | I5 |
| INV | `Contabilidad.ModoDePaso` | `EnLinea`, `PorLotes`, `NoPasa` | EnLinea (sellado) | None, DocumentType (por cadena) | I1 (efecto I2) |
| INV | `Contabilidad.Granularidad` | `PorDocumento`, `Resumido` | PorDocumento | None, DocumentType | I2 |
| INV | `Contabilidad.DisparadorDeLote` | `HoraDiaria`, `CierreDeTurno`, `CierreDePeriodo` | HoraDiaria | None, DocumentType | I2 |
| INV | `Contabilidad.HoraDeLote` | `HH:mm` hora de Colombia | 23:00 | None, DocumentType | I2 |
| INV | `Contabilidad.PoliticaSinRespuesta` | `ConfirmarConPendiente`, `Bloquear` | ConfirmarConPendiente | None | I2 |
| INV | `Contabilidad.ValidacionPreviaSegundos` | int | 3 | None | I2 |
| INV | `Ventas.BajoCosto` | `Alertar`, `Bloquear` | Alertar | None | I3 |
| INV | `Ventas.PersonaInactivaDeContado` | `Permitir`, `Bloquear` | Permitir | None | I3 |
| INV | `Ventas.LoteVencido` | `Bloquear`, `Advertir` | Bloquear | None | I6 |
| INV | `Ventas.ReservaDiasVencimiento` | int | 15 | None | I6 |
| INV | `Ventas.RemisionDiasMaximosSinFacturar` | int | 30 | None | I6 |
| INV | `Caja.BaseModo` | `FondoFijo`, `BaseDelDia` | FondoFijo | None, CashRegister | I3 |
| INV | `Caja.TratamientoFaltante` | `Gasto`, `CargoAlCajero` | Gasto | None, PointOfSale | I3 |
| INV | `Caja.ArqueoCiego` | bool | false | None, PointOfSale | I3 |
| INV | `Caja.UnaSesionPorCajero` | bool | true | None | I3 |
| INV | `Cartera.CuentaPorCobrarRegistradaPor` | `Contabilidad`, `Cartera` | Contabilidad (sellado; forzado mientras IC pendiente) | None | I3 |
| INV | `Cartera.IntegracionHabilitadaDesde` | fecha o vacío | vacío = IC pendiente | None | IC |
| INV | `Cartera.PoliticaSinRespuesta` | `Bloquear`, `PermitirConAprobacion` | Bloquear (sólo con IC; en el provisional rige PermitirConAprobacion) | None, ThirdPartyKind | IC |
| INV | `Cartera.ConsultaSegundos` | int | 5 | None | IC |
| INV | `Informes.DeterioroPorcentajeGastosVenta` | decimal (fracción) | 0 | None | I3 |
| INV | `Informes.DiasSinMovimiento` · `Informes.DiasProximoAVencer` · `Informes.UmbralesAbc` | int · int · texto | 90 · 30 · `80/15/5` | None | I6 |
| INV | `Informes.TopeFaltantesPorcentaje` | decimal (fracción), con `LegalSource` | 0 | None | I6 (vista `shrinkage-cap`, FR-086) |
| TAX | `Tributario.ResponsableIva` | bool | true | None | I1 |
| TAX | `Tributario.GranContribuyente` · `.AgenteRetencionIva` · `.Autorretenedor` | bool | false | None | I1 |
| TAX | `Tributario.RegimenTributarioEspecial` | bool | true | None | I1 |
| TAX | `Tributario.RedondeoUvtAPesos` | `Peso`, `Centena`, `Mil` | Peso | None | I1 |
| EINV | `Dian.ObligadaAFacturar` | bool | true | None | I3 (lo lee la guardia desde I3) |
| EINV | `Dian.EsperaMaximaPosSegundos` | int | 15 | None, PointOfSale | I4 |
| EINV | `Dian.PlazoContingenciaHoras` | int, con `LegalSource` | 48 | None | I4 |
| EINV | `Dian.AlertaHorasAntesDelPlazo` | int | 6 | None | I4 |
| EINV | `Dian.MinutosAlertaSinValidar` | int | 10 | None | I4 |
| EINV | `Dian.UmbralFallasCircuito` | int | 3 | None | I4 |
| EINV | `Dian.AvisoResolucionPorcentaje` · `Dian.AvisoResolucionDias` | decimal · int | 0.90 · 30 | None | I4 |
| EINV | `Dian.EntregaCorreo` | `Erp`, `Canal` | Erp | None | I4 |
| EINV | `DocumentoSoporte.Generacion` | `PorOperacion`, `Semanal` | PorOperacion | None | I4 |

Fuera de `COR_ParameterVersions` pero también con vigencia (FR-012): prefijos y consecutivos
(`INV_DocumentSequences`), políticas de aprobación (`COR_ApprovalPolicies`), montos máximos
(`SEC_PermissionAmountLimits`), impuestos (`COR_TaxRates`), listas de precios (`INV_PriceLists`), topes
de descuento (`INV_DiscountCaps`), tolerancia de arqueo por medio (`COR_PaymentMeans.ToleranceAmount`:
cambiarla exige motivo y se copia en cada línea de arqueo al usarla, §2.2),
alertas (`COR_AlertTypes`), configuración de emisión (`COR_ElectronicEmissionSettings`). La UVT sigue en
`PAY_LegalParameters` (T23). Los valores **técnicos** (sondeo, espera exponencial, tamaños de tanda) van
en `appsettings`, sección `Integration` (T10).

### 2.9 Prefijos de ruta por área (T3)

| Área | Prefijo | Archivo de rutas |
|---|---|---|
| Catálogo | `/api/inventory/{units, product-categories, brands, accounting-groups, products, products/search, products/{id}/barcodes, products/{id}/units, products/{id}/taxes, products/{id}/accounting-group, adjustment-causes, sales-channels}` | `Endpoints/Inventory/CatalogEndpoints.cs` |
| Bodegas y existencias | `/api/inventory/{warehouse-types, warehouses, warehouses/{id}/locations, warehouses/{id}/activation, reorder-policies, stock, integrity/verify, integrity/rebuild}` | `WarehousesEndpoints.cs`, `StockEndpoints.cs` |
| Tipos y documentos genéricos | `/api/inventory/{document-types, documents}` (lista y detalle con alcance; sin escritura); I5 `documents/{id}/cost-impact` (consulta sin clave: `MotorDeCosteo.SimularImpacto`, sin guardar) | `DocumentTypesEndpoints.cs`, `DocumentsEndpoints.cs` |
| Ajustes, consumos, bajas, ensamble, ubicaciones | `/api/inventory/adjustments` (+ `/{id}`, `/{id}/confirm`, `/{id}/void`, `/{id}/discard`) | `AdjustmentsEndpoints.cs` |
| Traslados | `/api/inventory/transfers` (+ `/{id}/dispatch`, `/{id}/receive`, `/discrepancies/{id}/resolve`, `/{id}/void`) | `TransfersEndpoints.cs` |
| Conteos | `/api/inventory/counts` (+ `/{id}/open` congela la foto sin número, `/{id}/captures`, `/{id}/close` numera al confirmar, `/{id}/adjustment`, `/{id}/discard` con motivo para uno abierto, `/{id}/void` sólo para uno cerrado: crea un `Voiding` que lo referencia, sin kardex ni mensajes) | `CountsEndpoints.cs` |
| Compras | `/api/inventory/purchases/{receipts, supplier-invoices, supplier-invoices/{id}/radian-events, supplier-notes, returns, direct}`; I4 `purchases/support-documents`; I5 `purchases/{requests, orders, matches, landed-costs}` y `supplier-invoices/{id}/radian-events/emit` (`EmitRadianEventCommand`, cuerpo `{ eventCodes }`, 202) | `PurchasesEndpoints.cs` |
| Ventas | `/api/inventory/sales/{documents, invoices, credit-notes}`; I6 `sales/{quotes, orders, shipments, debit-notes}`, `/api/inventory/promotions` | `SalesEndpoints.cs` |
| POS | `/api/inventory/pos/{lookup, drafts, drafts/{id}/lines, drafts/{id}/lines/{lineId}, drafts/{id}/suspend, drafts/{id}/resume, drafts/{id}/discard, drafts/{id}/checkout}`, `/api/inventory/documents/{id}/reprint` | `PosEndpoints.cs` |
| Caja | `/api/inventory/{cash-sessions, cash-sessions/{id}/expected, cash-sessions/{id}/close, cash-movements, day-closes, day-closes/{id}/reopen}` | `CashEndpoints.cs` |
| Puntos, precios, descuentos | `/api/inventory/{points-of-sale, points-of-sale/{id}/cash-registers, price-lists, prices/resolve, discount-caps}` | `PointsOfSaleEndpoints.cs`, `PricingEndpoints.cs` |
| Períodos y puesta en marcha | `/api/inventory/{periods, periods/{year}/{month}/close, periods/{year}/{month}/reopen, opening-balances, legacy-figures}` | `PeriodsEndpoints.cs`, `GoLiveEndpoints.cs` |
| Vendedores | `/api/inventory/salespeople` (+ `template.xlsx`, `import`) | `SalespeopleEndpoints.cs` |
| Plataforma del módulo | `/api/inventory/{parameters, approval-policies, approvals, approvals/{id}/decide, amount-limits, scopes/users/{userPublicId}, alerts, alerts/{id}/attend, alert-types, messages, messages/reprocess, messages/send-not-applicable, templates}` | `ParametersEndpoints.cs`, `ApprovalsEndpoints.cs`, `ScopesEndpoints.cs`, `AlertsEndpoints.cs`, `MessagesEndpoints.cs` |
| Plantillas | `GET …/template.xlsx` (con `?withData=true` exporta lo existente) y `POST …/import?mode=review` o `?mode=apply`, junto a cada catálogo; mecánica, `ImportResultDto`, orden de carga, hojas, columnas y códigos `Import.*`: `contracts/plantillas.md` (única fuente) | en cada archivo |
| Core | `/api/core/{taxes, tax-rates, tax-rates/{id}/close, tax-rates/{id}/review, withholding-concepts, taxes/template.xlsx, taxes/import}` (I1; una vigencia nueva es otra fila, `review` limpia `ReviewPending` con motivo); I3 `/api/core/{payment-means, card-networks, card-acquirers, card-terminals, cash-denominations}` | `Endpoints/Core/TaxesEndpoints.cs`, `PaymentMeansEndpoints.cs` |
| Contabilidad | `/api/accounting/inventory/{rules, rules/{id}/versions, rules/template.xlsx, rules/import, voucher-mappings, completeness?date=, batches, batches/preview, batches/{id}, postings}` | `Endpoints/Accounting/InventoryIntegrationEndpoints.cs` |
| Facturación electrónica | `/api/electronic-invoicing/{settings, settings/verify-credential, resolutions, resolutions/{id}/channels, documents, documents/{id}, documents/{id}/retry, documents/{id}/query-status, documents/{id}/correct, documents/{id}/replace, documents/{id}/cancel, documents/{id}/transmit-by-current-channel, documents/{id}/download-link, contingencies, contingencies/{id}/close, readiness}`; `settings` se escribe con `POST` y `download-link` es `POST` (convención de adjuntos, PILA y dispersión) | `Endpoints/ElectronicInvoicing/*.cs` |
| Informes | `/api/reports/inventory/{vista}?format=` con `json`, `xlsx`, `pdf` o `docx` (una ruta por vista) | `Endpoints/Reports/InventoryReportsEndpoints.cs` |
| Plataforma general | `POST /api/audit/integrity/verify`; `GET /api/compliance/habeas-data/policies/current`; `GET /api/admin/roles/templates?module=Inventory`; `POST /api/admin/roles/from-template` | módulos existentes |

Toda ruta lleva `.RequirePermission(...)`; las exportaciones, `.RequirePermissionWhenExporting(...)`.
Sin permiso o fuera de alcance: el mismo 404 que algo inexistente. Única excepción: cuando el recurso ya
es visible y el permiso que falta depende del cuerpo (`Adjustments.SetUnitCost`,
`Warehouses.AcceptActivationDifference`, `Periods.AcceptUnbilledShipments`,
`DocumentTypes.DisableFiscalPosting`, el permiso de una clave de parámetro), la respuesta es 422 con
código propio, porque no revela existencia (precisión aplicada a FR-009; pregunta C10). Los comandos de
consumo de mensajes no tienen ruta.

### 2.10 Permisos (T48)

Siembra la API al arrancar (`PhaseZeroSecuritySeeder` → seeders por prefijo, antes de
`BuiltInRolesSeeder`), nunca el DbMigrator. Toda lectura sensible usa una acción distinta de `View`
porque `*.View` llega solo a Operator, ReadOnly y Auditor.

**`Inventory.*`** (`InventoryPermissionCatalogSeeder`)
- `Catalog.View`, `Catalog.Manage`, `Catalog.Import`, `Catalog.Export`, `Catalog.ReclassifyAccountingGroup`
- `Salespeople.View`, `Salespeople.Manage`
- `Warehouses.View`, `Warehouses.Manage`, `Warehouses.Activate`, `Warehouses.AcceptActivationDifference`
- `Stock.View` · `Costs.Read` · `Costing.Manage`
- `Integrity.Verify`, `Integrity.Rebuild`
- `Periods.View`, `Periods.Close`, `Periods.Reopen`, `Periods.AcceptUnbilledShipments`
- `DocumentTypes.View`, `DocumentTypes.Manage`, `DocumentTypes.DisableFiscalPosting`
- `Documents.View`, `Documents.Reprint`
- `Parameters.View`, `Parameters.Manage`
- `ApprovalPolicies.View`, `ApprovalPolicies.Manage` (políticas y montos máximos de `Inventory.*`)
- `Approvals.View`, `Approvals.Supervisor`, `Approvals.Management`
- `Scopes.Manage`, `Scope.AllWarehouses`, `Scope.AllPointsOfSale`
- `Adjustments.View`, `.Create`, `.Confirm`, `.Approve`, `.Void`, `.SetUnitCost`
- `Transfers.View`, `.Create`, `.Dispatch`, `.Receive`, `.Approve`, `.Void`
- `Counts.View`, `.Open`, `.Capture`, `.Close`, `.Approve`
- `Purchases.View`, `.Create`, `.Confirm`, `.Approve`, `.Void`, `.RegisterRadianEvent`, `.EmitRadianEvent` (I5)
- `Sales.View`, `.Create`, `.Confirm`, `.Approve`, `.Void`, `.SellOnCredit`, `.RefundOtherMeans`
- `Pos.Sell` · `Discounts.Authorize` · `Prices.View`, `Prices.Manage` · `DiscountCaps.Manage`
- `PointsOfSale.View`, `PointsOfSale.Manage`
- `CashSessions.View`, `CashSessions.ViewAll`, `CashSessions.Open`, `CashSessions.Close`
- `CashMovements.Create`, `CashMovements.Approve` · `CashDifferences.Approve`
- `DayClose.Execute`, `DayClose.Reopen`
- `OpeningBalance.Load`, `OpeningBalance.Approve` · `LegacyFigures.Import`
- `Messages.View`, `Messages.Reprocess`, `Messages.SendNotApplicable`
- `Reconciliation.View` · `Dashboard.View`
- `Reports.View`, `Reports.Export`, `Reports.ExportPersonalData`
- `Alerts.View`, `Alerts.Attend`, `Alerts.Manage`

**`Core.*`** (`CorePermissionCatalogSeeder`): `Core.Taxes.View`, `Core.Taxes.Manage`,
`Core.PaymentMeans.View`, `Core.PaymentMeans.Manage`.

**`Accounting.*`** (`AccountingPermissionCatalogSeeder`): `Accounting.InventoryRules.View`,
`Accounting.InventoryRules.Manage`, `Accounting.InventoryBatches.View`, `Accounting.InventoryBatches.Run`.
`GET /api/accounting/inventory/postings` usa el existente `Accounting.Vouchers.View` (`contracts/contabilidad.md` §7.3).

**`ElectronicInvoicing.*`** (`ElectronicInvoicingPermissionCatalogSeeder`): `Settings.View`,
`Settings.Manage`, `Resolutions.View`, `Resolutions.Manage`, `Documents.View`, `Documents.Transmit`,
`Documents.Correct`, `Documents.TransmitByCurrentChannel`, `Contingencies.View`, `Contingencies.Declare`.
Los artefactos (XML, respuestas, PDF) se leen con `Inventory.Sales.View` (ventas) o
`Inventory.Purchases.View` (documento soporte), como pide FR-068.

**Plataforma** (`DomainPermissionCatalogSeeder`): `AuditLog.VerifyIntegrity` (el Auditor lo recibe por
`AuditLog.*`). `GET /api/compliance/habeas-data/policies/current` exige `Core.People.Create`.

**Perfiles sugeridos** (`PerfilesSugeridos`, plantillas, no roles integrados): `inventario.administrador`,
`inventario.jefe`, `inventario.bodeguero`, `inventario.comprador`, `inventario.cajero`,
`inventario.aprobador`, `inventario.contador`, `inventario.auditor` (contenido en `contracts/api.md` §1.4,
con los códigos de esta lista).

### 2.11 Páginas y grupos de menú (T45)

Carpetas de Shared: `Pages/Inventario`, `Pages/Compras`, `Pages/Ventas`, `Pages/Pos` (las cuatro entran a
`ModulosMigrados`). Toda página usa `PermissionGate` e `IndicadorDeCarga`. Cuatro `NavMenuGroup` nuevos:
**Inventario**, **Compras**, **Ventas**, **Punto de venta**; más entradas en Maestros, Contabilidad y
Administración.

| Grupo | Ruta | E |
|---|---|---|
| Inventario | `/inventario/productos`, `/inventario/productos/{id:guid}`, `/inventario/categorias`, `/inventario/marcas`, `/inventario/unidades`, `/inventario/grupos-contables`, `/inventario/bodegas`, `/inventario/bodegas/{id:guid}`, `/inventario/tipos-de-documento`, `/inventario/causas-de-ajuste`, `/inventario/existencias`, `/inventario/kardex`, `/inventario/ajustes`, `/inventario/ajustes/{id:guid}`, `/inventario/traslados`, `/inventario/traslados/{id:guid}`, `/inventario/conteos`, `/inventario/conteos/{id:guid}`, `/inventario/periodos`, `/inventario/saldo-inicial`, `/inventario/cifras-solido`, `/inventario/parametros`, `/inventario/plantillas`, `/inventario/alertas`, `/inventario/aprobaciones`, `/inventario/politicas-de-aprobacion`, `/inventario/alcances`, `/inventario/integridad`, `/inventario/informes?vista=` | I1 |
| Inventario | `/inventario/activacion`, `/inventario/bandeja-de-mensajes`, `/inventario/conciliacion` | I2 |
| Inventario | `/inventario/tablero` | I6 |
| Compras | `/compras/recepciones`, `/compras/recepciones/{id:guid}`, `/compras/compra-directa`, `/compras/facturas-proveedor`, `/compras/facturas-proveedor/{id:guid}`, `/compras/notas-proveedor`, `/compras/devoluciones` | I1 |
| Compras | `/compras/documentos-soporte` | I4 |
| Compras | `/compras/solicitudes`, `/compras/ordenes`, `/compras/cruce`, `/compras/costos-adicionales` | I5 |
| Ventas | `/ventas/vendedores`, `/ventas/canales` | I1 |
| Ventas | `/ventas/documentos`, `/ventas/documentos/{id:guid}`, `/ventas/facturas/nueva`, `/ventas/notas-credito/nueva`, `/ventas/puntos-de-venta`, `/ventas/listas-de-precios`, `/ventas/listas-de-precios/{id:guid}`, `/ventas/topes-de-descuento`, `/ventas/informes?vista=` | I3 |
| Ventas | `/ventas/documentos-electronicos`, `/ventas/contingencias-dian` | I4 |
| Ventas | `/ventas/cotizaciones`, `/ventas/pedidos`, `/ventas/remisiones`, `/ventas/notas-debito`, `/ventas/promociones` | I6 |
| Punto de venta | `/pos` (layout `PosLayout`), `/pos/sesiones`, `/pos/sesiones/{id:guid}`, `/pos/sesiones/{id:guid}/cierre`, `/pos/movimientos-de-caja`, `/pos/cierre-del-dia` | I3 |
| Maestros | `/maestros/impuestos` (I1), `/maestros/medios-de-pago` (pestañas Medios, Franquicias, Adquirentes, Datáfonos de cobro, Denominaciones; I3), `/maestros/resoluciones-dian` (I4) | — |
| Contabilidad | `/contabilidad/inventario/matriz`, `/contabilidad/inventario/tipos-de-comprobante`, `/contabilidad/inventario/completitud`, `/contabilidad/inventario/lotes`, `/contabilidad/inventario/lotes/{id:guid}`; aviso en la pantalla de Períodos | I2 |
| Administración | `/admin/facturacion-electronica` (I4); pestaña «Integridad» en `/admin/auditoria` (I1); pestaña «Alcance comercial» en `/admin/usuarios` (I1); botón «Crear desde perfil sugerido» en `/admin/roles` (I1) | — |

Las 20 páginas del módulo actual, sus temas de `ManualCatalogo`, las tarjetas de `CentroReportes` y los
enlaces del grupo Cartera se retiran **en el mismo cambio** que publica cada reemplazo (Blazor falla
con rutas ambiguas al ejecutar, no al compilar).

### 2.12 Vistas de informes (`/api/reports/inventory/{vista}`, T44)

- I1: `kardex`, `valuation`, `stock`, `documents`, `count-differences`, `reorder-alerts`,
  `radian-events`, `legacy-comparison-kardex`, `legacy-comparison-valuation`.
- I2: `messages`, `accounting-batches`, `reconciliation`.
- I3: `sales-by-session`, `sales-by-register`, `sales-by-payment-means`, `cash-session`, `day-close`,
  `card-payments`, `cash-movements`, `cash-differences`, `voucher-redemptions`, `discount-approvals`,
  `impairment`.
- I4: `dian-documents`.
- I5: `purchase-matches`, `method-change-valuation`.
- I6: `margin` (`?by=product|category|salesperson|customer|point`), `turnover`, `abc`, `no-movement`,
  `expiring`, `purchase-suggestion`, `shrinkage-cap`.

Columnas ocultas con `_`: `_documento`, `_producto`, `_bodega`, `_mensaje`, `_lote`, `_sesion`.
Permisos: `Inventory.Reports.View` (json), `Inventory.Reports.Export` (archivo),
`Inventory.Reports.ExportPersonalData` además en vistas con datos de clientes (`margin?by=customer`,
`card-payments`, `sales-by-*` con cliente, `voucher-redemptions`); `reconciliation` exige también
`Inventory.Reconciliation.View`; `cash-session`/`day-close` de otros cajeros, `CashSessions.ViewAll`.

En `reconciliation`, las bodegas aún no activas que comparten cuentas con las activas **entran** con sus
cifras de SOLIDO a la fecha de corte y suman al valorizado del conjunto (como en FR-090); se muestran
aparte sólo para identificarlas. Así, con cero mensajes sin procesar, la diferencia es cero (SC-005).
Precisión aplicada a FR-081: «la diferencia no se les atribuye: entran con sus cifras importadas».
`shrinkage-cap` compara contra `Informes.TopeFaltantesPorcentaje` (§2.8).

### 2.13 Tipos de alerta (`COR_AlertTypes.TypeCode`, T39)

| TypeCode | Dispara | E |
|---|---|---|
| `Inventario.Reorden` | posición ≤ punto de reorden (FR-035) | I1 |
| `Inventario.Quiebre` | disponible < mínimo | I1 |
| `Inventario.IncidenteDeIntegridad` | la verificación del kardex encuentra diferencia (FR-003) | I1 |
| `Compras.EventosRadianFaltantes` | factura de proveedor a crédito sin 030/032 a los `Compras.DiasAlertaEventosRadian` | I1 |
| `Aprobaciones.Pendiente` | solicitud de aprobación esperando un nivel | I1 |
| `Integracion.MensajeSinEntregar` | 3 intentos o 15 min sin entregar (valores técnicos) | I2 |
| `Integracion.MensajeRechazado` | entrega `Rejected` (FR-080) | I2 |
| `Integracion.LoteNoCorrio` | lote programado vencido más la tolerancia técnica | I2 |
| `Inventario.VentaBajoCosto` | precio < costo con `Ventas.BajoCosto = Alertar` | I3 |
| `Personas.SinPoliticaDeDatos` | alta de persona sin política de Habeas Data publicada (T46, FR-011) | I3 |
| `Integracion.ValidacionFallida` | Cartera informa validación negativa (FR-061) | IC |
| `Dian.DocumentoSinValidar` · `Dian.DocumentoRechazado` · `Dian.PlazoDeContingencia` · `Dian.ContingenciaAbierta` · `Dian.ResolucionPorAgotar` · `Dian.ResolucionPorVencer` | FR-063, FR-065, FR-067 | I4 |
| `Inventario.ProximoAVencer` · `Inventario.RemisionSinFacturar` | FR-026, FR-052 | I6 |

Destinatarios por defecto que fija esta armonización (el resto, en la tabla `COR_AlertTypes` de
`data-model.md`): `Integracion.LoteNoCorrio` → `Accounting.InventoryBatches.Run` **y**
`Inventory.Messages.Reprocess` (InApp + Email, Warning), porque FR-080 pide avisar en Inventario y en
Contabilidad; `Personas.SinPoliticaDeDatos` → `Compliance.HabeasData.RecordConsent` (InApp + Email,
Warning).

Sin destinatario activo: se enruta a los titulares de `CompanyAdmin` y se marca `WithoutRecipient`
(SC-022); el reporte de completitud lista los tipos cuyo permiso no tiene ningún usuario activo.

### 2.14 Semillas paramétricas (por cooperativa; último `Order` usado: 76)

| Order | Semilla | Datos | E |
|---|---|---|---|
| 77 | `InventoryUnitsSeeder` | `Data/inventario-unidades.json` (con código Rec. 20) | I1 |
| 78 | `WarehouseTypesSeeder` | principal, punto de venta, averías, cuarentena, tránsito | I1 |
| 79 | `AdjustmentCausesSeeder` | causas de FR-037 + reclamación al transportador | I1 |
| 80 | `InventoryDocumentTypesSeeder` | un tipo por defecto por clase de I1 (incluido `Voiding`) con su secuencia | I1 |
| 81 | `TaxCatalogSeeder` | `Data/impuestos-co.json` (IVA 19/5/exento/excluido, INC, bolsas por unidad, ReteFuente por concepto en UVT, ReteIVA; ReteICA sin semilla), «pendiente de validar por la contadora» | I1 |
| 82 | `DivipolaSeeder` | `Data/divipola.json` → `COR_Cities.DaneCode` | I1 |
| 83 | `AlertTypesSeeder` | los tipos de §2.13 con permisos destinatarios por defecto | I1 |
| 84 | `InventoryVoucherMappingsSeeder` | `Data/inventory-voucher-mappings.json` (operación → tipo y cruce, §2.6) | I2 |
| 60 (existente) | `VoucherTypesSeeder` | `voucher-types.json` suma `NV`, `CP`, `TR`, `AC`, `CJ` (Module/INV); registra colisiones en el log | I2 |
| 85 | `CashDenominationsSeeder` | billetes y monedas vigentes | I3 |
| 86 | `DefaultPaymentMeansSeeder` | sólo `EFECTIVO` (clase Cash, `DianPaymentMeansCode` sugerido) | I3 |
| 87 | `ConsumidorFinalSeeder` | persona genérica «Consumidor final» del maestro, identificación según `CatalogoDian` | I3 |

Los permisos no son semilla paramétrica: los siembra la API al arrancar (T48). Los catálogos DIAN son
JSON embebidos versionados (T40), no semillas.

### 2.15 Migraciones (pares PostgreSQL / SQL Server, `tools/scripts/add-migration.ps1`)

| E | Nombre | Tipo | Contenido |
|---|---|---|---|
| I1 | `RetiroDelInventarioHeredado` | **destructiva con guarda** | suelta las 23 tablas; guarda por filas con aprobación por base (`COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'`); cabecera `MIGRACION-DESTRUCTIVA-APROBADA`; `Down()` las recrea vacías. Commit propio. |
| I1 | `PlataformaParaInventario` | aditiva | `COR_ParameterVersions`, `COR_OperationKeys`, `COR_BackgroundLeases`, `COR_Approval*`, `COR_AlertTypes`, `COR_Alerts`, `COR_AuditOutbox`, `COR_AuditChainHeads`, `COR_AuditAnchors`, `SEC_PermissionAmountLimits`, `COR_Tax*`, `COR_WithholdingConcepts`, columnas de `COR_People`/`COR_Cities`/`COR_Branches`, `COR_IntegrationMessages`, `COR_IntegrationMessageDependencies`, `COR_IntegrationMessageDeliveries`. |
| I1 | `InventarioComercialNucleo` | aditiva | tablas `INV_` de I1 (§2.2), `INV_Salespeople` con UK filtrado; en PostgreSQL `CREATE EXTENSION IF NOT EXISTS pg_trgm` + índice GIN sobre `INV_Products.SearchText`; en SQL Server índice no agrupado con `INCLUDE`. |
| I2 | `IntegracionContableDeInventario` | aditiva | `COR_IntegrationDeliveryAttempts`, `COR_IntegrationBatches`, `COR_IntegrationBatchCounters`, `ACC_InventoryPostingRules`, `ACC_InventoryVoucherMappings`, `ACC_InventoryPostings`, `ACC_AccountTaxRates.Rate` (9,6). |
| I3 | `VentasYPuntoDeVenta` | aditiva | `COR_PaymentMeans`, `COR_Card*`, `COR_CashDenominations`, tablas `INV_` de I3. |
| I4 | `DocumentosElectronicos` | aditiva | `COR_ElectronicEmissionSettings`, `COR_DianNumberingResolutions`, `COR_DianResolutionChannels`, `COR_ElectronicDocuments`, `COR_ElectronicDocumentVersions`, `COR_ElectronicDocumentTransmissions`, `COR_DianContingencyEvents`. |
| I5 | `ComprasYCosteoAvanzado` | aditiva | `INV_PurchaseMatchLines`, `INV_LandedCostAllocations`, `INV_CostLayers`, `INV_LayerConsumptions`; columnas `BalanceClosedAt`, `BalanceClosedByUserId`, `BalanceClosedReason` en `INV_Documents`. |
| I6 | `ComercioAmpliado` | aditiva | variantes, componentes, lotes, series, reservas, promociones. |

### 2.16 Componentes con nombre fijo

**Plataforma (Application/Common)**
- `Execution/`: `ContextoAmbiental` (AsyncLocal), `IEjecutorEnCooperativa`, `Actor` (record:
  `Kind`, `UserId?` (SEC_Users.Id, interno), `UserPublicId?`, `CentralUserId?`, `Name`, `Email?`,
  `Channel`, `Origin`, `Ip?`, `Reason?`), `IArrendamientos` (arrendar, renovar, soltar
  `COR_BackgroundLeases`).
  - **(nuevo, T040)** firmas: `ContextoAmbiental.Fijar(TenantDirectoryEntry, Actor, origen) → IDisposable` y
    `Activo`, `Cooperativa`, `Actor`, `Origen`; `Actor.ProcesoDeIntegracion(origen)` valida el prefijo
    (`Mensaje:`, `Lote:`, `Tarea:`), `Actor.NombreDelProceso`, `Actor.OrigenDeMensaje/OrigenDeLote/OrigenDeTarea`,
    `Actor.EsProceso`; `IEjecutorEnCooperativa.EjecutarAsync(…, ct) → Task<ResultadoEnCooperativa>` con
    `ResultadoEnCooperativa { Ejecutada=1, Omitida=2 }`; `IArrendamientos.ArrendarAsync(nombre, duracion?)`,
    `RenovarAsync(nombre, duracion?)` (bool) y `SoltarAsync(nombre)`; `IActorActual.ObtenerAsync(ct) → Task<Actor>`;
    `IOrigenDeLaPeticion { Ip, UserAgent, Endpoint, Canal (ExecutionChannel), Origen }`.
  - **(nuevo, T047–T051)** `Application/Common/Execution/NombresDeArrendamiento` (`Despacho`,
    `ReenvioDeAuditoria`, `FacturacionElectronica`, `TareasProgramadas`, `Correo`, `Todos`: los cinco nombres de
    `COR_BackgroundLeases`); `ITareaProgramada { string Nombre; bool DebeCorrer(DateTimeOffset ahoraLocal,
    DateTimeOffset? ultimaCorrida); Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) }` (se
    registra como singleton; recibe el ámbito de la cooperativa); `Persistence/Services/ArrendamientosEnBase`
    (`DuenoDelProceso`, `DuracionPorDefecto` = 120 s; constructor con dueño explícito para las pruebas);
    `Domain/Common/SinDiffDeAuditoriaAttribute` (lo necesitó `BackgroundLease`; `NoAuditarAttribute` sigue en T058);
    claves de `IntegrationOptions`: `Retries:{BaseDelaySeconds, MaxDelayMinutes, AlertAfterAttempts,
    AlertAfterMinutes}` y `ScheduledTasks:IntervalSeconds` (60) / `EmailDispatcher:IntervalSeconds` (15), validadas
    por `IntegrationOptions.Problemas()`; pasadas manuales `ProgramadorDeTareas.CorrerUnaPasadaAsync(ct)` /
    `(tenantPublicId, ct)` y `NotificationEmailDispatcher.DespacharUnaPasadaAsync(ct)`; en la fixture
    `CentralIdentityApiFixture.CorrerTareasProgramadasAsync(tenantPublicId)`. Origen del correo: `Tarea:email.dispatch`.
  - **(nuevo, T052–T057)** `Persistence/TransaccionExplicita.EjecutarAsync<T>(db, trabajo, ct)` (confirma salvo
    `Result.IsFailure`; se une a `CurrentTransaction`; descarta al repetir) y una sobrecarga `internal` con
    `descartarAlRepetir` que sólo usa `TransaccionDeLiquidacion` (sus handlers leen antes de abrirla);
    `Behaviors/{IOperacionIdempotente, IConMotivo, IOperacionDePuntoDeVenta}` (un archivo cada uno),
    `Behaviors/ValidadorConMotivo<T>` (base abstracta, `LargoMaximo` = 500), `Behaviors/EstadoDeLaOperacion`
    (Scoped: `Clave`, `EsRepeticion`, `PrimerUso`, `MarcarRepeticion`), `Behaviors/HuellaDeOperacion`
    (`Calcular(operacion, cuerpo)` = SHA-256 hex minúsculo de `{operación}
{JSON canónico}`, `Canonico`),
    `Behaviors/ErroresDeOperacion` (`ClaveRequerida()`, `ClaveReutilizada(operacion, primerUso)`);
    `Domain/Entities/Core/OperationKey.IndiceUnicoDeLaClave` = `UK_COR_OperationKeys_Key`;
    `AuditEventTypes.OperationReplayed`; en la API `Filters/ClaveDeOperacionFilter` (`Cabecera`,
    `CabeceraDeRepeticion`) con `ClaveDeOperacionExtensions.ConClaveDeOperacion()` y `HttpContext.ClaveDeOperacion()`
    (la ruta la copia al comando); en Shared `Services/Http/ClaveDeOperacion` (`Valor`, `Para(contenido)`,
    `Exito()`, `Aplicar(peticion, contenido)`, `FueRepeticion(respuesta)`).
  - **(nuevo, T058–T066)** auditoría con origen, entrega garantizada y sello:
    `Domain/Common/NoAuditarAttribute` (`Mascara = "***"`) y `Domain/Common/IHechoInmutable` (adelanto mínimo de
    T130, lo necesita `AuditAnchor`); `Application/Common/Audit/ModuloDeAuditoria` (`Inferir(ns, sinModulo = "General")`,
    constantes de módulo; el interceptor pasa `"Unknown"`; coincide por segmento entero, así `.Web` ya no atrapa
    `Identity.Auth.WebAuthn`); `AuditoriaEncadenada` (`Modulos`, `SufijoDelFlujo = ":10y"`, `Retencion` = 10 años,
    `EsEncadenado`, `Flujo(Guid|string)`, `AMilisegundos`, `Canal(ExecutionChannel)` → `web`/`app`/`pos`/`proceso`,
    `Entrada`, `LeerCarga`, `ComoJson`, `ContextoAsync`, `Evento`, `ParaMongo`, `RegistrarAsync(servicios, AuditLogCommand, ct)`
    —el método común de ingresar, exportar e imprimir—) y el record `ContextoDeAuditoria`; claves de metadata del
    evento `Channel`, `Origin`, `ActorKind`, `Actor`, `ActorUserPublicId`, `OperationKey`, `Reason`, `ErrorCode`,
    `ErrorMessage`; `AuditEventTypes.CommandRejected` (`"Rejected"`), `.CommandFailed` (`"Failed"`),
    `.AuditLogIntegrityVerified`; `AuditEventDocument.Metadata` (init, opcional); `AuditBehavior` recibe un cuarto
    parámetro opcional `IServiceProvider`; `ITenantDbContextFactory.AbrirLaDelAmbito()` (otra conexión a la base del
    ámbito, para el rechazo que sobrevive al rollback); `IAuditSignatureService.AnchorKeyVersion` y
    `.ComputeHmacBase64(payload, keyVersion)`; `AuditSignatureSettings.AnchorKeyVersion` (por defecto `dev-anclas-v1`,
    con su clave de desarrollo; nula si es `dev-v1` o no está en `Keys`: entonces no se ancla y el log dice
    `[Auditoria.AnclaSinClave]`); `Audit/Integrity/SelloDeIntegridad` (`Version = 1`, `Algoritmo`, `HashInicial` = 64
    ceros, `Cadena.{Campo, Stream, Seq, PrevHash, Hash, Alg, V}`, `Canonico(campos|BsonDocument)`, `Campos`, `Hash`,
    `CargaDeAncla`, `FirmarAncla`, `AnclaValida`); `Audit/Services/SelladoDeAuditoria` (el trabajo por cooperativa del
    reenviador: `Tanda` = 200, `TandasPorPasada` = 20, `AnclaCadaEventos` = 1.000, `Documento(entrada, seq, prevHash)`;
    separado del `BackgroundService` para que éste sólo recorra cooperativas); `AuditOutboxForwarder.ReenviarUnaPasadaAsync(ct)`
    / `(tenantPublicId, ct)`; `Audit/VerifyIntegrity/ILectorDeCadenaDeAuditoria` (`LeerAsync(tenantId, stream, desdeSeq,
    hastaSeq, ct)`, `AnclaValida`) con el record `EventoDeCadena`, implementado por `Audit/Integrity/LectorDeCadenaDeAuditoria`;
    `VerifyAuditIntegrityQuery(From, To, Stream?)`, `VerifyAuditIntegrityResult`, `AuditIntegrityIncident(Kind, Seq,
    EventId?, OccurredAt?)`, `AuditIntegrityIncidentKinds` (el `kind` viaja como texto) y `VerifyAuditIntegrityQueryValidator`;
    en la API `VerifyAuditIntegrityRequest`; en la fixture `CentralIdentityApiFixture.ReenviarAuditoriaAsync(tenantPublicId)`.
    Índices: `UK_COR_AuditOutbox_EventId`, `UK_COR_AuditOutbox_Stream_Seq`, `IX_COR_AuditOutbox_Pending`,
    `UK_COR_AuditChainHeads_Stream`, `UK_COR_AuditAnchors_Stream_Kind_Seq`, `UK_COR_AuditAnchors_Stream_AnchorDate`.
  - **(nuevo, T067–T072)** parámetros con vigencia: en `Domain/Common/Parametros` los enums `TipoDeParametro`
    { Bool=1, Int, Decimal, Text, Date, Time, Choice } y `EntregaDelComercio` { I1=1, I2, I3, IC, I4, I5, I6 } (no se
    guardan), el record struct `ValorInterpretado(Admitido, Texto, Valor)`, `DefinicionDeParametro` (`Modulo`, `Clave`,
    `Descripcion`, `Tipo`, `ValoresAdmitidos`, `ValoresDesde`, `DefectoSeguro`, `AmbitosAdmitidos`, `PermisoAdicional`,
    `SelladoAlConfirmar`, `ExigeFuenteLegal`, `DisponibleDesde`, `AdmiteVacio`, `Minimo`, `Maximo`, `Patron`;
    `Interpretar(texto, entrega)`, `Admitidos(entrega)`, `AdmiteAmbito`) y `CatalogoDeParametros` (`EntregaVigente` = I1,
    `Modulos`, `Todas`, `Buscar(modulo, clave)`); constantes de clave y de permiso en los tres catálogos
    (`ParametrosDeInventario.PermisoDeCosteo`, `ParametrosTributarios.Permiso`, `ParametrosDeFacturacionElectronica.Permiso`);
    `ParameterVersion.VigenteEn(fecha)`; índice `UK_COR_ParameterVersions_Module_Key_Scope_ValidFrom`. En
    `Application/Common/Parameters`: `ErroresDeParametros`, `ILectorDeParametros` (`LeerAsync(modulo, clave, fecha,
    ambito, ambitoId, ct)`, `VigenciasAsync(modulo?, clave?, ct)`; extensión `LeerComoAsync<T>`) con los records
    `ValorDeParametro` (`EsDefecto`, `Como<T>()`) y `VigenciaDeParametro`; `LectorDeParametros.VigenteA`;
    `IResolutorDeAmbitoDeParametro` (`ResolverAsync`, `DescribirAsync`) con `AmbitoDeParametro` y la implementación vacía
    `ResolutorDeAmbitoVacio`; `IReglasDeParametros` (`EvaluarAsync(AltaDeParametro, ct)`) con `AltaDeParametro`,
    `DecisionDeReglasDeParametro` (`Ambitos`, `AfectadosPorTipoDeDocumento`, `Adelante`) y `ReglasDeParametrosVacias`;
    `AddParameterVersion/{AddParameterVersionCommand, AddParameterVersionCommandValidator, AddParameterVersionCommandHandler,
    AddParameterVersionResponse, ReferenciaDeAmbitoDto}`; `ListParameters/{ListParametersQuery, ParameterDto,
    ParameterCurrentValueDto, ParameterOverrideDto, ParameterScheduledDto}`; `GetParameterHistory/{GetParameterHistoryQuery,
    ParameterHistoryItemDto}`; en la API `ParametersEndpoints.AgregarVigenciaRequest`. `ParameterDto` agrega
    `requiresLegalSource` al contrato de api.md §7. `Parameters.KeyNotFound` responde 404 (mapeado en `ErrorEnvelopeFilter`).
  - **(nuevo, T073–T078)** bandeja de salida y catálogo de mensajes: en `Application/Common/Integration` la solicitud
    `SolicitudDeEmision(Origen, OriginEventKey, Contenidos, Modo, CadenasDeLasQueDepende?, Relacionado?, ValidacionPrevia?,
    KindDelOriginal?)` con `OrigenDeEmision` (clase del documento como texto), `DocumentoRelacionado` y `ModoDeEntrega`
    { `Sellado(Modo, ScheduleKey?, BatchScopeKey?)`, `Heredado(OriginalPublicId, BatchScopeKey?)` };
    `EmisorDeMensajes.EmitirAsync(solicitud, ct) → IReadOnlyList<IntegrationMessage>` (Scoped, recuerda lo emitido en su
    ámbito; constantes `ModuloDeOrigen = "INV"`, `Moneda = "COP"`); `ClavesDeEvento` (`Confirmacion`, `Reclasificacion`,
    `ConfirmacionPor(Guid)`, `Cierre(v)`, `Reapertura(v)`, `EsValida(forma, clave)`); `ClavesDeLote` (`Horario(tipoRaiz,
    disparador, hora?, granularidad)`, `SesionDeCaja(Guid)`, `Periodo(año, mes)`); `ISenalDeMensajes { Avisar(mensajes);
    ChannelReader<Guid> Avisos }` y en la API `SenalDeMensajes` (`Capacidad` = 1024, descarta el aviso más viejo). En
    `Contracts/Inventory`: `OpcionesDeMensajes` (`Opciones`, `Serializar(contenido) → byte[]`), `CatalogoDeMensajesV1`
    (`Todos`, `Buscar(Type)`) con el record `TipoDeMensaje(Record, Type, Version, Destination, Kind?, FormaDeClave)` y el
    enum `FormaDeClaveDeEvento` { Confirmacion=1, ConfirmacionPorPublicId, Cierre, Reapertura, Reclasificacion } (no se
    guarda); `MessageOriginV1` (el `origin` del sobre) y los objetos anidados de §5–§8 de contracts/mensajes.md:
    `SalesTotalsV1`, `ApprovalRefV1`, `CreditTermsV1`, `SupplierDocumentV1`, `SupplierInvoiceLineV1`,
    `SupplierInvoiceTotalsV1`, `TaxableWithdrawalV1`, `VoidedContentV1`, `ReclassificationLineV1`, `CashierV1`,
    `CashCountDifferenceLineV1`, `PeriodValuationLineV1`, `UnbilledShipmentV1`, `AcknowledgedPendingV1`,
    `PartySnapshotV1`, `CreditPaymentV1`. `IntegrationEnvelopeV1.Payload` (`object`, al leer `JsonElement`) lleva el
    contenido, como en los ejemplos de §14. Índices `UK_COR_IntegrationMessages_Origin_Type_EventKey`,
    `IX_COR_IntegrationMessageDeliveries_Eligible` (`[Status] = 0`) e `…_InBatch` (`[Status] = 1`). Precisión de T9: el
    mensaje apunta al último de cada cadena **y** al último de esa cadena con entrega a su mismo destino (sin esa arista un
    `AjusteDeVentaACredito` no esperaría a su `VentaACreditoRegistrada` si el último del original fuera contable).
  - **(nuevo, T079–T086)** aprobaciones y montos máximos: en `Domain/Approvals`, `ApprovalSourceTypes` (constantes de
    `SourceType`), los records `NivelDeAprobacion(Order, Threshold, PermissionCode)`, `PoliticaDeAprobacion(DocumentTypePublicId?,
    ValidFrom, ValidTo?, Niveles)`, `ParticipantesDeAprobacion(Creador, Solicitante, Participantes, AprobadoresPrevios)`,
    `EvaluacionDeAprobacion(Resultado, Niveles, MontoMaximo?, NivelForzado, ReglaFija)`, `NivelSellado(Order, Threshold,
    Permission)` (la forma de `RequiredLevelsJson`) y los enums `ResultadoDeEvaluacion` { SinAprobacion=0, ConNiveles=1,
    ExcedeLimite=2 } y `MotivoDeExclusion` { Creator=1, Requester=2, Participant=3, PreviousLevel=4 } (no se guardan);
    `EvaluadorDePolitica.{ElegirPolitica, ReglaFija, Evaluar, ValidarDecision, ValidarNiveles}`. En
    `Application/Common/Approvals`: `IMotorDeAprobaciones` suma `InvalidarAsync(sourceType, sourcePublicId, subject)`
    (la fuente invalida cuando lo aprobado cambia, porque el rechazo `ContentChanged` revierte la transacción del
    aprobador) y `RetirarAsync(requestPublicId, motivo)`; records `EvaluacionConPolitica`, `SolicitudDeAprobacion`,
    `AprobadorPresente`, `DecisionDeAprobacion`; ganchos `IFuenteDeAprobacion` { `SourceType`, `AlAprobarAsync`,
    `AlDevolverAsync`, `EnAlcanceAsync`, `DescribirAsync` }, `IAvisosDeAprobacion` (`SinAvisosDeAprobacion` hasta T093),
    `IAutoridadDeOtroAprobador` (API: `AutoridadDeOtroAprobador`), `IReglasDePoliticaDeAprobacion`
    (`ReglasDePoliticaDeAprobacionVacias`, con `AltaDePoliticaDeAprobacion`), `IDesafiosDePresencia` con
    `DesafioDePresencia` (Caching: `RedisDesafiosDePresencia`); `VistaDeSolicitudes` (lo que comparten motor y
    consultas); `ErroresDeAprobaciones`, `PermisosLimitables`; DTOs `NivelDto`, `ApprovalPolicyDto`,
    `TipoDeDocumentoDeAprobacionDto`, `RolDto`, `PermissionAmountLimitDto`, `OrigenDeAprobacionDto`, `TipoDeOrigenDto`,
    `NivelDeSolicitudDto`, `DecisionDto`, `ApprovalRequestDto`, `EstadoDeFuenteDto`, `DecisionResultDto`,
    `DesafioDePresenciaDto`, `PresenciaDto`; carpetas `SaveApprovalPolicy`, `SetPermissionAmountLimit`,
    `ListApprovalPolicies`, `ListPermissionAmountLimits`, `DecideApproval`, `RequestPresenceChallenge`,
    `WithdrawApprovalRequest`, `ListMyPendingApprovals`, `GetApprovalRequest`. En `Interfaces/Security`,
    `AlcanceDeInventarioCerrado` (falla cerrado hasta T089). Índices `UK_COR_ApprovalPolicies_PolicyKey_ValidFrom`,
    `UK_COR_ApprovalPolicyLevels_PolicyId_Order`, `UK_COR_ApprovalRequests_Source_Subject_Pending`,
    `IX_COR_ApprovalRequests_Status_Module_CurrentLevel`, `UK_COR_ApprovalDecisions_RequestId_Level_Approved`,
    `UK_SEC_PermissionAmountLimits_Role_Permission_ValidFrom`. En la API, `ApprovalsEndpoints.{GuardarPoliticaRequest,
    DecidirRequest, DesafioRequest, RetirarRequest, FijarMontoRequest}`. Precisiones: `PermissionAmountLimit.MaxAmount` es
    nulable (api.md §15.3: nulo = sin límite); `Approvals.Policy.Overlaps` y `Approvals.AmountLimit.Overlaps` llevan
    `data.existingValidFrom`.
  - **(nuevo, T087–T095)** alcance y alertas: en `Interfaces/Security`, los puertos `IAsignacionesDeBodega`
    { `BodegasDelUsuarioAsync`, `BuscarAsync(publicIds)`, `ReemplazarAsync` } e `IAsignacionesDePuntoDeVenta`
    { `PuntosDelUsuarioAsync`, `BuscarAsync`, `ReemplazarAsync` } con `SinAsignacionesDeBodega`/`SinAsignacionesDePuntoDeVenta`,
    los records `ElementoDeAlcance(Id, PublicId, Code, Name)`, `AsignacionDeAlcance(Elemento, IsDefault)`,
    `AsignacionesDeAlcance(Items)` { `Ninguna`, `Ids`, `PorDefecto` } y `AsignacionPedida(Id, IsDefault)`,
    `ErroresDeAlcance` (`Inventory.Warehouse.NotFound`, `Inventory.PointOfSale.NotFound`, `Inventory.Scope.DefaultDuplicate`
    con `data.kind`) y `AlcanceDeInventario.De(...)`; `Application/Inventory/Common/FiltroDeAlcance` { `PorBodega`,
    `PorPunto`, `DocumentosPorBodega`, `DocumentoSinBodegaVisible`, `DocumentoSinBodegaOperable`, `AsegurarAsync` };
    `Application/Inventory/Security/Scopes/{VistaDeAlcanceComercial, GetUserCommercialScopeQuery,
    SetUserCommercialScopeCommand}` con `UserCommercialScopeDto`, `ScopeUserDto`, `WarehouseScopeDto`,
    `PointOfSaleScopeDto`, `WarehouseScopeInput`, `PointOfSaleScopeInput`; API `Services/AlcanceDeInventarioDeLaPeticion`
    y `Endpoints/Inventory/ScopesEndpoints.FijarAlcanceRequest`. En `Application/Common/Alerts`: `TiposDeAlerta` (el
    catálogo cerrado de §2.13 como constantes y `DefinicionDeTipoDeAlerta(TypeCode, Module, Description, Destinatarios,
    Canales, Severidad, DisponibleDesde, UsaDestinatarios, Umbrales)`), `IAlertas` { `LevantarAsync(AlertaALevantar)`,
    `AtenderPorProcesoAsync(dedupKey, nota)` } → `Alertas` (con `ClaveDe`), `AlertaALevantar`, `AlertaLevantada`,
    enum `DesenlaceDeAlerta` { Levantada=1, Repetida=2, TipoInactivo=3 } (no se guarda), `IDestinatariosPorPermiso`
    { `ResolverAsync`, `ContarActivosAsync`, `TiposSinDestinatarioAsync` } con `DestinatarioDeAlerta` y
    `DestinatariosDeAlerta(Usuarios, SinDestinatario)` (API: `DestinatariosPorPermiso`), `VisibilidadDeAlertas`,
    `AvisosDeAprobacionPorAlertas` (el `IAvisosDeAprobacion` real), `ErroresDeAlertas`, DTOs `AlertDto`,
    `AlertEntityDto`, `AlertTypeDto`; carpetas `RaiseAlert`, `AttendAlert`, `SaveAlertType`, `ListAlerts`, `GetAlert`,
    `ListAlertTypes`, `GetAlertTypeHistory`; código `Alerts.Type.ViewPermissionNotAllowed` (un permiso `*.View` no puede
    ser destinatario; §16.2 no le daba código). Índices `UK_COR_AlertTypes_TypeCode_ValidFrom`,
    `UK_COR_Alerts_DedupKey_Pending`, `IX_COR_Alerts_Status_TypeCode_RaisedAt`. `NotificationPayload.AlertPublicId` y
    `NotificationItemDto.AlertPublicId` (opcionales). En la API, `AlertsEndpoints.{AtenderRequest, VersionDeTipoRequest}`.
    Decisiones: `CompanyAdmin` **no** cuenta como destinatario por permiso (lo concede todo); es el respaldo de SC-022.
    Una alerta alcanza a quien tiene un permiso destinatario de la versión con que se levantó y alcance sobre su bodega y
    su punto, **o** a quien se le notificó. La severidad y el módulo de un tipo no se configuran.
  - **(nuevo, T038/T044)** `API/Services/PlataformaOptions` (sección `Plataforma`, `ZonaHoraria`);
    `Persistence/MultiTenancy/CooperativaDelAmbito.Crear` (la fábrica de `ErpTenantInfo`, sacada de
    `DependencyInjection`); `Shared/Services/Http/CanalDeOrigenHandler` con `Cabecera = "X-Canal"`, `Web = "web"`,
    `App = "app"`; propiedad de log `execution_origin` en `CentralIdentityLogEnricher`.
- `Interfaces/Security/IActorActual`, `Interfaces/IOrigenDeLaPeticion`, `Interfaces/Security/IAlcanceDeInventario`
  (record `AlcanceDeInventario`), `Interfaces/Security/ILimitesPorPermiso`.
- `Behaviors/IdempotencyBehavior` + marcador `IOperacionIdempotente { Guid OperationKey }`;
  marcadores `IConMotivo { string Reason }` e `IOperacionDePuntoDeVenta { Guid CashSessionPublicId }`.
- `Persistence/TransaccionExplicita` (generaliza `TransaccionDeLiquidacion`).
- `Integration/`: `EmisorDeMensajes`, `IDestinoDeMensajes`, `IMensajesEntrantes`, `MensajeEntrante`,
  `ResultadoDeConsumo` (Processed `(Guid AccountingDocumentPublicId, string VoucherTypeCode, string
  VoucherNumber)` | AlreadyProcessed | Rejected | Retry), `ISenalDeMensajes`;
  comandos `RegisterDeliveryResultCommand`, `OrderIntegrationBatchCommand`,
  `ReprocessMessagesCommand`, `SendNotApplicableMessagesCommand`, y los del ciclo del lote, sin ruta, que envía sólo el despachador: `ScheduleIntegrationBatchesCommand`, `StartIntegrationBatchCommand`, `CloseIntegrationBatchCommand` (nuevos); consultas
  `ListIntegrationMessagesQuery`, `PreviewIntegrationBatchQuery`.
- `Integration/Contracts/Inventory/*V1` (§2.6); `Integration/Accounting/IContabilidadParaInventario`
  (exactamente `EvaluarAsync`, `SaldosDeCuentasMapeadasAsync`, `CompletitudAsync`,
  `PrevisualizarLoteAsync`) con `MensajeContableDto`, `ResultadoDeContabilizacionDto`,
  `ConjuntoDeCuentasDto`; `Integration/Accounting/IDimensionesDeInventario` (lo implementa
  Inventario); `Integration/Lending/IConsultasDeCartera` (`EstadoCrediticioAsync`,
  `EstadoDeValidacionAsync`) y `CarteraNoHabilitada`.
- `Parameters/`: `LectorDeParametros`, `AddParameterVersionCommand`; `Domain/Common/Parametros/DefinicionDeParametro`;
  catálogos de claves `Domain/Inventory/Parameters/ParametrosDeInventario`,
  `Domain/Taxes/ParametrosTributarios`, `Domain/ElectronicInvoicing/ParametrosDeFacturacionElectronica`.
- `Approvals/`: `IMotorDeAprobaciones` (`EvaluarAsync`, `SolicitarAsync`, `DecidirAsync`,
  `PendientesParaMiAsync`), `MotorDeAprobaciones`, `DecideApprovalCommand`,
  `ListMyPendingApprovalsQuery`, `SaveApprovalPolicyCommand`, `SetPermissionAmountLimitCommand`;
  `Domain/Approvals/EvaluadorDePolitica` (puro).
- `Alerts/`: `IAlertas`, `RaiseAlertCommand`, `AttendAlertCommand`, `IDestinatariosPorPermiso`,
  `SaveAlertTypeCommand`.
- `Taxation/LectorDeUvt` (`IValorUvt`). `Imports/ErrorDeFila` (movido de `Accounting/Setup`, con `Sheet`; sus usos de
  la 009 se actualizaron, no hay alias) y `ModoDeImportacion { Review, Apply }`. Infraestructura común de importación
  (fase 3, T154–T160; todos **(nuevo)**): en `Application/Common/Imports`, `ImportResultDto` (+ `ResumenDeHojaDto`,
  `CambioDeFilaDto`, `CampoCambiadoDto`, `ResultadoDeFila`, `AccionDeImportacion { Create, Update, Unchanged }`),
  `ImportErrors`, `DefinicionDePlantilla`, `HojaDePlantilla`, `ColumnaDePlantilla`, `TipoDeValor` (los tipos de
  plantillas.md §0.4), `ArchivoDeImportacion` (el contenido no viaja al JSON; `Sha256` sí), `IComandoDeImportacion`
  (`Mode`, `File`, `Reason`, `OperationKey`), `DatosDePlantilla`, `EjecutorDeImportacion` (eventos `Import.Reviewed`,
  `Import.Applied`), `ContextoDeImportacion`, `HojaDeImportacion`, `FilaDeImportacion`, `CatalogoCitado<T>`;
  `Interfaces/Files/ITabularFileReader.{LeerHojaAsync, ListarHojasAsync}` y `ArchivosTabulares.{HojaFaltante,
  HojaDeTexto}`; `Reports/TipoDeColumna.{Cantidad, Costo}`. En `Application/Inventory/Imports`,
  `CatalogoDePlantillas` (+ `PlantillaDeParametrizacion`, claves `core.taxes`, `inventory.*`, `core.payment-means`,
  `accounting.inventory-rules`), `ListImportTemplatesQuery` (+ `ImportTemplateDto`). En la API,
  `Endpoints/Common/RutasDePlantilla.MapPlantilla`, `Endpoints/Inventory/TemplatesEndpoints`,
  `PlantillaDeImportacion.{Xlsx(DefinicionDePlantilla…), ConResultados, Formato}` y `ExportadorDeTablas.FormatoNumerico`.

**Inventario**
- **(nuevo, T186)** `Persistence/Configurations/Inventory/NucleoComercialSinMigracion` (`Tablas`,
  `ExcluirDeLasMigraciones(ModelBuilder)`): las nueve tablas `INV_` del documento genérico quedan fuera de las
  migraciones (`ExcludeFromMigrations`) entre `PlataformaParaInventario` y `InventarioComercialNucleo`; T440 la borra.
- Domain puro: `Inventory/Costing/{MotorDeCosteo, PromedioPonderado, Peps, Retroactivo, Prorrateo,
  CostoDeEntrada, Redondeo, ExplicacionDeCosto}`, `Inventory/Units/ConversionDeUnidades`,
  `Inventory/Documents/ClasesDeDocumento` (efecto, fiscal, mensajes, cadena, grupo, bodegas admitidas),
  `Inventory/Purchasing/CruceDeCompra`, `Sales/Pricing/ResolutorDeListaDePrecios`,
  `Sales/Cash/{CalculadoraDeEsperado, EvaluadorDeArqueo}`, `Sales/Payments/{ValidadorDePagos,
  DisponibilidadDeMedio}`, `Sales/Promotions/MotorDePromociones` (I6), `Common/Text/NormalizadorDeBusqueda`.
- Motores de costeo (fase 6, US3, T280–T282; todos **(nuevo)**): en `Domain/Inventory/Costing/ModeloDeCosteo` las piezas que
  no se guardan —`ValoracionDelMovimiento` { AlCostoVigente=1, AlCostoIndicado=2, AlCostoDeOrigen=3, DevolucionDeEntrada=4 },
  `PorcionDelAjuste` { EnExistencia=1, Vendida=2 }, `EstadoDeCosto` (+ `SalidaEnNegativo`), `MovimientoDeCosto`,
  `ParametrosDeCosteo`, `LineaDeKardexPropuesta`, `ReferenciaDeKardex`, `RechazoDeCosteo`, `ResultadoDeCosteo`—;
  `MotorDeCosteo.Aplicar` y `.CodigoExistenciaInsuficiente`; `Redondeo` con `RedondeoDeMontos` { Centavo=1, Peso=2 },
  `ResiduoDeRedondeo` { MayorValor=1, UltimaLinea=2 }, `Repartir` y `ValorPorBodega`; `ExplicacionDeCosto` + `PasoDeCosto`;
  `CostoDeEntrada` con `PedidoDeCostoDeCompra`, `ImpuestoDeCompra` y `CostoDeEntradaCalculado`; `Retroactivo.Insertar` con
  `PedidoRetroactivo`, `MovimientoRegistrado`, `MovimientoRetroactivo`, `ResultadoRetroactivo` y
  `AjusteRetroactivoPorDocumento`. Pruebas: `Domain.Tests/Inventory/Costing/{CasoDoradoDeCosteo, CasosDoradosDeCosteoTests,
  PropiedadesDelKardexTests, MotorDeCosteoTests}`.
- Application: `Inventory/Common/ICerrojoDeInventario` (impl. `Persistence/Inventory/CerrojoDeInventario`),
  `Inventory/Common/FiltroDeAlcance`, `Inventory/Kardex/{RegistroDeKardex, VerificacionDeIntegridad}`,
  `Inventory/Documents/Numeracion/Numerador`, `Inventory/Documents/Efectos/*` (una estrategia por clase),
  `Inventory/Integration/{EmisionDeInventario, DimensionesDeInventario}`, `Inventory/Reports/InventoryAuditEmitter`, `Inventory/Security/Scopes/{AsignacionesDeBodegaEnBase, AsignacionesDePuntoDeVentaEnBase}` (nuevo; implementaciones reales de `IAsignacionesDeBodega`/`IAsignacionesDePuntoDeVenta`), `Inventory/Replenishment/PosicionDeReposicion` (nuevo; único lector de disponible, en tránsito hacia la bodega y por recibir).
- Piezas del documento (base de inventario, T130–T141; todas **(nuevo)** salvo las ya nombradas arriba):
  Domain `Inventory/Documents/ClasesDeDocumento` describe cada clase con el record `DescripcionDeClase` y cuatro
  enumeraciones de comportamiento que **no se guardan** —`InventoryEffect` { Entry=1, Exit=2, Both=3, CostOnly=4,
  None=5, Reservation=6 }, `FiscalDirection` { Received=1, Emitted=2 }, `NumberedBy` { Sequence=1, DianResolution=2 },
  `AdmittedWarehouses` [Flags] { None=0, Operational=1, NotActivated=2, Transit=4 }—, con las constantes de los
  tipos de mensaje de §2.6 y la entrega por `EntregaDelComercio`; `Domain/Exceptions/{InvalidDocumentTransitionException,
  ImmutableEntityModifiedException}`; `IInmutableTrasConfirmar.PropiedadesMutablesTrasConfirmar`/`EstaFijo`.
  Application `Inventory/Common/ICerrojoDeInventario` con `PedidoDeCerrojo`, `ModoDeBloqueoDelSetup` { Compartido,
  Exclusivo }, `ClaveDeEstadoDeCosto`, `ClaveDeExistencia`, `ClaveDeDetalleDeExistencia`; `Inventory/Common/InventoryErrors`
  (catálogo de códigos de §8 y §9.6 de api.md); `FiltroDeAlcance.{DocumentosVisibles, BodegasDeSusOrigenesAsync,
  DocumentoVisible}`. Persistence `Inventory/SqlDelCerrojo` (+ `SentenciaDelCerrojo`, el SQL puro de cada motor que
  ejecuta `CerrojoDeInventario`) y `DbContext/GuardaDeInmutabilidad` (la guarda de T137 que llama `SaveChangesAsync`).
- Catálogo y bodegas (fase 4, US1, T198–T226; todos **(nuevo)**): Domain `Inventory/Units/ConversionDeUnidades` con los
  records `PedidoDeConversion`, `ResultadoDeConversion` y `RechazoDeConversion` (el borrador ya convierte con él);
  `ReorderPolicy.EsCoherente`/`Fijar`. Application `Inventory/Catalog/{CatalogErrors, CatalogDtos, ActivacionDeCatalogo}`,
  `Catalog/Products/{ReglasDeProducto (+ DatosDeProducto, ImpuestoPedido, ImpuestoResuelto), VistaDeProductos,
  ValidacionDeProducto, CodigosDeBarras, BusquedaDeProductos}` (`UnidadPedida`, `CodigoPedido`), `Catalog/Categories/VistaDeCategorias`,
  `Catalog/UnitsOfMeasure/ReglasDeUnidad`; `Inventory/Warehouses/{WarehouseErrors, WarehouseDtos, VistaDeBodegas,
  BodegasDelAlcance}` con `CreateWarehouseCommandHandler.AplicarReglasAsync` (+ `DatosDeBodega`, `AltaDeBodega`,
  `BodegaDeTransitoPedida`), la regla única de la bodega y su tránsito que reusa la plantilla 7;
  `Inventory/Common/IExistenciasParaElCatalogo` (+ `ExistenciaAgregada`) con la implementación vacía `ExistenciasSinKardex`
  (TryAdd; la reemplaza US2 sobre `INV_StockBalances`/`INV_StockDetails`): existencia de una bodega o ubicación para
  inactivarla y disponible para la búsqueda; `Inventory/Common/ReglasDePlataformaDeInventario` (resuelve el ámbito
  `Warehouse`; US3 le suma el resto e `IReglasDeParametros`); `Inventory/Documents/MaestrosDelDocumentoEnBase` (la real de
  `IMaestrosDelDocumento`; `CorteAsync` lo completa US3) y `UnidadDelDocumento.BaseUnitCode`; `CodigoDeCatalogo.Duplicado`
  con `existingPublicId`/`existingName`; `AdjuntosDeModulo.ProductoDeInventario` (`InventoryProduct`) y
  `TiposDeImagenDeProducto`. Persistence `Configurations/Inventory/EntidadDeInventario.ComoEntidadDeInventario`,
  `Catalog/IndiceDeBusquedaDeProductos` (GIN `gin_trgm_ops` o `INCLUDE`, según el motor; la extensión `pg_trgm` la crea
  T440), `Seeding/Parametric/SemillasDeInventario` (esperan a `InventarioComercialNucleo`) y
  `SincronizacionDeCatalogo.SincronizarUnidadesAsync` (versión en `COR_SystemSettings`,
  `INV.UnidadesSembradas.Version`).
- Ciclo común y tipos de documento (fase 3, T142–T152; todos **(nuevo)**): en `Application/Inventory/Documents`,
  `Efectos/IEfectoDeClase` (`Clase`, `AvisosDelBorradorAsync`, `ValidarAsync`, `MontoParaAprobar`, `Cerrojo`,
  `AplicarAsync`, `MensajesAsync`, `RevertirAsync`, `MensajesDeAnulacionAsync`) con el record `ContextoDeEfecto(Documento,
  Tipo, Clase, Original?)`, la base `EfectoDeClaseBase` y el registro `Efectos/EfectosDeClase` (`Para(clase)`, `Opera`;
  la anulación no tiene estrategia propia: la revierte la de la clase del original); el puerto `IMaestrosDelDocumento`
  (bodegas, productos, unidades, ubicaciones, causas, canales y `CorteAsync`) con los records `BodegaDelDocumento`,
  `ProductoDelDocumento`, `UnidadDelDocumento`, `UbicacionDelDocumento`, `ReferenciaDelCatalogo`, `CorteDeInventario` y
  la implementación `MaestrosDelDocumentoSinCatalogo` (TryAdd; la reemplazan US1/US3); los pasos opcionales
  `IPasoFiscalDeConfirmacion` e `IPasoDeValidacionPrevia` (+ `ResultadoDeValidacionPrevia`); `ReglasDelDocumento` (reglas
  comunes de §9.4, avisos al guardar y error al confirmar); `PermisosDeGrupo` (ver, crear, confirmar, anular y
  `PermisoLimitado` por `DocumentClassGroup`); `ErroresDelDocumento` (404 de lo referenciado:
  `Inventory.Product.NotFound`, `Inventory.Location.NotFound`, `Inventory.AdjustmentCause.NotFound`,
  `Inventory.SalesChannel.NotFound`, `Core.CostCenter.NotFound`, `Core.Person.NotFound`, y
  `Inventory.Document.Unauthorized` sin persona resuelta); `VistaDeDocumentos` (buscar con alcance y grupo,
  `allowedActions` —`Edit`, `Discard`, `Confirm`, `Void`—, resúmenes y detalle); `ConfirmacionDeDocumento` (el flujo
  canónico como servicio, con `PedidoDeConfirmacion(DocumentPublicId, GrupoEsperado?, RowVersion?, PorAprobacion)`),
  `FotoDeLaContraparte`, `FuenteDeAprobacionDeDocumento` (`IFuenteDeAprobacion` de `InventoryDocument`); `Queries/`
  con `FiltrosDeDocumentos`, `ListInventoryDocumentsQuery`, `GetInventoryDocumentQuery`; los DTO de
  `InventoryDocumentDtos.cs` (T143). `Numerador.AjustarSiguiente` (el único otro escritor de `NextValue`, desde
  `AddDocumentSequenceCommand`). En `Application/Inventory/DocumentTypes`: `DocumentClassDto`, `DocumentTypeDto`,
  `CamposObligatoriosDto`, `DocumentSequenceDto`, `PoliticaDelTipoDto`, `NivelDePoliticaDelTipoDto`,
  `ModoDePasoDelTipoDto`, `MarcasDelTipo`, `ReglasDeTipoDeDocumento`, `VistaDeTiposDeDocumento`. En la API
  `Endpoints/Inventory/CicloDeDocumentoRutas.MapCicloDeDocumento(grupo, DocumentClassGroup, prefijoDePermiso, nombre)`
  (con `MotivoRequest`, `ConfirmarRequest`, `AnularRequest`), `DocumentsEndpoints`, `DocumentTypesEndpoints`
  (`CrearTipoRequest`, `EditarTipoRequest`, `SecuenciaRequest`, `MotivoRequest`). Slug de catálogo
  `tipos-de-documento`. Semilla `InventoryDocumentTypesSeeder` (Order 80) con los códigos REC, FCP, NTP, DVP, AJP, AJN,
  CIN, BAJ, SIN, TRD, TRR, MUB, CON, AJC, ANU —y, desde US11, CONP y CONN, los ajustes de conteo con su política—; no siembra
  hasta que la base tenga `InventarioComercialNucleo`.
- Existencias y kardex (fase 5, US2, T249–T267; todos **(nuevo)** salvo los ya nombrados): Domain
  `Entities/Inventory/Transactions/KardexEntry` (hecho `AuditableEntityLong`, propiedades `init`, navegaciones
  `ReversesEntry`/`AffectsEntry`) y `Entities/Inventory/Projections/{StockBalance, StockDetail, CostState}`, las cuatro con
  `[SinDiffDeAuditoria]`; `ProductAccountingGroupChange` pasa a `init` (es hecho). Persistence
  `Configurations/Inventory/Transactions/KardexEntryConfiguration` (índices `IX_INV_KardexEntries_Product_Scope_Date`,
  `_Product_Warehouse_Date`, `_OperationDate`, `_DocumentId`, `_AffectsEntryId` filtrado) y
  `Configurations/Inventory/Projections/{StockBalanceConfiguration (UK_INV_StockBalances_Product_Warehouse),
  StockDetailConfiguration (UK_INV_StockDetails_Location con [LotId] IS NULL, UK_INV_StockDetails_Location_Lot con IS NOT NULL),
  CostStateConfiguration (UK_INV_CostStates_Product_Scope)}`, las cuatro tablas en `NucleoComercialSinMigracion`.
  Application `Inventory/Kardex/RegistroDeKardex` (`PrepararAsync` → `PreparacionDelRegistro(Cerrojo, ValorEstimado)`,
  `RegistrarAsync(documento, movimientos, ct, sugerencia?)` → `RegistroHecho` de `MovimientoEscrito`,
  `LeerParametrosAsync` → `ParametrosDelKardex(Metodo, Ambito, Montos, NegativoPorBodega)`; el movimiento es
  `MovimientoDeKardex(Linea, WarehouseId, QuantityBase, Valoracion, CostoUnitario?, Origen?, EsAnulacion, LocationId?)`),
  `Kardex/ReversionDeKardex` (`MovimientosAsync`, `RevertirAsync` → `ReversionHecha` con `DiferenciaDeCostoDeAnulacion`;
  sugerencias `NegativeAdjustment`/`SupplierReturn`), `Kardex/VerificacionDeIntegridad` (`AlcanceDeVerificacion`,
  `IncidenteDeKardex`, `ResultadoDeVerificacion`), `Kardex/FiltrosDeIntegridad`, `Kardex/VerifyInventoryIntegrityQuery`
  (`ClaveDelAlcance`: `Inventario.IncidenteDeIntegridad:todo` o la huella del alcance), `Kardex/RebuildInventoryProjectionsCommand`,
  `Kardex/VerificacionNocturnaDeIntegridad` (la `ITareaProgramada` `inventario.integridad`, desde las 02:00 locales, una vez por
  día; singleton registrado en `API/Program.cs`), `Kardex/StockQueries` (`GetStockQuery`, `GetProductStockQuery`,
  `ValorDeExistencias` —el valor por bodega al promedio del ámbito— y `ExistenciasEnKardex`, la real de
  `IExistenciasParaElCatalogo`), `Kardex/KardexDtos` (`StockRowDto`, `ProductStockDto` y sus partes, `IntegrityReportDto`,
  `IntegrityIncidentDto`, `RebuildResultDto`, `TiposDeIncidente`), `Replenishment/PosicionDeReposicion` (`LeerAsync` →
  `Posicion(Disponible, EnTransito, PorRecibir)`, `EnTransitoAsync` → `DespachoEnTransito`),
  `Documents/Efectos/{EfectoDeAjuste (base), EfectoDeAjustePositivo (PermisoDeCosto), EfectoDeSalidaPorAjuste (base) con
  EfectoDeAjusteNegativo, EfectoDeConsumoInterno, EfectoDeBaja}`, `Integration/EmisionDeInventario` (`AjusteAprobadoAsync`,
  `LineasDeCostoAsync`, `AnulacionAsync`, `AjusteDeCostoAsync`, `Invertido`, `Referencia`, `OperacionDeAjuste`,
  `ImportesYCantidades`), `Reports/{KardexReportQuery, StockReportQuery}`; `InventoryErrors.{AdjustmentUnitCostNotAllowed,
  AdjustmentUnitCostOnlyOnEntries, AdjustmentTaxableWithdrawalNotAvailable, CampoCausa = "adjustmentCause"}`;
  `AdjuntosDeModulo.SoporteDeAjuste` (`InventoryAdjustmentSupport`); `AuditEventTypes.InventoryIntegrityVerified`. El ciclo
  común emite cada `AjusteDeCostoReconocidoV1` como su propia unidad (`Confirmation:{afectado:N}`, heredado del afectado) y la
  anulación informa `costAdjustments`. API `Endpoints/Inventory/{StockEndpoints (VerificarRequest, ReconstruirRequest),
  AdjustmentsEndpoints}` y las vistas `kardex` (propios `location`, `includeCostAdjustments`) y `stock` (`onlyWithStock`).
  Shared `InventarioClient.{Existencias, Ajustes}` (`ClasesDeAjuste`, `DuenoDeSoportesDeAjuste`), `InventarioDtos.Existencias`
  y las páginas `Existencias`, `Kardex`, `Ajustes`, `AjusteDetalle` (también `/inventario/ajustes/nuevo`) e `Integridad`.
- Costo y períodos (fase 6, US3, T273–T295; todos **(nuevo)** salvo los de data-model): Domain
  `Entities/Inventory/Periods/{InventorySetup, InventoryPeriod (con `Inicio`/`Fin` sin columna), PeriodClosingBalance (navegación
  `Period`, `[SinDiffDeAuditoria]`)}`; Persistence `Configurations/Inventory/Periods/{InventorySetupConfiguration,
  InventoryPeriodConfiguration (UK_INV_Periods_Year_Month filtrado), PeriodClosingBalanceConfiguration
  (UK_INV_PeriodClosingBalances_Period_Version_Product_Warehouse, IX_INV_PeriodClosingBalances_Period_Superseded)}`, las tres en
  `NucleoComercialSinMigracion`; `DbSet` `InventorySetups`, `InventoryPeriods`, `PeriodClosingBalances`. Application
  `Inventory/Periods/{ValorizadoALaFecha (+ FilaDeValorizado; único cálculo del valorizado a una fecha: último cierre vigente
  más kardex posterior), RevisionDeCierre (`SiguientePorCerrar`, `RevisarAsync`), CloseInventoryPeriodCommand (+
  GetPeriodCloseCheckQuery), ReopenInventoryPeriodCommand (+ ListInventoryPeriodsQuery), PeriodDtos}` con los DTO
  `InventoryPeriodsDto`, `InventoryPeriodDto`, `UnbilledShipmentsAcceptedDto`, `PeriodCloseCheckDto`, `CloseBlockersDto`,
  `CloseWarningsDto`, `UnresolvedTransferDto`, `PendingMessagesDto`, `UnbilledShipmentDto`, `ClosePeriodResultDto`,
  `PeriodValuationDto`, `CodigoYNombreDto`, `BodegaDelValorizadoDto`, `ReopenPeriodResultDto`;
  `Inventory/Catalog/GrupoContableALaFecha` (`De`, `DeAsync`, `CodigosAsync`; lo usan el valorizado, el cierre y
  `EmisionDeInventario`); `Inventory/Catalog/Products/ChangeProductAccountingGroupCommand` con `ReclasificacionDeGrupo`
  (`ValidarAsync`, `AplicarAsync`, `BodegasAsync`; la comparten el comando y la plantilla de productos), `ReclasificacionHecha`,
  `GetProductAccountingGroupHistoryQuery` y los DTO `GrupoDelCambioDto`, `BodegaDelCambioDto`,
  `AccountingGroupChangeResultDto`, `AccountingGroupChangeDto`; `Inventory/Common/OperacionesDeInventario`
  (`SucursalPrincipalAsync`: la primera sucursal de `COR_Branches`, porque Inventario no lee la configuración contable;
  `ModoGeneralAsync`: el valor general del modo de paso para una operación sin tipo); `Inventory/Reports/ValuationReportQuery`
  (vista `valuation`, propio `includeTransit`); en el kardex, `LineaRetroactivaEscrita`, `AjusteRetroactivoRegistrado`,
  `RegistroHecho.AjustesRetroactivos`, `RegistroDeKardex.EsExentoAsync` y `EmisionDeInventario.AjustesRetroactivosAsync`;
  `InventoryErrors.{MesDeInventario, ConteoAbierto, TipoNombrado}` y las fábricas `Period*`, `RetroactiveNotAllowed`,
  `PostingMode*`; `CatalogErrors.{ProductAccountingGroupUnchanged, ProductMovementsAfterEffectiveDate}`;
  `ErroresDeParametros.{RequiereInicioDePeriodo, EnPeriodoCerrado}`; `ErroresDeAprobaciones.{PoliticaEnPeriodoCerrado,
  PoliticaRequerida}`. `ReglasDePlataformaDeInventario` implementa ahora `IResolutorDeAmbitoDeParametro` (bodega y tipo de
  documento), `IReglasDeParametros` e `IReglasDePoliticaDeAprobacion` (una instancia por petición para las tres;
  `PermisoDeFiscalSinPaso`, `PermisoDeConteo`: un tipo de ajuste cuya política vigente tiene un nivel con
  `Inventory.Counts.Approve` es «de ajuste de conteo» y no admite política vacía). Plantilla 8: `ImportDocumentTypesCommand
  .ConfirmFiscalWithoutPosting`, `ImportDocumentTypesCommandHandler.{ExtraFiscalesSinPaso = "fiscalTypesWithoutPosting",
  AvisoIgnorada}`. API `Endpoints/Inventory/PeriodsEndpoints` (`CerrarRequest`, `ReabrirRequest`), `CatalogEndpoints
  .{Reclasificar, CambioDeGrupoRequest}`, `RutasDePlantilla.MapPlantilla(camposDelFormulario)` y la vista `valuation`.
  Shared `InventarioClient.Periodos`, `InventarioDtos.Periodos` y la página `Periodos` (`/inventario/periodos`, con su enlace
  en el menú); pestaña «Grupo contable» de `ProductoDetalle`. Pruebas `Application.Tests/Inventory/{Costing/RetroactivoMinimoTests,
  Common/ReglasDePlataformaDeInventarioTests, Catalog/ChangeProductAccountingGroupCommandTests,
  Periods/{PeriodosDePrueba, InventoryPeriodCommandsTests}, Reports/ValuationReportQueryTests}`.
- Compras de I1 (fase 8, US9, T326–T358; todos **(nuevo)** salvo los de data-model y api.md): Domain
  `Entities/Inventory/Purchasing/{SupplierInvoiceDetail (Contado/Credito, NormalizarCufe, NormalizarNumero, PaymentForm, NumeroVisible,
  navegación Document), SupplierInvoiceEvent (FuenteDian/FuenteProveedor/FuenteErp, columna nueva `RegisteredAt`)}`,
  `Inventory/Purchasing/TransicionesDeEventoRadian` (+ `EventoRadianActual`; `Iniciales`, `RegistrarExterno`, `QuedaPendiente`,
  `Hecho`), `Inventory/Costing/DiferenciaDePrecio` (+ `PedidoDeDiferenciaDePrecio`; `MotorDeCosteo.DiferenciaDePrecio`), columna
  nueva `InventoryDocumentLine.AffectsCost` (nota del proveedor); Persistence `Configurations/Inventory/Purchasing/{SupplierInvoiceDetailConfiguration
  (UK_INV_SupplierInvoiceDetails_Document, UK_INV_SupplierInvoiceDetails_Supplier_Class_Number, UK_INV_SupplierInvoiceDetails_Cufe),
  SupplierInvoiceEventConfiguration (UK_INV_SupplierInvoiceEvents_Document_EventCode, IX_INV_SupplierInvoiceEvents_Status)}` en
  `NucleoComercialSinMigracion`; `DbSet` `SupplierInvoiceDetails`, `SupplierInvoiceEvents`. Application: en el ciclo común
  `Documents/IBorradorDeGrupo` (+ `BorradorEnCurso`, `ResultadoDelBorrador`; `SaveInventoryDraftCommandHandler` lo recibe
  opcional), `IConfirmacionEncadenada` (en `PasosOpcionalesDeConfirmacion`; la llama `FuenteDeAprobacionDeDocumento`),
  `IEfectoDeClase.OrigenesDelModoAsync` (los derivados copian el modo de su origen y sus mensajes lo heredan, con `related` =
  el origen), `SaveInventoryDraftRequest.{OperationMunicipalityDaneCode, SupplierPersonPublicId, Supplier, SupplierInvoicePublicId,
  NoteKind, Contraparte, TraeCamposDeCompra}` y `SupplierDocumentRequest`, `SaveInventoryDraftLine.{ReceiptLinePublicId,
  InvoiceLinePublicId, Amount, AffectsCost, Origen}`; `Inventory/Purchasing/Common/{CalculoTributarioDeCompra (+ CalculoDeCompra,
  TotalesDeCompra; `Perfil`, `Foto`, `DesdeLaFoto`, `AplicarTotales`), BorradorDeCompra (+ ContextoDeCompraDirecta),
  VinculosDeCompra (+ ConsumoDeRecepcion), DiferenciasDePrecioDeCompra, ColisionDeFacturaDeProveedor (IndiceDelNumero,
  IndiceDelCufe), ErroresDeCompras}`; `Documents/Efectos/{EfectoRecepcionDeCompra (+ ReglasDeCompra), EfectoFacturaDeProveedor,
  EfectoNotaDeProveedor, EfectoDevolucionAProveedor (SupportDocumentAdjustmentNoteRequired = false hasta I4)}`;
  `RegistroDeKardex.{CerrojoDeDiferenciasAsync, RegistrarDiferenciasDePrecioAsync}` (+ `DiferenciaDePrecioPedida`,
  `DiferenciaDePrecioRegistrada`); `EmisionDeInventario.{CompraRecibidaAsync, DevolucionAProveedorAsync,
  FacturaProveedorRegistradaAsync (+ LineaDeFacturaDeProveedor), AjusteDeDiferenciaDePrecioAsync}`;
  `Inventory/Purchasing/{PurchaseDtos (RadianEventDto, SupplierInvoiceInfoDto, ReceiptLineBalanceDto, PurchaseDocumentDto,
  SupplierInvoiceSummaryDto, PurchaseReceiptSummaryDto, DocumentoCreadoDto, FacturaCreadaDto, DirectPurchaseResultDto,
  DirectPurchaseInvoiceRequest, RegistrarEventoRadianRequest (con `Correct`: corregir un registro externo), SupplierInvoicePrefillDto,
  PrefillSupplierDto, PrefillLineDto, PrefillTaxDto, PrefillTotalsDto), ConfirmDirectPurchaseCommand (+ CompraDirectaEncadenada),
  RadianEventCommands (RegisterExternalRadianEventCommand, `AccionDeCorreccion = "Inventory.RadianEvent.Corrected"`,
  ListRadianEventsQuery), RevisionDeEventosRadian (`ClaveDeLaAlerta`, `RadianPendiente:{facturaPublicId}`) y la tarea
  `TareaDeEventosRadian` (`compras.eventos-radian`, 06:00 local, registrada en `Program.cs`), PurchaseQueries (FiltrosDeCompras,
  ConsultasDeCompras, ListPurchaseReceiptsQuery, ListSupplierInvoicesQuery, GetPurchaseDocumentQuery), LectorDeFacturaUbl
  (+ FacturaLeida, LineaLeida, ImpuestoLeido), PrefillSupplierInvoiceQuery}`; `Inventory/Reports/RadianEventsReportQuery`. API
  `Endpoints/Inventory/PurchasesEndpoints` (+ `CompraDirectaRequest`, `FiltrosDeComprasRequest`) y
  `CicloDeDocumentoRutas.MapCicloDeDocumento(…, conConsultas)`; vista `radian-events` (`onlyPending`). Shared
  `Services/Compras/ComprasClient` (+ `.Recepciones`, `.Facturas`, `.Devoluciones`; `Rutas`), `Models/Compras/ComprasDtos`
  (ClasesDeCompra, EventosRadian, DocumentoDelProveedorRequest, LineaDeCompraRequest, BorradorDeCompraRequest,
  FacturaDeCompraDirectaRequest, CompraDirectaRequest, EventoRadianDto, RegistrarEventoRadianRequest, InfoDelProveedorDto,
  SaldoDeLineaDeRecepcionDto, DocumentoDeCompraDto, ResumenDeRecepcionDto, ResumenDeFacturaDeProveedorDto, DocumentoCreadoDto,
  FacturaCreadaDto, ResultadoDeCompraDirectaDto, PrellenadoDeFacturaDto, ProveedorDelXmlDto, LineaDelXmlDto, ImpuestoDelXmlDto,
  TotalesDelXmlDto, FiltroDeCompras), en `InventarioDtos` `DocumentoDeInventarioDto.{Links, TaxLines}`,
  `LineaDeDocumentoDto.Links`, `VinculoDeDocumentoDto`, `RenglonDeImpuestoDto`, `VinculoDeLineaDeInventarioDto`;
  `Components/Compras/{SelectorDeProveedor, ImpuestosDeCompra}` y `Pages/Compras/{Recepciones, Recepcion, CompraDirecta,
  FacturasProveedor, FacturaProveedor, NotasProveedor, Devoluciones}.razor` con el grupo **Compras** del menú. Pruebas
  `Domain.Tests/Inventory/Purchasing/{TransicionesDeEventoRadianTests, DiferenciaDePrecioTests}`,
  `Application.Tests/Inventory/Purchasing/{ComprasDePrueba (+ AlertasDePrueba), RecepcionDeCompraTests, FacturaDeProveedorTests,
  NotaDeProveedorTests, DevolucionAProveedorTests, CompraDirectaCommandTests, EventosRadianTests, LectorDeFacturaUblTests}` y sus
  `Muestras/*.xml`.
- Traslados en dos pasos (fase 9, US10, T359–T381; todos **(nuevo)** salvo los de data-model y api.md): Domain
  `Entities/Inventory/Documents/TransferDiscrepancy` (`EstadoPendiente/EnAprobacion/Resuelta`, `ResolucionesAdmitidas`, `ExigeCausa`,
  `Admite`, `Pendiente()`, `PedirResolucion`, `VolverAPendiente`, `Resolver`; columnas nuevas `ResolvedQuantityBase` —lo ya resuelto
  por aprobaciones parciales— y `ResolutionQuantityBase` —la cantidad pedida—). Persistence
  `Configurations/Inventory/Documents/TransferDiscrepancyConfiguration` (`UK_INV_TransferDiscrepancies_Receipt_DispatchLine_Kind`,
  `IX_INV_TransferDiscrepancies_ResolvedAt`, `IX_INV_TransferDiscrepancies_DispatchDocumentId`) en `NucleoComercialSinMigracion`;
  `DbSet` `TransferDiscrepancies`; la semilla `InventoryDocumentTypesSeeder.{PermisoDeAprobacionDeDiferencias,
  MotivoDeLaPoliticaDeDiferencias}`. Application: `MovimientoDeKardex.AlCostoDe` (una entrada al costo de la salida de la misma
  llamada: tránsito y cambio de ubicación; en el mismo ámbito no mueve el último costo); `Documents/Efectos/{EfectoDespachoDeTraslado,
  EfectoRecepcionDeTraslado, EfectoMovimientoEntreUbicaciones, ReglasDeTraslado (ReversionNeutraAsync: la anulación de un cambio de
  lugar sin VoidDifference)}`; en `EfectoDeAjuste` los ganchos `MovimientosAsync` y `AdmiteTransitoAsync` (sólo `EfectoDeBaja` admite
  el tránsito, cuando resuelve un faltante `WriteOffFromTransit`, y sale al costo del despacho); `VistaDeDocumentos.{AccionDespachar
  = "Dispatch", AccionRecibir = "Receive", PermisoDeRecibir}`; `EmisionDeInventario.{TrasladoDespachadoAsync,
  TrasladoRecibidoAsync}`; `Inventory/Transfers/{ErroresDeTraslados (CampoDestino = "destinationWarehouse", CampoUbicacionDeDestino =
  "toLocation"), TransferDtos (EstadosDeTraslado, TransferSummaryDto, TransferLineDto, TransferDto, TransferRefDto,
  ResolvingDocumentDto, TransferDiscrepancyDto, DiferenciaCreadaDto, ReceiveTransferResultDto, ResolveTransferDiscrepancyResultDto,
  TransferDestinationDto), DispatchTransferCommand, ReceiveTransferCommand (+ LineaRecibidaRequest), ResolveTransferDiscrepancyCommand,
  CierreDeDiferencias, FuenteDeAprobacionDeDiferencia (SourceType TransferDiscrepancy), TransferQueries (VistaDeTraslados,
  ListTransfersQuery, GetTransferQuery, ListTransferDiscrepanciesQuery)}`; `Inventory/Warehouses/ListTransferDestinationsQuery`
  (`Proposito = "TransferDestination"`). Vínculo: la baja desde el tránsito consume la línea de despacho con `ReceiptOf` (lo que sale
  del tránsito deja de contar «en tránsito»; la clase del destino dice qué fue); la devolución al origen, con `ReturnOf`. API
  `Endpoints/Inventory/TransfersEndpoints` (+ `RecibirTrasladoRequest`, `ResolverDiferenciaRequest`; ruta auxiliar
  `GET /transfers/destinations` con `Transfers.Create`, la misma consulta que `GET /warehouses?purpose=TransferDestination`). Shared
  `InventarioClient.Traslados` (`ClaseDespachoDeTraslado`), `InventarioDtos.Traslados` (FiltroDeTraslados, ResumenDeTrasladoDto,
  LineaDeTrasladoDto, TrasladoDeLaDiferenciaDto, DocumentoQueResuelveDto, DiferenciaDeTrasladoDto, TrasladoDto, LineaRecibidaRequest,
  RecibirTrasladoRequest, DiferenciaCreadaDto, ResultadoDeRecepcionDto, ResolverDiferenciaRequest, ResultadoDeResolucionDto,
  DestinoDeTrasladoDto, SucursalDeDestinoDto), `TextosDeInventario.{TiposDeDiferencia, ResolucionesDeDiferencia, EstadosDeTraslado,
  EstadosDeDiferencia, DiferenciaFaltante, ResolucionBajaDesdeTransito, ResolucionAjusteDeSobrante}` y las páginas
  `Pages/Inventario/{Traslados (/inventario/traslados), Traslado (/inventario/traslados/nuevo, /inventario/traslados/{id:guid})}.razor`
- Conteos físicos (fase 10, US11, T382–T405; todos **(nuevo)** salvo los de data-model y api.md): Domain
  `Entities/Inventory/Documents/{CountSnapshotLine (MarcarRonda, Cerrar), CountCapture (IHechoInmutable)}` y el motor
  `Inventory/Counts/ComparacionDeConteo` (`ToleranciaDeReconteo`, `LineaDeFoto<T>`, `CapturaDeConteo<T>`,
  `MovimientoPosteriorALaFoto<T>`, `PedidoDeComparacion<T>`, `ComparacionDeLinea<T>`, `PrimeraRonda`, `RondaDeReconteo`,
  `FueraDeTolerancia`). Persistence `Configurations/Inventory/Documents/{CountSnapshotLineConfiguration
  (UK_INV_CountSnapshotLines_Location, _Location_Lot, IX_INV_CountSnapshotLines_Product_Document), CountCaptureConfiguration
  (IX_INV_CountCaptures_Document_Round_Line)}` en `NucleoComercialSinMigracion`; `DbSet` `CountSnapshotLines`, `CountCaptures`;
  `PedidoDeCerrojo.BodegasEnExclusivo` y `SqlDelCerrojo` `Dialecto.BloquearContraCompartidos` (`XLOCK` en SQL Server); la semilla
  `InventoryDocumentTypesSeeder.{AjustesDeConteo (CONP, CONN), PermisoDeAprobacionDeConteo, MotivoDeLaPoliticaDeConteo}`.
  Application `Inventory/Counts/{ErroresDeConteos (LineaPorRecontar, CampoCriterios), CountDtos (EstadosDeConteo,
  ReglasDeFechaDelAjuste, PhysicalCountRequest, CountSummaryDto, CountCriteriaDto, CountLineDto, CountAdjustmentRefDto,
  PhysicalCountDto, OpenPhysicalCountResultDto, CountReadRequest, RejectedReadDto, CapturedLineDto, CaptureResultDto,
  CountCaptureDto, ClosePhysicalCountResultDto, CountAdjustmentPreviewLineDto, CountAdjustmentPreviewDto, GeneratedAdjustmentDto,
  CountAdjustmentResultDto), VistaDeConteos (CriterioDelConteo, ParametrosDelConteo), BloqueoPorConteo, DetalleDeConteo,
  PhysicalCountDraftCommands (CreatePhysicalCountCommand, UpdatePhysicalCountCommand, ValidadorDeDefinicionDeConteo,
  DefinicionDeConteo), OpenPhysicalCountCommand, CapturePhysicalCountCommand, ClosePhysicalCountCommand, CountAdjustmentCommands
  (ReglaDelAjusteDeConteo con CausaDiferenciaDeConteo = "DIFCONTEO", GetCountAdjustmentPreviewQuery,
  GenerateCountAdjustmentCommand, FechaDelAjusteDeConteo), CountQueries (ListPhysicalCountsQuery, GetPhysicalCountQuery,
  ListCountCapturesQuery)}`, `Documents/Efectos/EfectoConteoFisico`, el gancho `Documents/IAntesDeConfirmarPorAprobacion` (lo llama
  `FuenteDeAprobacionDeDocumento` antes de reentrar), `Reports/CountDifferencesReportQuery`; parámetros opcionales nuevos
  `ReglasDelDocumento.Evaluar(claseDelOriginal)`, `ConfirmacionDeDocumento(bloqueoPorConteo)` y
  `SaveInventoryDraftCommandHandler(bloqueoPorConteo)`; `AjusteInventarioAprobadoV1.SourceDocument` = el conteo. API
  `Endpoints/Inventory/CountsEndpoints` (+ `CapturarRequest`, `AjusteRequest`) y la vista `count-differences`. Shared
  `InventarioClient.Conteos` (`ClaseConteoFisico`, `ListarPersonasParaContarAsync`), `InventarioDtos.Conteos`,
  `TextosDeInventario.{TiposDeConteo, AlcancesDeConteo, EstadosDeConteo, AlcancesDeConteoDeI1}` y las páginas
  `Pages/Inventario/{Conteos (/inventario/conteos), Conteo (/inventario/conteos/{id:guid})}.razor`. Pruebas
  `Domain.Tests/Inventory/Counts/{ComparacionDeConteoTests, Casos/*.json}` y `Application.Tests/Inventory/Counts/{ConteosDePrueba,
  AbrirConteoTests, BloqueoPorConteoTests, CapturarConteoTests, CerrarYAjustarConteoTests, DescartarYAnularConteoTests}`.
  con su enlace «Traslados» en el grupo **Inventario**. Pruebas `Domain.Tests/Inventory/Transfers/TransferDiscrepancyTests`,
  `Application.Tests/Inventory/Transfers/{TrasladosDePrueba, DespachoDeTrasladoTests, RecepcionDeTrasladoTests,
  ResolverDiferenciaDeTrasladoTests, AnulacionYUbicacionesDeTrasladoTests, AlcanceDeTrasladosTests}`.
- Puesta en marcha (fase 7, US4, T297–T324; todos **(nuevo)** salvo los de data-model y api.md): Domain
  `Entities/Inventory/GoLive/{WarehouseActivation (LargoDelMotivo), LegacyFigure (+ AccountingGroupId, columna nueva)}` y en
  `Warehouse` los métodos `FijarFechaDeCorte(DateOnly, bool conSaldoConfirmado = false)`, `Activar(DateOnly, int, DateTimeOffset)`
  y `EstaActiva` (las cuatro columnas de la activación con `init` y campo de respaldo: sólo cambian por esos métodos);
  Persistence `Configurations/Inventory/GoLive/{WarehouseActivationConfiguration (UK_INV_WarehouseActivations_Warehouse filtrado),
  LegacyFigureConfiguration (IX_INV_LegacyFigures_AsOf_Warehouse, IX_INV_LegacyFigures_Batch)}` en `NucleoComercialSinMigracion`;
  `DbSet` `WarehouseActivations`, `LegacyFigures`. Application `Inventory/GoLive/{GoLiveErrors (+ SaldoConfirmado),
  PlantillasDePuestaEnMarcha (PlantillaDeSaldoInicial, PlantillaDeCifrasDeSolido), ImportOpeningBalanceCommand (+
  ResumenDeSaldoInicialDto, ValorPorGrupoDto, DocumentoDeSaldoInicialDto, BodegaDeSaldoInicialDto; `ExtraPorBodega = "byWarehouse"`,
  `ExtraDocumentos = "documents"`, `MotivoDeDescarte`), ImportLegacyFiguresCommand (+ CifraPorFechaBodegaGrupoDto; `ExtraLote`,
  `ExtraReemplazados`, `ExtraPorFechaBodegaGrupo`), LegacyFiguresQueries (ListLegacyFigureBatchesQuery, ListLegacyFigureRowsQuery,
  LoteDeCifrasDto, FilaDeCifraDto), PuestaEnMarchaOptions, ActivateWarehouseCommand (+ GetWarehouseActivationPreviewQuery,
  ActivationPreviewDto, BodegaDeActivacionDto, SaldoInicialDeActivacionDto, DocumentoDeActivacionDto, ConjuntoDeCuentasDto,
  CuentaDelConjuntoDto, BloqueoDeActivacionDto, ActivationResultDto; `PermisoDeAceptarDiferencia`, `PrefijoSinComparacion`), y
  ComparacionDeActivacion (el cálculo común de la vista previa y la activación; `ConjuntosAsync` lo llena US7 con
  IContabilidadParaInventario)}`; `Inventory/Documents/Efectos/EfectoSaldoInicial`; `EmisionDeInventario.SaldoInicialCargadoAsync`;
  `Inventory/Reports/ComparativosConSolidoQueries` (`LegacyComparisonKardexQuery`, `LegacyComparisonValuationQuery`);
  `ReglasDelDocumento` suma «saldo inicial en bodega activa → `Inventory.OpeningBalance.WarehouseActive`». API
  `Endpoints/Inventory/GoLiveEndpoints`, en `WarehousesEndpoints` las rutas `/{id}/activation` (`Activar`, `ActivacionRequest`), las
  vistas `legacy-comparison-kardex` y `legacy-comparison-valuation` y `PuestaEnMarchaOptions` en `Program.cs`. Shared
  `InventarioClient.PuestaEnMarcha` (con sus DTO espejo), `ResultadoDeImportacionDto.{Extra, ExtraComo}`,
  `ImportarPlantilla.{Revisado, ConDatos}`, `Components/Inventario/ActivacionDeBodegaDialog.razor` y las páginas
  `Pages/Inventario/{SaldoInicial, CifrasSolido}.razor` con sus enlaces del menú. Guion `database/migration/solido-cifras-inventario.sql`.
  Pruebas `Application.Tests/Inventory/GoLive/{PuestaEnMarchaDePrueba, ImportOpeningBalanceCommandHandlerTests,
  SaldoInicialConfirmacionTests, ConvivenciaDeBodegasTests, ImportLegacyFiguresCommandHandlerTests,
  ActivateWarehouseCommandHandlerTests}` y `Reports/ComparativosConSolidoTests`.
- Comandos: `SaveInventoryDraftCommand`, `ConfirmInventoryDocumentCommand(DocumentPublicId,
  ExpectedGroup)`, `VoidInventoryDocumentCommand`, `DiscardInventoryDraftCommand`,
  `DispatchTransferCommand`, `ReceiveTransferCommand`, `ResolveTransferDiscrepancyCommand`,
  `OpenPhysicalCountCommand`, `CapturePhysicalCountCommand`, `ClosePhysicalCountCommand`,
  `ConfirmDirectPurchaseCommand`, `ClosePurchaseOrderBalanceCommand` **(nuevo, I5; data-model §9.8)**, `RegisterExternalRadianEventCommand`, `CloseInventoryPeriodCommand`,
  `ReopenInventoryPeriodCommand`, `ImportOpeningBalanceCommand`, `ImportLegacyFiguresCommand`,
  `ActivateWarehouseCommand`, `ChangeProductAccountingGroupCommand`, `RebuildInventoryProjectionsCommand`,
  `Import{Products,ProductCategories,Brands,UnitsOfMeasure,AccountingGroups,Warehouses,DocumentTypes,
  Salespeople,PointsOfSale,PriceLists,DiscountCaps}Command` (todos con `ModoDeImportacion`),
  `CreateSalespersonCommand` (restaura o crea), `DeleteSalespersonCommand`; POS:
  `CreatePosDraftCommand`, `AddPosLineCommand`, `UpdatePosLineCommand`, `RemovePosLineCommand`,
  `SuspendPosDraftCommand`, `ResumePosDraftCommand`, `DiscardPosDraftCommand`,
  `CheckoutPosDraftCommand`, `ReprintDocumentCommand`, `OpenCashSessionCommand`,
  `CloseCashSessionCommand`, `ExecuteDayCloseCommand`, `ReopenDayCloseCommand`; consultas
  `SearchProductsQuery`, `LookupPosProductQuery`, `ResolvePriceQuery`, `GetCashSessionExpectedQuery`,
  `VerifyInventoryIntegrityQuery`.

**Core**: `Domain/Taxes/{MotorTributario, IvaDescontable, ConversionUvt, TaxCatalogSnapshot,
PerfilTributario}`, `Application/Core/Taxes/{LectorDeCatalogoTributario, ImportTaxCatalogCommand}`,
`Application/Core/PaymentMeans/*`.
  Catálogo tributario (fase 3, sección tributaria, T161–T171; todos **(nuevo)**): en `Domain/Taxes`, la entrada y la
  salida del motor —`EntradaTributaria`, `LineaTributaria`, `ImpuestoDeLinea`, `RenglonTributario`, `ResultadoTributario`
  (con `Rechazos` y `Omisiones`), `RechazoTributario`, `ExplicacionTributaria` + `PasoTributario`—, lo que lleva la foto
  —`ImpuestoEnFoto`, `TarifaEnFoto`, `ConceptoEnFoto`, `CondicionesDeTarifa`—, `RedondeoUvt { Peso, Centena, Mil }`,
  `IvaDescontable.Explicar`, `PerfilTributario.EsAgenteDeRetencion` y las constantes `MotorTributario.{CodigoAmbiguo,
  CodigoTarifaNoVigente, CodigoImpuestoInexistente, ActividadGeneral}`; en `Domain/Entities/Core/Taxes`, `TaxDefinition`,
  `TaxRate` (`VigenteEn`, `SeCruzaCon`), `WithholdingConcept`; en `Application/Common/Taxation`, `UvtVigente` y
  `LectorDeUvt.{CodigoFaltante, Faltante}`; en `Application/Core/Taxes`, `TaxErrors`, `ReglasDelCatalogoTributario`
  (+ `IDatosDeTarifa`, `CampoDeTarifa`), `PlantillaDeImpuestos` (la definición de la plantilla 1 con sus columnas, que
  `CatalogoDePlantillas` toma), los comandos `Create/UpdateTaxDefinitionCommand`, `Create/UpdateTaxRateCommand`,
  `Create/UpdateWithholdingConceptCommand`, `CloseTaxRateCommand`, `ReviewTaxRateCommand`, las consultas
  `ListTaxDefinitionsQuery`, `GetTaxDefinitionQuery`, `ListTaxRatesQuery`, `GetTaxRateQuery`,
  `ListWithholdingConceptsQuery`, `GetTaxCatalogTemplateDataQuery`, y los DTO `TaxDefinitionDto` (con `Rates` en el
  detalle), `TaxRateDto` (con `OtherVersions`), `TaxRateConditionsDto`, `WithholdingConceptDto`; en Persistence,
  `Configurations/Core/Taxes/*Configuration` y `TaxCatalogSeeder` (Order 81, `Data/impuestos-co.json`); en la API,
  `Endpoints/Core/TaxesEndpoints` (tres grupos: `taxes`, `tax-rates`, `withholding-concepts`); en Shared,
  `Services/Core/{ImpuestosClient, ImpuestosDtos}` (`ImpuestoDto`, `TarifaTributariaDto` —ya había un `TarifaDto`
  contable—, `CondicionesDeTarifaDto`, `ConceptoDeRetencionDto`, `CatalogoTributarioTextos` y los *Request) y
  `Pages/Maestros/Impuestos.razor`. Slugs de `BuscarCodigoDeCatalogoQuery`: `impuestos`, `tarifas`,
  `conceptos-de-retencion`.

**Contabilidad** (`Application/Accounting/Inventory`): `Reglas/{OperacionesDeInventario, RolesDeCuenta,
ResolutorDeReglas}`, `CreateInventoryPostingRuleCommand`, `AddInventoryPostingRuleVersionCommand`,
`DeactivateInventoryPostingRuleCommand`, `ImportInventoryPostingRulesCommand`,
`GetInventoryRulesTemplateQuery`, `SetInventoryVoucherMappingCommand`;
`Contabilizacion/{ConstructorDeLineasDeInventario, DestinoContabilidad, VersionesAceptadas}`,
`PostInventoryMessagesCommand` (unidad por documento), `PostInventorySummaryGroupCommand` (grupo
resumido); `Lotes/AgrupadorDeResumidos`; `Consultas/{EvaluateInventoryPostingQuery,
InventoryAccountBalancesQuery, InventoryRulesCompletenessQuery, PreviewInventoryBatchQuery,
PendingInventoryMessagesQuery}`, adaptador `ContabilidadParaInventario`. Cambios a la 009:
`AccountingPoster.ValidarVariosAsync` (sin seguimiento), guarda INV en `PrepareReversalAsync`,
`PostingRequest.RegistradoPor` (`UsuarioDeOrigen?`), `ClosePeriodCommand.AcknowledgeInventoryPending`,
sobrecarga de `MovimientosContables.PrepararAsync` con alcance explícito, `EnlacesDeOrigen`
(`InventoryDocument`, `InventoryPostingBatch`), `AccountReferenceFinder`, `ListInvalidParameterizationsQuery`.

**Facturación electrónica**: `Application/ElectronicInvoicing/Channels/{ICanalDeEmisionElectronica,
ICanalesDeEmision, ICredencialesDeCanal, ResultadoDeCanal, CapacidadesDelCanal}`,
`Canonical/{DocumentoElectronicoCanonico (v1), ConstructorDelCanonico, IFuenteDeDocumentoElectronico}`
(Inventario lo implementa con `Application/Inventory/Integration/FuenteDeEmisionDeInventario`),
`Catalogs/CatalogoDian` (nace en la fundacional de I1 con unidades Rec. 20, medios de pago, tipos de identificación y conceptos de corrección, que usan I1 e I3; I4 lo amplía),
`Numeracion/NumeradorFiscal`, `GuardiaDeEmisionFiscal`, `IRepresentacionGraficaRenderer`; Domain
`ElectronicInvoicing/{TransicionesDelDocumentoElectronico, ReglaDeCorreccionFiscal, PlazoDeContingencia}`;
comandos `EmitElectronicDocumentCommand`, `QueryElectronicDocumentStatusCommand`,
`CorrectRejectedDocumentCommand` (caso a), `ReplaceRejectedDocumentCommand` (b),
`CancelRejectedDocumentCommand` (c), `OpenContingencyCommand`, `CloseContingencyCommand`,
`TransmitByCurrentChannelCommand`, `RegisterNumberingResolutionCommand`,
`LinkResolutionToChannelCommand`, `ConfigureEmissionCommand`, `VerifyChannelCredentialCommand`,
`EmitRadianEventCommand` (I5); consultas `GetDianReadinessQuery`, `ListElectronicDocumentsQuery`.

**Seguridad y cumplimiento**: `PerfilesSugeridos` (+ `PerfilSugerido` (nuevo)), `CreateRoleFromTemplateCommand` (+ `RoleFromTemplateDto` (nuevo)),
`ListRoleTemplatesQuery` (nuevo; + `RoleTemplateDto` (nuevo)), `ReglasDeRol` (nuevo; reglas de código y nombre de rol compartidas con `CreateRoleCommandValidator`),
`IAutorizacionDeDatos`, `AutorizacionAlCrear` (en `CreatePersonCommand` y los compuestos
`with-person`), `VerifyAuditIntegrityQuery`, `SelloDeIntegridad`, `AuditOutboxForwarder`.
  Personas y DIVIPOLA (fase 3, sección personas-divipola, T172–T180; todos **(nuevo)** salvo los ya nombrados): en
  `Application/Compliance/HabeasData`, `AutorizacionDeDatos` (implementa `IAutorizacionDeDatos`; `ResolverAsync`,
  `AgregarConsentimiento`, `DejarConstanciaSinPoliticaAsync`, constantes `SinPoliticaVigente`, `PoliticaDesconocida`,
  `PoliticaRequerida`, `CanalPorDefecto`), `AutorizacionResuelta`, `AutorizacionVigente`, `DecisionDeAutorizacion
  { Accepted, Declined }`, `AutorizacionAlCrearValidator`, `AccionesDeConsentimiento { Accepted, Revoked, Declined }`,
  `PoliticaVigenteDto`, `GetCurrentPolicyQuery`; en `Application/Core/People/Services`, `AltaConAutorizacion`
  (+ `AltaRealizada<T>`; el único que guarda un alta: `CreatePersonCommand` y los dos compuestos `with-person` lo usan)
  y `PersonFactory.AplicarPerfilTributario`; `PersonInputValidator.PatronDeCiiu`; en `Application/Core/Branches`,
  `MunicipioDeSucursal` (`Patron`, `ValidarAsync`, `Desconocido`); `AuditEventTypes.PersonDataAuthorizationNoCurrentPolicy`
  (`Person.DataAuthorization.NoCurrentPolicy`, la constancia «sin política vigente»); en
  `Application/ElectronicInvoicing/Catalogs`, `CatalogoDian` (`Embebido`, `DesdeJson`, `Catalogos`) con `CodigoDian`,
  `ConsumidorFinalDian`, `ConceptoDeCorreccionDian`, `ClaseDeNotaDian { NotaCredito, NotaDebito, NotaDeAjustePos,
  NotaDeAjusteDelDocumentoSoporte }` y los JSON `Catalogs/Data/{unidades-rec20, medios-de-pago,
  tipos-de-identificacion, conceptos-de-correccion}.json`; en Persistence, `DivipolaSeeder` (Order 82,
  `Data/divipola.json`, `SemillaDivipola`, `EntradaDivipola`, `Normalizar`); en Shared, `SeccionDePersona.DatosTributarios`,
  `AutorizacionAlCrearDto`, `PoliticaDeDatosVigenteDto`, `PersonasClient.PoliticaDeDatosVigenteAsync`,
  `[Parameter] CanalDeAutorizacion` de `PersonaDialog` y la clase CSS `.texto-politica`.

**API y Shared**: `API/Integration/{DespachadorDeMensajes, EjecutorEnCooperativa, ProgramadorDeTareas,
SenalDeMensajes, IntegrationOptions}`; `API/Services/{ActorDeLaPeticion, OrigenDeLaPeticion,
AlcanceDeInventarioDeLaPeticion}`; `Shared/Services/Http/CanalDeOrigenHandler` (cabecera `X-Canal`),
`Shared/Services/IImpresionDeDocumentos`, clientes `InventarioClient` (+ parciales), `ComprasClient`,
`VentasClient` (`.Pos`, `.Caja`, `.Precios`, `.Documentos`), `ImpuestosClient`, `MediosDePagoClient`,
`FacturacionElectronicaClient`, `IntegracionContableClient`; `wwwroot/js/pos.js`.
  Centro de informes y base de Shared (fase 3, sección informes-shared, T181–T185; todos **(nuevo)**): en
  `Application/Inventory/Reports`, `InventoryAuditEmitter` (`Modulo`, `EmitAsync`, `EmitirExportacionAsync`,
  `EmitirExportacionDeCatalogoAsync`; por la bandeja encadenada, no directo a Mongo), `FiltrosDeInformeDeInventario`
  (`Desde`, `Hasta`, `ALaFecha`, `ValidarRango`, `ParaAuditoria`, `RangoMaximoEnAnios`, `RangoInvalido`,
  `RangoDemasiadoLargo`) y `VistaDeInformeDeInventario` (`Key`, `Name`, `Description`, `FileName`, `Filters`, `OwnFilters`,
  `PersonalData`, `PersonalDataWhen` —`parametro=valor`—, `RequiredPermission`, `TraeDatosPersonales`);
  `AuditEventTypes.{InventoryReportExported, InventoryCatalogExported}`; en la API, `InventoryReportsEndpoints.Ruta` y
  `MapVistas` (donde cada historia registra sus vistas) y `InventoryReportsRoutes.MapVistaDeInventario` (+ `PermisoDeVer`,
  `PermisoDeExportar`, `PermisoDeDatosPersonales`): **la exportación la audita la ruta**, con las filas de la tabla, y las
  consultas de las vistas no la repiten; el **registro de vistas** es `GET /api/reports/inventory` (lee la metadata de las
  rutas publicadas). En Shared, `Services/Inventario/{InventarioClient (+ .Documentos, .TiposDeDocumento, .Plantillas,
  .Informes), InventarioDtos, ResultadoDeInventario, TextosDeInventario, FiltrosDeInformeDeInventarioModelo}` —
  `InventarioClient.RutasDeGrupo`, `RutaDeTipos`, `RutaDeInformes`; DTO `ReferenciaDeInventarioDto`,
  `UsuarioDeInventarioDto`, `ContraparteDeInventarioDto`, `DocumentoReferidoDeInventarioDto`, `AvisoDeInventarioDto`,
  `PaginaDeInventarioDto<T>`, `MotivoDeInventarioRequest`, `ClaseDeDocumentoDto`, `TipoDeDocumentoDto`,
  `CamposObligatoriosDelTipoDto`, `ConsecutivoDelTipoDto`, `NivelDePoliticaDelTipoDto`, `PoliticaDelTipoDto`,
  `ModoDePasoDelTipoDto`, `CrearTipoDeDocumentoRequest`, `EditarTipoDeDocumentoRequest`, `ConsecutivoRequest`,
  `FiltroDeDocumentosDeInventario`, `ResumenDeDocumentoDto`, `LineaDeDocumentoDto`, `UnidadDeLineaDto`,
  `TotalesDeDocumentoDto`, `DocumentoDeInventarioDto`, `LineaDeBorradorRequest`, `BorradorDeInventarioRequest`,
  `NivelPedidoDeInventarioDto`, `AprobacionPedidaDeInventarioDto`, `ResultadoDeConfirmacionDto`,
  `ResultadoDeAnulacionDto`, `AnulacionRequest`, `ConfirmacionRequest`, `PlantillaDeParametrizacionDto`,
  `ResumenDeHojaDto`, `CampoCambiadoDto`, `CambioDeFilaDto`, `ErrorDeImportacionDto`, `ResultadoDeImportacionDto`,
  `VistaDeInformeDto`—; `Components/Inventario/ImportarPlantilla.razor` (el único de importación) y las páginas
  `Pages/Inventario/{Informes, TiposDeDocumento}.razor`. Pruebas: `InventoryAuditEmitterTests`,
  `FiltrosDeInformeDeInventarioTests`, `VistaDeInformeDeInventarioTests`, `InventarioClientTests`,
  `FiltrosDeInformeDeInventarioModeloTests`, `TextosDeInventarioTests` (arquitectura) y
  `LosEndpointsProtegidosExigenPermiso.Las_vistas_de_inventario_exigen_ver_y_exportar`.
- Plantillas 2 a 7, API y pantallas del catálogo (fase 4, US1, T194–T239; todos **(nuevo)**): Application
  `Inventory/Imports/PlantillasDelCatalogo` con `PlantillaDeGruposContables`, `PlantillaDeUnidades`, `PlantillaDeMarcas`,
  `PlantillaDeCategorias`, `PlantillaDeProductos`, `PlantillaDeBodegas` (hojas, columnas y etiquetas de §2–§7) y
  `PlantillasQueSeImportanConI3` (columnas de §10–§13, sólo descarga en I1); los comandos `Import{AccountingGroups,
  UnitsOfMeasure, Brands, ProductCategories, Products, Warehouses}Command` con sus `Get…TemplateDataQuery` (la descarga con
  datos), `ImportProductsCommandHandler.AvisoDeDigitoDeControl` (`Import.Barcode.CheckDigit`) y `.DigitoDeControlErrado`,
  `ImportWarehousesCommandHandler.ExtraDeTransito` (clave `transitWarehouses` de `ImportResultDto.Extra`, con
  `BodegaDeTransitoPropuestaDto { branch, code, name, fromFile }`: la revisión anuncia la bodega de tránsito que nace);
  `ReglasDeProducto.Aplicar` (las mismas reglas con las referencias resueltas en bloque, `ReferenciasDeProducto`);
  `EjecutorDeImportacion.EjecutarAsync(…, despuesDeGuardar)` (lo que necesita los Id recién asignados —la ruta de una
  categoría nueva, la vigencia de una bodega nueva— se escribe tras el primer guardado, en la misma transacción);
  `AddParameterVersionCommandHandler.AgregarVigenciasAsync`/`CruceAsync` (el alta de vigencias sin guardar, que reusa la
  plantilla 7 para `stockNegativo`: el handler sigue siendo el único escritor de `COR_ParameterVersions`). API
  `Endpoints/Inventory/{CatalogEndpoints, WarehousesEndpoints, PointsOfSaleEndpoints, PricingEndpoints}` y
  `Endpoints/Core/PaymentMeansEndpoints` (los tres últimos, en I1, sólo la descarga vacía de las plantillas 10 a 13). Shared
  `Services/Inventario/{InventarioClient.Catalogo, InventarioClient.Bodegas, InventarioDtos.Catalogo}` (`ProductoElegido`),
  `Components/Inventario/SelectorDeProducto.razor` (el campo de producto con lector) y `CatalogoDeInventario.razor`
  (+ `CampoDeCatalogo`, `FilaDeCatalogo`, `TipoDeCampoDeCatalogo`: el catálogo simple que usan unidades, categorías, marcas,
  grupos, causas y canales), `ImportarPlantilla.Importable`, las páginas `Pages/Inventario/{Productos, ProductoDetalle,
  Categorias, Marcas, Unidades, GruposContables, CausasDeAjuste, Bodegas, BodegaDetalle, Plantillas}.razor` y
  `Pages/Ventas/Canales.razor`; `TextosDeInventario.{ClasesDeProducto, EstadosDeProducto, TratamientosDeIva, UsosDeUnidad,
  ComportamientosDeBodega, ActivacionesDeBodega}`. Pruebas: `ImportProductsCommandTests`, `ImportCatalogosTests`, las e2e
  `CatalogoYBodegasTests` y `Ventas/BusquedaDeProductos50kTests` (con `FactDeRendimientoAttribute`: sin
  `RUN_PERF_TESTS=1` se reporta omitida, nunca aprobada).
- Reorden y vistas básicas (fase 21, US17 parte I1, T942–T958; todos **(nuevo)**): Domain
  `Inventory/Replenishment/CalculoDeReposicion` (`Calcular(EntradaDeReposicion)` → `ResultadoDeReposicion { Posicion,
  RequiereReorden, Sugerido, Quiebre, Alerta }`, `DecimalesDeCantidad = 4`; posición ≤ punto pide reorden, sugerido = máximo −
  posición sólo entonces, quiebre = disponible **<** mínimo). Application `Inventory/Replenishment/EvaluacionDeReposicion`
  (+ `FilaDeReposicion`; la única que pone la posición de `PosicionDeReposicion` y el cálculo a una política; la comparten el
  aviso, la revisión y la vista), `AlertasDeReposicion` (`ClaveDeLaAlerta` = `{TypeCode}:{productoPublicId}:{bodegaPublicId}`,
  `De(fila)` → `Inventario.Reorden` y/o `Inventario.Quiebre` con `ScopeWarehousePublicId`), `AvisoDeReposicionAlConfirmar`
  (`CodigoDelAviso = Inventory.Stock.BelowReorderPoint`; lo llama `ConfirmacionDeDocumento` —parámetro opcional
  `avisoDeReposicion`— después del único `SaveChanges`, sobre las salidas del kardex del documento, y levanta por `IAlertas` en la
  misma transacción; nunca bloquea), `RevisionDeReorden` (+ `ResultadoDeRevisionDeReorden`, `Tanda = 500`; políticas de bodegas
  operativas, activadas y activas; levanta por `RaiseAlertCommand`) y la tarea `TareaDeRevisionDeReorden`
  (`inventario.reorden`, una vez al día desde `Integration:ReorderReview:StartHour`, por defecto 5; registrada en `Program.cs`;
  `IntegrationOptions.ReorderReviewOptions`); `Inventory/Reports/Vistas/{DocumentsReportQuery, ReorderAlertsReportQuery}` con
  su `Vista` estática (lo que la ruta publica) y `VistaDeInformeDeInventario.PersonalDataColumn` +
  `TablaTraeDatosPersonales(tabla)`: exportar `documents` exige `Inventory.Reports.ExportPersonalData` **cuando alguna fila trae
  contraparte**, lo decide `MapVistaDeInventario` después de consultar (mismo 404, sin auditar). Shared
  `Services/Inventario/DestinoDeDocumentoDeInventario` (`Documento(clase, id)`, `Kardex(producto, bodega)`: la profundización
  de `/inventario/informes`) y `VistaDeInformeDto.PersonalDataColumn`. Pruebas: `Domain.Tests/Inventory/Replenishment/
  {CalculoDeReposicionTests, Casos/*.json}`, `Application.Tests/Inventory/Replenishment/{ReposicionDePrueba,
  AvisoDeReposicionAlConfirmarTests, RevisionDeReordenTests}`, `Application.Tests/Inventory/Reports/VistasBasicasTests` y
  `Shared.Tests/Inventario/DestinoDeDocumentoDeInventarioTests`; las e2e `ReordenYQuiebreTests` e `InformesDeInventarioTests` se
  escriben y corren en el cierre de I1.

### 2.17 Códigos de error principales (familias)

`Operation.KeyRequired` (400), `Operation.KeyReused` (422) y cabecera `Idempotent-Replayed: true` ·
`Inventory.Stock.Insufficient` (`data.available`) · `Inventory.Period.Closed` ·
`Inventory.Warehouse.NotActive` · `Inventory.Document.HasDependents` (`data.dependents`) ·
`Inventory.Document.AlreadyVoided` · `Inventory.Document.FiscalUseCorrection` ·
`Inventory.PostingMode.ChainMismatch` · `Inventory.PostingMode.FiscalRequiresConfirmation` ·
`Inventory.Approval.AmountExceedsLimit` (`data.maxAmount`) · `Inventory.Unit.DecimalsNotAllowed` ·
`Inventory.Barcode.Duplicate` · `Inventory.Numbering.ResolutionUnavailable` ·
`Inventory.PurchaseOrder.NotOpen` **(nuevo)** (cerrar el saldo de una orden que no está abierta) ·
`Inventory.Prevalidation.NotPostable` (`data.errors[] {lineNumber, account, rule, whoFixes}`) ·
`Payments.TotalMismatch` · `Payments.VoucherAlreadyUsed` · `Approvals.SelfApprovalForbidden` ·
`Parameters.ValueNotAllowed` · `Parameters.Overlaps` · `Parameters.RequiresPeriodStart` ·
`Integration.VersionNotAccepted` · `Accounting.InventoryRule.{Missing, Overlaps,
RetroactiveOverPosted, TaxRateMismatch}` · `Accounting.InventoryMessage.{Unbalanced,
CurrencyNotSupported, WaitingForOriginal}` · `Accounting.Period.InventoryPending` ·
`Accounting.Document.InventoryCorrectsWithNewVoucher` · `ElectronicInvoicing.{NotReady,
CredentialMismatch, Document.NotRejected, Document.AwaitingResponse, Resolution.Exhausted,
Resolution.Expired}` · `Catalogo.CodigoDuplicado` (existente). Lo que falla por alcance o permiso
responde el 404 genérico (salvo la excepción de §2.9: 422 con código propio para permisos que dependen
del cuerpo).

Nombres fijados en la revisión de coherencia (cada regla tiene **un** código en todos los artefactos):
fecha anterior al inicio → `Inventory.Document.DateBeforeCutoff`; movimiento bloqueado por un conteo
abierto → `Inventory.Count.ProductsLocked`; clase que no corresponde a la ruta →
`Inventory.Document.TypeNotForRoute` (`data { class, group }`); alta manual de tránsito →
`Inventory.WarehouseType.TransitIsSystem`; activación sin consulta de saldos (en producción, antes de I2) →
`Inventory.Activation.AccountingUnavailable`; importación que no aplica → `Import.Invalid` (y
`Import.Cell.PermissionRequired`, `Import.Cell.Required`, `Archivo.HojaFaltante`; `contracts/plantillas.md`);
caja → `Inventory.CashSession.HasOpenDrafts`, `.NotOpen`, `.HasPendingMovements`,
`Inventory.CashMovement.DestinationRegisterClosed`, `Payments.CardNumberNotAllowed`; tipo de comprobante
de otro módulo → `Accounting.VoucherType.NotAllowedForModule`; cierre contable con mensajes pendientes →
`Accounting.Period.InventoryPending` (`data { pending, inBatch, rejected, oldestOperationDate, types[] }`); RADIAN → `Inventory.RadianEvent.OutOfOrder`,
`.ReceiptNotConfirmed`; DIAN → `ElectronicInvoicing.Document.AwaitingResponse` (**422**: el 409 queda para
`Concurrency.*`), `.EconomicFootprintChanged` (`data.fields[]`), `.MissingData` (`data.missing[] { field,
where, permission }`), `ElectronicInvoicing.NotReady` (`data.missing[]`); catálogo tributario →
`Core.Tax.NotFound`, `Core.TaxRate.{NotFound, Overlaps, InEffect, Ambiguous}`,
`Core.WithholdingConcept.{NotFound, InUse}`, `Core.Tax.Immutable` (nuevo: la plantilla no cambia la clase, la forma de
cálculo ni el impuesto base de un impuesto existente), `Core.TaxRate.MunicipalityUnknown` (nuevo, T176: el municipio de la
tarifa no está en `COR_Cities.DaneCode`; en la plantilla, `Import.Cell.NotFound` en la columna del municipio); personas →
`Person.DataAuthorization.PolicyUnknown` (nuevo: la versión de política no es de la cooperativa) y
`Person.DataAuthorization.PolicyRequired` (nuevo: hay política vigente y la autorización no dice cuál se mostró);
costeo → `Inventory.Costing.RetroactiveNotAllowed` (nuevo, T285: un documento que deja un movimiento con fecha anterior a otro
ya registrado del mismo producto y ámbito, fuera del saldo inicial de bodega no activa y del ajuste de conteo; `data {
lineNumber, productCode, laterMovement { documentPublicId, displayNumber, operationDate } }`); períodos →
`Inventory.Period.NotStarted` (nuevo, T289: cerrar sin `INV_Setup`), `.NotNext` (`data.nextToClose { year, month }`),
`.NotEnded`, `.OpenCounts`, `.WarningsNotAcknowledged` (`data.warnings`), `.UnbilledShipmentsNotAccepted`,
`.AcceptUnbilledNotAllowed`, `.NotLastClosed` (`data.lastClosed`), `.NotClosed`; plantillas → `Import.Cell.Ignored` (nuevo,
T286: aviso de una celda que la clase ignora, como el modo de paso del saldo inicial);
informes de inventario → `Inventory.Report.RangeInvalid` y `Inventory.Report.RangeTooLong` (nuevo, T182: rango al revés o de
más de 5 años, como el `Accounting.Report.RangeTooLong` de la 009); sucursales → `Branch.MunicipalityUnknown`; producto sin concepto de retención (obligatorio salvo plantillas y combos,
data-model §1.6) → `Inventory.Product.WithholdingConceptRequired` (nuevo, T217); vendedores → `Inventory.Salesperson.AlreadyActive`; puesta en marcha (US4) → `Inventory.OpeningBalance.{WarehouseActive,
TransitNotAllowed, AlreadyConfirmed (data.documents[])}`, `Inventory.OpeningBalance.ZeroCost` (nuevo, T308: aviso de fila, costo
unitario cero), `Inventory.LegacyFigures.CodeUnresolved` (aviso), `Inventory.LegacyFigures.GroupMismatch` (nuevo, T311: aviso, el
grupo del archivo no es el del producto a la fecha), `Inventory.Activation.{OpeningBalanceNotConfirmed, CutoffMismatch,
AccountingUnavailable, AlreadyActive, Difference, AcceptDifferenceNotAllowed (data.permissionCode)}`; compras (US9) → `Inventory.Purchase.GoodsWithoutReceipt` (nuevo, T341: una línea de mercancía de la factura sin línea de
recepción; sin recepción sólo van servicios), `Inventory.SupplierInvoice.IssueDateInvalid` (nuevo, T339: emisión posterior a hoy,
data-model §9.2), `Inventory.SupplierNote.{InvoiceFromOtherSupplier, InvoiceNotConfirmed, InvoiceLineRequired}` (nuevos, T342: la
nota va contra una factura confirmada del mismo proveedor, línea por línea); traslados (US10) → `Inventory.Transfer.TransitWarehouseMissing` (nuevo, T367: la sucursal del origen no tiene bodega de tránsito
activa), `Inventory.TransferDiscrepancy.NotFound` (nuevo: 404 de la diferencia, o fuera del alcance) y
`Inventory.TransferDiscrepancy.CauseNotAllowed` (nuevo, T371: la causa no admite bajas desde el tránsito —`AllowsTransitWriteOff`— o
entradas —`AllowsPositive`—; `data { causeCode, resolution }`); conteos (US11) → `Inventory.Count.AlreadyOpen` (nuevo, T392: abrir o editar un conteo con foto) y
`Inventory.Count.RoundNotOpen` (nuevo, T394: ronda 2 sin reconteo pendiente); punto de venta sin POS (`INV_PointsOfSale.PosEnabled = false`) en `POST /pos/drafts`, `GET /pos/lookup` y `resume` → `Inventory.Pos.NotEnabled` (nuevo; FR-058: el punto conserva cajas y sesiones para el cobro de oficina).

### 2.18 Pruebas con nombre fijo

**Arquitectura nuevas** (`tests/IngenIA365ERP.Architecture.Tests/Principles`):
`InventarioNoConoceContabilidadNiCartera` (ArchUnit + regex + lista exacta de
`IContabilidadParaInventario`/`IConsultasDeCartera`: la comprobación de FR-014),
`NingunTrabajoDeFondoOperaSinCooperativa` (todo `BackgroundService` con `ISender` pasa por
`IEjecutorEnCooperativa`, sólo `API/Program.cs` registra `AddHostedService` y ninguno llama a `SaveChangesAsync`: escribe sólo por comandos),
`LosComandosDeConsumoNoTienenRuta`, `LosComandosDeInventarioLlevanClave`,
`LosHechosInmutablesNoSeModifican`, `NadieEscribeElKardexFueraDelRegistro`, `SoloElNumeradorNumera`,
`ElComercioNoTieneValoresLegalesFijos` (Domain/Inventory, Application/Inventory, Domain/Taxes,
Application/Core/Taxes, Domain/ElectronicInvoicing, Application/ElectronicInvoicing),
`LasCantidadesYCostosTienenSuPrecision`, `LoDeInventarioNoSeReversa`, `LaUvtSeLeeEnUnSoloSitio`,
`LosParametrosSeLeenEnUnSoloSitio`, `LosPagosNoGuardanElNumeroDeTarjeta`,
`ElProveedorTecnologicoSoloLoConoceSuAdaptador` (Domain y Application no referencian
`IngenIA365ERP.ElectronicInvoicing` ni un SDK de proveedor; la API lo referencia sólo como raíz de
composición: únicamente `Program.cs` llama `AddElectronicInvoicing()` y ningún otro archivo de la API usa
sus tipos), `LasCredencialesDeFacturacionNoTocanLaBase`,
`LasConsultasDeInventarioRespetanElAlcance`, `LaSegregacionNoUsaUserIdDelToken`.

**Arquitectura ampliadas**: `LosEndpointsProtegidosExigenPermiso` (+ `Endpoints/Inventory/*.cs`,
`Endpoints/ElectronicInvoicing/*.cs`, `Endpoints/Core/Taxes*.cs`, `Endpoints/Core/PaymentMeans*.cs`,
`Endpoints/Reports/Inventory*.cs`), `LasPantallasDicenQueEstanCargando` (+ Inventario, Compras, Ventas,
Pos), `PrincipioXI_ContableImmutable` (+ `Entities/Inventory/Transactions`,
`Entities/Integration/Transactions`, `Entities/Approvals/Transactions`,
`Entities/ElectronicInvoicing/Transactions`), `LaPersonaSeEscribeEnUnSoloSitio` (autorización al crear),
`TodoEnlaceDelMenuTieneSuPagina`, `ManualCatalogoTests`, `PrincipioXII_MigracionesDestructivas`,
`Feature004_MigrationParity`; `NingunModuloEscribeMovimientosFueraDelContrato` y
`LaContabilidadNoTieneCuentasEnCodigo` quedan **sin cambios y verdes**.

**Piezas de las reglas de plataforma (nuevo, T018–T019)**: la ayuda `Helpers/FuenteSinComentarios`
(lee un fuente sin comentarios `/* */` ni líneas `//`, para que un resumen que nombra un tipo no cuente
como uso); en `NingunTrabajoDeFondoOperaSinCooperativa` las listas `TrabajosDeFondo`,
`ExcepcionesSinCooperativa` (`AuditIndexBootstrap`, `DatabaseInitializerHostedService`,
`InvitationExpiryJob`, `PasswordResetTokenCleanupJob`) y `GuardanPorSuCuenta`
(`NotificationEmailDispatcher`, estado técnico del correo; T448 decide si sigue); en
`LosComandosDeInventarioLlevanClave` `CarpetasConClave`, `ConsultasPorPost` (vacía: las consultas por POST
se nombran `*Query`) y `PendientesDeReescritura` (`CreateSalespersonCommand` → T424,
`UpdateSalespersonCommand` y `DeleteSalespersonCommand` → T425, que las sacan de la lista); en
`LosEndpointsProtegidosExigenPermiso` `RutasSueltas` (archivo, ruta, permiso) para
`POST /api/audit/integrity/verify` (`AuditLog.VerifyIntegrity`), porque `AuditLogModule.cs` también
publica `POST /api/audit/access`, abierta a toda sesión.

**Casos dorados (Domain.Tests)**: `Inventory/Costing/Casos/*.json` (01..16 de `research.md` R10, más el
17 «segunda bodega activada después de ventas en la primera, ámbito cooperativa», T18),
`Taxes/Casos/*.json`, `Sales/Pricing/Casos`, `Sales/Cash/Casos`, `Approvals/Casos`,
`Inventory/Purchasing/Casos` (US13-1..3), `Inventory/Costing/Prorrateo`; propiedad
`PropiedadesDelKardexTests`.

**Integración (Testcontainers, PostgreSQL y SQL Server)**: `Integration/{ElProcesoEnSegundoPlanAuditaEnSuCooperativaTests,
EntregaGarantizadaTests, IdempotenciaDeOperacionesTests, LotesProgramadosTests}`,
`Inventory/{ConcurrenciaDeExistenciasTests, CompraDirectaTests, TrasladoEnDosPasosTests,
ConteoYAjusteTests, CierreDePeriodoTests, SaldoInicialYActivacionTests}`,
`Accounting/{ContabilizacionPorMensajesTests, ContabilizacionDeVentasPorMensajesTests}` (la segunda en I3: los casos de I2 que necesitan ventas), `Ventas/{VentaPosCompletaTests, BonoUnicoConcurrenteTests,
UnaSesionPorCajaTests, BusquedaDeProductos50kTests, CreditoProvisionalTests}`,
`Security/{AprobacionMultinivelTests, AlcancePorBodegaTests, IntegridadDeAuditoriaTests}`,
`ElectronicInvoicing/DocumentosElectronicosTests` (con `CanalSimulado`). Las de volumen (SC-001 al pie
de la letra, 50.000 productos, SC-020) reportan **Skip explícito** sin `RUN_PERF_TESTS=1`, nunca un
`return` que cuente como aprobada. Las que cambian todo el libro corren en cooperativas aisladas del
mismo host (patrón `ContabilidadE2E.CooperativaAisladaAsync`), colección «Inventario e2e».
**(nuevo, T186)** `InventarioE2E.TokenMaestroAsync(fx, http)`: un access del maestro por fixture y por diez minutos,
que usa `CooperativaAisladaAsync` (una sesión por alta superaba el límite de 10 inicios de sesión por minuto). La
fixture levanta Redis con `--databases 256` (una ranura de caché por cooperativa aislada).
**(nuevo, T186)** `ReintentoPorConcurrenciaBehavior.IndicesDeConsecutivo`: índices únicos de un consecutivo cuyo
choque (`DbUpdateException`) se reintenta como una carrera de `RowVersion`; hoy `UK_ACC_Documents_Type_Number`.

---

## 3. Decisiones transversales (T1..T52)

Formato: **Conflicto** (qué propuso cada tema) · **Decisión** · **Por qué**.

### Nombres, módulo y retiro

**T1 · Idioma de los identificadores.**
Conflicto: mensajería, núcleo y contabilidad nombraron entidades, carpetas y comandos en español
(`MensajeDeIntegracion`, `EntregaDeMensaje`, `LoteDeIntegracion`, `ClaveDeOperacion`,
`RecepcionDeMensaje`, `Common/Ejecucion`, `ContabilizarUnidadDeInventarioCommand`,
`RegistrarResultadoDeEntregaCommand`, `AgregarVigenciaDeParametroCommand`); ventas, DIAN y compras, en
inglés. Decisión: la regla de §2.1. Renombres obligatorios: `MensajeDeIntegracion` →
`IntegrationMessage`; `EntregaDeMensaje` → `IntegrationMessageDelivery`; `DependenciaDeMensaje` →
`IntegrationMessageDependency`; `IntentoDeEntrega` → `IntegrationDeliveryAttempt`; `LoteDeIntegracion`
→ `IntegrationBatch`; `ContadorDeLotes` → `IntegrationBatchCounter`; `ClaveDeOperacion` →
`OperationKey`; `RecepcionDeMensaje`/`ACC_IntegrationReceipts` → `InventoryPosting`/`ACC_InventoryPostings`;
`Contabilizar{Mensaje,Unidad}DeInventarioCommand` → `PostInventoryMessagesCommand`;
`ContabilizarGrupoDeLoteCommand` → `PostInventorySummaryGroupCommand`; `RegistrarResultadoDeEntregaCommand`
→ `RegisterDeliveryResultCommand`; `OrdenarLoteCommand` → `OrderIntegrationBatchCommand`;
`ReprocesarMensajesCommand` → `ReprocessMessagesCommand`; `EnviarMensajesNoAplicaCommand` →
`SendNotApplicableMessagesCommand`; `BandejaDeMensajesQuery` → `ListIntegrationMessagesQuery`;
`VistaPreviaDeLoteQuery` → `PreviewIntegrationBatchQuery`; `AgregarVigenciaDeParametroCommand` →
`AddParameterVersionCommand`; `IdempotenciaBehavior` → `IdempotencyBehavior`; `ReconstruirProyecciones`
→ `RebuildInventoryProjectionsCommand`; `ConfirmarDocumentoCommand` → `ConfirmInventoryDocumentCommand`;
`CerrarPeriodoCommand`/`ReabrirPeriodoCommand` → `CloseInventoryPeriodCommand`/`ReopenInventoryPeriodCommand`;
`EvaluarContabilizacionDeInventarioQuery` → `EvaluateInventoryPostingQuery`;
`SaldosDeCuentasDeInventarioQuery` → `InventoryAccountBalancesQuery`; `CompletitudDeLaMatrizQuery` →
`InventoryRulesCompletenessQuery`; `PrevisualizarLoteDeInventarioQuery` → `PreviewInventoryBatchQuery`;
`MensajesDeInventarioPendientesQuery` → `PendingInventoryMessagesQuery`; carpetas `Common/Ejecucion`,
`Integracion`, `Aprobaciones`, `Alertas`, `Parametros` → `Common/Execution`, `Integration`, `Approvals`,
`Alerts`, `Parameters`. Puertos, motores y servicios conservan su nombre español
(`IEjecutorEnCooperativa`, `IContabilidadParaInventario`, `MotorDeCosteo`…). Por qué: la convención de
la constitución (comandos y consultas «verbo + entidad») y del código (`CloseFiscalYearCommand`,
`ImportAccountsCommand`); un solo criterio evita dos nombres para la misma cosa.

**T2 · Prefijos de tabla: ninguno nuevo.**
Conflicto: `INV_` para todo (núcleo), `SLS_` para ventas y POS (ventas), `FEL_` para facturación
electrónica (DIAN), `INT_` para mensajería (mensajería), `COR_`/`SEC_` para plataforma (seguridad).
Decisión: sólo prefijos de la constitución: `INV_` (todo el módulo comercial, ventas y POS incluidos),
`COR_` (plataforma, catálogos compartidos, facturación electrónica), `ACC_`, `SEC_`. Por qué: la lista
de prefijos está en los estándares de la constitución y el dueño pidió no modificarla; el choque que
temía ventas (`INV_SalesPoints`, `INV_Shifts`, `INV_Prices`) desaparece porque el retiro va antes
(T4) y los nombres nuevos son otros (`INV_PointsOfSale`, `INV_CashSessions`, `INV_PriceLists`); `COR_`
para facturación electrónica y mensajería deja ambas reutilizables por Cartera o Tesorería sin leer
tablas del módulo.

**T3 · Un solo módulo de código «Inventory»; rutas por dueño.**
Conflicto: ventas propuso `Entities/Sales`, `Application/Sales`, `/api/sales/*` y permisos `Sales.*`;
DIAN, `Sales.ElectronicDocuments.*`, `/api/electronic-documents` y `/api/dian/*`; mensajería,
`/api/integration/*`; seguridad y núcleo, todo bajo `Inventory.*`. Decisión: compras, ventas, POS y caja
son subcarpetas del módulo `Inventory` (entidades, Application, rutas `/api/inventory/...`, permisos
`Inventory.*`, módulo de auditoría «Inventory»). La facturación electrónica es módulo de plataforma
`ElectronicInvoicing` (`/api/electronic-invoicing/*`, `ElectronicInvoicing.*`). La bandeja, el reproceso
y el envío posterior son de Inventario (`/api/inventory/messages*`); el lote manual y su vista previa
son de Contabilidad (`/api/accounting/inventory/batches*`). Por qué: `ModuloContable.Inventario = "INV"`
ya es el origen de todo lo comercial, `AuditBehavior` e `AuditableEntityInterceptor` ya infieren
«Inventory» del espacio de nombres y la retención de 10 años se declara una vez; la facturación
electrónica sirve a otros módulos y no debe colgar de ventas. El menú sí separa Inventario, Compras,
Ventas y Punto de venta (T45).

**T4 · Retiro del módulo actual.**
Conflicto: guarda estricta como la 009 (sin aprobación posible) frente a guarda con aprobación por
base (núcleo); una o dos migraciones. Decisión: dos pares en commits separados:
`RetiroDelInventarioHeredado` (destructiva, primero, estado intermedio del modelo sin las 23 entidades
ni las nuevas) y después `InventarioComercialNucleo`. La guarda cuenta filas de las 23 tablas y se niega
nombrando tabla y cantidad (PostgreSQL `DO $$ … RAISE EXCEPTION`, SQL Server `THROW 50012`) salvo que la
base tenga `COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'` con respaldo, revisor y
fecha, insertada a mano después del `pg_dump`. Antes de fusionar se corre
`specs/012-inventario-comercial/diagnostico-inventario-heredado.sql` en DEV, QA y PDN. La operación de
vendedores se muda y endurece antes del retiro (`CreateSalespersonCommand` restaura o crea;
`UK_INV_Salespeople_PersonId` filtrado `[IsDeleted] = 0`). Las tareas de la 009 T120 (parte
Inventario), T125, T128 y T129 (Inventario) quedan marcadas «reemplazadas por la 012». Por qué:
Principio XII, FR-092, y porque reutilizar nombres de tabla en una sola migración obliga al generador a
hacer `AlterColumn` sobre las heredadas.

### Plataforma de ejecución y mensajería

**T5 · Contexto por cooperativa fuera de HTTP.**
Conflicto: seguir con `ITenantDbContextFactory` como el despachador de correo (DIAN), fabricar un
`HttpContext` falso, un holder scoped, o un contexto ambiental `AsyncLocal` (mensajería). Decisión:
`ContextoAmbiental` (AsyncLocal) + `IEjecutorEnCooperativa.EjecutarAsync(TenantDirectoryEntry, Actor,
origen, trabajo)`: un `AsyncServiceScope` nuevo por mensaje o comando, restaura al terminar, lanza si
hay `HttpContext` o si la entrada no trae base, y salta la cooperativa con base desactualizada sin
detener a las demás. Leen el ambiental cuando no hay `HttpContext`: `TenantContextAccessor`,
`CurrentUserService` (sólo `UserName` y `TenantId`), `CurrentCentralUserContextAccessor`,
`IpAddressAccessor`, `CentralIdentityLogEnricher` y la fábrica de `ErpTenantInfo`. `MongoAuditService`
lanza y registra Critical si hay contexto ambiental sin cooperativa (nunca `_Global`). Por qué: FR-083 y
D-03 exigen el mismo contexto que una petición y el camino común (Principio III); los accesores y la
auditoría son singletons y sólo el ambiental les llega.

**T6 · Identidad del actor.**
Conflicto: corregir `ICurrentUserService.UserId` en toda la plataforma (seguridad, opción b); agregar
`IActorActual` (seguridad) o `IContextoDeEjecucion` (mensajería); «fijar `ICurrentUserService` a la
identidad del proceso» (contabilidad). Decisión — la menos riesgosa que cumple FR-010 y FR-083: una sola
interfaz nueva de lectura, `IActorActual` → record `Actor` (§2.16), implementada por
`ActorDeLaPeticion` (reusa `PermisosDeLaPeticion.ResolverUsuarioYCooperativaAsync` y memoriza por
petición) y por el ambiental en segundo plano. El proceso automático es `Kind = Process`,
`Name = "Proceso de integración"`, sin IP, canal `Process`, origen `Mensaje:{id}`, `Lote:{número}` o
`Tarea:{nombre}`; los lotes manuales, reprocesos y envíos posteriores corren con la persona que los
ordenó (capturada en la orden). `ICurrentUserService.UserId` **no se cambia** en esta feature (su
arreglo es tarea aparte del dueño, §4). La segregación y las aprobaciones comparan `SEC_Users.Id` de
`IActorActual`, nunca el entero del token ni el correo; los documentos guardan `CreatedByUserId` y
`ConfirmedByUserId` (`SEC_Users.Id`). El usuario de origen viaja en el mensaje y en
`PostingRequest.RegistradoPor`; nunca es actor ni presta sus permisos. No se crea usuario técnico en
`SEC_Users`. Por qué: arregla lo que esta feature necesita sin cambiar el cuatro ojos ni «mis
borradores» del resto de la plataforma.

**T7 · Bandeja de salida (outbox) de mensajes de negocio.**
Conflicto: tablas `INT_*` (mensajería) o `COR_*`; mensajes desde I2 (alcance literal de las entregas) o
desde el primer documento. Decisión: tablas `COR_Integration*` (§2.2); `EmisorDeMensajes` agrega mensaje,
entregas y dependencias **sin guardar**, dentro del `SaveChanges` del documento (molde `AccountingPoster`).
Las tablas y el emisor entran en **I1**: todo documento confirmado nace con sus mensajes; sus entregas a
Contabilidad quedan `Pending` sin consumidor hasta I2 (mismo mecanismo que Cartera, T32). El mensaje es
inmutable (guardia `IHechoInmutable`, `PrincipioXI_ContableImmutable` y `PayloadSha256`); la entrega es
mutable con `RowVersion`. Id `bigint` identity es el orden; nunca se ordena por Guid. `PayloadJson` en
texto (no `jsonb`) por paridad. Por qué: FR-071 (no hay documento sin mensaje ni mensaje sin documento);
emitir desde I1 evita documentos de ensayo sin mensajes.

**T8 · Contratos de mensaje.**
Conflicto: records en `Application/Common/Integracion/Contratos/Inventario` (mensajería) o
`Application/Integracion/Contratos/Cartera` (compras); campos en español (compras) o inglés. Decisión:
todos los records en `Application/Common/Integration/Contracts/Inventory/<Type>V1.cs` (también los de
Cartera); sobre común `IntegrationEnvelopeV1` (§2.6); propiedades en inglés, JSON camelCase, enums como
texto, decimales exactos, tarifas como fracción. El contrato de cada tipo se publica en
`specs/012-inventario-comercial/contracts/mensajes.md` (y su lado contable en `contracts/contabilidad.md`). Un
cambio incompatible crea `V2`; el destino declara lo que acepta (`IDestinoDeMensajes.Acepta(type,
version)`); una versión no aceptada se rechaza con `Integration.VersionNotAccepted` y nunca se reescribe
lo emitido. Por qué: FR-072 y SC-023 (los mensajes para Cartera pueden esperar meses con v1).

**T9 · Orden, dependencias y herencia del modo.**
Decisión (mensajería 3 y 4, sin conflicto): al emitir se registran aristas hacia el último mensaje de
cada cadena de la que depende (su documento, el original que anula o corrige, los orígenes de un
derivado, cada documento afectado por un ajuste de costo). Elegible para un destino = entrega `Pending`,
`NextAttemptAt` vencido y ninguna dependencia con entrega a ese destino en {`Pending`, `InBatch`,
`Rejected`}; {`Processed`, `NotApplicable`, `ValidationFailed`} no bloquean; se procesa por Id
ascendente. Modo: el documento sella `PostingMode` al confirmar; la entrega a Contabilidad nace
`Pending` (Online), `InBatch` con `ScheduleKey` y `BatchScopeKey` (Batch) o `NotApplicable` (NotPosted);
informativos y Cartera, `Always`. Relacionados y derivados **no leen el parámetro**: copian modo y
`ScheduleKey` de la entrega de su original u origen (FR-075, FR-079; también el horario de lote).
`GrupoContableReclasificado` usa el valor general. FR-078 cambia en bloque `NotApplicable` → `InBatch`
(lote `SendNotApplicable`) en toda la clausura de dependencias, con actor persona y motivo.

**T10 · Despachador, arrendamientos y reintentos.**
Conflicto: candado Redis en la base 0 con TTL y `ExtendAsync` (mensajería), `IDistributedLock` para la
cadena de auditoría (seguridad), arrendamiento por fila en el documento electrónico (DIAN),
arrendamiento en SQL (alternativa de mensajería). Decisión: **arrendamiento en la base de cada
cooperativa**, `COR_BackgroundLeases`, tomado con `UPDATE … WHERE LeaseUntil < ahora OR Owner = yo`
(portable), TTL 120 s renovado entre tandas, para todo trabajo de fondo por cooperativa (nombres:
`integration.dispatch`, `audit.forward`, `einvoicing.process`, `scheduled.tasks`, `email.dispatch`). La
exactitud nunca depende del arrendamiento: `RowVersion` de la entrega, recibo único del destino, cabeza
de cadena con `RowVersion` y arrendamiento por fila del documento electrónico. Los candados Redis
existentes (nómina, invitaciones) no se tocan. `DespachadorDeMensajes` (API) recorre
`ITenantDirectory.ListActiveAsync`, sondea cada 5 s más la señal en proceso (`ISenalDeMensajes`,
`Channel<Guid>`), presupuesto 60 s por cooperativa, tandas de 100. Reintentos: los transitorios sin
límite con espera `min(15 s·2^(n−1), 15 min)` + jitter y alerta `Integracion.MensajeSinEntregar` a los 3
intentos o 15 min; los de negocio quedan `Rejected` sin reintento (alerta `Integracion.MensajeRechazado`)
y se reprocesan con `ReprocessMessagesCommand`, que crea un lote `Trigger = Reprocess`, pasa las
entregas a `InBatch` con ese `BatchId` y arrastra a los dependientes. Sin estado
«en proceso» persistido. Destino sin consumidor registrado: se salta, sin intentos ni alertas.
Configuración técnica en `appsettings`: `Integration:Dispatcher` (`Enabled`, `IntervalSeconds`,
`BudgetSeconds`, `LeaseTtlSeconds`, `BatchSize`) e `Integration:Retries`; la fixture de pruebas lo
apaga y conduce el ciclo a mano. Por qué: el Principio IV reserva la base 0 de Redis para lo que no es
de ninguna cooperativa (y `ITenantCacheSlots` dice que los candados de la base 0 protegen filas de la
base administrativa); el arrendamiento en SQL no depende de Redis, sirve a todos los procesos y queda
como evidencia.

**T11 · Consumo contable: unidad, comando y recibo.**
Conflicto: un comando por mensaje y registro de resultado aparte con `ACC_IntegrationReceipts`
(mensajería) frente a una unidad «todos los mensajes de una confirmación» que marca las entregas en el
mismo `SaveChanges` con `ACC_InventoryPostings` (contabilidad). Decisión: la **unidad de
contabilización** es el conjunto de mensajes de un mismo evento de origen (`OriginPublicId` +
`OriginEventKey`) con el mismo destino → **un comprobante por documento** (FR-077): `VentaFacturada` +
`CostoDeVentaReconocido` salen en un FV. `PostInventoryMessagesCommand(MessagePublicIds,
BatchPublicId?)` (`IReintentableAnteConcurrencia`, validador hermano, sin ruta, sin permisos) lee el
contenido por `IMensajesEntrantes`, verifica versión, moneda y que el original ya esté contabilizado
(si no, `Retry` con `Accounting.InventoryMessage.WaitingForOriginal`), arma líneas con
`ConstructorDeLineasDeInventario`, llama `AccountingPoster.PrepareAsync` y agrega `ACC_InventoryPostings`
(UK `MessagePublicId`) en **un** `SaveChanges`; la colisión del índice se traduce a `AlreadyProcessed`
(molde `PersonFactory.EsColisionDeDocumento`). Informativos: fila sin comprobante; valor cero: fila
«sin comprobante (valor cero)». El consumidor **no** toca tablas de la plataforma: el despachador, en
un ámbito nuevo, envía `RegisterDeliveryResultCommand`. Origen del comprobante:
`AccountingOrigin("INV", "InventoryDocument", documento.PublicId)`; resumido,
`AccountingOrigin("INV", "InventoryPostingBatch", lote.PublicId)`. Fecha: la de operación. Por qué: une
la atomicidad «comprobante + recibo» de contabilidad con la separación consumidor/plataforma de
mensajería, que además sirve a un destino externo como Cartera.

**T12 · Lotes de contabilización.**
Conflicto: `INT_Batches` de plataforma (mensajería) frente a `ACC_InventoryPostingBatches` con
`ACC_InventoryIntegrationSettings.NextBatchNumber` (contabilidad); permiso del lote manual en Inventario
(mensajería) o en Contabilidad (contabilidad). Decisión: el lote es de la **plataforma**
(`COR_IntegrationBatches`, número de `COR_IntegrationBatchCounters.NextValue`), para que Inventario lo
muestre sin leer `ACC_`; Contabilidad **planea** los grupos (`IDestinoDeMensajes.PlanearLote` →
`AgrupadorDeResumidos`), valida documento a documento con `AccountingPoster.ValidarVariosAsync` y excluye
los que fallan con sus relacionados; en resumido suma débitos y créditos por clave sin netear y
conserva el detalle de las líneas cuya cuenta exige tercero, documento cruce o base. Disparadores:
`Scheduled` (hora local de Colombia; UK filtrado `(ScheduleKey, ScheduledFor)` impide duplicar entre
réplicas), `CashSessionClose` (lo crea `CloseCashSessionCommand` en su `SaveChanges`), `PeriodClose`
(lo crea `CloseInventoryPeriodCommand`), `Manual` (`OrderIntegrationBatchCommand` con el
`cutoffMessagePublicId` de la vista previa —el Guid del último mensaje listado; el handler lo resuelve al
`CutoffMessageId` interno, que nunca sale en JSON—; responde 202; permiso `Accounting.InventoryBatches.Run`),
`Reprocess` y `SendNotApplicable` (permisos de Inventario). Un ámbito DI por unidad o grupo; el
arrendamiento se renueva entre grupos; un lote vacío queda `Empty`. Se descartan
`ACC_InventoryPostingBatches` y `ACC_InventoryIntegrationSettings`.

**T13 · Idempotencia de las operaciones de pantalla.**
Conflicto: cabecera `Idempotency-Key` + `COR_OperationKeys` + behavior (mensajería); columna
`ClientOperationId` con índice único por tabla (ventas); `INV_Documents.IdempotencyKey` (núcleo).
Decisión: sólo `COR_OperationKeys` + `IdempotencyBehavior`. El cliente genera un UUID al iniciar la
operación (abrir el diálogo, cargar el borrador), lo conserva en los reintentos (`RenovacionDeSesionHandler`
ya clona cabeceras) y lo renueva sólo tras un éxito o si cambia el contenido; la ruta lo mapea a
`OperationKey` del comando (`IOperacionIdempotente`). Éxito: se guarda el `Result` serializado en la
misma transacción; fallo: se revierte y la clave no queda. Repetición: mismo resultado + cabecera
`Idempotent-Replayed: true` + evento de auditoría `Operation.Replayed`; misma clave con otro contenido:
422 `Operation.KeyReused`; clave ausente: 400 `Operation.KeyRequired`. Aplica a todo comando con ruta de
`Application/Inventory`, `Application/ElectronicInvoicing`, `Application/Core/{Taxes,PaymentMeans}` y de
la plataforma invocado desde pantalla (parámetros, aprobaciones, alertas, alcances, montos, reproceso,
lotes, envío posterior); lo vigila `LosComandosDeInventarioLlevanClave`. Los mensajes usan su
`MessageId` (recibo único) y la emisión DIAN la clave `{tenantPublicId}:{ambiente}:{prefijo}{consecutivo}:v{versión}`.
Retención: indefinida (nada se borra solo; pregunta al dueño). Por qué: cubre transiciones (aprobar,
anular, cerrar) que una columna por tabla no cubre, y la clave es atómica con el efecto.

**T14 · Transacción explícita única y orden del pipeline.**
Decisión: `Application/Common/Persistence/TransaccionExplicita.EjecutarAsync` (estrategia de ejecución +
`BeginTransaction`; cada intento llama `DescartarCambios()` y relee todo; una llamada anidada se une a la
transacción en curso). Orden: Validation → Logging → **Idempotency** → Audit → ReintentoPorConcurrencia →
Performance. `ConfirmInventoryDocumentCommand` **no** es `IReintentableAnteConcurrencia`: una víctima de
interbloqueo (PostgreSQL 40P01/40001, SQL Server 1205) la repite entera la estrategia. Ningún handler
envía por `ISender` un comando reintentable dentro de su transacción (lección de
`TransaccionDeLiquidacion`, que pasa a delegar en `TransaccionExplicita`). Todo esto se prueba sólo con
Testcontainers en los dos motores (InMemory ignora transacciones).

### Existencias, documentos y costo

**T15 · Concurrencia de existencias.**
Conflicto: concurrencia optimista con `RowVersion` y reintentos (009 R3) frente a bloqueo pesimista
ordenado (núcleo). Decisión: bloqueo pesimista en el orden canónico de §1.3, implementado sólo en
`Persistence/Inventory/CerrojoDeInventario` con SQL de cada motor ejecutado por el propio
`ApplicationDbContext` (sin conexiones propias): primero asegura las filas de proyección
(`INSERT … ON CONFLICT DO NOTHING` / `INSERT … WHERE NOT EXISTS … WITH (UPDLOCK, HOLDLOCK)`), luego
bloquea (`SELECT … ORDER BY "Id" FOR UPDATE` / `WITH (UPDLOCK, ROWLOCK, HOLDLOCK) … ORDER BY Id`).
Ese INSERT no pasa por EF ni por el `AuditableEntityInterceptor`, así que escribe él mismo `PublicId`
(`gen_random_uuid()` / `NEWID()`), `CreatedAt` = UTC, `CreatedBy` = nombre del actor (`IActorActual`),
`IsDeleted` = 0 y ceros en las cantidades; lo prueba `ConcurrenciaDeExistenciasTests` en los dos motores.
`RowVersion` queda como segunda defensa. Disponible = físico − reservado; si falta, `Inventory.Stock.Insufficient`
con la cantidad disponible. La validación previa contable corre **antes** del cerrojo (T30). Un saldo
inicial grande se parte en documentos de hasta 4.000 líneas por bodega para no escalar el bloqueo a
tabla en SQL Server. Por qué: SC-001 (50 ventas simultáneas, exactamente 10 confirmadas) y SC-019 (30
cajas) no se cumplen con 5 reintentos optimistas.

**T16 · Numeración.**
Conflicto: `INV_DocumentSequences.NextValue` bloqueada al final (núcleo); incremento atómico
`UPDATE … RETURNING/OUTPUT` de `LastIssuedNumber` en la resolución (DIAN); `RowVersion` + reintento
(009). Decisión: un mecanismo, el cerrojo: la fila de numeración es la última del orden y se incrementa
por EF dentro de la transacción de confirmación. No fiscales y notas (`CreditNote`, `DebitNote`,
`PosAdjustmentNote`, `SupportDocumentAdjustmentNote`, `NonElectronic*`): `INV_DocumentSequences`
(`DocumentTypeId`, `Prefix`, `NextValue`, vigencia) — el cambio de prefijo es una fila nueva. Fiscales
con resolución (factura, DEE POS, documento soporte, contingencia 03): `COR_DianNumberingResolutions.LastIssuedNumber`
de la resolución vigente buscada por (`Kind`, `Prefix`, `Environment`) y asociada al canal sellado; el
tipo declara su prefijo (sin FK a la resolución). Se asigna al confirmar; el borrador no consume.
`INV_Documents` lleva el UK filtrado con `FiscalNumberReleased` **desde I1** (el caso b de FR-066 reusa el
número); la unicidad fiscal vive en `COR_ElectronicDocuments (Environment, Prefix, Consecutive)` sin
filtro. Sólo `Numerador` y `NumeradorFiscal` escriben esas columnas (`SoloElNumeradorNumera`).

**T17 · Modelo genérico de documentos.**
Conflicto: una tabla por clase; una cabecera genérica con satélites (núcleo); cabeceras propias para
compras (`INV_SupplierInvoices`, compras), para movimientos de caja (`SLS_CashMovements`, ventas) y para
el documento electrónico (`FEL_Documents`, DIAN); `SourceLineId` en la línea (núcleo) frente a
`INV_DocumentLineLinks` (compras). Decisión: una cabecera (`INV_Documents`) y una línea
(`INV_DocumentLines`) para las 34 clases de `DocumentClass`, más satélites (§2.2); movimiento de caja y
arqueo con diferencia **son clases** con satélites (`INV_CashMovementDetails`, `INV_CashDocumentLines`);
la factura del proveedor lleva su 1:1 (`INV_SupplierInvoiceDetails`); los vínculos van en
`INV_DocumentLinks` y `INV_DocumentLineLinks` (sin `SourceLineId`); la anulación es la clase `Voiding`
(`VoidsDocumentId` / `VoidedByDocumentId`). El documento electrónico queda aparte en `COR_` porque su ciclo
fiscal (versiones, transmisiones, contingencias) es otro. El comportamiento de cada clase vive en
`ClasesDeDocumento` y una estrategia por clase en `Inventory/Documents/Efectos`. **Grupos** (ruta y
familia de permisos): Purchases = PurchaseRequest, PurchaseOrder, PurchaseReceipt, SupplierInvoice,
SupplierNote, SupportDocument, SupportDocumentAdjustmentNote, LandedCost, SupplierReturn · Adjustments =
PositiveAdjustment, NegativeAdjustment, InternalConsumption, WriteOff, Assembly, LocationMove · Costing
= CostAdjustment (lo genera el sistema; sin alta manual) · Transfers = TransferDispatch, TransferReceipt
· Counts = PhysicalCount · Sales = SalesQuote, SalesOrder, Shipment, SalesInvoice,
SalesInvoiceFromShipments, PosEquivalentDocument, NonElectronicSalesReceipt, NonElectronicSalesNote,
CreditNote, PosAdjustmentNote, DebitNote · Cash = CashMovement, CashCountDifference · OpeningBalance ·
Voiding = el grupo del original. **Cadenas de modo de paso** (FR-075): Purchases = PurchaseReceipt,
SupplierInvoice, SupplierNote, SupportDocument, SupportDocumentAdjustmentNote, SupplierReturn · Sales =
Shipment, SalesInvoice, SalesInvoiceFromShipments, PosEquivalentDocument, NonElectronicSalesReceipt,
NonElectronicSalesNote, CreditNote, PosAdjustmentNote, DebitNote · Transfers = TransferDispatch,
TransferReceipt. Un documento confirmado implementa `IInmutableTrasConfirmar`: sólo cambian `Status`
(→ `Voided`), `VoidedByDocumentId`, `FiscalNumberReleased` y la auditoría. Por qué: numeración, estados,
aprobación, anulación, período, alcance, idempotencia y bandeja son iguales para todas las clases
(precedentes `ACC_Documents.Kind`, `PAY_PayrollRuns.Kind`).

**T18 · Kardex, proyecciones y hechos inmutables.**
Decisión (núcleo 2, 4, 4b, 4c, 11, sin conflicto): `INV_KardexEntries` sólo inserción; orden
`(OperationDate, Id)`; ajustes de costo como líneas `Kind = CostAdjustment` con `AffectsEntryId`;
retroactivo fechado en la salida afectada; proyecciones reconstruibles (`INV_StockBalances`,
`INV_StockDetails`, `INV_CostStates`, `INV_CostLayers`) con índices únicos sin filtro; valor por bodega
= cantidad × promedio del ámbito (nunca Σ `TotalCost` por bodega); el tránsito sale al costo de la línea
de despacho; `MotorDeCosteo` puro con casos dorados. Un solo escritor, `RegistroDeKardex`
(`NadieEscribeElKardexFueraDelRegistro`). `IHechoInmutable` (kardex, consumos de capa, mensajes,
dependencias, intentos, decisiones de aprobación, `InventoryPosting`, `DocumentPartySnapshot`,
`DocumentTaxLine`, versiones y transmisiones electrónicas, anclas) y `IInmutableTrasConfirmar` los hace
cumplir `ApplicationDbContext.SaveChangesAsync`, que rechaza `Modified`/`Deleted` no autorizados.
`VerificacionDeIntegridad` compara sumas del kardex contra proyecciones y levanta
`Inventario.IncidenteDeIntegridad`; `RebuildInventoryProjectionsCommand` exige `Inventory.Integrity.Rebuild`.

**Excepción de puesta en marcha y retroactivo mínimo en I1** (COOFLOPAL activa bodega por bodega con
`Costeo.Ambito = Cooperativa`; sin esto, el saldo inicial de la segunda bodega sería retroactivo frente a
las ventas ya registradas en la primera y se rechazaría hasta I5). Un `OpeningBalance` (y su `Voiding`)
de una bodega `NotActivated`, y los ajustes que el sistema genera desde un conteo aprobado (fechados en
la foto, FR-041), **no están sujetos a `Costeo.RetroactivosPermitidos`**. El motor los inserta en el orden
`(OperationDate, Id)`, verifica que no aparezca existencia negativa, recalcula las salidas posteriores del
ámbito cuando el nuevo promedio las cambia (con promedio ponderado, un ajuste de conteo valorado al
promedio de su fecha no las cambia) y registra `AjusteDeCostoReconocido` por documento afectado. Ese
soporte retroactivo mínimo (`Retroactivo` limitado a esas dos clases) se entrega en **I1**; los documentos
retroactivos generales, con `SimularImpacto` y el parámetro, siguen en I5. Precisión aplicada a la spec
(FR-045) y preguntas D8 y D9; caso dorado 17 «segunda bodega activada después de ventas en la primera,
ámbito cooperativa».

**T19 · Precisión decimal.**
Conflicto: tarifas porcentuales (7,4) (núcleo), fracción (9,4) como `ACC_AccountTaxRates` (contabilidad),
fracción sin escala (compras). Decisión: la convención global (18,2) no cambia; ayudante
`Persistence/Configurations/Inventory/PrecisionDeInventario.{Cantidad (18,4), Factor (18,6),
CostoUnitario (18,6), PrecioUnitario (18,6), Monto (18,2), Tarifa (9,6)}` y prueba
`LasCantidadesYCostosTienenSuPrecision`. Precio de lista `INV_PriceListItems.Price` (18,2); precio
unitario de línea (18,6); impuesto por unidad `AmountPerUnit` (18,2). **Toda tarifa es fracción (9,6)**
(0,19; 0,00966 para 9,66 ‰): `COR_TaxRates.Rate` y, por enmienda aditiva de la 009 en I2,
`ACC_AccountTaxRates.Rate`. Redondeo `MidpointRounding.AwayFromZero` según `Redondeo.Montos`; residuo
según `Redondeo.Residuo`, visible. Por qué: las tarifas de ICA por mil no caben en 4 decimales de
fracción, y C8 compara la tarifa del catálogo con la de la cuenta.

**T20 · Zona horaria.**
Conflicto: `America/Bogota` o desfase fijo −05:00; plataforma o por cooperativa; arreglarlo aquí o
aparte. Decisión: `IDateTimeService` gana `AhoraLocal` (`DateTimeOffset`) y `HoyLocal` (`DateOnly`) con la
zona de `Plataforma:ZonaHoraria` (por defecto `America/Bogota`; si la imagen no la trae, −05:00 fijo con
un aviso único en el log). Los instantes se guardan en UTC; la **fecha de operación** es `HoyLocal`.
La usan Inventario (fecha propuesta, «fecha ≤ hoy», período), POS (`OperatingDate` de la sesión = fecha
local de apertura; fecha fiscal = fecha local de la venta), lotes programados (hora local), DIAN (hora
con −05:00). Los demás módulos siguen con `TodayUtc` hasta adoptarlo (no se tocan aquí). Zona por
cooperativa: no en esta feature.

### Parámetros, impuestos, personas y medios de pago

**T21 · Parámetros con vigencia de plataforma.**
Conflicto: `INV_Parameters` copia de nómina, migrar `PAY_CompanyPolicies`, `COR_SystemSettings`, o una
tabla general en Core (núcleo, seguridad, DIAN y compras coinciden en esta última). Decisión:
`COR_ParameterVersions` + `LectorDeParametros` (único lector: valor a una fecha con caída
ámbito → general → defecto seguro; valor no admitido = error nombrado) + `AddParameterVersionCommand`
(sin cruces, cierra la anterior la víspera, motivo con `IConMotivo`, auditado con diferencia, reglas por
clave: método y ámbito de costeo sólo al inicio de un período sin movimientos posteriores; modo de paso
por cadena y con confirmación explícita para tipos fiscales). Las definiciones (`DefinicionDeParametro`:
clave, tipo, admitidos, defecto, ámbitos, permiso, sellado) viven en catálogos cerrados por módulo
(`ParametrosDeInventario`, `ParametrosTributarios`, `ParametrosDeFacturacionElectronica`) con los
códigos `INV`, `TAX`, `EINV`. `PAY_CompanyPolicies` no se migra. Lo vigila `LosParametrosSeLeenEnUnSoloSitio`.

**T22 · Catálogo de impuestos en Core y motor tributario.**
Conflicto: permisos `Inventory.Taxes.*` y pantalla `/inventario/impuestos` (seguridad, ventas) frente a
catálogo en Core (compras). Decisión: `COR_TaxDefinitions`, `COR_TaxRates`, `COR_WithholdingConcepts` en
I1, permisos `Core.Taxes.View/Manage`, pantalla `/maestros/impuestos`, rutas `/api/core/taxes*`, plantilla
`ImportTaxCatalogCommand`. Motor puro `Domain/Taxes/MotorTributario` (impuestos por línea redondeados por
línea; retenciones por documento y por (impuesto, concepto, municipio) con base ≥ mínimo en pesos;
tratamiento `Generated`/`Deductible`/`AddedToCost`/`WithholdingApplied`/`WithholdingSuffered`; notas y
devoluciones con la foto del original, sin volver a probar el mínimo) y `IvaDescontable.Determinar` en el
orden de FR-044. `INV_DocumentTaxLines` guarda la foto. Un impuesto por unidad (bolsas) usa una cuenta
sin «exige base gravable» (no se enmienda la regla 10 de la 009; pregunta al dueño). Por qué: Principio V
(datos fiscales en COR), precedente D-42 de la 010 y reutilización por Tesorería, Cartera y Activos.

**T23 · UVT.**
Decisión (compras, sin conflicto real): un solo lector, `Application/Common/Taxation/LectorDeUvt`
(`IValorUvt`), que lee el código `UVT` de `PAY_LegalParameters` a la fecha y falla visible si no hay
vigencia («No hay UVT vigente al {fecha}; regístrela en Parámetros legales»). `LaUvtSeLeeEnUnSoloSitio`
permite `PayrollLegalParameters` sólo en `Application/Payroll` y en ese lector. Cuando se promueva a
Core, cambia sólo el lector. No se crea otra UVT.

**T24 · Perfil tributario de las personas, de la cooperativa y DIVIPOLA.**
Decisión (compras 5 y 6, DIAN 18): enmienda de la 008 en I1: las seis marcas nuevas de `COR_People`
(§2.3) entran a `PersonInput`, su validador y una sección «Datos tributarios» de `PersonaDialog`; las
responsabilidades DIAN (O-13, O-15, O-23, O-47, R-99-PN), el tributo y el tipo de identificación DIAN se
**derivan** en `CatalogoDian` (el `IdType` heredado se traduce por tabla, como hace la dispersión). El
perfil de la cooperativa son los parámetros `TAX` con vigencia. `COR_Cities.DaneCode` con semilla
DIVIPOLA, `COR_Branches.MunicipalityDaneCode` (lo escriben las rutas existentes `POST/PUT
/api/core/branches` —el contrato decía `/api/accounting/branches`, que no existe; T178—, validado contra `COR_Cities.DaneCode`: 422
`Branch.MunicipalityUnknown`), y el documento de compra lleva
`OperationMunicipalityDaneCode`, propuesto desde la sucursal de la bodega que recibe. ReteICA se busca por
(municipio, CIIU, concepto) y cae a la fila `*` del municipio. `IcaRate` por persona no se usa.

**T25 · Medios de pago, tarjetas y datáfonos en Core.**
Conflicto: permisos `Inventory.PaymentMethods.*` (seguridad) frente a catálogo en Core (ventas); FK de la
matriz al medio (ventas) frente a código (contabilidad). Decisión: `COR_PaymentMeans`, `COR_CardNetworks`,
`COR_CardAcquirers`, `COR_CardTerminals`, `COR_CashDenominations` (I3), permisos `Core.PaymentMeans.*`,
pantalla `/maestros/medios-de-pago`, rutas `/api/core/payment-means*`. Nombre `PaymentMeans` (UBL
`cac:PaymentMeans`); `COR_PaymentMethods` no se toca. Dónde se ofrece cada medio vive en el módulo
(`INV_PaymentMeans{PointsOfSale,Channels,DocumentTypes}`), con la regla pura `DisponibilidadDeMedio`. La
clase es lo único que conoce el código (vueltas, crédito, forma de pago DIAN, arqueo por defecto). Se
guardan `Last4` y autorización, nunca el número de tarjeta (`LosPagosNoGuardanElNumeroDeTarjeta`). Un
bono de número único se protege con el UK filtrado de `INV_VoucherRedemptions`. La matriz referencia el
medio por `PaymentMeansCode` (T27). Modificar un medio (`PUT /api/core/payment-means/{id}`) exige
`reason`; la tolerancia de arqueo y la comisión esperada se copian en cada línea de arqueo y en cada pago
al usarse, de modo que el cambio rige para lo que se cierre o cobre después (FR-012, FR-096).

**T26 · Retenciones que practica el comprador y «total a pagar».**
Conflicto: FR-056 («la suma de los pagos iguala el total») frente a la venta a un agente retenedor
(riesgo de compras). Decisión: `Total` = base − descuentos + impuestos; `AmountDue` = `Total` −
retenciones sufridas (`DocumentTaxLine.Treatment = WithholdingSuffered`). La igualdad de FR-056 se
evalúa contra `AmountDue` (precisión aplicada a la spec: «la suma de los pagos MUST igualar el total a
pagar: el total menos las retenciones que practica el comprador agente retenedor»). La retención viaja en `VentaFacturada` como línea de retención (rol
`Retencion`), nunca como medio de pago.

### Contabilidad y Cartera

**T27 · Matriz de reglas contables.**
Decisión (contabilidad 2, con el ajuste de dimensiones): una tabla `ACC_InventoryPostingRules` con
`Role`; dimensiones del módulo y de los catálogos de Core **por código** (`AccountingGroupCode`,
`WarehouseCode`, `PointOfSaleCode`, `PaymentMeansCode`, `TaxRateCode` + `TaxRate`, `ReasonCode`), y FK
sólo a `COR_Branches` y `COR_CostCenters`; `DimensionKey` normalizada con `*` y UK filtrado
`(DimensionKey, ValidFrom)`; resolución por especificidad con pesos 16/8/4/2; vigencia al molde de
`AddPolicyVersionCommand`, sin cruces (`Accounting.InventoryRule.Overlaps`) y sin versiones retroactivas
sobre lo contabilizado (`RetroactiveOverPosted`); cada cuenta pasa por `AccountEligibility.Verificar(…,
ModuloContable.Inventario)` y la de impuesto debe ser de `TaxKind` compatible. Consecuencia para
Inventario y Core: **los códigos de grupo contable, bodega, punto de venta, medio de pago, tarifa y causa
de ajuste no cambian una vez creados** (la pantalla no los ofrece editables). Pantallas y plantilla en I2.

**T28 · Tipos de comprobante de Inventario.**
Conflicto: `CP` (contabilidad) frente a «`FC` Factura de compra» (compras) para la factura del proveedor.
Decisión: se conservan `FV`, `EI`, `SI` y se siembran `NV` (notas de venta), `CP` (compras: factura del
proveedor, documento soporte y notas), `TR` (traslados), `AC` (ajustes de costo y reclasificaciones),
`CJ` (caja POS), todos `Usage = Module`, `ModuleCode = INV`. Mapeo parametrizable en
`ACC_InventoryVoucherMappings` (por operación y, opcionalmente, por código de tipo de documento de
Inventario); por defecto el de §2.6. El tipo de la unidad lo da su **mensaje principal** (el primero
emitido: el comercial antes que el de costo). `DocumentoAnulado` usa el tipo de su original. Cruces por
defecto: `FV` en ventas, `NC`/`ND` en notas, `FC` en compras (son documentos cruce, no tipos de
comprobante). `VoucherTypesSeeder` registra la colisión en el log en vez de saltarla en silencio.

**T29 · Anulaciones, notas y ajustes: comprobantes nuevos.**
Decisión (contabilidad 5, sin conflicto): nada de Inventario usa `PrepareReversalAsync` (guarda en el
poster: `Accounting.Document.InventoryCorrectsWithNewVoucher`; prueba `LoDeInventarioNoSeReversa`). Cada
anulación, nota, devolución o ajuste es un comprobante `Kind = Regular` de su propio mensaje y su propia
fecha; `DocumentoAnulado` espeja las cuentas del original con las reglas vigentes a la fecha del
original; si su fecha cae en período cerrado, se rechaza a la bandeja (la fecha nunca se mueve). El
vínculo vive en `ACC_InventoryPostings` (`RelatedDocumentPublicId`); el comprobante original nunca se
marca `Reversed`. `ReverseDocumentCommand` conserva `ModuleOwned` con mensaje propio para INV.

**T30 · Validación previa.**
Decisión (contabilidad 3 + la ubicación que exige T15): `IContabilidadParaInventario.EvaluarAsync` →
`EvaluateInventoryPostingQuery`, que arma las líneas con el **mismo** `ConstructorDeLineasDeInventario`
del consumidor y las somete a `AccountingPoster.ValidarVariosAsync` (carga en bloque, sin seguimiento)
más las comprobaciones de la matriz (`Missing`, `TaxRateMismatch`, tipo de comprobante sin mapear,
descuadre, más de 2 decimales). Respuesta: `IsPostable`, errores y avisos con `lineNumber`, cuenta, regla
y `WhoFixes { module, page, permission }`. Corre en proceso (sin HTTP), **fuera del cerrojo** y con tiempo
máximo `Contabilidad.ValidacionPreviaSegundos`; «no responde» (excepción o tiempo agotado) aplica
`Contabilidad.PoliticaSinRespuesta`. El resultado se sella en `IntegrationMessage.PrevalidationOutcome`
(para medir SC-021). Los montos de costo usados en la evaluación son provisionales (promedio leído sin
bloqueo); las reglas de cuenta no dependen de ellos salvo el valor cero.

**T31 · Frontera con Contabilidad y Cartera.**
Conflicto: tres pruebas con nombres distintos (`InventarioNoTocaContabilidadNiCartera`,
`InventarioNoConoceLaContabilidad`, `InventarioNoLeeContabilidadNiCartera`). Decisión: una,
`InventarioNoConoceContabilidadNiCartera`: (1) ArchUnit: `Application.Inventory*` y
`Domain.Entities.Inventory*` no dependen de `*.Accounting*` ni `*.Lending*`, salvo
`Application.Common.Integration*`; (2) regex sobre el fuente de Inventario (DbSets contables y de
cartera, `"ACC_"`, `"LND_"`, `AccountingPoster`); (3) por reflexión, `IContabilidadParaInventario` declara
exactamente sus cuatro métodos e `IConsultasDeCartera` sus dos (la «comprobación automática» de FR-014);
(4) la inversa: `Application/Accounting/Inventory` no depende de `Domain.Entities.Inventory` (lee la
bandeja de la plataforma e `IDimensionesDeInventario`). Contabilidad sí puede leer `COR_Integration*`.

**T32 · Cartera pendiente (IC) y crédito provisional.**
Decisión (mensajería 13 + compras 12–15, complementarias): toda venta a crédito y sus ajustes emiten
`VentaACreditoRegistrada` / `AjusteDeVentaACredito` con entrega `Destination = Lending`, `Mode = Always`,
`Status = Pending` y dependencias hacia el original, en el `SaveChanges` de la venta. Mientras no haya un
`IDestinoDeMensajes` «Lending» registrado **o** `Cartera.IntegracionHabilitadaDesde` esté vacío, el
despachador las salta (sin intentos ni alertas) y la bandeja muestra «Pendiente — destino aún no
disponible (IC)». `IConsultasDeCartera` resuelve a `CarteraNoHabilitada` → flujo provisional: persona
identificada (no consumidor final) y activa en el maestro (asociado: `IsAssociate` y sin retiro;
cliente: `IsCustomer`); aprobación por el motor con monto suficiente y nunca del cajero; pago
`PendingValidation`; `VentaFacturada` lleva el pago de crédito por su medio y la matriz lo envía a una
cuenta que exige tercero y documento cruce `FV` (la vista `pending-documents` de la E2 sirve de cartera
provisional); `AccountsReceivableRecordedBy = Contabilidad` sellado; plazo, cuotas y línea sugerida son
datos del medio (sin llave a `LND_*`). Cuando llegue IC: se registra el destino y el parámetro; el
despachador entrega lo acumulado por Id respetando dependencias; Cartera deduplica por `MessageId`;
`ValidationFailed` (desde `Processed`, porque Cartera valida después de procesar) lo fija IC consultando
`EstadoDeValidacionAsync` (alerta
`Integracion.ValidacionFallida`). `PersonPublicId` va en el mensaje por si D-02 exige orden por persona.

### Seguridad, auditoría y alertas

**T33 · Motor de aprobaciones de plataforma.**
Conflicto: lógica por documento, extender `FourEyes`, motor genérico externo, o motor de plataforma
(seguridad; ventas y compras lo reutilizan). Decisión: `Domain/Approvals/EvaluadorDePolitica` (puro, casos
dorados) + `Application/Common/Approvals/MotorDeAprobaciones` + `COR_Approval*`. Al confirmar se evalúa
con la política vigente en la fecha de operación (sellada en la solicitud); con niveles, el documento
queda `PendingApproval` y congelado; la última aprobación confirma en su misma transacción (ahí se
numera); rechazar exige motivo y devuelve a `Draft`. Segregación fija: creador, solicitante,
participantes declarados (quien abrió o capturó el conteo, el cajero en diferencias de arqueo y en
crédito) y quien ya aprobó otro nivel. `ContentSha256` invalida la aprobación si cambia lo aprobado
(descuentos). Métodos: `OwnSession` (bandeja `/inventario/aprobaciones`; el POS consulta cada 2 s),
`InPersonPasskey` (`IWebAuthnService` con las credenciales del aprobador) e `InPersonTotp` (un solo uso),
nunca contraseña. Lo usan: tipos de documento, descuentos sobre tope, diferencias de arqueo, movimientos
de caja, faltantes y sobrantes de traslado, ajustes de conteo, saldo inicial, crédito provisional,
excepciones del cruce (I5). Qué se aprueba lo dice `Subject` (`ApprovalSubjects`, §2.5), en la política
y en la solicitud: la política se registra por (`Module = "Inventory"`, `Subject`, tipo de documento o
todos) y la API la expone con `subject`; el crédito provisional es `Subject = ProvisionalCredit` con
`SourceType = DocumentPayment`; un descuento sobre tope, `DiscountOverCap` con `DocumentLineDiscount`; un
faltante o sobrante de traslado, `TransferDiscrepancy` con la política **del tipo de la recepción de
traslado** (un solo sitio: la tabla de sujetos de `data-model.md`). `FourEyes` de Contabilidad y
`AllowSameUserApproval` de Nómina no se migran.

**T34 · Monto máximo por permiso.**
Decisión (seguridad 4): `SEC_PermissionAmountLimits` (rol, código validado contra el catálogo, monto,
moneda, vigencia, motivo); `ILimitesPorPermiso.MontoMaximoAsync(permiso, fecha)`; efectivo = el mayor de
los roles activos que conceden el permiso; un rol que lo concede sin fila = sin límite. Por encima: nivel
1 de la política del tipo más los niveles cuyo umbral se alcanza; sin política,
`Inventory.Approval.AmountExceedsLimit` con `data.maxAmount`. Aplica a `Purchases.Confirm`,
`Adjustments.Confirm`, `Sales.SellOnCredit`, notas y órdenes.

**T35 · Alcance por bodega y punto de venta.**
Conflicto: tablas del módulo (seguridad) frente a asignación «estilo `UserBranchAssignment`» (ventas) o
tabla genérica de seguridad. Decisión: `INV_UserWarehouseScopes` (I1) e `INV_UserPointOfSaleScopes` (I3);
falla cerrado salvo `Inventory.Scope.AllWarehouses` / `AllPointsOfSale`; `IAlcanceDeInventario`
memorizado por petición; en segundo plano, alcance total. `FiltroDeAlcance` en toda consulta (documentos
por bodega de origen **o** destino; kardex y existencias por bodega; sesiones por punto); todo comando
valida cada bodega y punto que toca y responde 404 si está fuera; el aprobador también necesita alcance.
La bodega de tránsito se ve a través de los traslados de las bodegas del alcance; su existencia propia,
sólo con alcance total o asignación explícita.

**T36 · Auditoría: origen, motivo, rechazos y diferencias.**
Conflicto: un interceptor nuevo con activación explícita `IAuditarCambios` (seguridad) frente al
`AuditableEntityInterceptor` existente, que ya captura antes, después y campos cambiados de toda entidad.
Decisión: **se amplía el existente**, no se crea otro; exclusión por entidad con `[SinDiffDeAuditoria]`
(`IntegrationMessageDelivery`, `IntegrationDeliveryAttempt`, `BackgroundLease`, `OperationKey`,
proyecciones, `KardexEntry` — el documento ya es la referencia —, `AuditOutboxEntry`, `AuditChainHead`,
`AuditAnchor`) y por propiedad con `[NoAuditar]` (`TechnicalKey`, `CredentialKey`, secretos). `InferModule`
(interceptor y `AuditBehavior`) reconoce, antes de `.Core`: `.ElectronicInvoicing`, `.Integration`,
`.Approvals`, `.Alerts`, `.Parameters`, `.Core.Taxes` → `Taxes`, `.Core.Payments`/`.Core.PaymentMeans`
→ `PaymentMeans`. `IOrigenDeLaPeticion` (IP del `IIpAddressAccessor` existente, User-Agent, canal,
endpoint): canal `web`/`app` por la cabecera `X-Canal` que pone `CanalDeOrigenHandler` en los tres
anfitriones; `pos` cuando el comando implementa `IOperacionDePuntoDeVenta`; `proceso` en segundo plano.
`IConMotivo.Reason` se copia al evento; un `Result.IsFailure` se registra como `Rejected` con
`Error.Code`; la clave de idempotencia y `ActorKind` van en metadata.

**T37 · Auditoría con entrega garantizada.**
Decisión (seguridad 9, ubicada según T10): para los **módulos encadenados** (T38), el evento se escribe
en `COR_AuditOutbox` en la misma transacción del cambio: las diferencias de entidad las agrega el
`AuditableEntityInterceptor` en `SavingChanges` (para esos módulos deja de mandarlas a Mongo, así no hay
doble registro) y el evento del comando lo agrega `AuditBehavior` dentro de la transacción (la de
`IdempotencyBehavior`, o una `TransaccionExplicita` propia). Los rechazos se escriben
por un contexto aparte (`ITenantDbContextFactory`) para sobrevivir al rollback; exportar, imprimir e
ingresar insertan su propia fila. `AuditOutboxForwarder` (Infrastructure/Audit, registrado sólo en la
API, arrendamiento `audit.forward`) reenvía a Mongo con `_id = EventId` (duplicado = hecho), por filas no
reenviadas ordenadas por Id; tras reenviar, `UPDATE` vacía `PayloadJson` y queda la fila delgada
(`Seq`, `Hash`): nunca DELETE (Principio VII). Los módulos no encadenados siguen por `MongoAuditService`
con su guarda (T5). `AuditRetention.ModulosDeDiezAnios` suma `Inventory`, `ElectronicInvoicing`,
`Integration`, `Approvals`, `Alerts`, `Parameters`, `Taxes`, `PaymentMeans`.

**T38 · Sello de integridad: alcance de la cadena.**
Conflicto: cadena sólo de Inventario o también Contabilidad y Navegación (pregunta de seguridad).
Decisión: un flujo por cooperativa y clase de retención, `Stream = "{tenantPublicId:N}:10y"`, con los
módulos de `AuditoriaEncadenada.Modulos` = `Inventory`, `ElectronicInvoicing`, `Integration`,
`Approvals`, `Alerts`, `Parameters`, `Taxes`, `PaymentMeans` y `Navigation` (FR-007 incluye «ingresar a
una opción»). **Accounting queda fuera** en esta feature (pregunta al dueño). El reenviador asigna
`Seq` y `Hash = SHA-256(prevHash || JSON canónico)` actualizando `COR_AuditChainHeads` con `RowVersion`
(no hay bifurcación aunque dos réplicas coincidan); canonicalización única en `SelloDeIntegridad` (claves
ordenadas, UTC ISO, decimales invariantes, versión `v`, casos dorados); anclas cada 1.000 eventos y
diarias en `COR_AuditAnchors` con HMAC de `AuditSignature` en versión propia; ancla génesis al activar.
`VerifyAuditIntegrityQuery` (`POST /api/audit/integrity/verify`, `AuditLog.VerifyIntegrity`, consulta
sin clave de idempotencia; cuerpo `{ from, to, stream? }`, por defecto el flujo de 10 años de la
cooperativa; rango mayor de 10 años: 400 `Validation.Invalid`) informa `{ stream, fromSeq, toSeq,
checked, anchorsChecked, incidents[] }` con `kind` alterado (`Altered`), eliminado (`Deleted`),
intercalado (`Interleaved`), ancla inválida (`AnchorInvalid`) y purgado por retención
(`PurgedByRetention`), y se audita como `AuditLog.IntegrityVerified` con el resultado.

**T39 · Alertas sobre las notificaciones existentes.**
Conflicto: sólo `COR_Notifications` (una fila por destinatario, sin «atendida por»), alertas calculadas,
`INV_Alerts`, o bandeja de plataforma (seguridad). Decisión: `COR_AlertTypes` + `COR_Alerts` (estado
compartido, `DedupKey`), `RaiseAlertCommand` / `AttendAlertCommand`; destinatarios por permiso y alcance
con `IDestinatariosPorPermiso`; entrega por `SendNotificationCommand` (nuevo `NotificationType.Alert`) y
correo por `NotificationEmailDispatcher`. Dos arreglos mínimos de plataforma entran en I1 porque sin
ellos FR-022 no se cumple: `ListMyNotificationsQuery` (y sus hermanas) resuelven al usuario por
`IActorActual` (hoy responden `Auth.Unauthorized` por el `UserId` nulo) y `NotificationEmailDispatcher`
toma el arrendamiento `email.dispatch` por cooperativa (hoy dos réplicas pueden mandar dos veces). El
empuje por SignalR no se arregla (las pantallas consultan). La bandeja principal es `/inventario/alertas`.
SC-022: sin destinatario se enruta a `CompanyAdmin` y se marca `WithoutRecipient`.

### Facturación electrónica, adjuntos y compras

**T40 · Facturación electrónica.**
Decisión (DIAN 1–20, con los ajustes de T2, T3, T10, T16): puerto `ICanalDeEmisionElectronica` en
Application con `CapacidadesDelCanal` y `ResultadoDeCanal` uniforme (nunca lanza por rechazo;
`ChannelUnavailable` si no salió, `InProcess` si pudo llegar); canónico `DocumentoElectronicoCanonico` v1
construido por **un solo** `ConstructorDelCanonico` a partir de una entrada neutral que entrega el módulo
fuente por el puerto `IFuenteDeDocumentoElectronico` (Inventario lo implementa con
`FuenteDeEmisionDeInventario`), para que la plataforma no lea tablas `INV_`; proyecto
`Infrastructure/IngenIA365ERP.ElectronicInvoicing` con `CanalSimulado` (obligatorio para CI y ensayo), el
adaptador del proveedor cuando se contrate y `CanalServicioCentral` cuando exista el servicio de la 010;
máquina de estados pura `TransicionesDelDocumentoElectronico`; la llamada al canal **nunca** dentro de la
transacción de confirmación; POS espera hasta `Dian.EsperaMaximaPosSegundos` y si no, «pendiente de
entrega»; esa espera agotada **cuenta como falla** para `CircuitoDeCanal`, de modo que tras
`Dian.UmbralFallasCircuito` las ventas nuevas abren contingencia 03 y reciben su representación en papel
en el acto (así se mide SC-004 también en la rama «o el documento de contingencia»); ante resultado
ambiguo se consulta antes de reenviar; espera 15 s, 1, 2, 5, 15, 30 y 60 min y
luego cada hora hasta el plazo; `ProcesadorDeDocumentosElectronicos` (arrendamiento `einvoicing.process`
+ arrendamiento por fila `LeaseUntil`/`LeaseOwner`, porque el intento en línea del POS y el procesador
pueden coincidir) corre por `IEjecutorEnCooperativa`; contingencia 04 sólo la declara el canal; 03 por
circuito (`Dian.UmbralFallasCircuito`) o a mano; casos a/b/c por comandos separados con
`ReglaDeCorreccionFiscal` (huella económica); credenciales en el Secret `erp-fe-credenciales`, una clave
`{tenantPublicId}.{channelCode}.json` (`channelCode` en mayúsculas: `SIMULADO`, `SERVICIO-CENTRAL`) montada sin `subPath` en `/secrets/facturacion-electronica/`,
ruta derivada sólo de la cooperativa resuelta (`CredencialesEnArchivo`), guion
`tools/scripts/crear-secreto-facturacion-electronica.ps1`; `GuardiaDeEmisionFiscal.Evaluar` es la única
decisión (la consultan la confirmación, la apertura de caja y `GET /api/electronic-invoicing/readiness`);
catálogos DIAN como JSON versionados (`CatalogoDian`); representación gráfica propia con QuestPDF y
QRCoder (se agrega a `IngenIA365ERP.API.csproj`) en carta y 80 mm; correo por el ERP con `IEmailSender`.
`DianEnvironment` se traslada a `Domain/Enums/Dian`. Norma citada: Res. 165/2023 y 167/2021 compiladas
en la Res. Única 000227 del 23-09-2025, Título 5 (precisión aplicada a la spec en FR-063). La caja usa el
tipo del rol de contingencia que respalda al rol de la venta (`PosSaleContingency` o
`InvoiceContingency`, §2.5). A diferencia de la 010, un número rechazado se reusa
(caso a y b): hay que dejarlo escrito para que nadie copie la nómina.

**T41 · Adjuntos (D-04).**
Decisión: `AdjuntosDeModulo.DeModulo` suma `InventoryProduct` (imágenes; sube quien tiene
`Inventory.Catalog.Manage`; borrado auditado), `InventoryAdjustmentSupport` (soportes de bajas y ajustes
negativos; sube quien crea; no se borran tras confirmar), `ElectronicSalesDocument` (lectura
`Inventory.Sales.View`, `Borrable = false`, sin subidas), `ElectronicPurchaseDocument` (lectura
`Inventory.Purchases.View`, igual) y `DianContingencyEvent` (constancias y evidencias del evento 03/04;
lectura `ElectronicInvoicing.Contingencies.View`, no se borra, sube quien tiene
`ElectronicInvoicing.Contingencies.Declare`). La descarga de los artefactos (`POST
documents/{id}/download-link`) exige sólo el permiso del dueño del adjunto; sin él, 404. `AttachmentPolicy.ModuleGeneratedMimeTypes` (+ `application/xml`,
`application/zip`, `application/json`) sólo para lo que genera un módulo; las subidas de personas no
cambian. Conservación legal 5 años; transición a almacenamiento frío a los 90 días (no es borrado). El
XML de la factura del proveedor se lee y no se guarda (pregunta al dueño).

**T42 · Eventos RADIAN.**
Conflicto: `FEL_RadianEvents` (DIAN) frente a `INV_SupplierInvoiceEvents` (compras). Decisión:
`INV_SupplierInvoiceEvents` desde I1 (estado del 030 y 032 por factura a crédito; registro externo con
`RegisterExternalRadianEventCommand` y alerta `Compras.EventosRadianFaltantes`); desde I5 la emisión es un
`COR_ElectronicDocuments` de `Kind = RadianEvent030/032` por el mismo puerto y la misma máquina de
estados, enlazado por `ElectronicDocumentPublicId`, por una sola ruta de compras: `POST
/api/inventory/purchases/supplier-invoices/{id}/radian-events/emit` (`{ eventCodes }`, 202; errores
`Inventory.RadianEvent.OutOfOrder`, `.ReceiptNotConfirmed`, `.DateInvalid`). No se crea tabla de eventos
en `COR_`.

**T43 · Búsqueda de productos.**
Decisión (ventas 5, adelantada a I1 porque FR-020 rige en toda pantalla): lectura exacta por igualdad
(código de barras con UK filtrado entre vivos; código de producto) y búsqueda mientras se escribe sobre
`INV_Products.SearchText` normalizado en C# (`NormalizadorDeBusqueda`). Semántica: «contiene» por cada
término, todos obligatorios (`EF.Functions.Like` con `%término%` escapado, uno por término). PostgreSQL:
`pg_trgm` + GIN (`gin_trgm_ops`), que sirve el `LIKE '%…%'`. SQL Server: recorrido del índice no agrupado
cubriente sobre `SearchText` con `INCLUDE (Code, Name, Status)`, aceptable a 50.000 productos. Si T1009 no
cumple SC-009 en SQL Server, se activa como alternativa un índice de texto completo sobre `SearchText`
(requiere el componente de texto completo en el servidor). 200 ms de
espera, mínimo 2 caracteres, cancelación de la anterior. SC-009 se mide en los dos motores con 50.000
productos (Skip explícito si no corre). Se verifica que el rol de los tres clústeres pueda crear
`pg_trgm`.

### Presentación, operación y puesta en marcha

**T44 · Informes.**
Decisión (ventas 6): `TablaExportable` + `EntregaDeInformes.EntregarAsync`, una ruta por vista (§2.12)
para que `LosEndpointsProtegidosExigenPermiso` las revise una a una; permisos de §2.12; cada exportación
se audita desde el handler con `InventoryAuditEmitter` (PublicId de la cooperativa, por la bandeja de
auditoría); el alcance se aplica en la consulta. QuestPDF sólo para documentos que se firman o entregan.

**T45 · Pantallas, POS y clientes.**
Decisión (ventas 4b, 7, 8): páginas y menú de §2.11; POS en `Pages/Pos/PuntoDeVenta.razor` con
`PosLayout` (incluye `AvisoDeVencimientoDeSesion` y `RegistroDeAccesos`), tabla liviana sin SfGrid, campo
de lectura siempre enfocado, atajos F2 buscar, F3 cantidad, F4 cliente, F6 vendedor, F7 descuento, F8
suspender, F9 recuperar, F10 cobrar, Supr quitar, Esc volver, Ctrl+Alt+M/C/R/D (evitando F5, F11, F12,
Ctrl+W); `wwwroot/js/pos.js` (teclas reservadas, ráfagas del lector, impresión en iframe) cargado con
`@Assets` y en `App/wwwroot/index.html`; `IImpresionDeDocumentos` (web con `pos.js`, MAUI nativa);
`Components/Inventario/BuscadorDeProducto` reutilizable. Clientes tipados registrados en `Web/Program.cs`,
`Web.Client/Program.cs` y `App/MauiProgram.cs`; de paso `MauiProgram` registra `PermisosDelUsuario`,
`PersonasClient`, `NominaClient`, `ContabilidadClient` y `DescargaDeArchivos`, que hoy le faltan.
`CanalDeOrigenHandler` va **después** de `RenovacionDeSesionHandler` (que sigue primero). La caja del
equipo se recuerda en `localStorage` con try/catch; decide el servidor.

**T46 · Autorización de datos al dar de alta.**
Decisión (seguridad 13): `PersonaDialog` sigue siendo el único escritor, con `[Parameter]
CapturarAutorizacion` y modo compacto; `GET /api/compliance/habeas-data/policies/current`
(`Core.People.Create`); `AutorizacionAlCrear { Decision, PolicyVersionPublicId, Channel }` opcional en
`CreatePersonCommand` y en los compuestos `with-person`, escrito en el mismo `SaveChanges`; acción
`Declined`; `IAutorizacionDeDatos.VigenteAsync(personPublicId)`. Sin política publicada: se permite el
alta dejando «sin política vigente» y levanta la alerta `Personas.SinPoliticaDeDatos` (§2.13; pregunta
C8). Sólo por PublicId.

**T47 · Trabajos de fondo.**
Decisión: todo `BackgroundService` se registra sólo en `API/Program.cs` (el DbMigrator nunca los
arranca), recorre `ITenantDirectory.ListActiveAsync`, toma su arrendamiento por cooperativa y ejecuta por
`IEjecutorEnCooperativa`. Trabajos: `AuditOutboxForwarder` (I1), `ProgramadorDeTareas` (I1: reorden,
quiebre, eventos RADIAN faltantes, verificación de integridad nocturna; I4: alertas DIAN; I6: liberar
reservas vencidas), `NotificationEmailDispatcher` con arrendamiento (I1), `DespachadorDeMensajes` (I2:
entregas en línea, lotes programados y órdenes), `ProcesadorDeDocumentosElectronicos` (I4). Todos esperan
`DatabaseReadiness.IsReady` y se apagan por configuración en la fixture.

**T48 · Permisos.**
Decisión: familias y lista de §2.10; permiso fijo por ruta según `DocumentClassGroup` (no hay permisos
dinámicos por tipo de documento); `Create`, `Confirm` y `Approve` separados (segregación); lecturas
sensibles con acción distinta de `View`; perfiles sugeridos como plantillas (`GET
/api/admin/roles/templates?module=Inventory`, `CreateRoleFromTemplateCommand` con
`Security.Roles.Create`); a los roles integrados no se les agrega escritura de inventario.

**T49 · Plantillas de importación.**
Decisión (núcleo 12): `PlantillaDeImportacion` + `ITabularFileReader`; `ErrorDeFila` se mueve a
`Application/Common/Imports` (reexportado desde contabilidad); todo comando de importación recibe
`ModoDeImportacion { Review, Apply }` (revisión previa sin guardar, FR-030); formatos `0.0000`
(cantidades) y `0.000000` (costos) en `TipoDeColumna`; todo o nada, reglas compartidas con el alta
unitaria. Orden de carga: impuestos y retenciones, grupos contables, unidades, marcas, categorías,
productos, bodegas y ubicaciones, tipos de documento, vendedores (persona citada por documento; si no
existe, la fila se rechaza), … hasta saldo inicial (14), cifras de SOLIDO (15) y matriz (16). El orden
completo, las hojas, las columnas, `ImportResultDto`, la exportación con `?withData=true` y los códigos
(`Import.Invalid`, `Import.Cell.*`, `Archivo.HojaFaltante`) tienen **una sola fuente**:
`contracts/plantillas.md` (§0.1 y §1–§16); ningún otro artefacto los repite. **Todas** las plantillas de
FR-095 se publican en I1 (las de puntos y cajas, medios de pago, listas de precios y topes: el `GET
template.xlsx` en I1 y su importación en I3); la de la matriz en I2; la de cifras de SOLIDO en I1. El
modelo de cada catálogo se congela antes de publicar su plantilla.

**T50 · POS y caja.**
Decisión (ventas 2a–2g y 4a): el borrador del POS vive en el servidor desde la primera lectura, ligado a
la sesión y sin número; se auditan las acciones de riesgo (quitar línea, descuento, precio manual,
suspender, recuperar, descartar, cobrar, reimprimir), no cada lectura; no se cierra una sesión con ventas
suspendidas. Una sesión abierta por caja y por cajero (UK filtrados). Base por `Caja.BaseModo`. Todo
pago cuyo medio se arquea pertenece a una sesión abierta del usuario, también el cobro de oficina.
Movimientos de caja como documentos (incluye `ReclassificationBetweenMeans`). Cerrar la sesión es
inmediato; una diferencia ≠ 0 crea el documento `CashCountDifference` (dentro de la tolerancia: motivo
sin aprobación; por encima: niveles, nunca el cajero) y al confirmarse emite
`DiferenciaDeArqueoAprobada` (incluye las aceptadas dentro de tolerancia). `ShortageToCashier` exige
`SEC_Users.PersonId`. Cierre del día por punto con reapertura con permiso y motivo. El esperado lo
calcula `CalculadoraDeEsperado` (pura).

**T51 · Precios, descuentos y promociones.**
Decisión (ventas 3a–3c): listas con ámbito anulable y `ScopeKey` (unicidad portable); resolución pura por
más dimensiones coincidentes, empate cliente > segmento > canal > sucursal, general al final, y respaldo
por producto en la siguiente lista aplicable; la línea guarda la lista usada. Topes por rol
(`INV_DiscountCaps`, el mayor de los roles; sin fila = 0); cambiar el precio de lista cuenta como
descuento; sobre el tope, aprobación (T33). Descuento por total prorrateado a las líneas. Promociones en
I6 como descuentos no condicionados, no acumulables, gana la de mayor descuento; el esquema de descuentos
por línea existe desde I3. Segmento = `Associate.AssociateClass` validado contra los valores existentes.

**T52 · Consumidor final y copia fiscal.**
Decisión: `ConsumidorFinalSeeder` (I3) crea la persona genérica del maestro; la copia de identificación
fiscal de la contraparte es `INV_DocumentPartySnapshots`, sólo inserción, versión 1 al confirmar; el caso
a de FR-066 agrega la versión siguiente con antes y después auditados (y queda también en la versión del
documento electrónico). Reimpresiones y representación gráfica usan la copia vigente o el archivo firmado.

---

## 4. Preguntas al dueño (consolidadas)

Las 79 preguntas de los siete temas quedan en 75 sin duplicados (se fundieron zona horaria, identidad
del actor, validaciones de la contadora y marcas tributarias, que aparecían dos veces); se suman tres
que salen de los riesgos y de esta armonización (A10, D7, H4), y tres de la revisión de coherencia
(C10, D8, D9): 81 en total. Cada una tiene una propuesta por defecto
coherente con la spec y la constitución; si el dueño no responde, **rige la propuesta**. «Bloquea» dice
qué no puede salir sin una respuesta explícita: una entrega, el ensayo (D-07) o la salida en producción.

### A. Salida de COOFLOPAL y operación

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| A1 | Si el diagnóstico encuentra filas en las tablas heredadas de alguna cooperativa, ¿se aprueba el retiro por base (fila `INV.RetiroHeredado.Aprobado` con `pg_dump` y segundo revisor)? ¿Quién es el segundo revisor? | Aprobación por base, con respaldo y revisor nombrado por el dueño | Despliegue de I1 en el ambiente donde haya filas |
| A2 | ¿Se verifican antes de I1, en los tres clústeres, el usuario de Mongo de la API (rol de sólo inserción sobre las bases por cooperativa) y que `AuditSignature` de producción no sea la clave `dev-v1`? | Sí, como tarea operativa; clave de anclas en versión nueva | I1 en producción |
| A3 | ¿Qué proveedor tecnológico contrata COOFLOPAL (o Ingenia365 como integrador)? ¿Se acepta la lista corta The Factory HKA / Dataico para una prueba pagada en sandbox (latencia p95 ≤ 5 s con 30 cajas, precio por ~150.000 documentos al mes), con credenciales por empresa y no de socio? | Sí; mientras tanto se desarrolla y ensaya con `CanalSimulado` | Adaptador real de I4 y la salida |
| A4 | ¿Cómo factura electrónicamente COOFLOPAL hoy? ¿Qué resoluciones, prefijos y software asociado tiene? ¿Tiene resolución de contingencia y numeración del documento equivalente POS? | Se registran como dato en `/maestros/resoluciones-dian` | Ensayo de I4 |
| A5 | Correo al comprador: ¿desde qué buzón y dominio (SPF/DKIM del relay actual)? | Lo envía el ERP (`Dian.EntregaCorreo = Erp`) | Salida (I4) |
| A6 | Impresión: ¿PC con Chrome/Edge en modo kiosco (`--kiosk-printing`) o app de Windows? ¿Impresoras de 58 u 80 mm? | Kiosco y 80 mm; guía operativa | Salida (operativa), no el desarrollo |
| A7 | ¿COOFLOPAL vende productos con INC, impuesto de bolsas, ICUI o IBUA? | Semilla con IVA 19/5, exento, excluido, INC y bolsas por unidad; ICUI/IBUA como tarifas por unidad si aplica | Ensayo (semilla tributaria validada) |
| A8 | Antes del ensayo, ¿valida la contadora los tipos de comprobante (`FV`, `EI`, `SI`, `NV`, `CP`, `TR`, `AC`, `CJ`) y cruces, los códigos DIAN sugeridos por clase de medio de pago (10, 48, 49, 42, 47, 20, 71…), las cuentas por medio y los valores de la semilla tributaria? | Se siembran marcados «pendiente de validar por la contadora» | Ensayo |
| A9 | ¿Vende COOFLOPAL productos pesados con códigos de balanza (EAN-13 con precio o peso)? | Fuera de alcance | No |
| A10 | ¿Acepta el dueño que sin POS fuera de línea una caída del ERP o de la red detiene la caja (la contingencia DIAN no la cubre)? | Sí (fuera de alcance por la spec) | No |

### B. Plataforma

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| B1 | ¿Se corrige en toda la plataforma `ICurrentUserService.UserId` (identidad central → `SEC_Users.Id`)? Arregla el cuatro ojos de Contabilidad (hoy `0 == 0` bloquea a todos) y la auditoría «system», pero cambia comportamientos existentes. | No en esta feature: `IActorActual` (T6); arreglo como tarea aparte con prueba del cuatro ojos | No |
| B2 | Zona horaria: ¿`America/Bogota` para toda la plataforma o por cooperativa? ¿Se pasan ya Contabilidad y Nómina a la fecha local? | `America/Bogota` por configuración de plataforma; los demás módulos la adoptan aparte | No |
| B3 | ¿Cuánto se conservan las claves de idempotencia (`COR_OperationKeys`)? | Indefinidamente (nada se borra solo) | No |
| B4 | Candado del despachador: ¿Redis base 0, ranura de la cooperativa o base SQL? | Arrendamiento en la base de la cooperativa (T10) | No |
| B5 | ¿Se pasa el despachador de correo al arrendamiento por cooperativa para evitar correos dobles con 2 réplicas? | Sí, en I1 | No |
| B6 | ¿El lote manual responde 202 y corre en segundo plano, con el avance en la bandeja? | Sí | No |
| B7 | ¿Se excluyen del diff de auditoría los cambios técnicos de estado de las entregas (queda la bitácora de intentos y el evento del comando)? | Sí | No |

### C. Auditoría y seguridad

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| C1 | ¿La cadena de sellos se extiende a Contabilidad (misma retención de 10 años)? | No por ahora: Inventario, sus piezas de plataforma y Navegación (T38) | No |
| C2 | ¿Basta el anclaje en SQL con HMAC, o se copia cada ancla a un almacén externo inmutable (S3 Object Lock)? | SQL + HMAC; copia externa después | No |
| C3 | ¿Se conserva indefinidamente la fila delgada (`Seq`, `Hash`) del outbox de auditoría, o se justifica una purga técnica? | Fila delgada indefinida | No |
| C4 | Monto máximo con varios roles: ¿el mayor o el menor? ¿Un rol sin fila de límite es «sin límite»? | El mayor; sin fila = sin límite | No |
| C5 | ¿Un usuario sin bodegas ni cajas asignadas no opera ninguna (falla cerrado), salvo con alcance total? | Sí | No |
| C6 | Niveles altos de aprobación: ¿dos permisos genéricos o cualquier código del catálogo? | Cualquier código del catálogo; `Approvals.Supervisor` y `.Management` como sugeridos | No |
| C7 | ¿Perfiles sugeridos como plantillas que se vuelven roles editables, o roles integrados fijos? | Plantillas | No |
| C8 | Alta desde el POS sin política de Habeas Data publicada: ¿se bloquea o se permite con constancia y alerta? | Se permite con constancia «sin política vigente» y alerta al administrador | No |
| C9 | Aprobación de descuentos sobre tope: ¿sólo remota o también presencial con passkey o TOTP del supervisor? | Ambas, nunca contraseña; WebAuthn en MAUI se prueba a mano | No |
| C10 | FR-009 pide que «sin permiso» responda igual que «inexistente». Cuando el recurso ya es visible y el permiso que falta depende del cuerpo (costo indicado, aceptar diferencia de activación, remisiones sin facturar, tipo fiscal sin paso, clave de parámetro), ¿se acepta 422 con código propio, que no revela existencia? | Sí: 422 con código propio (§2.9), como precisión de FR-009 | No |

### D. Catálogo, costeo y existencias

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| D1 | Largo del código de producto y de bodega (códigos de SOLIDO, referencias de proveedor). | Producto 20 (`LargoLargo`); bodega y demás catálogos 10 | Publicación de plantillas (I1) |
| D2 | Ámbito de costeo cooperativa cuando la contadora mapea bodegas del mismo grupo a cuentas de inventario distintas. | Ámbito cooperativa; la completitud avisa y exige una cuenta de inventario por grupo en ese ámbito | No |
| D3 | Residuo de redondeo: ¿a la línea o bodega de mayor valor, o a la última? | Mayor valor | No |
| D4 | Precio unitario de venta con 6 decimales o con 2. | Lista en pesos (18,2); precio de línea (18,6) | No |
| D5 | Prorrateo con promedio ponderado: regla para separar lo vendido de lo existente. | Proporción = mín(1, existencia actual / cantidad recibida), validada por la contadora | I5 |
| D6 | Retroactivos con PEPS: ¿se admiten o se restringen al promedio ponderado? | Sólo promedio ponderado | I5 |
| D7 | ¿El documento equivalente POS pertenece a la cadena de ventas de FR-075 y por eso comparte modo de paso con la factura de oficina? | Sí (lectura literal de FR-075) | No |
| D8 | Puesta en marcha bodega por bodega con ámbito de costeo cooperativa: el saldo inicial de una bodega que se activa después queda fechado antes de ventas ya registradas en otras. ¿Se admite como excepción a `Costeo.RetroactivosPermitidos`, o se exige `Costeo.Ambito = Bodega` mientras haya bodegas no activas? | Excepción: un `OpeningBalance` (y su `Voiding`) de una bodega `NotActivated` no depende del parámetro; el motor lo inserta en `(OperationDate, Id)`, recalcula las salidas posteriores y registra `AjusteDeCostoReconocido` por documento afectado (T18, entregado en I1) | I1 (salida de la segunda bodega) |
| D9 | Los ajustes que genera un conteo aprobado se fechan en la foto (FR-041) y suelen quedar antes de movimientos de otras bodegas. ¿Se admiten siempre, sin depender de `Costeo.RetroactivosPermitidos`? | Sí, con el mismo soporte retroactivo mínimo de D8 en I1 (precisión aplicada a FR-045) | I1 |

### E. Compras e impuestos

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| E1 | ¿El catálogo de impuestos y retenciones queda en Core? | Sí (T22) | No |
| E2 | ¿Se lee la UVT de `PAY_LegalParameters` con un solo lector? ¿Quién la mantiene si la cooperativa no usa nómina? | Sí; la mantiene quien tenga el permiso de parámetros legales | No |
| E3 | Enmienda de la 008: ¿se agregan las marcas tributarias al maestro y al diálogo único? ¿Cómo se traducen `TaxRegime`, `SourceWithholding` e `IcaType` de SOLIDO? | Sí; la traducción la define la contadora | Ensayo (cargue de personas con retenciones) |
| E4 | ¿Código DANE en `COR_Cities` (con DIVIPOLA) y municipio en `COR_Branches` como municipio por defecto de la operación? | Sí; conciliar las ciudades existentes por `LegacyCode` | No |
| E5 | Tolerancias del cruce: ¿porcentaje **y** valor, o cualquiera? ¿Excepción por proveedor? | Ambas; una por cooperativa | No (I5) |
| E6 | En I1 (dos vías), ¿una diferencia de precio sólo ajusta el costo o también se retiene? | Sólo ajusta el costo | No |
| E7 | Antes de I5, ¿cómo se trata el flete facturado aparte? | Al gasto; si viene en la misma factura, en el costo de la recepción | No |
| E8 | Factura a crédito sin 030 y 032: ¿sólo alerta o IVA «descontable condicionado»? | Sólo alerta | No |
| E9 | Notas parciales: ¿retenciones en proporción, con tarifas del original y sin volver a probar la base mínima? | Sí | No |
| E10 | ¿Se guarda el XML de la factura electrónica del proveedor (art. 632 ET), aunque la spec diga que sólo se lee? | No se guarda | No |
| E11 | Impuestos por unidad y varias tarifas de retención: ¿cuenta sin «exige base gravable» o enmienda de la regla 10 de la 009? ¿Una auxiliar por tarifa de ReteFuente/ReteICA? | Cuenta sin «exige base» para los de valor por unidad; una auxiliar por tarifa | No |

### F. Ventas, POS, caja y medios de pago

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| F1 | ¿Medios de pago, franquicias, adquirentes y datáfonos en Core, y en el POS sólo dónde se ofrecen? | Sí (T25) | No |
| F2 | Base de caja: ¿fondo fijo heredado o base del día desde caja fuerte? | Fondo fijo | No |
| F3 | ¿Las diferencias dentro de la tolerancia también se contabilizan? | Sí | No |
| F4 | Faltante: ¿a cargo del cajero (exige persona vinculada) o al gasto? ¿Sobrante siempre a su cuenta? | Gasto; sobrante a su cuenta | No |
| F5 | ¿Reclasificación entre medios como movimiento de caja? | Sí | No |
| F6 | ¿Arqueo ciego opcional? | Parámetro, apagado | No |
| F7 | ¿Un cajero, una sola sesión abierta en toda la cooperativa? | Sí (parámetro) | No |
| F8 | Si la lista más específica no trae un producto, ¿se toma de la siguiente aplicable? | Sí | No |
| F9 | Promociones en conflicto: ¿gana la de mayor descuento? ¿Un descuento manual se suma a una promoción? | Mayor descuento; el manual no se suma | No (I6) |
| F10 | Segmento del asociado: ¿texto de la clase o catálogo nuevo? | Texto de `AssociateClass`, validado contra los valores existentes | No |
| F11 | Bonos: ¿unicidad por medio? ¿Vuelven a quedar disponibles al anular o devolver? | Sí y sí | No |
| F12 | ¿El cierre del día se puede reabrir con permiso y motivo? | Sí | No |
| F13 | Auditoría del POS: ¿acciones de riesgo o cada lectura? | Acciones de riesgo | No |
| F14 | ¿Un cobro en efectivo desde la oficina exige sesión de caja abierta? | Sí | No |

### G. Contabilidad

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| G1 | ¿Un comprobante por documento con venta y costo, o dos? | Uno | No |
| G2 | La anulación ¿refleja las cuentas del original (reglas a su fecha) aunque la matriz haya cambiado? | Sí | No |
| G3 | ¿Se bloquea una versión de regla con vigencia anterior a lo ya contabilizado? | Sí; los errores pasados se corrigen con un CG manual | No |
| G4 | ¿Quién ordena el lote manual y quién reprocesa? | Lote: `Accounting.InventoryBatches.Run`; reproceso y envío posterior: Inventario | No |
| G5 | ¿La conciliación ignora el alcance de sucursal contable del usuario? | Sí (sólo agregados por cuenta) | No |
| G6 | Cierre de mes con mensajes pendientes: ¿avisar o bloquear? | Avisar y exigir reconocimiento, con «Procesar ahora» | No |
| G7 | ¿Inactivar una cuenta usada por la matriz avisa o bloquea? | Avisa; la completitud la lista | No |
| G8 | Resumidos: ¿acepta la contadora el detalle por documento de las líneas de impuesto? | Sí (FR-077 lo exige) | No |
| G9 | Los relacionados de un documento por lotes, ¿siguen también su horario? | Sí | No |

### H. Cartera (IC y D-02)

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| H1 | ¿Basta el orden por cadena de documento, o la entrega a Cartera debe ir en orden estricto por persona? | Por cadena; `PersonPublicId` va en el mensaje | IC |
| H2 | ¿Quién registra la cuenta por cobrar según la clase de crédito? | Comercial: Contabilidad; asociados: Cartera con reclasificación de la provisional | IC |
| H3 | Crédito provisional: ¿plazos y cuotas por medio? ¿Asociado activo en el maestro? ¿Cliente con `IsCustomer`? | Plazos y cuotas como datos del medio; se exigen ambas marcas | No |
| H4 | ¿Acepta el dueño la lista de lo que debe cubrir la spec de Cartera (`research.md` R26: consultas, recepción idempotente, obligaciones desde documentos comerciales, ajustes, crédito comercial, cuenta por cobrar, validación a la fecha, recaudo, IVA sobre financiación, permisos y conciliación)? | Sí, como insumo de D-02 | IC |

### I. DIAN

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| I1 | ¿Cuántos segundos espera la caja la validación? Sin respuesta, ¿basta «pendiente de entrega»? | 15 s; pendiente de entrega (sin comprobante provisional); cada espera agotada cuenta como falla del circuito y, pasado `Dian.UmbralFallasCircuito`, las ventas nuevas abren contingencia 03 con representación en papel (T40) | No |
| I2 | Contingencia 03: ¿automática por circuito y manual, o sólo manual? ¿Quién presenta la constancia ante la DIAN? | Ambas; la cooperativa | No |
| I3 | ¿Se guarda el PDF del proveedor? ¿Almacenamiento frío a los 90 días para lo que se conserva 5 años? | No; sí | No |
| I4 | Clave técnica: ¿en la base de la cooperativa, enmascarada, o en el Secret? | En la base, enmascarada y fuera del diff | No |
| I5 | ¿«Obligada a facturar electrónicamente» = sí por defecto para toda cooperativa nueva? | Sí | No |
| I6 | Documento soporte: ¿por operación o semanal? ¿Y una cooperativa no obligada con compras a no obligados? | Por operación; el DS sigue su propia norma, independiente del parámetro de venta | No |
| I7 | Si el comprador pide factura después del DEE POS, ¿se agrega el flujo nota de ajuste + factura? | **Resuelta en la spec** (FR-063): sí, en I4; contrato en `contracts/api.md` §18.3.1 | No |

---

## 5. Entregas: qué va en cada una

Ruta crítica de COOFLOPAL: **I1 → I2 → I3 → I4**, con I4 construido en paralelo sobre `CanalSimulado`.
El ensayo exige I1–I4 disponibles (FR-095). IC espera D-02; I5 e I6 van después de la salida.

### I1 · Núcleo (y la plataforma que todo lo demás necesita)

- **Primero**: el `GET template.xlsx` de todas las plantillas de FR-095 (incluidas las de puntos y cajas,
  medios de pago, listas y topes, que se importan en I3) y la de cifras de SOLIDO; modelo de cada
  catálogo congelado.
- **Migraciones**: `RetiroDelInventarioHeredado` (commit propio), `PlataformaParaInventario`,
  `InventarioComercialNucleo`; diagnóstico `diagnostico-inventario-heredado.sql`.
- **Plataforma**: `ContextoAmbiental` + `IEjecutorEnCooperativa` + lectura del ambiental en los accesores
  + guarda de `MongoAuditService`; `IActorActual`, `IOrigenDeLaPeticion`, `CanalDeOrigenHandler`;
  `IdempotencyBehavior` + `COR_OperationKeys`; `TransaccionExplicita`; `COR_BackgroundLeases`;
  `COR_ParameterVersions` + `LectorDeParametros`; motor de aprobaciones + `COR_Approval*`;
  `SEC_PermissionAmountLimits`; alertas (`COR_AlertTypes`, `COR_Alerts`) + arreglo de
  `ListMyNotificationsQuery` + arrendamiento del despachador de correo; auditoría (origen, motivo,
  rechazos, interceptor ampliado, `COR_AuditOutbox`, `AuditOutboxForwarder`, cadena, anclas, verificación,
  retención 10 años); `HoyLocal`; `COR_IntegrationMessages`, `…Dependencies`, `…Deliveries` +
  `EmisorDeMensajes` (sin consumidores todavía); `ProgramadorDeTareas`.
- **Core**: `COR_Tax*` + `MotorTributario` + `IvaDescontable` + `LectorDeUvt`; DIVIPOLA;
  `COR_Branches.MunicipalityDaneCode`; marcas tributarias de `COR_People` y sección «Datos tributarios»;
  `AutorizacionAlCrear`, acción `Declined`, `policies/current`.
- **Inventario**: catálogo completo de A salvo I6 (imágenes, códigos de barras, unidades, impuestos del
  producto, cambio de grupo con `GrupoContableReclasificado`), canales, bodegas, ubicaciones, tránsito
  por sucursal (creado con la primera bodega operativa, código propuesto y editable), reorden; tipos,
  secuencias, causas; clases operables `PurchaseReceipt`,
  `SupplierInvoice`, `SupplierNote`, `SupplierReturn`, `PositiveAdjustment`, `NegativeAdjustment`,
  `InternalConsumption` (el retiro gravado espera la lista general de I3), `WriteOff`, `OpeningBalance`,
  `TransferDispatch`, `TransferReceipt`, `LocationMove`, `PhysicalCount`, `CostAdjustment`, `Voiding`;
  compra directa; kardex, proyecciones, promedio ponderado con sus dos ámbitos, cerrojo, numerador,
  verificación y reconstrucción; **retroactivo mínimo** para el saldo inicial de bodegas no activas y los
  ajustes de conteo (T18, caso dorado 17); períodos; saldo inicial, cifras de SOLIDO y comparativos (la
  activación espera I2: antes de I2 el camino con `acceptDifference` y sin comparación existe sólo fuera
  de producción; en producción el POST responde 422 `Inventory.Activation.AccountingUnavailable` mientras
  no exista la consulta de saldos); vendedores; alcance por bodega; búsqueda de productos; RADIAN
  externo; permisos y perfiles; retiro de rutas, pantallas y menú del módulo actual.
- **Rutas**: catálogo, bodegas, existencias, tipos, documentos, ajustes, traslados, conteos, compras
  (`receipts`, `supplier-invoices`, `supplier-notes`, `returns`, `direct`, `radian-events`), períodos,
  saldo inicial, cifras, vendedores (`/api/inventory/salespeople`), parámetros, aprobaciones, montos,
  alcances, alertas, plantillas; catálogo tributario `/api/core/{taxes, tax-rates, withholding-concepts}*`;
  `POST /api/audit/integrity/verify`; plantillas de rol; informes de I1.
- **Pantallas**: las de I1 en §2.11 (Inventario, Compras, `/ventas/vendedores`, `/ventas/canales`,
  `/maestros/impuestos`, pestañas en `/admin/*`).
- **Pruebas**: arquitectura nuevas y ampliadas de §2.18 que apliquen; casos dorados de costeo, impuestos,
  aprobaciones; integración `ElProcesoEnSegundoPlanAuditaEnSuCooperativaTests`,
  `IdempotenciaDeOperacionesTests`, `ConcurrenciaDeExistenciasTests`, `CompraDirectaTests`,
  `TrasladoEnDosPasosTests`, `ConteoYAjusteTests`, `CierreDePeriodoTests`, `AprobacionMultinivelTests`,
  `AlcancePorBodegaTests`, `IntegridadDeAuditoriaTests`.

### I2 · Integración con Contabilidad

- **Migración** `IntegracionContableDeInventario`; semillas `voucher-types.json` (+`NV`, `CP`, `TR`,
  `AC`, `CJ`) e `InventoryVoucherMappingsSeeder`.
- **Plataforma**: `DespachadorDeMensajes`, `IDestinoDeMensajes`, `IMensajesEntrantes`,
  `RegisterDeliveryResultCommand`, `COR_IntegrationDeliveryAttempts`, lotes (`COR_IntegrationBatches`,
  `COR_IntegrationBatchCounters`, `OrderIntegrationBatchCommand`, `PreviewIntegrationBatchQuery`),
  `ReprocessMessagesCommand`, `SendNotApplicableMessagesCommand`, `ListIntegrationMessagesQuery`.
- **Contabilidad (enmienda 009)**: matriz (`ACC_InventoryPostingRules`, comandos, plantilla, pantallas),
  `ACC_InventoryVoucherMappings`, consumidor (`DestinoContabilidad`, `PostInventoryMessagesCommand`,
  `PostInventorySummaryGroupCommand`, `ACC_InventoryPostings`), `IContabilidadParaInventario` con sus
  cuatro consultas, `AccountingPoster.ValidarVariosAsync`, guarda de reversión, `PostingRequest.RegistradoPor`,
  `ClosePeriodCommand.AcknowledgeInventoryPending`, sobrecarga de `MovimientosContables`,
  `EnlacesDeOrigen`, `AccountReferenceFinder` con la matriz, `ListInvalidParameterizationsQuery`,
  `ACC_AccountTaxRates.Rate` (9,6), y los textos de la 009 que enumera `contracts/contabilidad.md` §12
  (spec, research, contrato, data-model, tasks, plan, manual, CLAUDE.md).
- **Inventario**: validación previa en la confirmación, modo de paso con efecto, bandeja, conciliación,
  activación de bodegas (`ActivateWarehouseCommand` con la comparación de FR-090), alertas de
  integración.
- **Rutas**: `/api/inventory/messages*`, `/api/inventory/warehouses/{id}/activation`,
  `/api/accounting/inventory/*`; vistas `messages`, `accounting-batches`, `reconciliation`.
- **Pantallas**: `/inventario/bandeja-de-mensajes`, `/inventario/conciliacion`, `/inventario/activacion`,
  `/contabilidad/inventario/*`, aviso en Períodos.
- **Pruebas**: `InventarioNoConoceContabilidadNiCartera`, `LoDeInventarioNoSeReversa`,
  `LosComandosDeConsumoNoTienenRuta`, `ContabilizacionPorMensajesTests`, `EntregaGarantizadaTests`,
  `LotesProgramadosTests`, `SaldoInicialYActivacionTests`; SC-020 con `RUN_PERF_TESTS`.

### I3 · Ventas y POS

- **Migración** `VentasYPuntoDeVenta`; semillas de denominaciones, `EFECTIVO` y consumidor final.
- **Core**: `COR_PaymentMeans`, `COR_CardNetworks`, `COR_CardAcquirers`, `COR_CardTerminals`,
  `COR_CashDenominations`; `/api/core/payment-means*`; `/maestros/medios-de-pago`.
- **Inventario**: puntos, cajas y sus tipos por rol, canales en el punto, disponibilidad de medios,
  sesiones, POS con borrador en el servidor, pagos mixtos, bonos, vueltas, movimientos de caja, arqueo por
  medio, diferencias y su aprobación, cierre del día; clases `SalesInvoice`, `PosEquivalentDocument`,
  `CreditNote`, `PosAdjustmentNote` (confirman sólo si `GuardiaDeEmisionFiscal` responde `Electronic`, es
  decir con I4 en una cooperativa obligada) y `NonElectronicSalesReceipt`, `NonElectronicSalesNote` (no
  obligadas); devoluciones al costo de salida; crédito provisional con `VentaACreditoRegistrada` y
  `AjusteDeVentaACredito` acumulados; listas de precios, topes y aprobación de descuentos; retiro gravado
  habilitado; alcance por punto (`INV_UserPointOfSaleScopes`); informes de ventas y caja, `impairment`;
  `pos.js`, `PosLayout`, `IImpresionDeDocumentos`; `CashMovementReceiptReport`, `CashCountReport`,
  `SalesDocumentReport` (comprobante no electrónico en carta).
- **Pruebas**: `LosPagosNoGuardanElNumeroDeTarjeta`, casos dorados de precios y caja,
  `VentaPosCompletaTests`, `BonoUnicoConcurrenteTests`, `UnaSesionPorCajaTests`,
  `BusquedaDeProductos50kTests`, `CreditoProvisionalTests`.

### IC · Crédito con Cartera (pendiente de D-02)

- Destino `Lending` registrado (`IDestinoDeMensajes` y consumidor según la spec de Cartera), implementación
  real de `IConsultasDeCartera`, parámetros `Cartera.IntegracionHabilitadaDesde`,
  `Cartera.PoliticaSinRespuesta`, `Cartera.ConsultaSegundos`; entrega en orden de lo acumulado;
  validación (`ValidationFailed` y alerta `Integracion.ValidacionFallida`). Sin tablas `INV_` nuevas.

### I4 · Documentos electrónicos DIAN

- **Proyecto** `Infrastructure/IngenIA365ERP.ElectronicInvoicing`; migración `DocumentosElectronicos`;
  QRCoder en la API; Secret `erp-fe-credenciales` y su guion; volumen en GitOps.
- **Piezas**: puerto y registro de canales, `CanalSimulado`, adaptador del proveedor, canónico v1 y
  `IFuenteDeDocumentoElectronico` (+ `FuenteDeEmisionDeInventario`), `CatalogoDian`, `NumeradorFiscal`,
  `GuardiaDeEmisionFiscal` y `readiness`, `ProcesadorDeDocumentosElectronicos`, circuito y contingencias
  03/04, casos a/b/c, cambio de canal con vigencia, representación gráfica (carta y 80 mm), correo,
  adjuntos `ElectronicSalesDocument`/`ElectronicPurchaseDocument`/`DianContingencyEvent` y `ModuleGeneratedMimeTypes`; clases
  `SupportDocument` y `SupportDocumentAdjustmentNote` operables; alertas DIAN.
- **Rutas y pantallas**: `/api/electronic-invoicing/*`, `/api/inventory/purchases/support-documents`,
  vista `dian-documents`; `/admin/facturacion-electronica`, `/maestros/resoluciones-dian`,
  `/ventas/documentos-electronicos`, `/ventas/contingencias-dian`, `/compras/documentos-soporte`.
- **Pruebas**: `ElProveedorTecnologicoSoloLoConoceSuAdaptador`, `LasCredencialesDeFacturacionNoTocanLaBase`,
  transiciones prohibidas, `DocumentosElectronicosTests`.

### I5 · Compras completas y costeo avanzado

Solicitud y orden (`PurchaseRequest`, `PurchaseOrder`), recepción contra orden y parcial, cruce a tres
vías (`CruceDeCompra`, `INV_PurchaseMatchLines`, aprobación de excepciones), costos adicionales
(`LandedCost`, `INV_LandedCostAllocations`, `Prorrateo`), emisión RADIAN (`EmitRadianEventCommand`), PEPS
(`INV_CostLayers`, `INV_LayerConsumptions`), retroactivos generales con `Costeo.RetroactivosPermitidos` y
`SimularImpacto` (`POST /api/inventory/documents/{id}/cost-impact`; el mínimo de puesta en marcha y de
conteos ya viene de I1), cambio de método con
`method-change-valuation`; migración `ComprasYCosteoAvanzado`; pantallas `/compras/{solicitudes, ordenes,
cruce, costos-adicionales}`; vista `purchase-matches`.

### I6 · Comercio ampliado y analítica

Cotización, pedido con reserva (`INV_Reservations` y su liberación programada), remisión, factura desde
remisiones, nota débito (y la electrónica), promociones (`MotorDePromociones`), variantes, combos, kits y
ensamble, lote, serie y vencimiento, conteo por clase ABC, reposición y sugerido de compra, reportes
avanzados (`margin`, `turnover`, `abc`, `no-movement`, `expiring`, `purchase-suggestion`,
`shrinkage-cap`) y tablero; migración `ComercioAmpliado`; pantallas `/ventas/{cotizaciones, pedidos,
remisiones, notas-debito, promociones}` y `/inventario/tablero`.

### Lo que esta feature **no** hace (para que nadie lo agregue por su cuenta)

Corregir `ICurrentUserService.UserId` en toda la plataforma; arreglar el empuje de SignalR; retirar
`COR_PaymentMethods` o `COR_Sequences`; migrar `PAY_CompanyPolicies` o la UVT a Core; encadenar la
auditoría de Contabilidad; pasar Contabilidad y Nómina a la fecha local; POS fuera de línea; Ingenia365
como proveedor tecnológico; lo interno de Cartera (D-02).
