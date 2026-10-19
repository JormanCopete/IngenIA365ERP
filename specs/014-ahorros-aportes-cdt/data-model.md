# Data Model — 014 Cartera financiera: captaciones (ahorro, CDAT y aportes)

**Feature**: 014 | **Fecha**: 2026-10-14 | **Rama**: `014-ahorros-aportes-cdt` | **Spec**: [spec.md](spec.md) |
**Research**: [research.md](research.md)

Este documento extiende el modelo de la Cartera financiera (`specs/013-cartera-financiera/data-model.md`, en adelante
«la 013»). Cubre los **cambios a tablas existentes** de la 013 (§1), la **configuración** de las líneas de captación y
de aportes (§2), las **cuentas** (§3), el **libro** de cuentas (§4), las **restricciones** (§5), los **procesos** de
retiro del asociado, asamblea, fallecimiento, no reclamados y fondo de liquidez (§6), la **migración** (§7), el mapa y
el conteo por entrega (§8), los parámetros y valores nacionales (§9) y la lista de lo que este documento agrega a los
nombres canónicos (§10).

**Fuente de los nombres**: `research.md` §0 («Nombres canónicos») manda. Tablas, entidades, enums, puertos y
escritores se copian literalmente de allí. Lo que §0 no nombra lo nombra este documento y lo marca **(nuevo en
data-model)** la primera vez; la lista completa, para reconciliar, está en §10. Las columnas de detalle no llevan
marca (regla T1 de la 013).

---

## 0. Convenciones

Rigen **sin repetirlas** las convenciones de la 013, parte 1, §0.1 a §0.15: entidad base (`AuditableEntity`;
`AuditableEntityLong` sólo en hechos de alto volumen), hechos inmutables **H** (`IHechoInmutable`,
`GuardaDeInmutabilidad`, `[EscrituraUnica]`), `PublicId` y números, precisión por alias (`PrecisionDeCartera`), fechas
(`…Date` = `date` local de Colombia; `…At` = `datetime` UTC; tres fechas `ValueDate`/`AccountingDate`/`RegisteredAt`;
liquidar a (D, R)), vigencias y bitemporalidad (`VigenciaBitemporal.Vigente`, versiones de sólo inserción con
`ValidFrom`, `VersionNumber`, `RegisteredAt` y sin `ValidTo`), enums por valor numérico, personas que actúan
(`…ByUserId` = `SEC_Users.Id` por `IActorActual`; el proceso firma con `ActorKind.Process`), códigos de catálogo
(`CodigoDeCatalogo`), concurrencia (`CerrojoDeCartera`, esperar en operaciones y saltar en el proceso, `RowVersion`
como segunda defensa), escritores únicos, auditoría (módulo `Lending`, encadenada, diez años; `[SinDiffDeAuditoria]`
con evento resumen), índices nombrados con `HasDatabaseName`, entregas y migraciones pares, y parámetros del módulo
`LND`. Las FK a otros módulos siguen la regla de la 013 §0.1: sólo a `COR_People`, `COR_Branches`, `SEC_Users`,
`COR_Banks` y `COR_BankFileFormats`; adjuntos por `…AttachmentPublicId` (Guid, sin FK); **nunca** FK hacia `ACC_`,
`INV_` ni `PAY_`.

Lo **propio** de esta feature:

### 0.1 Carpetas

Entidades en `Domain/Entities/Lending/Deposits/{Configuration, Accounts, Transactions, Projections, Restrictions,
Processes, Migration}`. `MemberAccountTransaction` vive en `Deposits/Transactions` y `PrincipioXI_ContableImmutable`
se amplía a ese espacio. Configuraciones EF en `Persistence/Configurations/Lending/Deposits` (research §0.1). Enums
nuevos en `Domain/Enums/Lending/DepositEnums.cs`.

### 0.2 Signo del libro de cuentas

En `LND_MemberAccountTransactions` **todo concepto suma por su signo** y el saldo de un concepto es Σ de sus filas
(FR-001). El significado del signo depende de la familia del concepto (`MemberAccountConcept`):

| Familia | Conceptos | `+` significa |
|---|---|---|
| **Del titular** | `Principal`, `InterestPayable`, `ContributionMinimum`, `ContributionOrdinary`, `ContributionExtraordinary`, `ContributionRevaluation`, `Bonus` | **la Cartera le debe al titular** (un abono de interés suma; un retiro resta) |
| **Por cobrar al titular** | `ChargeReceivable` | el titular le debe a la Cartera un cargo pendiente (FR-128) |
| **Memoria de lo retenido** | `Withholding`, `Gmf` | lo retenido al titular para pagar al Estado; **no** forma parte del saldo del titular ni del disponible; su suma del año cuadra con el certificado (SC-016) |

Reglas:
- Saldo del titular de una cuenta de captación = `Principal + InterestPayable + Bonus − ChargeReceivable`; de una
  cuenta de aportes = Σ de los cuatro conceptos `Contribution*`. El interés por pagar no es disponible hasta que se
  abona (`InterestCredited`) o se paga (`InterestPaid`).
- Una retención o un GMF se escriben **en pareja** en la misma operación: `−Principal` (o `−InterestPayable` en el
  pago de intereses de un CDAT) y `+Withholding` (o `+Gmf`).
- Ninguna operación de usuario deja el saldo del titular bajo cero ni bajo el mínimo (FR-056). El único negativo
  posible es **técnico** (reverso de una consignación ya usada) y lo marca `LND_MemberAccounts.TechnicalNegativeSince`.

### 0.3 Precisión

Se usan los alias de la 013 §0.4 (`Monto` decimal(18,2), `TasaAnual` decimal(9,6) como fracción EA, `TasaEquivalente`
decimal(28,18), `Fraccion` decimal(9,6)) y uno nuevo:

| Alias | Tipo | Dónde |
|---|---|---|
| `MontoCausado` **(nuevo en data-model)** | decimal(18,6) | `LND_MemberAccountTransactions.Amount` (todas las filas: la causación diaria guarda hasta 6 decimales, FR-010, R05), los conceptos de interés de `LND_MemberAccountDailyPositions`, `LND_InterestSettlements.AccruedInterest` y `RoundingDifference` |

- Las filas que no son de causación ni de ajuste de causación llevan a lo sumo 2 decimales (regla de dominio en
  `RegistroDeCuentas`; `Lending.Savings.AmountTooPrecise` **(nuevo en data-model)**). La liquidación redondea al peso
  (o a la unidad de la versión) y la diferencia contra lo causado se ajusta en la **misma** operación (FR-061, D9).
- Tasas de captación, tramos, márgenes negociados, tarifas de retención, de GMF y de penalidad: `TasaAnual` (fracción;
  0,100000 = 10 % EA). Una tasa con más de 6 decimales se rechaza con `Lending.Rate.TooPrecise` (013).
- Proporciones de cotitulares y beneficiarios, porcentaje de la cuota de aportes sobre el ingreso, fracción de
  ahorro permanente, porcentajes del fondo de liquidez: `Fraccion`.

### 0.4 Número de cuenta

- `LND_MemberAccounts.Number` nvarchar(20): consecutivo **por línea** (FR-019, FR-035) que asigna
  `NumeradorDeCartera` con `LND_NumberSequences.Kind = MemberAccount` y `ScopeKey` = `LND_CreditLines.Code`, **al
  final** del orden de cerrojos. Un borrador de apertura no consume número; el número no se reutiliza ni se cambia, ni
  siquiera al cumplir el titular la mayoría de edad o al renovar un CDAT.
- El **título del CDAT** lleva el número de la cuenta con sufijo de la versión de condiciones (`{Number}-{n}`, R11);
  vive en el documento emitido, no en la cuenta.
- Al activar, el consecutivo de cada línea arranca por encima del mayor número numérico migrado de esa línea (como el
  de `Loan` en la 013 §1.10). Un número migrado no numérico se conserva tal cual.

### 0.5 Errores

Prefijos (FR-013): `Lending.Savings.*` (cuentas de captación y su libro), `Lending.TermDeposit.*` (CDAT y
contractual), `Lending.Contributions.*` (aportes, retiro del asociado, asamblea). Los de restricciones usan
`Lending.Holds.*` y `Lending.Garnishment.*`; los de GMF, `Lending.Gmf.*` (research R07). Se conservan los de la 013
cuando la regla es la misma (`Lending.Rate.TooPrecise`, `Lending.NotActivated`, `Lending.Reversal.OfReversal`,
`Catalogo.CodigoDuplicado`). Los códigos que este documento usa sin estar en research llevan la marca.

### 0.6 Cerrojo

Orden fijo (R08): `LND_Setup` compartido → `LND_DebtorControls` por persona (titular, cotitulares de la operación)
→ `LND_LoanControls` (si la operación toca créditos: cruce, débito automático) → **`LND_MemberAccountControls`**
exclusivos por `Id`, todos los de la operación en una sentencia → numeración al final. El acumulado del GMF y el
límite inembargable se calculan **bajo el cerrojo de la persona**, que es lo que los hace consistentes entre cuentas.

### 0.7 Escritores únicos

| Escritor | Qué escribe | Prueba |
|---|---|---|
| `RegistroDeCuentas` (`Application/Lending/Deposits/Ledger`) | `LND_MemberAccountOperations`, `LND_MemberAccountTransactions`, la marca de agua de `LND_MemberAccountControls`, `LND_MemberAccountDailyPositions`, `LND_InterestSettlements`, `LND_WithholdingAccumulations`, `LND_GmfMonthlyUsage`, `LND_MemberAccountTermsVersions`, `LND_TermDepositSchedules` y `LND_AccountInstallments` | `NadieEscribeElLibroDeCuentasFueraDelRegistro` (las vigila todas) |
| `NumeradorDeCartera` (013) | `Number` de cuentas, operaciones, retiros, distribuciones y casos; `NextValue` | `SoloElNumeradorNumera` (ampliada) |
| `EmisionDeCartera` → `EmisorDeMensajes` (013) | los hechos de captaciones y aportes, en la transacción de cada operación | `SoloElEmisorEscribeLaBandeja` |
| `GravamenMovimientosFinancieros` y `RetencionSobreRendimientos` (motores) | los únicos que calculan GMF y retención; `RegistroDeCuentas` sólo escribe lo que devuelven | `LaRetencionYElGmfSoloLosCalculanSusMotores` |

### 0.8 Auditoría

Llevan `[SinDiffDeAuditoria]`, con su evento resumen por corrida, tanda o importación: `MemberAccountDailyPosition`,
`MemberAccountControl`, `WithholdingAccumulation`, `GmfMonthlyUsage` y las filas de preparación de §7.
`MemberAccountTransaction` **no** lo lleva (Principio XI), ni `LiquidityFundHolding` (lo registra una persona). Toda
lectura de saldos y movimientos de un titular, extracto, certificado o exportación es **lectura sensible auditada**
(FR-003). Los comandos llevan `IConOficina` con la oficina de la cuenta.

### 0.9 Entregas

Columna **E** de cada tabla: la entrega en que nace (K7 aportes; K8 ahorro a la vista y permanente; K9 CDAT y
contractual; K10 asamblea, inactividad y fallecimiento; K11 liquidez e informes). Como en la 013 §0.14, los **valores**
de enum de entregas posteriores existen desde K7 pero ningún comando de K7 los escribe, y una columna que apunta a una
tabla de una entrega posterior nace con esa entrega. Migraciones pares por entrega en §8.2.

---

## 1. Cambios a tablas existentes de la 013

Todos los cambios son **aditivos** (columnas nulas o con defecto, valores de enum al final, filas de semilla). Ninguno
cambia el comportamiento de las líneas de crédito: los 32 casos dorados de crédito siguen verdes (SC-019).

### 1.1 `LND_CreditLines` — `CreditLine` (K7; FR-027, R01)

| Columna nueva | Tipo | Nulo | Regla |
|---|---|---|---|
| `Nature` | int (`LineNature`) | no | `Placement` 1 (defecto: todas las existentes), `Deposit` 2, `Contribution` 3. **Inmutable** desde la creación. IX `IX_LND_CreditLines_Nature_Status (Nature, Status)` |

Reglas:
- `Code` sigue único en el catálogo **entre las tres naturalezas** (UK filtrado vivos sin cambio) y sigue siendo la
  dimensión `CreditLineCode` de la matriz.
- `Activar` exige usura sólo si `Nature = Placement`; `BloquearPorUsura` y el efecto de réplica de `USURA` ignoran las
  demás (`LasCaptacionesNoUsanLaUsura`). `Draft → Active` en captación y aportes: al tener una versión vigente de su
  naturaleza (§2.1 o §2.5) y, en captación, con la naturaleza activada en `LND_Setup` o en ambiente de prueba.
- Una línea `Deposit` o `Contribution` con cuentas vivas no se borra: se cierra a cuentas nuevas
  (`Lending.CreditLine.HasAccounts` **(nuevo en data-model)**); las vivas siguen.
- Una línea de aportes por cooperativa **activa** a la vez: UK `UK_LND_CreditLines_ActiveContribution (Nature)`
  filtrado `[Nature] = 3 AND [Status] = 2 AND [IsDeleted] = 0` (**nuevo en data-model**; el asociado tiene una sola
  cuenta de aportes).
- `CreditLineVersion` (colocación) sólo se crea para `Nature = Placement`; `DepositLineVersion` sólo para `Deposit`;
  `ContributionLineVersion` sólo para `Contribution` (`Lending.CreditLine.NatureMismatch` **(nuevo en data-model)**).
- `LND_UserCreditLineScopes` no cambia: el alcance por línea rige para las tres naturalezas.

### 1.2 `LND_Setup` — `LendingSetup` (K7/K8; FR-116, R21)

| Columna nueva | Tipo | Nulo | Regla |
|---|---|---|---|
| `DepositsActivationDate` | date | sí | día siguiente al corte de la carga de captaciones activada; nulo = sin captaciones |
| `DepositsActivationId` | int FK `LND_Activations` | sí | la escribe `ActivateDepositsCommand`, una vez (null → valor) |
| `DepositsActivatedAt` | datetime | sí | |
| `ContributionsActivationDate` | date | sí | ídem para aportes |
| `ContributionsActivationId` | int FK `LND_Activations` | sí | la escribe `ActivateContributionsCommand` |
| `ContributionsActivatedAt` | datetime | sí | |

Reglas: cada naturaleza se activa una vez y no se desactiva. Exige `ActivationDate` (créditos) no nulo (R21:
créditos primero). Sin la fecha de su naturaleza, las operaciones de depósito del contrato responden «no habilitado»
sin efectos (FR-093; `Lending.Savings.NotActivated`, `Lending.Contributions.NotActivated` **(nuevos en data-model)**)
e `IConsultaDeAportes` responde con `ConsultaDeAportesNoDisponible`.

### 1.3 `LND_NumberSequences` — `LendingNumberSequence` (K7; FR-019, R08)

Valores nuevos de `LendingNumberKind` (al final; **nuevos en data-model**, research sólo dice «clase nueva por línea»):

| Valor | `ScopeKey` | Usa |
|---|---|---|
| `MemberAccount` 10 | `LND_CreditLines.Code` | número de cuenta (§0.4) |
| `MemberAccountOperation` 11 | nombre de `MemberAccountOperationKind` | comprobante de la operación (§4.1) |
| `MembershipWithdrawal` 12 | `''` | radicado del retiro del asociado (§6.1) |
| `AssemblyDistribution` 13 | `''` | §6.3 |
| `DeceasedHolderCase` 14 | `''` | §6.5 |

Los documentos nuevos usan `IssuedDocument` 7 con `ScopeKey` = la clase (sin cambio).

### 1.4 `LND_PayrollAgreementVersions` y tablas nuevas del convenio (K7; FR-125, FR-126 E-05, R17)

El convenio de pagaduría de K2 (013 §24) deja de ser sólo de créditos. Si K2 se construye antes que K7 (lo previsto),
las columnas nacen con la migración de K7; si no, nacen con `CarteraLibranza`.

**Columnas nuevas de `LND_PayrollAgreementVersions`**

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `UsesFileGroups` | bit | no | 0 = un solo archivo con `OutboundFormatId`/`ResultFormatId` de la versión (comportamiento de K2); 1 = un archivo por grupo (§1.4.1) y los formatos de la versión dejan de usarse |
| `EmployeeIdentification` | nvarchar(20) | no | `EmployeeIdAtPaymaster` (defecto) o `TaxId`: con qué identifica la pagaduría a la persona en ida y vuelta |
| `IncludesAccountInstallments` | bit | no | 0 por defecto: el convenio cobra también cuotas de aportes, ahorro permanente y contractual |

`OutboundFormatId` y `ResultFormatId` pasan a **nulos** cuando `UsesFileGroups = 1` (`CK_LND_PayrollAgreementVersions_Formats`:
o los dos de la versión, o ninguno y al menos un grupo). Las causas de no descuento siguen en
`LND_PayrollAgreementResultCodes` (013 §24.4).

#### 1.4.1 `LND_PayrollAgreementFileGroups` — `PayrollAgreementFileGroup` (K7, H; FR-125)

Hija de la versión del convenio (inmutable con ella: una versión usada por un archivo `Sent` no se edita).

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `PayrollAgreementVersionId` | int FK `LND_PayrollAgreementVersions` | no | |
| `Code` | nvarchar(10) | no | `CodigoDeCatalogo`; UK `UK_LND_PayrollAgreementFileGroups_Version_Code` |
| `Name` | nvarchar(120) | no | «Créditos», «Aportes y ahorros» |
| `OutboundFormatId` · `ResultFormatId` | int FK `COR_BankFileFormats` | no | ámbitos de descuento de pagaduría (§1.7), `BankId` nulo, elegidos por referencia |
| `CutoffDay` · `PaymentDay` · `SendLeadDays` | tinyint · tinyint · int | sí | calendario propio; nulo = el de la versión |
| `Order` | int | no | orden de generación |

#### 1.4.2 `LND_PayrollAgreementConceptEquivalences` — `PayrollAgreementConceptEquivalence` (K7, H; FR-125)

