# Data Model — 015 Recaudo y Tesorería

**Feature**: 015 | **Fecha**: 2026-10-22 | **Rama**: `015-recaudo-tesoreria` | **Spec**: [spec.md](spec.md)

Tablas, columnas principales, índices, estados, cerrojos, precisión, enumeraciones, errores, alertas, parámetros y
migraciones de Recaudo (`RCD_`), Tesorería (`TES_`), las piezas de plataforma (`COR_`, E-06), el lado contable
(`ACC_`, E-04) y las columnas que suman Cartera y Captaciones (`LND_`, E-01 y E-02). Cada tabla dice su **entrega**
(R0, T0, R1, T1, R2, T2) y su migración. Los nombres los fija [decisiones-transversales.md](decisiones-transversales.md)
§2; las decisiones, [research.md](research.md).

Partes:
- §0 convenciones;
- §1–§8 Recaudo;
- §9–§13 Tesorería, §14 lo que cruza entre los dos módulos;
- §15 activación, cuadre y puesta en marcha;
- §16 plataforma, §17 contabilidad, §18 Cartera y Captaciones;
- §19 estados y transiciones, §20 enumeraciones, §21 errores, §22 alertas, §23 parámetros;
- §24 retiro de lo heredado, §25 migraciones por entrega, §26 nombres fijados.

---

## 0. Convenciones

### 0.1 Entidad base

- Toda entidad hereda `AuditableEntity` (`int Id`, `PublicId` con UK `UK_{Tabla}_PublicId`, `IsDeleted`/`DeletedAt`/
  `DeletedBy`, `RowVersion`, `CreatedAt/By`, `UpdatedAt/By`); esas columnas no se repiten. `AuditableEntityLong`
  (`bigint Id`) en las de volumen: `RCD_Receipts`, `RCD_ReceiptMeans`, `RCD_ReceiptApplications`,
  `RCD_ReceiptApplicationPortions`, `RCD_CashTransactions`, `TES_BankStatementLines`.
- Toda configuración declara `HasQueryFilter(e => !e.IsDeleted)`. Los catálogos con código llevan UK filtrado
  `[IsDeleted] = 0` y el código sigue `CodigoDeCatalogo` (10 caracteres, mayúsculas, sin espacios). Los documentos
  **nunca** se dan de baja y sus UK van sin filtro (FR-002, Principio VII).
- Claves foráneas `Restrict`, nunca en cascada. Hacia fuera del módulo sólo hay FK a la plataforma y al maestro:
  `COR_People`, `COR_Branches` (oficina), `SEC_Users`, `COR_Banks`, `COR_PaymentMeans`, `COR_CashDenominations`,
  `COR_CardTerminals`, `COR_BankFileFormats`. **Nunca** hay FK entre `RCD_` y `TES_`, ni hacia `ACC_`, `LND_`, `INV_`,
  `PAY_` o `TRS_`: lo que cruza se guarda como `…PublicId` y número (R-05, `RecaudoYTesoreriaSeHablanPorSusPuertos`).
- Adjuntos por `…AttachmentPublicId` (Guid de `COR_Attachments`, sin FK), con su tipo en `AdjuntosDeModulo`.

### 0.2 Hechos inmutables (marca H)

**H** = `IHechoInmutable`: se inserta una vez; `GuardaDeInmutabilidad` rechaza `Modified` y `Deleted` salvo las
columnas `[EscrituraUnica]` que pasan de nulo a valor (`LosHechosInmutablesNoSeModifican`). Son H: recibos, medios,
aplicaciones, porciones, anulaciones, aplicaciones reversadas por el módulo, egresos, movimientos de caja y de bóveda,
arqueos cerrados, cierres del día, ajustes de sesión, declaraciones, transacciones en efectivo, líneas de extracto,
archivos de pago y sus líneas, resultados del giro, eventos de cheques, conciliaciones cerradas (como versión). Los
**documentos con estado** (sesión, operación de caja, orden de pago, lote, cheque, partida, saldo a favor, remesa,
conciliación abierta) son mutables, pero cada tabla dice qué columnas cambian, en qué estado y por qué comando.

### 0.3 Números y PublicId

Todo documento expone `PublicId` y un `Number` visible (texto ≤ 30) que asigna el numerador al final de la
confirmación (R-13). Ninguna ruta acepta `Id` (Principio VI).

### 0.4 Precisión (`PrecisionDeRecaudo`, `PrecisionDeTesoreria`)

| Alias | Tipo | Uso |
|---|---|---|
| `Monto` | `decimal(18,2)` | todo valor en pesos |
| `Tarifa` | `decimal(9,6)` | tarifas y fracciones (GMF, tolerancias porcentuales) |
| `Cantidad` | `int` | cantidad de billetes o monedas por denominación |
| `Facial` | `decimal(18,2)` | valor facial de una denominación (catálogo de la 012) |

### 0.5 Cerrojos en orden fijo (FR-007, FR-170, R-12)

`CerrojoDeRecaudo` y `CerrojoDeTesoreria` (en `Persistence/Receipts`, `Persistence/Treasury`) toman filas en **una
sentencia por nivel ordenada por `Id`**, con `SELECT … FOR UPDATE` en PostgreSQL y `WITH (UPDLOCK, ROWLOCK, HOLDLOCK)`
en SQL Server. Los niveles que la transacción ya tiene no esperan.

**Recaudo** (cualquier operación que mueva dinero):

| # | Nivel | Tabla | Modo |
|---|---|---|---|
| 1 | Configuración | `RCD_Setup` (fila única) | compartido |
| 2 | Día del punto | `RCD_CollectionPointDays` del punto y día | compartido (exclusivo al cerrar el día) |
| 3 | Sesión | `RCD_CashSessions` | exclusivo |
| 4 | Bóveda | `RCD_Vaults` de la oficina (sólo con movimiento de bóveda o remesa) | exclusivo |
| 5 | Pagadores | `RCD_PayerControls` de pagador, quien efectúa y beneficiarios, por `Id` | exclusivo (la fila se crea si falta) |
| 6 | Orígenes | `RCD_PayerCreditBalances`, `RCD_UnidentifiedItems`, `RCD_PaymasterRemittanceLines`, `RCD_ReceivedChecks`, `RCD_Receipts` (al anular) por `Id` | exclusivo |
| 7 | Contratos | Cartera (`LND_Setup` → deudores → créditos) **y después** Captaciones (`LND_Setup` → persona → cuenta), por `CerrojoDeCartera` dentro de cada contrato | los toma el contrato |
| 8 | Tesorería por puerto | `TES_BankAccounts` de la cuenta de recaudo o de la remesa | compartido |
| 9 | Numeración | `COR_Sequences` del tipo de documento | exclusivo, al final |

**Tesorería**:

| # | Nivel | Tabla | Modo |
|---|---|---|---|
| 1 | Configuración | `TES_Setup` | compartido |
| 2 | Cuenta de origen | `TES_BankAccounts` | exclusivo al aprobar, generar, emitir cheque o confirmar |
| 3 | Chequera | `TES_Checkbooks` | exclusivo al emitir o anular |
| 4 | Lote | `TES_PaymentBatches` | exclusivo |
| 5 | Órdenes | `TES_PaymentOrders` por `Id` | exclusivo |
| 6 | Conciliación | `TES_Reconciliations` de la cuenta y mes | exclusivo al cruzar o cerrar |
| 7 | Numeración | `COR_Sequences` | exclusivo, al final |

**El recibo numera antes de los contratos** (decisión del dueño del 2026-10-23, DT §2.7): el nivel 9 se toma después del 6 y antes del 7, porque el contrato guarda el número del recibo en su operación; todos los demás documentos numeran al final. Ninguna operación toma una persona después de una cuenta suya (FR-170). `LosCerrojosDeRecaudoYTesoreriaVanEnOrden`
fija que sólo estos dos tipos toman cerrojos y el orden de las llamadas a los contratos.

**Dirección Tesorería → Recaudo** (CH16, TR10). El orden global es Recaudo antes que Tesorería también cuando quien
llama es Tesorería. Un comando de Tesorería que va a llamar a `IResultadoDeOrdenesDeRecaudo` (resultado de una orden de
origen `Receipts`: registrar el resultado, en bloque, rechazo técnico del archivo, entrega o anulación de un cheque) o a
`IPartidasDesdeConciliacion` (abono sin registro de una conciliación) llama **primero** a `TomarCerrojosAsync` del
puerto, que toma con `CerrojoDeRecaudo` los niveles de Recaudo que esa escritura necesita:

| Puerto | Niveles de Recaudo que toma antes | Después, Tesorería toma |
|---|---|---|
| `IResultadoDeOrdenesDeRecaudo` | 1 `RCD_Setup` (compartido) y 6 las filas de origen de esas órdenes (`RCD_PayerCreditBalances`, `RCD_UnidentifiedItems`) por `Id` | 1 `TES_Setup`, 2 la cuenta, 4 el lote, 5 las órdenes |
| `IPartidasDesdeConciliacion` | 1 `RCD_Setup` (compartido); la partida nueva no tiene fila que tomar | 1 `TES_Setup`, 6 la conciliación |

Ninguna transacción toma una fila `RCD_` después de una `TES_`; `LosCerrojosDeRecaudoYTesoreriaVanEnOrden` lo fija
(«ningún comando de `Treasury` llama a un método de escritura de esos puertos sin haber llamado antes a
`TomarCerrojosAsync`»).

### 0.6 Fechas

Fecha local `America/Bogota` (`HoyLocal`). Todo documento con dinero lleva `RegisteredAt` (instante UTC), `ValueDate`
(caja: hoy; banco: la del movimiento; pagaduría: la del descuento) y `AccountingDate` (la del período abierto que fija
Contabilidad al contabilizar; en Recaudo y Tesorería es la fecha propuesta y el hecho la lleva) (FR-008).

### 0.7 Versiones con vigencia

Catálogos con vigencia (`ValidFrom`, `ValidTo` nulo = abierta) no se solapan por código; un cambio cierra la vigencia
la víspera y abre otra. Lo que una sesión, un lote o un recibo selló al nacer no cambia (FR-173).

---

## 1. Recaudo · configuración (R0, `RecaudoNucleo`)

### 1.1 `RCD_Setup` (fila única)

| Columna | Tipo | Nota |
|---|---|---|
| `Status` | `ReceiptsModuleStatus` | `NotActivated` → `Active` (sólo por `ActivateReceiptsCommand`) |
| `ActivatedOn` | date? | fecha de activación |
| `CutoffTime` | time | hora de corte del día de Recaudo (FR-185); copia del parámetro vigente al activar |
| `ContractVersionObligations` · `ContractVersionAccounts` | smallint | versiones adoptadas que la operación usa (1 en R0) |

Se crea en la migración con `WHERE NOT EXISTS`. Toda operación la toma compartida (nivel 1).

### 1.2 `RCD_CollectionPoints` — puntos de recaudo

`BranchId` (FK `COR_Branches`), `Code` (UK filtrado), `Name`, `Status` (`Active`/`Inactive`), `ValidFrom`, `ValidTo`,
`DocumentPrefix` (opcional, FR-026), `IsMainCashBoxVault` (bool: la oficina usa su caja principal como bóveda, FR-180).

### 1.3 `RCD_CashBoxes` — cajas

`CollectionPointId`, `Code` (UK por punto), `Name`, `BaseMode` (`DailyBase` por defecto, `FixedFund`, D8),
`BaseAmount` (Monto), `CashWarnCap` (Monto?, tope de aviso), `CashBlockCap` (Monto?, FR-181: nulo = no bloquea),
`DailyCashOutCap` (Monto?, R1), `IsMainCashBox` (bool, FR-180), `Status`, `ValidFrom`, `ValidTo`. Un cambio rige desde la
próxima sesión (US1-3).

### 1.4 `RCD_CashBoxCashiers`

`CashBoxId`, `UserId` (FK `SEC_Users`; el usuario debe tener persona), `ValidFrom`, `ValidTo`. UK
`(CashBoxId, UserId, ValidFrom)`.

### 1.5 `RCD_PaymentMeansCollectionPoints` — medios ofrecidos por punto (FR-023)

`PaymentMeansId` (FK `COR_PaymentMeans`), `CollectionPointId`, `ValidFrom`, `ValidTo`. Validación al ofrecer: clase de
crédito → `Receipts.PaymentMeans.CreditClassNotAllowed` (FR-024, US1-1); tarjeta sin adquirente o datáfono →
`Receipts.PaymentMeans.CardSetupIncomplete` (US1-2); clase cheque mientras `Caja.AdmiteCheque` sea falso →
`Receipts.PaymentMeans.CheckDisabled`; en R0, clases distintas de efectivo, transferencia, consignación y cheque →
`Receipts.PaymentMeans.NotInDelivery`.

### 1.6 `RCD_DocumentTypes` — tipos de documento (FR-026)

