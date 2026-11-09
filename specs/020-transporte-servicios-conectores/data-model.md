# Data Model — 020 Transporte, servicios por vehículo y conectores externos

**Fecha**: 2026-11-08 · **Research**: [research.md](research.md)

## Convenciones

- **Entidades**:
  - Toda entidad hereda de `AuditableEntity`: `Id` interno, `PublicId`, auditoría y soft-delete.
  - Su configuración declara `HasQueryFilter(e => !e.IsDeleted)`.
  - Las marcadas **hecho** implementan `IHechoInmutable` y no se editan.
- **Migraciones**:
  - Son aditivas, en pares (PostgreSQL y SQL Server).
  - Hay tres:
    - **`ModulosYConectoresDePlataforma`**: base administrativa.
    - **`Transporte`**: base de cada cooperativa.
    - **`ConectoresDeLaCooperativa`**: base de cada cooperativa.
  - Las tres son nuevas y no tocan tablas existentes, salvo `COR_People.IsDriver`, una columna
    `bool` con defecto `false`.
- **Índices únicos «entre vivos»**: filtrados con `IsDeleted = false` y, donde aplica,
  `Status <> Retired`.

---

## 1. Base administrativa (`ADM_`)

### ADM_TenantModules

Un módulo habilitable por cooperativa (R1).

| Campo | Tipo | Regla |
|---|---|---|
| TenantId | int FK `ADM_Tenants` | |
| ModuleCode | varchar(5) | `TRN` hoy; catálogo fijo en código, `ModulosHabilitables` |
| IsEnabled | bool | |
| ChangedAt / ChangedBy / Reason | | motivo obligatorio (≤500) |

- Índice único `(TenantId, ModuleCode)` entre vivas.

### ADM_Connectors

| Campo | Tipo | Regla |
|---|---|---|
| Code | varchar(20) | `CodigoDeCatalogo` largo, único |
| Name | varchar(120) | |
| Description | varchar(500) | |
| IsActive | bool | inactivo = ninguna cooperativa lo puede usar |
| CurrentVersionId | int? | la versión publicada vigente |

### ADM_ConnectorVersions (hecho)

| Campo | Tipo | Regla |
|---|---|---|
| ConnectorId | int FK | |
| Number | int | 1, 2, 3… único por conector |
| BaseUrl | varchar(300) | `https://`, host público (R17) |
| DefinitionJson | text | `auth`, `operation`, `variables`, `responses`, `maxAttempts`, `timeoutSeconds` (R15), validado contra `DefinicionDeConector` de Domain |
| DefinitionSha256 | char(64) | |
| PublishedAt / PublishedBy / Note | | |

### ADM_TenantConnectors

| Campo | Tipo | Regla |
|---|---|---|
| TenantId | int FK | |
| ConnectorId | int FK | |
| IsEnabled | bool | |
| ChangedAt / ChangedBy / Reason | | |

- Índice único `(TenantId, ConnectorId)` entre vivas.

---

## 2. Base de la cooperativa: Transporte (`TRN_`)

### TRN_CatalogItems

| Campo | Tipo | Regla |
|---|---|---|
| Kind | smallint | `TransportCatalogKind`: Brand 1, VehicleClass 2, BodyType 3, Fuel 4, Modality 5, ServiceClass 6, TransitAuthority 7, RollingGroup 8, EquipmentType 9 |
| Code | varchar(10) | `CodigoDeCatalogo` |
| Name | varchar(120) | |
| IsActive | bool | inactivar, nunca borrar si está en uso |
| IsSystem | bool | sembrado; no se borra |

- Índice único `(Kind, Code)` entre vivos.

### TRN_DocumentTypes

| Campo | Tipo | Regla |
|---|---|---|
| Code | varchar(10) | `SOAT`, `RTM`, `TOP`, `RCC`, `RCE`, `TODORIESGO`, `REVPREV` sembrados |
| Name | varchar(120) | |
| IsRequired | bool | |
| WarningDays | int | 0..365 |
| IsActive / IsSystem | bool | |

### TRN_Vehicles