Un código o concepto de la pagaduría y las líneas y conceptos de la Cartera que agrupa.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `PayrollAgreementVersionId` | int FK | no | |
| `FileGroupId` | int FK `LND_PayrollAgreementFileGroups` | sí | obligatorio si la versión usa grupos |
| `PaymasterConceptCode` | nvarchar(20) | no | UK `UK_LND_PayrollAgreementConceptEquivalences_Version_Code` |
| `Description` | nvarchar(200) | no | |
| `AllocationRule` | int (`ConceptAllocationRule` **(nuevo en data-model)**) | no | `Priority` 1, `Proportional` 2: cómo se reparte lo que la pagaduría informa descontado por este código |
| `Priority` | int | no | prelación de este código frente a los demás del convenio cuando lo descontado no alcanza |
| `ItemsJson` | nvarchar(max) | no | JSON canónico: lista de `{ nature, creditLineCode \| "*", concept, priority }` (`concept` = `LoanConcept` o `MemberAccountConcept` por nombre, o `"Installment"` = la cuota completa). Al menos un ítem; `priority` única dentro del código con `Priority` |

Reglas: un ítem (línea × concepto) no aparece en dos códigos de la misma versión
(`Lending.Payroll.ConceptMappedTwice` **(nuevo en data-model)**). Una línea con cuota del convenio sin grupo o sin
código asignado, o un código sin regla de reparto, impide generar el archivo y dice cuál
(`Lending.Payroll.UnmappedLine` **(nuevo en data-model)**). Lo informado y no mapeado va a «no aplicados». La carga de
respuesta aplica créditos por `IContratoDeObligaciones` y cuotas de cuentas por `IContratoDeCuentas` en la misma unidad
de trabajo (R17).

### 1.5 `LND_IssuedDocuments`, `LND_IssuedDocumentEvents` y tabla nueva de enlace (K7–K9; FR-101 a FR-106, FR-068)

**`IssuedDocumentKind`** (al final, research §0.3): `AccountStatement` 7 (K8), `AccountBalanceCertificate` 8 (K8),
`DepositTaxCertificate` 9 (K8), `WithholdingCertificate` 10 (K8), `TermDepositTitle` 11 (K9), `ContributionCertificate`
12 (K7), `ContributionClearance` 13 (K7), `MembershipWithdrawalSettlement` 14 (K7). Sin cambio de esquema en
`LND_IssuedDocuments`: `AsOfDate`, `PeriodFrom/To` y `TaxYear` sirven igual; `FinalityUntil` se usa en el paz y salvo
de aportes. Las respuestas a juzgados (§5.2.1) se emiten con una clase propia, `GarnishmentResponse` 15 **(nuevo en
data-model)** (K8).

**Columna nueva de `LND_IssuedDocumentEvents`**

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `CausingAccountOperationId` | bigint FK `LND_MemberAccountOperations` | sí | el reverso que anuló el documento; exclusiva con `CausingOperationId` (`CK_LND_IssuedDocumentEvents_Cause`) |

#### 1.5.1 `LND_IssuedDocumentAccounts` — `IssuedDocumentAccount` **(nuevo en data-model)** (K7, H)

Gemela de `LND_IssuedDocumentLoans`: qué cuentas cubre un documento. `VigenciaDeDocumentos` la usa para anular por
reverso.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `IssuedDocumentId` · `MemberAccountId` | int FK | no | UK `(IssuedDocumentId, MemberAccountId)`; IX `(MemberAccountId)` |
| `LastOperationId` · `LastTransactionId` | bigint FK | sí | última operación y transacción consideradas |
| `BalanceAtCutoff` | `Monto` | sí | |
| `TermsVersionId` | int FK `LND_MemberAccountTermsVersions` | sí | título de CDAT: la versión de condiciones que titula |
| `PreviousIssuedDocumentId` | int FK `LND_IssuedDocuments` | sí | título: el título anterior al que reemplaza una prórroga o renovación (FR-068) |

### 1.6 `ACC_LendingPostingRules` — dimensiones nuevas (K7; FR-097, FR-099, FR-123 E-03, R19)

Cinco columnas **al final de la clave posicional** (`LendingPostingRule.ClaveDe` concatena por `|` sin letras; en el
código no hay prefijos de letra, sólo posición):

| Columna nueva | Tipo | Nulo | Valores |
|---|---|---|---|
| `LineNature` | nvarchar(15) | sí | `Placement`, `Deposit`, `Contribution`; nulo = `*` |
| `DepositClass` | nvarchar(15) | sí | `Demand`, `Contractual`, `Permanent`, `TermDeposit` |
| `IssueTermBand` | nvarchar(20) | sí | `UnderSixMonths`, `SixToTwelve`, `TwelveToEighteen`, `EighteenOrMore` |
| `ContributionClass` | nvarchar(15) | sí | `Minimum`, `Ordinary`, `Extraordinary`, `Revalued` |
| `AccountFlag` | nvarchar(15) | sí | `Standard`, `GmfExempt`, `Garnished` (research C20) |

`DimensionKey` = la clave de la 013 seguida de `|{LineNature|*}|{DepositClass|*}|{IssueTermBand|*}|{ContributionClass|*}|{AccountFlag|*}`
(nvarchar(300) basta). La migración de K7 **recalcula** `DimensionKey` de las filas existentes agregando cinco `*` (paso
de datos no destructivo; las reglas de crédito quedan con las nuevas en `*`).

Reglas:
- Los pesos de especificidad de la 013 **no cambian**. Las dimensiones nuevas son **exigidas por igualdad** en los
  roles que las declaran (§1.6.1) y **deben** ir nulas (`*`) en los demás (`Accounting.LendingRule.DimensionNotApplicable`
  **(nuevo en data-model)**).
- `ConceptCode` admite también el nombre de un `MemberAccountConcept` cuando `LineNature ≠ Placement`.
- `IssueTermBand` sólo se exige con `DepositClass` `TermDeposit` o `Contractual`; con otras clases va nula.
- `AccountFlag` del hecho (research C20): `Standard`, `GmfExempt` o `Garnished`, con precedencia `Garnished` >
  `GmfExempt` > `Standard`; lo exige `DepositPrincipal`. La **inactividad no es marca**: cambia el rol del capital a
  `DepositInactivePrincipal` (`CapitalDeCaptacionInactiva`).
- `CreditLineCode` sigue siendo la dimensión de línea para las tres naturalezas (R01).

#### 1.6.1 Roles nuevos (`RolesDeCuentaDeCartera`, al final; R19)

> **Canónico**: la lista completa de 17 roles (incluido `AbsorbedLoss`), sus familias (`Captacion`, `Patrimonio`) y
> exigencias es la de `contracts/contabilidad.md` §2; los nombres en el código son los españoles de research C10. La
> tabla siguiente es un resumen.

| Rol | Naturaleza | Exigidas además del rol | Cuenta CUIF sugerida en la plantilla (sin activar) |
|---|---|---|---|
| `DepositPrincipal` | pasivo | `LineNature`, `DepositClass` (+ `IssueTermBand` en CDAT y contractual) | 2105, 2110xx, 2125xx, 2130xx |
| `DepositInactivePrincipal` | pasivo | `LineNature`, `DepositClass` (la inactividad es el rol, no una marca) | 210510 |
| `InterestPayable` | pasivo | `LineNature`, `DepositClass` | 210595, 211095, 212595, 213095 |
| `InterestExpense` | gasto (resultado) | `LineNature`, `DepositClass` | 615005, 615010, 615015, 615020 |
| `WithholdingPayable` | pasivo | `LineNature` | 243535 |
| `GmfPayable` | pasivo | `LineNature`, `DepositClass` | 243005, 243010, 243015, 243020 |
| `BonusExpense` | gasto (resultado) | `DepositClass` | la que indique la contadora (P26) |
| `ChargeIncome` | ingreso (resultado) | concepto (código del cargo como `ReasonCode`) | la que indique la contadora |
| `ChargeReceivable` | activo | `LineNature` | ídem |
| `PenaltyIncome` | ingreso (resultado) | `DepositClass` | ídem |
| `ContributionCapital` | patrimonio | `LineNature = Contribution`, `ContributionClass` | 3105xx |
| `RevaluationFund` | patrimonio | — | la del fondo de revalorización |
| `SurplusFund` | patrimonio | motivo (fondo de origen como `ReasonCode`) | la de excedentes por distribuir |
| `RefundPayable` | pasivo | `LineNature` | la de devoluciones por pagar |
| `UnclaimedPayable` | pasivo | `LineNature` | la de no reclamados |
| `GarnishmentPayable` | pasivo | — | la de embargos por consignar |

Los roles de pasivo y patrimonio exigen `Operation` (como los de saldo de la 013); los de resultado admiten `*` con
peso 2. Los roles de crédito no cambian; el cruce al retiro y el débito automático usan los roles de crédito del lado
del crédito y los de esta tabla del lado de la cuenta, en un comprobante por unidad. Tipos de comprobante nuevos (R19):
`MA`, `CC`, `TD`, `AO`, `RA` en `ACC_LendingVoucherMappings` (semilla) y `voucher-types.json`; los tipos de hecho nuevos
entran a `LND_FactTypes` con su `ShortCode` y entrega (los nombra `contracts/contabilidad.md`).

### 1.7 `COR_BankFileFormats.Scope` (K7; FR-125, R17)

Sin cambio de esquema. Los grupos de archivo usan los ámbitos de descuento por pagaduría de K2
(`PayrollDeductionRequest` 5 y `PayrollDeductionResult` 6, `SeleccionPorReferencia`; 013 parte 3 §4.13). El nombre
`PaymasterDeduction` de research R17 designa ese par: **no** se agrega un tercer valor (research C01). Los
orígenes de campo `BankFieldSource` suman, en los ámbitos 5 y 6: `AccountNumber` 93, `AccountInstallmentPeriod` 94,
`PaymasterConceptCode` 95 **(nuevos en data-model)**, para la ida y para leer la respuesta por código.

### 1.8 Otras tablas de la 013 que suman columnas

| Tabla | Columna nueva | Tipo | Nulo | Regla | E |
|---|---|---|---|---|---|
| `LND_MigrationLoads` | `Nature` | int (`LineNature`) | no | defecto `Placement`; los tres UK de estado (`Draft`, `Applied`, `Activated`) pasan a `(Nature, Status)` con el mismo filtro: un borrador, una aplicada y una activada **por naturaleza** (FR-116) | K7 |
| `LND_MigrationLoads` | `AccountCount` | int | no | defecto 0 | K7 |
| `LND_Activations` | `Nature` | int (`LineNature`) | no | defecto `Placement`; UK `UK_LND_Activations_Nature (Nature)` filtrado vivos | K7 |
| `LND_ProcessRunErrors` | `MemberAccountId` | int FK `LND_MemberAccounts` | sí | exclusiva con `LoanId`; UK `(ProcessRunId, MemberAccountId, AttemptNumber)` filtrado no nulo; `Step` suma `Captaciones` | K7 |
| `LND_AccountingFactParts` | `TriggerAccountOperationId` · `AccountCount` | bigint FK `LND_MemberAccountOperations` · int | sí · no | `SourceKind` suma `AccountCatchUp` (**nuevo en data-model**); la causación de captaciones por lotes resumida por día (`CC`) lleva la lista por cuenta en el contenido | K7 |
| `LND_AttributionRules` | `MaxNegotiatedSpread` | `TasaAnual` | sí | criterio «puntos adicionales» de la tasa negociada (R13) | K9 |
| `COR_RegulatoryProfileVersions` | `MayTakeDeposits` · `DepositsFromAssociatesOnly` · `LiquidityFundCategory` · `DepositInsuranceEnrolled` · `IsGmfWithholdingAgent` | bit · bit · nvarchar(20) · bit · bit | no · no · sí · no · no | lo que FR-018 pide al perfil (defectos 0; categoría según `FONDO_LIQUIDEZ_*`) | K7 |

`LND_InstallmentNonDeductions` (013 §24.9) no cambia: el no descuento de cuotas de cuentas va en §3.7.

### 1.9 Enums existentes ampliados (al final; nunca se renumeran)

| Enum | Valores nuevos | Fuente |
|---|---|---|
| `IssuedDocumentKind` | 7–14 (research §0.3) y `GarnishmentResponse` 15 | §1.5 |
| `LendingNumberKind` | 10–14 | §1.3 |
| `LendingDecisionSubjects` (constantes de texto) | `DepositWithdrawal`, `NegotiatedRate`, `MembershipWithdrawal`, `DeceasedDelivery`, `ChargeWaiver` (research §0.3) y `TermDepositRedemption`, `ContributionRefund`, `AssemblyDistribution`, `GarnishmentRegistration` **(nuevos en data-model)** | FR-005, FR-051, FR-071, FR-079 |
| `LendingProcessKind` | ninguno (el ciclo diario suma un paso; R10) | — |
| `AccountingFactPartSource` | `AccountCatchUp` | §1.8 |
| `BankFieldSource` | 93–95 | §1.7 |
| `ClaseDeMedioDeRecaudo`, `IntegrationParticipantClass` | sin cambios (C3) | — |

---

## 2. Configuración de líneas

Las versiones son hechos de sólo inserción con la regla única de la 013 §0.6 (`ValidFrom` ≥ `HoyLocal` salvo
migración; `Lending.CreditLine.RetroactiveVersion`). Una versión nueva se registra **completa** con sus hijas en un solo
comando y nunca altera lo causado, liquidado o sellado (FR-007).

### 2.1 `LND_DepositLineVersions` — `DepositLineVersion` (K8, H; FR-007, FR-010, FR-028, FR-036, FR-038, FR-074)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `CreditLineId` | int FK `LND_CreditLines` | no | línea con `Nature = Deposit` |
| `VersionNumber` | int | no | UK `UK_LND_DepositLineVersions_Line_Version` |
| `ValidFrom` · `RegisteredAt` | date · datetime | no | §0.6 de la 013 |
| `Reason` | nvarchar(300) | no | |
| `DepositClass` | int (`DepositClass`) | no | **igual en todas las versiones de la línea** (`Lending.Savings.ClassChanged` **(nuevo en data-model)**) |
| **Elegibilidad** | | | |
| `RequiresAssociate` | bit | no | FR-030; forzado a 1 si el perfil regulatorio dice `DepositsFromAssociatesOnly` |
| `AllowsCoHolders` · `AllowsMinors` · `AllowsLegalEntities` | bit | no | FR-033 |
| `AllowsPartialWithdrawals` | bit | no | 0 en permanente (sólo al retiro) y en CDAT |
| `BranchPublicIdsJson` | nvarchar(max) | sí | oficinas que la ofrecen; nulo = todas |
| **Tasa** (FR-020, FR-028) | | | |
| `RateKind` | int (`RateKind`) | no | `Fixed` o `Variable` (013) |
| `AnnualRate` | `TasaAnual` | sí | EA fija o base sin tramos; obligatoria con `Fixed` y sin tramos |
| `IndexCode` · `IndexKey` · `Margin` · `RateFloor` · `RateCeiling` · `ResetPeriodicity` | nvarchar(20) · nvarchar(20) · `TasaAnual` ×3 · int (`RepaymentPeriodicity`) | sí | variable (`TasaDelDia.TasaVariable`) |
| `RateTierMode` | int (`RateTierMode`) | sí | obligatorio si hay tramos (§2.2); defecto `WholeBalance` (D8) |
| `DeclaresAgreedRate` | bit | no | 1 = la cuenta **sella** su tasa al abrir (FR-007); 0 = usa cada día la versión vigente |
| `AllowsNegotiatedRate` | bit | no | admite reglas de §2.4 (FR-127) |
| `KeepsNegotiatedMarginOnRollover` | bit | no | 0 por defecto (D34) |
| `RateChangeNoticeDays` | int | no | anticipación del aviso de cambio de tasa (FR-038); 0 en CDAT |
| **Cálculo** | | | |
| `BalanceMethod` | int (`BalanceMethod`) | no | defecto `DailyBalance` (D31); en CDAT siempre `DailyBalance` |
| `DayCount` · `RateEquivalence` | int (`DayCountConvention`) · int (`RateEquivalence`) | no | defecto `Actual365` y `Compound` (D7) |
| `RoundingUnit` · `RoundingMode` · `RoundingMoment` | `Monto` · int · int | no | defecto 1 peso, `AwayFromZero`, `PerTotal` (al liquidar, D9) |
| `MinOpeningAmount` · `MinBalance` · `MinBalanceForInterest` | `Monto` | no | mínimos para abrir, mantener (entra al disponible, FR-057) y ganar interés (bajo él, el día causa cero) |
| `SettlementPeriodicity` · `SettlementDay` | int (`RepaymentPeriodicity`) · tinyint | no · sí | periodicidad de liquidación (FR-061); en CDAT la da el plan; `SinglePayment` = al vencimiento |
| **Impuestos** | | | |
| `WithholdingRateParameterCode` | nvarchar(60) | no | clave del parámetro de tarifa de retención de la clase (§9: `RETENCION_TARIFA_AHORRO`, `…_CDAT`, `…_CONTRACTUAL`, `…_PERMANENTE`); la sugiere la clase |
| `IsGmfSubject` | bit | no | 1 en ahorro a la vista, contractual y permanente; el CDAT grava sólo en lo que diga `GMF_HECHOS_GENERADORES` |
| **Límites** (FR-051, FR-059) | | | |
| `MaxWithdrawalPerOperation` · `MaxWithdrawalPerDay` · `MaxCashPerOperation` · `MaxCashPerDay` | `Monto` | sí | nulo = sin límite de la línea |
| **Inactividad y suspensión** (FR-065, FR-084) | | | |
| `InactivityDays` | int | sí | defecto 360 (D21); nulo = no se inactiva |
| `AccruesWhileInactive` · `SuspendsAccrualOnTotalGarnishment` · `SuspendsAccrualOnDeceased` | bit | no | 1 · 0 · 0 por defecto |
| **CDAT** (sólo `DepositClass = TermDeposit`; FR-028, FR-067 a FR-073) | | | |
| `MinTermDays` · `MaxTermDays` | int | sí | rango del plazo |
| `InterestPeriodicities` | nvarchar(200) | sí | JSON con la lista de `RepaymentPeriodicity` admitidas (`SinglePayment` = al vencimiento) |
| `InterestTimings` | nvarchar(50) | sí | JSON con `InterestTiming` admitidos (`Arrears`, `Advance`) |
| `AutoRollover` · `MaxRollovers` | bit · int | sí | defecto 1 y sin máximo (nulo) (D17) |
| `RolloverRateRule` | int (`RolloverRateRule`) | sí | defecto `CurrentTableRate` (D17) |
| `CapitalizesOnRollover` | bit | sí | defecto 0 (D17); la retención va antes de capitalizar (FR-063) |
| `GraceBusinessDays` · `GraceAccrues` | int · bit | sí | defecto 3 y 0 (D17) |
| `AfterMaturityTreatment` | int (`AfterMaturityTreatment`) | sí | `StopAccruing` («vencido por pagar») o `MoveToDemand` |
| `MoveToDemandLineId` | int FK `LND_CreditLines` | sí | línea a la vista destino; obligatoria con `MoveToDemand` |
| `EarlyRedemptionAllowed` | bit | sí | defecto 0 (D18) |
| `EarlyRedemptionPenaltyMode` | int (`EarlyRedemptionPenaltyMode` **(nuevo en data-model)**) | sí | `PenaltyRate` 1 (reliquidar a la tasa de penalidad, defecto), `Discount` 2 (fracción del interés causado) |
| `PenaltyAnnualRate` · `PenaltyDiscountFraction` | `TasaAnual` · `Fraccion` | sí | según el modo |
| `MinDaysBeforeRedemption` | int | sí | |
| **Contractual** (sólo `Contractual`; FR-024, FR-074, FR-075) | | | |
| `InstallmentPeriodicity` · `MinInstallmentAmount` | int (`RepaymentPeriodicity`) · `Monto` | sí | |
| `MinTermMonths` · `MaxTermMonths` | int | sí | |
| `BonusAnnualRate` | `TasaAnual` | sí | tasa bonificada por cumplimiento; la bonificación es la diferencia contra la base |
| `BreachAnnualRate` | `TasaAnual` | sí | tasa castigo |
| `ToleratedLateInstallments` | int | sí | tolerancia de atrasos |
| `BreachEffect` | int (`ContractualBreachEffect` **(nuevo en data-model)**) | sí | `BreachRate` 1, `MoveToDemand` 2, `Cancel` 3 |
| `BonusRecognition` | int (`BonusRecognition` **(nuevo en data-model)**) | sí | `AtFulfillment` 1 (defecto), `Provisioned` 2 (P26, a validar) |
| **Permanente** (sólo `Permanent`) | | | |
| `WithdrawableOnlyOnMembershipWithdrawal` | bit | sí | 1 por defecto |
| `ContentSha256` | nvarchar(64) | no | huella canónica con sus hijas; la sella la cuenta |