`Code` (UK), `Name`, `Kind` (`ReceiptDocumentKind`: `CashReceipt`, `BankReceipt`, `AgreementReceipt` (R2),
`PaymasterReceipt`, `OnlineReceipt` (R2), `ContingencyReceipt`, `CashDisbursement` (R1), `Voiding`, `CashMovement`,
`SessionAdjustment`), `UsesPointPrefix` (bool), `Status`. Semilla: uno por clase. La numeración vive en `COR_Sequences`
con clave `RCD:{code}:{prefijo}`.

### 1.7 `RCD_MiscConcepts` — conceptos varios (FR-027, FR-179)

`Code` (UK), `Name`, `Direction` (`Income`/`Expense`), `RequiresThirdParty`, `RequiresCrossDocument`,
`RequiresInvoice` (bool, FR-179: no se cobra sin factura de Comercial como documento cruce), `RequiresApproval`,
`MaxAmount` (Monto?), `IsSystem` (bool: «giro devuelto», «sobrantes», «reintegros», «rendimientos» vienen sembrados y no
se inactivan), `Status`. Egresos varios apagados por defecto (FR-055, R1).

### 1.8 `RCD_ContingencyBooks` — talonarios de contingencia (FR-186)

`CollectionPointId`, `FromNumber`, `ToNumber` (int), `RegisteredBy`, `Status` (`Active`/`Exhausted`/`Voided`). Los números
usados se ven por `RCD_Receipts.ContingencyNumber`; un informe lista los no usados.

### 1.9 `RCD_SupervisorDelegations` (FR-184)

`CollectionPointId`, `SupervisorUserId`, `DelegateUserId`, `ValidFrom`, `ValidTo`, `Reason`. Delega aprobar diferencias y
cerrar el día; la segregación sigue siendo por persona.

### 1.10 `RCD_ChannelModalityRules` — modalidad por canal (FR-198, D38)

`Channel` (`ReceiptChannel`), `Target` (`Loan`/`Account`), `LoanModality` (`ModalidadDePago`?), `AccountModality`
(`ModalidadDeConsignacion`?), `Priority`, `ValidFrom`, `ValidTo`. Semilla: créditos `CycleInstallment`; cuentas
`Installments` si hay exigibles, si no `Free`.

### 1.11 Alcance (FR-011, R-30)

`RCD_UserCollectionPointScopes` (`UserId`, `CollectionPointId`), `RCD_UserCashBoxScopes` (`UserId`, `CashBoxId`),
`RCD_UserPaymasterScopes` (`UserId`, `PaymasterPersonId`). Sin fila para un usuario con permiso de alcance total
(`Receipts.Scope.All`), falla cerrado.

---

## 2. Recaudo · sesión, arqueo y bóveda (R0, `RecaudoNucleo`)

### 2.1 `RCD_CollectionPointDays` — día del punto (FR-033, FR-184)

`CollectionPointId`, `BusinessDate`, `Status` (`Open` → `Closed`), `ClosedBy`, `ClosedAt`, `DayCloseReportAttachmentPublicId`.
UK `(CollectionPointId, BusinessDate)`. Cerrar el día exige todas las sesiones del punto y día en `Closed` y sus
diferencias resueltas (`Receipts.Day.SessionsOpen`, `.DifferencesPending`).

### 2.2 `RCD_CashSessions` — sesión de caja

| Columna | Tipo | Nota |
|---|---|---|
| `CashBoxId` · `CashierUserId` · `CollectionPointDayId` | FK | |
| `Status` | `CashSessionState` | §19.2 |
| `OpenedAt` · `ClosedAt` | datetime | |
| `OpeningBase` | Monto | contado por denominaciones (`RCD_CashMovementDenominations` del movimiento de apertura) |
| `SealedSettingsJson` | text | modo de base, topes, arqueo ciego, tolerancias por medio, medios del punto y versión de parámetros al abrir (R-14) |
| `ExpectedByMeansJson` | text | proyección del esperado por medio; se recalcula en cada documento y se verifica al cerrar con `ArqueoDeCaja` |
| `IsForgotten` | bool | al cruzar la hora de corte (FR-034) |
| `ClosedByUserId` · `WitnessUserId` | FK? | cierre diferido por el supervisor con testigo |

Índices: único filtrado `(CashBoxId)` con `Status` en (`Open`, `Closing`, `ForgottenOpen`) — una sesión abierta por
caja; con `Caja.UnaSesionPorCajero`, único filtrado `(CashierUserId)` en esos estados (FR-022, US2-2: el rechazo dice
quién la tiene, `Receipts.Session.AlreadyOpen`).

### 2.3 `RCD_CashCounts` (H al cerrar) · `RCD_CashCountLines` · `RCD_CashCountDenominations`

- `RCD_CashCounts`: `CashSessionId` o `VaultId`, `Kind` (`Closing`, `Surprise`, `Deferred`), `CountedAt`,
  `CountedBy`, `WitnessUserId?`, `IsBlind` (sellado), `MinutesAttachmentPublicId?` (acta del sorpresivo).
- `RCD_CashCountLines`: por medio — `PaymentMeansId`, `Method` (`CashCountMethod` de la 012), `Expected`, `Counted`,
  `Difference`, `Tolerance`, `Reason?`, `ApprovalRequestPublicId?`, `Treatment` (`CashierReceivable`, `Expense`,
  `Surplus`, `None`).
- `RCD_CashCountDenominations`: `CashCountLineId`, `CashDenominationId`, `Quantity` (Cantidad), `Amount`.
- Para tarjetas, `RCD_CashCountTerminalBatches` (`CashCountLineId`, `CardTerminalId`, `BatchNumber`, `Amount`); para
  referencias y cheques, `RCD_CashCountReferences` (`CashCountLineId`, `ReceiptMeansId`, `Confirmed`).

### 2.4 `RCD_CashMovements` (H) · `RCD_CashMovementDenominations`

`Number`, `DocumentTypeId`, `Kind` (`CashMovementKindR`: `Opening`, `SupplyFromVault`, `WithdrawToVault`,
`TransferBetweenBoxes`, `MeansReclassification`, `Custody` (dinero retenido), `CustodyRelease`), `CashSessionId`,
`CounterpartSessionId?`, `VaultId?`, `FromMeansId?`, `ToMeansId?`, `Amount`, `Reason`, `SupportAttachmentPublicId?`
(obligatorio en la reclasificación desde efectivo, FR-030), `ReceiptId?` (reclasificación), `ApprovalRequestPublicId?`,
`SecondCustodianUserId?` (doble custodia, FR-182). La reclasificación **no toca el recibo**: mueve el esperado de un medio
a otro y entra al informe de excepciones (US2-7).

### 2.5 `RCD_CashSessionAdjustments` (H) — ajuste de una sesión cerrada (FR-033, US2-5)

`Number`, `CashSessionId`, `Kind` (`CountCorrection`, `MeansCorrection`), `Amount`, `Reason`, `ApprovalRequestPublicId`,
`SupportAttachmentPublicId`. Emite su hecho.

### 2.6 `RCD_Vaults` · `RCD_VaultCustodians` · `RCD_VaultMovements` (H) · `RCD_VaultDenominationBalances`

- `RCD_Vaults`: `BranchId` (UK), `Mode` (`Vault`/`MainCashBox`, con `MainCashBoxId?`), `Status`.
- `RCD_VaultCustodians`: `VaultId`, `UserId`, `ValidFrom`, `ValidTo`.
- `RCD_VaultMovements`: `Number`, `VaultId`, `Kind` (`SupplyToBox`, `ReceiveFromBox`, `RemittanceOut`,
  `RemittanceIn`, `Count`), `CashMovementId?`, `RemittancePublicId?` (de `TES_CashRemittances`), `Amount`,
  `CustodianUserId`, `SecondCustodianUserId` (obligatorio con `Caja.DobleCustodia`, activo por defecto, D34),
  `BagSeal?`, `MinutesAttachmentPublicId?`; denominaciones en `RCD_VaultMovementDenominations`.
- `RCD_VaultDenominationBalances`: proyección mutable `(VaultId, CashDenominationId) → Quantity, Amount`, que el
  cuadre diario compara contra la suma de movimientos (FR-036).

### 2.7 `RCD_DayCloses` (H) · `RCD_DayCloseLines`

Cierre del día del punto: `CollectionPointDayId`, totales por medio (`Expected`, `Counted`, `Difference`), cantidad de
sesiones, recibos y anulaciones; informe PDF con código de verificación (FR-033, FR-156).

### 2.8 `RCD_DailySquares` — cuadre diario por oficina (FR-036)

`BranchId`, `BusinessDate`, `CashInBoxes`, `CashInVault`, `CashInTransit`, `LedgerBalance` (de `SaldoContableAsync`),
`EmittedPendingBatch`, `Difference`, `Explanation?`, `Status` (`Squared`, `Difference`, `Explained`). Lo produce la tarea
`recaudo.cuadre-diario` después del lote del día (FR-185); sólo informa y alerta.

---

## 3. Recaudo · operación de caja y recibo (R0, `RecaudoNucleo`)

### 3.1 `RCD_PayerControls` — fila de cerrojo por persona

`PersonId` (UK). Sin datos de negocio; se crea si falta (molde de `LND_DebtorControls`). Serializa operaciones de
Recaudo sobre la misma persona.

### 3.2 `RCD_CashOperations` — operación de caja (FR-189, R-07)

| Columna | Tipo | Nota |
|---|---|---|
| `PublicId` | Guid | **será el `PublicId` del recibo** |
| `CashSessionId` | FK | |
| `PayerPersonId` · `BeneficiaryPersonId?` · `DeliveredByPersonId?` | FK `COR_People` | pagador, titular cuando paga un tercero, quien efectúa (FR-191) |
| `Status` | `CashOperationState` | `Preparing`, `Blocked`, `Withheld`, `Confirmed`, `Abandoned` (§19.3) |
| `ItemsJson` | text | aplicaciones elegidas con su `PublicId`, modalidad, valor, elección del deudor y última simulación (identificador, huella, avisos) |
| `MeansJson` | text | medios con valor y referencia |
| `ImputationJson` | text | porciones calculadas por `ImputacionDeMedios` o fijadas por el cajero |
| `OperationKey` | Guid | `Idempotency-Key` de la confirmación |
| `ScreeningOutcome` | `ScreeningOutcomeR` | `Clear`, `BindingMatch`, `NonBindingMatch` |
| `CustodyMovementId?` | FK | dinero retenido en custodia (lista no vinculante con retención) |
| `ReleasedBy?` · `ReleasedAt?` | | liberación por el oficial |
| `ReceiptId?` | FK | al confirmar |

No es H: cambia mientras prepara. Un `Blocked` no admite medios ni confirmación (`Receipts.Compliance.BindingListMatch`).

### 3.3 `RCD_Receipts` (H) — el recibo

| Columna | Tipo | Nota |
|---|---|---|
| `Number` · `DocumentTypeId` | | numerado al final |
| `Channel` | `ReceiptChannel` | `CashDesk`, `Bank`, `Paymaster`, `Agreement` (R2), `Online` (R2), `Contingency` |
| `BranchId` · `CollectionPointId?` · `CashBoxId?` · `CashSessionId?` · `CashierUserId?` | | caja; nulos en banco y pagaduría |
| `OwnBankAccountPublicId?` | Guid | canal banco y pagaduría (alcance, FR-175) |
| `PayerPersonId` · `DeliveredByPersonId?` | FK | FR-040, FR-191 |
| `ValueDate` · `ProposedAccountingDate` · `RegisteredAt` | | FR-008 |
| `TotalApplied` · `TotalMeans` · `ChangeGiven` | Monto | total de medios = aplicado + vueltas (FR-041) |
| `Status` | `ReceiptState` | `Valid`, `VoidPendingApproval`, `Voided` (§19.4) [columna mutable] |
| `SourceOfFundsDeclarationId?` | FK | FR-094 |
| `IsContingency` · `ContingencyBookId?` · `ContingencyNumber?` · `ContingencyImageAttachmentPublicId?` | | FR-186 |
| `BackdatedApprovalRequestPublicId?` · `BackdatedReason?` | | FR-192 |
| `PayerIsThirdParty` | bool | FR-177: el documento enmascara |
| `InputsHash` | text | huella (FR-172) |
| `OperationKey` | Guid | UK con la operación |
| `BankStatementLinePublicId?` · `PaymasterRemittanceId?` | | canal banco y pagaduría |
| `VerificationCode` | text | FR-156 |

Índices: UK `(DocumentTypeId, Number)`; `(BranchId, ValueDate)`; `(PayerPersonId, RegisteredAt)`; `(CashSessionId)`.
`Status` es la única columna que cambia (por la anulación), con `RowVersion`.

### 3.4 `RCD_ReceiptMeans` (H) — medios del recibo

`ReceiptId`, `PaymentMeansId`, `MeansClass` (`ClaseDeMedioDeRecaudo`), `Amount`, `Reference?`, `CardTerminalId?`,
`TerminalBatch?`, `ApprovalCode?`, `ReceivedCheckId?` (cheque), `VoucherNumber?` (bono de número único, UK filtrado
`(PaymentMeansId, VoucherNumber)`), `Status` (`Active`, `WrittenOff` por cheque devuelto o reverso sin dinero)
[mutable]. Nunca guarda el número de tarjeta (`LosPagosNoGuardanElNumeroDeTarjeta`).