| Campo | Tipo | Regla |
|---|---|---|
| Plate | varchar(10) | normalizada (mayúsculas, sin espacios ni guiones); única entre vivos |
| InternalNumber | varchar(10) | obligatorio; único entre vivos |
| RadioCode | varchar(20)? | único entre vivos si no es nulo |
| OwnerPersonId | int FK `COR_People` | vigente |
| OwnerSince | date | |
| Status | smallint | Active 1, Inactive 2, Retired 3 |
| StatusSince | date | |
| **Ficha técnica** | | |
| BrandId, VehicleClassId, BodyTypeId, FuelId, ModalityId, ServiceClassId, TransitAuthorityId | int? FK `TRN_CatalogItems` | el `Kind` se valida |
| Line | varchar(60)? | |
| ModelYear | smallint? | 1950..año+1 |
| Color | varchar(40)? | |
| EngineDisplacementCc | int? | |
| PassengerCapacity | smallint? | |
| EngineNumber | varchar(40)? | |
| ChassisNumber / Vin | varchar(40)? | |
| TransitLicenseNumber | varchar(30)? | |
| RegistrationDate | date? | |
| **Vinculación** | | |
| JoinedOn | date | fecha de ingreso; obligatoria |
| AffiliationContractNumber | varchar(30)? | |
| AffiliationContractFrom / To | date? | `To ≥ From` |
| OperationCardNumber | varchar(30)? | número de la tarjeta de operación (su vencimiento va en documentos) |
| RollingGroupId | int? FK `TRN_CatalogItems` | |
| MinDailyRolling / MinMonthlyRolling | decimal(18,2)? | ≥ 0, informativo |
| RetiredOn / RetirementReason | date? / varchar(500)? | sólo con `Status = Retired` |
| BranchId | int? FK `COR_Branches` | oficina del vehículo (alcance de sucursal; propuesta: la principal) |

### TRN_VehicleEvents (hecho)

| Campo | Tipo | Regla |
|---|---|---|
| VehicleId | int FK | |
| Kind | smallint | Registered 1, StatusChanged 2, OwnerChanged 3, Reentered 4 |
| EffectiveDate | date | no anterior al último evento del mismo tipo |
| FromValue / ToValue | varchar(60) | estado o `PublicId` de la persona |
| FromAffiliation / ToAffiliation | smallint? | asociado o externo **sellado** en el cambio de propietario |
| Reason | varchar(500) | obligatorio salvo Registered |
| Source | smallint | Screen 1, Import 2 |

### TRN_VehicleEquipment

| Campo | Tipo | Regla |
|---|---|---|
| VehicleId | int FK | |
| EquipmentTypeId | int FK `TRN_CatalogItems` (EquipmentType) | |
| Code | varchar(40)? | serie del taxímetro, código del radio, usuario de la app… |
| Status | smallint | Active 1, Inactive 2 |

- Único `(VehicleId, EquipmentTypeId)` entre activos.

### TRN_VehicleDocuments

| Campo | Tipo | Regla |
|---|---|---|
| VehicleId | int FK | |
| DocumentTypeId | int FK | |
| Number | varchar(40) | |
| Issuer | varchar(120)? | aseguradora o CDA |
| IssuedOn / ValidFrom | date? | |
| ExpiresOn | date | `≥ ValidFrom` |
| SupersededAt | datetime? | escrito una vez, al registrar otro del mismo tipo |

- Único `(VehicleId, DocumentTypeId)` entre los no reemplazados.

### TRN_Drivers y TRN_DriverAssignments

**TRN_Drivers**: `PersonId` (único entre vivos), `Status` (Active o Retired), `Since`, `RetiredOn`.
`RolDeConductor` escribe `COR_People.IsDriver`.

**TRN_DriverAssignments**:

| Campo | Tipo | Regla |
|---|---|---|
| DriverId | int FK | |
| VehicleId | int FK | |
| From | date | |
| To | date? | `≥ From` |
| IsPrimary | bool | a lo sumo un titular vigente por vehículo |

- Un mismo conductor no se asigna dos veces al mismo vehículo con vigencias cruzadas.

### TRN_Services

Un producto de Comercial como servicio de vehículo (R9).

| Campo | Tipo | Regla |
|---|---|---|
| ProductPublicId | uuid | producto `Kind = Service`; único entre vivos |
| Priority | int | 1 = primero; orden de líneas, pagos y reparto |
| CreditLineCode | varchar(10) | línea de Cartera `IsService`, sin interés (R11) |
| AssociateCreditMeansCode / CustomerCreditMeansCode | varchar(10) | medios de pago a crédito |
| ConnectorId | int? FK `COR_Connectors` | réplica; nulo = no notifica |
| IsActive | bool | |

### TRN_Subscriptions y TRN_SubscriptionPrices

**TRN_Subscriptions**:

| Campo | Tipo | Regla |
|---|---|---|
| VehicleId | int FK | |
| ServiceId | int FK | |
| From | date | |
| To | date? | `≥ From` |
| Reason | varchar(500)? | |

- Una sola suscripción por vehículo y servicio con vigencias cruzadas.