**Claves**. UK `(CreditLineId, VersionNumber)`; IX `(CreditLineId, ValidFrom, VersionNumber)`.

**Reglas** (handler y validador):
- Las columnas de una clase sólo se llenan en esa clase; las de CDAT son obligatorias en CDAT
  (`Lending.TermDeposit.LineIncomplete` **(nuevo en data-model)**).
- `MinTermDays ≤ MaxTermDays`; defectos dentro de sus listas; `RateFloor ≤ RateCeiling`.
- **Nunca** pasa por `ControlDeUsura` (`LasCaptacionesNoUsanLaUsura`).
- Una tasa con más de 6 decimales: `Lending.Rate.TooPrecise`.
- Un cambio de tasa en una línea a la vista sin tasa pactada genera el aviso a los titulares con
  `RateChangeNoticeDays` (FR-038): `ValidFrom` ≥ hoy + `RateChangeNoticeDays`
  (`Lending.Savings.RateChangeNoticeTooShort` **(nuevo en data-model)**).

**H**: sí, con sus hijas §2.2 y §2.3. **Escritor**: `CreateDepositLineVersionCommand` (`Lending.DepositLines.Manage`).

### 2.2 `LND_DepositLineRateTiers` — `DepositLineRateTier` (K8, H; FR-020, FR-028)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `DepositLineVersionId` | int FK | no | |
| `Order` | int | no | UK `UK_LND_DepositLineRateTiers_Version_Order` |
| `MinBalance` · `MaxBalance` | `Monto` | sí | tramo por saldo (a la vista, contractual, permanente) |
| `MinTermDays` · `MaxTermDays` | int | sí | CDAT |
| `MinAmount` · `MaxAmount` | `Monto` | sí | CDAT, por monto constituido |
| `AnnualRate` | `TasaAnual` | sí | EA con `Fixed` |
| `Margin` | `TasaAnual` | sí | con `Variable` |

**Reglas**. Exactamente una de `AnnualRate`/`Margin` según el `RateKind` de la versión. Los tramos de saldo de una
versión son contiguos y no se cruzan (`Lending.Savings.RateTiersOverlap` **(nuevo en data-model)**). Con
`WholeBalance` todo el saldo va a la tasa del tramo donde cae; con `Stepped`, cada porción a la tasa de su tramo
(`InteresDeCaptacion`). En CDAT gana el primer tramo por `Order` que casa plazo y monto; sin tramo, la tasa de la
versión. **H**: sí.

### 2.3 `LND_DepositLineCharges` — `DepositLineCharge` (K8, H; FR-128, D35)

Catálogo de cargos de la versión. **Apagado por defecto**: la semilla no crea filas y una fila nace con
`IsEnabled = 0`.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `DepositLineVersionId` | int FK | no | |
| `Code` | nvarchar(10) | no | `CodigoDeCatalogo`; UK `UK_LND_DepositLineCharges_Version_Code`; estable entre versiones (es el `ReasonCode` de `ChargeIncome`) |
| `Name` | nvarchar(120) | no | |
| `Kind` | int (`ChargeKind`) | no | |
| `IsEnabled` | bit | no | 0 por defecto |
| `Trigger` | int (`ChargeTrigger` **(nuevo en data-model)**) | no | `Periodic` 1, `WithdrawalOverFree` 2, `DepositOverFree` 3, `ExtraDocument` 4, `MediumReplacement` 5, `Inactivity` 6, `Manual` 7 |
| `Periodicity` | int (`RepaymentPeriodicity`) | sí | obligatoria con `Periodic` e `Inactivity` |
| `FreeOperationsPerPeriod` · `FreePeriod` | int · int (`RepaymentPeriodicity`) | sí | gratuitos por cuenta y período (D35) |
| `CalculationMode` | int (`ChargeCalculation` **(nuevo en data-model)**) | no | `FixedAmount` 1, `Percentage` 2 |
| `FixedAmount` | `Monto` | sí | |
| `Percentage` · `PercentageBase` | `Fraccion` · int (`ChargeBase` **(nuevo en data-model)**) | sí | `OperationAmount` 1, `AverageBalance` 2 |
| `MinAmount` · `MaxAmount` | `Monto` | sí | |
| `ExemptIfAverageBalanceAtLeast` · `ExemptIfMinimumBalanceAtLeast` | `Monto` | sí | exoneraciones combinables (cualquiera exonera) |
| `ExemptIfSeniorityMonthsAtLeast` | int | sí | antigüedad como asociado |
| `ExemptEmployees` · `ExemptGmfExemptAccount` · `ExemptMinors` | bit | no | |
| `ExemptBranchPublicIdsJson` | nvarchar(max) | sí | |
| `ShortfallPolicy` | int (`ChargeShortfallPolicy` **(nuevo en data-model)**) | no | `KeepPending` 1 (defecto: queda en `ChargeReceivable` y se cobra con la siguiente consignación), `Waive` 2 |
| `IsVatTaxed` | bit | no | gravado con IVA: la Cartera emite `CargoDeCaptacionCausadoV1` y Comercial factura (R14) |
| `CommercialItemCode` | nvarchar(30) | sí | código del servicio en Comercial; obligatorio con `IsVatTaxed` |

**Reglas**. Un cargo nunca deja la cuenta bajo el mínimo (FR-128). La exoneración individual aprobada vive en §4.9.
**H**: sí.

### 2.4 `LND_NegotiatedRateRules` — `NegotiatedRateRule` (K9, H; FR-127, D34, R13)

Reglas de tasa negociada **por línea** con vigencia propia (versionadas por `Code`), independientes de la versión de
la línea.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `CreditLineId` | int FK `LND_CreditLines` | no | línea `Deposit` con `AllowsNegotiatedRate` vigente |
| `Code` | nvarchar(10) | no | identidad de la regla |
| `VersionNumber` | int | no | UK `UK_LND_NegotiatedRateRules_Line_Code_Version (CreditLineId, Code, VersionNumber)` |
| `ValidFrom` · `RegisteredAt` | date · datetime | no | §0.6 de la 013 |
| `IsActive` | bit | no | una versión con 0 termina la regla |
| `Priority` | int | no | desempate entre reglas igual de específicas |
| `MinSeniorityMonths` | int | sí | antigüedad del asociado |
| `MinAmount` · `MaxAmount` | `Monto` | sí | |
| `MinTermDays` · `MaxTermDays` | int | sí | |
| `MinBaseRate` · `MaxBaseRate` | `TasaAnual` | sí | tasa de la tabla |
| `MinHolderTotalBalance` · `MaxHolderTotalBalance` | `Monto` | sí | saldo total del titular en captaciones y aportes |
| `BranchId` | int FK `COR_Branches` | sí | |
| `Channel` | nvarchar(20) | sí | canal (`X-Canal`) |
| `MaxSpread` | `TasaAnual` | no | margen máximo sobre la tabla en puntos EA (fracción; > 0) |
| `Reason` | nvarchar(300) | no | |

**Reglas**. `TasaNegociada` elige la regla vigente más específica (más criterios no nulos que casan), luego `Priority`.
El margen pedido nunca supera `MaxSpread` (`Lending.TermDeposit.SpreadAboveRule` **(nuevo en data-model)**); dentro de
la atribución del asesor (matriz con `MaxNegotiatedSpread` y monto, §1.8) se aplica; por encima, decisión con
`LendingDecisionSubjects.NegotiatedRate`. Lo sellado va en `LND_MemberAccountTermsVersions` (§3.3). **H**: sí.
**Escritor**: `Lending.DepositLines.Manage`.

### 2.5 `LND_ContributionLineVersions` — `ContributionLineVersion` (K7, H; FR-029, FR-040 a FR-049)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `CreditLineId` | int FK `LND_CreditLines` | no | línea con `Nature = Contribution` |
| `VersionNumber` | int | no | UK `UK_LND_ContributionLineVersions_Line_Version` |
| `ValidFrom` · `RegisteredAt` | date · datetime | no | |
| `Reason` | nvarchar(300) | no | |
| **Mínimo no reducible** (FR-042) | | | |
| `MinimumMode` | int (`ContributionAmountMode` **(nuevo en data-model)**) | no | `FixedAmount` 1, `IncomeFraction` 2, `MinimumWageFraction` 3 |
| `MinimumAmount` · `MinimumFraction` | `Monto` · `Fraccion` | sí | según el modo; con `MinimumWageFraction` se multiplica por `SMMLV` vigente |
| **Cuota periódica** (FR-040) | | | |
| `InstallmentMode` | int (`ContributionAmountMode`) | no | |
| `InstallmentAmount` · `InstallmentFraction` | `Monto` · `Fraccion` | sí | ingreso registrado de la persona (Personas) o `SMMLV` |
| `InstallmentRoundingUnit` · `InstallmentRoundingMode` | `Monto` · int (`RoundingMode`) | no | |
| `InstallmentPeriodicity` · `InstallmentDueDay` | int (`RepaymentPeriodicity`) · tinyint | no · sí | |
| `MinimumFirst` | bit | no | 1 = lo pagado completa el mínimo antes de ir a ordinarios |
| **Reparto con ahorro permanente** (FR-049, D3) | | | |
| `PermanentSavingsLineId` | int FK `LND_CreditLines` | sí | línea `Deposit` de clase `Permanent` |
| `PermanentSavingsFraction` | `Fraccion` | sí | parte de la cuota que va a ahorro permanente; la parte de aportes ≥ `FRACCION_MINIMA_APORTES` (`Lending.Contributions.FractionBelowLegal` **(nuevo en data-model)**) |
| **Extraordinarios y límites** | | | |
| `AllowsExtraordinary` | bit | no | FR-041 |
| `MaxParticipationFraction` | `Fraccion` | sí | límite propio, nunca menos estricto que `LIMITE_PARTICIPACION_APORTES_PN/PJ` (FR-043) |
| `ExcessAction` | int (`ExcessContributionAction` **(nuevo en data-model)**) | no | `Reject` 1, `RefundByDecision` 2 |
| `AllowsTransferBetweenMembers` | bit | no | FR-048, sólo si los estatutos lo permiten |
| **Devolución** | | | |
| `RefundTermDays` | int | no | plazo estatutario de devolución (FR-079) |
| `LossDeductionEnabled` | bit | no | 0 por defecto (D15, FR-078) |
| `ContentSha256` | nvarchar(64) | no | |

**Reglas**. Los aportes no causan intereses: la versión **no tiene tasa** (R02). La cuota obligatoria total (aportes +
permanente) de un fondo de empleados no supera `TOPE_CUOTA_FONDO_EMPLEADOS_PCT` del ingreso
(`Lending.Contributions.InstallmentAboveCap` **(nuevo en data-model)**); lo decide el perfil regulatorio
(`EmployeeFundCategory`). **H**: sí. **Escritor**: `CreateContributionLineVersionCommand`
(`Lending.ContributionLines.Manage`).

---

## 3. Cuentas

### 3.1 `LND_MemberAccounts` — `MemberAccount` (K7; FR-001, FR-019, FR-030 a FR-039, FR-056, FR-084, R02)

La cuenta del asociado: ahorro a la vista, contractual, permanente, CDAT o aportes. Documento con estado (mutable en
las columnas que dice cada transición); nunca se borra: se cancela (I-3).

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `Number` | nvarchar(20) | no | §0.4; UK `UK_LND_MemberAccounts_Number` sin filtro |
| `CreditLineId` | int FK `LND_CreditLines` | no | línea `Deposit` o `Contribution` |
| `Nature` | int (`LineNature`) | no | copia de la línea: `Deposit` o `Contribution` (`CK_LND_MemberAccounts_Nature`: ≠ `Placement`) |
| `DepositClass` | int (`DepositClass`) | sí | obligatoria en `Deposit`, nula en `Contribution` (`CK_LND_MemberAccounts_Class`) |
| `HolderPersonId` | int FK `COR_People` | no | **titular**; inmutable. Un menor es el titular (FR-033) |
| `BranchId` | int FK `COR_Branches` | no | oficina de la cuenta (alcance, dimensión contable) |
| `HandlingMode` | int (`HandlingMode`) | no | `Indistinct` (defecto) o `Joint` |
| `Status` | int (`MemberAccountStatus`) | no | ver transiciones |
| `StatusChangedAt` | datetime | no | |
| `Origin` | int (`MemberAccountOrigin` **(nuevo en data-model)**) | no | `Opening` 1, `Migration` 2 |
| `OpenedOn` · `OpenedByUserId` | date · int FK `SEC_Users` | no · sí | nulo en migración (proceso) |
| `ActivatedOn` | date | sí | día en que pasó a `Active` |
| `InactiveSince` | date | sí | con `Status = Inactive` |
| `MaturedOn` | date | sí | CDAT o contractual vencido sin prórroga |
| `ClosingRequestedAt` · `ClosedOn` · `ClosedByUserId` · `CloseReason` | datetime · date · int FK · nvarchar(300) | sí | |
| `IsGarnished` | bit | no | marca de dimensión contable y de pantalla; la escribe sólo el registro de embargos (§5.2) en la misma transacción que crea o libera la retención |
| `RequiresDataUpdate` · `DataUpdateRequiredSince` | bit · date | no · sí | mayoría de edad del titular (FR-033, R22) o reingreso; se limpia al actualizar el conocimiento y la autorización de datos |
| `ContractualStatus` | int (`ContractualStatus` **(nuevo en data-model)**) | sí | sólo contractual: `Current` 1, `Breached` 2, `Fulfilled` 3 |
| `TechnicalNegativeSince` | date | sí | negativo técnico por reverso (FR-056); bloquea retiros y alerta |
| `AccrualSuspendedSince` · `AccrualSuspensionReason` | date · nvarchar(40) | sí | FR-065: `TotalGarnishment`, `Deceased` |
| `SourceAccountNumber` | nvarchar(30) | sí | número de origen en la migración (búsqueda; no identifica) |
| `MigrationLoadId` | int FK `LND_MigrationLoads` | sí | |

**Claves e índices**.
- UK `UK_LND_MemberAccounts_LiveContribution (HolderPersonId)` filtrado `[Nature] = 3 AND [Status] <> 6 AND
  [IsDeleted] = 0`: **una cuenta de aportes viva por persona**. El reingreso abre otra (FR-085).
- UK `UK_LND_MemberAccounts_LivePermanent (HolderPersonId, CreditLineId)` filtrado `[DepositClass] = 3 AND [Status] <> 6
  AND [IsDeleted] = 0` **(nuevo en data-model)**: un ahorro permanente vivo por persona y línea.
- IX `(HolderPersonId, Status)`, `(CreditLineId, Status)`, `(BranchId, Status)`, `(SourceAccountNumber)` filtrado no nulo.

**Estados y transiciones** (`MemberAccountStatus`):

```text
                 apertura completa (habeas data, conocimiento, listas, firmas, mínimo)
  Opening ────────────────────────────────────────────────▶ Active
     │ desiste antes del primer movimiento                    │  ▲
     ▼                                                        │  │ movimiento del titular (FR-084)
  Closed ◀──────────── Closing ◀─── cancelación ──────────────┤  │
     ▲  pago o traslado      ▲  (FR-039: sin bloqueos,         ▼  │
     │  del saldo            │   embargos ni canje)        Inactive  (proceso, InactivityDays)
     │                       │
     │                       └──────────── Matured ◀── vencimiento sin prórroga ni instrucción
     │                                       (CDAT con StopAccruing; contractual cumplido o vencido)
     └── retiro del asociado (cuenta de aportes, §6.1), entrega por fallecimiento, traslado a no reclamados
```

- `Opening → Active`: `OpenMemberAccountCommand` con todos los requisitos (FR-006, FR-032); en aportes, con la persona
  como asociado vigente (FR-030). La consignación inicial puede ir en la misma operación.
- `Active ↔ Inactive`: el paso diario (inactividad) y cualquier transacción cuyo tipo cuente como «movimiento del
  titular» (FR-058: no cuentan `InterestAccrued`, `InterestCredited`, `ChargeApplied`, `Reclassification`). Emite el
  hecho de reclasificación por inactividad.