### 3.5 `RCD_ReceiptApplications` (H) — aplicaciones

| Columna | Tipo | Nota |
|---|---|---|
| `ReceiptId` · `Ordinal` | | orden de la pantalla |
| `Target` | `ApplicationTarget` | `Loan`, `Account`, `MiscConcept`, `CommercialReceivable` (R1), `PendingApplication` (R1) |
| `ProductPublicId` · `ProductNumber` | | crédito o cuenta (dato, sin FK) |
| `BeneficiaryPersonId` | FK | |
| `LoanModality?` · `AccountModality?` · `PrepaymentChoice?` · `InstallmentNumber?` · `InstallmentPublicIdsJson?` | | FR-038, FR-039, FR-190 |
| `AdvanceNoticeAccepted` | bool | anticipado que no abona a capital (FR-190) |
| `MiscConceptId?` · `CrossDocumentReference?` · `InvoicePublicId?` | | FR-050, FR-179 |
| `Amount` | Monto | aplicado |
| `ModuleVoucherPublicId?` · `ModuleVoucherNumber?` | | comprobante que devolvió el módulo (la suma de sus porciones) |
| `FinalityUntil?` | date | firmeza que informó Cartera (FR-048) |
| `Status` | `ApplicationState` | `Applied`, `ReversedByModule`, `Voided` [mutable] |

### 3.6 `RCD_ReceiptApplicationPortions` (H) — porciones (FR-188)

`ReceiptApplicationId`, `ReceiptMeansId`, `MeansClass`, `Amount`, `IdempotencyKey` (UK, R-09), `ModuleOperationPublicId`,
`ModuleOperationNumber`, `Status` (`Applied`, `Reversed`) [mutable], `ReversalKey?`. Las porciones son los ítems que
viajan a los contratos.

### 3.7 `RCD_SourceOfFundsDeclarations` (H) (FR-094)

`PersonId` (quien declara), `ReceiptOperationPublicId`, `CashAmount`, `Origin` (texto), `SupportAttachmentPublicId`,
`DeclaredAt`, `ThresholdVersionPublicId` (la versión del parámetro que la exigió), `RequiredBy` (`ReceiptsThreshold`,
`ModuleSimulation`).

---

## 4. Recaudo · anulación, reversos, saldos a favor y cheques recibidos (R0)

### 4.1 `RCD_ReceiptVoidings` (H) (FR-057, FR-058, FR-194)

`Number`, `ReceiptId` (UK), `Reason`, `RequestedBy`, `RequestedAt`, `ApprovalRequestPublicId`, `ApprovedBy?`, `Status`
(`PendingApproval` → `Applied` | `Rejected`) [mutable hasta `Applied`], `InSameSession` (bool), `OmittedApplicationsJson`
(las ya reversadas por el módulo), `PayerCreditBalanceId?` (si la sesión ya cerró). Al rechazarse, el recibo vuelve a
`Valid`.

### 4.2 `RCD_ModuleReversals` (H) — aplicación reversada por el módulo (FR-059, FR-195)

`InboundMessageId` (el aviso), `ReceiptApplicationId`, `PortionId?`, `Module` (`CAR`), `ModuleReversalPublicId`,
`ModuleReversalNumber`, `Amount`, `Cause` (`MoneyReceived`, `MoneyNotReceived`, `PendingConfirmation`),
`ConfirmedBy?`, `PayerCreditBalanceId?`. `PendingConfirmation` cuando el aviso no trae la causa: una persona la confirma.

### 4.3 `RCD_PayerCreditBalances` · `RCD_PayerCreditBalanceMovements` (H) — saldos a favor de pagadores (FR-054)

- `RCD_PayerCreditBalances`: `PersonId`, `Kind` (`Payer`, `Paymaster`), `Origin` (`NonInteractiveSurplus`,
  `VoidAfterSessionClose`, `ModuleReversal`, `PaymasterSurplus`, `PaymasterUnapplied`, `RejectedLine`), `OriginDocumentPublicId`,
  `Amount`, `Balance` [mutable], `Status` (`Open`, `Resolved`), `OwnBankAccountPublicId?` (alcance), `OpenedOn`.
- Movimientos: `Kind` (`Recognized`, `RefundedByOrder`, `RefundedByCash` (R1), `AppliedByReceipt`), `Amount`,
  `DocumentPublicId` (orden de pago, egreso o recibo). Antigüedad y alerta `Recaudo.SaldoAFavorAntiguo`.

### 4.4 `RCD_CashierReceivables` · `RCD_CashSurpluses` (FR-183)

- `RCD_CashierReceivables`: `CashierPersonId`, `CashCountLineId`, `Amount`, `Balance`, `PayrollDeductionAuthorizationAttachmentPublicId?`
  (obligatoria para descontar por Nómina), `Status` (`Open`, `Collected`, `WrittenOff`).
- `RCD_CashSurpluses`: `CashCountLineId`, `Amount`, `ToIncomeFrom` (fecha = conteo + `Caja.Sobrantes.DiasAntesDeIngreso`),
  `Status` (`Liability`, `MovedToIncome` con aprobación, `Returned`).

### 4.5 `RCD_ReceivedChecks` · `RCD_ReceivedCheckEvents` (H) (FR-083 a FR-087)

- `RCD_ReceivedChecks`: `DraweeBankId` (FK `COR_Banks`), `CheckNumber`, `AccountNumber`, `DrawerPersonId`, `IssueDate`,
  `Amount`, `IsThirdParty`, `SamePlace` (bool), `PresentationDeadline` (fecha, aviso), `Status` (`Received`,
  `InCustody`, `Deposited`, `InClearing`, `Paid`, `Returned`) [mutable], `ReturnCauseCode?`, `RemittancePublicId?`.
  UK `(DraweeBankId, AccountNumber, CheckNumber)` sin filtro.
- Eventos: cada transición con quién, cuándo y soporte.
- `RCD_CheckReturnCauses` (catálogo sembrado, código del banco y descripción, «pendiente de validar»).
- La **devolución** (`ReturnReceivedCheckCommand`) reversa por los contratos las porciones pagadas con el cheque, da de
  baja el medio (`RCD_ReceiptMeans.Status = WrittenOff`), avisa y alerta; si el dinero ya se resolvió (devolución
  entregada), crea `RCD_PayerReceivables` (cuenta por cobrar al pagador, FR-060): `PersonId`, `Amount`, `Balance`,
  `OriginReceivedCheckId`, `Status`.

---

## 5. Recaudo · bancos: identificación y partidas por identificar (R0)

El extracto es de Tesorería (§13.1, R-06). Recaudo guarda **su** identificación por el `PublicId` de la línea.

### 5.1 `RCD_BankItemIdentifications` (H)

`BankStatementLinePublicId` (UK con `Sequence`), `Sequence` (identificaciones parciales de una misma línea),
`OwnBankAccountPublicId`, `MovementDate`, `Amount`, `Outcome` (`Receipt`, `UnidentifiedItem`, `Classified`,
`ReturnedDisbursement`, `PaymasterRemittance`, `PossibleDuplicateDismissed`), `ReceiptId?`, `UnidentifiedItemId?`,
`MiscConceptId?`, `PaymentOrderPublicId?` (giro devuelto, FR-199), `PaymasterRemittanceId?`, `IdentifiedBy`, `RuleId?`
(R1). La suma de las identificaciones de una línea nunca supera su valor (`Receipts.BankItem.OverIdentified`).

### 5.2 `RCD_UnidentifiedItems` — partidas por identificar (FR-070 a FR-073)

| Columna | Tipo | Nota |
|---|---|---|
| `Number` | | |
| `BankStatementLinePublicId` · `OwnBankAccountPublicId` · `BankId` | | origen |
| `MovementDate` · `Amount` · `Reference?` · `DepositorName?` · `DepositorTaxId?` | | |
| `Balance` | Monto | lo que falta identificar [mutable] |
| `Status` | `UnidentifiedItemState` | `Open`, `PartiallyIdentified`, `Identified`, `Classified`, `ReturnedToDepositor` (§19.6) |
| `AgingStep` | smallint | último escalón alertado (30, 60, 90 por defecto) |
| `OriginKind` | | `Statement`, `Reconciliation` (FR-216), `OpeningLoad` |

`RCD_UnidentifiedItemResolutions` (H): `UnidentifiedItemId`, `Kind` (`IdentifiedToReceipt`, `ClassifiedToConcept`,
`RefundedByOrder`, `MovedToIncome` (decisión registrada, FR-073)), `Amount`, `DocumentPublicId`, `ApprovalRequestPublicId?`,
`By`.

### 5.3 `RCD_ProcessingRuns` · `RCD_ProcessingRunLines` (FR-171, R-17)

Corrida no interactiva (aplicación de un extracto, de un giro de pagaduría, de un archivo de convenio en R2):
`Kind`, `SourcePublicId`, `StartedBy`, `StartedAt`, `FinishedAt`, `Lines`, `Applied`, `Rejected`, `Total`, `InputsHash`.
Líneas: `LineRef`, `IdempotencyKey`, `Outcome` (`Applied`, `AlreadyApplied`, `Rejected`, `Skipped`), `Code`, `Message`,
`ReceiptId?`, `ResolutionPublicId?`. Reprocesar una línea repite su clave.

**Auditoría de la corrida** (Principio X, FR-014, TR37): al terminar cada tanda y al cerrar la corrida se escribe por
`COR_AuditOutbox`, en la misma transacción que la última línea de la tanda, el evento `Receipts.ProcessingRun.Summary`
con `runPublicId`, conteos, `InputsHash` y la lista `receipts[] { publicId, sha256 }` de cada recibo aplicado en esa tanda
(`sha256` = `HuellaDeRecibo` del recibo tal como quedó), más un evento por línea rechazada. La lista cubre exactamente los
recibos de las líneas `Applied` (los de reproceso, en la tanda del reproceso); cada huella se verifica recalculándola
desde el recibo.

---

## 6. Recaudo · pagadurías y libranza (R0)

### 6.1 `RCD_InboundMessages` — recibo de mensajes (FR-151)

`MessagePublicId` (UK), `Type`, `Version`, `ReceivedAt`, `Outcome` (`Processed`, `Rejected`), `Code?`, `Detail?`. Molde
de `LND_InboundMessages`.

### 6.2 `RCD_PayrollDeductionFiles` · `RCD_PayrollDeductionFileLines` (H) (FR-075)

- Archivo recibido de `DescuentosPorPagaduria` v1 o v2: `FileMessagePublicId` (UK), `MessageVersion`, `PaymasterPersonId`,
  `AgreementCode`, `AgreementVersion`, `FileGroupCode?`, `PeriodYear`, `PeriodMonth`, `SubPeriod?`, `FileVersion`,
  `FilePublicId`, `FileAttachmentPublicId`, `FileSha256`, `FormatCode`, `SendBy`, `LineCount`, `Amount`, `PastDueAmount`,
  `SupersedesFileMessagePublicId?`, `Status` (`Received`, `Sent`, `Responded`, `Superseded`) [mutable].
- Líneas estructuradas: `DeductionFileLinePublicId` (UK), `Kind` (`Loan`, `AccountInstallment`), `ProductPublicId`,
  `ProductNumber`, `InstallmentPublicId?`, `InstallmentNumber?`, `PersonId`, `PayerEmployeeId`, `PaymasterConceptCode?`,
  `DueDate`, `Amount`, `PastDueAmount`, `DeductionOutcome?` [EscrituraUnica], `DeductedAmount?` [EscrituraUnica],
  `DeductionDate?` [EscrituraUnica], `RemittedAmount` [mutable, suma de lo aplicado por giros].

### 6.3 `RCD_PayrollFileSendings` (H) (FR-076)

`PayrollDeductionFileId` (UK por versión), `Channel` (`Email`, `Portal`, `Physical`, `Other`), `SentAt`, `Reference?`,
`SentBy`, `EmittedMessagePublicId` (`EnvioDeDescuentosConfirmado`). Repetir no cambia nada (US7-4).

### 6.4 `RCD_PaymasterResponses` · `RCD_PaymasterResponseLines` (FR-077, FR-201)

- `PayrollDeductionFileId`, `Version` (una respuesta rechazada por Cartera se corrige con otra), `Source` (`Format`,
  `GenericTemplate`, `Manual`), `ResponseAttachmentPublicId`, `ResponseSha256`, `LoadedBy`, `Status` (`Previewed`,
  `Emitted`, `RejectedByLending`, `AcceptedByLending`) [mutable], `EmittedMessagePublicId?`, `RejectionCode?`,
  `RejectionDataJson?`.
- Líneas: por línea de Cartera (v1) o por empleado y código (v2): `DeductionFileLinePublicId?`, `PayerEmployeeId?`,
  `PaymasterConceptCode?`, `Expected`, `Deducted`, `DeductionDate?`, `Outcome`, `PaymasterResultCode?`, `Reason?`,
  `Unmatched` (bool, «no aplicado» por falta de equivalencia, US7-1).