**TRN_SubscriptionPrices**: SubscriptionId, Price (decimal(18,2) > 0), From, To?. Sin vigencias
cruzadas.

### TRN_BillingPolicies (con vigencia)

| Campo | Tipo | Regla |
|---|---|---|
| ValidFrom / ValidTo | date / date? | sin cruces |
| Proration | smallint | FullMonth 1 (defecto), Prorated 2 |
| SalesDocumentTypeCode | varchar(10) | tipo de documento de venta de Comercial |
| WarehouseCode / SalesPointCode | varchar(10)? | lo que exija ese tipo |
| DueDay | smallint | día de vencimiento de la cuota (1..28, defecto el último del mes = 0) |

### TRN_BillingRuns

| Campo | Tipo | Regla |
|---|---|---|
| Period | char(6) | `aaaamm` |
| Mode | smallint | Batch 1, Single 2 |
| PaymentMode | smallint | Credit 1, Cash 2 |
| Status | smallint | Queued, Running, Finished, FinishedWithOmissions, Failed |
| StartedAt / FinishedAt / StartedBy | | |
| InvoicesCount / TotalAmount | int / decimal | |
| SummaryJson | text | omitidos: `{vehículo, motivo, código}` |
| OperationKey | uuid | idempotencia del lanzamiento |

### TRN_ServiceCharges

El cargo de un servicio a un vehículo en un período (R10, R13, R14).

| Campo | Tipo | Regla |
|---|---|---|
| VehicleId / ServiceId | int FK | |
| Period | char(6) | |
| BillingRunId | int FK | |
| OwnerPersonId | int FK | el propietario facturado |
| InvoicePublicId | uuid | `INV_Documents` |
| InvoiceNumber | varchar(30) | con prefijo |
| InvoiceLinePublicId | uuid | `INV_DocumentLines` |
| PaymentPublicId | uuid? | `INV_DocumentPayments` del servicio (a crédito o de contado) |
| LoanPublicId | uuid? | la obligación de Cartera, cuando se conoce (R12) |
| Amount | decimal(18,2) | valor de la línea = valor de su pago |
| Route | smallint | Cartera 1, CommercialReceivable 2, Cash 3 |
| Status | smallint | Open 1, Paid 2, Voided 3 |
| PaidAt | datetime? | |
| ExternalId | uuid | se genera al crear y **nunca cambia** |
| NotifiedRequestPublicId | uuid? | `COR_ConnectorRequests` si se notificó |
| ReviewFlag | smallint | None 0, PaymentVoidedAfterNotice 1, PaidAgainAfterVoid 2, NoConnector 3 |

- Índice **único** `(VehicleId, ServiceId, Period)` entre `Status <> Voided` (FR-031).
- Índices por `LoanPublicId`, `PaymentPublicId` e `InvoiceNumber`.

### TRN_ChargeAllocations (hecho)

| Campo | Tipo | Regla |
|---|---|---|
| ChargeId | int FK | |
| Source | smallint | CarteraPayment 1, CommercialReceipt 2, CashAtInvoice 3 |
| SourceMessagePublicId | uuid? | el mensaje de la bandeja que la originó (idempotencia) |
| ReceiptPublicId / ReceiptNumber | uuid? / varchar | |
| Amount | decimal(18,2) | negativo en una reversa |
| ReversesAllocationId | int? | |
| AppliedAt | datetime | |

- Único `(ChargeId, SourceMessagePublicId)`: el mismo mensaje no asigna dos veces.

---

## 3. Base de la cooperativa: conectores (`COR_`)

### COR_Connectors y COR_ConnectorVersions (réplica)

Copia de `ADM_Connectors` y `ADM_ConnectorVersions` para las cooperativas habilitadas (R15):
`PlatformPublicId`, `Code`, `Name`, `IsEnabled` (de `ADM_TenantConnectors`), la versión vigente y
sus versiones con `Number`, `BaseUrl`, `DefinitionJson` y `DefinitionSha256`. La escribe sólo
`IProvisionadorDeConectores`.

### COR_ConnectorCredentials

| Campo | Tipo | Regla |
|---|---|---|
| ConnectorId | int FK | único entre vivos |
| ProtectedPayload | text | DataProtection, propósito `IngenIA365ERP.Connectors.Credential.v1`; `[NoAuditar]`; nunca en DTO, export ni log (R16) |
| SetAt / SetBy | | |
| LastVerifiedAt / LastVerificationOk / LastVerificationMessage | | resultado de «probar conexión», sin token |

### COR_ConnectorRequests

El envío que se ve en el tablero (R18).