- `Active → Matured`: CDAT tras la gracia sin prórroga con `StopAccruing`; contractual al vencer sin instrucción. Con
  `MoveToDemand`, en vez de `Matured` el saldo se traslada y la cuenta pasa a `Closed`.
- `* → Closing → Closed`: cancelación (FR-039), retiro, entrega, no reclamados. `Closing` existe mientras hay una orden
  de giro en curso (§3.9); una orden rechazada devuelve a `Active` y reintegra.
- `Closed` es final. Una cuenta `Closed` no recibe operaciones (`Lending.Savings.AccountClosed` **(nuevo en
  data-model)**).

**Escritor**: los comandos de apertura y cierre, por `RegistroDeCuentas` cuando hay operación. **H**: no.

### 3.2 `LND_MemberAccountParties` — `MemberAccountParty` (K7; FR-005, FR-033, FR-034, I-1)

Estado con fechas (no versión): una fila por persona, rol y vigencia; cerrar es escribir `ValidTo` con un comando
auditado.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `PersonId` | int FK `COR_People` | no | toda parte es persona del maestro (Principio V) |
| `Role` | int (`MemberAccountPartyRole`) | no | `Holder`, `CoHolder`, `Signatory`, `LegalRepresentative`, `Beneficiary` |
| `Share` | `Fraccion` | sí | titular y cotitulares: proporción (Σ = 1 entre los vigentes; la usa el certificado tributario, FR-104); beneficiarios: porcentaje (Σ = 1 por conjunto) |
| `SignatureLimit` | `Monto` | sí | firmantes: límite por operación; nulo = sin límite |
| `RepresentationBasis` | nvarchar(30) | sí | `LegalRepresentative`: `Parent`, `Guardian`, `CourtOrder`, `LegalEntityRepresentative` |
| `RepresentationSupportAttachmentPublicId` | uniqueidentifier | sí | registro civil, decisión judicial o certificado de existencia; **obligatorio** con `LegalRepresentative` |
| `SignatureSpecimenAttachmentPublicId` · `PhotoAttachmentPublicId` | uniqueidentifier | sí | firma y foto como adjuntos (Q2) |
| `BeneficiarySetNumber` | int | sí | conjunto de beneficiarios al que pertenece la fila; cambiar beneficiarios cierra el conjunto vigente y abre el siguiente |
| `BeneficiaryRecordAttachmentPublicId` | uniqueidentifier | sí | constancia firmada por el titular (FR-034) |
| `ValidFrom` · `ValidTo` | date | no · sí | |
| `EndReason` | nvarchar(300) | sí | mayoría de edad, revocación, fallecimiento, cambio de beneficiarios |
| `EndedByUserId` | int FK | sí | nulo = proceso |

**Claves**. UK `UK_LND_MemberAccountParties_Holder (MemberAccountId)` filtrado `[Role] = 1 AND [ValidTo] IS NULL AND
[IsDeleted] = 0` (un titular vigente, que es `MemberAccounts.HolderPersonId`); UK `(MemberAccountId, PersonId, Role)`
filtrado `[ValidTo] IS NULL AND [IsDeleted] = 0`; IX `(PersonId, Role)`.

**Reglas**.
- Cotitulares sólo si la línea los admite; Σ `Share` de titular y cotitulares vigentes = 1
  (`Lending.Savings.SharesDoNotAddUp` **(nuevo en data-model)**). Beneficiarios: Σ = 1 por conjunto (FR-034).
- **Menor de edad**: titular menor ⇒ exactamente un `LegalRepresentative` vigente con soporte; mientras sea menor, sólo
  el representante firma retiros y traslados (`Lending.Savings.MinorRequiresRepresentative` **(nuevo en data-model)**).
  Al cumplir `MAYORIA_DE_EDAD_ANIOS`, el proceso cierra la vigencia del representante (`EndReason` «mayoría de edad»),
  marca la cuenta `RequiresDataUpdate` y anula los retiros del representante en espera (R22).
- Cuenta en aportes: sólo `Holder` y `Beneficiary`.
- En el fallecimiento cuenta el conjunto de beneficiarios **vigente en la fecha de defunción** (§6.5).
- Cambiar firmantes o beneficiarios exige `Lending.Accounts.Parties.Manage`; FR-005 compara por persona.

**H**: no. **Escritor**: comandos de partes (`Lending.Accounts.Parties.Manage`).

### 3.3 `LND_MemberAccountTermsVersions` — `MemberAccountTermsVersion` (K9, H; FR-007, FR-023, FR-067, FR-070, FR-074, FR-127, R11)

Condiciones **selladas** de un CDAT, de un contractual o de una cuenta con tasa pactada o negociada. Patrón de
`LoanTermsVersion`. Las cuentas a la vista y permanentes sin tasa pactada no tienen filas: leen la versión vigente de la
línea cada día.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `VersionNumber` | int | no | UK `UK_LND_MemberAccountTermsVersions_Account_Version`; sufijo del título |
| `OperationId` | bigint FK `LND_MemberAccountOperations` | no | la operación que la produjo |
| `EffectiveValueDate` · `RegisteredAt` | date · datetime | no | |
| `Cause` | int (`AccountTermsCause` **(nuevo en data-model)**) | no | `Constitution` 1, `Rollover` 2, `Renewal` 3, `Migration` 4, `ContractualSetup` 5, `ContractualBreach` 6, `AgreedRate` 7, `RateNegotiation` 8 |
| `DepositLineVersionId` | int FK `LND_DepositLineVersions` | no | la versión copiada |
| `RulesVersion` · `InputsSha256` | nvarchar(20) · nvarchar(64) | no | |
| **Monto, plazo y fechas** | | | |
| `PrincipalAmount` | `Monto` | no | capital al iniciar (con lo capitalizado en una prórroga) |
| `TermDays` · `TermMonths` | int | sí | CDAT en días; contractual en meses |
| `StartDate` · `MaturityDate` · `PaymentDate` | date | sí | `PaymentDate` = el hábil siguiente si vence en no hábil, sin interés adicional (D19) |
| `IssueTermBand` | int (`IssueTermBand`) | sí | CDAT y contractual; dimensión contable (FR-073) |
| **Tasa** | | | |
| `TableAnnualRate` | `TasaAnual` | no | tasa de la tabla (versión o tramo) |
| `NegotiatedSpread` | `TasaAnual` | sí | margen aprobado; nulo = sin negociación |
| `AppliedAnnualRate` | `TasaAnual` | no | = tabla + margen |
| `NegotiatedRateRuleId` | int FK `LND_NegotiatedRateRules` | sí | la versión de la regla aplicada |
| `NegotiationDecisionId` | int FK `LND_CreditDecisions` | sí | decisión por encima de la atribución del asesor |
| `NegotiationApprovedByUserId` · `NegotiationReason` | int FK · nvarchar(300) | sí | quién aprobó y por qué (FR-127) |
| `PeriodRate` | `TasaEquivalente` | sí | `i_p` sellada del período del plan |
| **Intereses** | | | |
| `InterestPeriodicity` · `InterestTiming` | int (`RepaymentPeriodicity`) · int (`InterestTiming`) | sí | CDAT |
| `DayCount` · `RateEquivalence` · `RoundingUnit` · `RoundingMode` · `RoundingMoment` | int · int · `Monto` · int · int | no | |
| `InterestDestination` | int (`InterestDestination` **(nuevo en data-model)**) | sí | `ToAccount` 1, `PaymentOrder` 2, `Capitalize` 3 (sólo contractual y prórroga) |
| `DestinationAccountId` | int FK `LND_MemberAccounts` | sí | con `ToAccount` |
| `DestinationPersonId` · `DestinationBankId` · `DestinationAccountType` · `DestinationBankAccountNumber` | int FK `COR_People` · int FK `COR_Banks` · tinyint · nvarchar(25) | sí | con `PaymentOrder` (tercero admitido, FR-091) |
| **Vencimiento** | | | |
| `MaturityInstruction` | int (`MaturityInstruction` **(nuevo en data-model)**) | sí | `Rollover` 1, `RenewWithNewTerms` 2, `CancelAndPay` 3 |
| `AutoRollover` · `MaxRollovers` · `RolloverCount` | bit · int · int | sí · sí · no | `RolloverCount` = prórrogas acumuladas hasta esta versión |
| `RolloverRateRule` · `KeepsNegotiatedMargin` · `CapitalizesOnRollover` | int · bit · bit | sí | sellados de la línea |
| `GraceBusinessDays` · `GraceAccrues` · `AfterMaturityTreatment` | int · bit · int | sí | |
| `EarlyRedemptionAllowed` · `EarlyRedemptionPenaltyMode` · `PenaltyAnnualRate` · `PenaltyDiscountFraction` | bit · int · `TasaAnual` · `Fraccion` | sí | |
| **Contractual** | | | |
| `InstallmentAmount` · `InstallmentPeriodicity` · `InstallmentCount` | `Monto` · int · int | sí | |
| `BonusAnnualRate` · `BreachAnnualRate` · `ToleratedLateInstallments` · `BreachEffect` · `BonusRecognition` | `TasaAnual` ×2 · int · int · int | sí | |
| `ProjectedFinalAmount` | `Monto` | sí | valor final proyectado (FR-024) |
| `PreviousVersionId` | int FK self | sí | cadena de prórrogas y renovaciones |

**Reglas**. Nace con la constitución, cada prórroga o renovación (FR-070), la migración, la apertura de un contractual,
su incumplimiento y la negociación de tasa en una cuenta que la admite. La prórroga aplica `RolloverRateRule` y
conserva el margen sólo si `KeepsNegotiatedMargin`. Una renovación puede cambiar monto, plazo y tasa; si el plazo cambia
`IssueTermBand`, la operación emite el hecho de reclasificación (FR-073). Una tasa con más de 6 decimales:
`Lending.Rate.TooPrecise`. Sin tasa vigente para el plazo o con `RolloverCount = MaxRollovers`, no hay prórroga: rige
`AfterMaturityTreatment` (FR-070). **H**: sí. **Escritor**: `RegistroDeCuentas`.

### 3.4 `LND_TermDepositSchedules` — `TermDepositScheduleItem` (K9, H; FR-023, FR-069)

Plan de intereses del CDAT por versión de condiciones. Proyección inmutable: la autoridad es `PlanDeCdat`.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `TermsVersionId` | int FK `LND_MemberAccountTermsVersions` | no | |
| `PeriodNumber` | int | no | UK `UK_LND_TermDepositSchedules_Terms_Period` |
| `PeriodStart` · `PeriodEnd` · `PaymentDate` | date | no | `PaymentDate` hábil (D19) |
| `Days` | int | no | según la convención |
| `Interest` | `Monto` | no | interés del período (vencido o anticipado, `i_a = i_v / (1 + i_v)`) redondeado |
| `IsLastPeriod` | bit | no | el último absorbe el redondeo contra el plan |

**Reglas**. Σ `Interest` = interés total del plan al centavo; la causación diaria de cada período suma exactamente su
`Interest` (el último día absorbe la diferencia; R11). Una prórroga o renovación genera el plan de la versión nueva; el
anterior queda. No lleva estado de pago: pagado o pendiente sale del libro (`InterestPaid` con `SchedulePeriodNumber`).
**H**: sí.

### 3.5 `LND_AccountInstallments` — `AccountInstallment` (K7, H, `AuditableEntityLong`; FR-040, FR-087, FR-089, D12)

Cuotas exigibles de aportes, ahorro permanente y contractual. No es crédito: sin mora, calificación ni deterioro.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `PeriodYear` · `PeriodMonth` · `SubPeriodNumber` | smallint · tinyint · tinyint | no | período de la cuota (quincenal, decadal, semanal: `SubPeriodNumber`) |
| `DueDate` | date | no | |
| `Amount` | `Monto` | no | > 0 |
| `ContributionShare` · `PermanentSavingsShare` | `Monto` | sí | reparto de una cuota combinada (la de aportes lleva la parte del permanente, FR-040); la parte del permanente se exige en la cuenta permanente con `LinkedInstallmentId` |
| `LinkedInstallmentId` | bigint FK self | sí | cuota hermana del ahorro permanente |
| `GeneratedByProcessRunId` · `GeneratedByOperationId` | int FK `LND_ProcessRuns` · bigint FK | sí | exactamente uno (proceso o apertura/migración) |
| `RulesVersion` · `InputsSha256` | nvarchar(20) · nvarchar(64) | no | |
| `CancelledAt` · `CancelReason` · `CancelledByOperationId` | datetime · nvarchar(300) · bigint FK | sí | **`[EscrituraUnica]`**: cuotas futuras anuladas por retiro, cancelación o cambio de cuota |

**Claves**. UK `UK_LND_AccountInstallments_Account_Period (MemberAccountId, PeriodYear, PeriodMonth, SubPeriodNumber)`;
IX `(MemberAccountId, DueDate)`.

**Reglas**. Lo pagado de una cuota = Σ de las transacciones con su `InstallmentId`; su estado (`Pending`,
`PartiallyPaid`, `Paid`, `Cancelled`) **se calcula al leer**, como la cuota del crédito. El proceso las genera por
período con la versión de línea vigente (o las condiciones selladas del contractual) y es idempotente por el UK.
`ListarExigiblesAsync` devuelve las no pagadas con período, vencimiento y saldo. **H**: sí.

### 3.6 `LND_DirectDebitMandates` — `DirectDebitMandate` (K8; FR-090, D25, R16)

Estado con fechas.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | cuenta de ahorro a la vista o contractual que se debita |
| `AuthorizedByPersonId` | int FK `COR_People` | no | titular (o representante) que autoriza |
| `Scope` | int (`DirectDebitScope` **(nuevo en data-model)**) | no | `SingleLoan` 1, `AllLoansOfHolder` 2 |
| `LoanId` | int FK `LND_Loans` | sí | obligatorio con `SingleLoan` |
| `AllowsPartialDebit` | bit | no | sin disponible: 0 = no debita (FR-090) |
| `AuthorizationAttachmentPublicId` | uniqueidentifier | no | soporte firmado |
| `ValidFrom` · `ValidTo` | date | no · sí | |
| `RevokedAt` · `RevokedByUserId` · `RevokeReason` | datetime · int FK · nvarchar(300) | sí | |

UK `(MemberAccountId, LoanId)` filtrado `[ValidTo] IS NULL AND [LoanId] IS NOT NULL AND [IsDeleted] = 0`. El paso diario,
en la fecha exigible, crea una operación `DirectDebit` (§4.1) con clave `CAR:debito-automatico:{loanPublicId:N}:{fecha}`
y aplica el pago por el contrato de obligaciones con medio `InternalOffset`, en una unidad (cerrojos crédito → cuenta).
**H**: no. **Escritor**: `Lending.DirectDebits.Manage`.

### 3.7 `LND_AccountInstallmentNonDeductions` — `AccountInstallmentNonDeduction` **(nuevo en data-model)** (K7, H; FR-089, FR-125)

Lo no descontado por nómina o pagaduría queda exigible **con su motivo** (FR-089). Gemela de
`LND_InstallmentNonDeductions` para cuotas de cuentas.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `InstallmentId` | bigint FK `LND_AccountInstallments` | no | |
| `Origin` | int (`NonDeductionOrigin`) | no | `Contract` (Nómina), `PaymasterResult` (pagaduría) |
| `Reason` | int (`NonDeductionReason`) | no | |
| `RequestedAmount` · `DeductedAmount` | `Monto` | no | |
| `CollectorCode` · `ReferenceKey` | nvarchar(20) · nvarchar(150) | no | quién informó y su clave |
| `OccurredOn` | date | no | |

UK `(InstallmentId, CollectorCode, ReferenceKey)`. **H**: sí.

### 3.8 `LND_GmfExemptAccounts` — `GmfExemptAccount` (K8; FR-022, FR-036, D32, R07)

Cuenta marcada exenta de GMF, con vigencia. Sólo aplica con la política `SingleMarkedAccount`.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `PersonId` | int FK `COR_People` | no | titular |
| `MemberAccountId` | int FK | no | cuenta de ahorro (no CDAT, no aportes) del titular |
| `ValidFrom` · `ValidTo` | date | no · sí | |
| `MarkedByUserId` · `MarkReason` | int FK · nvarchar(300) | sí · no | nulo en migración |
| `EndedByUserId` · `EndReason` | int FK · nvarchar(300) | sí | |

**Claves**. UK `UK_LND_GmfExemptAccounts_LivePerson (PersonId)` filtrado `[ValidTo] IS NULL AND [IsDeleted] = 0`:
**una sola cuenta exenta vigente por persona**; marcar otra se rechaza con `Lending.Gmf.AlreadyMarked` nombrando la
vigente. Cambiar la marca cierra la vigente y abre la nueva en el mismo comando. IX `(MemberAccountId)`. **H**: no.
**Escritor**: `Lending.GmfExemptions.Manage`.

### 3.9 `LND_AccountPayoutOrders` — `AccountPayoutOrder` **(nuevo en data-model)** (K7; FR-079, FR-083, FR-091)

Orden de giro a Tesorería de un retiro por transferencia, una devolución, una entrega, intereses de CDAT a terceros o
una consignación a juzgado. Mismo mensaje que el desembolso (`OrdenDeGiroEmitidaV1`) y mismos estados
(`DisbursementOrderStatus`); tabla propia porque `LND_DisbursementOrders` exige crédito (punto a reconciliar, §10).

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `OperationId` | bigint FK `LND_MemberAccountOperations` | no | UK |
| `Number` | nvarchar(20) | no | numerador `DisbursementOrder` (013) |
| `Purpose` | int (`PayoutPurpose` **(nuevo en data-model)**) | no | `Withdrawal` 1, `ContributionRefund` 2, `DeceasedDelivery` 3, `TermDepositInterest` 4, `TermDepositRedemption` 5, `GarnishmentConsignment` 6, `Cancellation` 7 |
| `BeneficiaryPersonId` · `BeneficiaryTaxIdType` · `BeneficiaryTaxId` · `BeneficiaryName` | int FK · nvarchar(10) · nvarchar(20) · nvarchar(200) | no | copia sellada |
| `DestinationBankId` · `DestinationAccountType` · `DestinationAccountNumber` | int FK `COR_Banks` · tinyint · nvarchar(25) | sí | |
| `Amount` | `Monto` | no | > 0 |
| `Status` · `StatusChangedAt` | int (`DisbursementOrderStatus`) · datetime | no · sí | |
| `IssuedAt` · `IssuedByUserId` · `ResultDueOn` | datetime · int FK · date | no · sí · no | |
| `GarnishmentId` | int FK `LND_Garnishments` | sí | con `GarnishmentConsignment` |
| `MembershipWithdrawalId` · `DeceasedHolderCaseId` | int FK | sí | |