### 6.5 `RCD_PaymasterRemittances` · `RCD_PaymasterRemittanceLines` (FR-078 a FR-080)

- Giro: `Number`, `PaymasterPersonId`, `Source` (`BankItem`, `CashReceipt`, `CheckReceived`), `BankStatementLinePublicId?`,
  `SourceReceiptId?`, `Amount`, `RemittedOn`, `PeriodsJson` (archivos que cubre, en orden), `Status` (`Registered`,
  `Applying`, `Applied`, `PartiallyApplied`) [mutable], `AllocationOrder` (`AgreementPriority`, `AnalystOrder`),
  `SurplusCreditBalanceId?`.
- Líneas cubiertas: `PaymasterRemittanceId`, `DeductionFileLinePublicId`, `Amount`, `IdempotencyKey`
  (`REC:{giro:N}:{línea:N}`), `Outcome` (`Pending`, `Applied`, `Rejected`), `ReceiptId?`, `RejectionCode?`,
  `CreditBalanceId?` (lo rechazado queda como saldo de la pagaduría, US7-6).

---

## 7. Recaudo · control de efectivo y SARLAFT (R0; reporte en R1)

### 7.1 `RCD_CashTransactions` (H, volumen) — registro de transacciones en efectivo (FR-097, FR-204)

`DocumentPublicId`, `DocumentKind` (`Receipt`, `CashDisbursement`, `Voiding`), `Direction` (`In`, `Out`), `PersonId`
(pagador o beneficiario), `DeliveredByPersonId?`, `ReceivedByPersonId?`, `CashAmount`, `OtherMeansAmount`, `Channel`,
`BranchId`, `OccurredOn`, `Month` (aaaamm), `DeclarationId?`, `AlertsJson`. Índice `(PersonId, Month)`. En R0 sólo
Recaudo; desde R1 el acumulado suma lo del punto de venta por la consulta de E-03 sin copiar sus filas. Una anulación
escribe una fila `Voiding` con signo contrario (el registro nunca se edita).

### 7.2 `RCD_ComplianceAlertsLog` — señales evaluadas

`PersonId`, `Signal` (`Accumulated`, `Fragmentation`, `FrequentThirdPartyPayer`, `RepeatedUnidentifiedDepositor`,
`BindingListMatch`, `NonBindingListMatch`), `WindowFrom`, `WindowTo`, `Count`, `Total`, `ParameterVersionPublicId`,
`AlertPublicId`. Deja el rastro de qué versión del parámetro levantó la señal (FR-097).

### 7.3 `RCD_CashTransactionReports` · `RCD_CashTransactionReportLines` (R1, `RecaudoCompleto`) (FR-096)

Reporte por período: `PeriodYear`, `PeriodMonth`, `Version`, `Scope` (`CashOnly`, `All`), `ApplicationState`
(`Active`, `Suspended`, sellado), `LayoutCode`, `LayoutVersion`, `LayoutVerified` (sellado), `Status` (`Validated` sin
archivo con bloqueantes, `Generated`, `Superseded`, `Uploaded`), `FileAttachmentPublicId?`, `Sha256?`, `UploadReference?`,
`UploadedAt?`, `IssuesJson`, `IsAbsenceReport`. Líneas individuales y múltiples con persona, montos y documentos. Molde
de `LND_RegulatoryFiles`.

---

## 8. Recaudo · R1 y R2

### 8.1 R1 (`RecaudoCompleto`)

| Tabla | Para qué | FR |
|---|---|---|
| `RCD_CashDisbursements` (H) | egreso de caja: `Number`, `Kind` (`SavingsWithdrawal`, `LendingCreditBalanceRefund`, `PayerCreditBalanceRefund`, `MiscExpense`, `AccountClosingDelivery`), `CashSessionId`, `BeneficiaryPersonId`, `ReceivedByPersonId?`, `ModuleAuthorizationPublicId?` (comprobante de Captaciones o Cartera), `Amount`, `GmfAmount?` (de Captaciones o el suyo), `SignatureAttachmentPublicId?`, `IdempotencyKey`, `Status` (`Delivered`, `Voided`) | FR-051 a FR-056, FR-061 |
| `RCD_CashDisbursementVoidings` (H) | anulación de egreso, sólo si el dinero vuelve | FR-061 |
| `RCD_PendingApplications` · `RCD_PendingApplicationResolutions` | recaudo pendiente de aplicar (persona de una carga aplicada sin activar), por persona y producto, aplicado en la activación del módulo | FR-046, D14 |
| `RCD_ReportedDeposits` | consignación reportada en ventanilla: `Status` (`ToVerify`, `Verified`, `Expired`), soporte, línea del extracto que la confirma | FR-069, D11 |
| `RCD_IdentificationRules` | reglas de identificación: criterio (`PaymentReference`, `DepositorTaxId`, `ReportedDeposit`, `ExactDueAmount`), prioridad, aplicar al cargar (bool), modalidad del canal | FR-067, D15 |
| `RCD_CommercialReceivableApplications` | detalle de la aplicación `CommercialReceivable`: factura, saldo consultado, tercero | FR-049 |
| `RCD_PaymasterCalendar` | fecha de pago de la nómina informada por la pagaduría y fecha legal calculada de giro | FR-082 |

### 8.2 R2 (`RecaudoCanales`)

| Tabla | Para qué | FR |
|---|---|---|
| `RCD_CollectionAgreements` · `RCD_CollectionAgreementVersions` | convenio de recaudo (banco o corresponsal): cuenta recaudadora, estructura de la referencia (longitud, dígito), formato del archivo (ámbito 8), comisión informativa | FR-088, FR-092 |
| `RCD_PaymentReferences` | referencia de pago estable por producto (y persona) | FR-090 |
| `RCD_AgreementFiles` · `RCD_AgreementFileLines` | archivo diario del convenio, con su huella | FR-089, FR-197 |
| `RCD_AgreementReconciliations` | lo informado contra lo abonado, neto de comisión, IVA y retenciones (`FR-218`) | FR-093 |
| `RCD_GatewayFiles` · `RCD_GatewayFileLines` | archivos de pasarelas y PSE (ámbito 10) | FR-091 |
| `RCD_ClearingBatches` | canje completo de cheques recibidos | FR-084 |

---

## 9. Tesorería · cuentas bancarias propias y fondo de liquidez (T0, `TesoreriaNucleo`)

### 9.1 `TES_Setup` (fila única)

`Status` (`TreasuryModuleStatus`: `NotActivated`, `Active`), `ActivatedOn?`, `ResultWithThirdPerson` (bool, sellado de
`Lotes.TerceraPersonaParaResultado`). Compartido en el nivel 1.

### 9.2 `TES_BankAccounts` — cuenta bancaria propia (FR-102)

| Columna | Tipo | Nota |
|---|---|---|
| `BankId` | FK `COR_Banks` | con `TransferCode` (FR-102) |
| `AccountNumber` · `AccountType` (`Savings`, `Checking`) | | UK `(BankId, AccountNumber)` sin filtro |
| `BranchId` | FK | oficina |
| `Purposes` | `BankAccountPurpose` (flags) | `Collection`, `Payments`, `Payroll`, `Agreement`, `LiquidityFund`; `LiquidityFund` es exclusivo (FR-207, `Treasury.BankAccount.LiquidityFundExclusive`) |
| `IsGmfExempt` | bool | FR-102 |
| `BankThirdPartyPersonId` | FK `COR_People` | el banco como tercero (exógena, FR-222) |
| `Status` · `ValidFrom` · `ValidTo` | | |
| `LastStatementBalance?` · `LastStatementDate?` | | proyección del último extracto (FR-103) |

El vínculo con la cuenta contable **no** está aquí: `ACC_OwnBankAccountLinks` (§17.3). La pantalla lo muestra por
`IContabilidadParaTesoreria`.

### 9.3 `TES_UserBankAccountScopes` (FR-106)

`UserId`, `BankAccountId`. Alcance de Tesorería y de los canales sin caja de Recaudo (FR-175).

### 9.4 `TES_LiquidityFundUses` (FR-207)

`Number`, `BankAccountId`, `Cause` (`DepositorAttention`, `DepositReduction`), `Amount`, `Reason`, `SupportAttachmentPublicId`,
`ApprovalRequestPublicId`, `SupervisorNoticeAttachmentPublicId?`, `Status` (`PendingApproval`, `Approved`, `Used`,
`Rejected`, `Expired`), `UsedByDocumentPublicId?`. Un lote, una remesa o un traslado desde una cuenta del fondo exige un
uso `Approved` por el monto (`Treasury.LiquidityFund.UseRequired`, SC-028). Los abonos no se restringen.

---

## 10. Tesorería · órdenes, lotes, archivos y resultados (T0)

### 10.1 `TES_InboundMessages`

Recibo único por `MessagePublicId`, como §6.1.

### 10.2 `TES_PaymentOrders` — bandeja única de órdenes de pago (FR-107)

| Columna | Tipo | Nota |
|---|---|---|
| `Number` | | número de Tesorería |
| `Origin` | `PaymentOrderOrigin` | `LendingDisbursement`, `DepositsPayout` (Captaciones, v2), `Receipts` (devolución de Recaudo), `Manual` (T1), `SupplierInvoice` (T2), `AccountingPayable` (T2) |
| `OriginOrderPublicId` · `OriginOrderNumber` · `OriginMessagePublicId?` · `OriginMessageVersion?` | | UK `(Origin, OriginOrderPublicId)`: una orden repetida no se duplica (US10-6) |
| `OrderKind` | texto ≤ 40 | `OrderKind` de la v2 (`LoanDisbursement`, `DepositWithdrawal`, `MembershipRefund`…) o el de Recaudo (`PayerCreditBalanceRefund`, `LendingCreditBalanceRefund`, `UnidentifiedItemRefund`) |
| `Form` | `PaymentForm` | `Transfer`, `Check`, `ThirdPartyPayment` |
| `BeneficiaryPersonId` | FK | siempre una persona |
| `DestinationBankId?` · `DestinationAccountNumber?` · `DestinationAccountType?` | | sellados del mensaje |
| `PersonBankAccountId?` | FK `COR_PersonBankAccounts` | la cuenta registrada que casa (FR-109, FR-210) |
| `GrossAmount` · `NetAmount` | Monto | se gira exactamente el neto (FR-116) |
| `PayDate` · `Concept` | | |
| `PaidByLendingFile` | bool | `PaidByFile` de Cartera: no entra a lotes (FR-108) |
| `LendingDisbursementFileId?` | FK | el archivo de Cartera que la paga (E-01 c) |
| `ReplacesOrderId?` | FK | cadena de regiros (FR-121) |
| `GmfExempt` · `GmfExemptionReason?` · `GmfEstimate?` | | FR-206 |
| `Status` | `PaymentOrderState` | §19.8 [mutable] |
| `PendingReasonsJson` | text | motivos de pendiente (sin cuenta, banco sin código, lista vinculante, cuenta en espera) |
| `AccountChangedRecently` | bool | marca FR-210 [mutable] |
| `VoidRequested` | bool | llegó la anulación del origen [mutable] |

Índices: `(Status, PayDate)`, `(BeneficiaryPersonId)`, único filtrado `(Id)` en `TES_PaymentBatchItems` vigentes (§10.4).

### 10.3 `TES_PaymentBatches` — lote de pago (FR-110, FR-111)

`Number`, `BankAccountId` (origen; nunca del fondo de liquidez sin uso aprobado), `Form` (`File`, `BankPortal`, `Checks`),
`BankFileFormatId?` (vigente al preparar, sellado), `PreparedBy`, `PreparedAt`, `OrderCount`, `Total`, `GmfEstimate`,
`ApprovalRequestPublicId?`, `Status` (enum `PaymentBatchState`, §20: `Draft`, `PendingApproval`, `Approved`,
`FileGenerated`, `Sent`, `PartiallyConfirmed`, `Confirmed`, `RejectedByBank`, `Voided`) [mutable], `InputsHash`.

### 10.4 `TES_PaymentBatchItems`

`PaymentBatchId`, `PaymentOrderId`, `Amount`, `Flags` (`RecentAccountChange`, `SharedDestination`, `InternalUserAccount`,
`ImportedAccountWithoutCertificate`), `ExplicitlyReviewedBy?` (obligatorio con `RecentAccountChange`, US26-1),
`Status` (`Included`, `Removed`, `Paid`, `Rejected`) [mutable]. Único filtrado: una orden en a lo sumo un ítem cuyo lote no
esté `Voided` ni `RejectedByBank` y cuyo ítem no esté `Removed` (FR-116, SC-013).

### 10.5 `TES_PaymentFiles` (H) · `TES_PaymentFileLines` (H) (FR-112, FR-212)

- `PaymentBatchId`, `Version`, `BankFileFormatId`, `FileAttachmentPublicId`, `Sha256`, `SizeBytes`, `LineCount`, `Total`,
  `GeneratedBy`, `GeneratedAt`, `DownloadedBy?` [EscrituraUnica], `DownloadedAt?`, `SentAt?` [EscrituraUnica],
  `UploadedAtBankBy?` (persona que lo cargó en el banco), `AuthorizedAtBankBy?` (persona que lo autorizó allí),
  `VoidedAt?` [EscrituraUnica], `VoidReason?`, `RejectedByBankAt?` [EscrituraUnica].