| Campo | Tipo | Regla |
|---|---|---|
| MessagePublicId | uuid | `IntegrationMessage` de la `SolicitudDeConectorV1`; único |
| ConnectorId / VersionNumber | | versión con que salió |
| OriginRef | varchar(60) | `TRN:{cargo:N}` (el conector no conoce Transporte) |
| ExternalId | uuid | el identificador externo |
| Status | smallint | Pending, Sent, SentWithWarning, Retrying, Review, OnHold, PaymentVoided, ResolvedManually |
| LastStatusCode | int? | |
| LastResponseExcerpt | varchar(2000)? | |
| Warning | varchar(500)? | |
| ResolvedBy / ResolvedAt / ResolutionReason | | cierre manual |

### COR_ConnectorCalls (hecho)

Un intento: `RequestId`, `AttemptNumber`, `StartedAt`, `DurationMs`, `Kind` (Login u Operation),
`StatusCode`, `Outcome`, `ResponseExcerpt` (2 KB), `RequestBodySanitized`.

---

## 4. Cambios en lo existente

| Dónde | Cambio |
|---|---|
| `COR_People` | `IsDriver bool` (defecto `false`); la escribe sólo `RolDeConductor` |
| `IntegrationParticipantClass` | `Transport`, `Connectors` |
| `IntegrationDestinations` | `TRN` (Transporte), `CON` (Conectores) |
| `ModulosDeOrigen` | `TRN` |
| `TipoDeMensaje` | `DestinosOpcionales` (R12); `PagoAplicado`, `PagoReversado`, `ReciboEmitido`, `ReciboAnulado` y `DocumentoAnulado` declaran `TRN` |
| `SalesLineInput` | `PrecioPactado` (sólo con borrador de proceso, R9) |
| `IConsultaDeObligaciones` | `PorPagoDeVentaAsync(paymentPublicIds)` → obligación, número, estado |
| `MyPermissionsDto` | `EnabledModules` |
| `IAdminDbContext` | `TenantModules`, `Connectors`, `ConnectorVersions`, `TenantConnectors` |

## 5. Transiciones de estado

**Vehículo**

```text
Active ──inactivar──▶ Inactive ──activar──▶ Active
Active|Inactive ──retirar──▶ Retired   (terminal; reingreso = vehículo nuevo)
```

**Cargo de servicio**

```text
Open ──Σ asignaciones ≥ valor──▶ Paid ──reversa (Σ < valor)──▶ Open
Open ──anular factura (DocumentoAnulado)──▶ Voided
Paid ──anular factura──▶ queda Paid + ReviewFlag (el pago sigue vivo hasta que Recaudo/Cartera lo reverse)
```

Al pasar por primera vez a `Paid`, con conector configurado, se emite `SolicitudDeConectorV1`. Nunca
una segunda vez (R14).

**Envío de conector**

```text
Pending ──200 Done──▶ Sent
Pending ──Done con aviso──▶ SentWithWarning
Pending ──Retry/401──▶ Retrying ──…──▶ Sent | Review (reintentos agotados)
Pending|Retrying ──conector deshabilitado o sin credenciales──▶ OnHold ──habilitar──▶ Retrying
Pending ──ManualReview/no previsto──▶ Review ──reprocesar──▶ Retrying
Sent|SentWithWarning ──pago reversado──▶ PaymentVoided
Review|PaymentVoided ──cerrar a mano con motivo──▶ ResolvedManually
```

## 6. Validaciones clave (servidor; la pantalla las repite)

| Código | Cuándo |
|---|---|
| `Vehicle.PlateDuplicate` / `.InternalNumberDuplicate` / `.RadioCodeDuplicate` | con `existingPublicId` del vehículo vivo que la tiene |
| `Vehicle.OwnerRequired`, `Vehicle.OwnerNotFound` | |
| `Vehicle.EffectiveDateBeforeLastChange` | |
| `Vehicle.Retired` | no se edita, salvo la consulta |
| `Vehicle.CatalogKindMismatch` | |
| `Driver.AlreadyAssigned` | |
| `Driver.PrimaryAlreadyExists` | |
| `Subscription.Overlaps`, `SubscriptionPrice.Overlaps` | |
| `TransportService.ProductNotService` | |
| `TransportService.CreditLineNotService` | la línea no es de servicio o tiene interés |
| `Billing.PeriodAlreadyBilled` | sólo en una corrida individual |
| `Billing.NoPolicy` | |
| `Connector.InsecureUrl` | |
| `Connector.PrivateAddress` | |
| `Connector.UnknownVariable` | |
| `Connector.NotEnabled` | |
| `Connector.NoCredentials` | |
| `Module.NotEnabled` | 403 |