**Reglas**. Una orden rechazada o anulada **reintegra** el saldo con una operación de reverso enlazada (FR-091) y la
cuenta vuelve de `Closing` a su estado anterior. **H**: no (documento con estado, como la orden de la 013).

---

## 4. Libro

### 4.1 `LND_MemberAccountOperations` — `MemberAccountOperation` (K7, H, `AuditableEntityLong`; FR-001, FR-002, FR-009, FR-050 a FR-059, FR-086 a FR-092, R03)

El hecho primario —lo que decidió una persona, un recaudador o el proceso— y el comprobante de la operación. Una
operación toca una cuenta principal y, en traslados, una contraparte; sus filas de libro llevan cada una su cuenta.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | cuenta principal |
| `CounterpartMemberAccountId` | int FK `LND_MemberAccounts` | sí | traslado, cruce de aportes a ahorro, destino de intereses |
| `Kind` | int (`MemberAccountOperationKind` **(nuevo en data-model)**) | no | ver tabla |
| `Number` | nvarchar(20) | no | numerador `MemberAccountOperation`; UK |
| `Sequence` | int | no | por cuenta principal, bajo el cerrojo; UK `(MemberAccountId, Sequence)` |
| `ValueDate` · `AccountingDate` · `RegisteredAt` | date · date · datetime | no | 013 §0.5; fecha valor hacia atrás hasta `FECHA_VALOR_DIAS_CAPTACIONES` (D11) |
| `Amount` | `Monto` | no | valor de la operación (0 si no mueve dinero) |
| `ActorKind` · `ActorUserId` · `OriginUserCentralId` | int · int FK · uniqueidentifier | no · sí · sí | |
| `CollectorClass` · `CollectorCode` | nvarchar(30) · nvarchar(20) | sí | recaudador del contrato (`ClaseDeRecaudador` como texto); `CAR` en operaciones internas |
| `IdempotencyKey` · `RequestSha256` | nvarchar(150) · nvarchar(64) | sí | gramática de claves de la Cartera (FR-009) |
| `ContractVersion` | smallint | sí | versión adoptada de `IContratoDeCuentas` |
| `PaymentMeansClass` | nvarchar(30) | sí | `ClaseDeMedioDeRecaudo` como texto |
| `ReceiptNumber` · `ReceiptDate` · `ReceiptBranchPublicId` · `ReceiptMeansReference` | nvarchar(40) · date · uniqueidentifier · nvarchar(80) | sí | recibo del recaudador |
| `ClearingUntil` | date | sí | medio con días de disponibilidad: el valor queda en canje (FR-050) |
| `ClearedAt` | datetime | sí | **`[EscrituraUnica]`**: confirmación del recaudador |
| `CashOriginDeclarationAttachmentPublicId` | uniqueidentifier | sí | declaración de origen de fondos sobre el umbral (FR-059) |
| `AuthorizedByUserId` · `AuthorizationDecisionId` | int FK · int FK `LND_CreditDecisions` | sí | retiro sobre la atribución (FR-051), redención (FR-071), aprobador presente con segundo factor |
| `SignedByPersonIdsJson` | nvarchar(400) | sí | firmantes que autorizaron (manejo conjunto, límites, representante) |
| `RelatedLoanOperationId` | bigint FK `LND_LoanOperations` | sí | cruce interno o débito automático: el pago aplicado al crédito |
| `MembershipWithdrawalId` · `AssemblyDistributionId` · `DeceasedHolderCaseId` · `GarnishmentId` · `AccountChargeId` | int FK | sí | proceso que la originó (K7–K10) |
| `PayoutOrderId` | int FK `LND_AccountPayoutOrders` | sí | |
| `InboundMessageId` | int FK `LND_InboundMessages` | sí | resultado del giro, factura del cargo |
| `MigrationLoadId` | int FK | sí | `OpeningBalance` |
| `ReversesOperationId` | bigint FK self | sí | UK filtrado no nulo |
| `ReversedByOperationId` | bigint FK self | sí | **`[EscrituraUnica]`** |
| `Reason` | nvarchar(500) | sí | obligatorio en reversos, cargos manuales, retiros excepcionales |
| `Payload` | nvarchar(max) | no | entradas canónicas: valor, fecha valor, medio, firmas, simulación |
| `InputsSha256` · `RulesVersion` | nvarchar(64) · nvarchar(20) | no | |
| `PrevalidationOutcome` | nvarchar(20) | sí | sello de la validación previa contable (D23) |

**Claves e índices**. UK `(CollectorCode, IdempotencyKey)` filtrado `[IdempotencyKey] IS NOT NULL` (SC-005); UK
`(ReversesOperationId)` filtrado no nulo; IX `(MemberAccountId, ValueDate)`, `(CounterpartMemberAccountId)` filtrado,
`(RelatedLoanOperationId)` filtrado, `(ClearingUntil)` filtrado `[ClearedAt] IS NULL AND [ClearingUntil] IS NOT NULL`.

**Clases** (`MemberAccountOperationKind`, todas **(nuevas en data-model)**):

| Valor | Origina | Filas típicas del libro | E |
|---|---|---|---|
| `OpeningBalance` 1 | activación | `OpeningBalance` por concepto, sin hechos | K7 |
| `Deposit` 2 | contrato `ConsignarAsync`, Nómina, pagaduría | `Deposit` (+ aplicación a cuotas con `InstallmentId`) | K7 |
| `Withdrawal` 3 | contrato `RetirarAsync` | `Withdrawal`, `GmfCharged` | K8 |
| `Transfer` 4 | contrato `TrasladarAsync`, pantalla | `TransferOut`/`TransferIn`, `GmfCharged` según `GMF_HECHOS_GENERADORES` | K8 |
| `Reversal` 5 | contrato `ReversarAsync`, Cartera | espejos de la original y sus dependientes (FR-002) | K7 |
| `InterestSettlement` 6 | proceso o pantalla (`Lending.Interest.Settle`) | `InterestCredited`, `AccrualAdjustment` (redondeo), `WithholdingApplied` | K8 |
| `TermDepositConstitution` 7 | constitución | `Deposit` o `TransferIn` | K9 |
| `TermDepositInterestPayment` 8 | proceso en la fecha del plan | `InterestPaid`, `WithholdingApplied` | K9 |
| `TermDepositRollover` 9 | proceso tras la gracia | `Capitalization` (si capitaliza), `Reclassification` | K9 |
| `TermDepositRenewal` 10 | pantalla | ídem con condiciones nuevas | K9 |
| `TermDepositRedemption` 11 | vencimiento o anticipada | `InterestPaid`, `PenaltyApplied`, `Withdrawal`/`TransferOut`, `GmfCharged` | K9 |
| `Cancellation` 12 | cierre de cuenta (FR-039) | liquidación + `Withdrawal`/`TransferOut` | K8 |
| `Charge` 13 | proceso o evento | `ChargeApplied` | K8 |
| `ChargeWaiver` 14 | pantalla (`Lending.Charges.Waive`) | `ChargeWaived` | K8 |
| `InternalOffset` 15 | retiro del asociado | `InternalOffset` (débito de aportes y ahorros) | K7 |
| `DirectDebit` 16 | paso diario | `InternalOffset` | K8 |
| `ContributionRefund` 17 | retiro, exceso, amortización | `Refund` | K7 |
| `AssemblyCredit` 18 | revalorización o excedentes | `AssemblyCredit` | K10 |
| `Reclassification` 19 | proceso (inactividad, plazo de emisión, clase de aporte) | `Reclassification` | K7 |
| `ContractualMaturity` 20 | proceso al vencer el contractual | `BonusRecognized`, `InterestCredited`, `Withdrawal`/`TransferOut` | K9 |
| `ToUnclaimed` 21 | proceso o pantalla | `ToUnclaimed` | K10 |
| `DeceasedDelivery` 22 | entrega aprobada | `Withdrawal`/`TransferOut` | K10 |
| `GarnishmentConsignment` 23 | consignación al juzgado | `GarnishmentConsigned` | K8 |

**Reglas**. La escribe `RegistroDeCuentas` en la transacción de la operación, con sus filas del libro, sus versiones,
su retención, su GMF y sus mensajes. Primero pone la cuenta al día hasta su fecha (causación faltante). Un reverso no se
reversa (`Lending.Reversal.OfReversal`). El reverso de un pago aplicado al crédito por cruce arrastra el débito de la
cuenta y deja el retiro en `ToRedo` (R15). **H**: sí, con `ReversedByOperationId` y `ClearedAt` en `[EscrituraUnica]`.

### 4.2 `LND_MemberAccountTransactions` — `MemberAccountTransaction` (K7, H, `AuditableEntityLong`; FR-001, FR-002, FR-012, FR-058, FR-062, Principio XI)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `OperationId` | bigint FK `LND_MemberAccountOperations` | sí | |
| `ProcessRunId` | int FK `LND_ProcessRuns` | sí | causación del ciclo |
| `Kind` | int (`MemberAccountTransactionKind`) | no | research §0.3 y los nuevos de abajo |
| `Concept` | int (`MemberAccountConcept`) | no | §0.2 |
| `InstallmentId` | bigint FK `LND_AccountInstallments` | sí | pago de una cuota exigible |
| `SchedulePeriodNumber` | int | sí | período del plan del CDAT al que pertenece la causación o el pago |
| `TermsVersionId` | int FK `LND_MemberAccountTermsVersions` | sí | CDAT y contractual |
| `Amount` | `MontoCausado` | no | con signo, ≠ 0 (`CK_LND_MemberAccountTransactions_Amount`) |
| `ValueDate` · `AccountingDate` · `RegisteredAt` | date · date · datetime | no | |
| `ReversesTransactionId` | bigint FK self | sí | espejo |
| `MessagePublicId` | uniqueidentifier | sí | el hecho que la llevó; nulo sólo en `OpeningBalance` |

`MemberAccountTransactionKind` suma al final, **(nuevos en data-model)**: `ChargeWaived` 20, `BonusRecognized` 21,
`AssemblyCredit` 22.

**Claves e índices**. `CK_LND_MemberAccountTransactions_Origin`: exactamente uno de `OperationId` y `ProcessRunId`.
UK `UK_LND_MemberAccountTransactions_Accrual (MemberAccountId, Concept, ValueDate)` filtrado `[Kind] = 6 AND
[ReversesTransactionId] IS NULL`: la causación de una cuenta, concepto y día ocurre una vez (idempotencia del proceso).
UK `(ReversesTransactionId)` filtrado no nulo. IX `(MemberAccountId, ValueDate, Id)`, `(MemberAccountId,
AccountingDate)`, `(AccountingDate)`, `(OperationId)`, `(ProcessRunId)`, `(InstallmentId)` y `(MessagePublicId)`
filtrados no nulo.

**Tipos y signo** (por concepto, §0.2):

| `Kind` | Filas típicas |
|---|---|
| `OpeningBalance` | `+` por concepto al corte (FR-114) |
| `Deposit` · `TransferIn` · `AssemblyCredit` | `+Principal` o `+Contribution*` |
| `Withdrawal` · `TransferOut` · `InternalOffset` · `Refund` · `ToUnclaimed` · `GarnishmentConsigned` | `−Principal` o `−Contribution*` (y `−InterestPayable` si se entrega lo causado) |
| `InterestAccrued` · `AccrualAdjustment` | `±InterestPayable` (6 decimales; FR-060, FR-062) |
| `InterestCredited` | `−InterestPayable`, `+Principal` |
| `InterestPaid` | `−InterestPayable` (el neto sale por la orden o el traslado) |
| `WithholdingApplied` · `GmfCharged` | `−Principal` (o `−InterestPayable`) y `+Withholding` / `+Gmf` |
| `ChargeApplied` · `ChargeWaived` | `−Principal` o `+ChargeReceivable` · `−ChargeReceivable` |
| `PenaltyApplied` | `−InterestPayable` o `−Principal` |
| `Capitalization` | `−InterestPayable`, `+Principal` (neto de retención) |
| `Reclassification` | `−` y `+` del mismo valor entre conceptos (clase de aporte) o sin valor neto con nueva dimensión (inactividad, plazo de emisión) |
| `BonusRecognized` | `+Bonus` y, al abonarse, `−Bonus`, `+Principal` |

**Reglas**. Sólo inserción; un solo escritor. La causación del proceso se escribe en bloque sin `ChangeTracker`, con
un evento resumen por tanda. Corregir es un espejo o un `AccrualAdjustment` enlazado en el día actual (FR-062). La
liquidación a (D, R) la da `ReconstruccionDeCuenta`; el libro cumple Σ libro con `ValueDate ≤ d` = reconstrucción por
concepto para todo `d ≤ LastAccruedDate`. Volumen: una fila de causación por cuenta con interés y día (≈ 36 millones al
año con 100.000 cuentas); se particiona por año cuando haga falta. **H**: sí.

### 4.3 `LND_MemberAccountDailyPositions` — `MemberAccountDailyPosition` (K7, H versionada, `AuditableEntityLong`; I-6, FR-094, FR-109)

Instantánea por cuenta y día, de sólo inserción; alimenta la consulta de aportes, informes, saldos diarios y liquidez.
Nunca es la autoridad.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `PositionDate` · `Version` · `RegisteredAt` | date · int · datetime | no | UK `(MemberAccountId, PositionDate, Version)` |
| `Status` · `BranchId` · `CreditLineId` · `Nature` · `DepositClass` · `IssueTermBand` | int · int · int · int · int · int | no ×4 · sí ×2 | dimensiones de alcance e informe |
| `Principal` · `ContributionMinimum` · `ContributionOrdinary` · `ContributionExtraordinary` · `ContributionRevaluation` · `Bonus` · `ChargeReceivable` | `Monto` | no | saldos por concepto |
| `InterestPayable` | `MontoCausado` | no | |
| `HeldAmount` · `GarnishedAmount` · `ClearingAmount` · `MinimumBalance` · `AvailableBalance` | `Monto` | no | FR-057 desglosado |
| `WithholdingYearToDate` · `GmfMonthToDate` | `Monto` | no | memoria |
| `AppliedAnnualRate` | `TasaAnual` | sí | |
| `DaysToMaturity` | int | sí | CDAT; lo usa el fondo de liquidez por madurez residual |
| `InstallmentsInArrears` · `ArrearsAmount` | int · `Monto` | no | cuotas vencidas no pagadas (FR-094) |
| `IsInactive` · `IsGarnished` · `IsGmfExempt` · `IsAccrualSuspended` | bit | no | |
| `InputsSha256` | nvarchar(64) | no | |

IX `(PositionDate, BranchId, CreditLineId)`, `(PositionDate, Nature)`. **Reglas**. La escribe `RegistroDeCuentas` en
cada tanda del paso de captaciones y, cuando una operación con fecha valor anterior cambia días proyectados, agrega una
`Version` (nunca sobrescribe). `[SinDiffDeAuditoria]`. **H**: sí.

### 4.4 `LND_MemberAccountControls` — `MemberAccountControl` (K7; FR-031, FR-061, R08)

Una fila por cuenta, **sin saldos**: cerrojo y marcas de agua. Nace en el mismo `SaveChanges` que la cuenta.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | UK sin filtro |
| `LastAccruedDate` | date | no | última fecha causada; al nacer, la fecha valor de la apertura o el corte |
| `LastSettledThrough` | date | sí | fin del último período liquidado (FR-061) |
| `Sequence` | int | no | última asignada a una operación |
| `LastRegisteredAt` | datetime | no | `RegisteredAt` monótono por cuenta |
| `LastPositionDate` | date | sí | |
| `LastHolderActivityDate` | date | sí | último movimiento del titular (FR-058, FR-084) |
| `LastInstallmentPeriod` | nvarchar(10) | sí | `AAAA-MM-s` de la última cuota generada |

**Reglas**. Causar d exige `LastAccruedDate = d − 1`. Liquidar un período exige `LastAccruedDate ≥ fin del período` y
es idempotente por `LND_InterestSettlements` (SC-003). `[SinDiffDeAuditoria]`. **H**: no.

### 4.5 `LND_InterestSettlements` — `InterestSettlement` (K8, H; FR-061, FR-063, FR-064, FR-069, SC-003, SC-016)

Liquidación de intereses de una cuenta y un período (o un pago del plan del CDAT).

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `OperationId` | bigint FK `LND_MemberAccountOperations` | no | UK |
| `PeriodFrom` · `PeriodTo` | date | no | |
| `SchedulePeriodNumber` | int | sí | CDAT |
| `AccruedInterest` | `MontoCausado` | no | Σ causado del período |
| `GrossInterest` | `Monto` | no | redondeado según la versión |
| `RoundingDifference` | `MontoCausado` | no | `GrossInterest − AccruedInterest`, ajustado en la misma operación |
| `ThresholdReached` | bit | no | algún día alcanzó el umbral diario (R06) |
| `ThresholdDailyAmount` | `Monto` | sí | `UMBRAL_RETENCION_UVT_DIA × UVT` del período |
| `InflationaryComponentFraction` · `InflationaryComponentAmount` | `Fraccion` · `Monto` | sí | P27 |
| `WithholdingBase` · `WithholdingRate` · `WithholdingAmount` | `Monto` · `TasaAnual` · `Monto` | no | 0 si no aplica |
| `WithholdingExemptionReason` | nvarchar(30) | sí | `NonTaxpayer`, `SelfWithholder`, `BelowThreshold`, `ScopeExcluded` |
| `NetInterest` | `Monto` | no | `GrossInterest − WithholdingAmount` |
| `Destination` | int (`InterestDestination`) | no | |
| `TaxProfileSnapshotJson` | nvarchar(max) | no | perfil tributario leído (012) y parámetros vigentes |
| `ReversedByOperationId` | bigint FK | sí | **`[EscrituraUnica]`** |
| `InputsSha256` | nvarchar(64) | no | |