- Líneas: `PaymentBatchItemId`, `LineNumber`, `BeneficiaryTaxId`, `BankCode`, `AccountNumber`, `Amount`, `LineSha256`
  (lo que se coteja contra la respuesta).

### 10.6 `TES_PaymentResponses` · `TES_PaymentResponseLines` (FR-118, FR-212)

- `PaymentFileId`, `Source` (`BankFormat`, `StatementMatch`, `Manual`), `ResponseAttachmentPublicId?`, `Sha256?`,
  `LoadedBy`, `LoadedAt`.
- Líneas: `PaymentFileLineId?`, `Outcome` (`Paid`, `Rejected`), `BankCause?`, `BankDate`, `Amount`, `AccountNumber`,
  `Discrepancy` (bool: valor o cuenta distinta del archivo generado; la línea no se informa hasta que una persona la
  revise, US11-7), `ReviewedBy?`, `DisbursementResultId?`.

### 10.7 `TES_DisbursementResults` (H) — resultado del giro (FR-118, FR-215)

`PaymentOrderId`, `Outcome` (`Disbursed`, `Rejected`, `CheckNotCashed`, `Voided`), `OccurredOn`, `Amount`,
`TreasuryReference`, `CheckNumber?`, `Reason?`, `RecordedBy` (siempre una persona), `SupportAttachmentPublicId?`
(obligatorio en el registro manual), `PaymentResponseLineId?`, `StatementLinePublicId?`, `EmittedMessagePublicId`.
UK `(PaymentOrderId, Outcome)`: el mismo resultado dos veces no cambia nada (US11-5). Las transiciones las valida
`TransicionesDelResultado` (§19.9).

### 10.8 `TES_LendingDisbursementFiles` · `TES_LendingDisbursementFileOrders` (E-01 c)

Archivo de desembolsos de Cartera informado por `ArchivoDeDesembolsosGenerado` / `…Anulado`: `FileMessagePublicId`,
`FilePublicId`, `FileSha256`, `BankAccountPublicId?`, `Total`, `Status` (`Generated`, `Voided`); órdenes que cubre. Sólo
para registrar resultados y conciliar (FR-108, D16).

---

## 11. Tesorería · chequeras, cheques girados y remesas (T0)

### 11.1 `TES_Checkbooks` (FR-134, FR-219)

`BankAccountId`, `FromNumber`, `ToNumber`, `NextNumber` [mutable bajo cerrojo], `Status` (`Active`, `Exhausted`,
`Voided`), `CustodianUserId`, `CustodyAttachmentPublicId?` (FR-182). Una chequera agotada pide la siguiente
(`Treasury.Checkbook.Exhausted`, US19-1).

### 11.2 `TES_IssuedChecks` · `TES_IssuedCheckEvents` (H) (FR-135, FR-136)

- `CheckbookId`, `CheckNumber` (UK por chequera), `PaymentOrderId?`, `PaymentBatchItemId?`, `BeneficiaryPersonId`,
  `Amount`, `IssuedOn`, `Crossed` (bool, por defecto sí), `FirstBeneficiaryOnly` (bool, por defecto sí; ligado a la
  exención de FR-206), `Status` (`Issued`, `Delivered`, `Cashed`, `Voided`, `NotCashed`, `SpoiledBlank`) [mutable],
  `DeliveryReceiptAttachmentPublicId?` (constancia, FR-113), `DeliveredOn?`, `CashedOn?`, `VoidReason?`,
  `IsOpeningLoad` (bool: cheque en tránsito de la puesta en marcha).
- Eventos por transición con quién y soporte. Un hueco de numeración sólo se explica por un `SpoiledBlank` o `Voided`
  (FR-136).

### 11.3 `TES_CashRemittances` (FR-105, R-15)

`Number`, `Direction` (`VaultToBank`, `BankToVault`), `BranchId`, `VaultPublicId` (de Recaudo), `BankAccountId`, `Amount`,
`BagSeal`, `MinutesAttachmentPublicId`, `DispatchedAt`, `Status` (`InTransit`, `Confirmed`, `Expired`, `Voided`)
[mutable], `ConfirmedByStatementLinePublicId?`, `ConfirmedBy?`, `ConfirmedAt?`. Sin confirmar en
`Remesas.DiasParaConfirmar`: alerta `Tesoreria.RemesaSinConfirmar`.

---

## 12. Tesorería · extractos y conciliación mínima (T0)

El extracto (§12.1) va en `TesoreriaNucleo`; la conciliación (§12.2 a §12.4) en el par nuevo `TesoreriaConciliacionMinima`,
posterior al congelamiento del 10/11 (§25). Es la **única** conciliación bancaria de la cooperativa: reemplaza a la de
Contabilidad (§17.5, TR41).

### 12.1 `TES_BankStatements` · `TES_BankStatementLines` (H) (FR-065, FR-196, FR-197, R-06)

- `TES_BankStatements`: `BankAccountId`, `Source` (`BankFormat`, `GenericTemplate`, `Manual`), `BankFileFormatId?`,
  `FileAttachmentPublicId?`, `FileSha256?` (UK filtrado: un archivo ya cargado se reconoce, FR-197), `From`, `To`,
  `OpeningBalance?`, `ClosingBalance?`, `LoadedBy`, `LoadedAt`, `Lines`, `Omitted` (cuántas omitió por huella, US6-1),
  `Illegible` (líneas no cargadas).
- `TES_BankStatementLines` (`AuditableEntityLong`): `BankStatementId`, `BankAccountId`, `MovementDate`, `Direction`
  (`Credit`, `Debit`), `Amount`, `Reference?`, `BankSequence?`, `DepositorTaxId?`, `DepositorName?`, `Description?`,
  `Fingerprint` (huella por formato, FR-197) con UK `(BankAccountId, Fingerprint)` cuando el formato trae identificador del
  banco; sin él, `PossibleDuplicateOfLineId?` y `DuplicateDecision` (`Pending`, `Distinct`, `Duplicate`) para que una
  persona decida; `RecaudoStatus` (`NotApplicable`, `Pending`, `Identified`, `PartiallyIdentified`) [mutable, lo actualiza
  el puerto `IMovimientosBancariosParaRecaudo`]; `ReconciliationStatus` (`Open`, `Matched`, `ManuallyMarked`) [mutable].

### 12.2 `TES_Reconciliations` (FR-216, FR-131)

`BankAccountId`, `PeriodYear`, `PeriodMonth`, `Version` (reabrir crea versión nueva sobre la cerrada), `Kind`
(`Minimal`, `Complete` en T2), `ConciliatorUserId`, `StatementBalance`, `LedgerBalance` (de E-04), `ReconcilingItemsTotal`,
`Difference`, `Status` (`Open`, `Closed`, `Reopened`, `Outdated`), `ClosedAt?`, `ReportAttachmentPublicId?`,
`ReopenReason?`, `ReopenedBy?`. `Outdated` lo pone sólo `IConciliacionBancariaParaContabilidad` cuando Contabilidad reabre
el período del mes (antes lo hacía `ReopenPeriodCommand` sobre `ACC_BankReconciliations`); se vuelve a conciliar con una
versión nueva. UK `(BankAccountId, PeriodYear, PeriodMonth, Version)`. Cerrar exige `Difference = 0`
(`Treasury.Reconciliation.NotSquared`). Segregación: el conciliador no preparó ni aprobó pagos de esa cuenta en el período
(`Treasury.Reconciliation.SegregationConflict`, FR-217).

### 12.3 `TES_ReconciliationMatches` (H)

`ReconciliationId`, `StatementLineId`, `RecordKind` (`ReceiptsBankReceipt`, `Remittance`, `TreasuryPayment`, `IssuedCheck`,
`LedgerMovement`, `LendingDisbursementFile`), `RecordPublicId`, `Amount`, `Mode` (`Automatic`, `Manual`), `Rule` (en T0:
`ValueDateReference`; en T2, la regla), `MatchedBy`. Un registro y una línea se cruzan a lo sumo una vez en una
conciliación vigente (UK filtrados, US25-1). Un cheque girado cruzado pasa a `Cashed` (US25-4); una remesa, a
`Confirmed`.

### 12.4 `TES_ReconcilingItems`

`ReconciliationId`, `Kind` (`IssuedCheckNotCashed`, `DepositInTransit`, `RemittanceInTransit`, `BankChargeNotRecorded`,
`BankCreditNotRecorded`, `UnidentifiedCredit`, `UnidentifiedDebit`), `Amount`, `Since`, `SourcePublicId?`,
`UnidentifiedItemPublicId?` (abono sin registro enviado a Recaudo, US25-2), `Note?`, `AlertPublicId?` (cargo no
identificado: embargo, débito no autorizado, FR-217).

---

## 13. Tesorería · T1 y T2

### 13.1 T1 (`TesoreriaCompleta`)

| Tabla | Para qué | FR |
|---|---|---|
| `TES_ManualOrderConcepts` | catálogo cerrado de órdenes manuales: tope, retención exigida, dos aprobadores | FR-211 |
| `TES_BankAccountSigners` · `TES_BankAccountTokens` | firmantes y tokens con combinaciones por monto y vigencia; cambios con acta | FR-208 |
| `TES_Transfers` | traslados entre cuentas propias con aprobación y tránsito hasta el extracto | FR-104 |
| `TES_PaymentVouchers` | comprobante de pago numerado por pago, soporte al beneficiario | FR-117 |
| `TES_MonthEndCheckRuns` | cierre del mes de cheques: reclasificación de emitidos no entregados y paso a cuenta por pagar de los no cobrados antiguos | FR-220 |

### 13.2 T2 (`TesoreriaAmpliada`)

| Tabla | Para qué | FR |
|---|---|---|
| `TES_BankNotes` | notas bancarias (GMF, comisión, intereses en bruto con retención, impuestos) con aprobación y su hecho | FR-130, FR-218 |
| `TES_ReconciliationRules` | reglas configurables de emparejamiento | FR-129 |
| `TES_AcquirerSettlements` · `TES_AcquirerSettlementLines` | conciliación de adquirentes con los lotes de datáfono de Recaudo y del punto de venta | FR-133 |
| `TES_CashFlowCategories` · `TES_CashFlowProjections` | flujo de caja proyectado, real contra proyectado | FR-137, FR-138 |
| `TES_LiquiditySnapshots` | posición diaria con sus componentes y los que faltaron | FR-137 |
| `TES_LegacyPendingLoads` | cheques sin cobrar y facturas por pagar migrados desde lo heredado antes del retiro | FR-161 |

---

## 14. Lo que cruza entre Recaudo y Tesorería (sin FK, R-05)

| Columna | En | Apunta a | Escrita por |
|---|---|---|---|
| `OwnBankAccountPublicId` | `RCD_Receipts`, `RCD_BankItemIdentifications`, `RCD_UnidentifiedItems`, `RCD_PayerCreditBalances` | `TES_BankAccounts` | Recaudo, con lo que devuelve `ICuentasBancariasPropias` |
| `BankStatementLinePublicId` | `RCD_BankItemIdentifications`, `RCD_UnidentifiedItems`, `RCD_Receipts`, `RCD_PaymasterRemittances` | `TES_BankStatementLines` | Recaudo; Tesorería actualiza `RecaudoStatus` por su puerto |
| `RemittancePublicId` | `RCD_VaultMovements`, `RCD_ReceivedChecks` | `TES_CashRemittances` | Recaudo, con lo que devuelve `IRemesasDeTesoreria` |
| `VaultPublicId` | `TES_CashRemittances` | `RCD_Vaults` | Tesorería, con lo que le pasa Recaudo |
| `PaymentOrderPublicId` | `RCD_PayerCreditBalanceMovements`, `RCD_UnidentifiedItemResolutions`, `RCD_BankItemIdentifications` (giro devuelto) | `TES_PaymentOrders` | Recaudo, con lo que devuelve `IOrdenesDePagoDeTesoreria` |
| `UnidentifiedItemPublicId` | `TES_ReconcilingItems` | `RCD_UnidentifiedItems` | Tesorería, con lo que devuelve `IPartidasDesdeConciliacion` |
| `RecordPublicId` (`ReceiptsBankReceipt`) | `TES_ReconciliationMatches` | `RCD_Receipts` | Tesorería, con lo que devuelve `IRecaudoParaConciliacion` |

`RecaudoYTesoreriaSeHablanPorSusPuertos` fija que ningún tipo de `Receipts` nombra un `DbSet` de `Treasury` ni al revés.

---

## 15. Activación, cuadre y puesta en marcha (R0, T0)

### 15.1 `RCD_Activations` · `TES_Activations`

`ActivatedOn`, `CutoffDate` (la activación rige el día siguiente al corte, FR-159), `ActivatedBy`, `RequirementsJson`
(lo verificado), `OpeningSquareId?`, `GoLiveChecklistPublicId?`, `IsMinimal`. Una sola vigente por módulo. Nacen en
`RecaudoNucleo` y `TesoreriaNucleo` con la **activación mínima** del bloque 2 (TR42: `IsMinimal = true`,
`OpeningSquareId` nulo, sólo los requisitos básicos); la FK de `OpeningSquareId` llega con el par
`RecaudoYTesoreriaPuestaEnMarcha`. La activación completa (fase 19) exige el cuadre y deja `IsMinimal = false`; en
producción no hay activación mínima.

### 15.2 `RCD_OpeningSquares` · `RCD_OpeningSquareLines` · `TES_OpeningSquares` · `TES_OpeningSquareLines` (FR-158, FR-228)

Con `RCD_ReferenceFigures` y `TES_ReferenceFigures`, en el par nuevo `RecaudoYTesoreriaPuestaEnMarcha` (§25).

- Cabecera: `Kind` (`Definitive`, `Provisional`), `Status` (`Pending`, `Squared`, `AcceptedWithMinutes`,
  `ProvisionalApproved`, `Superseded`) [mutable], `ApprovedBy?`, `Reason?`, `DeadlineOn?` (provisional),
  `MinutesAttachmentPublicId?`, `DefinitiveSquareId?` (el provisional queda enlazado a su definitivo).
- Líneas por concepto y dimensión: Recaudo — efectivo por bóveda y caja, partidas por identificar, cheques recibidos en
  canje, saldos a favor; Tesorería — saldo por cuenta bancaria, cheques girados en tránsito, remesas y consignaciones en
  tránsito. `Loaded`, `Ledger` (de `SaldoContableAsync`), `Difference`.

### 15.3 Cargas de puesta en marcha (FR-157)

Plantillas por `EjecutorDeImportacion` (todo o nada, revisión previa, error por fila y columna); escriben en las tablas
reales con `IsOpeningLoad = true` (cajas, bóvedas, cheques en tránsito, partidas con `OriginKind = OpeningLoad`, cheques
recibidos en canje, remesas en tránsito) y **sin hechos** (FR-158). Una partida sin fecha se rechaza; un cheque en
tránsito de una chequera no registrada se rechaza (Edge Cases). Cifras de referencia por `RCD_ReferenceFigures` /
`TES_ReferenceFigures` (`Concept`, `Dimension`, `Amount`, `LoadedBy`) para cotejar totales y el paralelo (FR-160,
FR-226).

---

## 16. Plataforma (`COR_`, E-06; `PlataformaParaRecaudoYTesoreria`, R0/T0)

### 16.1 `COR_PersonBankAccounts` (FR-210, R-21)

| Columna | Tipo | Nota |
|---|---|---|
| `PersonId` | FK | |
| `BankId` · `AccountType` · `AccountNumber` · `HolderName` · `HolderTaxId` | | |
| `CertificateAttachmentPublicId?` | Guid | certificación bancaria (titularidad) |
| `Origin` | `PersonBankAccountOrigin` | `Registered`, `Imported` (copia de `Person.SupplierBank*`) |
| `Status` | `PersonBankAccountState` | `PendingApproval`, `Active`, `Rejected`, `Retired` [mutable] |
| `RequestedBy` · `ApprovedBy?` · `ApprovedAt?` | | quien aprueba es otra persona |
| `PayableFrom?` | date | aprobación + `CuentasDestino.DiasDeEspera` |
| `ReplacesId?` | FK | cada cambio es una fila nueva; la anterior queda `Retired` |

Índices: `(BankId, AccountNumber)` para detectar la cuenta compartida entre beneficiarios o con un usuario interno
(alerta `Plataforma.CuentaDestinoCompartida`). Lectura y escritura sensibles auditadas.

### 16.2 `COR_GoLiveChecklists` · `COR_GoLiveChecklistItems` (FR-227, R-28)

- Lista: `Name`, `Environment`, `TargetDate`, `Status` (`Open`, `Go`, `NoGo`, `PartialGo`), `DecidedBy?`, `DecisionAt?`.
- Ítems: `Module` (`REC`, `TES`, `CAR`, `Plataforma`), `Code`, `Kind` (`Automatic`, `Manual`), `Status` (`Green`,
  `Red`, `NotApplicable`), `Evidence`, `CheckedAt`, `OverrideApprovedBy?`, `OverrideReason?` (seguir con un rojo exige la
  aprobación registrada del dueño), `ScenarioAttachmentPublicId?` (escenarios de no-go parcial y plan de reversa).

### 16.3 Columnas y valores nuevos en lo existente

| Dónde | Qué | Por qué |
|---|---|---|
| `BankFileScope` | `SupplierPayments = 3` se **renombra** `TreasuryPayments = 3`; nuevos `BankStatement = 7`, `CollectionAgreementFile = 8` (R2), `TreasuryPaymentResponse = 9`, `PaymentGatewayFile = 10` (R2), `AcquirerSettlement = 11` (T2) | FR-169, R-20 |
| `COR_BankFileFormats` | semillas genéricas `EXTRACTO-GENERICO` (ámbito 7), `PAGOS-GENERICO` (ámbito 3, CSV), `RESPUESTA-PAGOS-GENERICO` (ámbito 9), `RESPUESTA-PAGADURIA-GENERICA` (ámbito 6) | FR-196, FR-201, FR-213 |
| `ScreeningContext` (listas; `Domain/Enums/Lending/OriginationEnums.cs`) | `Collection = 6`, `Disbursement = 7` (1 a 5 ya están tomados: `Application`, `Sale`, `CreditLimit`, `ListRescan`, `AccountOpening`) | FR-098, FR-203 |
| `COR_AlertTypes` | los de §22 | FR-169 |
| `IntegrationParticipantClass`, `ClavesDeRecaudador` | `Test` y `TST` quedan reservados sin uso; el escritor los rechaza en todo ambiente | FR-223 |
| `COR_IntegrationParticipants` | migración de datos `RetiroDeLosDoblesDePrueba`: sólo cierra vigencias (`ValidTo`), porque la entidad es `IHechoInmutable` | FR-223 |
| `AccountingModules` | `Receipts = 128` | R-02 |
| `ModuloContable` | `Recaudo = "REC"` | R-02 |
| `AdjuntosDeModulo` | `ReceiptsSourceOfFunds`, `ReceiptsContingencyImage`, `ReceiptsCountMinutes`, `ReceiptsPaymasterResponse`, `TreasuryPaymentFile`, `TreasuryPaymentResponse`, `TreasuryStatement`, `TreasuryDeliveryReceipt`, `TreasuryReconciliationReport`, `PersonBankCertificate` (no borrables los que generan los módulos) | FR-112, FR-156 |

---

## 17. Contabilidad (`ACC_`, E-04; `IntegracionContableDeRecaudoYTesoreria`, R0/T0)

### 17.1 `ACC_ReceiptsPostingRules` · `ACC_TreasuryPostingRules`

Molde de `ACC_LendingPostingRules`: `FactType`, `Role`, dimensiones por **código** (`MeansCode`, `MeansClass`,
`CollectionPointCode`, `CashBoxCode`, `BranchCode`, `OwnBankAccountCode`, `MiscConceptCode`, `TargetModule`,
`CollectorClass`, `OrderKind`, `OrderOrigin`, `Cause`), cada una exigida por igualdad o `*`, con pesos de especificidad
(detalle en [contracts/contabilidad.md](contracts/contabilidad.md) §4), `ChartOfAccountId`, `CostCenterId?` (se asigna, no
pesa), `ValidFrom`, `ValidTo`. Sin retroactividad sobre lo contabilizado. La plantilla sugiere filas sin cuenta; no se
siembra ninguna regla con cuenta.

### 17.2 `ACC_ReceiptsPostings` · `ACC_TreasuryPostings` · `ACC_ReceiptsVoucherMappings` · `ACC_TreasuryVoucherMappings`

Recibos de contabilización por mensaje (único `MessagePublicId`) y tipo de comprobante por hecho, como en Cartera.

### 17.3 `ACC_OwnBankAccountLinks` (FR-102, R-22)

`OwnBankAccountPublicId` (de `TES_BankAccounts`), `ChartOfAccountId` (FK, debe ser bancaria: `EsBancaria`), `ValidFrom`,
`ValidTo`. Único vigente en los dos sentidos (una cuenta propia ↔ una cuenta contable,
`Accounting.OwnBankAccount.AlreadyLinked`). Lo escribe sólo `IContabilidadParaTesoreria.VincularCuentaBancariaAsync`.
Resuelve el rol `Banco` de las dos matrices.

### 17.4 Guardas de cierre (E-04)

Sin tabla nueva: `Accounting.Period.OpeningSquarePending` (cuadre provisional pendiente en el mes de la activación,
FR-228) y, en T1, `Accounting.Period.ReconciliationsOpen` / `.CashSessionsOpen` (aviso informativo por defecto,
bloqueante si `Contabilidad.CierreExigeConciliaciones`, FR-132), leídos por `IEstadoOperativoParaElCierre` (§3 de
[contracts/interfaces.md](contracts/interfaces.md)).

---

### 17.5 Conciliación bancaria de la 009, sin uso (E-04, TR41)

La conciliación de Tesorería (§12) **reemplaza** a la de Contabilidad (009, FR-055 a FR-060; decisión del dueño del
2026-10-23). `ACC_BankReconciliations`, `ACC_BankStatementLines` y `ACC_BankStatementColumnMaps` (entidades
`BankReconciliation`, `BankStatementLine`, `BankStatementColumnMap`) quedan **sin uso**: nadie las escribe
(`NadieEscribeLaConciliacionBancariaDeContabilidad`) y `ReopenPeriodCommand` deja de marcarlas `Outdated` y llama a
`IConciliacionBancariaParaContabilidad`. **No se borran en esta feature**: su retiro, si se hace, es una migración
destructiva aparte, con guarda, respaldo y segundo revisor (pendiente, research.md «Pendientes»).

## 18. Cartera y Captaciones (`LND_`, E-01 y E-02)

| Entrega | Migración | Cambio |
|---|---|---|
| R0/T0 | — (sin esquema) | código: requisitos de activación sin la clase `Test`; emisión de `ArchivoDeDesembolsosGenerado`/`…Anulado` (E-01 c) |
| R1 | `CarteraYCaptacionesConPartidaCitada` (nueva, aditiva; las de K1 están congeladas) | `LND_LoanOperations.BankItemPublicId` (Guid?) y `LND_MemberAccountOperations.BankItemPublicId` (Guid?), para la exención de fecha valor de la v2; índices no únicos |
| R1 | — | consulta de la carga aplicada sin activar (E-01 b); cancelación con entrega por el contrato (E-02 d) |
| T2 | — | recaudos esperados (E-01 d, E-02 f); saldo del fondo de liquidez (E-02 e) |

---

## 19. Estados y transiciones

### 19.1 Convención

Cada transición tiene comando, permiso y hecho (FR-189). `→` es la transición; entre corchetes el comando.

### 19.2 Sesión de caja (`CashSessionState`)

```
            [OpenCashSession]                     [RegisterClosingCount]
  (nada) ─────────────────────► Open ───────────────────────────────► Closing
                                  │ cruza la hora de corte                │ sin diferencias o dentro de tolerancia
                                  ▼                                       ▼
                           ForgottenOpen ──[CloseForgottenSession]──► Closed
                                                                          ▲
                                       Closing ── diferencia > tolerancia ─┤
                                          ▼                                │ [ApproveCountDifference] (otra persona)
                               ClosedWithDifferencePending ────────────────┘
```

Un `Closed` no se reabre: `RCD_CashSessionAdjustments` (FR-033).

### 19.3 Operación de caja (`CashOperationState`)

```
Preparing ──[ConfirmCashOperation]──► Confirmed (nace el recibo)
   │  lista vinculante (al buscar o confirmar)  ─► Blocked  ──[liberación del oficial]──► Preparing
   │  lista no vinculante con retención ─────────► Withheld ──[ReleaseWithheldOperation, oficial]──► Preparing (simulación nueva)
   │                                                        └─[RefundWithheldMoney, egreso R1 / orden de pago]──► Abandoned
   └──[AbandonCashOperation]──► Abandoned
```

### 19.4 Recibo (`ReceiptState`) y aplicación (`ApplicationState`)

```
Valid ──[RequestReceiptVoiding]──► VoidPendingApproval ──[ApproveReceiptVoiding, otra persona]──► Voided
                                          └──[RejectReceiptVoiding]──► Valid
Aplicación: Applied ──[aviso del módulo]──► ReversedByModule
            Applied ──[anulación del recibo]──► Voided       (una ReversedByModule se omite, FR-194)
Porción:    Applied ──[cheque devuelto | anulación]──► Reversed
```

### 19.5 Cheque recibido (`ReceivedCheckState`)

`Received → Deposited → InClearing → Paid`; `Received | Deposited | InClearing → Returned` [`ReturnReceivedCheck`];
`InCustody → Received` al llegar su fecha (posfechado admitido por parámetro). En R0 el paso a `InClearing` y `Paid` es
manual o por cruce del extracto.

### 19.6 Partida por identificar (`UnidentifiedItemState`)