UK `UK_LND_InterestSettlements_Account_Period (MemberAccountId, PeriodTo)` filtrado `[ReversedByOperationId] IS NULL`.
La explicación día por día no se guarda: la da `ReconstruccionDeCuenta` (FR-011). La simulación (FR-064) no escribe.
**H**: sí.

### 4.6 `LND_WithholdingAccumulations` — `WithholdingAccumulation` (K8, H, `AuditableEntityLong`; FR-063, FR-104, FR-105, SC-016)

Acumulado de rendimientos y retención **por titular, año y concepto**, como filas por liquidación y parte (no un
contador: el acumulado es la suma). Repartido por la proporción de cada titular y cotitular vigente.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `PersonId` | int FK `COR_People` | no | titular o cotitular |
| `TaxYear` | smallint | no | |
| `MemberAccountId` | int FK | no | |
| `DepositClass` | int (`DepositClass`) | sí | concepto del certificado (rendimientos de ahorro, de CDAT…) |
| `InterestSettlementId` | int FK | sí | |
| `OperationId` | bigint FK | no | |
| `Share` | `Fraccion` | no | proporción aplicada |
| `GrossInterest` · `InflationaryComponentAmount` · `WithholdingBase` · `WithholdingAmount` | `Monto` | no | parte del titular |
| `ReversesAccumulationId` | bigint FK self | sí | espejo del reverso |
| `Origin` | nvarchar(20) | no | `Settlement` o `Migration` (año en curso cargado, FR-113) |

IX `(PersonId, TaxYear)`, `(MemberAccountId, TaxYear)`. `[SinDiffDeAuditoria]`. **H**: sí. **Escritor**:
`RegistroDeCuentas`.

### 4.7 `LND_GmfMonthlyUsage` — `GmfMonthlyUsage` (K8, H, `AuditableEntityLong`; FR-022, FR-053, FR-066, R07)

Uso del tope exento y GMF cobrado **por persona y mes**, como filas por disposición. El acumulado del mes es Σ, leído
bajo el cerrojo de la persona.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `PersonId` | int FK `COR_People` | no | |
| `Year` · `Month` | smallint · tinyint | no | |
| `MemberAccountId` | int FK | no | |
| `OperationId` | bigint FK | no | |
| `TaxableEventCode` | nvarchar(40) | no | hecho generador según `GMF_HECHOS_GENERADORES` |
| `DisposedAmount` · `ExemptAmount` · `TaxableBase` · `GmfAmount` | `Monto` | no | |
| `Policy` | int (`GmfExemptionPolicy`) | no | vigente en la fecha |
| `Rate` | `TasaAnual` | no | `GMF_TARIFA` (puede ser 0) |
| `ReversesUsageId` | bigint FK self | sí | |
| `Origin` | nvarchar(20) | no | `Operation` o `Migration` (acumulado del mes de corte) |

IX `(PersonId, Year, Month)`. `[SinDiffDeAuditoria]`. **Reglas**. Con tarifa 0 o política `NotApplicable` desde una
fecha, no se escriben filas desde esa fecha y lo anterior se conserva. **H**: sí.

### 4.8 `LND_AccountCharges` — `AccountCharge` (K8; FR-128, R14)

Cargo causado. Documento con estado.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `DepositLineChargeId` | int FK `LND_DepositLineCharges` | no | versión del cargo aplicada |
| `ChargeCode` · `Kind` | nvarchar(10) · int (`ChargeKind`) | no | copia |
| `PeriodFrom` · `PeriodTo` | date | sí | cargos periódicos |
| `TriggerOperationId` | bigint FK `LND_MemberAccountOperations` | sí | cargos por evento |
| `BaseAmount` · `Amount` | `Monto` | no · no | |
| `Status` | int (`AccountChargeStatus` **(nuevo en data-model)**) | no | `Collected` 1, `Pending` 2, `Waived` 3, `Reversed` 4 |
| `AppliedOperationId` · `CollectedOperationId` | bigint FK | no · sí | causación; cobro posterior de un pendiente |
| `WaivedAt` · `WaivedByUserId` · `WaiverReason` · `WaiverDecisionId` | datetime · int FK · nvarchar(300) · int FK `LND_CreditDecisions` | sí | `Lending.Charges.Waive`, sujeto `ChargeWaiver` |
| `ReversedByOperationId` | bigint FK | sí | |
| `IsVatTaxed` | bit | no | |
| `ChargeMessagePublicId` · `ReversalMessagePublicId` | uniqueidentifier | sí | `CargoDeCaptacionCausadoV1` / `CargoDeCaptacionReversadoV1` a Comercial |
| `CommercialInvoiceNumber` · `CommercialInvoiceStatus` | nvarchar(40) · nvarchar(20) | sí | informativo desde `FacturaDeCargoRegistradaV1` |

**Claves**. UK `(MemberAccountId, DepositLineChargeId, PeriodFrom)` filtrado `[PeriodFrom] IS NOT NULL`; UK
`(TriggerOperationId, DepositLineChargeId)` filtrado no nulo. **Transiciones**: `Pending → Collected` (siguiente
consignación) · `Pending → Waived` · `Collected → Reversed` · `Pending → Reversed`. **Escritor**: `RegistroDeCuentas`
(`ChargeApplied`, `ChargeWaived`). **H**: no.

### 4.9 `LND_AccountChargeExemptions` — `AccountChargeExemption` **(nuevo en data-model)** (K8; FR-128)

Exoneración individual aprobada con motivo, con vigencia.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `ChargeCode` | nvarchar(10) | no | código estable del cargo |
| `ValidFrom` · `ValidTo` | date | no · sí | |
| `Reason` | nvarchar(300) | no | |
| `ApprovedByUserId` · `DecisionId` | int FK · int FK `LND_CreditDecisions` | no · sí | segregación: quien pide no aprueba |

UK `(MemberAccountId, ChargeCode)` filtrado `[ValidTo] IS NULL AND [IsDeleted] = 0`. **H**: no.

---

## 5. Restricciones

### 5.1 `LND_AccountHolds` — `AccountHold` (K8; FR-054, FR-057, FR-082)

Bloqueo por motivo, por valor o total, con vigencia. Estado con fechas.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MemberAccountId` | int FK | no | |
| `Reason` | int (`HoldReason`) | no | research §0.3 + `Garnishment` 6 **(nuevo en data-model)** |
| `Amount` | `Monto` | sí | nulo = bloqueo total |
| `LoanId` · `CollateralId` | int FK `LND_Loans` · int FK `LND_Collaterals` | sí | `LoanCollateral`: garantía de un crédito de la Cartera |
| `GarnishmentId` | int FK `LND_Garnishments` | sí | obligatorio con `Garnishment` |
| `DeceasedHolderCaseId` | int FK | sí | `Deceased` |
| `ValidFrom` · `ValidTo` | date | no · sí | |
| `Notes` | nvarchar(500) | sí | |
| `SupportAttachmentPublicId` | uniqueidentifier | sí | |
| `PlacedByUserId` | int FK | sí | nulo = proceso |
| `ReleasedAt` · `ReleasedByUserId` · `ReleaseReason` | datetime · int FK · nvarchar(300) | sí | |

IX `(MemberAccountId)` filtrado `[ReleasedAt] IS NULL`. **Reglas**. Vigente = sin `ReleasedAt` y dentro de fechas. El
disponible resta los vigentes (FR-057). Una cuenta con bloqueo total no admite retiros ni cancelación (FR-039). Un
bloqueo `LoanCollateral` sólo lo libera la cancelación del crédito o su liberación de garantía. **Errores**:
`Lending.Holds.InsufficientAvailable` **(nuevo en data-model)**. **H**: no. **Escritor**: `Lending.Holds.Manage`;
los de `Garnishment` sólo el registro de embargos.

### 5.2 `LND_Garnishments` — `Garnishment` (K8; FR-055, FR-005, D20, R23)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `PersonId` | int FK `COR_People` | no | embargado |
| `CourtName` · `CourtCode` | nvarchar(200) · nvarchar(30) | no · sí | juzgado |
| `ProcessNumber` | nvarchar(40) | no | radicado |
| `OrderNumber` · `OrderDate` · `ReceivedOn` | nvarchar(40) · date · date | no | oficio |
| `ClaimantName` · `ClaimantTaxId` · `ClaimantPersonId` | nvarchar(200) · nvarchar(20) · int FK `COR_People` | no · sí · sí | demandante |
| `Amount` | `Monto` | sí | límite de la medida; nulo = sin límite |
| `IsChildSupport` | bit | no | alimentos: no aplica el límite inembargable |
| `DestinationBankAccountNumber` · `DestinationBankId` | nvarchar(25) · int FK `COR_Banks` | sí | cuenta de depósitos judiciales |
| `OrderAttachmentPublicId` | uniqueidentifier | no | |
| `Status` | int (`GarnishmentStatus` **(nuevo en data-model)**) | no | `Registered` 1, `Active` 2, `Released` 3, `Rejected` 4 |
| `RegisteredByUserId` · `ApprovedByUserId` · `ApprovedAt` | int FK · int FK · datetime | no · sí · sí | FR-005: quien registra no aprueba |
| `ReleaseOrderNumber` · `ReleaseOrderDate` · `ReleasedAt` · `ReleasedByUserId` | nvarchar(40) · date · datetime · int FK | sí | desembargo |
| `ReleaseAttachmentPublicId` | uniqueidentifier | sí | |

IX `(PersonId, Status)`. **Transiciones**: `Registered → Active` (aprobación: `LimiteInembargable` calcula lo
embargable sumando las cuentas de ahorro de la persona, salvo alimentos, en el orden `EMBARGO_ORDEN_CUENTAS`, y crea los
`AccountHold` `Garnishment` y marca `IsGarnished`) · `Registered → Rejected` · `Active → Released` (libera holds y
marca). Los **aportes nunca** se embargan: se responde al juzgado. Cada consignación es una operación
`GarnishmentConsignment` con orden de giro (`AccountPayoutOrder`, `GarnishmentConsignment`). Una consignación posterior
recalcula lo retenido bajo el cerrojo de la persona. **H**: no. **Escritor**: `Lending.Garnishments.Register/Release`.

#### 5.2.1 `LND_GarnishmentResponses` — `GarnishmentResponse` **(nuevo en data-model)** (K8, H; FR-055)

Respuestas al juzgado, cada una como documento emitido.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `GarnishmentId` | int FK | no | |
| `Kind` | int (`GarnishmentResponseKind` **(nuevo en data-model)**) | no | `Acknowledgement` 1, `BalanceReport` 2, `ContributionsNotGarnishable` 3, `ConsignmentNotice` 4, `ReleaseAcknowledgement` 5 |
| `IssuedDocumentId` | int FK `LND_IssuedDocuments` | no | clase `GarnishmentResponse` |
| `RetainedAmount` · `ConsignedAmount` | `Monto` | sí | |
| `RespondedAt` · `RespondedByUserId` | datetime · int FK | no | |

**H**: sí.

---

## 6. Procesos

### 6.1 `LND_MembershipWithdrawals` — `MembershipWithdrawal` (K7; FR-005, FR-076 a FR-080, FR-124, D14, D16, R15)

El trámite del retiro del asociado, de la solicitud a la devolución. Documento con estado.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `Number` | nvarchar(20) | no | numerador `MembershipWithdrawal`; UK |
| `PersonId` | int FK `COR_People` | no | el asociado |
| `ContributionAccountId` | int FK `LND_MemberAccounts` | no | su cuenta de aportes viva |
| `Cause` · `CauseDetail` | int (`MembershipWithdrawalCause`) · nvarchar(500) | no · sí | |
| `SupportAttachmentPublicIdsJson` | nvarchar(max) | no | soportes (lista de Guid) |
| `FiledOn` · `FiledByUserId` | date · int FK | no | `Lending.MembershipWithdrawals.File` |
| `Status` | int (`MembershipWithdrawalStatus`) | no | ver transiciones |
| `ApprovalSessionItemId` | int FK `LND_SessionItems` | sí | punto del acta del órgano (consejo o junta) que aprobó o rechazó |
| `EffectiveDate` | date | sí | fecha del retiro; la fija la aprobación |
| `DecisionReason` | nvarchar(500) | sí | rechazo |
| `DesistedAt` · `DesistReason` | datetime · nvarchar(300) | sí | sólo antes de aprobar |
| `ApprovedMessagePublicId` | uniqueidentifier | sí | `RetiroDeAsociadoAprobadoV1` a Asociados (fecha y causa; FR-124) |
| `SettlementVersion` | int | no | 0 hasta liquidar; sube en cada reliquidación tras `ToRedo` |
| `SettledAt` · `SettledByUserId` | datetime · int FK | sí | `Lending.MembershipWithdrawals.Settle` |
| `OffsetOperationId` | bigint FK `LND_MemberAccountOperations` | sí | la operación interna del cruce (FR-077) |
| `ContributionsTotal` · `SavingsTotal` · `LoansOffsetTotal` · `LossDeductionAmount` · `RefundAmount` | `Monto` | sí | totales de la liquidación vigente |
| `RefundDueOn` | date | sí | `EffectiveDate` + `RefundTermDays` de la versión de aportes |
| `RefundHeldByInstitutionalMinimum` | bit | no | FR-080: la devolución perforaría `APORTES_MINIMO_IRREDUCIBLE_ENTIDAD`; queda pendiente y avisa al gerente |
| `RefundDecisionId` | int FK `LND_CreditDecisions` | sí | devolución sobre la atribución (`ContributionRefund`) |
| `PaidAt` | datetime | sí | |
| `SettlementIssuedDocumentId` | int FK `LND_IssuedDocuments` | sí | `MembershipWithdrawalSettlement` |
| `ToRedoReason` | nvarchar(500) | sí | reverso de un pago cruzado (R15); también el motivo de la reapertura por un reverso tardío (D44) |
| `PendingLoanBalanceHandling` | int (`PendingLoanBalanceHandling`: `MoveToCashier` 1, `KeepRepaymentChannel` 2) **(nuevo, decisión del dueño del 2026-10-18, D45)** | sí | qué eligió quien liquidó para lo que los créditos siguen debiendo después del cruce; nulo sin deuda pendiente |

**Una solicitud viva por persona** (`Lending.Contributions.WithdrawalAlreadyFiled` **(nuevo en data-model)**): como
los filtros no admiten `IN` ni `OR` (013 §0.13), van dos UK `UK_LND_MembershipWithdrawals_Filed (PersonId)` filtrado
`[Status] = 1 AND [IsDeleted] = 0` y `UK_LND_MembershipWithdrawals_Approved (PersonId)` filtrado `[Status] = 2 AND
[IsDeleted] = 0`, y la regla de dominio cubre `Settled` y `ToRedo` bajo el cerrojo de la persona. IX `(Status)`.

**Estados y transiciones** (`MembershipWithdrawalStatus`):

```text
Filed ──aprobación con acta──▶ Approved ──liquidación + cruce──▶ Settled ──devolución girada o trasladada──▶ Paid
  │ └─ rechazo con acta ─▶ Rejected                                 │  ▲
  └─ desiste ─▶ Desisted                     reverso de un pago ─────▼  │ reliquidación
                                                                   ToRedo