`Open → PartiallyIdentified → Identified`; `Open | PartiallyIdentified → Classified` (con aprobación) |
`ReturnedToDepositor` (orden de pago); nunca cambia sola: el escalón de antigüedad sólo alerta (FR-073).

### 19.7 Archivo de descuentos en Recaudo

`Received → Sent` [`ConfirmPayrollFileSent`] `→ Responded` [`EmitPaymasterResult`, aceptado por Cartera]; `Received →
Superseded` al llegar una versión que lo reemplaza. Respuesta: `Previewed → Emitted → AcceptedByLending |
RejectedByLending` (se corrige con otra versión).

### 19.8 Orden de pago (`PaymentOrderState`)

```
 Received ──validación──► Pending (motivos) ──corrige el maestro / libera el oficial──► Ready
 Received ──validación──► Ready ──[AddToBatch]──► InBatch ──lote aprobado──► Approved ──archivo enviado / cheque emitido──► Sent
 Sent ──resultado Disbursed──► Disbursed ──(sólo Check) resultado CheckNotCashed──► CheckNotCashed
 Sent ──resultado Rejected──► Rejected                    (el origen decide: reverso, reintegro o regiro)
 Received | Pending | Ready | InBatch (lote no enviado) ──anulación del origen──► Voided (resultado Voided)
 Sent con anulación del origen ──► sigue hasta conocer el resultado (VoidRequested = true)
 Received ──valor ≤ 0 o duplicada──► RejectedOnArrival (evaluación negativa en la bandeja)
 PaidByLendingFile ──resultado registrado──► Disbursed | Rejected   (nunca entra a un lote)
```

Archivo anulado sin enviar o rechazado entero por el banco: sus órdenes vuelven a `Ready` **sin** resultado (FR-112,
FR-120). Una orden de regiro llega como orden nueva con `ReplacesOrderId`.

### 19.9 Resultado del giro (`TransicionesDelResultado`, FR-215)

| Estado previo de la orden | Admite | No admite |
|---|---|---|
| `Sent` (transferencia o pago a tercero) | `Disbursed`, `Rejected` | `CheckNotCashed`, `Voided` |
| `Sent` (cheque) | `Disbursed` (al entregar con constancia) | `Rejected`, `Voided` |
| `Disbursed` (cheque) | `CheckNotCashed` (al anular un cheque entregado no cobrado) | `Rejected`, `Voided` |
| `Disbursed` (transferencia) | nada (el abono devuelto entra por Recaudo, FR-199) | todo |
| `Received`/`Pending`/`Ready`/`InBatch` con anulación del origen | `Voided` | `Disbursed`, `Rejected` |
| antes de `Sent` sin anulación | nada (`Treasury.Result.NotSent`, US11-8) | todo |

Ningún proceso registra un resultado; repetirlo no cambia nada.

### 19.10 Lote, cheque girado y conciliación

- Lote: `Draft → PendingApproval → Approved → FileGenerated → Sent → PartiallyConfirmed → Confirmed`; `FileGenerated →
  Voided` (no enviado); `Sent → RejectedByBank` (rechazo técnico del archivo, FR-120); `Approved | PendingApproval →
  Draft` al rechazar la aprobación.
- Cheque girado: `Issued → Delivered → Cashed`; `Issued → Voided` (dañado o anulado antes de entregar); `Delivered →
  NotCashed` (anulado con permiso tras los días configurados); blanco dañado `SpoiledBlank`.
- Conciliación: `Open → Closed → Reopened` (versión nueva `Open` con motivo y permiso).

---

## 20. Enumeraciones (dominio en inglés; valores fijos)

| Enum | Valores |
|---|---|
| `ReceiptsModuleStatus` · `TreasuryModuleStatus` | `NotActivated = 1`, `Active = 2` |
| `ReceiptChannel` | `CashDesk = 1`, `Bank = 2`, `Paymaster = 3`, `Agreement = 4`, `Online = 5`, `Contingency = 6` |
| `ReceiptDocumentKind` | `CashReceipt = 1`, `BankReceipt = 2`, `AgreementReceipt = 3`, `PaymasterReceipt = 4`, `OnlineReceipt = 5`, `ContingencyReceipt = 6`, `CashDisbursement = 7`, `Voiding = 8`, `CashMovement = 9`, `SessionAdjustment = 10` |
| `CashSessionState` | `Open = 1`, `Closing = 2`, `ClosedWithDifferencePending = 3`, `Closed = 4`, `ForgottenOpen = 5` |
| `CashOperationState` | `Preparing = 1`, `Blocked = 2`, `Withheld = 3`, `Confirmed = 4`, `Abandoned = 5` |
| `ReceiptState` | `Valid = 1`, `VoidPendingApproval = 2`, `Voided = 3` |
| `ApplicationState` | `Applied = 1`, `ReversedByModule = 2`, `Voided = 3` |
| `ApplicationTarget` | `Loan = 1`, `Account = 2`, `MiscConcept = 3`, `CommercialReceivable = 4`, `PendingApplication = 5` |
| `CashMovementKindR` | `Opening = 1`, `SupplyFromVault = 2`, `WithdrawToVault = 3`, `TransferBetweenBoxes = 4`, `MeansReclassification = 5`, `Custody = 6`, `CustodyRelease = 7` |
| `CountTreatment` | `None = 0`, `CashierReceivable = 1`, `Expense = 2`, `Surplus = 3` |
| `ModuleReversalCause` | `MoneyReceived = 1`, `MoneyNotReceived = 2`, `PendingConfirmation = 3` |
| `ReceivedCheckState` | `Received = 1`, `InCustody = 2`, `Deposited = 3`, `InClearing = 4`, `Paid = 5`, `Returned = 6` |
| `UnidentifiedItemState` | `Open = 1`, `PartiallyIdentified = 2`, `Identified = 3`, `Classified = 4`, `ReturnedToDepositor = 5` |
| `BankAccountPurpose` (flags) | `Collection = 1`, `Payments = 2`, `Payroll = 4`, `Agreement = 8`, `LiquidityFund = 16` |
| `PaymentOrderOrigin` | `LendingDisbursement = 1`, `DepositsPayout = 2`, `Receipts = 3`, `Manual = 4`, `SupplierInvoice = 5`, `AccountingPayable = 6` |
| `PaymentForm` | `Transfer = 1`, `Check = 2`, `ThirdPartyPayment = 3` |
| `PaymentOrderState` | `Received = 1`, `Pending = 2`, `Ready = 3`, `InBatch = 4`, `Approved = 5`, `Sent = 6`, `Disbursed = 7`, `Rejected = 8`, `CheckNotCashed = 9`, `Voided = 10`, `RejectedOnArrival = 11` |
| `PaymentBatchForm` | `File = 1`, `BankPortal = 2`, `Checks = 3` |
| `PaymentBatchState` | `Draft = 1`, `PendingApproval = 2`, `Approved = 3`, `FileGenerated = 4`, `Sent = 5`, `PartiallyConfirmed = 6`, `Confirmed = 7`, `RejectedByBank = 8`, `Voided = 9` |
| `DisbursementOutcome` | `Disbursed = 1`, `Rejected = 2`, `CheckNotCashed = 3`, `Voided = 4` (texto en el mensaje, como v1) |
| `IssuedCheckState` | `Issued = 1`, `Delivered = 2`, `Cashed = 3`, `Voided = 4`, `NotCashed = 5`, `SpoiledBlank = 6` |
| `RemittanceState` | `InTransit = 1`, `Confirmed = 2`, `Expired = 3`, `Voided = 4` |
| `ReconciliationState` | `Open = 1`, `Closed = 2`, `Reopened = 3`, `Outdated = 4` |
| `OpeningSquareKind` | `Definitive = 1`, `Provisional = 2` |
| `PersonBankAccountState` | `PendingApproval = 1`, `Active = 2`, `Rejected = 3`, `Retired = 4` |
| `BankFileScope` (existente) | ver §16.3 |
| `ScreeningContext` (existente, `Domain/Enums/Lending/OriginationEnums.cs`) | se suman `Collection = 6`, `Disbursement = 7` |

`LosEnumsDelContratoNumeranComoElDominio` se extiende a los que viajan en mensajes.

---

## 21. Errores (familias; mensaje en español que dice qué hacer)

| Código | Cuándo |
|---|---|
| `Receipts.NotActivated` · `Treasury.NotActivated` | operación antes de activar el módulo |
| `Receipts.Session.Required` · `.AlreadyOpen` · `.NotOpen` · `.Forgotten` · `.CashierExclusive` | US2-1, US2-2, FR-022, FR-034 |
| `Receipts.Session.CashBlockCapReached` | tope de bloqueo del efectivo (FR-181) |
| `Receipts.Count.DifferenceNeedsApproval` · `.SelfApproval` | FR-032, FR-012 |
| `Receipts.Day.SessionsOpen` · `.DifferencesPending` | FR-033 |
| `Receipts.PaymentMeans.CreditClassNotAllowed` · `.CardSetupIncomplete` · `.CheckDisabled` · `.NotInDelivery` · `.VoucherAlreadyUsed` | FR-023, FR-024, FR-041 |
| `Receipts.Receipt.MeansTotalMismatch` · `.ChangeOnlyInCash` · `.ItemRejected` (con `data.items[]`) · `.DistributionChanged` (con `data.simulations[]`) · `.SurplusNotAllocated` · `.ChoiceRequired` · `.InvoiceRequired` | FR-041 a FR-043, FR-179, FR-190 |
| `Receipts.Receipt.BackdatedNotAllowed` · `.BackdatedNeedsApproval` | FR-047, FR-192 |
| `Receipts.Voiding.SelfApproval` · `.ContractRejected` · `.AlreadyVoided` · `.PayrollOwned` | FR-057, FR-062, US5 |
| `Receipts.Compliance.BindingListMatch` · `.DeclarationRequired` · `.DeliveredByRequired` | FR-094, FR-191, FR-203 |
| `Receipts.BankItem.OverIdentified` · `.AlreadyIdentified` · `.SplitMismatch` | FR-066, FR-068 |
| `Receipts.UnidentifiedItem.ClassificationNeedsApproval` | FR-072 |
| `Receipts.Paymaster.VersionNotAdopted` · `.AlreadySent` · `.HashMismatch` · `.ResponseRejected` · `.RemittanceExceedsPending` | FR-075 a FR-080 |
| `Receipts.Check.Postdated` · `.ThirdPartyNotAllowed` · `.AlreadyReturned` | FR-083, FR-085 |
| `Treasury.BankAccount.LiquidityFundExclusive` · `.NotLinked` · `.BankWithoutTransferCode` | FR-102, FR-207 |
| `Treasury.LiquidityFund.UseRequired` | FR-207, SC-028 |
| `Treasury.Order.InvalidAmount` · `.PaidByLendingFile` · `.AlreadyInBatch` · `.DestinationNotRegistered` · `.DestinationWaiting` · `.BindingListMatch` | FR-107 a FR-110, FR-116, FR-203, FR-210 |
| `Treasury.Batch.SelfApproval` · `.FormatMismatch` · `.SourceIsLiquidityFund` · `.ReviewRequired` · `.NotApproved` | FR-110 a FR-112, US26-1 |
| `Treasury.File.SameUserDownload` · `.AlreadySent` · `.NotSent` | FR-112 |
| `Treasury.Result.NotSent` · `.InvalidTransition` · `.Discrepancy` · `.SupportRequired` | FR-118 a FR-123, FR-212, FR-215 |
| `Treasury.Checkbook.Exhausted` · `.NumberOutOfRange` | FR-134, US19-1 |
| `Treasury.Statement.AlreadyLoaded` · `.IllegibleLines` | FR-066, FR-196, FR-197 |
| `Treasury.Reconciliation.SegregationConflict` · `.AlreadyMatched` · `.NotSquared` · `.Closed` | FR-216, FR-217 |
| `Treasury.Compliance.BindingListMatch` | FR-203 |
| `Receipts.Activation.RequirementsMissing` · `Treasury.Activation.RequirementsMissing` (con `data.missing[]`) | FR-150, US9-4 |
| `Integration.Participant.TestDoublesRetired` | FR-223 |
| `Receipts.Setup.CutoffAfterLendingCycle` | versión de `Recaudo.HoraDeCorte` que no cae antes del ciclo de Cartera (FR-185, TR43) |
| `Approvals.PresenceRequired` (`428`, **nuevo**) | la caja pide el aprobador presente; la repetición lleva `X-Approval-Request` y la misma clave; las fallas del desafío son las `Approvals.Presence.*` existentes (`NotRequester`, `Invalid`, `Expired`, `TotpReused`) (FR-013, api.md §1.4) |
| `Lending.Payment.BankItemDateMismatch` (**nuevo**, de Cartera, R1) | v2 del contrato con la partida citada y `ValueDate ≠ MovementDate` (contratos-v2.md §1.2) |
| `Core.PersonBankAccount.SelfApproval` · `.CertificateRequired` | FR-210 |
| `Accounting.Period.OpeningSquarePending` · `Accounting.OwnBankAccount.AlreadyLinked` · `Accounting.Document.ModuleOwned` (Recaudo y Tesorería) | FR-145, FR-228 |
| `Validation.Invalid` · `Generic.NotFound` · `Request.BodyInvalid` · `Operation.KeyReused` (409, misma clave y otra huella) | comunes |