```

- `Filed → Approved`: el punto de sesión del órgano (FR-076); quien radica no vota ni aprueba (FR-005). Emite
  `RetiroDeAsociadoAprobadoV1` en la misma transacción. Desde aquí el asociado sólo recibe operaciones del retiro
  (FR-030).
- `Approved → Settled`: el analista liquida aportes por clase, ahorros con intereses a la fecha, CDAT según la línea y
  los créditos vivos; el cruce aplica por `IContratoDeObligaciones` con medio `InternalOffset` y la prelación de los
  estatutos (D14) **en una sola operación**: si algo falla, nada se aplica (FR-077). Escribe las líneas §6.2.
- `Settled → Paid`: la orden de giro (`AccountPayoutOrder`, `ContributionRefund`) en `Disbursed` o el traslado.
- `Settled → ToRedo`: el reverso de un pago cruzado arrastra el débito de la cuenta; `ToRedo → Settled` reliquida con
  `SettlementVersion + 1`.

**H**: no. **Escritor**: comandos de retiro; las operaciones, `RegistroDeCuentas`.

### 6.2 `LND_MembershipWithdrawalLines` — `MembershipWithdrawalLine` (K7, H; FR-076 a FR-078)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `MembershipWithdrawalId` | int FK | no | |
| `SettlementVersion` · `LineNumber` | int | no | UK `(MembershipWithdrawalId, SettlementVersion, LineNumber)` |
| `Kind` | int (`MembershipWithdrawalLineKind` **(nuevo en data-model)**) | no | `ContributionBalance` 1, `SavingsBalance` 2, `TermDepositBalance` 3, `InterestToDate` 4, `Withholding` 5, `Gmf` 6, `LoanOffset` 7, `LossDeduction` 8, `Refund` 9 |
| `MemberAccountId` · `LoanId` | int FK | sí | |
| `Concept` | nvarchar(30) | sí | `MemberAccountConcept` o `LoanConcept` por nombre |
| `Amount` | `Monto` | no | con signo: `+` a favor del asociado, `−` lo que se descuenta |
| `AccountOperationId` · `LoanOperationId` | bigint FK | sí | operaciones que la aplicaron |
| `Notes` | nvarchar(300) | sí | |

Σ `Amount` de la versión = `RefundAmount` (≥ 0). Ninguna línea perfora el mínimo no reducible salvo en el retiro mismo
(FR-042). **H**: sí.

### 6.3 `LND_AssemblyDistributions` — `AssemblyDistribution` (K10; FR-045, FR-046, FR-047, D13)

Revalorización de aportes, distribución de excedentes o amortización decididas por la asamblea.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `Number` | nvarchar(20) | no | numerador `AssemblyDistribution`; UK |
| `Kind` | int (`AssemblyDistributionKind`) | no | |
| `FiscalYear` | smallint | no | ejercicio cuyos excedentes se distribuyen |
| `DecisionSessionItemId` | int FK `LND_SessionItems` | no | decisión de la asamblea (registrada sobre los órganos de la Cartera) |
| `BaseMethod` | int (`RevaluationBase` **(nuevo en data-model)**) | sí | `AverageBalance` 1 (defecto, D13), `YearEndBalance` 2 |
| `DecidedFraction` | `Fraccion` | sí | porcentaje decidido |
| `LegalLimitFraction` · `LegalLimitSource` | `Fraccion` · nvarchar(100) | sí | revalorización: `IPC` del año anterior (P11) |
| `FundAmount` | `Monto` | no | lo disponible en el fondo |
| `CalculatedTotal` · `ProrationFactor` · `AppliedTotal` | `Monto` · `Fraccion` · `Monto` | no · sí · no | prorrateo al fondo |
| `SurplusDestination` | int (`SurplusDestination` **(nuevo en data-model)**) | sí | `Contributions` 1, `Savings` 2, `Payment` 3 |
| `DestinationLineId` | int FK `LND_CreditLines` | sí | con `Savings` |
| `Status` | int (`AssemblyDistributionStatus` **(nuevo en data-model)**) | no | `Simulated` 1, `Approved` 2, `Applied` 3, `Voided` 4 |
| `SimulationVersion` | int | no | sube en cada simulación mientras no esté aprobada |
| `RegisteredByUserId` · `ApprovedByUserId` · `ApprovedAt` | int FK · int FK · datetime | no · sí · sí | segundo usuario (FR-005, FR-045) |
| `AppliedAt` · `ApplyingProcessRunId` | datetime · int FK `LND_ProcessRuns` | sí | se aplica en tandas, una operación `AssemblyCredit` por cuenta |
| `InputsSha256` | nvarchar(64) | no | |

**Transiciones**: `Simulated → Approved → Applied`; `Simulated|Approved → Voided`. **Reglas**. Aplicar es idempotente
por línea (`OperationId` `[EscrituraUnica]`). Los aportes no causan intereses: la revalorización no es interés. **H**:
no. **Escritor**: `Lending.Assembly.Register/Approve`.

### 6.4 `LND_AssemblyDistributionLines` — `AssemblyDistributionLine` (K10, H; FR-045, FR-046)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `AssemblyDistributionId` · `SimulationVersion` | int FK · int | no | UK `(AssemblyDistributionId, SimulationVersion, PersonId)` |
| `PersonId` | int FK `COR_People` | no | |
| `ContributionAccountId` | int FK `LND_MemberAccounts` | no | |
| `DestinationAccountId` | int FK `LND_MemberAccounts` | sí | ahorro de destino; nulo en pago |
| `BaseAmount` · `CalculatedAmount` · `ProratedAmount` | `Monto` | no | |
| `OperationId` | bigint FK `LND_MemberAccountOperations` | sí | **`[EscrituraUnica]`** al aplicar |
| `PayoutOrderId` | int FK `LND_AccountPayoutOrders` | sí | con `Payment` |

**H**: sí (con `[EscrituraUnica]`).

### 6.5 `LND_DeceasedHolderCases` — `DeceasedHolderCase` (K10; FR-065, FR-072, FR-082, FR-083, D22)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `Number` | nvarchar(20) | no | numerador `DeceasedHolderCase`; UK |
| `PersonId` | int FK `COR_People` | no | titular fallecido; UK `(PersonId)` filtrado `[Status] <> 4 AND [IsDeleted] = 0` |
| `DeathDate` · `ReportedOn` | date | no | |
| `DeathCertificateAttachmentPublicId` | uniqueidentifier | no | |
| `Route` | int (`DeliveryRoute` **(nuevo en data-model)**) | no | `Beneficiaries` 1, `WithoutProbate` 2, `Probate` 3 |
| `BeneficiarySnapshotJson` | nvarchar(max) | sí | conjunto de beneficiarios vigente en `DeathDate` por cuenta (FR-034) |
| `TotalAmount` | `Monto` | no | saldo a entregar a la fecha |
| `WithoutProbateCap` | `Monto` | sí | `TOPE_ENTREGA_SIN_SUCESION` vigente; el total no lo supera con `WithoutProbate` |
| `SupportAttachmentPublicIdsJson` | nvarchar(max) | no | soportes que exige `ENTREGA_SIN_SUCESION_SOPORTES` o la sucesión |
| `DeliveryPlanJson` | nvarchar(max) | sí | a quién, cuánto, por qué medio |
| `Status` | int (`DeceasedCaseStatus` **(nuevo en data-model)**) | no | `Registered` 1, `Approved` 2, `Delivered` 3, `Cancelled` 4 |
| `RegisteredByUserId` · `ApprovedByUserId` · `ApprovedAt` · `DecisionId` | int FK · int FK · datetime · int FK `LND_CreditDecisions` | no · sí · sí · sí | segundo usuario (FR-083); sujeto `DeceasedDelivery` |
| `DeliveredAt` | datetime | sí | |

**Reglas**. Registrar el caso **bloquea** las cuentas del titular (`AccountHold` `Deceased`, total) y, si la línea lo
dice, suspende la causación (FR-065); un CDAT no se paga al vencimiento (FR-072). La entrega son operaciones
`DeceasedDelivery` con órdenes de giro o traslados. **H**: no. **Escritor**: `Lending.DeceasedCases.Register/Approve`.

### 6.6 `LND_UnclaimedBalances` — `UnclaimedBalance` (K10; FR-081, FR-084)

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `PersonId` | int FK `COR_People` | no | titular |
| `SourceMemberAccountId` | int FK `LND_MemberAccounts` | no | |
| `Source` | int (`UnclaimedSource` **(nuevo en data-model)**) | no | `UncashedRefund` 1, `InactiveBalance` 2, `UncashedPayout` 3 |
| `SourcePayoutOrderId` | int FK `LND_AccountPayoutOrders` | sí | |
| `Amount` | `Monto` | no | |
| `TransferredOn` · `TransferOperationId` | date · bigint FK | no | operación `ToUnclaimed` |
| `Status` | int (`UnclaimedStatus` **(nuevo en data-model)**) | no | `Held` 1, `Claimed` 2 |
| `ClaimedOn` · `ClaimPayoutOrderId` · `ClaimedByUserId` | date · int FK · int FK | sí | |

**Reglas**. El traslado por la norma de cuentas abandonadas sólo se habilita con `CUENTAS_ABANDONADAS_TRASLADO_HABILITADO`
(FR-084, P7). Reclamar sale por orden de giro sobre la cuenta de origen aunque esté `Closed` (única excepción a
`Lending.Savings.AccountClosed`). **H**: no. **Escritor**: `Lending.Unclaimed.Manage`.

### 6.7 `LND_LiquidityFundSnapshots` — `LiquidityFundSnapshot` (K11, H; FR-025, FR-109, R25)

Instantánea inmutable de base y requerido del fondo de liquidez a una fecha.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `SnapshotDate` · `Version` · `RegisteredAt` | date · int · datetime | no | UK `(SnapshotDate, Version)` |
| `Category` | nvarchar(20) | no | categoría del perfil regulatorio vigente (`LiquidityFundCategory`) |
| `RegimeStage` | nvarchar(30) | no | régimen o etapa de transición vigente (parámetros `FONDO_LIQUIDEZ_*`) |
| `BaseByClassJson` | nvarchar(max) | no | depósitos por clase y banda de madurez residual, con intereses causados y por causar hasta el vencimiento en categoría plena |
| `RequiredAmount` | `Monto` | no | |
| `ConstitutedAmount` | `Monto` | no | Σ de `LND_LiquidityFundHoldings` vigentes en la fecha |
| `Shortfall` | `Monto` | no | max(0, requerido − constituido); > 0 alerta |
| `ParametersSnapshotJson` | nvarchar(max) | no | porcentajes leídos con su versión |
| `ProcessRunId` · `RequestedByUserId` | int FK · int FK | sí | proceso o pantalla |
| `AlertPublicId` | uniqueidentifier | sí | |
| `InputsSha256` | nvarchar(64) | no | |

Lee las posiciones diarias (§4.3) de la fecha. **H**: sí.

### 6.8 `LND_LiquidityFundHoldings` — `LiquidityFundHolding` (K11; FR-109)

Lo constituido que registra el tesorero. Estado con fechas; audita su antes/después.

| Campo | Tipo | Nulo | Regla |
|---|---|---|---|
| `Kind` | int (`LiquidityHoldingKind` **(nuevo en data-model)**) | no | `BankDeposit` 1, `TermInvestment` 2, `TrustFund` 3, `Other` 99 |
| `InstitutionPersonId` | int FK `COR_People` | no | entidad depositaria |
| `Reference` | nvarchar(60) | no | número del título o de la cuenta |
| `Amount` | `Monto` | no | |
| `ConstitutedOn` · `MaturesOn` · `WithdrawnOn` | date | no · sí · sí | |
| `SupportAttachmentPublicId` | uniqueidentifier | no | |
| `RegisteredByUserId` | int FK | no | `Lending.LiquidityFund.Manage` |

**H**: no.

---

## 7. Migración (K7–K9; FR-113 a FR-118, R27)

Reutiliza la mecánica de la 013 §29–§30: `LND_MigrationLoads` (con `Nature`, §1.8) y sus estados `Draft` 1 →
`Applied` 2 → `Replaced` 3 / `Activated` 4, **un borrador, una aplicada y una activada por naturaleza**;
`LND_MigrationLoadParts` (una fila por archivo, `PartKey` = la plantilla); `LND_MigrationLoadDifferences` (cuadre por
línea, concepto y conjunto de cuentas mapeadas por la matriz, FR-115; `ConceptFamily` suma `DepositPrincipal`,
`InterestPayable`, `Contribution{clase}`, `ChargeReceivable`); `LND_Activations` (con `Nature`). Aplicar una carga sólo
guarda la carga; nada nace en el libro hasta activar. Ninguna plantilla crea personas. Las líneas se crean antes por
`lending.deposit-lines` y `lending.contribution-lines`, que registran versiones por el mismo camino que la pantalla (no
tienen preparación), salvo su hoja de equivalencias.

### 7.1 Preparación por parte (H; `[SinDiffDeAuditoria]`)

Columnas comunes (como la 013 §29.3): `MigrationLoadPartId` int FK, `RowNumber` int (UK `(MigrationLoadPartId,
RowNumber)`), `PersonId` int FK `COR_People` donde la fila es de una persona, `SourceAccountNumber` nvarchar(30) donde
es de una cuenta. `AuditableEntityLong` en cuotas. Las columnas de importación las fija `contracts/plantillas-y-archivos.md`.

| Tabla — entidad · plantilla | Clave natural | Columnas propias | Al activar nace | E |
|---|---|---|---|---|
| `LND_MigrationLineEquivalenceRows` — `MigrationLineEquivalenceRow` · hoja de `lending.deposit-lines` / `lending.contribution-lines` | `SourceProductCode` | `SourceProductCode` (20), `CreditLineCode` (+ `CreditLineId` resuelto), `Nature` | nada: resuelve la línea de las demás filas | K7 |
| `LND_MigrationContributionAccountRows` — `MigrationContributionAccountRow` · `lending.contribution-accounts` | `SourceAccountNumber` | `PersonId`, `SourceProductCode`?, `OfficePublicId`, `OpenedOn`, `MinimumBalance`, `OrdinaryBalance`, `ExtraordinaryBalance`, `RevaluedBalance`, `MinimumNonReducible`, `InstallmentAmount`? (la sellada si difiere de la línea) | cuenta `Contribution` (`Origin = Migration`), control con la marca en el corte, operación `OpeningBalance` por clase sin hechos | K7 |
| `LND_MigrationAccountInstallmentRows` — `MigrationAccountInstallmentRow` · `lending.account-installments` | `SourceAccountNumber`, período | `PeriodYear`, `PeriodMonth`, `SubPeriodNumber`, `DueDate`, `Amount`, `PaidAmount` | cuotas atrasadas pendientes (`AccountInstallment`, con lo pagado como `OpeningBalance` aplicado) | K7 |
| `LND_MigrationAccountPartyRows` — `MigrationAccountPartyRow` · `lending.member-account-parties` | `SourceAccountNumber`, `PersonId`, `Role` | `Role`, `Share`?, `SignatureLimit`?, `RepresentationBasis`?, `ValidFrom`, `HandlingMode`? (en la fila del titular) | `MemberAccountParty`; sin soporte de representación de un menor: aviso y cuenta con `RequiresDataUpdate` | K7 |
| `LND_MigrationAccountRows` — `MigrationAccountRow` · `lending.member-accounts` | `SourceAccountNumber` | `PersonId`, `SourceProductCode`, `OfficePublicId`, `DepositClass`, `OpenedOn`, `Status` (`Active`/`Inactive`), `InactiveSince`?, `Principal`, `InterestPayable` (causado no liquidado, 6 decimales), `ChargeReceivable`?, `LastSettledThrough`?, `LastHolderActivityDate`?, `AgreedAnnualRate`?; contractual: `InstallmentAmount`, `InstallmentCount`, `PaidInstallments`, `LateInstallments`, `MaturityDate`, `BonusAnnualRate`, `ContractualStatus` | cuenta, control, condiciones selladas cuando la línea declara tasa pactada o es contractual (`Cause = Migration`), `OpeningBalance` por concepto sin hechos | K8 |
| `LND_MigrationHoldRows` — `MigrationHoldRow` · `lending.holds-and-garnishments` | `SourceAccountNumber`, `Kind`, `Reference` | `Kind` (`Hold`/`Garnishment`), `HoldReason`?, `Amount`?, `ValidFrom`, `ValidTo`?; embargo: `CourtName`, `ProcessNumber`, `OrderNumber`, `OrderDate`, `ClaimantName`, `IsChildSupport`, `RetainedAmount` | `AccountHold` y `Garnishment` `Active` con su retención (dato desde la carga aunque su entrega sea posterior) | K8 |
| `LND_MigrationGmfExemptionRows` — `MigrationGmfExemptionRow` · `lending.gmf-exemptions` | `PersonId` | `SourceAccountNumber` (cuenta marcada), `MarkedSince`, `MonthExemptUsed` (acumulado del mes de corte) | `GmfExemptAccount` y una fila `GmfMonthlyUsage` con `Origin = Migration` | K8 |
| `LND_MigrationTaxYearRows` — `MigrationTaxYearRow` · `lending.tax-year-to-date` | `PersonId`, `SourceAccountNumber` | `TaxYear`, `GrossInterest`, `InflationaryComponentAmount`, `WithholdingAmount`, `GmfAmount` | `WithholdingAccumulation` con `Origin = Migration` (y GMF del año para el certificado) | K8 |
| `LND_MigrationTermDepositRows` — `MigrationTermDepositRow` · `lending.term-deposits` | `SourceAccountNumber` | `PersonId`, `SourceProductCode`, `OfficePublicId`, `ConstitutedOn`, `StartDate`, `PrincipalAmount`, `TermDays`, `AppliedAnnualRate`, `TableAnnualRate`?, `NegotiatedSpread`?, `InterestPeriodicity`, `InterestTiming`, `LastInterestPaidThrough`?, `InterestPayable`, `RolloverCount`, `MaturityInstruction`, `InterestDestination` y su destino, `SourceTitleNumber`? | cuenta `TermDeposit`, condiciones selladas (`Migration`), plan regenerado por `PlanDeCdat` desde el período en curso, `OpeningBalance` | K9 |

(`?` = admite nulo.) La plantilla `lending.contribution-balances` de la 013 (`LND_ContributionBalanceLoads`) se acepta
como fuente de las filas de aportes (FR-113).

**Validación entre partes** (`ApplyMigrationLoadCommand` con `Nature`, todo o nada, errores por parte, fila y columna):
cada `SourceAccountNumber` de otra parte existe en su parte de cuentas; cada `SourceProductCode` tiene equivalencia;
Σ `Share` de titulares = 1 y de beneficiarios = 1; una sola cuenta exenta por persona; una cuenta de aportes por
persona; el plan regenerado de cada CDAT casa con `InterestPayable` dentro de la tolerancia de redondeo. Después calcula
el cuadre contra Contabilidad por línea, concepto y cuenta (FR-115) y escribe `LND_MigrationLoadDifferences`.

### 7.2 Activación por naturaleza (FR-116, FR-117, R21)

`ActivateContributionsCommand` y `ActivateDepositsCommand` escriben una fila de `LND_Activations` con su `Nature` y
la fecha en `LND_Setup` (§1.2), en una transacción explícita por tandas, con la misma regla que la 013 §30:
corte + 1, plazo de fecha valor, mes abierto, diferencias aceptadas con acta, carga reemplazable hasta activar. Una
cooperativa sin historia activa sin carga (FR-117). Requisitos propios: créditos activados; participantes registrados
(Recaudo, Tesorería, Nómina); en producción, K7 a K11 completas (Q1). Las cifras de referencia (FR-118) son la vista de
totales de la carga, sin nombrar el sistema de origen.

---

## 8. Mapa y conteo

### 8.1 Mapa de relaciones

```text
CONFIGURACIÓN
LND_CreditLines (Nature) ─< LND_CreditLineVersions (Placement, 013)
          │               ─< LND_DepositLineVersions ─< LND_DepositLineRateTiers
          │               │                          └< LND_DepositLineCharges
          │               ─< LND_ContributionLineVersions
          └─< LND_NegotiatedRateRules (por línea, versionadas por Code)
LND_PayrollAgreements ─< LND_PayrollAgreementVersions ─< LND_PayrollAgreementFileGroups >─ COR_BankFileFormats
                                                     └< LND_PayrollAgreementConceptEquivalences

CUENTAS
COR_People ─< LND_MemberAccounts ─1 LND_MemberAccountControls (cerrojo y marcas)
                  ├< LND_MemberAccountParties >─ COR_People (titular, cotitular, firmante, representante, beneficiario)
                  ├< LND_MemberAccountTermsVersions (H) ─< LND_TermDepositSchedules (H)
                  ├< LND_AccountInstallments (H) ─< LND_AccountInstallmentNonDeductions (H)
                  ├< LND_DirectDebitMandates >─ LND_Loans
                  ├< LND_AccountPayoutOrders
                  └── LND_GmfExemptAccounts (una vigente por persona)

LIBRO
LND_MemberAccounts ─< LND_MemberAccountOperations (H) ─< LND_MemberAccountTransactions (H)  ◀── también la corrida
                              │        └── RelatedLoanOperationId ──▶ LND_LoanOperations (cruce, débito automático)
                              ├< LND_InterestSettlements (H)
                              ├< LND_WithholdingAccumulations (H, por persona y año)
                              ├< LND_GmfMonthlyUsage (H, por persona y mes)
                              └< LND_AccountCharges ─ LND_AccountChargeExemptions
LND_MemberAccounts ─< LND_MemberAccountDailyPositions (H, proyección versionada)

RESTRICCIONES
LND_MemberAccounts ─< LND_AccountHolds >─ LND_Garnishments ─< LND_GarnishmentResponses >─ LND_IssuedDocuments
                                       >─ LND_Loans / LND_Collaterals (garantía)

PROCESOS
LND_MembershipWithdrawals ─< LND_MembershipWithdrawalLines >─ LND_MemberAccounts, LND_Loans
          └── LND_SessionItems (acta del órgano)
LND_AssemblyDistributions ─< LND_AssemblyDistributionLines   (decisión en LND_SessionItems)
LND_DeceasedHolderCases ─< LND_AccountHolds (Deceased)        LND_UnclaimedBalances
LND_LiquidityFundSnapshots (H)   LND_LiquidityFundHoldings

DOCUMENTOS Y MIGRACIÓN
LND_IssuedDocuments ─< LND_IssuedDocumentAccounts >─ LND_MemberAccounts, LND_MemberAccountTermsVersions
LND_MigrationLoads (Nature) ─< LND_MigrationLoadParts ─< LND_Migration…Rows (9 de esta feature)
LND_Activations (Nature) ◀── LND_Setup.DepositsActivationId / ContributionsActivationId
```

### 8.2 Tablas nuevas por entrega

| Entrega | Migración par **(nuevo en data-model)** | Tablas nuevas | N.º |
|---|---|---|---|
| K7 | `CaptacionesAportes` | `LND_ContributionLineVersions`, `LND_MemberAccounts`, `LND_MemberAccountParties`, `LND_MemberAccountOperations`, `LND_MemberAccountTransactions`, `LND_MemberAccountDailyPositions`, `LND_MemberAccountControls`, `LND_AccountInstallments`, `LND_AccountInstallmentNonDeductions`, `LND_AccountPayoutOrders`, `LND_MembershipWithdrawals`, `LND_MembershipWithdrawalLines`, `LND_PayrollAgreementFileGroups`, `LND_PayrollAgreementConceptEquivalences`, `LND_IssuedDocumentAccounts`, `LND_MigrationLineEquivalenceRows`, `LND_MigrationContributionAccountRows`, `LND_MigrationAccountInstallmentRows`, `LND_MigrationAccountPartyRows` | 19 |
| K8 | `CaptacionesAhorro` | `LND_DepositLineVersions`, `LND_DepositLineRateTiers`, `LND_DepositLineCharges`, `LND_InterestSettlements`, `LND_WithholdingAccumulations`, `LND_GmfMonthlyUsage`, `LND_GmfExemptAccounts`, `LND_AccountHolds`, `LND_Garnishments`, `LND_GarnishmentResponses`, `LND_AccountCharges`, `LND_AccountChargeExemptions`, `LND_DirectDebitMandates`, `LND_MigrationAccountRows`, `LND_MigrationHoldRows`, `LND_MigrationGmfExemptionRows`, `LND_MigrationTaxYearRows` | 17 |
| K9 | `CaptacionesCdat` | `LND_NegotiatedRateRules`, `LND_MemberAccountTermsVersions`, `LND_TermDepositSchedules`, `LND_MigrationTermDepositRows` | 4 |
| K10 | `CaptacionesAsamblea` | `LND_AssemblyDistributions`, `LND_AssemblyDistributionLines`, `LND_DeceasedHolderCases`, `LND_UnclaimedBalances` | 4 |
| K11 | `CaptacionesLiquidez` | `LND_LiquidityFundSnapshots`, `LND_LiquidityFundHoldings` | 2 |
| | | **Total** | **46** |

Las FK de columnas que apuntan a tablas de una entrega posterior nacen con esa entrega (p. ej.
`LND_MemberAccountOperations.GarnishmentId` en K8, `.AssemblyDistributionId` y `.DeceasedHolderCaseId` en K10,
`LND_AccountHolds.DeceasedHolderCaseId` en K10, `LND_MemberAccountTransactions.TermsVersionId` en K9). La migración
destructiva con guarda `RetiroDeCaptacionesHeredadas` (research R28) va **antes** de `CaptacionesAportes` y borra las
38 tablas conservadas y las 7 `CDT_*`.

**Tablas existentes que cambian** (columnas): `LND_CreditLines`, `LND_Setup`, `LND_PayrollAgreementVersions`,
`LND_IssuedDocumentEvents`, `LND_MigrationLoads`, `LND_Activations`, `LND_ProcessRunErrors`, `LND_AccountingFactParts`,
`LND_AttributionRules`, `ACC_LendingPostingRules`, `COR_RegulatoryProfileVersions` (**11**). Sólo valores de enum o
semilla: `LND_NumberSequences`, `LND_IssuedDocuments`, `LND_FactTypes`, `ACC_LendingVoucherMappings`,
`COR_BankFileFormats`, `COR_NationalValues`/`ADM_NationalValues`, `COR_ParameterVersions`, `SEC_Permissions` (**8**).

---

## 9. Parámetros y valores nacionales (R26; FR-014, FR-015)

Todo se siembra **«pendiente de validar»** con su fuente (`LegalSource`) y vigencia; ningún valor legal está en el
código (`LaCarteraNoTieneValoresLegalesFijos` cubre `Domain/Lending/Deposits` y `Application/Lending/Deposits`). Los
valores entre paréntesis son los que fijan la spec o la investigación; los demás los carga la contadora.

### 9.1 Parámetros de la Cartera (`COR_ParameterVersions`, `Module = 'LND'`)

| Clave | Tipo | Defecto | Ámbitos | Lo leen | Fuente | E |
|---|---|---|---|---|---|---|
| `RETENCION_TARIFA_AHORRO` | Decimal (fracción) | (0,07) | None, CreditLine | §2.1, §4.5 | DUR 1625, tarifa residual | K8 |
| `RETENCION_TARIFA_CDAT` | Decimal | (0,04) | None, CreditLine | §4.5 | DUR 1625 art. 1.2.4.2.83; Decreto 572 de 2025 | K9 |
| `RETENCION_TARIFA_CONTRACTUAL` · `RETENCION_TARIFA_PERMANENTE` | Decimal | sin valor (PV) | None, CreditLine | §4.5 | DUR 1625 | K8/K9 |
| `UMBRAL_RETENCION_UVT_DIA` | Decimal (UVT) | (0,055) | None | `RetencionSobreRendimientos` | DUR 1625 art. 1.2.4.2.6 | K8 |
| `RETENCION_ALCANCE_UMBRAL` | Choice (`WithholdingThresholdScope`) | `ExceptTermDeposit` (D10) | None | ídem | P2 | K8 |
| `RETENCION_COMPONENTE_INFLACIONARIO_APLICA` | Bool | true (P27) | None | ídem | ET; Decreto 898 de 2026 | K8 |
| `GMF_TARIFA` | Decimal | (0,004); admite 0 | None | `GravamenMovimientosFinancieros` | ET art. 872 | K8 |
| `GMF_TOPE_EXENCION_UVT` | Decimal (UVT) | (350) | None | ídem | ET art. 879 num. 1 | K8 |
| `GMF_POLITICA_EXENCION` | Choice (`GmfExemptionPolicy`) | `SingleMarkedAccount` (P25) | None | ídem, §3.8 | ET art. 881-1 | K8 |
| `GMF_HECHOS_GENERADORES` | Text (JSON) | gravados: retiro, traslado a otro titular, traslado ahorro↔CDAT, redención de CDAT abonada en cuenta, cancelación; exento: traslado entre cuentas de ahorro del mismo titular (D32) | None | ídem, §4.7 | ET arts. 871, 879 num. 14; Oficio DIAN 2787 de 2019 | K8 |
| `FONDO_LIQUIDEZ_*` | Decimal / Text | la tabla del Decreto 976 de 2026 y su transición, por categoría | None | `FondoDeLiquidez`, §6.7 | Decreto 976 de 2026 | K11 |
| `TOPE_CUOTA_FONDO_EMPLEADOS_PCT` | Decimal | (0,10) | None | §2.5 | investigación §3.5 | K7 |
| `FRACCION_MINIMA_APORTES` | Decimal | (0,10) | None | §2.5 | investigación §3.5 | K7 |
| `LIMITE_PARTICIPACION_APORTES_PN` · `LIMITE_PARTICIPACION_APORTES_PJ` | Decimal | sin valor (P12) | None | §2.5, FR-043 | Ley 79 de 1988 (a validar) | K7 |
| `MAYORIA_DE_EDAD_ANIOS` | Int | (18) | None | paso diario (R22) | Código Civil | K7 |
| `EMBARGO_ORDEN_CUENTAS` | Text (JSON) | orden por clase de cuenta (PV) | None | `LimiteInembargable` | P5 | K8 |
| `FECHA_VALOR_DIAS_CAPTACIONES` | Int | (5) (D11) | None | §4.1 | reglamento | K7 |
| `Riesgo.ExcepcionAportesArrastre` (013) · `Riesgo.DescuentoAportesDeterioro` | Bool | false | None | `ContextoDeAportes` (R20) | 013 D19 | K7 |
| `EFECTIVO_UMBRAL_DECLARACION_ORIGEN` **(nuevo en data-model)** | Decimal (pesos) | sin valor (P4) | None | §4.1 FR-059 | norma SARLAFT vigente | K7 |
| `APORTES_MINIMO_IRREDUCIBLE_ENTIDAD` **(nuevo en data-model)** | Decimal (pesos) | sin valor (estatutos) | None | §6.1 FR-080 | estatutos | K7 |
| `ENTREGA_SIN_SUCESION_SOPORTES` **(nuevo en data-model)** | Text (JSON) | lista de soportes (PV, P6) | None | §6.5 | reglamento | K10 |
| `CUENTAS_ABANDONADAS_TRASLADO_HABILITADO` **(nuevo en data-model)** | Bool | false (P7) | None | §6.6 | Ley 1777 (a validar) | K10 |

### 9.2 Valores nacionales (`CodigosDeValorNacional`, replicados a cada cooperativa)

| `Code` | `Key` | `Unit` | Lo usa | Semilla | E |
|---|---|---|---|---|---|
| `UVT` (existente) | `''` | `Pesos` | umbral de retención, tope GMF | 2026: 52.374 | K7 |
| `IPC` (existente) | `ANUAL` | `Fraccion` | límite de revalorización (P11) | no | K10 |
| `SMMLV` (existente) | `''` | `Pesos` | cuota y mínimo de aportes en fracción del salario mínimo | 2026 (013) | K7 |
| `FESTIVO` (existente) | fecha | — | gracia del CDAT, pago en hábil (`CalendarioHabil`) | 013 | K9 |
| `COMPONENTE_INFLACIONARIO` | año gravable | `Fraccion` | `RetencionSobreRendimientos` | año gravable 2025 según Decreto 898 de 2026 (PV) | K8 |
| `LIMITE_INEMBARGABLE` | `''` | `Pesos` | `LimiteInembargable` | se registra cada publicación (PV, P5) | K8 |
| `TOPE_ENTREGA_SIN_SUCESION` | `''` | `Pesos` | §6.5 | se registra (PV, P6) | K10 |
| `TOPE_SEGURO_DEPOSITOS` **(nuevo en data-model)** | `''` | `Pesos` | depósitos asegurados por ahorrador (FR-110) | se registra (PV) | K11 |

---

## 10. Lista «(nuevo en data-model)» — para reconciliar con research §0

**Tablas** (13): `LND_AccountInstallmentNonDeductions` (`AccountInstallmentNonDeduction`), `LND_AccountPayoutOrders`
(`AccountPayoutOrder`; alternativa: generalizar `LND_DisbursementOrders` con `LoanId` nulo),
`LND_IssuedDocumentAccounts` (`IssuedDocumentAccount`), `LND_GarnishmentResponses` (`GarnishmentResponse`),
`LND_AccountChargeExemptions` (`AccountChargeExemption`), y las hermanas de `LND_MigrationAccountRows` que research no
nombra: `LND_MigrationLineEquivalenceRows`, `LND_MigrationContributionAccountRows`,
`LND_MigrationAccountInstallmentRows`, `LND_MigrationAccountPartyRows`, `LND_MigrationTermDepositRows`,
`LND_MigrationHoldRows`, `LND_MigrationGmfExemptionRows`, `LND_MigrationTaxYearRows` (research las cubre como «y
hermanas»; se listan para fijar el nombre).

**Columnas en tablas existentes**: `LND_CreditLines` UK `UK_LND_CreditLines_ActiveContribution`;
`LND_PayrollAgreementVersions.UsesFileGroups`, `.EmployeeIdentification`, `.IncludesAccountInstallments`;
`LND_IssuedDocumentEvents.CausingAccountOperationId`; `LND_MigrationLoads.Nature`, `.AccountCount`;
`LND_Activations.Nature`; `LND_ProcessRunErrors.MemberAccountId`; `LND_AccountingFactParts.TriggerAccountOperationId`,
`.AccountCount`; `LND_AttributionRules.MaxNegotiatedSpread`; `COR_RegulatoryProfileVersions.MayTakeDeposits`,
`.DepositsFromAssociatesOnly`, `.LiquidityFundCategory`, `.DepositInsuranceEnrolled`, `.IsGmfWithholdingAgent`;
`LND_MemberAccounts` UK `UK_LND_MemberAccounts_LivePermanent`.

**Enums nuevos**: `MemberAccountOperationKind`, `MemberAccountOrigin`, `ContractualStatus`, `AccountTermsCause`,
`InterestDestination`, `MaturityInstruction`, `EarlyRedemptionPenaltyMode`, `ContractualBreachEffect`,
`BonusRecognition`, `ChargeTrigger`, `ChargeCalculation`, `ChargeBase`, `ChargeShortfallPolicy`,
`AccountChargeStatus`, `ContributionAmountMode`, `ExcessContributionAction`, `ConceptAllocationRule`,
`DirectDebitScope`, `PayoutPurpose`, `GarnishmentStatus`, `GarnishmentResponseKind`, `MembershipWithdrawalLineKind`,
`RevaluationBase`, `SurplusDestination`, `AssemblyDistributionStatus`, `DeliveryRoute`, `DeceasedCaseStatus`,
`UnclaimedSource`, `UnclaimedStatus`, `LiquidityHoldingKind`.

**Valores agregados a enums**: `MemberAccountTransactionKind` `ChargeWaived` 20, `BonusRecognized` 21,
`AssemblyCredit` 22; `HoldReason` `Garnishment` 6; `IssuedDocumentKind` `GarnishmentResponse` 15; `LendingNumberKind`
`MemberAccount` 10 … `DeceasedHolderCase` 14; `AccountingFactPartSource` `AccountCatchUp`; `BankFieldSource`
`AccountNumber` 93, `AccountInstallmentPeriod` 94, `PaymasterConceptCode` 95; `LendingDecisionSubjects`
`TermDepositRedemption`, `ContributionRefund`, `AssemblyDistribution`, `GarnishmentRegistration`.

**Alias de precisión**: `MontoCausado` decimal(18,6).

**Migraciones pares**: `CaptacionesAportes` (K7), `CaptacionesAhorro` (K8), `CaptacionesCdat` (K9),
`CaptacionesAsamblea` (K10), `CaptacionesLiquidez` (K11).

**Parámetros y valores nacionales**: `EFECTIVO_UMBRAL_DECLARACION_ORIGEN`, `APORTES_MINIMO_IRREDUCIBLE_ENTIDAD`,
`ENTREGA_SIN_SUCESION_SOPORTES`, `CUENTAS_ABANDONADAS_TRASLADO_HABILITADO`; valor nacional `TOPE_SEGURO_DEPOSITOS`.

**Códigos de error**: `Lending.Savings.AmountTooPrecise`, `.NotActivated`, `.ClassChanged`, `.RateTiersOverlap`,
`.RateChangeNoticeTooShort`, `.AccountClosed`, `.SharesDoNotAddUp`, `.MinorRequiresRepresentative`;
`Lending.Contributions.NotActivated`, `.FractionBelowLegal`, `.InstallmentAboveCap`, `.WithdrawalAlreadyFiled`;
`Lending.TermDeposit.LineIncomplete`, `.SpreadAboveRule`; `Lending.CreditLine.HasAccounts`, `.NatureMismatch`;
`Lending.Payroll.ConceptMappedTwice`, `.UnmappedLine`; `Lending.Holds.InsufficientAvailable`;
`Accounting.LendingRule.DimensionNotApplicable`.

**Puntos a reconciliar** (no son nombres nuevos, son decisiones). **Resueltos en research §2–§4** (C01 ámbitos 5 y 6 de K2; C20 `AccountFlag` = `Standard`, `GmfExempt`, `Garnished`, y la inactividad es rol): lo que sigue es el texto original, sólo como historia.
1. **Ámbito de archivo de pagaduría** (§1.7): research R17 dice «`Scope` nuevo `PaymasterDeduction`»; la 013 ya fija
   `PayrollDeductionRequest` 5 y `PayrollDeductionResult` 6 para K2. Aquí se reutiliza ese par y no se agrega valor; si
   se prefiere un ámbito propio para cuentas, sería `PaymasterDeduction` 7.
2. **Estilo de las claves de parámetro** (§9.1): research R26 usa claves en mayúsculas con guion bajo
   (`RETENCION_TARIFA_AHORRO`); el catálogo de la 013 usa `Grupo.Nombre` (`Pagos.DiasMaximosFechaValor`, CH36) y
   reserva el estilo en mayúsculas para los códigos `LIMITE` de valores nacionales. Conviene decidir si estas claves se
   escriben como `Retencion.TarifaAhorro`… o si algunas (umbral, tope GMF, límites de participación) son límites legales
   `LIMITE` con dirección de lo estricto.
3. **`DimensionKey`** (§1.6): la 013 documenta prefijos de letra en la clave, pero `LendingPostingRule.ClaveDe` concatena
   por posición sin letras. Este documento sigue al código.
4. **`AccountFlag`** (§1.6): con una sola posición en la clave y precedencia `Garnished` > `Inactive` > `GmfExempt`; si
   la contadora quiere combinar marcas, la dimensión debe pasar a tres booleanas.
5. **Orden de giro de cuentas** (§3.9): tabla propia frente a generalizar `LND_DisbursementOrders`.