---

## 22. Tipos de alerta (`COR_AlertTypes.TypeCode`)

`Recaudo.SesionOlvidada`, `Recaudo.TopeDeEfectivo`, `Recaudo.DiferenciaPorAprobar`, `Recaudo.DiaDelPuntoAbierto`,
`Recaudo.AplicacionReversadaPorModulo`, `Recaudo.ChequeDevuelto`, `Recaudo.PartidaAntigua` (30/60/90),
`Recaudo.ExtractoSinCargar`, `Recaudo.PartidasAbiertasAlCierre`, `Recaudo.SaldoAFavorAntiguo`,
`Recaudo.NoAplicadoDePagaduria`, `Recaudo.GiroDePagaduriaTardio` (R1), `Recaudo.FechaValorTardia`,
`Recaudo.ListaVinculante`, `Recaudo.ListaNoVinculante`, `Recaudo.Fraccionamiento`, `Recaudo.AcumuladoDeEfectivo`,
`Recaudo.PagadorOcasionalSobreUmbral`, `Recaudo.ContingenciaPorDigitar`, `Recaudo.CuadreDiarioConDiferencia`,
`Recaudo.CuadreProvisionalPendiente`, `Recaudo.PoliticaContableEnAdvertir`, `Recaudo.HechoNoContabilizable`;
`Tesoreria.OrdenSinResultado`, `Tesoreria.ChequeSinCobrar`, `Tesoreria.DiscrepanciaEnRespuesta`,
`Tesoreria.CuentaDestinoCambiada`, `Tesoreria.GiroDevuelto`, `Tesoreria.ListaVinculante`, `Tesoreria.RemesaSinConfirmar`,
`Tesoreria.CargoNoIdentificado`, `Tesoreria.PartidaConciliatoriaAntigua`, `Tesoreria.UsoDelFondoDeLiquidez` (aviso previo
a la Supersolidaria), `Tesoreria.CuadreProvisionalPendiente`, `Tesoreria.PoliticaContableEnAdvertir`;
`Plataforma.CuentaDestinoCompartida`, `Plataforma.GoLiveEnRojo`.

Destinatarios por permiso (`IDestinatariosPorPermiso`) y alcance (`FiltroDeAlcanceDeAlertas`).

---

## 23. Parámetros (`COR_ParameterVersions`, módulos `RCD` y `TES`; con vigencia y fuente)

Los marcados **P** van «pendiente de validar». Los marcados **sin valor** no se siembran: sin valor, no rigen.

| Clave | Módulo | Defecto | Fuente / nota |
|---|---|---|---|
| `Recaudo.HoraDeCorte` | RCD | 18:00 | FR-185 |
| `Caja.ArqueoCiego` · `Caja.BaseModo` · `Caja.UnaSesionPorCajero` · `Caja.DobleCustodia` · `Caja.TratamientoFaltante` | RCD | sí · `DailyBase` · sí · sí · `CashierReceivable` | D6, D7, D8, D34 |
| `Caja.Sobrantes.DiasAntesDeIngreso` | RCD | sin valor | P8 |
| `Caja.AdmiteCheque` · `Caja.AdmiteChequeDeTercero` · `Caja.AdmitePosfechadoEnCustodia` | RCD | no · sí · no | D21, D37 |
| `Caja.CobroSoloDeSuOficina` | RCD | no | D36 |
| `Caja.FechaValorAnterior.MaxDias` | RCD | 0 | FR-192 |
| `Recibo.OrdenDeImputacion` | RCD | inmediatos primero, cheque al final | D35 |
| `Cheques.PlazoPresentacionMismaPlaza` · `.OtraPlaza` · `Cheques.BloquearFueraDePlazo` | RCD | 15 días · 1 mes · no | C. Co. art. 718, **P** |
| `Cheques.CargoPorDevolucion` | RCD | apagado | C. Co. art. 731, **P** |
| `Partidas.EscalonesDias` · `Partidas.DestinoVencido` | RCD | 30, 60, 90 · `SiguenComoPasivo` | D10, **P** (P8) |
| `Extractos.DiasHabilesSinCarga` | RCD | 2 | FR-196 |
| `Libranza.DiasHabilesGiro` | RCD | 3 | Ley 1527 art. 6, **P**, informativo (R1) |
| `Sarlaft.UmbralDeclaracionOrigenFondos` | RCD | 5.000.000 | **P** (P7); por operación |
| `Sarlaft.UmbralTransaccionIndividual` | RCD | 5.000.000 | proyecto de circular Supersolidaria dic-2024, respuesta UIAF 2025, **P** |
| `Sarlaft.UmbralTransaccionesMultiples` | RCD | 30.000.000 | ídem, **P**; mes calendario por persona |
| `Sarlaft.Fraccionamiento` | RCD | 3 operaciones bajo el umbral de declaración en 7 días | **P** |
| `Sarlaft.PagadorTerceroFrecuente.Titulares` · `Sarlaft.ConsignanteRepetido.Partidas` | RCD | 3 titulares distintos en el mes · 2 partidas abiertas | **P** (P7); **(nuevo, 2026-10-24)** |
| `Sarlaft.Reporte.Estado` | RCD | `Vigente` (por período) | **P**; la suspensión de 2025 se registra como versión |
| `Sarlaft.Reporte.Alcance` · `Sarlaft.Reporte.ExigeAusencia` | RCD | `SoloEfectivo` · no | **P** |
| `Sarlaft.ListasNoVinculantes.Efecto` | RCD, TES | `Sigue` | D22 |
| `Contabilidad.FaltaDeRegla` · `Contabilidad.ModoDePaso` | RCD, TES | `Bloquear` · recibos de caja `PorLotes`, lo demás `EnLinea` | D25 |
| `Conservacion.Anios` | RCD, TES | 10 (piso 5) | C. Co. art. 60, **P** (P19) |
| `Giros.DiasSinResultado` · `Cheques.DiasSinCobrar` · `Cheques.DiasACuentaPorPagar` | TES | 3 · 30 · 180 | **P** |
| `Lotes.TerceraPersonaParaResultado` | TES | no | D18 |
| `CuentasDestino.DiasDeEspera` · `CuentasDestino.DiasDeMarca` | plataforma | sin valor | D40, P4 |
| `Remesas.DiasParaConfirmar` | TES | 2 | — |
| `Conciliacion.DiasDeHolgura` | TES | 3 | FR-216 |
| `Contabilidad.CierreExigeConciliaciones` | ACC | no | D29 (T1) |
| `Gmf.Tarifa` | TES (y RCD en R1) | 0,004 | ET art. 871, **P** (P10) |
| `Gmf.HechosGeneradoresDeEgresos` · `Gmf.ExencionDesembolsos` · `Gmf.Redondeo` | TES | devoluciones de saldos a favor y partidas gravadas; desembolso al deudor exento · al peso | DUR 1625 de 2016 y ET art. 879, **P** |

Los límites de monto por permiso viven en `SEC_PermissionAmountLimits` (sin valor sembrado: P4).

---

## 24. Retiro de lo heredado de Tesorería (después de T2, FR-161)

Migración destructiva con guarda `RetiroDeLaTesoreriaHeredada` (commit propio, marcador
`MIGRACION-DESTRUCTIVA-APROBADA`, declarada **irreversible**, respaldo previo y segundo revisor):

1. **Diagnóstico** por base en cada ambiente: `specs/015-recaudo-tesoreria/diagnostico-tesoreria-heredada.sql` (lo crea
   la tarea de T2) cuenta filas de `TRS_Checks`, `TRS_Concepts` y `TRS_Invoices` y lista los pendientes (cheques sin
   cobrar, facturas por pagar).
2. **Exportación para archivo** de toda fila existente (xlsx y csv con huella, como adjunto no borrable).
3. **Migración de pendientes** a Tesorería por plantilla (`TES_LegacyPendingLoads` → cheques en tránsito y órdenes por
   pagar de T2), cotejada con las cifras de referencia.
4. **Guarda**: se niega si queda alguna fila sin migrar, salvo la fila `COR_SystemSettings`
   `TES.RetiroHeredado.Aprobado` con respaldo y segundo revisor.
5. Borra las tres tablas, sus entidades, handlers, rutas (`/api/treasury/{checks,concepts,invoices,cash-flow}`) y
   pantallas (`/tesoreria/{cheques,conceptos,facturas,flujo-caja}`) **sin alias**, y la marca «E3 (feature 009)».

Lo heredado de tarjeta débito (`Domain/Entities/Debit`, `/tarjeta-debito/*`) **no se toca** (D32, P9).

---

## 25. Migraciones pares por entrega

| # | Entrega | Migración | Tipo | Contenido |
|---|---|---|---|---|
| 1 | R0/T0 | `PlataformaParaRecaudoYTesoreria` | aditiva con datos | `COR_PersonBankAccounts` (+ copia de `Person.SupplierBank*` como `Imported`), `COR_GoLiveChecklists(Items)`, semillas de formatos genéricos, tipos de alerta, tipos de adjunto |
| 2 | R0/T0 | `RetiroDeLosDoblesDePrueba` | datos, idempotente | cierra las filas vigentes `IsTestDouble = true` de `COR_IntegrationParticipants` y sus adopciones **sólo con `ValidTo`** (la entidad es `IHechoInmutable`: `IsEnabled` y `Reason` no se escriben; el motivo va a la auditoría), como `EscritorDelRegistroDeIntegracion.RetirarAsync`; en producción no hace nada |
| 3 | R0 | `RecaudoNucleo` | aditiva | §1 a §7.2 y `RCD_Activations` de §15.1 (64 tablas); semilla de `RCD_Setup`, tipos de documento, conceptos del sistema, reglas de modalidad, causales de devolución |
| 4 | T0 | `TesoreriaNucleo` | aditiva | §9 a §11, §12.1, §10.8 y `TES_Activations` de §15.1 (22 tablas); semilla de `TES_Setup` |
| 5 | R0/T0 | `IntegracionContableDeRecaudoYTesoreria` | aditiva | §17.1 a §17.3; semilla de tipos de comprobante sugeridos y mapeos sin cuenta |
| 5a | T0 | `TesoreriaConciliacionMinima` | aditiva, posterior al 10/11 | §12.2 a §12.4 (3 tablas `TES_`) |
| 5b | R0/T0 | `RecaudoYTesoreriaPuestaEnMarcha` | aditiva, posterior al 10/11 | §15.2 y las cifras de referencia (3 `RCD_`, 3 `TES_`) y la FK de `OpeningSquareId` en las activaciones |
| 6 | R1 | `RecaudoCompleto` | aditiva | §7.3, §8.1 |
| 7 | R1 | `CarteraYCaptacionesConPartidaCitada` | aditiva | §18 |
| 8 | T1 | `TesoreriaCompleta` | aditiva | §13.1 |
| 9 | R2 | `RecaudoCanales` | aditiva | §8.2 |
| 10 | T2 | `TesoreriaAmpliada` | aditiva | §13.2 |
| 11 | después de T2 | `RetiroDeLaTesoreriaHeredada` | **destructiva con guarda** | §24 |

Orden de aplicación en R0/T0: 1 → 3 → 4 → 5 → 5a → 5b → 2 (el de las marcas de tiempo). Los pares 1, 3, 4 y 5 se
regeneran mientras se construyen hasta el despliegue en QA del 10/11 (T0271) y ahí se **congelan**, con sus huellas en
`LasMigracionesCongeladasNoSeRegeneran` (decisión del dueño del 2026-10-23); desde ese día no se regenera nada y todo
cambio de R0/T0 es un par nuevo y aditivo (5a, 5b, 2 y los que hagan falta), cuyas huellas se sellan al cierre (T0337). Las semillas van con
`INSERT … WHERE NOT EXISTS` en los dos motores. Los permisos `Receipts.*` y `Treasury.*` y las plantillas de rol los
siembra la API al arrancar (no el DbMigrator), como los de Cartera.

---

## 26. Nombres fijados en este documento

Entidades y tablas de §1 a §17; enums de §20; errores de §21; alertas de §22; parámetros de §23; migraciones de §25;
`CerrojoDeRecaudo`, `CerrojoDeTesoreria`, `NumeradorDeRecaudo`, `NumeradorDeTesoreria`, `PrecisionDeRecaudo`,
`PrecisionDeTesoreria`, `ImputacionDeMedios`, `ArqueoDeCaja`, `SenalesDeEfectivo`, `TransicionesDelResultado`,
`EvaluadorDeGmfDeEgresos`, `CalculoDeGmf`, `DestinoRecaudo`, `DestinoTesoreria`, `PaymentBatchState`,
`Receipts.ProcessingRun.Summary`, `TomarCerrojosAsync`, `TesoreriaConciliacionMinima`, `RecaudoYTesoreriaPuestaEnMarcha`.
La lista consolidada está en
[decisiones-transversales.md](decisiones-transversales.md) §2.
