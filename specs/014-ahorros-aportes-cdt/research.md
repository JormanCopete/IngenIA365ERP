# Research: Cartera financiera — captaciones (feature 014)

**Fecha**: 2026-10-14 · **Spec**: [spec.md](spec.md) · **Fundamento financiero**:
[investigacion-financiera.md](investigacion-financiera.md)

Formato de cada decisión: **Decisión** · **Por qué** · **Alternativas descartadas** · **Trazas** (FR, D,
preguntas). La sección §0 fija los **nombres canónicos**: manda sobre cualquier otra redacción de
nombres en `data-model.md`, `contracts/` y `quickstart.md`.

Punto de partida verificado en el código (rama `013-cartera-financiera`, `c6ba3f2c`): la 014 se construye
**cuando la 013 esté completa** (Q3). Si al empezar la 013 cambió algo de lo que aquí se nombra (nombres de
enums, la clave de la matriz, el ciclo diario), manda el código y esta investigación se enmienda antes de
`/speckit-tasks`.

No queda ningún `NEEDS CLARIFICATION` técnico: el stack es el del repositorio y las preguntas de negocio
están resueltas en la spec (Clarifications 2026-10-14) o tienen decisión por defecto.

---

## 0. Nombres canónicos

### 0.1 Espacios y carpetas

| Capa | Ruta | Contenido |
|---|---|---|
| Dominio puro | `src/Core/IngenIA365ERP.Domain/Lending/Deposits/` | Motores de captaciones (§0.4) |
| Entidades | `src/Core/IngenIA365ERP.Domain/Entities/Lending/Deposits/` | Cuentas, CDAT, libro, retenciones, cargos, retiro, asamblea, liquidez |
| Enums | `src/Core/IngenIA365ERP.Domain/Enums/Lending/DepositEnums.cs` | Enums nuevos (§0.3) |
| Aplicación | `src/Core/IngenIA365ERP.Application/Lending/Deposits/` | `Lines`, `Accounts`, `Operations`, `Accrual`, `TermDeposits`, `Contributions`, `Holds`, `Charges`, `Withdrawals`, `Assembly`, `Liquidity`, `Integration`, `Documents`, `Reports`, `Migration`, `Activation` |
| Puertos | `src/Core/IngenIA365ERP.Application/Common/Integration/Lending/` | `IContratoDeCuentas` (nuevo); `IConsultaDeAportes` se mueve aquí desde `Common/Integration/Savings` |
| Contabilidad | `src/Core/IngenIA365ERP.Application/Accounting/Lending/` | Roles, hechos y dimensiones nuevos en lo existente |
| Persistencia | `src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Lending/Deposits/` | Configuraciones EF |
| API | `src/Presentation/IngenIA365ERP.API/Endpoints/Lending/Deposits/` | Grupos de rutas |
| Pantallas | `src/Presentation/IngenIA365ERP.Shared/Pages/Cartera/Captaciones/` | Pantallas `/cartera/ahorros*`, `/cartera/cdat*`, `/cartera/aportes*`, `/cartera/retiros-asociado*` |
| Casos dorados | `tests/IngenIA365ERP.Domain.Tests/Lending/Deposits/Casos/*.json` | §R12 |

### 0.2 Tablas (prefijo `LND_`, ningún nombre heredado)

Los nombres heredados que **no** se pueden reutilizar: `LND_SavingsAccounts`, `LND_SavingsParameters`,
`LND_DepositAccounts`, `LND_DepositAudit`, `LND_DepositEntries`, `LND_DepositSeals`, `LND_DepositSignatures`,
`LND_ContributionReductions`, `LND_ContributionReductionParams`, `LND_AssociateWithdrawals`,
`LND_WithdrawalStatuses`, `LND_TransactionCodes`, `LND_Documents`, `LND_Checkbooks`, `LND_CheckClearing`,
`LND_Cashiers`, `LND_CashBases` y los `CDT_*`. Por eso la cuenta se llama **`MemberAccount`** («cuenta del
asociado»), neutral entre ahorro, CDAT y aportes.

| Tabla | Entidad | Qué es | Entrega |
|---|---|---|---|
| `LND_CreditLines` (existente) | `CreditLine` | Identidad común de la línea; **columna nueva `Nature`** | K7 |
| `LND_DepositLineVersions` | `DepositLineVersion` | Versión inmutable de una línea de captación | K8 |
| `LND_DepositLineRateTiers` | `DepositLineRateTier` | Tramos de tasa por saldo, plazo y monto | K8 |
| `LND_DepositLineCharges` | `DepositLineCharge` | Catálogo de cargos de la línea (FR-128) | K8 |
| `LND_NegotiatedRateRules` | `NegotiatedRateRule` | Reglas de tasa negociada (FR-127) | K9 |
| `LND_ContributionLineVersions` | `ContributionLineVersion` | Versión inmutable de la línea de aportes | K7 |
| `LND_MemberAccounts` | `MemberAccount` | Cuenta (ahorro, contractual, permanente, CDAT, aportes) | K7 |
| `LND_MemberAccountParties` | `MemberAccountParty` | Titulares, cotitulares, firmantes, representantes, beneficiarios | K7 |
| `LND_MemberAccountTermsVersions` | `MemberAccountTermsVersion` | Condiciones selladas (CDAT, contractual, tasa negociada) | K9 |
| `LND_TermDepositSchedules` | `TermDepositScheduleItem` | Plan de intereses del CDAT, por versión de condiciones | K9 |
| `LND_MemberAccountOperations` | `MemberAccountOperation` | Hecho primario de cada operación (recaudador, clave, medio) | K7 |
| `LND_MemberAccountTransactions` | `MemberAccountTransaction` | Libro por concepto, tres fechas, reverso espejo (bigint) | K7 |
| `LND_MemberAccountDailyPositions` | `MemberAccountDailyPosition` | Posición diaria versionada | K7 |
| `LND_MemberAccountControls` | `MemberAccountControl` | Marca de agua de causación y última liquidación | K7 |
| `LND_InterestSettlements` | `InterestSettlement` | Liquidación de intereses de un período (H) | K8 |
| `LND_WithholdingAccumulations` | `WithholdingAccumulation` | Rendimientos y retención por titular, año y concepto | K8 |
| `LND_GmfMonthlyUsage` | `GmfMonthlyUsage` | Acumulado exento del mes por persona | K8 |
| `LND_GmfExemptAccounts` | `GmfExemptAccount` | Cuenta marcada exenta, con vigencia | K8 |
| `LND_AccountInstallments` | `AccountInstallment` | Cuotas exigibles (aportes, permanente, contractual) | K7 |
| `LND_AccountHolds` | `AccountHold` | Bloqueos | K8 |
| `LND_Garnishments` | `Garnishment` | Embargos y sus respuestas | K8 |
| `LND_AccountCharges` | `AccountCharge` | Cargos causados (FR-128) | K8 |
| `LND_DirectDebitMandates` | `DirectDebitMandate` | Autorización de débito automático | K8 |
| `LND_MembershipWithdrawals` | `MembershipWithdrawal` | Trámite del retiro del asociado (FR-076) | K7 |
| `LND_MembershipWithdrawalLines` | `MembershipWithdrawalLine` | Liquidación: aportes, ahorros, cruces, pérdidas, devolución | K7 |
| `LND_AssemblyDistributions` | `AssemblyDistribution` | Revalorización y excedentes (FR-045, FR-046) | K10 |
| `LND_AssemblyDistributionLines` | `AssemblyDistributionLine` | Por asociado | K10 |
| `LND_DeceasedHolderCases` | `DeceasedHolderCase` | Fallecimiento y entrega a beneficiarios | K10 |
| `LND_UnclaimedBalances` | `UnclaimedBalance` | No reclamados | K10 |
| `LND_LiquidityFundSnapshots` | `LiquidityFundSnapshot` | Base y requerido del fondo de liquidez (H) | K11 |
| `LND_LiquidityFundHoldings` | `LiquidityFundHolding` | Constituido que registra el tesorero | K11 |
| `LND_PayrollAgreementFileGroups` | `PayrollAgreementFileGroup` | Grupos de archivo del convenio (FR-125) | K7 |
| `LND_PayrollAgreementConceptEquivalences` | `PayrollAgreementConceptEquivalence` | Conceptos equivalentes y reparto | K7 |
| `LND_MigrationAccountRows` y hermanas | `MigrationAccountRow`… | Preparación de la migración (§R27) | K7–K9 |

`LND_Setup` (`LendingSetup`) suma `DepositsActivationDate`/`…Id`/`…At` y `ContributionsActivationDate`/
`…Id`/`…At`. `LND_UserCreditLineScopes` sirve igual (la línea es común).

### 0.3 Enums nuevos (valores fijos, nunca se renumeran)

| Enum | Valores |
|---|---|
| `LineNature` | `Placement` 1, `Deposit` 2, `Contribution` 3 |
| `DepositClass` | `Demand` 1 (a la vista), `Contractual` 2, `Permanent` 3, `TermDeposit` 4 (CDAT) |
| `BalanceMethod` | `DailyBalance` 1, `AverageBalance` 2, `MinimumBalance` 3 |
| `RateTierMode` | `WholeBalance` 1, `Stepped` 2 |
| `InterestTiming` | `Arrears` 1 (vencido), `Advance` 2 (anticipado) |
| `RolloverRateRule` | `SameRate` 1, `CurrentTableRate` 2, `Agreed` 3 |
| `AfterMaturityTreatment` | `StopAccruing` 1, `MoveToDemand` 2 |
| `MemberAccountStatus` | `Opening` 1, `Active` 2, `Inactive` 3, `Matured` 4, `Closing` 5, `Closed` 6 |
| `MemberAccountPartyRole` | `Holder` 1, `CoHolder` 2, `Signatory` 3, `LegalRepresentative` 4, `Beneficiary` 5 |
| `HandlingMode` | `Indistinct` 1, `Joint` 2 |
| `MemberAccountConcept` | `Principal` 1, `InterestPayable` 2, `Withholding` 3, `Gmf` 4, `ContributionMinimum` 5, `ContributionOrdinary` 6, `ContributionExtraordinary` 7, `ContributionRevaluation` 8, `ChargeReceivable` 9, `Bonus` 10 |
| `MemberAccountTransactionKind` | `OpeningBalance` 1, `Deposit` 2, `Withdrawal` 3, `TransferIn` 4, `TransferOut` 5, `InterestAccrued` 6, `AccrualAdjustment` 7, `InterestCredited` 8, `InterestPaid` 9, `WithholdingApplied` 10, `GmfCharged` 11, `ChargeApplied` 12, `PenaltyApplied` 13, `Capitalization` 14, `Reclassification` 15, `InternalOffset` 16, `Refund` 17, `ToUnclaimed` 18, `GarnishmentConsigned` 19 |
| `GmfExemptionPolicy` | `SingleMarkedAccount` 1, `PerHolderLimit` 2, `NotApplicable` 3 |
| `WithholdingThresholdScope` | `AllDepositClasses` 1, `ExceptTermDeposit` 2, `None` 3 |
| `HoldReason` | `Internal` 1, `LoanCollateral` 2, `SignaturePending` 3, `Deceased` 4, `ScreeningMatch` 5, `Other` 99 |
| `ChargeKind` | `MaintenanceFee` 1, `ExtraOperation` 2, `ExtraDocument` 3, `MediumReplacement` 4, `Inactivity` 5, `Other` 99 |
| `MembershipWithdrawalCause` | `Voluntary` 1, `Exclusion` 2, `Death` 3, `Other` 99 |
| `MembershipWithdrawalStatus` | `Filed` 1, `Approved` 2, `Settled` 3, `Paid` 4, `Desisted` 5, `Rejected` 6, `ToRedo` 7 |
| `AssemblyDistributionKind` | `Revaluation` 1, `SurplusDistribution` 2, `Amortization` 3 |
| `ContributionClass` | `Minimum` 1, `Ordinary` 2, `Extraordinary` 3, `Revalued` 4 |
| `IssueTermBand` | `UnderSixMonths` 1, `SixToTwelve` 2, `TwelveToEighteen` 3, `EighteenOrMore` 4 |

Valores **agregados al final** de enums existentes:
- `LendingProcessKind`: `DepositsDailyCycle` 7 sólo si el plan de la 013 cambió el ciclo; por defecto
  **no** se agrega: el ciclo diario suma un paso (§R10).
- `IssuedDocumentKind`: `AccountStatement` 7, `AccountBalanceCertificate` 8, `DepositTaxCertificate` 9,
  `WithholdingCertificate` 10, `TermDepositTitle` 11, `ContributionCertificate` 12,
  `ContributionClearance` 13, `MembershipWithdrawalSettlement` 14.
- `ClaseDeMedioDeRecaudo`: sin cambios (los cruces usan `InternalOffset` 22).
- `IntegrationParticipantClass`: sin cambios; `Contributions` 3 queda sin uso nuevo (C3 de la spec).
- `AttributionKind` (o su equivalente en `Governance`): `DepositWithdrawal`, `NegotiatedRate`,
  `MembershipWithdrawal`, `DeceasedDelivery`, `ChargeWaiver`.

### 0.4 Motores puros (`Domain/Lending/Deposits/`)

| Motor | Hace | Reutiliza |
|---|---|---|
| `MotorDeCaptaciones` | Fachada, como `MotorDeCartera` | — |
| `InteresDeCaptacion` | Interés diario por método de saldo y tramos | `EquivalenciaDeTasas`, `ContadorDeDias`, `Causacion.Acumulado/DelDia`, `RedondeoDeCartera`, `TasaDelDia.TasaVariable` |
| `RetencionSobreRendimientos` | Umbral diario, total, componente inflacionario, perfil | `AritmeticaDecimal` |
| `GravamenMovimientosFinancieros` | GMF con política, tope, hechos generadores, tarifa cero | — |
| `PlanDeCdat` | Plan vencido o anticipado, cuadre de causación, prórroga, renovación, redención | `EquivalenciaDeTasas`, `CalendarioHabil` |
| `AhorroContractual` | Valor final, bonificación, incumplimiento | `AritmeticaDecimal` |
| `TasaNegociada` | Regla aplicable y margen máximo | — |
| `CargosDeCuenta` | Cargo, gratuitos, exoneraciones | — |
| `RevalorizacionYExcedentes` | Base, límite IPC, prorrateo al fondo | — |
| `FondoDeLiquidez` | Base y requerido por categoría, bandas y transición | — |
| `ReconstruccionDeCuenta` | Saldo por concepto a D con información hasta R | patrón de `ReconstruccionDeCredito` |
| `LimiteInembargable` | Saldo embargable sumando cuentas, salvo alimentos | — |

### 0.5 Puertos y escritores únicos

- **`IContratoDeCuentas`** (nuevo, `Common/Integration/Lending`): `ConsultarAsync`, `ListarExigiblesAsync`,
  `SimularAsync`, `ConsignarAsync`, `RetirarAsync`, `TrasladarAsync`, `ReversarAsync`. `VersionDelContrato = 1`.
  Implementación real `Application/Lending/Integration/ContratoDeCuentasDeCartera`; antes de activar,
  `ContratoDeCuentasSinActivar`.
- **`IConsultaDeAportes`** (existente): se mueve a `Common/Integration/Lending` y su implementación real es
  `Application/Lending/Integration/ConsultaDeAportesDeCartera`; `ConsultaDeAportesNoDisponible` queda para
  cooperativas sin aportes activados.
- **`IConsultasDeCartera`**: suma `ProductosDeCaptacionAsync` (FR-095).
- **Escritor único del libro de cuentas**: `Application/Lending/Deposits/Ledger/RegistroDeCuentas`
  (`NadieEscribeElLibroDeCuentasFueraDelRegistro`).
- **Cerrojo**: `CerrojoDeCartera` gana el nivel «cuenta» en el orden fijo: setup → persona → crédito →
  cuenta, por `Id` (§R08).

### 0.6 Rutas

API (todas bajo `RequirePermission`):
- `/api/lending/lines` (nuevo nombre de lectura común, con filtro `nature`; `/api/lending/credit-lines`
  sigue para colocación) · `/api/lending/lines/{id}/deposit-versions` · `/contribution-versions` ·
  `/negotiated-rate-rules` · `/charges`
- `/api/lending/accounts` (cuentas de captación y aportes), `/api/lending/accounts/{id}/operations`,
  `/holds`, `/parties`, `/charges`, `/settlements`, `/statement`
- `/api/lending/term-deposits` (constituir, prorrogar, renovar, redimir)
- `/api/lending/contributions` (cuotas, extraordinarios)
- `/api/lending/garnishments`, `/api/lending/direct-debits`, `/api/lending/gmf-exemptions`
- `/api/lending/membership-withdrawals`, `/api/lending/assembly-distributions`,
  `/api/lending/deceased-cases`, `/api/lending/unclaimed`
- `/api/lending/liquidity-fund`
- `/api/lending/deposits/migration/*`, `/api/lending/deposits/activation`
- `/api/reports/lending/{vista}` con vistas nuevas (§R24)

Pantallas: `/cartera/lineas` (catálogo común con naturaleza), `/cartera/ahorros`, `/cartera/ahorros/{id}`,
`/cartera/cdat`, `/cartera/cdat/nuevo`, `/cartera/cdat/{id}`, `/cartera/aportes`, `/cartera/aportes/{id}`,
`/cartera/retiros-asociado`, `/cartera/retiros-asociado/{id}`, `/cartera/embargos`,
`/cartera/asamblea`, `/cartera/fallecimientos`, `/cartera/no-reclamados`, `/cartera/fondo-de-liquidez`,
`/cartera/captaciones/migracion`, `/cartera/captaciones/activacion`. Las rutas heredadas `/cartera/cdt*`,
`/cartera/depositos`, `/cartera/retiros`, `/cartera/extractos`, `/cartera/parametros-ahorro`,
`/cartera/parametros-sipla`, `/cartera/codigos-movimiento`, `/cartera/zonas`, `/cartera/tipos-zona`,
`/cartera/estados-retiro` se retiran (FR-120).

### 0.7 Permisos (catálogo `Lending.*`, sembrado por la API al arrancar)

`Lending.DepositLines.View/Manage`, `Lending.ContributionLines.View/Manage`, `Lending.Accounts.View/Read/Open/Close`,
`Lending.Accounts.Deposit/Withdraw/Transfer/Reverse`, `Lending.Accounts.Parties.Manage`,
`Lending.TermDeposits.Constitute/Renew/Redeem`, `Lending.NegotiatedRates.Request/Approve`,
`Lending.Contributions.Manage`, `Lending.Holds.Manage`, `Lending.Garnishments.Register/Release`,
`Lending.DirectDebits.Manage`, `Lending.GmfExemptions.Manage`, `Lending.Charges.Manage/Waive`,
`Lending.Interest.Settle`, `Lending.MembershipWithdrawals.File/Approve/Settle`,
`Lending.Assembly.Register/Approve`, `Lending.DeceasedCases.Register/Approve`, `Lending.Unclaimed.Manage`,
`Lending.LiquidityFund.View/Manage`, `Lending.Deposits.Migration`, `Lending.Deposits.Activate`. `Read`
implica datos del titular; `View`, no (convención de la 013).

---

## 1. Decisiones

### R01 · La línea común conserva `LND_CreditLines` y suma `Nature`

- **Decisión**: la identidad común de toda línea es la entidad existente `CreditLine` (`LND_CreditLines`)
  con una columna nueva `Nature` (`LineNature`, por defecto `Placement` para todas las existentes). La
  versión de condiciones depende de la naturaleza: `CreditLineVersion` (colocación, sin cambios),
  `DepositLineVersion` y `ContributionLineVersion`. `CreditLine.Code` sigue siendo la dimensión de la
  matriz para las tres naturalezas, y `UserCreditLineScope` da el alcance por línea sin cambios.
- **Por qué**: el dueño pidió el concepto de línea común. Reutilizar la identidad conserva el alcance,
  la clave de la matriz, la unicidad del código en el catálogo y las pantallas del catálogo. Renombrar la
  tabla a `LND_Lines` tocaría cientos de referencias de la 013 ya verificadas, sin cambio funcional.
- **Alternativas descartadas**: tabla de identidad nueva `LND_Lines` (renombre masivo); una sola versión
  con columnas de las tres naturalezas (vuelve opcionales la modalidad y la usura y debilita las
  invariantes de crédito); catálogo de captaciones separado (contradice el pedido del dueño).
- **Riesgo**: el método `Activar` de `CreditLine` exige usura; se condiciona a `Nature == Placement`, y
  `BloquearPorUsura` no aplica a las demás. Pruebas: los 32 casos dorados de crédito siguen verdes (SC-019).
- **Trazas**: FR-027, FR-028, FR-029, FR-121, D2.

### R02 · Una sola entidad de cuenta para captación y aportes

- **Decisión**: `MemberAccount` con `LineNature` (`Deposit` o `Contribution`) y `DepositClass` (nulo en
  aportes). Los conceptos (`MemberAccountConcept`) cubren capital, interés, retención, GMF y clases de
  aporte; un aporte no causa intereses porque su versión de línea no tiene tasa.
- **Por qué**: libro, posición, cerrojo, contrato, documentos y migración son iguales; separar duplicaría
  todo. El nombre neutral evita los nombres heredados.
- **Alternativas**: tablas separadas `Deposit` y `ContributionAccount` (doble escritor, doble contrato).
- **Trazas**: FR-001, FR-040 a FR-049.

### R03 · Libro propio con las reglas del libro del crédito

- **Decisión**: `MemberAccountOperation` es el hecho primario (recaudador, clave, medio, payload, reverso
  de operación) y `MemberAccountTransaction` el libro por concepto, `bigint`, sólo inserción, con
  `ValueDate`, `AccountingDate`, `RegisteredAt`, `ReversesTransactionId`, signo + = la Cartera le debe al
  titular. `MemberAccountDailyPosition` es instantánea versionada (I-6 de la 013). `MemberAccountControl`
  guarda `LastAccruedDate` y `LastSettledThrough`. Único escritor `RegistroDeCuentas`.
- **Por qué**: `LoanTransaction` tiene FK al crédito y conceptos de crédito; generalizarla rompe la 013.
- **Alternativas**: un libro genérico común a créditos y cuentas (reescribe la 013 ya verificada).
- **Trazas**: FR-001, FR-002, FR-011, I-2.

### R04 · Reconstrucción de la cuenta a D con información hasta R

- **Decisión**: `ReconstruccionDeCuenta` (puro) recibe operaciones y versiones de línea hasta R y devuelve
  saldos por concepto e intereses día por día hasta D. La causación diaria del proceso es idempotente por
  diferencia de acumulados (`Causacion.DelDia`), como en el crédito.
- **Por qué**: reproducibilidad (FR-011, SC-001) y ajuste por fecha valor sin reescribir (FR-062).
- **Trazas**: FR-011, FR-060 a FR-062.

### R05 · Interés de captación

- **Decisión**: `i_d = (1 + EA)^(1/B) − 1` con `EquivalenciaDeTasas.TasaDiaria` (compuesta por defecto),
  `B` según la convención de la versión. Saldo diario por defecto (D31): el interés del día es
  `saldo con fecha valor del cierre × i_d`. Promedio: igual con interés simple. Mínimo: `saldo mínimo del
  período × ((1+EA)^(n/B) − 1)` al liquidar. Tramos `WholeBalance` o `Stepped`. Día bajo el mínimo para
  interés: cero. Tasa variable con `TasaDelDia.TasaVariable` si la versión lo declara. La causación guarda
  hasta 6 decimales; la liquidación redondea al peso y la diferencia se ajusta en la misma operación.
- **Por qué**: investigación §3.3; reutiliza motores verificados.
- **Trazas**: FR-020, FR-060, FR-061, D7, D9, D31.

### R06 · Retención en la fuente

- **Decisión**: `RetencionSobreRendimientos` recibe los intereses diarios del período a liquidar y aplica:
  1. perfil tributario de la persona (012): no contribuyente o autorretenedor → cero;
  2. umbral diario `UMBRAL_RETENCION_UVT_DIA` (0,055 UVT) × UVT vigente: si ningún día lo alcanza → cero;
     si alguno lo alcanza → base = total del período. El alcance del umbral es el parámetro
     `RETENCION_ALCANCE_UMBRAL` (`ExceptTermDeposit` por defecto, D10);
  3. componente inflacionario (`RETENCION_COMPONENTE_INFLACIONARIO_APLICA`, por defecto sí para persona
     natural no obligada a llevar contabilidad, P27) con el último porcentaje publicado
     (`COMPONENTE_INFLACIONARIO`, valor nacional nuevo);
  4. tarifa por clase de línea (`RETENCION_TARIFA_CDAT` 4 %, `RETENCION_TARIFA_AHORRO` 7 %).
  Se practica en cada abono o pago, antes de capitalizar (FR-063).
- **Por qué**: DUR 1625 arts. 1.2.4.2.6 y 1.2.4.2.83; Decreto 572 de 2025; Decreto 898 de 2026.
- **Trazas**: FR-021, FR-063, D10, P2, P27.

### R07 · GMF

- **Decisión**: `GravamenMovimientosFinancieros` con parámetros con vigencia `GMF_TARIFA` (0,004; admite 0),
  `GMF_TOPE_EXENCION_UVT` (350), `GMF_POLITICA_EXENCION` (`SingleMarkedAccount` por defecto, P25) y la tabla
  `GMF_HECHOS_GENERADORES` (por defecto gravados: retiro, traslado a otro titular, traslado ahorro↔CDAT,
  redención de CDAT abonada en cuenta, cancelación; exento: traslado entre cuentas de ahorro del mismo
  titular). Con tarifa cero o política `NotApplicable` desde una fecha, no se calcula desde esa fecha. El
  acumulado exento es por persona y mes (`LND_GmfMonthlyUsage`); la cuenta marcada vive en
  `LND_GmfExemptAccounts` con vigencia; marcar una segunda se rechaza (`Lending.Gmf.AlreadyMarked`).
- **Por qué**: ET arts. 871, 879, 881-1; decisión del dueño (P25).
- **Trazas**: FR-022, FR-036, FR-053, FR-066, D32.

### R08 · Concurrencia

- **Decisión**: `CerrojoDeCartera` toma la cuenta después del crédito en el orden fijo, por `Id`. Las
  operaciones que tocan crédito y cuenta (cruce, débito automático) toman ambos en ese orden. El
  consecutivo de número de cuenta usa `LND_NumberSequences` con una clase nueva por línea.
- **Trazas**: FR-019, FR-077, FR-090.

### R09 · Contrato de cuentas

- **Decisión**: puerto propio `IContratoDeCuentas` en vez de agrandar `IContratoDeObligaciones`. Mismas
  reglas: síncrono en la unidad de trabajo del recaudador (exige transacción en curso), registro de
  participantes con `ContractName = ProveedorDeCuentas`, versión adoptada, clave con la gramática de la
  Cartera, validador y evento de auditoría (desviación T101 de la 013). `ListarExigibles` devuelve cuotas de
  aportes, permanente y contractual. Un retiro en efectivo lo autoriza la Cartera y lo entrega Recaudo.
- **Por qué**: el contrato de obligaciones tiene cinco métodos fijados por prueba y semántica de deuda
  (prelación, anticipos); depósitos es otra semántica. Un puerto más en la lista cerrada es la enmienda
  mínima.
- **Alternativas**: agregar métodos a `IContratoDeObligaciones` (rompe `LaCarteraSoloSeTocaPorSusInterfaces`
  y mezcla semánticas).
- **Trazas**: FR-086 a FR-093, E-01.

### R10 · Ciclo diario y cierre mensual

- **Decisión**: `CicloDiarioDeCartera` suma un paso «captaciones» después de la causación de créditos,
  ejecutado por `PasoDeCaptaciones` en tandas de `Lending:Process:BatchSize`: causación, liquidación,
  cuotas, vencimientos y gracia, prórrogas, contractual, inactividad, cargos periódicos, mayoría de edad.
  Errores a `LND_ProcessRunErrors`. `CierreMensualDeCartera` suma la sección de captaciones (cierre de
  causación y posición, sin SARC), y la guarda de Contabilidad `LendingNotClosed` la cubre. Sólo corre en
  cooperativas con captaciones o aportes activados.
- **Por qué**: un solo proceso por cooperativa (una corrida a la vez) evita competencia por cerrojos y
  reutiliza la puesta al día.
- **Trazas**: FR-031, FR-100, FR-017.

### R11 · CDAT

- **Decisión**: el CDAT es una `MemberAccount` de clase `TermDeposit` con `MemberAccountTermsVersion`
  (sellada) y su plan `LND_TermDepositSchedules`. Fórmulas de la investigación §3.2 en `PlanDeCdat`. La
  causación diaria reparte el interés de cada período con la fórmula compuesta por día y el último día
  absorbe el redondeo contra el plan. Gracia en días hábiles (`CalendarioHabil`). Prórroga: nueva versión
  de condiciones con la regla de tasa de la línea; renovación: misma cuenta, nueva versión con monto,
  plazo o tasa nuevos y título nuevo (el número del título lleva sufijo de versión). Reclasificación por
  `IssueTermBand` como hecho.
- **Por qué**: una cuenta por CDAT conserva el libro y el historial; la versión sellada repite el patrón
  `LoanTermsVersion`.
- **Alternativas**: cuenta nueva por renovación (pierde trazabilidad y multiplica cuentas).
- **Trazas**: FR-023, FR-067 a FR-073, D17, D19.

### R12 · Casos dorados

- **Decisión**: JSON en `Domain.Tests/Lending/Deposits/Casos/`, con el mismo formato que los de crédito y
  un `calcular.py` que los recalcula. Mínimo 40 (SC-002), incluidos los de la investigación con sus
  valores: 481.244; 488.088; 79.741 × 6 = 478.446; 79.111; 6.884; 3.649; 2.465.306; 2.486.777; 6.676;
  41.952; más retención con y sin umbral, con y sin componente inflacionario, persona exenta, prórroga con
  capitalización, redención anticipada, tramos, bisiesto, cambio de tasa, revalorización limitada y
  prorrateada, fondo de liquidez por bandas, tasa negociada, cargos con gratuitos y exoneración.
- **Trazas**: FR-026, SC-002, SC-019.

### R13 · Tasa negociada

- **Decisión**: `NegotiatedRateRule` por línea con criterios (antigüedad mínima, rangos de monto y plazo,
  tasa base, saldo total del titular, oficina, canal) y margen máximo en puntos EA. `TasaNegociada` (puro)
  elige la regla más específica. La aprobación usa la matriz de atribuciones de la Cartera con el tipo
  `NegotiatedRate` (dimensiones: puntos y monto); por encima, aprobador presente con segundo factor o
  sesión con acta. Lo sellado: tasa de tabla, margen, regla, aprobador, motivo.
- **Trazas**: FR-127, D34.

### R14 · Cargos

- **Decisión**: `DepositLineCharge` (catálogo por versión de línea, apagado por defecto) y `AccountCharge`
  (causado: cobrado, pendiente, condonado, reversado). Si está gravado, la Cartera emite
  `CargoDeCaptacionCausadoV1` a Comercial, que factura; el reverso emite `CargoDeCaptacionReversadoV1` para
  la nota crédito. Comercial devuelve el estado de la factura por mensaje (`FacturaDeCargoRegistradaV1`),
  sólo informativo.
- **Por qué**: decisión del dueño (pregunta 4) y D20 de la 013.
- **Trazas**: FR-128, D35.

### R15 · Retiro del asociado

- **Decisión**: `MembershipWithdrawal` con estados `Filed → Approved → Settled → Paid` (y `Desisted`,
  `Rejected`, `ToRedo`). La aprobación es un `SessionItem` de un órgano de la Cartera (consejo o junta).
  Al aprobar se emite `RetiroDeAsociadoAprobadoV1` a Asociados (Core), que cambia el estado. La
  liquidación arma `MembershipWithdrawalLine` por concepto; el cruce usa la aplicación de pagos del crédito
  con medio `InternalOffset`, en una operación que toma cerrojos de crédito y cuentas; el reverso del pago
  arrastra el débito y deja el retiro en `ToRedo`. La devolución sale por orden de giro.
- **Trazas**: FR-076 a FR-080, FR-124, D16.

### R16 · Débito automático

- **Decisión**: `DirectDebitMandate` (cuenta, crédito o todos los del titular, débito parcial sí/no,
  vigencia). El paso diario, en la fecha exigible, debita y aplica el pago como operación interna
  (`InternalOffset`), con la clave `CAR:debito-automatico:{loanPublicId:N}:{fecha}`.
- **Trazas**: FR-090, D25.

### R17 · Pagadurías

- **Decisión**: el convenio de pagaduría de K2 (`PayrollAgreement`, `PayrollAgreementVersion`) suma
  `PayrollAgreementFileGroup` (formato de envío y de respuesta como `COR_BankFileFormats` con `Scope`
  nuevo `PaymasterDeduction` —**reemplazado por C01**—, calendario) y `PayrollAgreementConceptEquivalence` (código de la pagaduría →
  lista de líneas y conceptos, regla de reparto `Priority` o `Proportional`, prelación). El generador de
  archivos de K2 agrupa por grupo de archivo; la carga de respuesta reparte por la equivalencia y aplica:
  créditos por el contrato de obligaciones, cuotas de cuentas por el contrato de cuentas, en la misma
  unidad de trabajo. Lo no mapeado va a «no aplicados». El motor de archivos planos es el de la plataforma
  (`FlatFileWriter`), con lectura de respuesta nueva (`FlatFileReader`).
- **Trazas**: FR-125, FR-126 (E-05).

### R18 · Nómina

- **Decisión**: dos conceptos nuevos `DESC_APORTES` y `DESC_AHORROS` (`WellKnownConceptCodes`), cuyo
  valor sale de `ListarExigibles` del contrato de cuentas para el empleado y que se aplican por
  `ConsignarAsync` al aprobar la corrida, en la misma unidad de trabajo, como `DESC_CARTERA`. Lo no
  descontado queda exigible con motivo.
- **Trazas**: FR-089, FR-122 (E-02).

### R19 · Contabilidad

- **Decisión**: mismos mensajes de la Cartera (emisor `CAR`), con contratos nuevos en
  `Application/Common/Integration/Contracts/Lending/Deposits/*V1.cs`. La matriz `ACC_LendingPostingRules`
  suma **dimensiones** al final de la clave posicional (`LineNature`, `DepositClass`, `IssueTermBand`,
  `ContributionClass`, `AccountFlag`) y **roles** al final de `RolesDeCuentaDeCartera`:
  `DepositPrincipal`, `DepositInactivePrincipal`, `InterestPayable`, `InterestExpense`, `WithholdingPayable`,
  `GmfPayable`, `BonusExpense`, `ChargeIncome`, `ChargeReceivable`, `ContributionCapital`
  (por clase), `RevaluationFund`, `SurplusFund`, `RefundPayable`, `UnclaimedPayable`,
  `GarnishmentPayable`, `PenaltyIncome`. Los pesos de especificidad de la 013 no cambian; las dimensiones
  nuevas son exigidas por igualdad cuando el rol las declara. Tipos de comprobante nuevos: `MA`
  (movimientos de cuentas), `CC` (causación de captaciones, por lotes resumido por día), `TD` (CDAT),
  `AO` (aportes y asamblea), `RA` (retiro de asociado). La plantilla sugiere las cuentas CUIF de la
  investigación §3.9 sin activarlas.
- **Por qué**: reutiliza consumidor, resolutor, validación previa, conciliación y guarda de cierre.
- **Alternativas**: matriz propia de captaciones (duplica todo el lado contable).
- **Trazas**: FR-096 a FR-100, FR-123 (E-03).

### R20 · Consulta de aportes

- **Decisión**: `ConsultaDeAportesDeCartera` responde desde `MemberAccountDailyPosition` (o reconstrucción
  si la fecha no tiene posición) en cooperativas con aportes activados. El DTO suma `Minimum` y
  `InstallmentsInArrears` al final (versión del contrato 2; la 1 sigue para quien no la adopte).
  `ContextoDeAportes` (D19 de la 013) deja de forzar los parámetros a falso: los lee de los parámetros de
  Cartera `Riesgo.ExcepcionAportesArrastre` y `Riesgo.DescuentoAportesDeterioro`.
- **Trazas**: FR-094, C2.

### R21 · Activación por naturaleza

- **Decisión**: `ActivateDepositsCommand` y `ActivateContributionsCommand` con la misma regla de
  `ActivateLendingCommand` (corte + 1, acta de diferencias, cuadre recalculado, plazo de fecha valor, mes
  abierto). Requisitos: créditos activados primero (los cruces y la consulta los necesitan), participantes
  registrados (Recaudo, Tesorería, Nómina) y, en producción, las entregas K7 a K11 completas (Q1).
- **Trazas**: FR-116, FR-093, Q1, Q3.

### R22 · Mayoría de edad

- **Decisión**: el paso diario compara la fecha de nacimiento de la persona (Personas) con el parámetro
  `MAYORIA_DE_EDAD_ANIOS` (18, con fuente); al cumplir, cierra la vigencia del representante, marca la
  cuenta `RequiresDataUpdate` y avisa a ambos. Retiros del representante en espera se anulan.
- **Trazas**: FR-033.

### R23 · Inembargabilidad

- **Decisión**: `LimiteInembargable` suma los saldos de ahorro de la persona en la cooperativa y retiene
  lo que excede `LIMITE_INEMBARGABLE` (valor nacional nuevo) salvo embargo por alimentos; el orden de
  cuentas lo da el parámetro `EMBARGO_ORDEN_CUENTAS`. Los aportes nunca. Cada respuesta al juzgado se
  emite como documento.
- **Trazas**: FR-055, D20.

### R24 · Informes y documentos

- **Decisión**: vistas nuevas de `VistaDeInformeDeCartera`: `deposit-balances`, `deposit-transactions`,
  `deposit-accounts-by-status`, `term-deposit-maturities`, `depositor-concentration`,
  `interest-and-withholding`, `gmf`, `contributions-by-member`, `installments-in-arrears`, `unclaimed`,
  `garnishments`, `negotiated-rates`, `charges`, `liquidity-fund`, `insured-deposits`,
  `deposits-reconciliation`, `sicses-captaciones`, `sicses-saldos-diarios`, `sicses-fondo-liquidez`,
  `exogena-1019`, `exogena-1020`. Los formatos regulatorios son layouts versionados como dato
  (`Domain/Common/Layouts`), con `verified = false` y alerta «formato sin cotejar» hasta cotejarlos.
  Documentos con `IssuedDocumentKind` nuevos y QuestPDF, con la mecánica de la 013.
- **Trazas**: FR-101 a FR-112.

### R25 · Fondo de liquidez

- **Decisión**: parámetros con vigencia por categoría (`FONDO_LIQUIDEZ_*`) con la tabla del Decreto 976 de
  2026 y su transición; `FondoDeLiquidez` calcula por clase y madurez residual (intereses causados y por
  causar hasta el vencimiento contractual en plenas). Instantánea inmutable por fecha.
- **Trazas**: FR-025, FR-109.

### R26 · Valores nacionales y parámetros nuevos

- **Valores nacionales** (`CodigosDeValorNacional`, replicados): `COMPONENTE_INFLACIONARIO`,
  `LIMITE_INEMBARGABLE`, `TOPE_ENTREGA_SIN_SUCESION`. Ya existen `UVT`, `IPC`, `SMMLV`, `FESTIVO`.
- **Parámetros de la Cartera con vigencia y fuente** (módulo `LND`, «pendiente de validar» al sembrar):
  `RETENCION_TARIFA_AHORRO`, `RETENCION_TARIFA_CDAT`, `RETENCION_TARIFA_CONTRACTUAL`,
  `RETENCION_TARIFA_PERMANENTE`, `UMBRAL_RETENCION_UVT_DIA`, `RETENCION_ALCANCE_UMBRAL`,
  `RETENCION_COMPONENTE_INFLACIONARIO_APLICA`, `GMF_TARIFA`, `GMF_TOPE_EXENCION_UVT`,
  `GMF_POLITICA_EXENCION`, `GMF_HECHOS_GENERADORES`, `FONDO_LIQUIDEZ_*`, `TOPE_CUOTA_FONDO_EMPLEADOS_PCT`,
  `FRACCION_MINIMA_APORTES`, `LIMITE_PARTICIPACION_APORTES_PN`, `LIMITE_PARTICIPACION_APORTES_PJ`,
  `MAYORIA_DE_EDAD_ANIOS`, `EMBARGO_ORDEN_CUENTAS`, `FECHA_VALOR_DIAS_CAPTACIONES`.
- `LaCarteraNoTieneValoresLegalesFijos` cubre `Domain/Lending/Deposits` y `Application/Lending/Deposits`.
- **Trazas**: FR-014, FR-015.

### R27 · Migración

- **Decisión**: plantillas nuevas en el registro de plantillas de la plataforma: `lending.deposit-lines`,
  `lending.contribution-lines`, `lending.member-accounts`, `lending.member-account-parties`,
  `lending.term-deposits`, `lending.contribution-accounts`, `lending.account-installments`,
  `lending.holds-and-garnishments`, `lending.gmf-exemptions`, `lending.tax-year-to-date`. Filas de
  preparación `LND_Migration*Row`. Revisión previa, todo o nada, errores por fila y columna, saldo inicial
  por concepto sin hechos, cuadre contra los saldos contables de las cuentas mapeadas por la matriz, carga
  reemplazable. La plantilla `lending.contribution-balances` de la 013 se acepta como fuente de los saldos
  de aportes.
- **Trazas**: FR-113 a FR-118.

### R28 · Retiro de lo heredado

- **Decisión**: una migración par destructiva con guarda `RetiroDeCaptacionesHeredadas` que borra las 38
  tablas conservadas (lista de `CarteraHeredada` menos las de crédito ya retiradas) y las 7 `CDT_*`. Se niega
  si alguna tiene filas sin la fila `COR_SystemSettings` `LND.CaptacionesHeredadas.RetiroAprobado`;
  diagnóstico SQL en `specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql`. Se retiran el
  código de `Domain/Entities/Savings`, `Domain/Entities/CDT`, `Application/Savings`, `Application/CDT`,
  `Endpoints/Savings`, `Endpoints/CDT`, `Pages/AhorrosYAportes`, el módulo de auditoría `Savings` y la
  exención por nombre de `/api/savings/*`. `LND_SiplaGroupParameters`, `LND_SiplaParameters`,
  `LND_MoneyLaunderingDeclarations`, `LND_UnusualTransactions*` y `LND_Blacklist` también se retiran (Q2:
  lo que esta feature necesita de SIPLA se renueva como parámetros de efectivo y origen de fondos).
- **Por qué**: Q2 (retirar todo y renovar lo necesario); Principio XII.
- **Trazas**: FR-119, FR-120.

### R29 · Pruebas de arquitectura

- **Se enmiendan**: `ElEspacioLendingEsSoloCartera` (ya no hay conservado: se convierte en
  `LaCarteraHeredadaNoVuelve` ampliada a las 45 tablas), `LaCarteraSoloSeTocaPorSusInterfaces` (seis
  puertos: suma `IContratoDeCuentas`; `IConsultaDeAportes` pasa a la lista),
  `CarteraNoConoceContabilidad` (sin cambios, cubre el espacio nuevo), `ElMotorDeCarteraEsDecimal`
  (cubre `Domain/Lending/Deposits`), `EnumeracionesDeCarteraTests` (enums nuevos).
- **Nuevas**: `NadieEscribeElLibroDeCuentasFueraDelRegistro`, `LasCuentasSeTocanSoloPorElContrato`
  (Nómina, Recaudo, Tesorería), `LaRetencionYElGmfSoloLosCalculanSusMotores`,
  `LasCaptacionesNoUsanLaUsura` (ninguna ruta de captación llama a `ControlDeUsura`).
- **Trazas**: FR-008, FR-121.

### R30 · Pruebas de integración

- **Decisión**: colección «Captaciones e2e» sobre `CentralIdentityApiFixture` con una cooperativa aislada
  (créditos activados primero), en PostgreSQL y SQL Server. Cubre cada historia por HTTP con el recaudador
  y la tesorería de prueba, y una prueba de volumen (`RUN_PERF_TESTS=1`) para la causación de 100.000
  cuentas (SC-006).
- **Trazas**: SC-001 a SC-019.

### R31 · Pantallas

- **Decisión**: pantallas en `Pages/Cartera/Captaciones` con `IndicadorDeCarga` por zona, `PersonSearchPicker`,
  `PermissionGate` y los componentes de la Cartera; el catálogo de líneas común (`/cartera/lineas`) filtra
  por naturaleza y abre el editor de la versión que corresponde. `LasPantallasDicenQueEstanCargando` suma
  el módulo. Todo enlace del menú tiene su página (`TodoEnlaceDelMenuTieneSuPagina`).
- **Trazas**: FR-016.

---

## 2. Conciliación con data-model.md (2026-10-14)

`data-model.md` se verificó contra el código y estas decisiones **reemplazan** lo dicho arriba donde
choquen:

- **C01 · Ámbito de archivo de pagaduría** (enmienda R17): se reutilizan los ámbitos de `BankFileScope`
  que la 013 fija para K2 (valores 5 y 6, que nacen con K2); no se agrega `PaymasterDeduction`.
- **C02 · Claves de parámetro** (enmienda R26): siguen la convención `Grupo.Nombre` del catálogo de la
  013; las que son pisos o techos legales se registran como límites (`LIMITE`). Los códigos en mayúsculas
  de R26 son nombres de referencia; los definitivos están en `data-model.md` §9.
- **C03 · Clave de la matriz** (R19): por posición, como `LendingPostingRule.ClaveDe` en el código; las
  cinco dimensiones nuevas van al final y la clave de las filas existentes se recalcula en la migración.
- **C04 · `AccountFlag`**: un solo valor con precedencia `Garnished` > `Inactive` > `GmfExempt`; si la
  contadora necesita combinarlas, pasa a tres booleanos (queda para su validación de la matriz).
- **C05 · Órdenes de giro de cuentas**: tabla propia `LND_AccountPayoutOrders`, con el mismo mensaje de
  orden de giro de la 013; `LND_DisbursementOrders` no se generaliza.
- **C06 · Sujetos de atribución**: no hay enum `AttributionKind`; los sujetos son textos de
  `LendingDecisionSubjects` (se agregan `DepositWithdrawal`, `NegotiatedRate`, `MembershipWithdrawal`,
  `DeceasedDelivery`, `ChargeWaiver`, `TermDepositRedemption`, `ContributionRefund`,
  `AssemblyDistribution`, `GarnishmentRegistration`).
- **C07 · Tablas agregadas**: `LND_AccountInstallmentNonDeductions`, `LND_AccountPayoutOrders`,
  `LND_IssuedDocumentAccounts`, `LND_GarnishmentResponses`, `LND_AccountChargeExemptions` y **nueve** filas de
  preparación de migración (`data-model.md` §7.1, incluida `LND_MigrationTermDepositRows` de K9). Total: **46 tablas nuevas** (K7 19, K8 17, K9 4, K10 4,
  K11 2) y 19 tablas existentes con columnas o valores nuevos.
- **C08 · Migraciones pares por entrega**: `CaptacionesAportes`, `CaptacionesAhorro`, `CaptacionesCdat`,
  `CaptacionesAsamblea`, `CaptacionesLiquidez`, más `RetiroDeCaptacionesHeredadas`; reemplazan el nombre
  único `CarteraCaptaciones` del plan.
- **C09 · Valor nacional adicional**: `TOPE_SEGURO_DEPOSITOS`; parámetros adicionales de efectivo, mínimo
  irreducible de la entidad, soportes de entrega sin sucesión y traslado de cuentas abandonadas.

## 3. Conciliación con contracts/ (2026-10-14)

- **C10 · Idioma de los roles de la matriz**: el código de la 013 nombra sus 26 roles en español
  (`Capital`, `InteresPorCobrar`…). Los roles nuevos se implementan **en español** con esta
  correspondencia; los nombres en inglés de R19 y de `contracts/contabilidad.md` son referencias:

  | Referencia (R19, contracts) | Nombre en el código |
  |---|---|
  | `DepositPrincipal` | `CapitalDeCaptacion` |
  | `DepositInactivePrincipal` | `CapitalDeCaptacionInactiva` |
  | `InterestPayable` (rol) | `InteresPorPagar` |
  | `InterestExpense` | `GastoPorIntereses` |
  | `WithholdingPayable` | `RetencionPorPagar` |
  | `GmfPayable` | `GmfPorPagar` |
  | `BonusExpense` | `GastoPorBonificacion` |
  | `ChargeIncome` | `IngresoPorCargos` |
  | `ChargeReceivable` | `CargoDeCuentaPorCobrar` |
  | `ContributionCapital` | `AportesSociales` |
  | `RevaluationFund` | `FondoDeRevalorizacion` |
  | `SurplusFund` | `FondoDeExcedentes` |
  | `RefundPayable` | `DevolucionPorPagar` |
  | `UnclaimedPayable` | `NoReclamadosPorPagar` |
  | `GarnishmentPayable` | `EmbargosPorConsignar` |
  | `PenaltyIncome` | `IngresoPorPenalidad` |
  | `AbsorbedLoss` | `PerdidaAbsorbida` |

  Los nombres de concepto del libro (`MemberAccountConcept.InterestPayable`…) siguen en inglés: son otro
  enum.
- **C11 · Orden de giro**: `OrdenDeGiroEmitida` v2 (tipo de orden y cuenta); los desembolsos siguen en v1.
- **C12 · Hechos genéricos**: `CaptacionReclasificada` (con causa: plazo de emisión, inactividad,
  embargo, exención) y `OperacionDeCuentaReversada` (reverso de cualquier operación de cuenta).
- **C13 · Recaudador nuevo**: `ClaseDeRecaudador.Treasury = 6` al final, para transferencias que recibe
  Tesorería; `PagoAplicado` pasa a `AdmiteProceso = true` sólo para `OwnLoanPayoff` (débito automático).
- **C14 · Canje**: se libera solo al cumplirse los días hábiles de la clase de medio
  (`Captaciones.DiasDeCanjePorClaseDeMedio`); el contrato v1 no tiene «confirmar».
- **C15 · Comercial es el destino `Inventory`** (la 012 vive en ese módulo): `CargoDeCaptacionCausado`
  y `…Reversado` van `CAR → Inventory`; la respuesta `FacturaDeCargoRegistrada` va `INV → Lending`.
- **C16 · Asociados como destino**: destino `Associates` con consumidor `DestinoAsociados`, participante
  `ASO` y recibo `COR_AssociateInboundMessages` (enmienda E-04).
- **C17 · Tipos de comprobante heredados**: `AH` y `CD` de la semilla de la 009 quedan inactivos; los
  nuevos son `MA`, `CC`, `TD`, `AO`, `RA`.
- **C18 · Cifras de contratos**: 168 rutas nuevas en `/api/lending` y `/api/reports/lending` más 1 en
  Nómina; 74 retiradas sin alias; 39 permisos nuevos; 41 mensajes (33 hechos); 17 roles; 5 dimensiones
  (14 a 18); 5 tipos de comprobante; tabla de Nómina `PAY_PayrollAccountDeductions`.
- **C19 · Orden del retiro de lo heredado**: va **al principio** (fase fundacional), antes de crear las
  tablas nuevas, como el retiro de la 013: las pantallas nuevas reutilizan las rutas `/cartera/ahorros*` y
  `/cartera/aportes` de las heredadas, y el diagnóstico de DEV, QA y producción se hace antes de construir.

## 4. Correcciones tras /speckit-analyze (2026-10-15)

- **C20 · `AccountFlag`** (reemplaza C04): valores `Standard`, `GmfExempt`, `Garnished`, precedencia `Garnished` >
  `GmfExempt` > `Standard`, exigido por el rol del capital. La **inactividad cambia el rol** del capital
  (`CapitalDeCaptacionInactiva`, CUIF 210510), no la marca. Así queda en data-model, mensajes y contabilidad.
- **C21 · Layouts regulatorios**: viven en `Application/Lending/Regulatory/Layouts/` con el catálogo
  `CatalogoDeLayoutsDeCartera` (como dice `contracts/plantillas-y-archivos.md`), sobre el motor de layouts de
  `Domain/Common/Layouts`; reemplaza la ubicación de R24 y de la tarea de K11.
- **C22 · K2 de la 013 es prerrequisito**: las pagadurías configurables (FR-125, E-05) extienden el convenio de
  pagaduría de K2 (`PayrollAgreement`, ámbitos 5 y 6). Como la 014 empieza con la 013 completa (Q3), K2 existe;
  si al empezar no existiera, las tareas de E-05 pasan a la 013.
- **C23 · Código corto `CAV`** para `CaptacionesActivadas` (el `CTA` chocaba con el documento cruce `CTA`).
- **C24 · Prefijos de error**: además de `Lending.Savings.*`, `Lending.TermDeposit.*` y `Lending.Contributions.*`
  (FR-013) se admiten `Lending.Gmf.*`, `Lending.Holds.*`, `Lending.Garnishment.*` y `Lending.DepositLine.*`, como
  fija `contracts/api.md`.
- **C25 · Desviaciones constitucionales** (registradas en plan.md, Complexity Tracking): las mismas que aprobó la
  013 (T101 y la causación por lotes), extendidas a captaciones.
- **C26 · Parámetros agregados**: `Captaciones.AnticipacionAvisoCambioDeTasaDias` (FR-038),
  `Captaciones.DiasPlazoDevolucionAportes` (FR-079, plazo estatutario), `Captaciones.DiasParaNoReclamados`
  (FR-081) y `Riesgo.DescuentoAportesDeterioro` (FR-094, junto al existente de arrastre). La confirmación de la
  contadora sobre cuentas abandonadas (FR-084) es el parámetro `CuentasAbandonadas.TrasladoHabilitado` (falso).
- **C27 · Tipos de documento**: son **9** (`IssuedDocumentKind` 7 a 15, incluida la respuesta al juzgado).

## 5. Enmienda tras verificar contra el código (2026-10-18, T001)

Verificado contra la rama `014-ahorros-aportes-cdt` en `b2481d0f` (la 013 completa por merge: K1 a K6b). **Manda el código**; estas
notas reemplazan lo dicho arriba, en `data-model.md`, `contracts/` y `tasks.md` donde choquen.

- **E01 · La 013 está construida.** Sus tareas abiertas son del dueño o de ambiente (T0183, T0198, T0855, T0856, T0895, T0931,
  T0952, T0960–T0962 —los dos documentos de T0960 y T0961 existen—, T1011, T1041, T1057, T1071, T1078–T1089): no impiden empezar.
  **K2 existe** (`PayrollAgreement`, `BankFileScope.PayrollDeductionRequest` 5 y `PayrollDeductionResult` 6): C22 se cumple y E-05
  queda en la 014.
- **E02 · Nombres que siguen iguales**: `CreditLine`, `CreditLineVersion`, `LendingSetup`, `LoanTransaction`, `RegistroDeCartera`
  (`Application/Lending/Ledger`), `CicloDiarioDeCartera` (`Application/Lending/Accrual`), `CierreMensualDeCartera`
  (`Application/Lending/Risk`), `LendingPostingRule.ClaveDe` (14 partes por posición: hecho, rol y las 12 dimensiones de
  `DimensionDeCartera` 1–12; `Operation` es la 13 del enum), `RolesDeCuentaDeCartera` (26 roles en español y 8 familias),
  `LendingDecisionSubjects` (clase de constantes de texto, C06), `ReconstruccionDeCredito` (`Domain/Lending/Replay`),
  `MotorDeCartera`, `CerrojoDeCartera` y `SqlDelCerrojoDeCartera` (`Persistence/Lending`), `ICerrojoDeCartera` y
  `NumeradorDeCartera` (`Application/Lending/Common`), `ConsumoDeCartera`, `ResolutorDeReglasDeCartera`, `OperacionesDeCartera`,
  `VistaDeInformeDeCartera`, `ValidadoresDelContrato`, `ContratosDeIntegracion`, `ParametrosDeCartera`, `CodigosDeValorNacional`,
  `LendingPermissionCatalogSeeder`, `PrecisionDeCartera`, `VigenciaBitemporal`. `IConsultaDeAportes` y
  `ConsultaDeAportesNoDisponible` siguen en `Application/Common/Integration/Savings` (T041 los muda).
- **E03 · `LendingNumberKind` 10 a 15 ya están ocupados** por la 013 (`LoanNovelty` 10, `Waiver` 11, `WriteOffBatch` 12,
  `PortfolioEvaluation` 13, `PaymentAgreement` 14, `LegalProcess` 15). Las clases de número de captaciones van **al final**:
  `MemberAccount` **16**, `MemberAccountOperation` **17**, `MembershipWithdrawal` **18**, `AssemblyDistribution` **19**,
  `DeceasedHolderCase` **20** (reemplaza data-model §1.3 y §10).
- **E04 · `ClaseDeRecaudador` vive en Application**, en `Common/Integration/Lending/EnumsDelContratoDeCartera.cs` (no en
  `Domain/Enums`): `Receipts` 1, `Payroll` 2, `Contributions` 3, `OwnLoanPayoff` 4, `Test` 5; `Treasury` **6** se agrega allí (T014).
- **E05 · `AccountingFactPartSource`** tiene 1 a 5 (`CloseReopening` 5): `AccountCatchUp` es **6**.
- **E06 · `EntregaDeCartera`** llega hasta `K6b` = 7 y `CatalogoDeCartera.EntregaVigente = K6b`. Las entregas de esta feature se
  agregan al final: `K7` 8, `K8` 9, `K9` 10, `K10` 11, `K11` 12. Los parámetros y valores nacionales de la 014 se declaran con su
  entrega y, como en la 013 (`CadaParametroRespetaLaEntregaDeSuModulo`), **no se ofrecen ni se aceptan** hasta que la entrega que los
  habilita suba `EntregaVigente` (eso lo hace cada entrega, no T014–T017).
- **E07 · Las plantillas de rol no viven en el sembrador**: viven en `Application/Security/Roles/Plantillas/PerfilesSugeridos.cs`
  (módulo `Lending`). `CreateSuggestedLendingRolesCommand` (sin claves) crea todas las del módulo y el requisito de activación
  `LendingRolesCreated` las exige todas: con las cuatro de captaciones son **17** y el asistente de activación las crea juntas. Las e2e
  de la 013 que decían «trece» se ajustan a 17 (T018).
- **E08 · Casos dorados de crédito**: ya no son 32. `Domain.Tests/Lending/Casos` tiene 45 JSON (más los de libranza, novedades,
  riesgo, puntaje, cobranza, sesiones y atribuciones en sus carpetas). SC-019 se lee como: **toda** la suite
  `dotnet test tests/IngenIA365ERP.Domain.Tests --filter Lending` sigue verde sin tocar ningún JSON (T013).
- **E09 · `ContextoDeAportes`** es el record de dominio `Domain/Lending/Risk/ContextoDeAportes.cs`
  (`RigeD19`, `IndicadoresDisponibles`); la bandera que lo fuerza es la constante `ContextoDeRiesgo.RigeD19 = true` de
  `Application/Lending/Risk/ContextoDeRiesgo.cs`. T084 toca los dos. `Riesgo.ExcepcionAportesArrastre` ya existe (K1).
- **E10 · El «sembrado» de parámetros es el catálogo**: `ParametrosDeCartera.Definiciones` declara cada clave con tipo, defecto seguro
  (texto), ámbitos, entrega y `PendienteDeValidar`; los pisos y techos legales se acotan con `LimitesLegales` (valor nacional `LIMITE`)
  y la fila con vigencia la escribe la cooperativa por `COR_ParameterVersions`. No hay valores legales en el código: los defectos son
  texto. Las claves definitivas de §9.1 (convención `Grupo.Nombre`, C02) quedan en la tabla de E15.
- **E11 · Migraciones**: la última de la 013 es `CarteraVivienda` (`20261017062235` PostgreSQL / `20261017062205` SQL Server). El
  retiro de la 014 es `RetiroDeCaptacionesHeredadas` (`20261018143710` / `20261018143627`), en commit propio; `CaptacionesAportes`
  va después. Las de la 013 están congeladas (T0802).
- **E12 · Permisos**: el sembrador de la 013 tiene **123** códigos (api.md §2 lo da bien); con los 39 de la 014, 162.
- **E13 · Auditoría**: la regla `.Savings` → `Savings` de `ModuloDeAuditoria` se retiró con lo conservado (T007); todo lo de
  captaciones es `Lending` (encadenado, diez años).
- **E14 · Efectos fuera de Cartera del retiro** (T007), declarados en `docs/operaciones/captaciones-retiro-heredado.md`: la tarjeta
  débito (`/api/debit/cards`) leía `LND_SavingsAccounts`; hasta que su propia feature la vincule a una cuenta de captación (spec, «Fuera de
  esta feature») responde `DebitCard.SavingsAccountUnavailable`. `GetPersonDetailQuery` cuenta 0 ahorros hasta T043.
  `AccountReferenceFinder` perdió sus ramas de parámetros de ahorro y CDT. Salieron además `Pages/CDT` (2 pantallas) con su grupo de
  menú «CDT» y `API/Reports/PersonPortfolioReport.cs` (los informes PDF pasan de 12 a 11).
- **E15 · Claves definitivas de los parámetros** (T017; convención `Grupo.Nombre` de C02; reemplaza los nombres en mayúsculas de R26 y
  data-model §9.1). Entre paréntesis, el defecto sembrado «pendiente de validar» y la entrega; «vacío» = sin valor (rige el límite legal
  o no aplica hasta que la cooperativa lo fije).

  | Referencia (R26, §9.1, C26) | Clave | Defecto · entrega |
  |---|---|---|
  | `RETENCION_TARIFA_AHORRO` · `_CDAT` · `_CONTRACTUAL` · `_PERMANENTE` | `Retencion.TarifaAhorro` · `.TarifaCdat` · `.TarifaContractual` · `.TarifaPermanente` | 0,07 K8 · 0,04 K9 · vacío K9 · vacío K8 (ámbitos general y línea) |
  | `UMBRAL_RETENCION_UVT_DIA` · `RETENCION_ALCANCE_UMBRAL` · `RETENCION_COMPONENTE_INFLACIONARIO_APLICA` | `Retencion.UmbralDiarioUvt` · `.AlcanceDelUmbral` · `.ComponenteInflacionarioAplica` | 0,055 · `ExceptTermDeposit` · true (K8) |
  | `GMF_TARIFA` · `GMF_TOPE_EXENCION_UVT` · `GMF_POLITICA_EXENCION` · `GMF_HECHOS_GENERADORES` | `Gmf.Tarifa` · `.TopeExencionUvt` · `.PoliticaDeExencion` · `.HechosGeneradores` | 0,004 · 350 · `SingleMarkedAccount` · JSON de D32 (K8) |
  | `FONDO_LIQUIDEZ_*` | `FondoDeLiquidez.PorcentajesPorCategoria` (JSON) | vacío (K11) |
  | `TOPE_CUOTA_FONDO_EMPLEADOS_PCT` · `FRACCION_MINIMA_APORTES` · `LIMITE_PARTICIPACION_APORTES_PN` · `_PJ` | `Aportes.TopeCuotaFondoEmpleados` · `.FraccionMinima` · `.LimiteParticipacionPersonaNatural` · `.LimiteParticipacionPersonaJuridica` | vacío (K7); son pisos y techos legales: los acotan los `LIMITE` nuevos `APORTES_TOPE_CUOTA_FONDO_EMPLEADOS` (0,10 sembrado), `APORTES_FRACCION_MINIMA` (0,10 sembrado), `APORTES_LIMITE_PARTICIPACION_PN` y `_PJ` (sin valor, P12) |
  | `APORTES_MINIMO_IRREDUCIBLE_ENTIDAD` | `Aportes.MinimoIrreducibleEntidad` | vacío (K7) |
  | `MAYORIA_DE_EDAD_ANIOS` · `FECHA_VALOR_DIAS_CAPTACIONES` | `Captaciones.MayoriaDeEdadAnios` · `Captaciones.DiasMaximosFechaValor` | 18 · 5 (K7) |
  | canje (C14) · `EFECTIVO_UMBRAL_DECLARACION_ORIGEN` · `EMBARGO_ORDEN_CUENTAS` | `Captaciones.DiasDeCanjePorClaseDeMedio` · `Efectivo.UmbralDeclaracion` · `Embargos.OrdenDeCuentas` | `{"Check": 3, "*": 0}` K8 · vacío K7 · vacío K8 |
  | `Riesgo.DescuentoAportesDeterioro` | igual | false (K7) |
  | `ENTREGA_SIN_SUCESION_SOPORTES` · `CUENTAS_ABANDONADAS_TRASLADO_HABILITADO` | `Fallecimiento.SoportesDeEntregaSinSucesion` · `CuentasAbandonadas.TrasladoHabilitado` | vacío · false (K10) |
  | C26 | `Captaciones.AnticipacionAvisoCambioDeTasaDias` · `.DiasPlazoDevolucionAportes` · `.DiasParaNoReclamados` | vacío K8 · vacío K7 · vacío K10 |

  Valores nacionales (T016): `COMPONENTE_INFLACIONARIO` (clave = año gravable, fracción, K8; semilla 2025 = 0,5543 del Decreto 898 de 2026,
  que el sembrador deja en espera hasta K8), `LIMITE_INEMBARGABLE` (K8), `TOPE_ENTREGA_SIN_SUCESION` (K10) y `TOPE_SEGURO_DEPOSITOS` (K11),
  sin semilla los tres últimos. El sembrador central ya no registra error por un código de una entrega posterior: lo deja en espera.

## 6. Enmienda del libro de cuentas (2026-10-18, T021–T043)

Construido el bloque B de la fase fundacional. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md` y `contracts/` donde
choquen.

- **E16 · La operación no nombra su orden de giro.** `LND_MemberAccountOperations.PayoutOrderId` (data-model §4.1) no existe: la operación es
  hecho de sólo inserción y la orden necesita la operación (`LND_AccountPayoutOrders.OperationId`, único) para nacer, así que el enlace de ida y
  vuelta obligaría a reescribir el hecho. Se navega desde la orden. Tampoco nacen todavía, por la regla de §0.9 y §8.2, `GarnishmentId` y
  `AccountChargeId` (K8), `AssemblyDistributionId` y `DeceasedHolderCaseId` (K10) de la operación, `TermsVersionId` (K9) de la transacción, ni
  `GarnishmentId` y `DeceasedHolderCaseId` de la orden.
- **E17 · FK de K7 que esperan su tabla.** `MemberAccountOperation.MembershipWithdrawalId` y `AccountPayoutOrder.MembershipWithdrawalId` nacen como
  columnas nulas con índice y su FK la pone T096 (`LND_MembershipWithdrawals`); `MemberAccountTransaction.InstallmentId` (bigint), igual con T081
  (`LND_AccountInstallments`). Como `CaptacionesAportes` es regenerable hasta T056, la FK entra en la misma migración.
- **E18 · Únicos de hechos sin filtro de baja.** `UK_LND_Activations_Nature` va sin `[IsDeleted] = 0` (data-model §1.8 dice «filtrado vivos»):
  la activación es hecho y los únicos de hechos no filtran por baja lógica (`ModeloDeCarteraNucleoTests`); como un hecho nunca se da de baja, es
  lo mismo. Los dos FK autorreferidos de la operación llevan nombre propio (`FK_LND_MemberAccountOperations_Reverses` / `_ReversedBy`): los
  generados pasaban de 63 caracteres y PostgreSQL los truncaba al mismo prefijo.
- **E19 · Lo que cambia en la 013 por las columnas nuevas.** `LND_PayrollAgreementVersions.OutboundFormatId`/`ResultFormatId` pasan a nulos con
  `CK_LND_PayrollAgreementVersions_Formats` (sin grupos, los dos; con grupos, ninguno); la consulta del convenio los lee nulos y el resto de K2 los
  sigue leyendo de una versión sin grupos. Los tres únicos de `LND_MigrationLoads` pasan a `(Nature, Status)`: las consultas de la 013 que buscan
  «la carga aplicada» o «la activada» siguen sin filtrar por naturaleza porque hoy sólo hay cargas de colocación; la activación de captaciones
  (T055, US9) las acota a `Nature = Placement` al nacer la primera carga de otra naturaleza.
- **E20 · La reconstrucción de la cuenta trabaja sobre el libro.** `ReconstruccionDeCuenta` (T033) recibe las filas de
  `LND_MemberAccountTransactions` con signo (`MovimientoDeCuenta`) y suma las de fecha valor ≤ D registradas hasta R; el espejo de un reverso lleva la
  fecha valor de su original, así que lo anula desde que se registra y no antes (SC-001). R04 decía «operaciones y versiones de línea»: en K7 la cuenta
  no causa (los aportes no tienen tasa) y el interés diario de K8 (`InteresDeCaptacion`) recibe la serie `SaldosDiarios` y devuelve filas
  `InterestAccrued`, que entran al libro y a esta misma suma. La versión de reglas que sella es la de la Cartera (`VersionesDeReglas.Vigente`) mientras
  ninguna regla de captación cambie un centavo. Códigos nuevos: `Lending.Savings.LedgerInconsistent` (un espejo sin su original o que no lo anula
  exactamente) y `Lending.Savings.AmountTooPrecise` (data-model §0.3); el reverso de un reverso es `Lending.Reversal.OfReversal` en el motor (013) y
  `Lending.Savings.ReversalOfReversal` en el registro y el contrato (contrato-de-cuentas §3.11).
- **E21 · Numeración.** `LendingNumberKind` 18–20 (retiro, asamblea, fallecimiento) se piden con `NumeroDeProcesoDeCaptacionesAsync` y, hasta que sus
  tablas nazcan (US10, K10), el numerador no salta números existentes de esas clases. La orden de giro de cuentas usa el consecutivo de las de
  desembolso (data-model §3.9) y el numerador salta los números de las dos tablas.
- **E22 · Puertos (T039–T043).** `ContratoDeCuentasSinActivar` vive en `Application/Lending/Integration` (tasks.md; contrato-de-cuentas §2 lo ponía en el
  espacio del puerto) porque, para responder «con el código de la naturaleza» (§3.13), lee la naturaleza de la cuenta por su `PublicId` —sólo lectura,
  sin seguimiento—; una cuenta que no existe responde como captación, y el traslado con una cuenta de aportes, como aportes. El record del motor que la
  reconstrucción devuelve se llama `SaldosDeCuenta` (no `EstadoDeCuenta`): ese nombre es el enum del puerto (§3.2). La consulta real de productos
  (`ProductosDeCaptacionDeCartera`, detrás de `ConsultasDeCartera.ProductosDeCaptacionAsync`) toma el estado de cada naturaleza de su fila en
  `LND_Activations.Nature` hasta que T053 traiga las fechas a `LND_Setup`; el saldo es la suma del libro a la fecha y el disponible el de la última
  posición. `MaturityDate` va nulo hasta K9 y `MigratedNotActivated` falso hasta la migración de captaciones (US9). `GetPersonDetailQuery` ya cuenta
  las cuentas de ahorro vivas y suma ahorros y aportes por esta consulta (E14 pedía esperar a T043); la baja de persona (`Person.HasLiveDepositProducts`)
  sigue en T102. Tesorería (`ClaseDeRecaudador.Treasury`) recauda con `Transfer` y `BankDeposit` y su clave es `TES:{documento:N}:{cuenta:N}`; la
  matriz de medios es la misma de los dos contratos, así que Tesorería también puede pagar créditos por `IContratoDeObligaciones` si adopta
  `ProveedorDeObligaciones` (research C13 dice «a cuentas y créditos»).
- **E23 · Pruebas de arquitectura (T035–T038).** `LaRetencionYElGmfSoloLosCalculanSusMotores` se fija así: todo archivo de producción que arma filas
  `WithholdingApplied` o `GmfCharged` nombra su motor (`RetencionSobreRendimientos`, `GravamenMovimientosFinancieros`), salvo `RegistroDeCuentas`, que
  sólo valida la pareja; y los dos motores, cuando nazcan (K8), son clases estáticas de `Domain/Lending/Deposits` sin base ni reloj.
  `LasCaptacionesNoUsanLaUsura` recorre las carpetas de captaciones (aplicación, dominio, entidades, rutas y pantallas). `LasCuentasSeTocanSoloPorElContrato`
  vigila Nómina y Tesorería, y la carpeta de Recaudo (`Application/Receipts`), que todavía no existe. La ampliación de `LosHechosDeCarteraNoLlevanCuentas`
  admite en `Contracts/Lending/Deposits` la referencia a la cuenta del asociado (`MemberAccountRefV1`, `AccountPublicId`, `AccountNumber`, `AccountCount`):
  ahí `Account` no es una cuenta del plan. `NadieEscribeElLibroDeCarteraFueraDelRegistro` (013) admite que `RegistroDeCuentas` mueva las marcas que
  la cuenta comparte de nombre con el crédito (`LastAccruedDate`, `LastRegisteredAt`, `LastPositionDate`), y sólo eso.

## 7. Enmienda de la contabilidad base, el ciclo y la activación (2026-10-18, T044–T059)

Construido el bloque C de la fase fundacional. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md` y `contracts/` donde
choquen.

- **E24 · El error de naturaleza se llama `Accounting.LendingRule.LineNatureMismatch`.** `contracts/contabilidad.md` §4.1 lo nombraba
  `Accounting.LendingRule.NatureMismatch`, pero ese código ya es, desde la 013, el **aviso** «la naturaleza de la cuenta no es la del rol»
  (`ReglasDeLaMatrizDeCartera.AvisoNaturalezaDistinta`). Una regla de un rol de captaciones con `lineNature = Placement` responde
  `LineNatureMismatch` (422, en `ReglasDeLaMatrizDeCartera`); un valor fuera de su enum en cualquiera de las cinco dimensiones nuevas, el
  `DimensionCodeUnknown` de la 013 (el contrato decía `InvalidDimensionValue`), y una exigida en `*`, `DimensionRequired` (el contrato decía
  `RequiredDimension`).
- **E25 · `EntregaDelHecho` numera como `EntregaDeCartera`.** El contrato (§3) decía `K7 = 7 … K11 = 11`, pero el 7 ya es `K6b`: son
  `K7 = 8`, `K8 = 9`, `K9 = 10`, `K10 = 11`, `K11 = 12` (E06).
- **E26 · Los 33 hechos nacen declarados en la fase fundacional.** El catálogo de mensajes (`TipoDeMensaje.Record`), `LND_FactTypes` y la tabla
  hecho × rol nombran el `Type` de un record, así que T048 crea los 33 records `…V1` en `Contracts/Lending/Deposits` con el contenido de
  `mensajes.md` §4.0/§4.1 y sus bloques auxiliares (`CoveredInstallmentV1`, `InternalWithdrawalV1`, `AccrualDayV1`, `TermDepositFundingV1`,
  `TermDepositDestinationV1`, `NegotiatedRateRefV1`, `TermDepositAmountChangeV1`, `TermDepositPenaltyV1`, `ContractualBonusV1`,
  `AccountChargeRefV1`, `ChargeCollectedV1`, `AssemblyDistributionRefV1`, `MembershipWithdrawalRefV1`, `WithdrawalOffsetV1`, `WithdrawalLossV1`,
  `GarnishmentRefV1`, `DepositFactMirrorV1`, `DepositLoadTotalV1`), como la 013 declaró en K1 los de K3 y K4. Las historias que los **emiten**
  (T085, T112, T134, T174, T183) pueden completarlos mientras ninguno se haya emitido. Decisiones de detalle: los valores de enum viajan como
  texto (`string`), igual que en la 013; los hechos que viajan como acompañante **o** como parte (`AjusteDeCausacionDeCaptacion`,
  `InteresDeCaptacionLiquidado`, `RetencionPracticada`) tienen la forma de parte —la emisión de una operación es una parte de una línea—, con la
  clave `ConfirmacionOPorPublicId`; `CaptacionesActivadas.AccountCount` (no `accounts`). Admiten proceso (`EmisionesDeProcesoPermitidas`) los 15 de
  la columna «Proceso» de §1.1. Los acompañantes llevan en `LND_FactTypes`, en el mapeo y en `OperacionesDeCartera` el comprobante y el modo de su
  principal más frecuente (`GmfRetenido` y `CargoPendienteCobrado` `MA`; `RetencionPracticada` y `AjusteDeCausacionDeCaptacion` `CC` por lotes;
  `PenalidadAplicada` `TD`; `PerdidaAplicadaAlRetiro` `RA`); rige el del principal (C01 de la 013). `OperacionDeCuentaReversada` hereda el modo y,
  en su fila, `MA`: tomar el comprobante del original es del constructor de US8 (T142, T143).
- **E27 · Roles y hechos en el código.** Los 17 roles de C10 con sus exigidas de `contabilidad.md` §2 (`CapitalDeCaptacion` exige clase y marca y,
  en CDAT y contractual, el plazo de emisión por `DimensionesDe(rol, motivo, claseDeCaptacion)`; los demás, como la tabla). Motivos:
  `IngresoPorCargos` los `ChargeKind`; `DevolucionPorPagar` los `OrderKind` de `mensajes.md` §5.3 salvo `LoanDisbursement`. `OperacionesDeCartera`
  pasa de 37 a 70 con la tabla de §3 en español; las «cuentas destino» del CDAT (`CapitalDeCaptacion`, `DevolucionPorPagar`, `RecaudoPorAplicar`) y
  el espejo del reverso (todos los roles de captaciones y el puente, en los dos lados) son listas. `LosHechosDeCarteraNoLlevanCuentas` admite en
  `Contracts/Lending/Deposits` la propiedad de tipo `MemberAccountRefV1`, `AccountFlag` y «Cuenta» en el nombre de un hecho (es la del asociado:
  `CargoDeCuentaCausado`, `OperacionDeCuentaReversada`); el barrido de fuente de la 013 deja esa carpeta a su regla propia.
- **E28 · Semillas contables (T046).** `voucher-types.json` suma `MA`, `CC`, `TD`, `AO`, `RA` (29 tipos) y una columna `active`: `AH` y `CD` nacen
  inactivos y, si una cooperativa ya los tenía sembrados y activos **sin comprobantes**, la semilla los inactiva (lo único que cambia de lo
  existente; con comprobantes los deja y lo registra, `[Semilla.TipoDeComprobanteRetiradoEnUso]`). El mapeo suma los 30 hechos de negocio con
  cruce `CTA` y dos excepciones de Nómina (`ConsignacionEnCuentaAplicada` y `AporteRecibido` → `DN`): 66 filas.
- **E29 · Lo que queda para US8.** La regla y su API (alta, versión, consulta, resolución, catálogo) ya llevan las cinco dimensiones
  (`DimensionesDeReglaDeCarteraDto`, `DimensionesDeReglaDeCarteraGuardadaDto`, `ValoresDeCartera`, el catálogo de valores); la **plantilla y la
  importación** de la matriz las suman con su versión nueva (T144), los DTO de `Shared` y las pantallas con T149, y el constructor de líneas de los
  hechos de captaciones con T143 (hoy un hecho de captaciones no arma líneas: nadie lo emite todavía). `IDimensionesDeCartera.CatalogoAsync`
  devuelve las líneas de las tres naturalezas con `LineaDeCarteraDto.LineNature`; `depositClass` llega con las versiones de línea de captación (K8).
- **E30 · Ciclo diario y cierre (T051, T052).** El paso de captaciones corre al terminar la corrida `DailyCycle`, después de la causación de todos
  los créditos y antes de la muestra de verificación (`PasosDelCiclo.Captaciones = 9`, al final para no renumerar). Recorre las cuentas vivas de las
  naturalezas que rigen el día d en tandas de `Lending:Process:BatchSize` con su propio cursor en memoria —`LND_ProcessRuns` no gana columnas— y entrega
  cada tanda a los `ISubpasoDeCaptaciones` que registre cada historia (ninguno en la fase fundacional); lo que falla queda en `LND_ProcessRunErrors` con
  `MemberAccountId`. Si K8 necesita repartir las cuentas entre varias tandas del conductor, el cursor pasa a la corrida con una migración de su entrega.
  La sección del cierre (`SeccionDeCaptacionesDelCierre`) va en la transacción final antes del mismo `CierreDeCarteraRealizado` y deja en los totales de la
  corrida las naturalezas que cerró; `CarteraEnElCierre` cuenta `CaptacionesActivadas` como activación para `LendingNotClosed` y avisa
  `LendingLoadWillExpire` también con `CargaDeCaptacionesAplicada`.
- **E31 · Activación sin carga (T053–T055).** `LND_Setup` suma las seis columnas de data-model §1.2 y `ActivacionesDeCaptacion` es su único lector (también
  de `ProductosDeCaptacionDeCartera`, que hasta aquí deducía el estado de `LND_Activations`, pendiente 2 del bloque B). La activación sin carga (FR-117)
  escribe una fila de `LND_Activations` **sin carga y sin acta**: `MigrationLoadId` y `SessionItemId` pasan a nulos (el único de la carga se filtra a
  `IS NOT NULL`) porque sin carga no hay diferencias que aprobar; el corte es la víspera de la fecha elegida. No nace ninguna carga de otra naturaleza,
  así que las consultas de la 013 sobre `LND_MigrationLoads` (pendiente 1 del bloque B) siguen sin tocar hasta US9; la única lectura de la 013 que tomaba
  «la última activación» (`ConstructorDeDocumentosDeCartera`, ventas fuera de Cartera) filtra ya `Nature = Placement`. Errores: `Lending.Activation.LoansFirst`
  (créditos sin activar o después de la fecha), `Lending.Activation.LoadNotAvailable` (**nuevo**: con carga, hasta US9), `.CutoffInvalid` (fecha futura),
  `.AlreadyActivated` (con `data.nature`), `.RequirementsMissing` y `Lending.Savings.DeliveryNotAvailable` (`data: { nature, availableFrom }`, también para
  aportes, que llegan con K7; captaciones con K8) **en todo ambiente**: es lo que el despliegue trae. Por eso la fixture «Captaciones e2e» sólo activa con K8
  desplegado, que es lo que habrá en la corrida final.
- **E32 · Requisitos (T054).** `AccountingMonthOpen` sigue la decisión del dueño del 2026-10-14 para créditos (un mes cerrado no limita y avisa);
  `AccountsCollectorRegistered` exige la adopción de `ProveedorDeCuentas` en cualquier ambiente y, en producción, un recaudador real;
  `TreasuryDestinationRegistered` en producción exige `OrdenDeGiroEmitida` v2 adoptada, así que la v2 (mensajes.md §5.3) se declara aquí:
  `OrdenDeGiroEmitidaV2` en `Contracts/Lending`, en el catálogo (v2, admite proceso), y `TesoreriaDePrueba` la acepta; la emite US10.
  `PayrollConceptsReady` no se puede evaluar sin leer `PAY_` y sale como aviso. Sólo en producción (fuera queda `Waived`): `DeliveriesComplete` (K11
  desplegada, Q1), `ProfileAllowsClasses` (captaciones: el perfil vigente con `MayTakeDeposits`; el alta del perfil regulatorio acepta ya los cinco
  campos de data-model §1.8) y `LinesWithMatrix`, que pregunta a `IContabilidadParaCartera.EvaluarAsync` por el hecho principal de cada línea activa de la
  naturaleza —hasta que el constructor de captaciones de US8 (T143) arme líneas la respuesta no prueba nada, pero en producción nada se activa sin K11—.
  Por lo mismo la fixture e2e no siembra matriz: fuera de producción `LinesWithMatrix` queda dispensado y la cooperativa opera con
  `Contabilidad.FaltaDeRegla = Advertir`.
- **E33 · El cambio a las implementaciones reales.** Como en la 013, la implementación real responde «no habilitado» mientras su naturaleza no tenga fecha en
  `LND_Setup`: `ContratoDeCuentasDeCartera` (T083) y `ConsultaDeAportesDeCartera` (T084) leen `ActivacionesDeCaptacion` y se registran en lugar de
  `ContratoDeCuentasSinActivar` y `ConsultaDeAportesNoDisponible`. T055 deja la fecha que las cambia; el registro de la DI es de esas tareas.
- **E34 · `CaptacionesAportes` regenerada (T056).** Con las columnas de T044 (cinco en `ACC_LendingPostingRules`), T053 (seis en `LND_Setup`) y T055
  (`LND_Activations.MigrationLoadId` y `SessionItemId` nulos, el único de la carga filtrado), ahora `20261018170720` PostgreSQL / `20261018170634` SQL Server
  (paridad OK, el scaffold no trajo cambios ajenos). La migración de datos de T044 es SQL a mano al final de `Up` —y su inversa al principio de `Down`—, en
  los dos motores: sólo toca las claves de 14 posiciones, así que repetirla no hace nada; al regenerar se vuelve a poner. El cliente `CaptacionesClient`
  (T057) nace con la activación y el grupo de menú «Captaciones y aportes» nace sin mostrarse (`HayEnlacesDeCaptaciones = false`) hasta que la primera
  historia le ponga un enlace: un grupo vacío en el menú no lleva a ninguna parte.

## 8. Enmienda de las líneas de captación y de aportes (2026-10-18, T060–T071, US1)

Construida la historia 1. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen.

- **E35 · Las tres tablas de US1 van en `CaptacionesAportes`.** T063 decía que las versiones de captación iniciaban la migración par `CaptacionesAhorro`
  (C08); por decisión del dueño de esta fase todo cambio de esquema va a `CaptacionesAportes` mientras sea regenerable (T056): `LND_DepositLineVersions`,
  `LND_DepositLineRateTiers` y `LND_ContributionLineVersions` nacen allí (`20261018174431` PostgreSQL / `20261018174400` SQL Server). Si al congelarla
  K8 necesita su migración propia, `CaptacionesAhorro` nace con lo que K8 sume después. Dos FK llevan nombre propio
  (`FK_LND_DepositLineRateTiers_Version`, `FK_LND_ContributionLineVersions_PermanentSavingsLine`): los generados pasaban de 63 caracteres (E18).
- **E36 · Códigos de error.** Donde `data-model.md` y `contracts/api.md` no coinciden mandan los del contrato: `Lending.DepositLine.Retroactive` y
  `Lending.ContributionLine.Retroactive` (no `Lending.CreditLine.RetroactiveVersion`), `Lending.DepositLine.ClassImmutable` (no
  `Lending.Savings.ClassChanged`), `Lending.DepositLine.VersionOverlaps` / `Lending.ContributionLine.VersionOverlaps` y `Lending.Line.NotFound` (404).
  Lo mal formado responde `Lending.DepositLine.Invalid` / `Lending.ContributionLine.Invalid` (422, `data.errors[{ field, code, message }]`, como las líneas
  de crédito). Se conservan de `data-model.md` `Lending.Savings.RateTiersOverlap`, `Lending.TermDeposit.LineIncomplete` (también para el contractual:
  el prefijo de §0.5 cubre CDAT y contractual), `Lending.Contributions.FractionBelowLegal` y `Lending.Savings.RateChangeNoticeTooShort`. Nuevo:
  `Lending.DepositLine.ReferencedLineInvalid` (la línea a la vista destino de un CDAT o la del ahorro permanente de los aportes no es de la clase que se
  pide).
- **E37 · La clase vive en la versión.** `LND_CreditLines` no gana columna de clase: la clase de una línea de captación es la de su primera versión y todas
  la repiten (`ClassImmutable`). El filtro `depositClass` de la lista y el `depositClass` de la respuesta la leen de las versiones.
- **E38 · Activar una línea.** El alta lleva `active`: sin él la línea queda en borrador (`Draft`); `POST /{id}/reopen` reabre una cerrada **y también
  activa una en borrador**, revalidando lo mismo que el alta activa: la entrega desplegada (aportes con K7, captaciones con K8,
  `Lending.Savings.DeliveryNotAvailable` con `data: { nature, availableFrom }`, en todo ambiente como E31), el perfil (captación) y la única línea de
  aportes activa (`Lending.Contributions.LineAlreadyActive`). No se exige la naturaleza activada en `LND_Setup` (data-model §1.1): la activación de la
  naturaleza pide líneas activas con matriz (`LinesWithMatrix`), y exigirlo al revés sería circular. Con `EntregaVigente = K6b`, hoy un despliegue crea las
  líneas pero no las activa; las pruebas fijan la entrega.
- **E39 · Perfil regulatorio.** El perfil de la 013 no distingue clases: `MayTakeDeposits` autoriza las cuatro y, si no, `allowedClasses` va vacío
  (US1-1: «sólo puede ofrecer aportes»). `DepositsFromAssociatesOnly` fuerza «exige asociado» en cada versión. Un fondo de empleados es un perfil con
  `EmployeeFundCategory`.
- **E40 · Topes de aportes.** El tope de la cuota de un fondo de empleados (`Aportes.TopeCuotaFondoEmpleados`, acotado por `APORTES_TOPE_CUOTA_FONDO_EMPLEADOS`)
  se controla en la línea sólo si la cuota es fracción del ingreso; con valor fijo o salarios mínimos depende del ingreso de cada asociado y lo controla la
  cuota (US3). La fracción mínima a aportes (`Aportes.FraccionMinima`) es la parte de la cuota que no va a ahorro permanente. Los dos, sólo en fondos de
  empleados, leídos a la fecha desde la que rige la versión, con la fuente del límite que rigió. El límite de participación propio de la línea se compara
  con el más estricto de persona natural y jurídica (`Lending.Parameter.BelowLegalFloor`).
- **E41 · Aviso de cambio de tasa (T067, FR-038).** Se avisa sólo en una línea a la vista que no declara tasa pactada, cuando la tasa cambia (clase, tasa,
  índice, margen, piso, techo, modo o tramos; no el orden) y hay titulares o cotitulares vigentes de cuentas vivas. La anticipación es la mayor entre
  `Captaciones.AnticipacionAvisoCambioDeTasaDias` (K8: no se acepta hasta subir `EntregaVigente`, y mientras tanto rige la de la línea) y
  `RateChangeNoticeDays` de la versión. El aviso es el informativo `CAMBIOTASA` (plantilla nueva, tres canales, «pendiente de validar»; la semilla pasa de
  28 a 31 filas) por la mensajería de la Cartera (`ColaDeMensajesAlDeudor`), por correo, con origen `DepositRateChange`; la cola admite como destinatario de
  un informativo sin crédito al titular o cotitular de una cuenta viva, y la lista blanca de los informativos suma `NumeroDeCuenta`, `NombreDeLaLinea`,
  `TasaAnterior`, `TasaNueva` y `FechaDelCambio`. Nunca tumba la versión: lo que no se encola queda en el log.
- **E42 · Rutas y cuerpos de §3.** Hechas las de K7/K8 que no dependen de otra historia: lista (`nature`, `status`, `depositClass`, `includeClosed`; sólo
  las naturalezas cuyo `View` tiene quien pregunta), detalle con `date` y `recordedAsOf` (bitemporal), alta de captación y de aportes, versiones de cada
  naturaleza (lista, detalle, alta) y cerrar/reabrir (12 rutas, `LinesEndpoints.cs`). Quedan para sus historias: reglas de tasa negociada (K9, US7),
  cargos (K8, US4, T114), la cotización `rate-quote` (con el motor de interés de K8) y las plantillas `lending.deposit-lines` y `lending.contribution-lines`
  (US9). Los cuerpos siguen las columnas de data-model: `rate.kind` es `Fixed`/`Variable` y los tramos son una lista dentro de la tasa (el ejemplo decía
  `"Tiered"`); `tax.withholdingRateParameterCode` (vacío = la tarifa de la clase) en lugar de `withholdingClass`; `breachEffect` con los valores de
  `ContractualBreachEffect`; los cargos llegan con US4; `ContributionLineVersionInput` es `{ validFrom, reason, minimumNonReducible { kind, amount,
  fraction }, installment { kind, amount, fraction, roundingUnit, roundingMode, periodicity, dueDay, minimumFirst }, permanentSavingsShare?
  { depositLinePublicId, fraction }, allowsExtraordinary, maxParticipationFraction, excessAction, allowsTransferBetweenMembers, refundTermDays,
  lossDeductionEnabled }`.
- **E43 · La pantalla del catálogo común.** `/cartera/lineas` ya era la de líneas de crédito (013) y una página no comparte ruta con otra: T070 la vuelve el
  catálogo común con selector de naturaleza (`?naturaleza=credito|captacion|aportes`, cada una para quien tiene su `View`; sin la consulta de crédito
  abre en la primera que la persona sí puede ver). Lo de captación y aportes es el componente `Pages/Cartera/Captaciones/LineasDeCaptacion.razor` (lista,
  alta, detalle con la versión a una fecha y su historial, versión nueva que parte de la vigente, cerrar y reabrir/activar) con el editor
  `EditorDeLineaDeCaptacion.razor`; las tasas se digitan en puntos y viajan como fracción. El grupo de menú «Captaciones y aportes» se muestra con
  «Líneas de captación» y «Líneas de aportes» (enlaces a la misma página con su naturaleza); «Líneas de crédito» sigue en el grupo «Cartera».

## 9. Enmienda de la apertura de cuentas (2026-10-18, T072–T080, US2)

Construida la historia 2. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen.

- **E44 · Conocimiento, habeas data y origen de fondos en la apertura.** El conocimiento del asociado (SARLAFT) es de su propia feature (spec, «Fuera de
  esta feature») y no hay en el código una fecha de «conocimiento al día»: la apertura exige los **datos mínimos de conocimiento** de la persona —documento,
  nombres y apellidos o razón social y, si es persona natural, la fecha de nacimiento, sin la cual no se sabe si es menor— (`Lending.Savings.MemberDataIncomplete`,
  `data.missing`). La autorización de tratamiento (FR-006) es la finalidad `GeneralTreatment` del titular y de cada cotitular (`Lending.Authorization.Missing`
  con `personPublicId`). La declaración de origen de fondos se pide al abrir sólo si el depósito de apertura de la línea alcanza `Efectivo.UmbralDeclaracion`
  (`Lending.Savings.SourceOfFundsRequired`); no tiene columna en la cuenta: viaja en el pedido y queda en la auditoría del comando. La de cada consignación
  en efectivo (FR-059) es del contrato de cuentas (US4).
- **E45 · Por qué una cuenta nace en apertura.** `data-model.md` §3.1 no tiene una bandera de «exige aprobación» en la línea: una cuenta nace `Opening` por
  una **coincidencia en listas** del titular o de un cotitular (la «apertura excepcional» de FR-005) o porque la versión exige **depósito de apertura**; el
  detalle lo dice en `openingBlockers` (`ScreeningMatch`, `InitialDeposit`). Las listas se consultan con el valor nuevo `ScreeningContext.AccountOpening = 5`
  (al final del enum; la columna es entera y no tiene restricción); una coincidencia deja el bloqueo automático `RestrictiveListMatch` y la alerta de la 013, la
  libera sólo el oficial por `/api/lending/screening-results/{id}/release`, y `approve-opening` exige que ya no haya bloqueos sin levantar
  (`Lending.Screening.Blocked`, 422). La segregación usa `LND_CaseParticipations` con el expediente `MemberAccount`: abrir es `Radicar` y aprobar es `Decidir`
  (la regla fija de la 013), así que la apertura exige un usuario con persona (`Lending.Security.UserWithoutPerson`). Con el canal de listas caído no se abre
  (`Lending.Adapter.Unavailable`, como el cupo). Una cuenta aprobada que todavía espera el depósito de apertura sigue `Opening`: la activa el primer depósito
  por el contrato de cuentas (US4).
- **E46 · Número.** El consecutivo por línea lleva como prefijo el código de la línea y un guion (`AHVISTA-000001`, a lo sumo 17 caracteres): sin prefijo, dos
  líneas se saltarían los números la una a la otra en el único sin filtro de `LND_MemberAccounts.Number`. Toda cuenta abierta tiene número, también la
  retenida: no hay borradores de apertura.
- **E47 · Titularidad.** Las reglas son puras (`Domain/Lending/Deposits/ReglasDeTitularidad`). La proporción del titular es el resto de las de los cotitulares:
  todas o ninguna (sin ninguna, por igual, y el titular absorbe el redondeo a seis decimales); sin cotitulares queda nula (`ProporcionesCuadran` ya lo admitía).
  Códigos nuevos: `Lending.Savings.JointRequiresTwoSigners` (manejo conjunto con un solo firmante), `.PartyRepeated` (una persona, un papel; el titular no repite),
  `.PartyInvalid` (proporción fuera de un cotitular, límite fuera de un firmante, fundamento desconocido, representante menor de edad),
  `.LegalEntitiesNotAllowed` y `Lending.Contributions.OnlyHolderAndBeneficiaries`. En aportes sólo hay titular y beneficiarios (data-model §3.2), así que los
  aportes de un menor **no** llevan representante en la cuenta. Los beneficiarios se digitan en porcentaje (hasta 4 decimales) y suman exactamente 100 %; se
  guardan como fracción; la constancia es opcional al abrir y obligatoria al cambiarlos (`Lending.Savings.BeneficiaryRecordRequired`). `PUT /{id}/parties` recibe
  el conjunto completo: la parte igual sigue, lo demás se cierra la víspera y nace hoy; los beneficiarios cambian en conjunto (`BeneficiarySetNumber` + 1).
  `MemberAccountParty.Terminar` admite cerrar la víspera de su inicio (una parte reemplazada el mismo día no rigió nunca y no se borra).
- **E48 · Exención del GMF.** `LND_GmfExemptAccounts` es de K8 pero nace en `CaptacionesAportes` (la decisión de E35). Los comandos llevan el nombre del contrato
  (`MarkGmfExemptAccountCommand`, `EndGmfExemptAccountCommand`; tasks.md decía `Unmark…`); el alta suma `replaceCurrent` para cambiar la marca en el mismo
  comando (data-model §3.8), y nacen `Lending.Gmf.Retroactive` (no se marca ni se termina hacia atrás), `.AlreadyEnded` y `.NotFound`. La política se lee de
  `Gmf.PoliticaDeExencion` (K8): antes de su entrega el lector no la acepta y rige el defecto `SingleMarkedAccount`. `GET /gmf-exemptions/usage` llega con el
  GMF de K8 (US4).
- **E49 · Mayoría de edad.** `MayoriaDeEdad` es el primer `ISubpasoDeCaptaciones` registrado. Cierra al representante la víspera del cumpleaños (aunque el
  proceso corra después), marca `RequiresDataUpdate` desde el cumpleaños y avisa al titular y al representante con el informativo nuevo `MAYOREDAD` (tres canales,
  «pendiente de validar»; la semilla pasa de 31 a 34 filas; origen `MemberAccountMajority`; la lista blanca suma `FechaDeMayoriaDeEdad`); la cola admite como
  destinatario de un informativo sin crédito al representante legal vigente y el aviso se encola antes de cerrar su vigencia. `POST /{id}/data-update`
  (api.md lo ponía en K8) nace aquí porque es la salida de esa marca. Los retiros del representante en espera de aprobación nacen con K8 (US4).
- **E50 · Soportes de la titularidad.** El soporte de la representación, la constancia de beneficiarios, la firma, la foto y la declaración de origen de fondos
  son adjuntos con dueño el **titular** (`LendingAccountPartySupport`; la cuenta todavía no existe al subirlos): se leen con `Lending.Accounts.Read`, se suben con
  `Lending.Accounts.Open` o `Lending.Accounts.Parties.Manage` y no se borran.
- **E51 · Rutas.** Ocho en `/api/lending/accounts` (lista, conteos, detalle, apertura, aprobar la apertura, partes GET y PUT, actualización de datos) y tres en
  `/api/lending/gmf-exemptions` (lista, marcar, terminar). El saldo de la lista y del detalle es la suma del libro (data-model §0.2) y el disponible es el mismo
  hasta que K8 traiga la posición diaria con sus retenciones. La marca de causación inicial del control es la fecha de apertura.
- **E52 · Lo que la apertura todavía no abre.** Un CDAT se constituye por su ruta (`Lending.Savings.UseTermDepositRoute`); el contractual sella sus condiciones
  y llega con K9 (`Lending.Savings.DeliveryNotAvailable`, `availableFrom = K9`). Una cuenta se abre con la fecha de hoy (`Lending.Savings.OpeningDateInvalid`).
  Otros códigos nuevos: `Lending.Savings.LineNotOpen`, `.LineWithoutVersion`, `.OfficeNotAllowedByLine`, `.HolderDeceased`, `.PermanentAlreadyOpen`.
- **E53 · `CaptacionesAportes` regenerada (T072).** Con `LND_GmfExemptAccounts`, ahora `20261018184016` PostgreSQL / `20261018183933` SQL Server (paridad OK, el SQL
  de datos de T044 conservado, el scaffold sin cambios ajenos).

## 10. Enmienda de aportes: cuota, recaudo, consulta y pagadurías (2026-10-18, T081–T095, US3)

Construida la historia 3. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen.

- **E54 · Ingreso para la cuota.** La cuota «porcentaje del ingreso» toma el ingreso registrado del asociado, `COR_Associates.ExternalSalary`; sin él la cuota
  no se genera y el ciclo lo informa (`Lending.Contributions.IncomeMissing`). El SMMLV sale de los valores nacionales; sin él, `NationalValues.Missing`.
- **E55 · Ahorro permanente sin cuenta.** La cuota de aportes guarda las dos partes (`ContributionShare`, `PermanentSavingsShare`); la del permanente es otra
  cuota de la cuenta permanente, enlazada. Si el asociado no tiene cuenta permanente viva, sólo se exige la parte de aportes (queda en el log del ciclo).
- **E56 · Contrato de cuentas real en K7.** `ContratoDeCuentasDeCartera` reemplaza al sin activar: consulta, exigibles, simulación y consignación en aportes y en
  las cuotas de ahorro permanente y contractual. Los depósitos libres, retiros y traslados en cuentas de ahorro responden `Lending.Savings.DeliveryNotAvailable`
  (K8) y aportes no se retira por el contrato (`Lending.Contributions.NotWithdrawable`). `Free` en aportes es extraordinario; un sobrepago sin extraordinarios
  admitidos se rechaza (`Lending.Contributions.OverpaymentNotAllowed`) —no se usa saldo a favor—. La validación previa contable es la de A8 de la 013. La
  atribución por montos de efectivo (§1.6) llega con US4. El límite de participación sólo se aplica con capital social mayor que cero (el primer aporte de una
  cooperativa nueva no puede exceder un capital que no existe).
- **E57 · Saldo sin posiciones.** La consulta de aportes v2, el resumen, los informes, la constancia y el paz y salvo suman el libro con fecha valor ≤ corte;
  las posiciones diarias las escribe el paso de captaciones desde K8. Mismo saldo para todos (SC-010).
- **E58 · Devoluciones y cesiones.** Tabla nueva `LND_ContributionRequests` (Kind, Status, causa, punto del acta, decisión). La devolución sin retiro sólo se paga
  por orden de giro en K7 (`TransferToAccount` → K8); la aprueba otra persona (expediente `ContributionRequest`); el exceso de participación que no existe
  responde `Lending.Contributions.NoParticipationExcess`. Se suma `POST …/{id}/reject` (api.md no lo traía) para negar con motivo.
- **E59 · Nómina recauda aportes y ahorros (E-02).** Conceptos sembrados `DESC_APORTES` y `DESC_AHORROS` (deducciones de valor fijo, elemento DIAN
  «Deducciones/Cooperativa»); origen de novedad `AccountDeduction = 7` con `AccountPublicId`/`AccountNumber`; tabla `PAY_PayrollAccountDeductions` gemela de la de
  créditos. Prelación **fija** aportes → créditos → ahorros (`PayrollCalculationEngine.PrelacionDeCartera`) mientras no exista la política
  `PrelacionDescuentosDeCartera`. Aprobar consigna `Installments` con clave `NOM:{corrida}:{cuenta}`, fecha valor el fin del período (la fecha de Nómina no tiene
  el tope de «futura» ni de «días hacia atrás»: sólo el corte de la activación) y lo no descontado cuota por cuota en orden de vencimiento. Rechazo: la línea baja y
  el neto sube (`Payroll.AccountDeductionNotApplied`); «no contabilizable»: falla la aprobación (`Payroll.AccountDeductionNotPostable`); reversar la nómina
  reversa cada consignación, todo o nada (`Payroll.AccountReversalRejected`). La liquidación definitiva **no** recauda cuentas en K7 (excluye estas novedades).
  La migración adopta `ProveedorDeCuentas` v1 para el participante `NOM` (SQL a mano en `CaptacionesAportes`).
- **E60 · Constancia y paz y salvo.** Tabla de enlace `LND_IssuedDocumentAccounts` sin `TermsVersionId` (llega con K9). La firmeza del paz y salvo se mide por la
  fecha valor de la última operación, no por su registro. Reversar una operación de cuenta anula los documentos que la citaban.
- **E61 · Pagadurías configurables (T090 completo, T091 parcial).** Grupos y equivalencias cuelgan de la versión del convenio; fijarlos registra una versión
  nueva que copia la vigente. Reglas puras en `Domain/Lending/Payroll/EquivalenciasDePagaduria` (JSON canónico, `Lending.PayrollAgreement.ConceptMappedTwice`,
  `.EquivalenceWithoutItems`, `.EquivalencePriorityRepeated`, `.EquivalenceWithoutSplit`, `.LineWithoutGroup`, `.FileGroupDuplicate`, `.EquivalenceDuplicate`) y el
  reparto `Priority`/`Proportional` (al centavo, residuo a la de mayor pendiente, el exceso sobra). **Pendiente**: la generación por grupo, `DescuentosPorPagaduria`
  v2, la lectura de la respuesta con equivalencias y su aplicación por los dos contratos. Falta un dato de diseño: una cuenta de aportes no tiene vínculo con un
  convenio (el `LoanPayrollLink` es del crédito), así que no se sabe qué cuotas de cuentas van en el archivo de una pagaduría. Mientras tanto, generar con un
  convenio que usa grupos responde `Lending.PayrollFile.FileGroupsPending`. `identityEquivalences` y `causeEquivalences` de api.md §18.3 no se construyeron.
- **E62 · Riesgo.** La excepción de arrastre (capital ≤ aportes) y el descuento de aportes en el deterioro individual se leen de parámetros cuando aportes está
  activado; sin ellos rige D19. La pérdida esperada (Anexo 2) **no** descuenta aportes (pregunta para la contadora).
- **E63 · Rutas y pantallas.** `/api/lending/contributions` (resumen, cuotas, devoluciones y cesiones con aprobar y negar), §18.3 en `/payroll-agreements` y
  `/api/payroll/runs/{runId}/account-deductions`. Pantallas `/cartera/aportes` (resumen y solicitudes por aprobar) y `/cartera/aportes/{persona}` (ficha); la
  pestaña «Grupos y equivalencias» en `/cartera/pagadurias/{id}`. `asCollateralOf` del resumen llega con la garantía de aportes (K8).
- **E64 · `CaptacionesAportes` regenerada (US3).** Con `LND_AccountInstallments`, `LND_AccountInstallmentNonDeductions`, `LND_ContributionRequests`,
  `LND_IssuedDocumentAccounts`, `LND_PayrollAgreementFileGroups`, `LND_PayrollAgreementConceptEquivalences`, `PAY_PayrollAccountDeductions` y las columnas
  `AccountPublicId`/`AccountNumber` de `PAY_Novelties`; ahora `20261018204949` PostgreSQL / `20261018204907` SQL Server (paridad OK, el scaffold sin cambios
  ajenos). Conserva el SQL de datos de T044 y suma, a mano, la adopción de `ProveedorDeCuentas` v1 por el participante `NOM` (y su retiro en `Down`).

## 11. Enmienda del retiro del asociado (2026-10-18, T096–T107, US10)

Construida la historia 10. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen.

- **E65 · Las líneas de la liquidación.** Una versión de la liquidación (`LND_MembershipWithdrawalLines`) lleva una línea por concepto de cada cuenta liquidada
  (`ContributionBalance`, `SavingsBalance`, en +), por concepto del descuento por pérdidas (`LossDeduction`, −), por cuenta, concepto y crédito del cruce
  (`LoanOffset`, −) y por cuenta y concepto de lo que se devuelve (`Refund`, **informativa en +**). La regla de data-model §6.2 se lee como: Σ de las líneas que
  **no** son `Refund` = `RefundAmount`. La reliquidación tras `ToRedo` escribe otra versión (`SettlementVersion + 1`); las líneas son hecho.
- **E66 · Códigos.** Se usan los de api.md §11 y se suman `Lending.MembershipWithdrawal.{NotSettled, EffectiveDateInvalid, SettlementDateInvalid,
  LossDeductionNotEnabled, OffsetRejected, NothingToRefund, AccountsClosed, InvalidTransition}`, `Lending.Document.IssuedAutomatically`,
  `Associate.{NotFound, WithdrawalDateMismatch, WithdrawalOwnedByLending}` y `Person.HasLiveDepositProducts`. La fecha de la liquidación va entre la fecha efectiva
  del retiro y hoy.
- **E67 · El cruce.** Los fondos se toman **primero de los aportes** (en el orden del débito de aportes: extraordinarios, revalorizados, ordinarios, mínimo) y
  después de los ahorros vivos que no son CDAT (los CDAT siguen hasta su vencimiento y la vista previa lo avisa; los intereses a la fecha los suma US5). Los
  créditos son los de la persona como deudora `Active` o `WrittenOff`, liquidados a la fecha por `IMotorDeCreditoEnDesembolso.LiquidarAsync`; la prelación D14 es
  en mora primero (más días primero), luego mayor tasa (la de la última versión de condiciones), luego el número. El pago va por
  `IContratoDeObligaciones.AplicarAsync` con el recaudador `CAR`/`OwnLoanPayoff`, medio `InternalOffset`, `FullPayoff` si alcanza y `FreeAmount` si no, y la clave
  `CAR:{liquidación:N}:{crédito:N}` con una liquidación nueva en cada intento (la reliquidación no choca con la clave del cruce reversado). Por cada **crédito y
  cuenta** nace una operación `InternalOffset` con `RelatedLoanOperationId` (el pago) y `MembershipWithdrawalId`, con su `CruceAlRetiroAplicado` (origen
  `MembershipWithdrawalSettlement`): así el reverso de un pago arrastra sólo lo que pagó. `OffsetOperationId` es la primera. Cerrojo: `LND_Setup` compartido →
  titular y codeudores → créditos → cuentas; el contrato vuelve a tomarlos sin esperar. Todo en una transacción: si un pago no queda aplicado, falla con
  `OffsetRejected` y nada queda.
- **E68 · Las pérdidas.** api.md decía `applyLossDeduction` en la vista previa y `lossDeduction: { sessionItemPublicId }` al liquidar; la proporción tiene que venir
  del acta, así que las dos rutas reciben `lossDeduction: { sessionItemPublicId, year, proportion }` (fracción sobre el total de aportes, al centavo). Exige
  `LossDeductionEnabled` en la versión de aportes (D15) y un punto de acta cerrado que apruebe (de cualquier asunto: lo dispone la asamblea o el consejo). Sale de los
  aportes antes de cruzar, en su **propia** operación `InternalOffset` sin crédito con `PerdidaAplicadaAlRetiro` solo (mensajes.md §2.1 lo ponía como acompañante del
  cruce, pero puede haber pérdida sin cruce).
- **E69 · El arrastre del reverso.** `ReversionDePago` (013) recibe el enganche opcional `IArrastreDeReversoDePago` (`ArrastreDelCruceAlRetiro`): lo valida **antes**
  de escribir y, después del reverso, reversa cada operación de cuenta que pagó ese crédito (`OperacionDeCuentaReversada`, que anula la liquidación emitida) y deja el
  retiro `ToRedo`. Si el retiro ya está pagado y sus cuentas canceladas, el reverso del pago se rechaza con `Lending.MembershipWithdrawal.AccountsClosed`
  (**pregunta para el dueño**).
- **E70 · Decidir con acta.** La decisión la registra la ruta `/approve` o `/reject` con el punto de una sesión **cerrada** cuyo asunto es este trámite
  (`PuntoDeActaCerrado` para aprobar; para negar, el resultado `Deny` o `Return`). Para que una sesión admita el punto se registra el asunto de acta
  `AsuntoDeRetiroDeAsociado` (sujeto `MembershipWithdrawal`, quien radicó no vota, el cierre no aplica nada). Segregación radicar × decidir sobre el expediente del
  trámite. La fecha efectiva no es anterior a la radicación. `RetiroDeAsociadoAprobado` sale en el mismo guardado; Asociados deja `Status = "R"` y `WithdrawalDate`
  (no llena `WithdrawalReasonId`: la causa no es su catálogo).
- **E71 · La devolución.** Una orden por cuenta con saldo que devolver: `AccountPayoutOrder` con propósito `ContributionRefund` y `MembershipWithdrawalId` (el
  `OrderKind` del mensaje es `MembershipRefund`), sobre una operación `ContributionRefund` (aportes) o `Withdrawal` (ahorro) con filas `Refund`, `DevolucionOrdenada` y
  `OrdenDeGiroEmitida` v2. El plazo es la fecha efectiva + `RefundTermDays` de la versión de aportes o, sin él, `Captaciones.DiasPlazoDevolucionAportes`. El
  beneficiario bancario es el que se pide o la cuenta de depósito registrada del asociado si su tipo de cuenta se lee. **Mínimo irreducible**: si el capital social
  después de todo lo que sale de aportes (pérdidas, cruces y devolución) queda bajo `Aportes.MinimoIrreducibleEntidad`, se retiene **toda** la devolución (el retiro se
  liquida igual) y se levanta la alerta nueva `Cartera.DevolucionRetenidaPorMinimo` (K7, a `Lending.MembershipWithdrawals.Approve`). `refund/approve` ordena lo que
  quedó por devolver —la retenida, comparando otra vez el mínimo, o la reintegrada por un giro rechazado (reemplaza al `reissue`)— con segregación propia: quien
  liquida radica la devolución y otra persona la aprueba (raíz de expediente derivada del trámite). La atribución con monto (§1.6, `RefundDecisionId`) llega con US4.
  En K7 sólo hay orden de giro (`TransferToAccount` → K8).
- **E72 · Pagado y cuentas.** Sin nada que devolver el retiro queda `Paid` al liquidar y sus cuentas sin saldo se cancelan; con devolución, las cuentas quedan
  `Closing` y el retiro pasa a `Paid` cuando todas sus órdenes vivas están giradas y no queda saldo, y entonces se cancelan. `ConsumeTreasuryResultCommand` busca la
  orden también en `LND_AccountPayoutOrders` (así lo hace ya la devolución sin retiro de T086): girada → `DevolucionGirada` (origen la orden, clave
  `Confirmation:{orden:N}`); rechazada, anulada o cheque no cobrado → reverso de la operación de origen (`initiatedBy = TreasuryResult`) y la cuenta sigue `Closing`.
  No escribe `LND_DisbursementOrderResults` (exige una orden de desembolso).
- **E73 · La liquidación emitida.** `MembershipWithdrawalSettlement` la emite el comando de liquidar **después** de confirmar, por `IssueLendingDocumentCommand` con
  `MembershipWithdrawalPublicId` (propiedad que la ruta no lee: por `POST /documents` la clase responde `IssuedAutomatically`); si no se puede (sin firmante) el retiro
  queda liquidado y se registra en el log. El PDF lo pinta `DocumentosDeCarteraReport` (no hay carpeta `API/Reports/Captaciones`, como en T087). Cubre las cuentas
  liquidadas: el reverso de un cruce o de una devolución la anula.
- **E74 · Asociados como destino.** `IntegrationParticipantClass.Associates = 9`; el participante `ASO` y su adopción de `RetiroDeAsociadoAprobado` v1 se siembran a
  mano en `CaptacionesAportes` (y se retiran en `Down`). El recibo `COR_AssociateInboundMessages` guarda sólo lo procesado. El catálogo de Cartera pasa a 78 tipos.
- **E75 · FR-030 y FR-085.** Con un retiro aprobado, liquidado o por rehacer la persona no abre cuentas (`AlreadyInProgress`); las consignaciones por el contrato no
  se bloquean todavía (K8, US4). El reingreso abre cuentas nuevas: las canceladas no cuentan para la única viva.
- **E76 · Pantallas.** La lista y la ficha se abren con cualquiera de los tres permisos; los puntos de acta se digitan por su `PublicId` (no hay selector de puntos
  cerrados); la liquidación y la aprobación de la devolución van sin beneficiario (el servidor toma la cuenta de depósito del asociado); los soportes no se suben
  desde la radicación todavía.
- **E77 · `CaptacionesAportes` regenerada (US10).** Con `LND_MembershipWithdrawals`, `LND_MembershipWithdrawalLines`, `COR_AssociateInboundMessages` y las FK que
  esperaban (E17); ahora `20261018213853` PostgreSQL / `20261018213822` SQL Server (paridad OK, el scaffold sin cambios ajenos). Los dos únicos sobre `PersonId` son
  índices con nombre (EF junta los anónimos de las mismas columnas) y las FK largas llevan nombre propio (E18). Conserva el SQL de T044 y T089 y suma el de `ASO`.

## 12. Enmienda de los movimientos por el contrato (2026-10-18, T108–T126, US4)

Construida la historia 4. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. `EntregaVigente`
sigue en K6b: lo de K8 que ya existe se lee con los defectos del catálogo (como en K7).

- **E78 · Ahorro libre, retiros y traslados (cierra E56).** El contrato consigna libre en cuentas a la vista (modalidad `Free` a capital; `Installments` sin cuotas
  en una a la vista también va a capital) y en una cuenta en apertura sólo si la única retención es el depósito inicial (la activa cuando el capital alcanza el
  mínimo de apertura); CDAT y contractual siguen en `DeliveryNotAvailable` (K9). `RetirarAsync` admite efectivo y cheque; `CloseAccount = true` responde
  `DeliveryNotAvailable` hasta la cancelación de US5 (T135). `TrasladarAsync` es una sola operación `Transfer` con las dos cuentas bloqueadas en orden de `Id` (R08);
  el destino puede ser de otro titular o de aportes (por cuotas). La consulta muestra el disponible real (`DisponibleDeCuenta`, motor puro) y `AcceptsWithdrawals`;
  es el mismo número que valida el retiro (SC-007). Bloqueos y embargos valen cero hasta US11; la posición diaria sigue fuera (E57/E51, US5).
- **E79 · Firmas, topes y efectivo.** `FirmasDeCuenta` (motor puro) decide con el manejo, los límites de firma de los firmantes y el representante del menor;
  faltar una firma es `Lending.Savings.SignaturesMissing` con las personas que faltan. Topes por operación y por día de la versión de la línea
  (`WithdrawalLimitExceeded`) y el efectivo del día (`CashLimitExceeded`). Códigos nuevos en `ErroresDeMovimientos`.
- **E80 · Atribución con monto (cierra E71 para las cuentas).** Retirar o trasladar sobre `Lending.Accounts.Withdraw`, y consignar efectivo sobre
  `Lending.Accounts.Deposit`, crea una solicitud de la **plataforma de aprobaciones** (sujetos nuevos `AccountWithdrawal` y `AccountCashDeposit`, fuente
  `MemberAccountOperation`, origen = la cuenta, huella = el pedido; regla fija de un nivel con el mismo permiso y un aprobador presente con segundo factor por
  `FuenteDeAprobacionDeCuenta`). La pantalla responde **202** `PendingApproval` con la solicitud y no mueve saldo; el contrato responde rechazo
  `Lending.Savings.PendingApproval`. Repetir con `approvalPublicId` aprobado aplica. La atribución de la devolución del retiro del asociado (`RefundDecisionId`)
  **no** se cableó todavía: la mecánica ya existe (`ReglasDeMovimientos.AtribucionAsync`), falta su sujeto propio (queda para US5, con la cancelación).
- **E81 · GMF.** `GravamenMovimientosFinancieros` (motor puro): tarifa, tope exento en UVT por persona y mes, política (cuenta marcada, por titular, no aplica) y
  hechos generadores del parámetro `Gmf.HechosGeneradores`; tarifa cero desde una fecha. El valor se redondea **al peso** (pendiente de validar por la contadora).
  El uso del mes (`LND_GmfMonthlyUsage`) lo escribe sólo `RegistroDeCuentas`; un reverso escribe su espejo y devuelve el tope al mes del original. El GMF va en
  pareja `GmfCharged` (−capital, +GMF) con `GmfRetenido` acompañante del retiro. El débito automático genera GMF como **retiro** (pendiente de validar).
  `GET /gmf-exemptions/usage` responde el uso del mes de la persona en el alcance.
- **E82 · Canje.** `Captaciones.DiasDeCanjePorClaseDeMedio` (defecto `{"Check": 3, "*": 0}`) en días hábiles con el calendario (sin sábados, domingos ni
  festivos); la operación lleva `ClearingUntil` y lo en canje no suma al disponible. La liberación del canje la hará el paso diario de US5 (hoy se calcula por fecha).
- **E83 · Cargos.** El catálogo vive en la versión de la línea (`LND_DepositLineCharges`, entra en su huella) y el campo `event` de api.md se llama `trigger`
  (`Periodic`, `WithdrawalOverFree`, `DepositOverFree`, `ExtraDocument`, `MediumReplacement`, `Inactivity`, `Manual`). Cada cargo es su operación `Charge`
  (`CGO-`): cobrado si el capital alcanza sin bajar del mínimo; si no, por cobrar (`ChargeReceivable`) y lo cobra la siguiente consignación en su misma operación
  (`CargoPendienteCobrado`). Los periódicos se causan el último día de su período por el subpaso `CargosPeriodicos` (idempotente por cuenta, código y
  `PeriodFrom`); los de inactividad sólo en cuentas inactivas. Condonar es `ChargeWaiver` (`CGN-`) con segregación sobre el expediente del cargo (quien lo
  registró no lo condona); la exoneración individual la aprueba quien la registra, con la misma segregación. Reversar un cargo es reversar su operación; el cargo
  cuya deuda ya cobró una consignación posterior **no** se reversa (`Lending.Savings.ChargeCollectedLater`): se condona. Reversar la consignación que cobró un
  pendiente lo devuelve a `Pending` (transición nueva `Collected → Pending`).
- **E84 · Comercial (T117 parcial).** `CargoDeCaptacionCausado` y `…Reversado` (`CAR → Inventory`) se emiten con el ítem comercial de la línea y quedan en la
  bandeja: **no hay consumidor en Comercial** (la facturación de la 012 vive en sus documentos de venta y la emisión electrónica es I4); `DestinoInventario` sigue
  aceptando sólo las respuestas de obligaciones, así que esos mensajes esperan `Pending`. La respuesta `FacturaDeCargoRegistrada` (`INV → Lending`, informativa)
  sí se consume: la reparte `ConsumeCommercialCreditMessageCommand` a `FacturasDeCargos`, que sólo guarda número y estado en el cargo. **Pregunta para el dueño**:
  dónde nace la factura del cargo gravado (¿un documento de venta de servicio con el ítem de la línea, en I3/I4?).
- **E85 · Reversos.** Desde la pantalla de la Cartera (`POST /{id}/operations/{op}/reverse`) nunca queda un sobregiro (`ReversalWouldOverdraw` con la operación que
  lo impide); lo de Nómina o de un recaudador que no sea consignación lo reversa él (`ReversalOwnedByCollector`); lo de un proceso (cruce del retiro, débito
  automático, devoluciones, cargos) desde su origen (`ReversalOwnedByProcess`). La consignación de un recaudador reversada desde la Cartera emite
  `ConsignacionReversadaPorCartera` (destino por la clase del recaudador: Recaudo o Tesorería) para que anule su recibo. Por el contrato, el reverso de una
  consignación ya usada sí se aplica y deja **sobregiro técnico** con la alerta nueva `Cartera.SobregiroTecnico` (K8, a `Lending.Accounts.Reverse`). No hay
  parámetro de ventana de reverso (`ReversalWindowClosed` no se implementó; el contrato reversa lo suyo sin límite de días).
- **E86 · FR-030.** Con un retiro del asociado aprobado, liquidado o por rehacer, las consignaciones de esa persona se rechazan
  (`Lending.Savings.MembershipWithdrawalInProgress`); consignar en aportes o en una línea que exige asociado pide afiliación activa (`MembersOnly`). Cierra E75.
- **E87 · Débito automático.** Subpaso `DebitoAutomatico` del paso de captaciones (después de las cuotas y los cargos periódicos): por cada autorización vigente y
  cada cuota exigible ese día (la misma consulta que usa Nómina), aplica el pago por el contrato de obligaciones con el recaudador `CAR` (`OwnLoanPayoff`), medio
  `InternalOffset` y clave `CAR:debito-automatico:{crédito:N}:{aaaammdd}` (el validador de la recogida la admite) y debita la cuenta con una operación
  `DirectDebit` que nombra el pago. `PagoAplicado` sigue sin admitir proceso **salvo** el del débito automático (`TipoDeMensaje.ProcesoSi`, predicado sobre el
  contenido). El reverso del pago arrastra el del débito (`ArrastreDelCruceAlRetiro` ampliado; sin retiro que rehacer). El soporte de la autorización sólo se exige
  presente (no se verifica que el adjunto exista).
- **E88 · Mayoría de edad (T120).** Al pasar la cuenta al titular se anula (`Cancelled`) la solicitud de retiro en espera de la cuenta; el aviso es el de la mayoría
  de edad (MAYOREDAD) a los dos.
- **E89 · Informes y dobles.** Vistas `deposit-balances` (K7, sin titulares), `deposit-transactions` (K7), `gmf` y `charges` (K8), con datos del titular las tres
  últimas. Dobles de prueba nuevos: `GET /collector/accounts`, `POST /collector/accounts/simulations` (sin clave), `/withdrawals` y `/transfers`; el recaudador de
  prueba acepta `ConsignacionReversadaPorCartera`. El «recaudador Treasury» de T113 no tiene doble propio: la tesorería de prueba es destino de órdenes, no
  recaudador registrado (clase de participante distinta).
- **E90 · Pantalla.** La ficha de la cuenta suma `MovimientosDeCuenta` (componente en `Pages/Cartera/Captaciones`, sin ruta propia): operaciones con comprobante,
  libro de sólo lectura (lectura auditada `Lending.Savings.TransactionsViewed`), retiro por orden de giro (beneficiario: el titular con su cuenta registrada) y
  traslado a otra cuenta del titular con simulación, cargos y débito automático. El editor de la línea todavía no ofrece el catálogo de cargos: se registra por API.
- **E91 · `CaptacionesAportes` regenerada (US4).** Con `LND_GmfMonthlyUsage`, `LND_DepositLineCharges`, `LND_AccountCharges`, `LND_AccountChargeExemptions` y
  `LND_DirectDebitMandates`; ahora `20261018232957` PostgreSQL / `20261018232925` SQL Server (paridad OK, el scaffold sin cambios ajenos). Conserva el SQL de T044,
  T089 y `ASO`.

## 13. Decisiones del dueño del 2026-10-18

El dueño respondió las preguntas abiertas de las fases 1–7 el **2026-10-18**. **Manda el código**; estas notas reemplazan lo dicho antes en
`research.md`, `data-model.md`, `contracts/` y `tasks.md` donde choquen. Criterio general del dueño: el ERP es multiempresa, todo valor por defecto se
parametriza por entidad (parámetro con vigencia o catálogo editable) y los plazos regulatorios se informan, no se imponen. Las tareas están en
`tasks.md`, fase «Decisiones del dueño del 2026-10-18» (T237–T258).

### 13.1 Lo que cambió el código

- **D14 · Requisitos de activación en todos los ambientes (enmienda E32).** `RequisitosDeActivacionDeCaptaciones` ya no deja `Waived` fuera de producción:
  `DeliveriesComplete`, `ProfileAllowsClasses` y `LinesWithMatrix` faltan igual en cualquier ambiente, y `TreasuryDestinationRegistered` exige la orden de
  giro v2 adoptada siempre. Fuera de producción sólo cambia **qué cuenta**: los dobles de prueba valen como recaudador y como Tesorería. La fixture
  `CaptacionesE2E` los cumple con `RequisitosDeCaptacionesAsync` (roles sugeridos, perfil con `mayTakeDeposits`, matriz mínima de reglas); escrita, sin correr.
  **Enmienda E-02 (g), feature 015 (T0308; 2026-10-26)**: los dobles de prueba ya no cuentan en ningún ambiente —se retiraron con Recaudo y Tesorería
  reales—: el recaudador de cuentas es Recaudo (`REC`) y la Tesorería, `TES` real, también fuera de producción. La decisión del 2026-10-18 (D2/D14) queda
  así enmendada; las e2e de captaciones activan Recaudo y Tesorería reales (`RecaudoYTesoreriaE2E.RegistrarYActivarAsync`).
- **D15 · Retiro de lo heredado (cierra T005).** No hay clientes en producción: las tablas heredadas se pueden borrar ya. El dueño es el segundo revisor y
  autoriza; el despliegue inserta en cada base la fila `COR_SystemSettings` `LND.CaptacionesHeredadas.RetiroAprobado`
  (`docs/operaciones/captaciones-retiro-heredado.md` §9a). La guarda de `RetiroDeCaptacionesHeredadas` no se tocó.
- **D17 · El administrador de la empresa no aprueba tasas negociadas.** `Lending.NegotiatedRates.Approve` sale de lo que recibe `CompanyAdmin`
  (`LendingPermissionCatalogSeeder.ExcluidosDelAdministrador`), como las decisiones y el voto.
- **D18 · Perfiles de rol por módulo (enmienda E07).** `PerfilesSugeridos.DeCreditos()` (los 13 de la 013) y `DeCaptaciones()` (los 4 de la 014):
  `LendingRolesCreated` exige sólo los de créditos y el requisito nuevo `DepositRolesCreated` los de captaciones. Crear los roles sugeridos sin elegir
  sigue creando los 17.
- **D19 · Tesorería paga créditos.** El contrato de obligaciones admite a Tesorería si su participante adopta `ProveedorDeObligaciones`
  (`ContratoDeObligacionesDeCartera.RecaudadorAsync`); `PagoReversadoPorCartera` va al destino del recaudador (Recaudo o Tesorería). contrato-de-cuentas §6.3.
- **D20 · La ficha muestra sólo lo del titular.** `GetPersonDetailQuery` cuenta las cuentas donde la persona es **titular**; las de cotitular se ven en una
  ventana aparte (botón en la ficha, `GET /api/core/people/{id}/co-held-accounts`, componente `CuentasComoCotitular`).
- **D29 · Tope de la cuota de fondos de empleados.** `Aportes.TopeCuotaFondoEmpleados` arranca en **0,10** (pendiente de validar), editable por la entidad
  como todo parámetro de Cartera. El `LIMITE` nacional sigue acotándolo y vive en la central (**pregunta abierta**: ¿debe la entidad poder superarlo?).
- **D34 · Constancia de beneficiarios al abrir.** Abrir una cuenta con beneficiarios exige el adjunto de la constancia
  (`Lending.Savings.BeneficiaryRecordRequired`, `data.field = beneficiaryRecordAttachmentPublicId`), no sólo al cambiarlos.
- **D35 · Plantillas de mensaje.** Los textos de `MAYOREDAD`, `CAMBIOTASA` y los demás avisos de captaciones son estándar y editables por la entidad en
  `/cartera/plantillas-de-mensaje` (ya existía); la pantalla ofrece ahora las variables informativas de la 014 (`TextosDeCartera.VariablesPermitidas`).
- **D36 · Pagadurías (cierra T091).** Todo concepto que se paga por nómina se puede descontar por pagaduría: la cuenta va al archivo de la pagaduría que es
  la **empresa empleadora de la ficha del asociado** (`COR_Associates.EmployerCompanyId` → NIT de la pagaduría). Construido: archivo por grupo con el
  formato del grupo y el código de la pagaduría en cada línea (`LineasDeGrupoDeDescuento`), `DescuentosPorPagaduria` v2, `ResultadoDeDescuentos` v2
  repartido por equivalencias (`RepartoDeRespuestaDePagaduria`; créditos por el camino de la v1, cuotas de cuentas a `LND_PayrollDeductionAccountResults`
  con su no descontado por `RegistroDeCuentas`, filas sin equivalencia o descuento sobrante en `LND_PayrollDeductionUnmatchedLines`), el giro de cuentas
  por `ConsignarAsync` con la línea del archivo (`PendientesDeGiroDeCuentas`, `LND_MemberAccountOperations.PayrollDeductionAccountResultId`) y el doble de
  Recaudo para la v2. `Lending.PayrollFile.FileGroupsPending` se retiró. Enmienda a data-model §1.4.2: una línea sin código **no** impide generar (no va en
  ningún archivo y la vista previa la cuenta); `Lending.Payroll.UnmappedLine` no se construyó. La e2e `PagaduriasTests` cubre los pasos 2–4 del quickstart §3
  con créditos (el escenario de libranza no tiene asociados con aportes cuya empresa sea la pagaduría); las cuotas de cuentas las prueban las unitarias.
  Cierra la falta de diseño de E61.
- **D37 · Prelación de los descuentos de Cartera en Nómina.** Política de empresa `PrelacionDescuentosDeCartera` con vigencia (seis órdenes posibles,
  defecto aportes → créditos → ahorros); el motor la recibe por `CalculationPolicies.CarteraDeductionOrder` y la huella sólo la incluye si no es la de defecto.
- **D39 · Pérdida esperada y aportes (corrige E62).** La norma **sí** lo dice: el Anexo 2 del Capítulo II de la Circular Básica Contable y Financiera
  (modificado por la Circular Externa 68 de 2024), numeral 4.2, define el valor expuesto del activo «descontando el valor de los aportes y del ahorro
  permanente de acuerdo con lo establecido en el numeral 5.1 del Anexo 1», y el numeral 4.3 excluye los aportes como garantía para la PDI; el Anexo 1,
  numeral 5.1, fija las condiciones (sin pérdidas acumuladas, solvencia y, en cooperativas de ahorro y crédito y fondos de empleados de categoría plena, el 80 %
  del capital mínimo irreducible; reparto a prorrata del saldo de los créditos). Fuentes:
  <https://www.supersolidaria.gov.co/sites/default/files/data/20240726_anexo_2.pdf>,
  <https://www.supersolidaria.gov.co/sites/default/files/data/20250131_anexo_1_cap_2_tit_4.pdf> y la guía de deterioro de la Supersolidaria (junio de 2024,
  §3.1b, §3.2 i/j/n, §6.19) <https://www.supersolidaria.gov.co/sites/default/files/data/20250211_guia_deterioro.pdf>. Aplicado: `MotorDePerdidaEsperada`
  descuenta los aportes a prorrata del valor expuesto (capital, luego intereses, luego otros) cuando el parámetro de descuento está activo, los indicadores de la
  entidad lo permiten y la norma del cierre lo admite; la garantía «aportes» cuenta como sin garantía para la PDI. **Pendiente**: el descuento del ahorro
  permanente (la norma lo admite; falta el saldo por crédito) y que la contadora valide los indicadores del numeral 5.1.
- **D41 · Orden de los fondos en el cruce del retiro.** Parámetro `Captaciones.OrdenDeFondosAlRetiro` (defecto aportes → ahorros, según estatutos).
- **D43 · Mínimo irreducible.** Retiene **sólo** la parte de aportes de la devolución; los ahorros se devuelven.
- **D44 · Reverso tardío (enmienda E69).** Si el pago de un crédito cruzado se reversa con el retiro ya pagado y las cuentas canceladas, ya no se rechaza con
  `AccountsClosed`: la cuenta se **reabre** (`Closed → Closing`), el retiro vuelve a `Settled` y el saldo queda a favor del exasociado. Exige el permiso
  `Lending.MembershipWithdrawals.ReverseAfterPayment`, levanta la alerta `Cartera.ReversoTardioDeCruce` y envía correo a revisoría fiscal y gerencia (roles
  sugeridos) y a la lista del parámetro `Captaciones.DestinatariosReversoTardio`. Si la cuenta ya tiene otra viva del mismo producto,
  `Lending.MembershipWithdrawal.ReopenConflict`.
- **D45 · Un comprobante para el cruce y el pago.** El cruce al retiro y el pago del crédito van en la **misma** unidad contable: un `CruceAlRetiroAplicado` por
  crédito, con sus partes por cuenta, emitido con el origen y la clave del pago (acompaña a `PagoAplicado`); el excedente a devolver va al puente de Tesorería.
  Si el asociado queda debiendo, la persona elige al liquidar qué pasa con el saldo (`PendingLoanBalanceHandling`: pasar a caja —traslada la forma de pago de
  los créditos con libranza— o seguir con su forma de pago). Los reversos siguen en comprobantes separados (el constructor de captaciones de T143 los unirá).
- **D46 · Motivo del retiro en Asociados.** `RetiroDeAsociadoAprobado` lleva `withdrawalReasonCode` traducido por el parámetro
  `Captaciones.MotivosDeRetiroDeAsociados` (causa → código del catálogo de motivos); Asociados llena `WithdrawalReasonId`.
- **D51 · Comercial factura los cargos (cierra T117 y la pregunta de E84).** `DestinoInventario` reparte `CargoDeCaptacionCausado` y `…Reversado` a
  `ConsumeDepositChargeMessageCommand`: deja lista la factura de venta del servicio por el flujo de ventas (borrador `SalesInvoice`, referencia
  `CGO:{cargo:N}`; tipo y bodega en `Ventas.TipoDeFacturaDeCargosDeCartera` y `Ventas.BodegaDeCargosDeCartera`), o la descarta o deja lista la nota crédito
  total al reversar. mensajes.md §5.5.
- **D52 · Doble de Tesorería como recaudador (cierra T113 y E89).** `RecaudadorDePrueba.Tesoreria` (`TES`, clase `Treasury`) consigna transferencias por el
  contrato de cuentas y las reversa (`/collector/treasury/accounts/*`); la tesorería de prueba acepta los avisos de reverso. api.md §18.4.
- **D53 · Aprobación configurable de los movimientos de cuentas (cierra E80 para la cooperativa).** Retiros y consignaciones en efectivo sobre la atribución
  usan la política de la plataforma de aprobaciones por cooperativa (niveles con umbral y permiso; pantalla `/cartera/captaciones/aprobaciones`,
  `GET/POST /api/lending/approval-policies`) y los montos de `Lending.Accounts.Withdraw`/`.Deposit` son limitables por rol; sin política rige la regla fija.
- **D54 · GMF y canje como defectos configurables.** Redondeo del GMF (`Gmf.Redondeo`, defecto al peso), el débito automático como hecho generador
  «retiro» (`DirectDebit` en `Gmf.HechosGeneradores`) y el canje de cheques (`Captaciones.DiasDeCanjePorClaseDeMedio`, 3 días hábiles) son parámetros editables
  por la cooperativa, pendientes de validar por la contadora.

### 13.2 Verificado sin cambio de código

- **D23 · Tipos de comprobante y modo.** El mapeo hecho → tipo de comprobante de los hechos de captaciones es editable por la cooperativa en
  `/contabilidad/cartera/tipos-de-comprobante`, y el modo de paso por hecho en el parámetro `Contabilidad.ModoDePaso` (ámbito `LendingFactType`). Nada fijo
  en código más allá de la propuesta sembrada.

### 13.3 Sólo se anotan (sin cambio)

- **D16 (enmienda E14).** La tarjeta débito sigue sin función (`DebitCard.SavingsAccountUnavailable`) hasta el módulo de tarjetas.
- **D24.** Las clases de comprobante AH y CD de lo heredado quedan inactivas: sólo para los comprobantes de migración y saldos iniciales de ese proceso.
- **D38.** La liquidación definitiva de Nómina sigue recaudando sólo créditos.
- **D40.** La excepción de arrastre (capital ≤ aportes) es correcta como está.
- **D21, D22, D25–D28, D30–D33 y D42.** Confirman lo construido: sin cambio.

### 13.4 Lo que sigue abierto

- D29: si la entidad puede superar el `LIMITE` nacional del tope de la cuota de fondos de empleados.
- D39: el descuento del ahorro permanente en la pérdida esperada y la validación de los indicadores del numeral 5.1 del Anexo 1.
- D45: el reverso del cruce en un solo comprobante y las líneas puente dentro del mismo comprobante esperan el constructor de captaciones (T143); la devolución
  sigue en su propia unidad (orden de giro). — El constructor ya lo admite y la ida va en un `RA` con el puente anulado (fase 9, E110); el reverso sigue
  separado hasta que el dueño decida (§15.1).
- D44: ~~el correo del reverso tardío sale antes de confirmar la transacción (mejor esfuerzo)~~ — resuelto en la fase 8 (E92).
- D36: las cuotas de cuentas en una pagaduría por HTTP de punta a punta (falta un escenario e2e con asociados, aportes y empresa empleadora).

## 14. Enmienda de los intereses y la retención (2026-10-18, T127–T141, US5)

Construida la historia 5 y el arreglo pendiente de D44. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md`
donde choquen. `EntregaVigente` sigue en K6b: los parámetros de K8 (`Retencion.*`) se leen con los defectos del catálogo hasta que suba la entrega.

- **E92 · D44: el correo del reverso tardío sale después del commit.** `ArrastreDelCruceAlRetiro` lee los destinatarios (roles de gerencia y revisoría y
  `Captaciones.DestinatariosReversoTardio`) dentro de la transacción y anota el envío en `TareasTrasElCommit` (el mecanismo post-commit de la 012, que corre
  `TrasElCommitBehavior` cuando la petición más externa terminó bien y no queda transacción abierta). Si el reverso se revierte, no sale ningún aviso; sin
  quien ejecute tras el commit (un proceso fuera del mediador) no se envía y queda la alerta `Cartera.ReversoTardioDeCruce`. Cierra §13.4 (D44).
- **E93 · El motor del interés (T127).** `InteresDeCaptacion` causa por **diferencia de acumulados** sobre la ventana del período de liquidación (lo causado el
  día d es `Seis(A(d)) − Seis(A(d − 1))`, seis decimales al par, como la hoja `calcular.py`): saldo diario `A(d) = Σ interés(saldo_k, tasa_k, n_k)`; saldo
  promedio `A(d) = promedio_d · Σ r_k · n_k` (interés simple sobre el promedio de lo corrido: sin tramos da lo mismo que el diario, investigación §3.3);
  saldo mínimo `A(d) = mínimo_d · (Π (1 + r_k)^{n_k} − 1)`. Con el promedio y el mínimo lo causado de un día puede ser **negativo** (el día en que baja el
  mínimo): es correcto, el acumulado no deriva. `r_k(B)` es la tasa diaria efectiva de la base ese día (la del tramo o la ponderada si es escalonado), así la
  tasa puede cambiar cualquier día. La tasa diaria lineal es `NA_m / B` con m de la periodicidad de liquidación (`EquivalenciaDeTasas`; la hoja se corrigió:
  decía `EA / B`). La base es el **capital** (el interés por pagar no capitaliza hasta abonarse; lo que está en canje sí gana interés —pendiente de validar
  por la contadora—). El día de la apertura no causa (la ventana empieza en la apertura, excluida). El período de liquidación es el calendario de la
  periodicidad (el de los cargos) o, con `SettlementDay` y una periodicidad de meses, el que termina ese día del mes (o el último si es más corto); con pago
  único no hay períodos (se liquida al cancelar).
- **E94 · La retención (T128).** `RetencionSobreRendimientos`: perfil, umbral diario, componente inflacionario y tarifa, con la norma de la investigación §3.6
  —DUR 1625 de 2016 art. 1.2.4.2.6 (umbral diario de 0,055 UVT del sector solidario: si un día lo alcanza, se retiene sobre el total), art. 1.2.4.2.83 con
  el Decreto 572 de 2025 (CDAT 4 %), tarifa residual 7 %; ET arts. 38 a 41 y DUR art. 1.2.1.12.6 (componente inflacionario; 55,43 % para el año gravable
  2025, Decreto 898 de 2026)—. Decisiones de aplicación: el umbral se mide **por cuenta y por día** con lo causado ese día; el perfil sale de `COR_People`
  (012): `WithholdingExempt` es no contribuyente, `IsSelfWithholder` autorretenedor, sin `PersonType` «sin perfil» y se retiene; el componente se descuenta a
  toda **persona natural** (no hay en el perfil una marca de «obligada a llevar contabilidad»: pregunta al dueño); el componente que rige es el último
  publicado (el del año anterior y, si no está, el de dos años antes; sin ninguno no se descuenta); la tarifa es el parámetro que nombra la versión, con
  ámbito de línea (el permanente y el contractual vacíos caen a la del ahorro). Parámetro nuevo **`Retencion.Redondeo`** (K8, `Peso` o `Centavo`, defecto
  al peso, mitad hacia arriba, pendiente de validar por la contadora), como `Gmf.Redondeo` (D54): la retención y el componente se redondean así.
- **E95 · Causación en el ciclo (T131, T134).** Causación y liquidación son los **últimos** subpasos del paso de captaciones (después de las cuotas, los cargos
  periódicos y el débito automático, que mueven los saldos del día): el día se causa con su saldo de cierre y después se liquida lo vencido. Una tanda va en
  una transacción con el cerrojo del proceso (salta las cuentas ocupadas); cada día causado es una tanda de la corrida (`BatchCount + 1`) con una parte `CCA-`
  por sucursal; si lo que está en el libro de la ventana no coincide con lo que el motor recalcula hasta la víspera (una fecha valor que nadie ajustó, una
  versión registrada tarde), el proceso escribe antes la corrección (`AccrualAdjustment`, `AjusteDeCausacionDeCaptacion` causa `RateCorrection`, parte
  `AJC-`). La puesta al día de varios días avisa `Cartera.CausacionDeCaptacionesAtrasada` (tipo de alerta nuevo, K8, a `Lending.Interest.Settle` y
  `Lending.Processes.Run`). En la puesta al día de una operación (la cancelación, el retiro del asociado) las filas van en la operación y la parte es
  `ALD-{operación}-{n}` (`AccountCatchUp`). La **validación previa contable** no se aplica todavía en el proceso de captaciones: queda para US8 (T142).
- **E96 · El ajuste por fecha valor va enlazado y aparte (T133).** Una consignación, un retiro, un traslado o un reverso con fecha valor en días ya causados
  recalculan la ventana hasta el último día causado y escriben la diferencia en la **misma operación** (`AccrualAdjustment` fechado el último día causado).
  Su hecho `AjusteDeCausacionDeCaptacion` (causa `ValueDatedOperation`, los días que cambiaron y el hecho principal en `causeFact`) sale **aparte**, con el
  mismo origen y su propio comprobante, en lugar de acompañante de la unidad (mensajes.md §2.1): así no hubo que tocar la emisión de cada operación. Lo
  activa el servicio `AjusteDeCausacionDeCuenta`, inyectado en las cuatro operaciones.
- **E97 · Fecha valor en un período ya liquidado.** Si la fecha valor cae antes de la última liquidación, el período liquidado no se reliquida y esos días no se
  ajustan (la ventana empieza en la última liquidación). Con la fecha valor de 5 días hacia atrás (D11) pasa al cruzar un fin de mes. **Pregunta al dueño.**
- **E98 · La liquidación (T132).** Operación `InterestSettlement` (`LQI-`) con el ajuste de redondeo, el abono (`InterestCredited`: −interés por pagar,
  +capital) y la retención en pareja (−capital, +retención), el registro `LND_InterestSettlements` y los acumulados por titular y cotitulares (por su
  proporción vigente al fin del período; el titular absorbe el residuo). Sin interés no nace operación: sólo avanza la marca del período. Emite
  `InteresDeCaptacionLiquidado` (+ `RetencionPracticada`) como **una unidad por operación** (una línea), por lotes resumido si la emite el proceso
  (mensajes.md §2.2 decía partes `LQC-`: la operación ya es el origen natural y Contabilidad agrupa igual). La liquidación y la cancelación **no se reversan
  desde la pantalla** (`Lending.Savings.ReversalOwnedByProcess`). Errores nuevos: `Lending.Savings.AccrualBehind`, `.SettlementPeriodOpen` (también «ya
  liquidado hasta…»), `.AccountDoesNotAccrue`, `.AccrualFailed`, `.SettlementFailed`.
- **E99 · La cancelación (T135).** Sólo el ahorro a la vista (aportes y permanente se cancelan con el retiro del asociado,
  `Lending.Contributions.ClosureOnlyByWithdrawal`; CDAT y contractual, K9), con la fecha de hoy (`Lending.Savings.CloseDateInvalid`). Bloquean
  (`Lending.Savings.CloseBlocked` con la lista): embargo, bloqueo, canje, sobregiro técnico, titular fallecido y **cargos por cobrar** (pregunta al dueño).
  Nace la operación `Cancellation` (`CAN-`, valor 0) que lleva la puesta al día de la causación y la liquidación a la fecha (origen `AccountClosing`); los
  períodos vencidos sin liquidar se liquidan antes por separado. La entrega es su **propia** operación: retiro por orden de giro (propósito `Cancellation`,
  hecho generador del GMF `Cancellation`) o traslado a otra cuenta, del valor que con su GMF agota el saldo. Con giro, la cuenta queda `Closing` y se cancela
  cuando Tesorería informa el giro; si lo rechaza, vuelve a activa con su saldo. La cancelación en efectivo por el contrato (`closeAccount`) sigue sin entrega
  hasta que exista Recaudo.
- **E100 · Retiro del asociado con intereses (T136).** La vista previa suma a cada ahorro que gana interés el neto liquidado a la fecha (bruto menos retención)
  y la liquidación del retiro, bajo el cerrojo y antes de cruzar, liquida esos ahorros a la fecha (`LiquidacionALaFecha`: períodos vencidos, la operación
  `InterestSettlement`, la puesta al día en ella y la liquidación). Si hubiera períodos vencidos sin liquidar, el redondeo por período puede mover un peso y la
  huella responde `VistaPreviaCambio`: se vuelve a pedir la vista previa.
- **E101 · Suspensión (FR-065).** El titular fallecido con `SuspendsAccrualOnDeceased` y la cuenta embargada con `SuspendsAccrualOnTotalGarnishment` dejan
  la marca `AccrualSuspendedSince` desde el primer día que la detecta el proceso; hasta US11 (embargos) la marca `IsGarnished` se toma como embargo total.
  Una cuenta inactiva cuya línea no causa en la inactividad no causa. La marca de agua avanza aunque no cause.
- **E102 · Lo que lee el interés.** `LectorDeInteres`: la versión que rige cada día (la sellada al abrir si la versión declara tasa pactada, FR-007), la tasa
  variable con el último índice publicado a la fecha de su reajuste (`ResetPeriodicity`, mensual si no lo dice) a seis decimales, como el reajuste de la 013, y
  los saldos de capital de la reconstrucción de la cuenta a (D, R), también con lo agregado en la unidad de trabajo sin guardar.
- **E103 · Rutas, vista y pantalla (T137, T139).** Cinco rutas en `MemberAccountsEndpoints.cs`: `GET /{id}/settlements`, `POST /{id}/settlements/simulate`,
  `POST /{id}/settlements` (201 la nueva; 200 con `alreadyExisted` la existente, SC-003), `GET /{id}/close-preview` y `POST /{id}/close`
  (`Lending.Accounts.Close`). La vista `interest-and-withholding` (K8, con datos del titular). `InteresesDeCuenta.razor` es un componente de la ficha de la
  cuenta de captación, sin ruta propia (como `MovimientosDeCuenta`).
- **E104 · `CaptacionesAportes` regenerada (T130).** Con `LND_InterestSettlements` (único `(MemberAccountId, PeriodTo)` sobre las no reversadas, único por
  operación) y `LND_WithholdingAccumulations` (bigint); ahora `20261019030947` PostgreSQL / `20261019030916` SQL Server (paridad OK, 201 líneas nuevas y
  ninguna quitada en cada motor). Conserva el SQL de T044, T089 y `ASO`.
- **E105 · Casos dorados (T129).** 9 `interes-*` y 8 `retencion-*` (29 JSON de captaciones con los de GMF y cargos), todos recalculados por `calcular.py`
  (`--verificar`): 6.884 por saldo diario y por promedio y 3.649 por saldo mínimo (investigación §3.3); tramos escalonados y por total; cambio de tasa a
  mitad de mes; febrero de 2028 en real/365; mínimo para interés; consignación fechada atrás (lo sabido antes y después); umbral no alcanzado (el mes del
  6.884) y alcanzado (400.000.000) con y sin componente; el CDAT de 488.088 (8.702 con el componente de 2025, 19.524 sin él, cero si el umbral alcanza
  también al CDAT); persona exenta y autorretenedor.

### 14.1 Preguntas para el dueño

- **Persona natural «obligada a llevar contabilidad».** El componente inflacionario sólo se descuenta a la persona natural **no** obligada; el perfil
  tributario de la 012 no tiene esa marca. A: se descuenta a toda persona natural (lo construido); B: una marca nueva en el perfil de la persona (Personas);
  C: un parámetro por cooperativa que diga si sus asociados naturales se tratan como no obligados. Recomendado: B.
- **Fecha valor en un período ya liquidado (E97).** A: no se reliquida (lo construido); B: el ajuste entra en el período abierto y se abona con la próxima
  liquidación; C: se rechaza una fecha valor anterior a la última liquidación. Recomendado: B.
- **Cancelar con cargos por cobrar (E99).** A: se bloquea hasta cobrarlos o condonarlos (lo construido); B: la cancelación los cobra del saldo antes de
  entregar. Recomendado: B (FR-039 dice «cobra GMF y cargos»).

## 15. Enmienda de la integración contable de captaciones (2026-10-18, T142–T150, US8)

Construida la historia 8. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. No hubo cambio de
esquema: `CaptacionesAportes` sigue como la dejó la fase 8 (E104).

- **E106 · El reverso nombra la contrapartida de su original.** `OperacionDeCuentaReversadaV1` suma, opcionales al final, `CollectorClass` (la clase del
  recaudador del original; `OwnLoanPayoff` en el débito automático y el cruce), `OrderKind` (el de la orden de giro del original) y `ReasonCode` (el
  `ChargeKind` de un cargo o de su condonación), y `DepositFactMirrorV1` suma `Account`, `HolderPersonPublicId` y `Dimensions` para el espejo de **otra**
  cuenta del original (el destino de un traslado). Hasta aquí el reverso sólo traía las filas de la cuenta de la operación y no decía a dónde iba la
  contrapartida: el reverso de un traslado no cuadraba y el de una consignación no sabía a qué puente volver. `ReversionDeCuentas` los llena (la orden se lee
  por su operación; el cargo de una condonación, por su carga útil). Ningún hecho se había emitido en producción (E26): cambiar el contenido no rompe a nadie.
  El reverso toma el **tipo de comprobante de su original** (contabilidad.md §5) y, si el original no tiene mapeo, el suyo.
- **E107 · El constructor de captaciones (T143).** `ConstructorDeLineasDeCartera.Captaciones.cs`: los importes de un hecho de cuenta son el cambio del
  saldo de cada concepto (+ = la Cartera le debe más al titular); cada concepto va a su rol (`RolDelConceptoDeCuenta`) por su lado natural y lo que no cuadra
  va a la **contrapartida del hecho** (el puente del recaudador en una consignación, un aporte o un retiro por el contrato; la devolución por pagar en un
  retiro por giro o una devolución; el gasto por intereses en la causación y su ajuste; el ingreso por cargos; el puente `OwnLoanPayoff` en el cruce y el débito
  automático; la pérdida absorbida; los embargos por consignar; los no reclamados; la penalidad; la bonificación; los fondos de la asamblea). La liquidación, la
  retención, el traslado, el cargo pendiente que cobra una consignación y la prórroga del CDAT cuadran solos. Cada línea lleva el número de la cuenta: es su
  cruce `CTA` (del mapeo de su hecho) si la cuenta contable exige documento, y en las cuentas que exigen tercero, cruce o base va **una línea por cuenta**,
  también en el resumido. En una unidad que junta hechos de las dos naturalezas (el cruce al retiro con el pago del crédito, D45) cada línea toma el cruce del
  mapeo de su propio hecho: el pagaré en las del crédito, la cuenta en las de captaciones. Un hecho que mueve el saldo y no dice a dónde va la contrapartida
  (un retiro sin recaudador ni orden; un CDAT fondeado desde otra cuenta o abonado a otra cuenta sin sus dimensiones) responde el error nuevo
  `Accounting.LendingFact.CounterpartUnknown`, defecto del emisor: **K9 tiene que emitir la cuenta de origen o de destino del CDAT como parte del hecho, con sus
  dimensiones**. Dos arreglos del emisor: (1) la marca de la cuenta (C20) la lee una vez `HechoDeCuentaAsync` (la exención vigente a la fecha contable) y la
  llevan todos los hechos de la cuenta —antes la consignación y el cargo salían «Standard» aunque la cuenta fuera la exenta, y el retiro sin GMF también—;
  (2) el GMF de un traslado va a la cuenta por pagar de la clase del producto que se debita (la de origen; antes la del destino, y con aportes
  «Contribution», que no es una clase de captación). El constructor no hace IO y es el mismo de la validación previa, el consumo, la vista previa del lote y la
  conciliación.
- **E108 · Validación previa (T142, T145).** Rige D23 «igual que los créditos», es decir, A8 de la 013 = «Bloquear todo»: con `Contabilidad.FaltaDeRegla =
  Bloquear` bloquean la pantalla (422), el contrato (rechazo con su código) y el proceso (la tanda no avanza, sus cuentas quedan en `LND_ProcessRunErrors` con
  `Lending.Prevalidation.NotPostable` y sale `Cartera.HechoNoContabilizable`); con `Advertir` se aplica, se sella y se alerta. La spec (US8-2) y
  contabilidad.md §7 decían que el contrato y los procesos sólo sellan y alertan: es el texto de A8 antes de la decisión del dueño del 2026-10-14 (pregunta en
  §15.1). La pregunta se hace con **hechos representativos** (`HechosRepresentativosDeCaptaciones`) con las dimensiones vigentes de la cuenta, su titular, su
  número, la fecha de la operación y un origen propio: hasta aquí iban sin fecha (el 1 de enero del año 1, donde ninguna regla rige), sin origen (todas las
  preguntas de un lote eran una sola unidad), sin titular ni número (la cuenta que exige tercero respondía «falta el tercero») y el traslado sin destino.
  `EvaluateLendingPostingQuery` además evalúa con la fecha de hoy un hecho sin fecha y como su propia unidad uno sin origen. Pantalla
  (`ValidacionPreviaDeCaptaciones.EnPantallaAsync`): retiro por giro y traslado (con su GMF), cancelación (liquidación y retención si la cuenta causó alguna vez,
  y la entrega con su GMF) y liquidación del retiro del asociado (cruce, pérdida, devolución por naturaleza y liquidación de los ahorros). Proceso: la causación
  pregunta por la tanda; la liquidación periódica, los cargos periódicos (por su clase, cobrado y por cobrar) y el débito automático preguntan **sólo por las
  cuentas que operan ese día**, una vez por combinación (`PreguntaDeTanda`). Activación (`LinesWithMatrix`): todos los hechos que la naturaleza emite desde el
  primer día, por clase de línea —en aportes, el aporte, el cruce y la devolución del retiro; en el ahorro a la vista y el permanente, la consignación, el
  retiro por giro con su GMF, la causación, la liquidación y la retención—; la fixture e2e `CaptacionesE2E` siembra esas reglas cuando ya hay líneas activas.
- **E109 · La conciliación (T146).** `SaldosContablesDeCaptaciones` suma el libro de cuentas por fecha contable por rol (capital, capital inactivo, interés por
  pagar, aportes por clase, cargo por cobrar), línea, naturaleza, clase, marca y oficina; la conciliación de la Cartera los agrega a los de créditos y la
  vista `deposits-reconciliation` (`Lending.Reconciliation.View`, sin datos de titulares) muestra los conjuntos de captaciones con lo que explica cada
  diferencia. El puerto `CombinacionDeSaldo` y `GetLendingAccountBalancesQuery` llevan las cinco dimensiones. Decisiones: la **retención y el GMF por pagar no se
  concilian** contra el libro de la cuenta (en el libro sólo crecen; la cuenta contable baja cuando la cooperativa paga a la DIAN): se concilian con la
  declaración. La marca y la inactividad son las de la cuenta hoy (la exención, la vigente al corte): si cambian sin un `CaptacionReclasificada` —que todavía
  nadie emite: la inactividad, el embargo y la exención lo emitirán con US11 y el proceso de inactividad— la diferencia aparece entre la marca vieja y la nueva
  **sólo si la contadora las lleva a cuentas distintas** (la plantilla sugiere la misma). El plazo de emisión del CDAT y del contractual llega con K9 (la cuenta
  no lo guarda todavía). La causación va al centavo por cuenta y día y la liquidación abona el bruto redondeado: el residuo de centavos por cuenta queda en el
  interés por pagar contable; lo ve la conciliación y lo decide la contadora (§15.2).
- **E110 · D45 en el reverso.** El constructor ya arma en **un** comprobante el reverso del cruce al retiro con el reverso del pago del crédito —el puente
  `OwnLoanPayoff` se anula dentro, como en la ida— y el reverso toma el tipo `RA` de su original; `PagoReversado` es acompañante de `OperacionDeCuentaReversada`
  para que la unidad tenga principal. **El emisor no cambió**: `ArrastreDelCruceAlRetiro` sigue sacando el reverso del pago y los de las cuentas en unidades
  separadas (cada una cuadra por el puente) hasta que el dueño decida (§15.1).
- **E111 · La plantilla v2 y la completitud (T144).** `PlantillaDeMatrizDeCartera` suma `naturaleza`, `claseDeCaptacion`, `plazoDeEmision`, `claseDeAporte`,
  `marcaDeCuenta` y `cuentaSugerida` (informativa: la importación no la aplica) y sigue leyendo un archivo de la versión 1. Las filas sugeridas sin cuenta
  (`SugerenciasDeCaptaciones`) salen de las naturalezas y clases de las líneas del catálogo de Cartera, que ahora trae la clase de captación
  (`LineaDeCarteraDto.DepositClass`); las cuentas CUIF sugeridas son **dato embebido** (`Accounting/Lending/Plantilla/sugerencias-cuif-captaciones.json`), no
  literales en código (`LaContabilidadNoTieneCuentasEnCodigo`). El gasto por intereses se sugiere con el hecho en `*` (no fijado a la causación), porque el
  ajuste de la causación usa el mismo rol. La completitud exige, por línea, las combinaciones de K7 y K8 (`FaltantesDeCaptaciones`, con la naturaleza y la
  cuenta sugerida en cada fila) y avisa: devolución o embargo por pagar en una cuenta que exige cruce, causación que irá una línea por cuenta, marca exenta o
  embargada en otra cuenta que la estándar y el puente de Tesorería sin regla. Lo de la asamblea, los no reclamados, los embargos y Tesorería se sugiere pero no
  se exige. Las líneas de captación ya no generan filas de crédito con modalidad vacía. La lista de reglas de la API no filtra por las dimensiones de
  captaciones (se filtra por rol o por cuenta).
- **E112 · Comprobantes y cierre (T146, T147).** El comprobante de captaciones responde `Accounting.Document.ModuleOwned` con su propio mensaje («se corrige en
  la Cartera con el reverso de la operación de la cuenta») y, si es un resumido, cuántas cuentas reúne. `Accounting.Period.LendingNotClosed` dice, con captaciones
  activadas, desde qué día está pendiente la causación de captaciones (`data.depositAccrualPendingFrom`, el día siguiente al último `InteresDeCaptacionCausado`
  recibido en el mes; nada si ninguna cuenta ha causado, como en una cooperativa sólo de aportes).
- **E113 · La tabla hecho × rol (T142).** Admiten además el capital de la cuenta inactiva: el GMF, la liquidación, la retención, el cargo causado, el pendiente
  cobrado, la condonación y el embargo (la inactividad cambia el rol, C20). Una muestra por cada uno de los 30 hechos de negocio se desglosa sin fallos y en
  roles de la tabla, y sobre las operaciones reales de la Cartera (consignar, retirar con GMF, trasladar, reversar por el recaudador) el capital y el GMF que el
  constructor lleva a cada cuenta son exactamente los del libro.
- **E114 · Pantallas (T149).** `/contabilidad/cartera/matriz` filtra pestañas y roles por naturaleza (créditos, captaciones y aportes, todas; el puente del
  recaudo es de las dos), pide en el alta las cinco dimensiones —el plazo de emisión también cuando el capital de CDAT o contractual lo exige, aunque el
  catálogo no lo liste— y las muestra; `/contabilidad/cartera/completitud` filtra por naturaleza y muestra la cuenta sugerida. Las familias «Captaciones» y
  «Aportes y fondos» y los 17 roles tienen nombre en pantalla.

### 15.1 Preguntas para el dueño

- **El reverso del cruce al retiro en un solo comprobante (D45, E110).** A: seguir con comprobantes separados para el reverso (lo construido; cada uno cuadra
  por el puente); B: que el reverso del pago y los de las cuentas que lo pagaron salgan en una unidad, un comprobante `RA`, como la ida (el constructor ya lo
  arma; falta que el emisor los junte). Recomendado: B, por simetría con la ida.
- **La validación previa del contrato de cuentas (E108).** A: igual que los créditos (A8, «Bloquear todo»: con `Bloquear` el contrato rechaza la consignación
  o el retiro que no se contabilizaría; lo construido); B: lo que el contrato de cuentas recibe de Recaudo, Nómina y Tesorería nunca se bloquea: se aplica, se
  sella y se alerta (el texto original de US8-2). Recomendado: A, por coherencia con la decisión de A8 y porque el defecto de la plataforma es «Bloquear».

### 15.2 Para la contadora (pendiente de validar)

- Las cuentas CUIF sugeridas (`sugerencias-cuif-captaciones.json`), en especial el ahorro permanente (213005 o 213010), la bonificación (615015) y los
  revalorizados (310505).
- Si la cuenta exenta del GMF o la embargada van a otra cuenta que la estándar (entonces hace falta la reclasificación al marcar o desmarcar).
- El residuo de centavos entre la causación diaria al centavo y el bruto liquidado (E109): ajustarlo en la liquidación o dejarlo en el interés por pagar.
- La conciliación de la retención y el GMF por pagar con la declaración (fuera de la Cartera).

## 16. Enmienda de la migración de saldos (2026-10-19, T151–T161, US9)

Construida la historia 9. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. Cambio de
esquema: `CaptacionesAportes` **regenerada** en los dos motores (PostgreSQL `20261019051709`, SQL Server `20261019051623`) con siete tablas de preparación
y la columna `LND_MigrationLoads.ReferenceFiguresJson`; el SQL escrito a mano (T044, `NOM`, `ASO`) se reinsertó sin cambios y la paridad pasa. Las
migraciones congeladas de la 013 y `RetiroDeCaptacionesHeredadas` no se tocaron.

- **E115 · Las equivalencias de productos son una parte de la carga.** `line-equivalences` (código heredado → línea nueva, con la clase en captaciones) se
  carga primero y vive sólo en la migración: una cuenta trae `linea` o `codigoHeredado`, y el código sin equivalencia responde
  `Lending.Migration.LineEquivalenceMissing`. No hay hoja de equivalencias en plantillas de líneas: **las plantillas de importación de líneas
  (`lending.deposit-lines`, `lending.contribution-lines`) no se construyeron**; las líneas se crean por pantalla (T152 parcial).
- **E116 · Partes que llegan después.** `term-deposits` llega con el CDAT (K9) y `holds-and-garnishments` con los bloqueos y embargos (US11): figuran
  en el catálogo de partes con «llega con …» y sin plantilla, y la tabla `…HoldRows` no se creó. Las columnas del ahorro contractual y de la tasa
  negociada existen en la plantilla de cuentas y, si traen valor, la fila se rechaza como «todavía no disponible» (K9) hasta su entrega.
- **E117 · Una carga, dos mundos.** `MigrationLoad.Nature` separa colocación de captaciones y aportes: las consultas y comandos de la 013 filtran
  `Nature == Placement`; la aceptación de diferencias (`AcceptMigrationDifferencesCommand`, con `DeCaptaciones`) y el punto del acta
  (`PuntoDeActaCerrado`) sirven a las dos. Una carga aplicada reemplaza a la anterior **de su naturaleza** (`Replaced`, nunca se borra).
- **E118 · El GMF del año queda en la carga.** Del acumulado tributario, rendimientos, componente inflacionario y retención pasan al acumulado de la cuenta
  (`Origin = Migration`); el GMF del año se lee de la fila de preparación para el certificado (no hay acumulado de GMF por cuenta).
- **E119 · Titularidad y estado migrados.** La parte de cotitulares lleva la columna `fundamento` (Parent, Guardian, CourtOrder,
  LegalEntityRepresentative) para el representante legal; su soporte se sube después: la parte nace con **soporte diferido** y la cuenta con
  `RequiresDataUpdate`. Una cuenta `Inactive` nace inactiva **desde el corte** (no desde su último movimiento, que queda como dato de la fila). El número
  migrado se conserva (`NumeradorDeCartera.ConservarNumeroDeCuenta`) y el consecutivo de la línea se lleva por encima; una cuenta de aportes sin número
  toma el consecutivo.
- **E120 · Informativos en aportes.** `minimoNoReducible` individual, `ingresoRegistrado` y `cuotaPactada` se guardan en la fila y sólo avisan: la cuenta
  todavía no tiene dónde guardar un mínimo individual y la cuota sale de la línea.
- **E121 · El interés migrado no se vuelve a causar.** La ventana de causación de una cuenta migrada arranca en el corte (`LectorDeInteres.CorteDeMigracion`
  = víspera de la activación); el saldo inicial `InterestPayable` cuenta como causado para la primera liquidación, que lo liquida sin causarlo otra vez.
  Para el umbral de retención se reparte por igual entre los días que cubre (desde `ultimaLiquidacionHasta` hasta el corte) — pendiente de validar.
- **Errores nuevos.** `Lending.Migration.PartNotForNature`, `.PartNotAvailable`, `.AccountNumberDuplicate`, `.LineEquivalenceMissing`,
  `.MinorWithoutRepresentative`, `.AccountNotInLoad`.
- **Activación con carga.** Fecha = corte + 1, acta obligatoria, cuadre recalculado (la versión nueva se guarda fuera de la transacción, como en la
  013), plazo de fecha valor = corte + `Captaciones.DiasMaximosFechaValor`, cuentas en tandas de 500 dentro de una sola transacción; saldo inicial por
  concepto con su operación `SIN-` y la de compensación aparte, cuotas atrasadas con lo pagado y su motivo, marca y uso del GMF del mes. El único hecho es
  el informativo `CaptacionesActivadas`.

### 16.1 Para el dueño

- **Plantillas de importación de líneas (E115).** A: dejarlas fuera (las líneas son pocas y se crean por pantalla; lo construido); B: construirlas para
  cooperativas con muchos productos. Recomendado: A.
- **Mínimo no reducible individual (E120).** A: informativo (lo construido); B: una columna en la cuenta de aportes que el retiro respete. Recomendado: B si
  algún estatuto lo fija por asociado; si no, A.
- **Fecha de inactividad de las cuentas migradas (E119).** A: inactiva desde el corte (lo construido: el plazo para los no reclamados corre desde la
  activación); B: desde su último movimiento real (el plazo puede vencer al día siguiente de activar). Recomendado: A.

### 16.2 Para la contadora (pendiente de validar)

- El reparto por igual del interés migrado entre los días que cubre para medir el umbral diario de retención (E121).
- Las cifras de referencia por línea y concepto que entregará la cooperativa al corte, y las diferencias de cuadre que se aceptan con acta.

## 17. Enmienda de la seguridad, las atribuciones y la auditoría (2026-10-19, T162–T165, US16)

Construida la historia 16. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. Sin cambio de
esquema: `CaptacionesAportes` sigue como la dejó la fase 10 (§16).

- **E122 · Un catálogo de segregación (T163).** `Domain/Lending/Segregation/SegregacionDeCaptaciones` declara los nueve expedientes de FR-005 y api.md §1.7
  —apertura de la cuenta, solicitudes de aportes (devolución y cesión), retiro del asociado, su devolución, cargos de la cuenta, embargo (K8), tasa negociada
  (K9), entrega por fallecimiento y decisión de asamblea (K10)— con su par: registrar es `Radicar` y aprobar o levantar es `Decidir`, es decir, la **regla
  fija** de la 013, que la cooperativa no apaga con `Segregacion.Incompatibilidades` (puede sumar pares, no quitar éstos). La comparación es por usuario y
  por persona (`ReglasDeSegregacion`; `RegistroDeIntervenciones` exige que el usuario tenga persona). La segregación compara por la **raíz** del expediente,
  no por su tipo: los cargos usaban la cuenta como raíz, la misma de la apertura, así que **quien abrió una cuenta no podía condonar ni exonerar ninguno de
  sus cargos**. Ahora los expedientes que cuelgan de un recurso ajeno (cargos de una cuenta, devolución del retiro) tienen raíz derivada
  (`SegregacionDeCaptaciones.Raiz`, SHA-256 de «tipo:recurso», la misma fórmula que ya usaba la devolución desde US10, así que esas intervenciones no
  cambian). Las intervenciones de cargos registradas antes en DEV quedan en la raíz vieja (no hay datos en producción). Los expedientes de K8–K10 quedan
  declarados para que sus historias los usen; `LaSegregacionDeCaptacionesUsaSuCatalogo` exige que todo expediente de `Application/Lending/Deposits` salga del
  catálogo, que los derivados usen su raíz y que nadie escriba `LND_CaseParticipations` fuera de `RegistroDeIntervenciones`.
- **E123 · El alcance también en la otra cuenta (T162).** La regla de arquitectura de T038 miraba el archivo: con que una consulta lo aplicara, pasaban
  todas. Revisado handler por handler, la **otra** cuenta de una operación se leía directo: el destino de un traslado, de su simulación y de la entrega de una
  cancelación (un usuario de la oficina norte trasladaba a una cuenta de la oficina sur y la simulación le confirmaba que existía), la cuenta de aportes
  que recibe una cesión (en la solicitud y en la aprobación), el crédito que paga un débito automático y las cuentas que suma el resumen de aportes de una
  persona. Todos pasan por `LectorDeCuentas.CuentaEnAlcanceAsync` o `AsegurarAsync`: fuera, la misma respuesta que lo inexistente (404 de la cuenta, del
  crédito o de la persona; en la cesión, «la persona no tiene cuenta de aportes») con `Lending.Access.OutOfScope`. El resumen de aportes exige el alcance
  sobre **todas** las cuentas de la persona (falla cerrado: un total que incluye una cuenta ajena la revela). `LasCaptacionesExigenPermisoYAlcance` suma dos
  reglas: cada handler nombra `IAlcanceDeCartera` (o `SolicitudesDeAportes`, que lo aplica por dentro) y ninguna cuenta se lee por el `PublicId` del pedido
  fuera del lector. Crear una línea de captación o de aportes queda exceptuado con motivo (no toca una línea existente ni una oficina; entra al alcance por
  asignación, como las de crédito). Siguen sin alcance, a propósito: los puertos para otros módulos (contrato de cuentas, consulta de aportes; FR-004: se
  autorizan con el permiso del módulo que llama), el ciclo diario y el cierre (proceso, alcance total) y la liquidación del retiro del asociado, que cruza los
  créditos y liquida las cuentas de la persona en cualquier oficina (el trámite está en el alcance de su cuenta de aportes; es un procedimiento de la entidad).
- **E124 · Lecturas sensibles (T164).** Nacen las rutas de api.md §4 que faltaban: `GET /api/lending/accounts/{id}/balance?date=&recordedAsOf=`
  (`GetMemberAccountBalanceQuery`: los diez conceptos de la reconstrucción del libro a (D, R) con su huella, el saldo del titular, el disponible con lo en canje
  y el mínimo de la versión a D y, en las cuentas que ganan interés, lo causado sin liquidar a D con la explicación día por día) y `GET /{id}/statement?from=&to=`
  (`GetMemberAccountStatementQuery`: saldo inicial, una línea por operación por fecha valor —lo causado sin operación va en una línea por mes—, retención,
  GMF, intereses abonados, saldo final y el disponible de hoy; a lo sumo 366 días, para más el documento). Decisiones: sin `recordedAsOf` el saldo toma
  **todo lo registrado** (el sello del libro es monótono y puede ir unos instantes delante del reloj); sin versión de la línea a (D, R) el saldo sale sin lo
  causado; sólo `NationalValues.Missing` impide responder. Eventos nuevos `Lending.Savings.BalanceViewed` (el saldo, el detalle de la cuenta, la lista de
  cuentas de una persona, la vista previa de la cancelación y el resumen de aportes, éstos dos con la persona como entidad) y `Lending.Savings.StatementViewed`;
  `Lending.Savings.TransactionsViewed` suma el uso del GMF de una persona. Van en una lista propia, `LendingAuditEmitter.LecturasSensiblesDeCaptaciones`, sin tocar
  la lista cerrada de la 013. Las exportaciones de las vistas de captaciones las audita ya el centro de informes (`Lending.Report.Exported`) y los documentos al
  titular, la emisión y la reimpresión de la 013 (`Lending.Document.*`). `LasLecturasSensiblesDeCaptacionesSeAuditan` fija cada lectura con su método. La lista
  general de cuentas sin titular no se audita (no es la consulta de los saldos de un tercero). Las dos rutas nuevas **no tienen pantalla** todavía: la ficha
  sigue mostrando el detalle (auditado) y el libro (auditado).
- **E125 · La e2e (T165).** `SeguridadDeCaptacionesTests` (colección «Captaciones e2e», cooperativa aislada «seguridad»): a los asesores de O1 y O2 se les
  suma la plantilla `cartera.asesor-captaciones` (sin alcance total) y la línea a su alcance; cinco escenarios —retiro sin permiso igual a inexistente con
  `Lending.Access.Denied`; el asesor de O2 sobre el detalle, el saldo, el extracto y como destino de un traslado de una cuenta de O1, con
  `Lending.Access.OutOfScope`; las lecturas auditadas del asesor de O1 con (D, R); el cargo registrado por uno y condonado por quien abrió la cuenta; la cadena
  íntegra y luego un evento de lectura alterado y otro borrado—. **e2e en la corrida final.**
- **E126 · Lo que la segregación no cubre.** La aprobación de un retiro o una consignación en efectivo sobre la atribución (D53) es de la plataforma de
  aprobaciones: excluye al solicitante por `SEC_Users.Id` (no por persona) y **no** mira si el aprobador presente tiene en su alcance de Cartera la oficina de
  la cuenta (pregunta para el dueño).

### 17.1 Preguntas para el dueño

- **Traslados y cesiones a cuentas de otra oficina (E123).** A: el destino también tiene que estar en el alcance de quien opera (lo construido; falla
  cerrado); B: el destino puede ser de cualquier oficina, aunque el asesor no pueda ver esa cuenta; C: un parámetro por cooperativa que diga si se admite el
  destino fuera del alcance. Recomendado: A, y C sólo si alguna cooperativa opera traslados entre oficinas desde la ventanilla.
- **El aprobador presente fuera de la oficina de la cuenta (E126).** A: basta el permiso y el monto (lo construido); B: el aprobador también tiene que tener la
  oficina y la línea de la cuenta en su alcance de Cartera. Recomendado: B.

## 18. Enmienda del CDAT (2026-10-19, T166–T180, US6, K9)

Construida la historia 6 y los pendientes de CDAT de las fases 9 y 10. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y
`tasks.md` donde choquen. Cambio de esquema: `CaptacionesAportes` **regenerada** dos veces en los dos motores (última PostgreSQL `20261019073218`, SQL Server
`20261019073145`) con cinco tablas (`LND_MemberAccountTermsVersions`, `LND_TermDepositSchedules`, `LND_NegotiatedRateRules`, `LND_NegotiatedRateRequests`,
`LND_MigrationTermDepositRows`) y las columnas `LND_MemberAccountTransactions.TermsVersionId`, `LND_MemberAccounts.PendingTermsJson` y
`.MaturityInstructionJson`; el SQL escrito a mano (T044, `NOM`, `ASO`) se reinsertó sin cambios y la paridad pasa. Las migraciones congeladas de la 013 y
`RetiroDeCaptacionesHeredadas` no se tocaron.

**Norma aplicada** (investigación financiera §3.1, §3.2, §3.6 a §3.9, con sus fuentes): el CDAT es título de legitimación, no título valor (C. de Co. art. 645);
plazo, redención, prórroga y su tasa son del reglamento de cada entidad, por eso todo es condición de la línea con vigencia y defecto «pendiente de validar»;
retención del 4 % sobre rendimientos de CDAT (Decreto 572 de 2025, art. 1; DUR 1625 de 2016 art. 1.2.4.2.83), umbral diario del art. 1.2.4.2.6 fuera del CDAT
por defecto (`Retencion.AlcanceDelUmbral = ExceptTermDeposit`, D10, P2 abierta), retención antes de capitalizar (FR-063); GMF de la redención abonada en
cuenta sobre capital más intereses (Oficio DIAN 2787 de 2019; ET arts. 871 y 879) y del traslado entre ahorro y CDAT (concepto DIAN 082568 de 2011);
plazo de emisión de las cuentas 211005 a 211020 del CUIF de la Supersolidaria. El seguro de depósitos (Fogacoop) es de K11 (US15) y la inembargabilidad de
US11: no se tocan aquí.

- **E127 · K9 sin subir la entrega.** `EntregaVigente` sigue en K6b, como quedó con K7 y K8: el CDAT y la tasa negociada exigen la entrega K9 desplegada
  (`Lending.Savings.DeliveryNotAvailable`, `availableFrom = K9`); las pruebas la fijan. Subirla es del despliegue (Polish), con las demás.
- **E128 · El plan del CDAT (T166, `PlanDeCdat`).** El plazo se cuenta en días calendario desde el inicio y se parte en períodos de los días de la periodicidad
  (mensual 30, trimestral 90…; el último absorbe lo que sobre; al vencimiento, uno). Interés de n días `C × ((1 + r_d)^n − 1)` con `r_d` la tasa diaria
  compuesta de la base de la convención (= `(1+EA)^(n/B) − 1`); anticipado `C × i_v / (1 + i_v)`; cada período a la unidad sellada. Pago vencido el fin del
  período, anticipado el inicio; en día no hábil (sábado, domingo o `FESTIVO`) el siguiente hábil sin interés adicional (D19). Gracia en días hábiles desde el
  vencimiento. Plazo de emisión por días con meses de 30 (menos de 180; 180 a 360; más de 360 y menos de 540; 540 o más) — pendiente de validar por la
  contadora. Casos dorados (T167): `cdat-01` a `cdat-10` (481.244; 488.088; 6 × 79.741; 79.111; prórroga 10.468.564; reliquidación; descuento; festivo;
  plazo irregular) y `tasa-negociada-01/02`, todos por `calcular.py --verificar` (41 casos).
- **E129 · La causación cuadra con el plan.** Lo causado a k días del período es `Seis(C × ((1 + r_d)^k − 1) × f)` (f = 1 vencido, `1/(1+i_v)`
  anticipado) y el último día absorbe el redondeo contra el plan (R11); en el anticipado el interés por pagar queda negativo durante el período y la
  causación lo lleva a cero (contabilidad.md §6). El CDAT causa por su plan dentro de `CausacionDeCaptaciones`; la liquidación periódica de US5 ya no lo toca
  (clases que liquidan ≠ clases que causan). Pagar un período no mueve `LastSettledThrough`.
- **E130 · Condiciones selladas al fondear (T169, T170).** El CDAT nace en apertura con su propuesta en `PendingTermsJson` (no es hecho); la versión 1 y el
  plan se sellan en la operación de constitución, con inicio el día del fondeo, por `RegistroDeCuentas.SellarCondiciones`. Ruta nueva
  `POST /term-deposits/{id}/fund` (fondeo por traslado de lo que quedó esperando; permiso de constituir). Fondeo por recaudador: la consignación del contrato
  de cuentas en un CDAT en apertura, por el monto exacto (`FundingAmountMismatch`); un CDAT constituido no recibe consignaciones (`NoAdditionalDeposits`).
  Aprobar la apertura (listas) no activa un CDAT. La apertura común pasó a `AperturaDeCuentas` sin cambiar la de las demás cuentas.
- **E131 · Destinos.** Intereses y vencimiento: una cuenta a la vista viva del mismo titular (en el alcance) o un giro a un beneficiario
  (`AccountPayoutOrder` `TermDepositInterest`/`TermDepositRedemption` y `OrdenDeGiroEmitida` v2). Origen del fondeo: una cuenta de ahorro viva del titular.
- **E132 · La contrapartida de la fase 9 (resuelve `CounterpartUnknown`).** `TermDepositFundingV1` y `TermDepositDestinationV1` suman, opcionales al final, la
  otra cuenta con su titular, sus dimensiones y sus importes; el constructor la usa como contrapartida (las dos puntas cuadran, como el traslado); con giro,
  `DevolucionPorPagar`; con recaudador, el puente. `HechoDeCuenta` lleva el plazo de emisión de la última versión de condiciones (`IssueTermBand`). La
  activación (`LinesWithMatrix`) pregunta por los hechos del CDAT en cada plazo de emisión que admite el rango de la línea.
- **E133 · Tasa negociada (T168, T171).** Tabla nueva `LND_NegotiatedRateRequests` (la solicitud con su estado; data-model no la tenía). Reglas versionadas
  por código, terminar = versión inactiva. El criterio «puntos adicionales» de la matriz (`AttributionRule.MaxNegotiatedSpread`) casa cuando el margen pedido
  lo **supera**; cabe en la atribución si quien pide puede decidir el nivel efectivo (sin matriz, si tiene `Lending.NegotiatedRates.Approve`). La decide una
  persona con el nivel en su propia sesión (que ya exige segundo factor) o un órgano con punto de acta cerrado (asunto `NegotiatedRate`); segregación por
  persona. Con tasa variable el CDAT sella la tasa del día.
- **E134 · Ciclo (T172).** Subpaso `CicloDelCdat` después de la liquidación: paga cada período neto de retención (a cuenta con GMF o por giro); al cierre del
  último día de gracia aplica la instrucción efectiva (`MaturityInstructionJson` o la sellada): cancelar y pagar, o prorrogar si la línea lo hace y no se agotó
  el máximo y hay tasa; si no, el tratamiento (`StopAccruing` → vencido; `MoveToDemand` → a la cuenta a la vista del titular en la línea destino o vencido si no
  la tiene). Prórroga: inicio al vencer si la gracia causa, si no al terminar la gracia; tasa por la regla de la línea (`Agreed` cae a la tabla vigente);
  margen sólo si se conserva; capitaliza neto de retención; `CaptacionReclasificada` si cambia el plazo de emisión (sólo el hecho). CDAT embargado o de
  titular fallecido al vencer: vencido, a disposición, sin pago ni prórroga. Los períodos de un CDAT migrado con fecha de pago hasta el corte cuentan como pagados.
- **E135 · GMF de los intereses abonados en cuenta** con el hecho `RedencionDeCdatAbonada` (disposición del CDAT abonada en cuenta, Oficio 2787 de 2019),
  sobre la cuenta destino — pendiente de validar por la contadora.
- **E136 · Instrucción, renovación y redención (T173).** La instrucción se cambia hasta el fin de la gracia en `MaturityInstructionJson` (la sellada es hecho).
  Renovar sólo entre el vencimiento y el fin de la gracia; paga antes lo pendiente; margen sólo dentro de la atribución (`NegotiationRequiresApproval`).
  Redención anticipada sólo si la línea la admite, con penalidad por reliquidación (descuenta lo ya pagado en la versión; lo pagado de más sale del capital)
  o por descuento, y atribución con monto `Lending.TermDeposits.Redeem` (limitable; 202 `PendingApproval`); en la gracia es cancelación sin penalidad. Con
  giro la cuenta queda `Closing`; una orden rechazada devuelve el saldo. La cancelación de cuentas remite el CDAT a su ruta.
- **E137 · Retiro del asociado (T175).** Los CDAT vivos van como líneas informativas `TermDepositBalance`, no suman a la devolución ni impiden `Paid`; siguen
  hasta su vencimiento (la línea no tiene una condición que diga otra cosa).
- **E138 · Vistas (T176).** `term-deposit-maturities` y `negotiated-rates` (K9) y `depositor-concentration` (K8), con datos del titular y alcance.
- **E139 · Migración de CDAT (pendiente de la fase 10).** Parte `term-deposits` (plantilla §4.3, K9): al aplicar se regenera el plan; el vencimiento, los
  períodos pagados (los de fecha de pago hasta el corte) y el interés causado (más de un peso de diferencia: `Lending.TermDeposit.ScheduleMismatch`) se
  cotejan; la cuenta destino es a la vista del mismo titular, de la carga o viva. Al activar nace el CDAT con su versión 1 `Migration`, el plan, el saldo inicial
  de capital e interés (seis decimales; en el anticipado puede ser negativo) ligado a su versión y período, después de las demás cuentas; sin hechos. El
  plazo de emisión entra al cuadre y a la conciliación del capital del CDAT. El embargo de la fila es sólo una marca hasta US11 (`GarnishmentPending`).
- **E140 · Lo que queda.** El título «CDAT, no negociable» (documento `TermDepositTitle`) lo emite US13 (T195/T196): `GET /{id}/titles` responde vacío hasta
  entonces. El aviso al titular de la prórroga y del vencimiento no se encola todavía. Las e2e de US6 (`CdatTests`) y de la migración de un CDAT están
  **escritas y compiladas; e2e en la corrida final**.

- **E141 · Pantallas (T179).** `/cartera/cdat`, `/cartera/cdat/nuevo` y `/cartera/cdat/{id}`, y el editor de reglas de tasa negociada dentro del detalle de
  una línea de CDAT que la admite (sin ruta propia); la apertura de cuentas sigue sin ofrecer líneas de CDAT. Tasas y márgenes en puntos, viajan como
  fracción. Quedan fuera por ahora: el giro desde la pantalla va siempre al titular con su cuenta bancaria registrada (sin selector de beneficiario), la
  renovación y la instrucción no cambian el destino, los firmantes del fondeo no se piden en pantalla, la redención es a la fecha de hoy, el editor de reglas
  no ofrece oficina ni tasa base como criterio y la ficha del CDAT no muestra el saldo ni el extracto (las rutas de §4 existen).

### 18.1 Preguntas para el dueño

- **CDAT embargado o de titular fallecido al vencer (E134).** A: queda vencido a disposición (lo construido); B: se prorroga capitalizando; C: pasa a la vista.
  Recomendado: A hasta que US11 traiga la orden del juzgado.
- **Máximo de prórrogas cero.** A: el validador de la línea lo rechaza (lo construido: sin prórroga se apaga `AutoRollover`); B: admitir 0 como «nunca
  prorrogar». Recomendado: A.
- **Regla `Agreed` en la prórroga automática.** A: cae a la tasa vigente de la tabla (lo construido); B: conserva la tasa pactada. Recomendado: B si el
  reglamento de la cooperativa lo pacta así.
- **Cuenta de los intereses (E131).** A: sólo a la vista del mismo titular (lo construido); B: también de un cotitular u otro titular, con el GMF del traslado.
  Recomendado: A.
- **Giro de un CDAT migrado.** A: a la cuenta bancaria registrada del titular (lo construido); B: columnas de banco en la plantilla. Recomendado: B si la
  cooperativa paga intereses a terceros.
- **Orden de redención rechazada después de ajustes y penalidad.** A: se reversa la operación entera; B: se devuelve sólo el capital. Recomendado: A.

### 18.2 Para la contadora (pendiente de validar)

- Plazo de emisión por días (meses de 30) y la frontera de 12 meses (CUIF 211010 incluye 12 meses).
- GMF de los intereses abonados en cuenta (E135) y su clase generadora (CDAT).
- La penalidad por reliquidación como ajuste a la baja de la causación (devuelve gasto) y lo pagado de más descontado del capital (hoy contra el ingreso por
  penalidad).
- El interés anticipado con el interés por pagar en débito durante el período (o un diferido, P1).
- Los defectos de D17/D18 (prórroga automática sin capitalizar, 3 días hábiles de gracia sin causación, redención anticipada no admitida).

## 19. Enmienda del ahorro contractual (2026-10-19, T181–T186, US7, K9)

Construida la historia 7 y los pendientes del contractual de las fases 9, 10 y 11. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`,
`contracts/` y `tasks.md` donde choquen. Cambio de esquema: `CaptacionesAportes` **regenerada** en los dos motores (PostgreSQL `20261019104128`, SQL Server
`20261019104056`) con `LND_MemberAccountTermsVersions.MigratedPaidInstallments` y siete columnas del contrato en `LND_MigrationAccountRows`
(`ContractualInstallment`, `ContractualPaidInstallments`, `ContractualLateInstallments`, `ContractualMaturityDate`, `AgreedAnnualRate`,
`ContractualBonusAnnualRate`, `ContractualStatus`); el SQL escrito a mano (T044, `NOM`, `ASO`) se reinsertó sin cambios —el antes y el después difieren sólo
en esas ocho columnas— y la paridad pasa. Las migraciones congeladas de la 013 y `RetiroDeCaptacionesHeredadas` no se tocaron.

**Norma aplicada** (investigación financiera §3.4): el ahorro contractual es un depósito a término con cuotas pactadas cuyo premio y castigo son del
reglamento de cada entidad (no hay tasa ni tolerancia legal), por eso la cuota mínima, el rango de plazo, las tasas bonificada y de incumplimiento, la
tolerancia, el efecto del incumplimiento y cuándo se reconoce la bonificación son condición de la línea con vigencia, y los días de gracia un parámetro con
defecto «pendiente de validar». La bonificación es rendimiento financiero: lleva la retención del contractual (`Retencion.TarifaContractual`, DUR 1625 de 2016
art. 1.2.4.2.83 y siguientes) como el interés, y entra al certificado y a los acumulados del año. El plazo de emisión sigue las cuentas 2110 del CUIF de la
Supersolidaria como en el CDAT (E128), por meses.

- **E142 · El motor (T181, `AhorroContractual`).** Valor final de una serie de aportes: `Σ A × ((1 + r_d)^N − 1)` con `r_d` la tasa diaria compuesta de la
  base de la convención y N los días de la convención desde cada aporte hasta el corte (capitalizar cada mes no cambia el valor final: la capitalización
  compuesta es la misma). **La bonificación y la penalidad se cuentan sobre los aportes, no sobre el saldo**: bonificación = interés a la tasa bonificada −
  interés a la tasa base; penalidad = interés a la base − interés al castigo; las dos a la unidad sellada. Cuotas: con periodicidad de meses, cada k meses
  desde el inicio (`AddMonths`, conserva el día; el 31 cae al último día del mes); con periodicidad de días (semanal, decadal, quincenal), cada k días.
  Vencimiento = inicio + plazo en meses. Siete casos dorados `contractual-01` a `-07` (2.465.306; 2.486.777 y bonificación 21.471; diez aportes: 64.332 a la
  base, 32.331 al castigo, penalidad 32.001; tolerancia y gracia; fechas reales en Actual/365, bonificación 21.463; quincenal de 18 meses, 36 cuotas,
  `EighteenOrMore`; trimestral de 6 meses), verificados por `calcular.py --verificar` (48 casos).
- **E143 · El contrato se sella al abrir.** `POST /accounts` con `contractual { installment, termMonths, maturityInstruction? }` en una línea contractual (sin el
  bloque responde `Lending.Contractual.TermsRequired`; con el bloque en otra clase, `.TermsNotAllowed`). El contrato se **revisa antes** de abrir —cuota
  mínima (`Lending.Contractual.InstallmentBelowMinimum`), rango de plazo (`.TermOutOfRange`), que quepa al menos una cuota (`.NoInstallments`), la tasa base
  y el destino de la instrucción—, para no dejar una cuenta abierta sin contrato. Dentro de la transacción de la apertura: la operación `ContractualSetup`
  (24, monto 0, prefijo `ACT-`), la versión 1 de condiciones con causa `ContractualSetup` y la **primera cuota** (vence un período después de abrir). En la
  versión, `PrincipalAmount` = **lo pactado** (cuota × cuotas del plan; el `CHECK > 0` de la tabla lo exige), `ProjectedFinalAmount` = valor final cumpliendo,
  la tasa base de la línea a lo pactado (fija, variable del día o el tramo por saldo) y las bonificada y de castigo de la línea, la tolerancia, el efecto y el
  reconocimiento: el contrato no cambia aunque la línea cambie después.
- **E144 · Cuotas y movimientos.** `GeneradorDeCuotas` delega al contractual en `GeneradorDeCuotasDeContractual`: genera la cuota siguiente el día que vence la
  anterior, con la marca de período del control (año-mes del vencimiento y ordinal para las periodicidades de días). Se consigna **sólo por cuotas**
  (`Installments`); la libre responde `Lending.Contractual.FreeDepositNotAllowed`; lo que excede la cuota pendiente no se admite. El traslado **a** un
  contractual paga cuotas igual. **No admite retiros** antes de vencer (`Lending.Contractual.WithdrawalNotAllowed`), salvo la entrega del propio contrato. La
  causación y la liquidación mensual son las de US5, con la tasa **sellada del día** (la base, o la de castigo desde el incumplimiento) y sin causar después
  del vencimiento.
- **E145 · El ciclo del contractual (subpaso «Contractual», después del CDAT).** Por cuenta, en su transacción con el cerrojo: (1) **vencimiento**: con todas
  las cuotas pagadas, **cumple** —se abona la bonificación al capital con su retención (E148) y se entrega—; con cuotas sin
  pagar espera los días de gracia de la última y luego queda **incumplido** (causa `MaturityWithPendingInstallments`), se reliquida al castigo y se entrega;
  (2) **antes del vencimiento**, más cuotas atrasadas que las toleradas —atrasada = vencida hace más de los días de gracia— lo dejan **incumplido** (causa
  `Arrears`) con el efecto de la línea: `BreachRate` sigue hasta el vencimiento a la tasa de castigo (versión de condiciones `ContractualBreach` desde ese
  día), `MoveToDemand` liquida y traslada a una cuenta a la vista viva del titular, `Cancel` liquida y entrega según la instrucción; (3) con la línea en
  `Provisioned`, el día del vencimiento de cada cuota se provisiona la bonificación a la fecha. **Entregar** = liquidar a la fecha, anular las cuotas sin
  pagar (`CancelledAt`, con la operación que termina), y trasladar (`TransferToAccount`) o girar (`DisbursementOrder`); sin instrucción el contrato queda
  **vencido, a disposición**, y se entrega por «Cancelación».
- **E146 · Días de gracia de las cuotas.** Parámetro nuevo **`Captaciones.DiasDeGraciaDeCuotaContractual`** (K9, entero ≥ 0, ámbito general y línea, defecto
  **5**, pendiente de validar): la nómina y la pagaduría descuentan unos días después del vencimiento; sin gracia, cada cuota descontada por nómina se
  contaría atrasada.
- **E147 · El incumplimiento.** Operación `ContractualBreach` (25, prefijo `ICT-`, monto = la penalidad): la reliquidación al castigo de los aportes hasta la
  fecha (o el vencimiento, si ya venció). **La penalidad sale del capital** (transacción `PenaltyApplied`, concepto `Principal`), no del interés por pagar: la
  liquidación siguiente abona entero lo causado (se cuenta sobre las filas de causación, no sobre el saldo del concepto), y descontarlo de ahí dejaba el
  interés por pagar negativo y un residuo de capital que la entrega no vaciaba (lo destapó la prueba del efecto `Cancel`). Si había bonificación provisionada,
  se reversa en la misma unidad. Hechos: `PenalidadAplicada` (clase `ContractualBreach`) y, si hay reversión, `BonificacionReconocida` en negativo. Aviso al
  titular con la plantilla nueva **`CONTRATINC`** (correo, SMS y WhatsApp; variables `CuotasAtrasadasDelContrato`, `FechaDeIncumplimiento`,
  `EfectoDelIncumplimiento`; origen `ContractualSavings`), encolado sin tumbar el incumplimiento si falla.
- **E148 · La bonificación (T183).** Operación `ContractualBonus` (26) para la provisión (prefijo `BCT-`, concepto `Bonus`, transacción `BonusRecognized`) y la
  reversión; el abono al cumplir va en la operación `ContractualMaturity` (20, prefijo `VCC-`) al **capital** con su retención (`WithholdingApplied`) y sus
  acumulados del año (`WithholdingAccumulation.OrigenBonificacion = "Bonus"`). Con `Provisioned`, al cumplir se abona lo provisionado y la diferencia. El hecho
  `BonificacionReconocida` (`BON`) ahora acredita `InteresPorPagar` (provisión), `CapitalDeCaptacion` o `CapitalDeCaptacionInactiva` (abono), con
  acompañantes `PenalidadAplicada` y **`RetencionPracticada`**. La validación previa (A8) y la activación preguntan por los hechos representativos del
  contractual por cada plazo de emisión que admite la línea (rango en meses → bandas).
- **E149 · Cancelar antes de vencer.** Por la ruta de siempre (`POST /accounts/{id}/close`): un contrato vigente se **incumple** (causa
  `EarlyTermination`) y se reliquida al castigo antes de liquidar y entregar; la vista previa trae `contractualPenalty` y `contractualExplanation`. Un contrato
  vencido con el vencimiento todavía pendiente del proceso responde `Lending.Contractual.MaturityPending`. Arreglado de paso: el GMF de la entrega se calcula
  con el **hecho generador del traslado** cuando se traslada (antes, con el de la cancelación: el traslado exento entre cuentas del mismo titular cobraba GMF
  en la vista previa y la cuenta quedaba `Closing` con el GMF de diferencia); vale también para cancelar una cuenta a la vista.
- **E150 · El libro y el motor no dan lo mismo en 30/360 (es correcto).** El libro causa día a día con la convención de la línea y liquida cada mes al peso; en
  30/360, del 28 de febrero al 1 de marzo cuenta tres días. Con 12 × 200.000 al 6 % el libro abona ~65.549 de interés base y el motor 65.306: la diferencia
  (~0,4 %) es de la convención, no un error. **La bonificación y la penalidad salen del motor** (21.471 exactos), y lo entregado al cumplir es lo que el libro
  tiene más la bonificación (2.487.020 en la prueba). Con Actual/365 la diferencia es de redondeo.
- **E151 · Rutas (T182, T185).** `GET /accounts/{id}/contractual` (`Accounts.Read`, **nueva**, no estaba en api.md: el contrato con su plan, cumplimiento,
  proyección e instrucción), `GET /accounts/{id}/terms?version=` (`Accounts.Read`, las versiones selladas de un CDAT o un contractual) y
  `PUT /accounts/{id}/maturity-instruction` (`Accounts.Parties.Manage`, con clave; cuerpo **plano** `{ kind, targetAccountPublicId?, payee?, reason }` como
  dice api.md; sólo un contractual que no haya terminado: `Lending.Savings.NotContractual`, `Lending.Contractual.NotCurrent`,
  `.DestinationInvalid`). La instrucción se guarda en `LND_MemberAccounts.MaturityInstructionJson`; la sellada al abrir es hecho. `MemberAccountDto.contractual`
  trae el resumen (`installment`, `periodicity`, `installments`, `paidInstallments`, `arrearsInstallments`, `status`, `maturityDate`). Las tres consultas
  respetan el alcance y la lectura queda auditada (fase 11).
- **E152 · Pantalla (T185).** `ContratoDeAhorro` dentro de la ficha de la cuenta (`/cartera/ahorros/{id}`, sólo en un contractual): condiciones selladas,
  cumplimiento con aviso si supera la tolerancia, lo que lleva y lo proyectado (a la base y cumpliendo), plan de cuotas, historia de condiciones e instrucción
  al vencimiento. La apertura (`/cartera/ahorros`) pide cuota y plazo al elegir una línea contractual; la vista previa de la cancelación muestra el costo de
  cancelar el contrato. Textos de las clases de operación 24–26 y de fila 20–22 en el cliente.
- **E153 · Migración de saldos (fase 10).** La plantilla `member-accounts` admite la clase `Contractual` con su contrato: `cuotaContractual`, `cuotasPagadas` y
  `fechaDeVencimientoContractual` obligatorias, `cuotasAtrasadas`, `tasaPactadaEA` (vacía = la de la línea al corte), `tasaBonificadaEA` (vacía = la de la
  línea) y `estadoContractual` nuevo (`Current` o `Breached`; `Fulfilled` no: un contrato vencido se liquida en el sistema anterior antes de migrar, por eso el
  vencimiento debe ser **posterior al corte**). El plazo son los meses enteros entre la apertura y el vencimiento; las cuotas se rehacen hacia atrás desde el
  vencimiento con la periodicidad de la línea. Aviso (no error) si pagadas + atrasadas no suman las vencidas al corte (`Lending.Migration.ContractualInstallmentsDiffer`)
  o si la cuota queda bajo la mínima actual de la línea; las cuotas atrasadas de `account-installments` deben caer en un vencimiento del plan
  (`Lending.Migration.ContractualInstallmentNotInPlan`). Al nacer: versión 1 de condiciones con causa `Migration` y `MigratedPaidInstallments` (las pagadas
  cuentan como aportes **fechados en su vencimiento del plan** para la bonificación), marca de la última cuota vencida al corte y, si venía incumplido, la
  operación `ContractualBreach` sin penalidad y la versión de castigo desde el corte.
- **E154 · Lo que queda.** El certificado tributario de la bonificación y el extracto del contrato salen con US13; el contractual en la liquidación del retiro
  del asociado sigue la regla del CDAT (hasta su vencimiento, línea informativa) cuando US13 la extienda; la entrega por fallecimiento es de K10.

### 19.1 Preguntas al dueño

- **Bonificación cuando se cumple con cuotas pagadas tarde (dentro de la tolerancia).** A: se bonifica igual si al vencer todas están pagadas (lo construido);
  B: cada cuota pagada tarde pierde su parte de la bonificación; C: se pierde toda. Recomendado: A, salvo que el reglamento diga otra cosa.
- **Cancelación anticipada a pedido del titular.** A: se trata como incumplimiento con la reliquidación al castigo (lo construido); B: además, una penalidad
  fija o porcentual de la línea. Recomendado: A (B se puede sumar como cargo de la línea).
- **Efecto del incumplimiento sin instrucción y con efecto `Cancel`.** A: queda vencido, a disposición, hasta que se entregue por «Cancelación» (lo
  construido); B: se traslada a la cuenta a la vista del titular si tiene una. Recomendado: B si la cooperativa quiere evitar saldos inmovilizados.

### 19.2 Para la contadora (pendiente de validar)

- Los defectos de la línea contractual y el parámetro de 5 días de gracia.
- La bonificación abonada como gasto financiero (cuenta del rol «gasto por bonificación») y su retención en la misma tarifa del contractual.
- La penalidad por incumplimiento: hoy descuenta el capital y va al ingreso por penalidad; alternativa, ajustar a la baja el gasto de intereses del año.
- La provisión de la bonificación (P26) en el interés por pagar y su reversión al incumplir.
- El plazo de emisión del contractual por meses (menos de 6; 6 a 12; 12 a 18; 18 o más) para las cuentas 2110 del CUIF.
- La diferencia de la convención 30/360 entre el libro y la cifra del reglamento (E150).

## 20. Enmienda de los bloqueos, los embargos y la inembargabilidad (2026-10-19, T187–T193, US11, K8)

Construida la historia 11 y los pendientes de embargos de las fases 9 a 12. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`,
`contracts/` y `tasks.md` donde choquen. Cambio de esquema: `CaptacionesAportes` **regenerada** en los dos motores (mismos sellos: PostgreSQL
`20261019104128`, SQL Server `20261019104056`; las operaciones nuevas al final de `Up` y al principio de `Down`) con `LND_AccountHolds`, `LND_Garnishments`,
`LND_GarnishmentResponses`, `LND_MigrationHoldRows`, `LND_MemberAccounts.AccrualResumedOn` y `LND_MemberAccountOperations.GarnishmentId`; el SQL escrito a
mano (T044, `NOM`, `ASO`) quedó intacto —el antes y el después del `.cs` sólo difieren en líneas agregadas— y la paridad pasa. Las migraciones congeladas de la
013 y `RetiroDeCaptacionesHeredadas` no se tocaron. `EntregaVigente` sigue en K6b (E127): las claves `Embargos.*` se leen con sus defectos y
`LIMITE_INEMBARGABLE` se admite en la central desde K8.

**Norma aplicada** (todo «pendiente de validar por el abogado»; los valores van como valores nacionales con vigencia, nunca en el código):

- **Depósitos de ahorro, embargo judicial.** Son inembargables hasta el monto que fija la autoridad competente, salvo para el pago de créditos alimentarios
  (CGP, Ley 1564 de 2012, art. 594 num. 2). El monto lo publica cada año la Superintendencia Financiera por carta circular (para el período del 1 de octubre de
  2025 al 30 de septiembre de 2026, $55.099.308 según la Circular 058 de 2025, valor que confirma el maestro al registrarlo; el anterior, $52.385.727 por la
  Circular 061 de 2024). Se registra como `LIMITE_INEMBARGABLE` (pesos, sin semilla: cada publicación con su fuente). Si aplica a los depósitos de una
  cooperativa vigilada por la Supersolidaria lo discute la doctrina (P5 sigue abierta); rige D20: se aplica a las personas naturales mientras el abogado no diga
  otra cosa.
- **Cobro coactivo (DIAN y entidades públicas).** El límite es de 25 SMMLV depositados en la **cuenta de ahorros más antigua** de la persona natural; la persona
  jurídica no tiene límite (Estatuto Tributario art. 837-1, modificado por la Ley 1066 de 2006 art. 9; la Ley 1066 art. 5 da la jurisdicción coactiva a las
  entidades públicas). Va como `LIMITE` `EMBARGO_COACTIVO_SMMLV` = 25 veces (sembrado, pendiente de validar) por el `SMMLV` vigente.
- **Aportes sociales.** Inembargables, afectados desde su origen como garantía de las obligaciones con la cooperativa (Ley 79 de 1988, art. 49); en los fondos
  de empleados también el ahorro permanente (Decreto Ley 1481 de 1989, art. 16, en la redacción de la Ley 2496 de 2025): por eso la clase inembargable es un
  parámetro de cada entidad.
- **Perfeccionamiento y consignación.** El embargo de sumas depositadas se entiende consumado con la recepción del oficio, la entidad constituye el depósito
  y lo pone a disposición del juzgado dentro de los tres días siguientes, y el valor de la medida lo fija el juez con el crédito y las costas más un 50 %
  (CGP art. 593 num. 10). De ahí que el embargo **rija al registrarse** (sin aprobación) y que el plazo de respuesta sea un parámetro informativo.
- **Alimentos.** La excepción del CGP art. 594 num. 2; la prelación de los descuentos judiciales por alimentos sobre los de la cooperativa (Ley 79 de 1988, art.
  144) no se toca aquí.
- Fuentes consultadas: Gerencie, «Embargo de cuentas de ahorros por cooperativas» <https://www.gerencie.com/embargo-de-cuentas-de-ahorros-por-cooperativas.html>;
  Oficio DIAN 1763 de 2026 <https://normograma.dian.gov.co/dian/compilacion/docs/oficio_dian_1763_2026.htm>; Ley 2496 de 2025
  <https://normativa.colpensiones.gov.co/compilacion/docs/ley_2496_2025.htm>; los montos de la Superfinanciera por las circulares citadas (fuente secundaria:
  <https://www.jfk.com.co/uploads/mng/INEMBARGABILIDAD%202024-2025.pdf>). El texto oficial lo coteja el abogado.

- **E155 · El motor (T188, `LimiteInembargable`).** Puro y sin valores legales: recibe las cuentas de la persona (saldo del titular, lo que ya retienen otros
  embargos, si su clase la protege el límite y si es embargable), el tope pendiente del embargo (valor de la medida menos lo consignado), si es por alimentos, el
  límite en pesos (nulo = ninguno) y su aplicación. Orden: las clases de `Embargos.OrdenDeCuentas` (las que no nombra, después), luego la apertura, luego el Id.
  Con el límite **sobre la suma**, lo protegido se asigna a las cuentas protegidas **de la última a la primera** del orden (así se retiene primero de las que el
  orden pone delante y lo protegido queda en cuentas protegidas); **sobre la más antigua**, sólo esa cuenta guarda hasta el límite. El límite es de la
  **persona**: lo que retiene un embargo anterior no se repite (concurrencia por orden de recibo). Siete casos dorados `embargo-01` a `-07` por `calcular.py`
  (55 casos verificados en total): 15.000.000 del exceso de 70.000.000 sobre 50.000.000; alimentos; orden de clases; coactivo sobre la más antigua; segundo
  embargo sin nada embargable; CDAT no protegido con el permanente inembargable; persona jurídica.
- **E156 · Parámetros por entidad (K8, todos pendientes de validar).** `Embargos.ClasesProtegidas` (defecto `[Demand, Contractual, Permanent]`: el CDAT no es
  depósito de ahorro), `Embargos.ClasesInembargables` (`[]`; un fondo de empleados pone `[Permanent]`), `Embargos.AplicacionDelLimite` (judicial sobre la suma,
  coactivos sobre la más antigua), `Embargos.LimiteSoloPersonasNaturales` (true), `Embargos.DiasHabilesParaResponder` (3, informativo) y
  `Embargos.PlantillasDeRespuesta` (`{}`: rigen los textos estándar). El orden ya existía (`Embargos.OrdenDeCuentas`, vacío = antigüedad).
- **E157 · Rige al registrarse; quien registra no levanta.** `data-model.md` §5.2 tenía `Registered → Active` por aprobación de otra persona; la norma (CGP 593
  num. 10) consuma el embargo con el oficio, así que nace `Active` y retiene en el mismo guardado. La segregación de FR-005 queda en el par del catálogo
  (`SegregacionDeCaptaciones.Embargo`): registrar es `Radicar`, levantar es `Decidir` (`Lending.Segregation.Conflict`). `Registered` no se usa; `Rejected` es el
  oficio que sólo nombra aportes. El plazo de respuesta (`ResponseDueOn`, días hábiles sin sábados, domingos ni festivos) es **informativo**: el detalle y la lista
  avisan `Lending.Garnishment.ResponseOverdue` y no bloquean nada. Registrar también pone el bloqueo de deudor `Garnishment` de la 013
  (`garnishment:{embargo}`), que la originación ya mira, y levantarlo lo quita.
- **E158 · Respuestas al juzgado.** `LND_GarnishmentResponses` guarda la clase, la fecha de envío, el texto enviado (la plantilla de la entidad o la estándar, llena
  con `{Oficio}`, `{Proceso}`, `{Embargado}`, `{Retenido}`, `{Cuentas}`…), el soporte firmado y lo retenido y consignado a la fecha. `IssuedDocumentId` nace **nulo**:
  el documento emitido de la Cartera con código de verificación es de US13 (T195), que lo llenará. Ruta nueva `GET /garnishments/{id}/responses/draft?kind=` (el
  borrador, lectura auditada). El aviso «aportes inembargables» se registra solo al recibir un oficio que los nombra.
- **E159 · La reclasificación por la marca (pendiente de la fase 9).** `ReclasificacionDeCuenta` (`Application/Lending/Deposits/Holds`) es el único que emite
  `CaptacionReclasificada` por un cambio de condición: una operación `Reclassification` (prefijo `RCL-`, valor 0, sin filas en el libro, con `GarnishmentId`) y el
  hecho con los saldos de capital e interés por pagar de las dimensiones anteriores a las nuevas (causas `Garnishment`/`GarnishmentRelease`,
  `GmfExemption`/`GmfExemptionEnd`; `Inactivity`/`Reactivation` quedan declaradas para US14, que sólo tiene que llamar a `ReclasificarAsync` al inactivar o
  reactivar). Sin saldo no nace nada. La marca de la exención del GMF que rige **hoy** (marcar desde hoy, terminar ayer, reemplazar la vigente) reclasifica en el
  mismo comando; la que rige desde una fecha futura no (pendiente: un subpaso diario de marcas, junto con la inactividad de US14). No pasa por la validación
  previa: la orden del juez no se detiene por la matriz (con «Bloquear» y una regla faltante, el hecho queda en la bandeja y alerta al consumirse).
- **E160 · Embargo total y causación (FR-065).** `IsGarnished` es ahora «retiene algo»; el embargo **total** es lo embargado ≥ el saldo del titular.
  `RetencionDeEmbargos` suspende la causación desde el día en que la cuenta queda totalmente embargada (si la línea lo dice) y la reanuda desde el día en que
  deja de estarlo (`AccrualResumedOn`: los días suspendidos siguen suspendidos en la ventana). `LectorDeInteres.Suspendido` y la causación leen la marca
  (`MemberAccount.SuspendidaEl`), no `IsGarnished` (E101 queda reemplazada). El CDAT no se suspende: sigue su plan y, si está embargado al vencer, queda vencido a
  disposición (E134, revisado: la marca la pone el bloqueo del embargo sobre el CDAT).
- **E161 · Contrato de las rutas (api.md §8).** Los nombres del contrato (`officeReference`, `isAlimony`, `retained[]`, `unembargableLimit`, `responses[]`) más
  `authority` (`Court`, `TaxAuthority`, `OtherPublicEntity`), `processNumber`, `officeDate`, `receivedOn`, `claimantName`, `includesContributions`,
  `destinationBankPublicId/AccountNumber`, `responseDueOn`/`responseOverdue`, `explanation[]` y `warnings[]`. **`ContributionsNotGarnishable` y
  `NothingGarnishable` son avisos en `warnings` de un 201**, no errores (un 422 habría revertido la respuesta registrada, que la spec exige guardar). Estados en el
  JSON por número (`GarnishmentStatus`: 2 activo, 3 levantado, 4 rechazado). El desembargo con `amount` menor que la medida la reduce (sigue activo); sin
  `amount`, la levanta toda. Bloqueos: `GET/POST /api/lending/accounts/{id}/holds` y `POST …/holds/{holdId}/release` (`Lending.Holds.Manage`); por esa vía sólo los
  motivos `Internal`, `LoanCollateral`, `SignaturePending`, `Other` (`Lending.Holds.OwnedByProcess` para los demás); el de garantía exige un crédito vivo y no se
  levanta mientras el crédito viva (`Lending.Holds.LoanStillActive`); uno por valor no supera el disponible (`Lending.Holds.InsufficientAvailable`); uno total deja el
  disponible en cero.
- **E162 · Lo retenido y el disponible.** `RetencionDeEmbargos` es el único escritor de los bloqueos `Garnishment` (si el valor cambia, levanta el anterior y pone
  otro: queda la historia) y lo llaman el registro, el levantamiento, la consignación y el subpaso diario **«Embargos»** (antes de la causación), que recalcula a
  las personas con embargos activos —lo consignado después del registro, los depósitos posteriores, un CDAT que venció— una vez por día bajo el cerrojo de la
  persona (`LND_DebtorControls`) y de sus cuentas, saltando las ocupadas. `LectorDeRestricciones` es el único lector: el disponible de `ReglasDeMovimientos` (y por
  él el del contrato de cuentas, el retiro, el traslado, la cancelación), el saldo a (D, R) y la consulta del contrato (que nombra «Embargo» y «Bloqueo») restan
  lo bloqueado y lo embargado. Un depósito posterior al registro **no** se retiene en línea: el retiro del mismo día puede tomarlo hasta el recálculo de la noche
  (pregunta al dueño).
- **E163 · La consignación (FR-091).** `POST /{id}/consign` recalcula, consigna en el orden del embargo (un CDAT sólo si venció), una operación
  `GarnishmentConsignment` por cuenta (prefijo `EMB-`, filas `GarnishmentConsigned` de capital y, si no alcanza, de interés por pagar), su orden de giro
  (`AccountPayoutOrder` `GarnishmentConsignment`, beneficiario el juzgado, la cuenta de depósitos judiciales del pedido o del embargo) y `EmbargoConsignado` +
  `OrdenDeGiroEmitida` v2; no supera lo retenido (`Lending.Garnishment.ExceedsRetained`). Pasa por la validación previa de pantalla (D23). Lo consignado no se
  guarda: es la suma de esas operaciones no reversadas. Si Tesorería rechaza el giro, el reverso de la operación devuelve el saldo y lo deja retenido para el mismo
  embargo; el reverso lleva, opcional al final, `CounterpartPersonPublicId` (el juzgado, tercero de los embargos por consignar) y el constructor lo asienta contra
  `EmbargosPorConsignar`. La consignación y la reclasificación no se reversan desde la pantalla. **El GMF de la consignación no se cobra**: no es hecho generador
  del parámetro y el motor de GMF no está cableado en ella (pregunta para la contadora).
- **E164 · Alcance, segregación y lecturas (fase 11).** Un embargo exige el alcance sobre **todas** las cuentas de la persona (falla cerrado, como el resumen de
  aportes: revela lo retenido de cada una); los bloqueos, la cuenta en el alcance; la vista `garnishments` deja fuera a la persona con una cuenta fuera del
  alcance. Evento nuevo **`Lending.Garnishment.Viewed`** (la lista, el detalle y el borrador), en `LecturasSensiblesDeCaptaciones`. El soporte (oficio, desembargo,
  respuesta) es el adjunto nuevo `LendingGarnishmentSupport` con dueño la persona embargada: se lee con `Lending.Accounts.Read`, lo sube quien registra o levanta
  embargos o administra bloqueos y no se borra.
- **E165 · Vista `garnishments` (K8, con datos del titular).** Los embargos recibidos en un rango con autoridad, oficio, medida, alimentos, estado, retenido,
  consignado, plazo de respuesta y respuestas; filtro `status`.
- **E166 · Migración (pendiente de la fase 10).** La parte `holds-and-garnishments` (§4.6) se importa con K8: `cuenta`, `tipo` (`Hold` o `Garnishment`; `Deceased`
  llega con K10), `referencia` (llave con la cuenta y el tipo), `motivo`, `credito`, `valor`/`total` del bloqueo y, del embargo, `juzgado` (persona viva),
  `autoridad`, `proceso`, `demandante`, `valorDeLaMedida`, `esPorAlimentos`, `fechaDelOficio` y `retenido`. La aplicación valida que la cuenta esté en la carga,
  que un embargo no recaiga sobre aportes ni retenga más que el saldo, que un oficio sea de una sola persona y que la garantía nombre un crédito vivo. Al activar
  nacen los embargos (uno por persona y oficio, activos, sin soporte —se adjunta después—) y los bloqueos con lo retenido **al corte** (no se recalcula al
  activar; el recálculo del día siguiente aplica los parámetros de la entidad), y la cuenta queda marcada (sin hecho: la migración no emite). La marca de embargo de
  la fila del CDAT (E139) sigue como marca. El bloqueo de deudor de la 013 no se pone en la migración de captaciones (la de créditos tiene su parte de bloqueos).
- **E167 · Lo que queda.** El documento emitido de la respuesta al juzgado (US13); la reclasificación de la marca exenta que rige desde una fecha futura y la de la
  inactividad (US14); el GMF de la consignación si la contadora lo decide; la e2e (`EmbargosTests`) **escrita y compilada; e2e en la corrida final**.

### 20.1 Preguntas para el dueño

- **Depósitos posteriores al registro del embargo (E162).** A: se retienen en el recálculo de la noche (lo construido; el titular puede retirar el mismo día lo
  que consigne después del oficio); B: el retiro y el traslado recalculan en línea al leer el disponible de una persona con embargos con tope pendiente (más lento
  en esas cuentas); C: un bloqueo total automático de la cuenta mientras el embargo tenga tope pendiente. Recomendado: B.
- **Embargo y aprobación (E157).** A: rige al registrarse y otra persona sólo levanta (lo construido, por el CGP 593 num. 10); B: queda «registrado» hasta que
  otra persona lo apruebe (data-model original). Recomendado: A.
- **Concurrencia de embargos (E155).** A: por orden de recibo, el primero toma lo embargable y los siguientes lo que sobre (lo construido); B: a prorrata entre los
  embargos activos; C: alimentos primero, luego por orden de recibo. Recomendado: C (la prelación legal de alimentos), a validar por el abogado.
- **Bloqueo de deudor en la Cartera (E157).** A: registrar un embargo de depósitos pone el bloqueo de deudor `Garnishment` que la originación mira (lo
  construido); B: son independientes. Recomendado: A.

### 20.2 Para la contadora y el abogado (pendiente de validar)

- El valor de `LIMITE_INEMBARGABLE` de cada período (Superintendencia Financiera) y si aplica a los depósitos de la cooperativa (P5); los 25 SMMLV del coactivo.
- Qué clases son depósitos de ahorro protegidos (el contractual y el permanente) y cuáles son inembargables por estatuto (el permanente en fondos de empleados).
- Que el límite proteja sólo a personas naturales también en el embargo judicial; la aplicación sobre la suma o la cuenta más antigua por autoridad.
- Los textos estándar de las cinco respuestas al juzgado y los tres días hábiles de respuesta.
- La cuenta de los embargos por consignar (`EmbargosPorConsignar`) y si la cuenta embargada va a otra cuenta contable que la estándar (entonces la reclasificación
  mueve saldos entre cuentas; con la misma cuenta no deja líneas).
- El GMF de la consignación al juzgado (E163).

## 21. Enmienda de los documentos al titular (2026-10-19, T194–T200, US13, K8/K9)

Construida la historia 13 y los pendientes de documentos de las fases 11 (pantalla del saldo y del extracto), 12 (el título «CDAT, no negociable»), 13
(el certificado de la bonificación y el extracto del contrato) y 14 (el documento de la respuesta al juzgado). **Manda el código**; estas notas reemplazan
lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. Cambio de esquema: `CaptacionesAportes` **regenerada** en los dos motores (mismos
sellos: PostgreSQL `20261019104128`, SQL Server `20261019104056`; las operaciones nuevas al final de `Up` y al principio de `Down`) con
`LND_IssuedDocumentAccounts.TermsVersionId` (FK a la versión de condiciones, con índice) y `LND_IssuedDocuments.IssuedByUserId` nulo; el SQL escrito a mano
(T044, `NOM`, `ASO`) quedó intacto —el antes y el después del `.cs` sólo difieren en líneas agregadas— y la paridad pasa. Las migraciones congeladas de la
013 y `RetiroDeCaptacionesHeredadas` no se tocaron. `EntregaVigente` sigue en K6b (E127): las clases de K8 y K9 exigen su entrega desplegada
(`Lending.Document.KindNotAvailable`, ahora con su propia `availableFrom`) y las claves `Documentos.*` nuevas se leen con sus defectos.

**Norma aplicada** (todo «pendiente de validar por la contadora»; ningún valor legal en el código):

- **Certificado de retención por conceptos distintos de los laborales.** Estatuto Tributario art. 381: año gravable y ciudad, nombre y NIT del agente
  retenedor y su dirección, nombre y NIT del retenido, monto total y concepto del pago sujeto a retención, cuantía de la retención; DUR 1625 de 2016 art.
  1.6.1.13.2.40: se expiden a más tardar el último día hábil de marzo del año siguiente (también los del GMF) —**informativo**, nada se bloquea por la
  fecha—; art. 1.6.1.12.12: las personas jurídicas pueden entregarlos impresos por computador **sin firma autógrafa**. Por eso la leyenda es el parámetro
  `Documentos.LeyendaCertificadoRetencion` con ese texto estándar.
- **Componente inflacionario** (ET arts. 38 a 41, DUR art. 1.2.1.12.6): el certificado tributario lo informa como no constitutivo de renta ni ganancia
  ocasional, con lo que cada liquidación descontó (E94).
- **CDAT**: título de legitimación, no título valor (C. de Co. art. 645), como en §18.
- Fuentes: Gerencie, «Plazos para expedir certificados de retención en la fuente» <https://www.gerencie.com/plazos-para-expedir-certificados-de-retencion-en-la-fuente.html>;
  Actualícese, «Modelo de certificado de retención en la fuente por conceptos diferentes a rentas laborales»
  <https://actualicese.com/modelo-de-certificado-de-retencion-en-la-fuente-por-conceptos-diferentes-a-rentas-laborales/> y «Plazo para expedir certificados
  de retención en 2025» <https://actualicese.com/plazo-para-expedir-certificados-de-retencion-en-2025/>; Siigo, «Certificados de retenciones»
  <https://www.siigo.com/blog/obligaciones-fiscales/certificados-retenciones/>. Las fuentes no coinciden en el decreto que modificó por última vez el art.
  1.6.1.13.2.40 (Decretos 2345 de 2019, 2487 de 2022 y 2229 de 2023): la contadora coteja el texto vigente en el compilado oficial.

- **E168 · Quién emite y los eventos de título (T194).** `IssuedLendingDocument.IssuedByUserId` pasa a **nulo** sólo para lo que emite el proceso diario
  sin persona —el título de la prórroga automática—; el evento `Issued` lo dice con `ActorKind = Process` y la lista muestra «Proceso diario de Cartera».
  `IssuedDocumentEventKind` suma `Superseded` 4 y `Settled` 5 e `IssuedDocumentStatus` los mismos valores; el estado sale de
  `EstadoDeDocumentoEmitido` con `EventosDeEstado` (anulado manda, luego reemplazado, luego liquidado, luego firmeza). Los dos eventos nuevos nombran la
  operación de cuenta en `CausingAccountOperationId` (la prórroga, la renovación o la redención). La lista filtra por los dos estados nuevos y por
  `accountPublicId`, y el DTO del documento suma `accounts[]`.
- **E169 · La emisión en la unidad de trabajo de quien la pide.** La parte transaccional de `IssueLendingDocumentCommandHandler` pasó a
  `EmisionDeDocumentosDeCartera` (sin cambio de firma del manejador): cerrojo —setup compartido, créditos, **cuentas** en orden de Id—, R = el último
  registro de lo que cubre (también `MemberAccountControls.LastRegisteredAt`), armado, código, número al final, PDF subido antes de guardar, documento,
  créditos, cuentas (con `TermsVersionId` y el título anterior), evento y correo. La usan el manejador (dentro de `TransaccionExplicita`) y los documentos
  que nacen solos dentro de la transacción de su operación.
- **E170 · Documentos de cuenta y de la persona.** El extracto y el certificado de saldo llevan `accountPublicId`: la cuenta en el alcance (fuera, el 404 de
  la cuenta), su naturaleza activada y la persona **titular o cotitular** de ella en cualquier vigencia (código nuevo `Lending.Document.NotAccountHolder`).
  El certificado tributario y el de retención exigen captaciones activadas y el alcance sobre **todas** las cuentas de la persona (falla cerrado, como
  E164). La ruta lee también `includeRestrictions` e `includePermanentSavings` (la constancia de aportes no lo recibía por HTTP).
- **E171 · Códigos.** El contrato decía `Lending.Document.YearNotClosed`: se usa el de la 013, `Lending.Document.TaxYearNotClosed` (un código por
  significado). `Lending.Document.FutureDateNotAllowed` para el certificado de saldo a una fecha futura. El título del CDAT y la respuesta al juzgado por
  `POST /documents` responden `IssuedAutomatically`. Un título reemplazado o liquidado **se descarga** (consulta) pero **no se envía** por correo:
  `Lending.Document.VoidedCannotReissue` con su propio mensaje (documentos.md §2.5).
- **E172 · La misma copia del extracto (US13-1).** Mismo extracto = misma clase, misma cuenta, misma persona, mismo período, sin anulación y sin filas del
  libro de esa cuenta registradas después del `InformationCutoff` del emitido con fecha valor hasta el fin del período. Se devuelve con
  `alreadyIssued = true`, sin consumir número, y la entrega se audita como reimpresión.
- **E173 · El título del CDAT (FR-068, R11; T195, T196).** `TitulosDeCdat` lo emite **solo** en la transacción de la operación que sella una versión: la
  constitución al fondearse (por traslado o por el recaudador), la prórroga del proceso diario (sin persona) y la renovación. Número `TIT-{consecutivo}-{versión}`:
  la primera versión toma el consecutivo de la clase y las siguientes conservan la base del título anterior. El título vigente anterior recibe
  `Superseded` y la redención o cancelación (también al vencer, por `SalidaDeCdat`) le da `Settled` al vigente. El reverso de la operación que lo selló
  (la fila de cuenta guarda su `LastOperationId` y la versión) lo anula. Sin firmante o con el almacén caído **la operación sigue** y queda
  `[Cartera.TituloDeCdatSinEmitir]`; el título pendiente se emite después con la ruta nueva `POST /api/lending/term-deposits/{id}/titles`
  (`Lending.Documents.Issue`, con clave; devuelve el vigente si ya existe). `GET /term-deposits/{id}/titles` responde la versión que titula y el estado
  calculado. Los CDAT **migrados** no reciben título al activar: se emite con esa ruta (pregunta al dueño).
- **E174 · El certificado tributario de captaciones (SC-016; T195, T196).** Motor puro `CertificadoTributarioDeCaptaciones` (`Domain/Lending/Deposits/DocumentosAlTitular.cs`):
  por cuenta en que la persona es titular o fue cotitular, abierta en el año, el saldo del titular al 31 de diciembre y el GMF del año **por su proporción**
  vigente al 31 de diciembre (o al cierre de la cuenta) —`ProporcionesDeTitularidad`: la misma regla del reparto de los acumulados, el titular absorbe el
  residuo, así la suma de los certificados de todos los titulares es exactamente lo de la cuenta—, y los rendimientos, el componente, la base y la retención
  que le tocaron en cada liquidación (`LND_WithholdingAccumulations`, ya repartidos; también la bonificación del contractual, origen `Bonus`, lo que cierra
  el pendiente del certificado de la bonificación de §19, y lo migrado del año). El GMF migrado del año sale de la fila `tax-year-to-date` de la carga de la
  cuenta (E118). Los aportes (saldo al 31 de diciembre y revalorización del año) sólo con `Documentos.CertificadoTributarioIncluyeAportes`. El certificado de
  retención agrupa por clase de captación y tarifa (`CertificadoDeRetencionDeCaptaciones`) sobre los acumulados con fecha valor en el período; la ciudad es la
  de la cooperativa —la de la consignación la informará Contabilidad—.
- **E175 · Parámetros por entidad (K8, criterio del dueño).** `Documentos.CertificadoTributarioIncluyeAportes` (`false`, P10), `Documentos.LeyendaCertificadoRetencion`
  (el texto estándar con la norma), `Documentos.TextosDeCaptaciones` (JSON `{ clase: texto }`: el texto propio de la entidad que se suma como leyenda a
  cada clase) y `Documentos.PeriodicidadDeExtractos` (`Mensual`, `Trimestral`, `Semestral` o `Anual`; informativa: rige el período del lote). Logotipo,
  imagen de firma y firmante ya eran de la entidad (013); la plantilla del correo que lleva el documento es `ENVIODOC`, editable (D35). Todas pendientes de
  validar.
- **E176 · La respuesta al juzgado como documento (T195, T196).** Contra lo que decía documentos.md §7, la respuesta **sí** es un documento emitido de la clase
  `GarnishmentResponse` 15 (data-model §1.5, research R23 «cada respuesta al juzgado se emite como documento»), prefijo `RJZ-`, a nombre de la persona
  embargada, con el texto enviado, la autoridad, el oficio, el proceso y lo retenido y consignado a la fecha. Nace en la transacción de la respuesta —la que
  registra la persona (`RegisterGarnishmentResponseCommand`, ahora en transacción) y la de «aportes inembargables» que nace con el oficio— y su Id queda en
  `LND_GarnishmentResponses.IssuedDocumentId`. No cubre cuentas: el reverso de una operación **no** la anula (es lo que se le dijo al juzgado ese día). Sin
  firmante, la respuesta se registra sin documento y el DTO trae el aviso `Lending.Garnishment.ResponseDocumentNotIssued`; el DTO suma
  `issuedDocumentPublicId` y `issuedDocumentNumber`.
- **E177 · El lote (T196, T198).** `POST /api/lending/documents/batches` (`Lending.Documents.Issue`, con clave) admite `DepositTaxCertificate` y
  `WithholdingCertificate` del año (o del período pedido) —uno por persona con productos vivos en el período— y, por el criterio del dueño,
  `AccountStatement` del último período terminado según `Documentos.PeriodicidadDeExtractos` —uno por cuenta viva, a nombre de su titular—, sólo dentro del
  alcance de quien lo corre. Cada documento sale por `IssueLendingDocumentCommand` en su propia transacción (su número, su código, su auditoría, la misma
  copia del extracto); uno que no sale queda en el detalle y el lote sigue. **Corre dentro de la petición y responde 200 con el resumen**, no 202 con una
  corrida: una corrida de la Cartera es «una corriendo por cooperativa» e impediría el ciclo diario mientras dura (pregunta al dueño para volúmenes grandes).
- **E178 · Pantalla (T199).** Componente `DocumentosDeCuenta` (sin ruta propia) en la ficha de la cuenta (`/cartera/ahorros/{id}`), en la del CDAT
  (`/cartera/cdat/{id}`, con «Emitir el título pendiente» y los títulos con versión y estado) y en la ficha de aportes (`/cartera/aportes/{persona}`, con la
  constancia y el paz y salvo): el **saldo a una fecha** y el **extracto en pantalla** (las rutas de la fase 11, lecturas auditadas, que no tenían pantalla),
  la emisión en un paso para descargar o enviar por correo y los documentos de la cuenta o de la persona con su estado, el PDF guardado y su código. La
  pantalla de documentos de la Cartera nombra las clases de captaciones y los estados nuevos. El lote no tiene pantalla todavía (se corre por API).
- **E179 · La anulación por reverso (T194, T196).** `VigenciaDeDocumentosDeCuentas` (puro) decide por clase entre los documentos que cubren alguna cuenta de la
  operación y la vieron: el extracto si la fecha valor llega hasta el **fin** del período (documentos.md §4 decía «del período»: una operación anterior
  cambia el saldo inicial que el extracto certificó); el certificado de saldo, la constancia y la liquidación del retiro con fecha valor hasta su corte; el
  certificado tributario con fecha valor hasta el 31 de diciembre de su año (cambia el saldo certificado o lo tributario); el de retención sólo por una
  operación tributaria (abono o pago de intereses, retención, GMF, bonificación) con fecha valor en el período; el título por la operación que lo selló;
  el paz y salvo de aportes si la operación pagó cuotas; la respuesta al juzgado nunca. Lo llama `ReversionDeCuentas` como antes (US3).
- **E180 · Lo que el extracto todavía no dice.** De documentos.md §2.1 faltan la exención del GMF **restante** del mes (se informa la usada), el estado de
  la factura de un cargo gravado y la tasa sellada del CDAT o del contractual (el extracto toma la de la versión de la línea; el título y el contrato traen
  la sellada). En aportes y en el ahorro permanente y contractual el extracto lista las cuotas del período con su estado (esto cierra el pendiente del
  extracto del contrato de §19).
- **E181 · Pruebas (T197, T200).** Domain: `DocumentosAlTitularTests` (proporciones, SC-016 con dos cotitulares al 60/40, retención agrupada, anulación por
  clase, estados de título). Application: `DocumentosDeCaptacionesTests` (extracto, certificado de saldo, tributario 60/40 que cuadra con la cuenta, la
  anulación por el reverso de una consignación según la clase, el título que nace al constituir con el sufijo `-1`, la versión siguiente con `-2` que
  reemplaza al primero y la redención que lo liquida, el CDAT que se constituye sin título si falta el firmante, el período del lote). La e2e
  `DocumentosDeCaptacionesTests` (US13-1 la misma copia, US13-2 la verificación vigente y anulada tras el reverso, y los rechazos) está **escrita y
  compilada; e2e en la corrida final**, que exige `EntregaVigente` ≥ K8.

### 21.1 Preguntas para el dueño

- **El lote de documentos (E177).** A: corre dentro de la petición y responde el resumen (lo construido; sirve hasta unos pocos miles de titulares); B: una
  tarea de fondo por cooperativa que no bloquea el ciclo diario y avisa al terminar; C: B, además programada cada año (certificados en marzo) y cada período
  de extractos. Recomendado: B ahora y C cuando el correo masivo esté probado.
- **El título de los CDAT migrados (E173).** A: no se emite al activar; se emite a pedido desde la ficha (lo construido); B: se emiten todos al activar la
  carga, a nombre de cada titular. Recomendado: B, para que cada ahorrador reciba su título del sistema nuevo desde el primer día.
- **El GMF en el certificado de los cotitulares (E174).** A: el GMF de la cuenta se reparte por la proporción de cada titular (lo construido); B: todo el GMF
  va al titular que lo pagó (la cuenta debitada es de él ante la DIAN). Recomendado: A si la contadora lo valida; B si la DIAN pide el GMF por quien figura
  como titular del producto.

### 21.2 Para la contadora (pendiente de validar)

- El contenido del certificado tributario de captaciones (renglones, saldos al 31 de diciembre por proporción, componente inflacionario como no gravado) y
  si los aportes van en él (P10, `Documentos.CertificadoTributarioIncluyeAportes`).
- La leyenda del certificado de retención (`Documentos.LeyendaCertificadoRetencion`), la ciudad que se imprime (la de la cooperativa mientras Contabilidad no
  informe la de la consignación) y el texto vigente del art. 1.6.1.13.2.40 del DUR.
- El reparto del GMF entre cotitulares (pregunta de §21.1).
- La periodicidad de los extractos de la cooperativa (`Documentos.PeriodicidadDeExtractos`) y sus textos propios por clase (`Documentos.TextosDeCaptaciones`).

## 22. Enmienda de la asamblea: revalorización, excedentes y amortización (2026-10-19, T201–T206, US12, K10)

Construida la historia 12. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. Cambio de
esquema: `CaptacionesAportes` **regenerada** en los dos motores (mismos sellos: PostgreSQL `20261019104128`, SQL Server `20261019104056`; las operaciones
nuevas al final de `Up` y al principio de `Down`) con `LND_AssemblyDistributions`, `LND_AssemblyDistributionLines` y
`LND_MemberAccountOperations.AssemblyDistributionId`; la migración par `CaptacionesAsamblea` de tasks.md **no** nace (E35: todo va a `CaptacionesAportes`
mientras sea regenerable). El SQL escrito a mano (T044, `NOM`, `ASO`) quedó intacto —el antes y el después del `.cs` sólo difieren en líneas agregadas— y
la paridad pasa. Las migraciones congeladas de la 013 y `RetiroDeCaptacionesHeredadas` no se tocaron. `EntregaVigente` sigue en K6b (E127): registrar exige la
entrega K10 desplegada (`Lending.Savings.DeliveryNotAvailable`, `availableFrom = K10`) y las claves `Asamblea.*` se leen con sus defectos.

**Norma aplicada** (todo «pendiente de validar por la contadora»; ningún valor legal en el código):

- **Aplicación del excedente.** Ley 79 de 1988, art. 54: del excedente, 20 % como mínimo a la reserva de protección de aportes, 20 % al fondo de educación y
  10 % al de solidaridad; el remanente, según los estatutos o la asamblea, a (1) la **revalorización de aportes** «teniendo en cuenta las alteraciones en su
  valor real», (2) servicios comunes y seguridad social, (3) **retorno** a los asociados en relación con el uso de los servicios o la participación en el
  trabajo y (4) un **fondo para amortización de aportes**. Art. 55: el excedente se aplica primero a compensar pérdidas de ejercicios anteriores y a
  restablecer la reserva. Art. 53: el ejercicio cierra el 31 de diciembre; art. 28: la asamblea ordinaria se reúne en los tres primeros meses del año.
  Fuente: <https://www.alcaldiabogota.gov.co/sisjur/normas/Norma1.jsp?i=9211>.
- **Tope de la revalorización.** Ley 79 art. 19 parágrafo (mantener el poder adquisitivo de los aportes «dentro de los límites que fije el reglamento») y el
  **Decreto 3081 de 1990, art. 1**: hasta el **IPC certificado por el DANE para el año inmediatamente anterior**. La Circular Básica Contable y Financiera de
  la Supersolidaria, **Título I, Capítulo V, numerales 5 y 5.1**, fija el procedimiento: la asamblea constituye el fondo con el remanente, aprueba revalorizar
  hasta ese IPC con cargo al fondo (lo que sobra queda para revalorizaciones futuras), sin retroactividad, **sobre el monto promedio día/año** de los aportes
  de cada asociado. Fuentes: <https://www.supersolidaria.gov.co/sites/default/files/circular_basica_contable_financiera/20250624_circ_contable_finan_completa.pdf>
  y el concepto unificado de devolución de aportes
  <https://www.supersolidaria.gov.co/sites/default/files/conceptos_juridicos_y_contables/concepto_unificado_devolucion_o_compensacion_de_aportes_sociales.pdf>.
- **Fondos de empleados.** Decreto Ley 1481 de 1989, art. 19, en la redacción de la Ley 2496 de 2025 (art. 8): 20 % a la reserva de protección de aportes,
  10 % al fondo de desarrollo empresarial solidario; el fondo para mantener el poder adquisitivo de los aportes no supera el **50 % del excedente**; la
  amortización parcial en igualdad de condiciones llega hasta el **49 % de los aportes**; en los fondos de empleados **no hay retornos**. Fuente:
  <https://normativa.colpensiones.gov.co/colpens/compilacion/docs/ley_2496_2025.htm>.
- **Amortización.** Ley 79, art. 52: con un fondo especial alimentado del remanente, en **igualdad de condiciones** y sólo cuando la asamblea juzgue que la
  situación económica lo permite. CBCF Título I Cap. V num. 2.3 y 4.3: la igualdad es la misma proporción para todos; si los aportes amortizados llegan al
  50 % del capital social, el proyecto requiere autorización previa de la Supersolidaria; los aportes amortizados forman parte del mínimo no reducible (CUIF
  311010).
- **Proyecto de distribución.** CBCF Título II Cap. I num. 5.1.2 lit. f: el consejo aprueba el proyecto de distribución de excedentes que la asamblea decide;
  después se reporta en el formato 162 (anual).
- **Tributario.** La cooperativa del régimen tributario especial tributa al 20 % sobre el beneficio neto o excedente, tomado del fondo de educación y
  solidaridad (ET art. 19-4; DUR 1625 de 2016 arts. 1.2.1.5.2.7 y 1.2.1.5.2.8; Concepto DIAN 387 de 2025
  <https://normograma.dian.gov.co/dian/compilacion/docs/oficio_dian_0387_2025.htm>): eso es de Contabilidad, no de la Cartera. Para el **asociado**, la DIAN
  ha tratado la revalorización como ingreso **gravado** sujeto a retención (Conceptos 029502 de 2002 y 068192 de 2004, fuente secundaria
  <https://cijuf.org.co/codian04/octubre/c68192.htm>) y los pagos con cargo al remanente como ingreso gravado con retención por «otros ingresos» (Oficio
  24051 de 2007 <https://normograma.dian.gov.co/dian/compilacion/docs/oficio_dian_24051_2007.htm>). No se encontró un concepto posterior a 2016 sobre retornos
  ni revalorización: **no se construyó retención** sobre lo que abona la asamblea (pregunta al dueño y a la contadora, §22.1).

- **E182 · El motor (T201, `RevalorizacionYExcedentes`).** Puro, sin reloj ni valores legales: la base de cada asociado es el **saldo promedio diario** del
  ejercicio (Σ del saldo de cada día del 1 de enero al 31 de diciembre por fecha valor ÷ 365 o 366, a dos decimales al par; CBCF «promedio día/año», D13) o el
  saldo al 31 de diciembre; la tasa es el porcentaje decidido o el valor sobre la base total; en la revalorización nunca supera el límite legal; lo calculado
  es base × tasa a la unidad de la entidad (mitad hacia arriba) o, con un valor sin límite, el valor repartido por la base con el **mayor residuo**; en la
  amortización nunca más que lo amortizable (saldo de hoy menos el mínimo no reducible, FR-042); si lo calculado supera el fondo, el fondo (hacia abajo a la
  unidad) se reparte por lo calculado con el mayor residuo y la suma es **exactamente** el fondo (US12-1). Cinco casos dorados `asamblea-01` a `-05` por
  `calcular.py --verificar` (60 casos en total): la prueba independiente (5 % con IPC 4 % y fondo de 100.000: 142.657 calculados, factor 0,700982), saldo al
  cierre sin límite, excedentes por valor con el residuo, amortización hasta el mínimo y 2028 bisiesto al centavo.
- **E183 · Entidades (T202).** `AssemblyDistribution` (documento con estado) y `AssemblyDistributionLine` (hecho de sólo inserción, escrito una vez al
  aplicar con todo: no lleva columnas de escritura única). Cambios contra data-model §6.3/§6.4: `DecisionSessionItemId` es **nulo** hasta aplicar (el punto del
  acta nombra a la decisión, así que la decisión nace antes); `CorrectsDistributionId` (la reversada que corrige, de la que hereda el acta); `DecidedValue`
  junto a `DecidedFraction` (uno de los dos, `CK_LND_AssemblyDistributions_Decision`); `RoundingUnit`, `Reason`, `RegisteredAt`, `AppliedOn`,
  `SupportAttachmentPublicIdsJson`, `AppliedMessagePublicId`, `ReversedAt/ByUserId/Reason` y `VoidedAt/VoidReason`; **no** nace `ApplyingProcessRunId` (se aplica
  dentro de la petición, en una transacción). Estados: `Simulated` 1 (el borrador), `Applied` 3, `Voided` 4 (descartado) y el nuevo **`Reversed` 5**; `Approved`
  2 no se usa (aprobar y aplicar son un solo acto de otra persona). Único `UK_LND_AssemblyDistributions_Session (DecisionSessionItemId, Kind)` filtrado a
  `Applied`: la reversada no estorba a su corrección. En la línea: `LimitedByMinimum`, `PayoutOrderId` y la operación única. `SimulationVersion` queda en 1: la
  simulación no se guarda (es una consulta sin clave) y la huella la ata a lo que se aplica.
- **E184 · El acta y el flujo (T203).** Registrar (borrador) → simular → llevar al orden del día de la asamblea con el **asunto de acta** nuevo
  `AssemblyDistribution` (`AsuntoDeDecisionDeAsamblea`: quien registró no vota; cerrar el acta no aplica nada) → **otra persona** aplica con el punto cerrado
  que aprueba esta decisión (`PuntoDeActaCerrado`), el **soporte** (el acta firmada, adjunto nuevo `LendingAssemblySupport` con dueño la decisión: se lee con
  `Register` o `Approve`, se sube mientras sea borrador y no se borra) y la huella de la simulación que vio (`Lending.Savings.PreviewChanged`). La asamblea
  general se registra como un órgano de la cooperativa (clase `Other`) cuyos miembros en el sistema son sus dignatarios. US12-2: aplicar dos veces responde
  `Lending.AssemblyDistribution.AlreadyApplied`; una corrección (`correctsPublicId`, misma clase y ejercicio que una **reversada**) hereda el acta y no pide otro
  punto; dos aplicadas con la misma acta y clase responden `DuplicateSession`. Segregación del catálogo (`SegregacionDeCaptaciones.DecisionDeAsamblea`, ya
  declarada en US16): registrar es `Radicar`; aplicar y reversar, `Decidir`. **Alcance total**: la decisión es de toda la cooperativa —abona en todas las
  oficinas y líneas—, así que sólo la ve y la opera quien tiene alcance total (fuera, el 404 de la decisión con `Lending.Access.OutOfScope`; la lista vacía).
- **E185 · Quiénes entran.** Los titulares de una cuenta de aportes activa o inactiva con base positiva. Fuera, con aviso en la simulación: quien tiene el
  retiro aprobado, liquidado o por rehacer (su liquidación ya cuenta sus aportes) y, en la amortización con `Asamblea.AmortizacionExcluyeDeudores` (defecto
  `true`, FR-044), quien tiene créditos vivos. La amortización no deja el capital social bajo `Aportes.MinimoIrreducibleEntidad` (`EntityMinimumBreached`).
- **E186 · Lo que deja en cada cuenta.** Una operación por asociado con `AssemblyDistributionId`: **revalorización** `AssemblyCredit` (prefijo `RVA-`, fila
  `AssemblyCredit` en aportes revalorizados); **excedentes a aportes** `AssemblyCredit` (`EXD-`) en la clase de `Asamblea.ClaseDeAporteDeExcedentes` (defecto
  ordinarios); **a ahorro**, en la cuenta viva del titular en la línea de destino (a la vista o permanente; sin ella, `DestinationAccountMissing` con la lista,
  y la simulación lo avisa); **pagados**, una operación `AssemblyCredit` **sin filas** en la cuenta de aportes con su orden de giro (`PayoutPurpose.SurplusPayment`
  8, nuevo); **amortización** `ContributionRefund` (`AMZ-`) con filas `Refund` en el orden del débito de aportes y su orden (`ContributionRefund`, plazo de la
  versión). La fila `AssemblyCredit` **no** es movimiento del titular (no reactiva una cuenta inactiva). Lo de la asamblea no se reversa desde la pantalla de la
  cuenta (`ReversalOwnedByProcess`).
- **E187 · Los hechos (T203; constructor de la fase 9).** **Un** hecho masivo por decisión —`RevalorizacionDeAportesAplicada`, `ExcedentesDistribuidos` o
  `AportesAmortizados`— con origen la decisión (`AssemblyDistribution`), una línea por cuenta con sus dimensiones y totales por combinación; ningún hecho
  lleva una cuenta contable. Las órdenes salen con `OrdenDeGiroEmitida` v2. `ExcedentesDistribuidosV1` suma, opcional al final, `Payments` (`SurplusPaymentV1`:
  cuenta de aportes, titular, dimensiones, valor y orden): el pago en dinero no mueve cuentas del asociado y el constructor lo asienta del **fondo de
  excedentes a los retornos por pagar** (`DevolucionPorPagar`, motivo `SurplusPayment`, tercero el asociado). La validación previa de pantalla (D23) pregunta
  una vez por combinación de dimensiones (`HechosRepresentativosDeCaptaciones.Asamblea`).
- **E188 · El reverso entero.** Una operación `Reversal` por cuenta (espejo de sus filas, fecha valor de la aplicación) y **un** `OperacionDeCuentaReversada`
  con un espejo por cuenta (`Mirrors` con cuenta, titular y dimensiones vigentes), heredando el modo de la decisión; el constructor devuelve cada espejo a su
  fondo —o a la devolución por pagar de la amortización— con la sucursal de su cuenta. No se reversa: con los excedentes pagados en dinero
  (`PaymentNotReversible`), mientras una orden de la amortización siga viva o girada (`PayoutsInProgress`; una amortización rechazada por Tesorería ya volvió a
  los aportes por el reintegro de siempre) ni si una cuenta quedaría en negativo (`ReversalWouldOverdraw`: un retiro posterior ya usó lo abonado). El reverso
  anula los documentos que vieron esas operaciones.
- **E189 · El giro rechazado del pago de excedentes.** Como no salió de ninguna cuenta, no hay reintegro: la orden queda con su resultado y lo decretado sigue
  por pagar en Contabilidad (retornos por pagar). **Volver a girarla no tiene ruta todavía** (pendiente, §22.1).
- **E190 · Parámetros por entidad (K10, criterio del dueño; todos pendientes de validar).** `Asamblea.BaseDeRevalorizacion` (`AverageBalance`, D13),
  `Asamblea.Redondeo` (`Peso` o `Centavo`), `Asamblea.SerieDelIpc` (`ANUAL`: el valor nacional `IPC` de esa serie que rige el 31 de enero siguiente al
  ejercicio, la variación de diciembre ya publicada; la simulación muestra su fecha de referencia y su fuente), `Asamblea.DestinoDeExcedentes`
  (`Contributions`), `Asamblea.ClaseDeAporteDeExcedentes` (`Ordinary`) y `Asamblea.AmortizacionExcluyeDeudores` (`true`). El porcentaje o el valor, el fondo y el
  destino de cada decisión son del acta. El IPC es un valor nacional con vigencia (sin semilla: lo registra el maestro con su fuente); sin él la revalorización
  responde `NationalValues.Missing`. El ejercicio tiene que estar cerrado (`year < hoy`).
- **E191 · Rutas y pantalla (T205).** Siete rutas en `AssemblyDistributionsEndpoints.cs` (lista, detalle, registrar, simular sin clave, descartar —nueva:
  `POST /{id}/void`—, aplicar, reversar); `apply` recibe `sessionItemPublicId` (nulo en una corrección), `expectedPreviewSha256`, `supportAttachmentPublicIds` y
  `reason` (api.md decía `{ expectedPreviewSha256, reason }`); `POST /` recibe además `correctsPublicId`. Pantalla `/cartera/asamblea` (menú «Captaciones y
  aportes» → «Asamblea»): lista, registro, simulación con el límite del IPC, el prorrateo, los avisos y las líneas, subida del acta, aplicación con el punto
  del acta (digitado por su identificador, como en el retiro) y reverso.
- **E192 · Pruebas (T204, T206).** Domain: `RevalorizacionYExcedentesTests` (los cinco dorados y las reglas: suma exacta al fondo, sin límite en excedentes,
  sin base, pedido mal formado, mayor residuo). Application: `AsambleaTests` (10: la prueba independiente con la segregación y el acta; aplicar dos veces,
  reversar entera y corregir con la misma acta; la simulación que cambió; sin soporte o sin IPC; la entrega K10 y el ejercicio cerrado; excedentes por valor a
  aportes; a ahorro sin y con cuenta de destino; pagados por orden sin tocar cuentas y no reversables; amortización hasta el mínimo sin deudores y con su
  orden; sin alcance total) y `HechosDeAsambleaTests` (3: el constructor con la revalorización, el pago de excedentes y el reverso entero). La e2e
  `AsambleaTests` (US12-1 y US12-2 por HTTP con el reloj de la suite en marzo del año siguiente) está **escrita y compilada; e2e en la corrida final**, con
  `EntregaVigente` ≥ K10.

### 22.1 Preguntas para el dueño

- **Retención en la fuente sobre lo que abona la asamblea.** La DIAN trató la revalorización como ingreso gravado sujeto a retención (2002, 2004) y los
  pagos con cargo al remanente con retención por «otros ingresos» (2007). A: no se retiene (lo construido; el certificado tributario informa la
  revalorización si la entidad lo pide); B: retener sobre la revalorización y los retornos con una tarifa por parámetro de la entidad, como los rendimientos;
  C: B sólo sobre los retornos pagados en dinero. Recomendado: B, si la contadora y el tributarista lo confirman.
- **La base de los retornos (excedentes).** La ley reparte el retorno «en relación con el uso de los servicios»; lo construido lo reparte por los aportes
  (saldo promedio o al cierre). A: sobre los aportes (lo construido); B: sobre los intereses pagados por cada asociado en el año a sus créditos; C: una
  plantilla con la base de cada asociado que la cooperativa calcula. Recomendado: C (cada cooperativa mide el uso de servicios a su manera).
- **Asociados retirados durante el ejercicio.** La CBCF dice que a quien se retira antes de diciembre se le aplica lo que apruebe la asamblea, prorrateado
  hasta su retiro. A: no reciben (lo construido: sólo cuentas activas o inactivas); B: se calcula su línea y se le paga por orden de giro como exasociado.
  Recomendado: B.
- **Volver a girar un pago de excedentes rechazado (E189).** A: queda por pagar en Contabilidad y Tesorería lo paga por fuera (lo construido); B: una ruta para
  volver a emitir la orden; C: pasado un plazo, a no reclamados (US14). Recomendado: B y C.
- **Excedentes a ahorro sin cuenta de destino.** A: no se aplica hasta que todos tengan cuenta (lo construido); B: a quien no tiene cuenta se le capitaliza en
  aportes; C: se le paga por orden de giro. Recomendado: B.
- **La asamblea como órgano.** A: se registra como un órgano (clase «otro») con sus dignatarios y el punto del acta se vota en el sistema (lo construido); B:
  sólo número, fecha del acta y el soporte, sin sesión. Recomendado: A si la cooperativa ya usa las actas de la Cartera; si no, B.
- **Aplicación legal del excedente.** La Cartera no verifica los porcentajes mínimos de reservas y fondos (art. 54) ni el 50 % de los fondos de empleados: es
  la contabilización del acta. A: dejarlo en Contabilidad (lo construido); B: un aviso en la simulación si el fondo supera el remanente que la entidad informe.
  Recomendado: A.

### 22.2 Para la contadora (pendiente de validar)

- Las cuentas sugeridas: fondo para revalorización 3310, fondo de excedentes 3505 o la reserva del acta, aportes revalorizados (310505 o una auxiliar),
  retornos decretados por pagar 246515, amortización contra 246505; y el traslado del fondo para amortización (3305) a aportes amortizados (311010), que no
  emite la Cartera.
- Los defectos `Asamblea.*` (saldo promedio, al peso, serie `ANUAL` del IPC, excedentes a aportes ordinarios, amortización sin deudores).
- La regla del IPC: el valor de la serie que rige el 31 de enero siguiente al ejercicio (la variación de diciembre).
- El tratamiento tributario de la revalorización y los retornos para el asociado (§22.1) y si van al certificado tributario
  (`Documentos.CertificadoTributarioIncluyeAportes`).

## 23. Enmienda de la inactividad, los no reclamados y el fallecimiento del titular (2026-10-19, T207–T213, US14, K10)

Construida la historia 14 y los pendientes de las fases 10 (la inactividad de una cuenta migrada cuenta desde el corte), 14 (la reclasificación por la
inactividad y la de la exención del GMF que rige desde una fecha futura) y 16 (el punto de enganche de los excedentes rechazados, anotado sin decidir).
**Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. Cambio de esquema: `CaptacionesAportes`
**regenerada** en los dos motores (mismos sellos: PostgreSQL `20261019104128`, SQL Server `20261019104056`; las operaciones nuevas al final de `Up` y al
principio de `Down`, por una migración temporal fusionada) con `LND_DeceasedHolderCases`, `LND_UnclaimedBalances` y `DeceasedHolderCaseId` en
`LND_AccountHolds`, `LND_MemberAccountOperations` y `LND_AccountPayoutOrders`; el SQL escrito a mano (T044, `NOM`, `ASO`) quedó intacto —el antes y el
después del `.cs` sólo difieren en líneas agregadas— y la paridad pasa. Las migraciones congeladas de la 013 y `RetiroDeCaptacionesHeredadas` no se
tocaron. `EntregaVigente` sigue en K6b (E127): el registro del fallecimiento, el traslado a no reclamados y los subpasos de inactividad y no reclamados
exigen la entrega K10 desplegada y las claves K10 se leen con sus defectos.

**Norma aplicada** (todo «pendiente de validar por la contadora y el abogado»; ningún valor legal en el código):

- **Cuentas abandonadas — Ley 1777 de 2016.** Art. 2: es abandonada la cuenta corriente o de ahorros sin depósito, retiro, transferencia ni débito o crédito
  durante **tres años ininterrumpidos**; los abonos de intereses y los cargos de comisiones de la entidad **no** impiden considerarla abandonada (lo mismo que
  FR-058 dice del movimiento del titular). Art. 3: los saldos que superen **322 UVR** se trasladan **a título de mutuo** a un fondo especial del ICETEX
  (créditos educativos). La Corte Constitucional declaró exequible el art. 1 (Sentencia C-347 de 2017). Fuentes:
  <https://www.dmsjuridica.com/buscador_20179478954/legislacion/leyes/?p=1834> y
  <https://www.ambitojuridico.com/sites/default/files/BancoMedios/Archivos/comunicado-sentencia-c-347-17.pdf>.
- **Decreto 953 de 2016** (adiciona el Título 17 del Libro 1 de la Parte 2 del Decreto 2555 de 2010): el traslado lo hacen «las entidades financieras que
  están autorizadas para ofrecer cuentas de ahorro o cuentas corrientes» con listados trimestrales al ICETEX (art. 2.1.17.1.3); el **parágrafo** del mismo
  artículo dice que la transferencia **no altera los saldos** de los balances y extractos de las cuentas ni restringe que el titular disponga de su dinero, y
  el art. 2.1.17.1.4 fija **un día** como plazo máximo para entregar el saldo pedido por el titular, que reclama siempre ante su entidad, nunca ante el
  ICETEX. Fuente: <https://www.cijuf.org.co/sites/cijuf.org.co/files/normatividad/2016/DeMINHACIENDA953_16.pdf>.
- **Aplicabilidad a las cooperativas.** La Ley define la cuenta abandonada por referencia a las entidades vigiladas por la **Superintendencia Financiera**; el
  ICETEX ha concluido que sólo los establecimientos de crédito bajo esa vigilancia trasladan saldos, y la Superintendencia de la Economía Solidaria emitió un
  concepto unificado («Cuentas abandonadas», radicado 20161100263381 de 2016) sobre si las cooperativas con actividad financiera pueden aplicarla —su texto
  completo no se pudo leer (fuente bloqueada), la conclusión queda **a validar por el abogado**:
  <https://ambitojuridico.com/sites/default/files/BancoMedios/Archivos/cpto-20161100263381-16.pdf>—. El art. 36 de la Ley 454 de 1998 (funciones de la
  Supersolidaria) no trae una regla propia de cuentas inactivas: la inactividad, su efecto y el traslado a no reclamados son **del reglamento de ahorro** de
  cada cooperativa. Por eso: (a) la inactividad la fija cada línea (`InactivityDays`, D21: 360 por defecto); (b) el traslado de las inactivas a no reclamados
  es una regla **interna** de la cooperativa, apagada por defecto (`CuentasAbandonadas.TrasladoHabilitado = false`, P7) y, si se enciende, con su propio plazo
  (`CuentasAbandonadas.DiasSinMovimiento`, defecto 1.095 días = los tres años de la Ley 1777 como referencia); (c) **no se construyó el traslado al ICETEX**:
  según el Decreto 953 sería un mutuo de la entidad que no toca el saldo del titular (un asunto de Tesorería, no un movimiento del libro de cuentas), y no
  aplica a las cooperativas mientras el abogado no diga lo contrario (pregunta §23.1).
- **Entrega de depósitos sin juicio de sucesión.** El Estatuto Orgánico del Sistema Financiero (Decreto 663 de 1993), art. 127, permite a los
  establecimientos entregar los depósitos del fallecido al cónyuge, compañero permanente y herederos sin juicio de sucesión hasta un tope que se actualiza;
  la cooperativa lo aplica por su reglamento (P6). El tope es el valor nacional `TOPE_ENTREGA_SIN_SUCESION` (pesos, sin semilla: lo registra el maestro
  con su fuente) y los soportes, el parámetro `Fallecimiento.SoportesDeEntregaSinSucesion` (JSON, sin valor: lo fija el reglamento).
- **Beneficiarios.** Cuenta el conjunto vigente en la **fecha de la defunción** (FR-034).
- **Plazos informativos.** Ningún plazo de esta historia bloquea por fecha: los días de inactividad, el aviso previo y los plazos de no reclamados sólo
  disparan el proceso o el aviso.

- **E193 · Entidades (T207).** `DeceasedHolderCase` y `UnclaimedBalance` en `Domain/Entities/Lending/Deposits/Processes/DeceasedHolderCase.cs`. Cambios contra
  data-model §6.5/§6.6: `Route` es **nula** hasta registrar la entrega (al registrar el fallecimiento no se sabe por dónde se entrega); estado nuevo
  **`DeliveryPending` 5** (la entrega registrada que espera la aprobación; el contrato lo llamaba así) —`Approved` 2 es «aprobada, giros en curso» y
  `Delivered` 3 «todas las cuentas canceladas»—; se agregan `PreviewSha256`, `Reason`, `RegisteredAt`, `DeliveryRegisteredByUserId/At` y
  `CancelledAt/ByUserId/CancelReason`; **no** nace `DecisionId` (la aprobación es la segregación del catálogo, no una decisión de atribución). En
  `UnclaimedBalance` se agregan `Reason`, `TransferredByUserId` (nulo = el proceso) y `ClaimOperationId`. Únicos: `UK_LND_DeceasedHolderCases_Person`
  filtrado `[Status] <> 4` (un caso no anulado por titular) y `UK_LND_UnclaimedBalances_TransferOperation`. Enums al final: `MemberAccountConcept.Unclaimed`
  **11**, `MemberAccountOperationKind.UnclaimedPayment` **27**, `PayoutPurpose.UnclaimedPayment` **9**, `DeceasedCaseStatus.DeliveryPending` **5**.
- **E194 · Dónde vive lo no reclamado.** El dinero trasladado **sale** del capital (o de los aportes) por la fila `ToUnclaimed` y **entra** al concepto nuevo
  `Unclaimed` del libro de la misma cuenta, que queda cancelada: no es saldo del titular (`SaldoDelTitular` no lo suma, no causa ni se retira) y su rol
  contable es `NoReclamadosPorPagar` (`RolDelConceptoDeCuenta`). Así el hecho `SaldoTrasladadoANoReclamados` cuadra solo, el pago es una fila más
  (`Refund` o `TransferOut` sobre `Unclaimed`) y el reverso de un giro rechazado lo devuelve a «no reclamados» con la maquinaria de siempre. La conciliación
  no lo concilia todavía (la plantilla lo sugiere pero no lo exige, E111).
- **E195 · Inactividad (T208).** Subpaso `Inactividad` después de las cuotas y antes de los cargos periódicos (el cargo `Inactivity` se causa sobre la cuenta ya
  inactiva). Mide desde `LastHolderActivityDate` (sin él, la activación o la apertura); en una cuenta **migrada**, nunca antes del corte (la víspera de la
  activación; E119 A). La cuenta pasa a `Inactive` desde el día en que el proceso lo detecta (como la suspensión, E101) y reclasifica sus saldos
  (`CaptacionReclasificada` causa `Inactivity`, el capital al rol `CapitalDeCaptacionInactiva`); el CDAT no se mide (no tiene movimientos del titular).
  Avisos: `PREINACTIV` `Captaciones.DiasDeAvisoPrevioDeInactividad` días antes (defecto 30, cero sin aviso) e `INACTIVA` al inactivar; plantillas estándar
  editables por la entidad (D35), una vez por plazo (la clave del mensaje). Corre desde K10.
- **E196 · Reactivación (T208).** `ReactivacionDeCuenta` reactiva **y reclasifica** (causa `Reactivation`) en la consignación, el retiro, el traslado (las
  dos puntas), la entrega del contractual vencido, el pago de un no reclamado por traslado y la entrega por fallecimiento a una cuenta: **después** de guardar
  las filas de la operación, porque el hecho de la operación se contabilizó con la cuenta todavía inactiva; así lo que se mueve es todo el saldo de ese
  momento. Antes la consignación reactivaba antes de emitir su hecho y el capital de la cuenta quedaba partido entre los dos roles.
- **E197 · La exención del GMF que rige desde una fecha futura (pendiente de la fase 14).** Subpaso `MarcasQueRigen` (antes de la inactividad): el día `d` en
  que una marca empieza (`ValidFrom = d`) o deja de regir (`ValidTo = d − 1`) reclasifica de la marca de antes a la de ahora, **sólo lo contabilizado antes
  de `d`** (lo de ese día ya llevó la marca nueva); no se repite si el comando ya reclasificó ese día. `ReclasificarAsync` suma `exentaAntes` y `saldos`.
- **E198 · No reclamados (T209).** Fuentes: **lo devuelto y no cobrado** —la cuenta cuya última orden de giro de devolución de aportes o del retiro, de
  cancelación o de salida del CDAT volvió (rechazada, anulada o cheque no cobrado), sin otra orden viva ni movimiento del titular después— y **el saldo de una
  cuenta inactiva** sólo con `CuentasAbandonadas.TrasladoHabilitado`. El ciclo traslada solo cuando se cumplen `Captaciones.DiasParaNoReclamados` (vacío = no
  traslada solo, como antes) o `CuentasAbandonadas.DiasSinMovimiento`; a mano (`POST /unclaimed/transfer`) el plazo rige si la cooperativa lo fijó; es todo o
  nada (`NotEligible` con la lista) y `RuleNotEnabled` si pide una inactiva sin la regla. No pasan: cuentas con bloqueos o embargos vigentes, de un titular
  con caso de fallecimiento vivo, con cargos por cobrar o sin saldo. Antes se liquidan los intereses a la fecha. La cuenta se cancela y se avisa
  (`NORECLAMA`). Un retiro del asociado cuya devolución pasó entera a no reclamados queda `Paid` y sus cuentas canceladas. Origen del hecho: el nombre de
  `UnclaimedSource` (`UncashedRefund`, `InactiveBalance`, `UncashedPayout`; el contrato decía `Refund|InactiveAccount|Delivery`).
- **E199 · El pago de un no reclamado (T209).** Operación `UnclaimedPayment` (prefijo `PNR-`) **sobre la cuenta de origen aunque esté cancelada** (data-model
  §6.6): por orden de giro (`PayoutPurpose.UnclaimedPayment`, `DevolucionOrdenada` y `OrdenDeGiroEmitida` v2 con `OrderKind` `UnclaimedPayment`) a la cuenta
  bancaria pedida o a la de depósito registrada, o por traslado a una cuenta de ahorro a la vista o permanente viva **del titular** (`TrasladoEntreCuentasAplicado`).
  El giro devuelto por Tesorería reversa la operación (vuelve a `Unclaimed`) y el saldo queda `Held` otra vez. El GMF del pago no se calcula (pregunta §23.1).
  En el cuerpo de `POST /{id}/pay`, `payee` es `{ bankPublicId, accountType, accountNumber }` (el beneficiario es siempre el titular).
- **E200 · Fallecimiento (T210).** Registrar (`Lending.DeceasedCases.Register`) exige el registro civil de defunción subido como soporte `LendingDeceasedSupport`
  (dueño la persona fallecida; se lee y sube con los dos permisos de fallecimiento; no se borra), la fecha no futura y el alcance sobre **todas** las cuentas de
  la persona (falla cerrado, como los embargos); bloquea **todas** sus cuentas vivas —captación y aportes— con `HoldReason.Deceased` total y el caso, y anula
  los retiros en espera de aprobación. La Cartera **no escribe** el maestro de personas: `TitularesFallecidos` toma por fallecido al titular que el maestro
  marca **o** que tiene un caso vivo, y con eso se suspende la causación en las líneas que lo dicen (FR-065) y el CDAT queda a disposición al vencer (FR-072);
  el detalle avisa si el maestro todavía no lo marca. Un registro errado se anula (`POST /{id}/void`, `Approve`) antes de aprobar la entrega y levanta los
  bloqueos.
- **E201 · La entrega (T210).** Se entregan las cuentas de **captación** vivas con saldo (a la vista, permanente, contractual y el CDAT vencido); los aportes
  van por el retiro del asociado con causa fallecimiento (aviso en la vista previa); el CDAT sin vencer, la cuenta embargada, con un giro en curso o con cargos
  por cobrar quedan fuera con su nota. Rutas: `Beneficiaries` (cada cuenta a su conjunto vigente en la defunción; sin beneficiarios, `NoBeneficiaries`),
  `WithoutProbate` (personas y porcentajes del soporte, iguales en todas las cuentas; el total no supera el tope, `CapExceeded`; tantos soportes como pida el
  parámetro, al menos uno) y `Probate` (al menos el soporte de la sucesión). La vista previa (sin clave) devuelve el reparto y la huella; registrar la entrega la
  compara (`Lending.Savings.PreviewChanged`) y la deja `DeliveryPending`; aprobar (`Approve`, **otra persona**: `Lending.Segregation.Conflict`) liquida los
  intereses a la fecha y entrega **el saldo del día** por los mismos porcentajes, al centavo por el **mayor residuo** (la suma es exactamente el saldo), una
  operación `DeceasedDelivery` (prefijo `FAL-`) por destinatario y cuenta, tomando del capital, luego del interés por pagar y luego de la bonificación. Por
  giro (`PayoutPurpose.DeceasedDelivery`, la orden con el caso) la cuenta queda `Closing` y el caso `Approved`; cuando Tesorería informa girada la última orden
  de la cuenta, la cuenta se cancela y suelta su bloqueo, y con todas canceladas el caso queda `Delivered`. Por traslado a una cuenta del destinatario la
  cuenta se cancela en el acto. Un giro rechazado reintegra el saldo a la cuenta, que sigue en cierre (pregunta §23.1).
- **E202 · Contabilidad.** No hay hechos nuevos: la entrega es `DevolucionOrdenada` (motivo `DeceasedDelivery`, ya en los motivos de `DevolucionPorPagar`) o
  `TrasladoEntreCuentasAplicado`; el traslado a no reclamados, `SaldoTrasladadoANoReclamados` (MA); su pago, `DevolucionOrdenada` (motivo `UnclaimedPayment`)
  desde `NoReclamadosPorPagar`. Los reversos los arman los mapeos nuevos de `ReversionDeCuentas` (`ToUnclaimed`, `UnclaimedPayment`, `DeceasedDelivery`) y
  sus clases de origen (`UnclaimedTransfer`, `UnclaimedPayment`, `DeceasedDelivery`). La validación previa de pantalla (D23) **no** se pregunta todavía en la
  aprobación de la entrega ni en el traslado y el pago de no reclamados (pendiente técnico: los hechos representativos de esas operaciones).
- **E203 · Rutas, vista y pantallas (T212).** `/api/lending/deceased-cases`: lista, detalle, registrar, vista previa (sin clave), registrar la entrega, aprobar y
  **anular** (nueva, no estaba en api.md §13); `/api/lending/unclaimed`: lista (`Accounts.Read`), traslado y pago (`Unclaimed.Manage`); la vista `unclaimed`
  (K10, `asOf`, con datos del titular y alcance por la oficina y la línea de la cuenta de origen). Once rutas nuevas. Lecturas sensibles auditadas con
  `Lending.Savings.BalanceViewed` (la lista de no reclamados, la lista y el detalle de los casos). Pantallas `/cartera/fallecimientos` (registro con el
  registro civil, cuentas, beneficiarios a la fecha de la defunción, vista previa y registro de la entrega con sus destinatarios y soportes, aprobación,
  anulación y entregas con sus giros) y `/cartera/no-reclamados` (lista por persona y estado, traslado a mano por identificadores y pago por giro o traslado),
  con su enlace en el menú «Captaciones y aportes». La entrega desde la pantalla va siempre por orden de giro a la cuenta de depósito registrada de cada
  destinatario (el traslado a una cuenta se pide por la API).
- **E204 · Pruebas (T211, T213).** Application: `CicloDeVidaDeCuentasTests` (11: inactiva a los días de la línea con su reclasificación y sin repetir, lo que
  no es movimiento del titular, la consignación que reactiva y reclasifica de vuelta, la migrada desde el corte, la exención a futuro que reclasifica el día que
  rige y una sola vez, sin la regla no se traslada, la inactiva que pasa a no reclamados a los días sin movimiento con su pago por giro y el giro devuelto que la
  retiene otra vez, US14-2 con dos órdenes de 500.000 y la cuenta cancelada al girarse ambas con la segregación, el registro una sola vez y sin beneficiarios, el
  tope sin sucesión, el reparto al centavo). La e2e `CicloDeVidaDeCuentasTests` (US14-1 con el ciclo diario y la consignación; US14-2 con la entrega aprobada
  por otra persona y las dos órdenes) está **escrita y compilada; e2e en la corrida final**, con `EntregaVigente` ≥ K10. No hay en las e2e un doble que informe
  el resultado del giro de una orden de cuentas: lo que sigue al giro lo cubren las pruebas de aplicación.
- **E205 · Lo que queda.** La migración de un bloqueo `Deceased` (`holds-and-garnishments`, E166) sigue «llega con K10» sin construir: los fallecimientos
  migrados se registran con la ruta después de activar. El **punto de enganche** de los excedentes pagados en dinero cuyo giro rechaza Tesorería (fase 16,
  E189) quedó **anotado, sin decidir**: hoy no salen de ninguna cuenta y no entran a no reclamados; si el dueño elige C de §22.1, la entrada natural es una
  fuente nueva de `UnclaimedSource` con el saldo en los retornos por pagar (no en una cuenta del asociado).

### 23.1 Preguntas para el dueño

- **Traslado de cuentas abandonadas al ICETEX (Ley 1777, Decreto 953).** A: no aplica a la cooperativa y no se construye (lo construido; la cooperativa puede
  pasar inactivas a no reclamados por su reglamento con `CuentasAbandonadas.TrasladoHabilitado`); B: si el abogado concluye que aplica, construir el listado
  trimestral para el ICETEX como un informe de Tesorería, sin tocar el saldo del titular (el Decreto lo exige así); C: B y además el mutuo registrado en
  Tesorería. Recomendado: A hasta el concepto del abogado; si aplica, B.
- **Giro de una entrega por fallecimiento que Tesorería rechaza.** A: el saldo vuelve a la cuenta, que sigue en cierre, y queda pendiente (lo construido: un
  caso aprobado no se vuelve a registrar); B: una ruta para volver a emitir la orden al mismo destinatario con otra cuenta bancaria; C: pasado
  `Captaciones.DiasParaNoReclamados`, a no reclamados a nombre del fallecido. Recomendado: B, y C como respaldo.
- **GMF de la entrega por fallecimiento y del pago de no reclamados.** A: no se cobra (lo construido); B: se cobra como retiro (hecho generador de
  `Gmf.HechosGeneradores`). Recomendado: que lo decida la contadora.
- **Inactividad del ahorro permanente y del contractual.** A: se miden como el ahorro a la vista si su línea trae días de inactividad (lo construido); B: sólo
  el ahorro a la vista. Recomendado: A (el CUIF tiene subcuentas de inactivas del contractual).

### 23.2 Para la contadora y el abogado (pendiente de validar)

- Si la Ley 1777 y el Decreto 953 aplican a la cooperativa (concepto unificado de la Supersolidaria 20161100263381 de 2016) y el plazo de las cuentas
  abandonadas que fije el reglamento (`CuentasAbandonadas.*`).
- Los días de inactividad de cada línea (D21: 360), si siguen causando inactivas, el cargo por inactividad y los días del aviso previo (30).
- `Captaciones.DiasParaNoReclamados` (sin valor: no traslada solo) y la cuenta de los no reclamados por pagar (CUIF sugerida 246595).
- El tope de la entrega sin juicio de sucesión (`TOPE_ENTREGA_SIN_SUCESION`, EOSF art. 127) que rige y los soportes del reglamento
  (`Fallecimiento.SoportesDeEntregaSinSucesion`).
- Los textos de los avisos `PREINACTIV`, `INACTIVA` y `NORECLAMA`.

## 24. Enmienda del fondo de liquidez, el riesgo de liquidez y los depósitos asegurados (2026-10-19, T214–T218, US15, K11)

Construida la historia 15 y el pendiente de la fase 17 (la validación contable previa del fallecimiento y de los no reclamados, E202). **Manda el código**;
estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. Cambio de esquema: `CaptacionesAportes` **regenerada** en los dos
motores (mismos sellos: PostgreSQL `20261019104128`, SQL Server `20261019104056`; las operaciones nuevas al final de `Up` y al principio de `Down`, por una
migración temporal fusionada) con `LND_LiquidityFundSnapshots` y `LND_LiquidityFundHoldings`; el SQL escrito a mano (T044, `NOM`, `ASO`) quedó intacto —el
antes y el después del `.cs` sólo difieren en líneas agregadas— y la paridad pasa. Las migraciones congeladas de la 013 y `RetiroDeCaptacionesHeredadas` no se
tocaron. `EntregaVigente` sigue en K6b: las claves K11 se leen con sus defectos y el corte del ciclo diario no corre hasta K11.

**Norma aplicada** (todo «pendiente de validar por la contadora»; ningún valor legal en el código: los pisos son valores nacionales `LIMITE` con vigencia y
fuente, y lo demás parámetros de la entidad):

- **Fondo de liquidez — Decreto 1068 de 2015, Libro 2, Parte 11, Título 7.** El Decreto 961 del 5-jun-2018 incorporó el Título (Capítulo I, gestión del
  riesgo de liquidez: vencimientos contractuales o esperados con horizonte mínimo de un año, art. 2.11.7.1.3; Capítulo II, fondo de liquidez) y el Decreto
  704 de 2019 modificó los arts. 2.11.7.2.1 a 2.11.7.2.4: al menos el **10 % de los depósitos**, de forma permanente, sobre el saldo de los estados
  financieros; el ahorro permanente al **2 %** si los estatutos sólo lo dejan retirar al desvincularse y al 10 % si admiten retiro parcial; instrumentos en
  establecimientos de crédito vigilados por la Superintendencia Financiera (cuentas, CDT, CDAT, bonos) o fondos de inversión colectiva del mercado monetario
  o abiertos sin pacto de permanencia; se usa sólo para atender retiros, con aviso previo a la Supersolidaria y plan de reconstitución; se reporta en el
  formato y la periodicidad que fije la Supersolidaria. Fuente: <https://normograma.com/keralty/compilacion/docs/decreto_0961_2018.htm>. La Circular Básica
  Contable y Financiera lo mide sobre el saldo de depósitos del mes de reporte (Título III, Capítulo I):
  <https://normograma.info/findeter/docs/circular_supersolidaria_0022_2020.htm>.
- **Decreto 976 del 4-ago-2026** (Diario Oficial 53.578; rige desde el 6-ago-2026): sustituye el Capítulo II. **Sección 1** (básica e intermedia, fondos de
  empleados y mutuales): el régimen anterior (10 % y 2 %). **Sección 2** (categoría plena, art. 2.11.7.2.2.1 y siguientes): base = capital **más intereses
  causados y por causar** hasta la maduración contractual; **10 %** del ahorro ordinario, del CDAT y el contractual hasta 30 días o con redención anticipada
  pactada y del permanente con retiro parcial; **5 %** del CDAT y el contractual de 31 a 540 días y del permanente que sólo se retira al desvincularse; **0 %**
  de más de 540 días; activos admisibles: cuentas en establecimientos de crédito, CDT con grado de inversión con descuento del 20 % y TES clase B (sin fondos
  de inversión colectiva). Art. 4: **42 meses de transición** y 24 meses para que la Supersolidaria expida instrucciones. Fuentes:
  <https://normograma.com/keralty/compilacion/docs/decreto_0976_2026.htm> y
  <https://www.valoraanalitik.com/wp-content/uploads/2026/08/DECRETO-No.-0976-DEL-4-DE-AGOSTO-DE-2026.pdf> (cotejar con el Diario Oficial).
- **Categorías**: cooperativas por activos en UVR (Decreto 1544 de 2024, art. 2.11.1.3: básica < 315 millones de UVR, intermedia hasta 1.400 millones,
  plena por encima; las de segundo grado son plenas) <https://normograma.dian.gov.co/dian/compilacion/docs/decreto_1544_2024.htm>; fondos de empleados por
  activos en pesos de 2017 ajustados por IPC (Decreto 344 de 2017, art. 2.11.5.1.3) <https://www.funcionpublica.gov.co/eva/gestornormativo/norma.php?i=79774>.
  La categoría la registra la entidad en su perfil regulatorio (`LiquidityFundCategory`, FR-018).
- **Riesgo de liquidez — Circular Básica Contable y Financiera (Circular Externa 22 de 2020), Título IV, Capítulo III (SARL).** Anexo 1 (cooperativas de ahorro y
  crédito y multiactivas con sección): IRL a 90 días en cuatro bandas (1–15, 16–30, 31–60, 61–90), cálculo al menos **semanal**; activos líquidos netos
  (efectivo, fondo de liquidez e inversiones negociables a valor de mercado, con descuento del Banco de la República o del 20 % donde no haya referencia);
  entradas (cartera ajustada por el índice de mora de más de 30 días), salidas contractuales (CDAT y contractual con capital e intereses hasta el vencimiento,
  obligaciones financieras, créditos aprobados no desembolsados, cuentas por pagar) y no contractuales (ahorro ordinario por VaR al 95 % escalado por √30,
  aportes y permanente por el retiro promedio); **IRL = (ALN + entradas acumuladas) ÷ (salidas acumuladas)**, límite **≥ 100 %**, y una banda 1 o 2 por
  debajo obliga a reportar cada semana y a un plan de ajuste hasta **más de 110 %**. Anexo 2 (fondos de empleados plenos o de primer nivel y mutuales):
  <https://www.supersolidaria.gov.co/sites/default/files/data/20250131_anexo_2_cap_3_tit_4.pdf>. Anexo 3 (todas): **brecha de liquidez** a un año en siete
  bandas (hasta 1 mes, 1–2, 2–3, 3–6, 6–9, 9–12 y más de 12 meses), mensual; la parte estable del ahorro a la vista y el permanente en la última banda.
- **Formatos SICSES** (instructivo D-GEIN-010 v04,
  <https://www.supersolidaria.gov.co/sites/default/files/public/sicses/d-gein-010_instructivo_reporte_formulario_oficialrendicion_cuentas_-_sicses_v4.pdf>):
  F27 fondo de liquidez (código 9067), F29 evaluación del riesgo de liquidez (código 159). Son de la fase 19 (T220) y su periodicidad la decide la entidad.
- **Seguro de depósitos — Fogacoop**: **50.000.000 por persona** en cada entidad inscrita, sumando sus productos, desde el 19-ago-2024 (antes 25 millones);
  no cubre aportes; inscritas: cooperativas financieras, de ahorro y crédito y multiactivas o integrales con sección de ahorro y crédito.
  <https://www.fogacoop.gov.co/seguro-para-su-ahorro/montos-cubiertos>,
  <https://www.fogacoop.gov.co/publicaciones/boletin-financiero-y-juridico/Pdf%20Boletines/Boletin83.pdf>.
- **Plazos y periodicidades informativos** (criterio del dueño): nada se bloquea por ellos. La entidad fija cada cuánto se toma la foto
  (`FondoDeLiquidez.Periodicidad`) y cuándo reporta.

- **E206 · La validación contable previa del ciclo de vida (pendiente de la fase 17).** La aprobación de la entrega por fallecimiento, el traslado a mano a no
  reclamados y el pago de un saldo no reclamado preguntan a Contabilidad antes de mover nada, como las demás pantallas (D23, `Contabilidad.FaltaDeRegla`;
  `ValidacionPreviaDelCicloDeVida` sobre `ValidacionPreviaDeCaptaciones.EnPantallaAsync`): con `Bloquear` y una regla faltante responden el 422
  `Lending.Prevalidation.NotPostable` (el caso sigue `DeliveryPending`, el saldo `Held`, no nace ninguna orden); con `Advertir`, siguen y alertan. Los hechos
  representativos son nuevos en `HechosRepresentativosDeCaptaciones` (`TrasladoANoReclamados`, `PagoDeNoReclamadoPorGiro`, `PagoDeNoReclamadoPorTraslado`)
  o los de siempre (la liquidación y la retención si la cuenta causó alguna vez, la devolución con motivo `DeceasedDelivery`, el traslado). El traslado del
  **ciclo diario** también pregunta por la tanda (`PreguntaDeTanda`): con `Bloquear` la cuenta queda en los errores de la corrida y sale
  `Cartera.HechoNoContabilizable`. El resultado de la pregunta no se sella todavía en el mensaje del traslado y del pago (`PrevalidationOutcome.NotApplicable`
  en `TrasladoANoReclamadosAEmitir` y `DevolucionAEmitir`, como antes). Pruebas: seis nuevas en `CicloDeVidaDeCuentasTests.ValidacionPrevia.cs`.
- **E207 · Valores legales.** Cinco `LIMITE` nuevos con su fuente (`CatalogoDeLimitesLegales`): `FONDO_LIQUIDEZ_DEPOSITOS` (0,10 desde 2018-06-05),
  `FONDO_LIQUIDEZ_PERMANENTE_SOLO_RETIRO` (0,02), `FONDO_LIQUIDEZ_PLENA_BANDA_MEDIA` (0,05 desde 2026-08-06), `FONDO_LIQUIDEZ_PLENA_PERMANENTE_SOLO_RETIRO`
  (0,05) e `IRL_MINIMO` (1 vez, acota `RiesgoDeLiquidez.UmbralIrl`). `TOPE_SEGURO_DEPOSITOS` **sí se siembra** (data-model §9.2 decía que no): 50.000.000 desde
  el 2024-08-19, en espera hasta K11, pendiente de validar el número de la resolución.
- **E208 · El motor (T214).** `FondoDeLiquidez` (puro) recibe la regla de la categoría, la regla general (la que rige en la transición), los pisos y los
  depósitos por clase (`ClaseDeDepositoParaLiquidez`: a la vista, CDAT, contractual, permanente con retiro parcial, permanente sólo al retirarse); cada
  porcentaje es el mayor entre el de la entidad y su piso; el requerido de cada renglón va al centavo. En transición (fecha anterior a `finDeLaTransicion`)
  rige la general y la de madurez se informa. La tabla vive en `FondoDeLiquidez.PorcentajesPorCategoria` (JSON por categoría, `*` para las demás) y **nace con
  los defectos del Decreto 976** (`ParametrosDeCartera.DefectoDelFondoDeLiquidez`; data-model y E15 decían «vacío»): `*` general 10 % y 2 %; `Plena` por madurez
  10/5/0 % con bandas de 30 y 540 días, permanente al 5 %, intereses causados y por causar, fin de la transición el 2030-02-06. Se suman `RiesgoDeLiquidez`
  (brecha por banda, acumulada e IRL acumulado a cuatro decimales; lo que cae después de la última banda con tope queda «fuera del horizonte») y
  `DepositosAsegurados` (por ahorrador hasta el tope). Siete casos dorados `liquidez-01` a `-07`, calculados por `calcular.py`.
- **E209 · Entidades (T215).** Contra data-model §6.7/§6.8: la foto suma `NewRegimeRequiredAmount` (lo que requeriría el régimen nuevo en transición),
  `MinimumIrl` (alias de precisión nuevo `Indicador`, 18,4), `LiquidityRiskJson` (la medición del IRL) y `Reason`; `RegimeStage` es `General`, `PorMadurez` o
  `Transicion`; es `IHechoInmutable` y una fecha tiene **una sola foto** (`Version` 1; pedirla otra vez devuelve la misma). La inversión suma `Note`,
  `EndReason` y `EndedByUserId`, y `WithdrawnOn` es el primer día en que ya no cuenta (vigente en `d` si `ConstitutedOn ≤ d < WithdrawnOn`).
  `LiquidityHoldingKind.GovernmentBond` **4** (TES clase B, al final). No nace `CaptacionesLiquidez` (E35).
- **E210 · Rutas (T217).** Las seis de api.md §15 y una nueva, `GET /api/lending/liquidity-fund/risk?date=&measure=Irl|Brecha`. El cuerpo de `POST /holdings`
  suma `reference` (obligatoria) y `publicId` (el que la pantalla reservó como dueño del soporte, como el indicador regulatorio); `instrument` es
  `LiquidityHoldingKind`; el **soporte es obligatorio** (data-model: no nulo; api.md lo ponía opcional) con el tipo de dueño nuevo
  `LendingLiquidityFundSupport` (se lee con `.View`, se sube y se borra con `.Manage` mientras ninguna inversión lo lleve). Las escrituras responden 200 con
  `Idempotency-Key`. Todo exige **alcance total** (`Lending.LiquidityFund.ScopeNotTotal`): el fondo es de toda la cooperativa. Errores:
  `Lending.LiquidityFund.ProfileCategoryMissing`, `.RulesInvalid`, `.Invalid`, `.HoldingNotFound`, `.HoldingEnded`, `.SupportRequired`, `.ScopeNotTotal`.
- **E211 · La base del fondo.** Se lee del **libro** (la autoridad), nunca de las posiciones: las cuentas de captación vivas a la fecha (aportes no), capital;
  interés por pagar y bonificación como causados; lo no reclamado no es del titular; el permanente por `WithdrawableOnlyOnMembershipWithdrawal` de su línea
  vigente; el vencimiento de la última versión de condiciones (CDAT y contractual); el interés por causar del CDAT es lo que falta pagar de su plan menos lo
  causado (el contractual no tiene plan de intereses: cero); la redención anticipada pactada es `EarlyRedemptionAllowed` de la línea.
- **E212 · El riesgo de liquidez (modelo de la entidad).** Activos líquidos netos: **sólo el fondo constituido** con el descuento de su clase
  (`RiesgoDeLiquidez.DescuentosPorInstrumento`: 0 % en cuentas, 20 % en lo demás); entradas: las cuotas de cartera por vencer de los créditos activos (capital e
  interés de la versión vigente de cada cuota, sin descontar abonos anticipados) por (1 − `RiesgoDeLiquidez.IndiceDeMora`); salidas contractuales: los
  intereses del CDAT en sus fechas de pago y su capital al vencimiento, el contractual con lo causado al vencimiento, lo vencido en la primera banda; no
  contractuales: `RiesgoDeLiquidez.FraccionDeRetiroALaVista` del ahorro a la vista en la primera banda y `.FraccionMensualDeRetiroDeAportes` del permanente
  y de los aportes cada mes; en la brecha, lo que queda va a la banda abierta. Bandas por parámetro (`BandasIrl` [15, 30, 60, 90]; `BandasDeBrecha`
  [30, 60, 90, 180, 270, 360, null]). **No están** el disponible y las inversiones de Tesorería, las obligaciones financieras, las cuentas por pagar, los
  créditos aprobados sin desembolsar, el VaR del ahorro a la vista ni los fondos sociales (pregunta §24.1).
- **E213 · Alertas, parámetros, ciclo y vistas (T216).** Tipos nuevos `Cartera.FondoDeLiquidezInsuficiente` (crítica, app y correo, con la diferencia),
  `Cartera.FondoDeLiquidezEnElMargen` (aviso, constituido < requerido × (1 + `FondoDeLiquidez.MargenDeAlerta`)) y `Cartera.RiesgoDeLiquidezBajo` (crítica, IRL
  bajo `RiesgoDeLiquidez.UmbralIrl`, con el umbral del plan de ajuste); destinatarios `Lending.LiquidityFund.Manage` (el tesorero) y
  `Lending.Decisions.Management` (la gerencia): **un destinatario nunca es un permiso de consulta** (regla de la plataforma de alertas), así que «al gerente»
  va por el permiso que sólo tiene la gerencia; el rol sugerido `cartera.gerente` suma `Lending.LiquidityFund.View` para ver la pantalla. Diez parámetros K11
  nuevos (`FondoDeLiquidez.MargenDeAlerta` 0,05, `.Periodicidad` Semanal; `RiesgoDeLiquidez.BandasIrl`, `.BandasDeBrecha`, `.UmbralIrl` 1, `.UmbralDePlanDeAjuste`
  1,10, `.FraccionDeRetiroALaVista` 0,10, `.FraccionMensualDeRetiroDeAportes` 0,01, `.IndiceDeMora` vacío, `.DescuentosPorInstrumento`), todos pendientes de
  validar. El paso de captaciones gana un **cierre del día** (`ICierreDelDiaDeCaptaciones`, después de las tandas; un fallo no detiene la corrida):
  `FondoDeLiquidezDelCiclo` toma la foto en los días de corte (`Diaria`; `Semanal` el domingo; `Mensual` el último día del mes), sólo con K11 y captaciones
  activadas; sin categoría en el perfil deja el aviso en el registro. Las vistas `liquidity-fund` (sin titulares, alcance total) e `insured-deposits` (con datos
  del titular, filtra por las cuentas del ahorrador en el alcance) se adelantaron de T219. Lo asegurado es por **titular** (capital e interés causado de todas
  sus cuentas de captación; los cotitulares no se reparten, pregunta §24.1) y sólo con `DepositInsuranceEnrolled`.
- **E214 · Pantalla y pruebas.** `/cartera/fondo-de-liquidez` (pestañas Posición, Riesgo de liquidez, Constituido —registrar con soporte y terminar— y Fotos,
  con «Tomar la foto de la fecha»), enlace en el menú «Captaciones y aportes». Domain: `FondoDeLiquidezTests` (7 dorados y 5 reglas); Application:
  `Liquidity/FondoDeLiquidezTests` (9: la prueba independiente por banda contra el cálculo manual, la transición, US15-1 con la alerta y una foto por fecha, el
  colchón y la inversión que deja de contar, el soporte bloqueado, la categoría que falta, el IRL con su alerta, los asegurados, el corte del ciclo). La e2e
  `FondoDeLiquidezTests` (US15-1 por HTTP con la foto, lo constituido, el IRL y la vista de asegurados) está **escrita y compilada; e2e en la corrida final**.
- **E215 · Lo que queda.** Los archivos SICSES F27 y F29 y la vista `sicses-fondo-liquidez` (fase 19, T220); la lectura de los activos líquidos y de las
  demás partidas del IRL desde Contabilidad (§24.1); la categoría del perfil es texto libre (`Basica`, `Intermedia`, `Plena` o la que la entidad use en la
  tabla del parámetro), no se deriva de los activos en UVR.

### 24.1 Preguntas para el dueño

- **Activos líquidos del IRL.** A: sólo el fondo de liquidez constituido (lo construido; el IRL sale más bajo que el de la norma); B: leer de Contabilidad el
  disponible y las inversiones negociables por cuentas CUIF que fije la entidad (11, 1203…), con el descuento de cada una; C: que el tesorero registre los demás
  activos líquidos a mano como el constituido. Recomendado: B, porque la Circular los cuenta y Contabilidad ya es la fuente.
- **Retiros esperados del ahorro a la vista.** A: una fracción fija por parámetro (lo construido, 10 % pendiente de validar); B: el VaR al 95 % escalado por
  √30 calculado con la historia diaria del libro, como pide el Anexo 1 (con un año de historia; mientras tanto, A). Recomendado: B con A de respaldo.
- **Depósitos asegurados de cuentas con cotitulares.** A: todo al titular (lo construido); B: repartir el saldo entre titular y cotitulares por partes iguales;
  C: lo que diga Fogacoop para cuentas conjuntas, a validar. Recomendado: C, y B si Fogacoop no lo regula.

### 24.2 Para la contadora (pendiente de validar)

- La tabla `FondoDeLiquidez.PorcentajesPorCategoria` contra el Diario Oficial del Decreto 976 de 2026 (porcentajes, bandas de 30 y 540 días, intereses en la base,
  fin de la transición) y la categoría de la entidad en su perfil.
- Los pisos `FONDO_LIQUIDEZ_*` e `IRL_MINIMO`, y el tope `TOPE_SEGURO_DEPOSITOS` (número de la resolución de Fogacoop).
- El colchón (`FondoDeLiquidez.MargenDeAlerta`, 5 %) y la periodicidad de la foto (semanal).
- Las bandas del IRL y de la brecha, el índice de mora, las fracciones de retiro y los descuentos por clase de inversión.

## 25. Enmienda de los informes y los reportes regulatorios de captaciones (2026-10-19, T219–T224, US17, K11)

Construida la historia 17. **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen. Cambio de esquema:
`CaptacionesAportes` **regenerada** en los dos motores (mismos sellos: PostgreSQL `20261019104128`, SQL Server `20261019104056`; las operaciones nuevas al final de
`Up` y al principio de `Down`, por una migración temporal fusionada) con la columna `LND_RegulatoryFiles.FormatCode`, su índice único y el CHECK de clase ampliado; el
SQL escrito a mano (T044, `NOM`, `ASO`) quedó intacto —el antes y el después del `.cs` sólo difieren en líneas agregadas— y la paridad pasa (52 migraciones en par). Las
migraciones congeladas de la 013 y `RetiroDeCaptacionesHeredadas` no se tocaron. `EntregaVigente` sigue en K6b: las vistas nuevas se registran con `AvailableFrom = K11`.

**Formatos investigados** (todos «pendiente de validar por la contadora»; los campos nacen con `verified = false`):

- **Instructivo SICSES D-GEIN-010 v04** (Supersolidaria, vigente desde el 23-may-2017,
  <https://www.supersolidaria.gov.co/sites/default/files/public/sicses/d-gein-010_instructivo_reporte_formulario_oficialrendicion_cuentas_-_sicses_v4.pdf>):
  **F21 informe individual de aportes o contribuciones, código 9013** (§6.4: tipo y número de identificación —en éste la tarjeta de identidad es «I»—, saldo de
  aportes a la fecha, aporte mensual, ordinarios, extraordinarios, revalorización, monto promedio día/año del ejercicio y última fecha de pago); **F27 fondo de
  liquidez, código 9067** (§6.7: generalidades con el saldo de depósitos —cuentas CUIF 210505, 210510, 211005–211020, 212505–212520, 213005, 213010— por clase y la
  devolución del ahorro permanente 1 al desvincularse / 2 retiro parcial; detalle por título: NIT, entidad, tipo 1 CDT, 2 CDAT, 3 bonos, 4 cuentas de ahorro,
  5 patrimonio autónomo, 6 fondos de valores; número, capital, intereses, plazo, vencimiento y si capitaliza); **informe de cotitulares de depósitos, código 9068**
  (§6.11: columnas 01 a 06 —titular, cotitular, tipo de depósito 1 cuenta de ahorro, 2 CDAT, 3 contractual, 4 permanente, 5 programado de vivienda, número—); el
  mismo §6.11 nombra el **(9026) informe individual de las captaciones** y fija dos de sus columnas: la 5 «tipo de ahorro» y la 16 «número de cuenta del producto»;
  **F29 evaluación del riesgo de liquidez, código 159** (§7.4: formato matricial por unidades de captura de todo el balance en bandas de tiempo).
- **Formato 169, saldos diarios del ahorro ordinario** (Circular Externa 05 de 2019 de la Supersolidaria, según los resúmenes publicados; el texto oficial no se
  pudo descargar): campos FECHA y VALOR, positivo, el del último día igual al saldo de la cuenta 210500; lo reportan las cooperativas de ahorro y crédito, las
  multiactivas e integrales con sección, los fondos de empleados de categoría plena y las mutuales con ahorro ordinario.
- **Exógena** — Resolución DIAN 000162 de 2023 (compilada en la Resolución 227 de 2025), año gravable 2024 y siguientes,
  <https://normograma.dian.gov.co/dian/compilacion/docs/resolucion_dian_0162_2023.htm>: **art. 3, formato 1019 versión 9** (cuentas corrientes y de ahorro;
  se informa quien tuvo movimientos crédito mensuales acumulados de más de 24 UVT o un saldo del período igual o superior a 24 UVT, «aunque por cuenta los valores
  sean menores»; titulares secundarios y firmas también; campos: identificación, nombres o razón social, dirección, departamento, municipio, país, tipo y número de
  cuenta, código de exención del GMF, saldo final, promedio, mediana, máximo y mínimo del saldo, total, número, promedio y mediana de los créditos, total, número y
  promedio de los débitos); **art. 4, formato 1020 versión 8** (certificados y títulos, **por período mensual sin importar la cuantía**: número, tipo de título,
  tipo de movimiento, saldo inicial, valor de la inversión, intereses causados, intereses pagados, retención y saldo final); **art. 19**: los aportes y derechos de los
  asociados van en el **formato 1010**, que no es de la Cartera.

- **E216 · Siete formatos, no cinco.** Además de los cinco de api.md §18.2 (`sicses-captaciones` = 9026, `sicses-saldos-diarios` = 169, `sicses-fondo-liquidez` =
  9067, `exogena-1019`, `exogena-1020`) se construyen `sicses-cotitulares` (9068, acompaña al 9026 cuando una cuenta tiene cotitulares) y `sicses-aportes` (9013, el
  «aportes por asociado» que pidió el dueño). El **F29 (159) no se construye**: es el balance completo por bandas (efectivo, inversiones, cartera, obligaciones,
  patrimonio) y la Cartera sólo conoce sus renglones de depósitos y aportes; queda como pregunta (§25.1). Las e2e y la pantalla los ofrecen todos.
- **E217 · Esquema.** `RegulatoryFileKind` suma `DepositsSicses` (3) y `Exogenous` (4) —nunca se renumera—; `LND_RegulatoryFiles.FormatCode` (`nvarchar(40)`,
  obligatorio en 3 y 4, nulo en 1 y 2: CHECK `CK_LND_RegulatoryFiles_BureauCode` ampliado) con `UK_LND_RegulatoryFiles_Format_Period_Version` filtrado a
  `FormatCode IS NOT NULL` (el traductor de filtros de PostgreSQL no entiende `IN`). La exógena se guarda con **`PeriodMonth = 12`** (contracts decía nulo: la columna
  sigue `NOT NULL` y el mes 12 es el del cierre que la habilita). `RegulatoryFile.Nueva` recibe el formato; `RegulatoryFileDto`, `RegulatoryLayoutDto` y la lista
  `/files` (filtro `format`) lo exponen.
- **E218 · Catálogo y layouts.** Los JSON viven junto a los de la 013 (`Application/Lending/Regulatory/Layouts/`, C21) y llevan `formatCode` (minúsculas, prefijo
  `sicses-` o `exogena-`, archivo `{formatCode}-{versión}-{aaaa-mm-dd}.json`, sin `bureauCode`); el catálogo se niega a cargar uno mal nombrado y busca el vigente con
  `VigenteDelFormato`. Vigencias: los del SICSES desde **2026-01-01** (la fecha de la norma va en `source`), la exógena desde **2024-01-01** (año gravable 2024). El
  SICSES va delimitado por `|`, UTF-8, montos con punto y dos decimales y fechas `dd/MM/yyyy` (como el 9027 de la 013); la exógena por `;` en **pesos enteros** como
  entrada del prevalidador de la DIAN (que arma el XML). Generar deja `Lending.Regulatory.LayoutSinCotejar` («formato sin cotejar») hasta que la contadora publique la
  versión cotejada; nunca va dentro del archivo.
- **E219 · La proyección (`ProyeccionDeCaptaciones`).** Lee el **libro** (la autoridad) con fecha valor hasta el corte. **Exige el cierre mensual vigente de la Cartera**
  del mes (SICSES) o de diciembre (exógena): sin él, `Lending.Regulatory.PeriodNotClosed`. 9026: una fila por cuenta de captación viva al último día del mes, con la
  tasa de las condiciones selladas (CDAT, contractual) o de la línea (la del tramo del saldo), el plazo, el vencimiento, la periodicidad de intereses, la última cuota
  pactada, el último movimiento del titular, el estado (1 activa, 2 inactiva, 3 vencida, 4 en cancelación), la exención de GMF, los cotitulares, el tipo de cuenta
  (1 individual, 2 conjunta, 3 alterna) y el embargo; el **código contable va vacío** (la Cartera no conoce cuentas). 9068: titular y cotitular vigentes al corte.
  9013: por asociado, las cuatro clases (los mínimos se suman a los ordinarios: el formato no tiene renglón propio), la cuota del mes, el promedio de los saldos diarios
  desde el 1 de enero o la apertura y el último pago. 169: el capital del ahorro a la vista al cierre de cada día del mes. 9067: los saldos de depósitos por clase
  de `FondoDeLiquidezDeCartera.DepositosAsync` (capital más causado) y las inversiones vigentes ese día; la inversión registrada no guarda intereses ni capitalización
  (van 0 y NO) y el CDT de un banco no se distingue del CDAT de un organismo de grado superior (va 1); una inversión «otra» no tiene tipo y bloquea, con la advertencia
  `Lending.Regulatory.InversionSinTipo`. 1019: por persona (titular principal y cotitulares como secundarios, cada uno con las cifras de la cuenta completa) y cuenta de
  ahorro a la vista, contractual o permanente (2, 3, 4) del año; estadísticas de `EstadisticasDeSaldos` sobre los saldos diarios del año y los movimientos agrupados
  por operación (sin el saldo inicial migrado ni las reclasificaciones); umbral = UVT del año × LIMITE `EXOGENA_1019_UMBRAL_UVT` (24, sembrado con su fuente; sin
  UVT o sin límite, todos y la advertencia `Lending.Regulatory.UmbralSinValor`). 1020: por persona, CDAT y mes vigente del año; saldos, inversión e intereses causados
  repartidos por la proporción del certificado tributario (`ProporcionesDeTitularidad`), intereses pagados y retención de los acumulados tributarios ya repartidos;
  movimiento 1 emisión (no la migrada), 3 cancelación, 2 renovación o prórroga, 4 vigente. Tipos de documento: SICSES por el instructivo (C, N, P, E, T, R, U, O; I en
  el 9013) y DIAN por su tabla (13, 31, 22, 41, 12, 11, 21, 42, 47, 48); un tipo sin equivalente en la DIAN bloquea. País 169.
- **E220 · Validar y generar.** `PreparacionDeArchivoRegulatorio.CaptacionesAsync` y `GeneracionDeArchivoRegulatorio.GenerarAsync(..., formato)`: la misma mecánica de
  la 013 (validar no guarda; con bloqueantes, versión `Validated` sin archivo y 422 `BlockingIssues`; con advertencias, reconocerlas; regenerar deja `Superseded`; lo
  cargado no se regenera), con las versiones **por formato** (dos formatos del mismo mes no compiten). Nombre del archivo
  `{número}_{FORMATO}_{aaaa-mm|aaaa}_v{n}.txt`. El plazo es **informativo** (decisión del dueño): el del SICSES de cartera (perfil, documento `TiposDeOrganizacion` o
  `Sicses.MesesDeCorte`/`DiasDePlazo`) sólo produce la advertencia del mes fuera de la periodicidad; la exógena no calcula plazo. Validar, generar y la vista previa
  exigen **alcance total** (`Lending.Regulatory.ScopeNotTotal`): el formato es de toda la cooperativa.
- **E221 · Vistas.** `deposit-accounts-by-status` (con datos del titular; captación y aportes abiertas al corte, con `status`, saldo del titular del libro y marcas
  —embargada, exenta de GMF, causación suspendida, datos por actualizar—; el estado es el de hoy: el ERP no guarda el de cada día) y una **vista previa por formato**
  (`year`, `month`; la exógena sólo `year`; por defecto el mes o el año anterior): una columna por campo del detalle con el texto que iría en el archivo, el encabezado
  del 9067, las advertencias y las inconsistencias en las notas. Con datos del titular las de 9026, 9068, 9013, 1019 y 1020; sin ellos 169 y 9067.
- **E222 · Rutas.** `POST /api/lending/regulatory/deposit-formats/{formatCode}/validate` con `{ year, month? }` en el **cuerpo** (api.md lo ponía igual) y
  `/generate` con `{ year, month?, acknowledgeWarnings }` e `Idempotency-Key`, ambas con `Lending.RegulatoryReports.Generate`; `GET /files?format=`. Errores nuevos:
  `Lending.Regulatory.FormatUnknown`, `.PeriodNotClosed`, `.PeriodInvalid` (SICSES sin mes), `.ScopeNotTotal`.
- **E223 · Pantallas.** `/cartera/captaciones/reportes-regulatorios` (`Lending.RegulatoryFiles.Read`; validar y generar con `Lending.RegulatoryReports.Generate`): el
  formato, el período, el estado de cotejo del layout, las inconsistencias, reconocer «formato sin cotejar», las versiones con descarga y «marcar cargado» (el
  componente `GeneracionesRegulatorias` de la 013 con el parámetro `Formato`). El centro de reportes suma «Captaciones y aportes» (saldos, cuentas por estado, aportes
  por asociado, intereses y retención, reportes regulatorios); `/cartera/informes` ofrece año, mes, los estados de cuenta y abre la ficha de la cuenta (`_cuenta`).
  Ninguna pantalla nombra al sistema anterior.
- **E224 · Pruebas.** Domain: `EstadisticasDeSaldosTests` (5). Application: `FormatosDeCaptacionesTests` (catálogo, reglas de carga, traducciones y **siete archivos
  dorados** `Casos/*.esperado.txt`, regenerables sólo con `CARTERA_ESCRIBIR_ESPERADO=1`) e `InformesDeCaptacionesTests` (8 sobre el libro real: la prueba independiente
  —el 9026 de un mes cuadra con `deposit-balances` y sale con «formato sin cotejar»—, regenerar y cargado, sin cierre y alcance parcial, saldos diarios, umbral de la
  1019, el CDAT en la 1020 y el fondo, cuentas por estado y el registro de vistas). Ajustadas las pruebas fijadas de la 013 (catálogo de layouts, `RegulatoryFileKind`,
  los 40 límites) y el alcance de las consultas nuevas. e2e `InformesDeCaptacionesTests` **escrita y compilada; e2e en la corrida final**.
- **E225 · Lo que queda.** El código contable del 9026 (lo resolvería la matriz de Contabilidad, como el 9027 de la 013); los intereses y la capitalización de las
  inversiones del fondo (la inversión registrada no los guarda); el F29; el 1010 de aportes (no es de la Cartera); cotejar los siete layouts (T235).

### 25.1 Preguntas para el dueño

- **F29 (159), evaluación del riesgo de liquidez.** A: no generarlo desde la Cartera (lo construido; la entidad lo diligencia con su balance); B: generar sólo los
  renglones de depósitos y aportes por banda (unidades de captura 2 y 3) como insumo para quien arma el formato; C: armarlo completo leyendo de Contabilidad las demás
  partidas por cuentas CUIF que fije la entidad (la misma decisión de §24.1 sobre los activos líquidos). Recomendado: B ahora y C con la respuesta de §24.1.
- **Código contable en el 9026.** A: vacío (lo construido); B: resolverlo por la matriz contable de captaciones, como el 9027 de cartera lo resuelve para el capital.
  Recomendado: B, si el instructivo lo pide obligatorio al cotejar.
- **Inversiones del fondo (9067).** A: CDT/CDAT, intereses en 0 y «no capitaliza» (lo construido); B: que el tesorero registre en cada inversión si es CDT o CDAT, los
  intereses causados y si capitaliza. Recomendado: B, porque el instructivo los pide por título.

### 25.2 Para la contadora (pendiente de validar)

- Cotejar los siete layouts contra el instructivo vigente y un archivo aceptado por el validador (T235): orden, largo y formato de cada campo, separador, formato de
  fecha y los códigos (tipos de identificación, tipo de ahorro, estado, tipo de cuenta, tipo de título, tipo de movimiento de la 1020, exención de GMF de la 1019).
- Si los mínimos se reportan dentro de los ordinarios en el 9013 y si el saldo de depósitos del 9067 lleva los intereses causados.
- El umbral de la 1019 (24 UVT) y su lectura «por persona sumando sus cuentas»; si el ahorro contractual y el permanente van en la 1019 (lo construido) o en otro
  formato.

## 26. Enmienda del cierre: congelamiento, entregas, documentación y revisión transversal (2026-10-19, T225–T233, Polish)

Fase 20 sin las e2e (T230: la corrida final de las e2e de captaciones y las de la 013 en los dos motores la lanza el dueño aparte) ni `CLAUDE.md` (T229, lo
escribe el dueño). **Manda el código**; estas notas reemplazan lo dicho en `data-model.md`, `contracts/` y `tasks.md` donde choquen.

- **E226 · Lo heredado que quedó vivo.** `GET /api/reports/cdt/certificate/{id}/pdf` (`CDTReportsEndpoints`, sin permiso, respondía siempre 404 «not yet
  implemented») y las clases `CdtCertificateReport` y `SavingsStatementReport` (sin llamador) se retiraron: quedaron fuera de T005–T010. El título del CDAT y
  el extracto salen de `DocumentosDeCaptacionesReport` (US13).
- **E227 · Congelamiento (T225).** `CaptacionesAportes` **no se regeneró**: `dotnet ef migrations has-pending-model-changes` limpio en PostgreSQL y SQL Server
  (cadena ficticia por variable de entorno; el worktree no tiene `appsettings.Development.json`) y `check-migration-parity.ps1` con 52 migraciones de
  aplicación en par. Quedan los sellos PostgreSQL `20261019104128` y SQL Server `20261019104056`, 53 `CreateTable`, y el SQL escrito a mano intacto (la regla
  de T044 `|*|*|*|*|*`, `ProveedorDeCuentas` para `NOM` y el participante `ASO`). Las `CaptacionesAhorro`, `CaptacionesCdat`, `CaptacionesAsamblea` y
  `CaptacionesLiquidez` de T225 no existen (E35). Como en la 013 (T0802), el congelamiento queda **fijado por una prueba**:
  `LasMigracionesCongeladasNoSeRegeneran` guarda la huella SHA-256 (sin BOM, saltos normalizados a LF, sin blancos finales) del `.cs` y del `.Designer.cs`
  de cada migración de la 013 y la 014 —desde `PlataformaParaCartera` hasta `CaptacionesAportes`, 17 por motor, 68 huellas— y que los dos motores congelan
  las mismas. Regenerar, aunque se conserve el sello, o tocar el SQL a mano la rompe. **Desde aquí todo cambio de esquema es una migración nueva.** Ojo: la
  huella incluye los comentarios; anotar respaldo y segundo revisor en la cabecera de `RetiroDeCaptacionesHeredadas` (Principio XII) exige actualizar la
  huella en el mismo commit (lo dice `docs/operaciones/captaciones-despliegue.md` §2.3). «Que aplican sobre una base de la 013 en los dos motores» lo prueba
  la corrida e2e final (la fixture aplica toda la cadena), no esta fase.
- **E228 · `EntregaVigente = K11`.** `CatalogoDeCartera.EntregaVigente` sube de K6b a **K11** de una vez (Q1: ninguna entrega de captaciones sale sola).
  Abre: las 62 claves de captaciones del catálogo de parámetros (antes se leían con su defecto), el componente inflacionario en la semilla central
  (`COMPONENTE_INFLACIONARIO` 2025), las clases de documento 7 a 15, las plantillas de migración de captaciones, los valores nacionales de K8 a K11, las
  vistas con `AvailableFrom` K8 a K11, las tareas diarias de captaciones en la vigilancia y el corte del fondo de liquidez del ciclo diario.
  Pruebas que fijaban K6b y pasan a K11: `EnumeracionesDeCarteraTests`, `CatalogoDeLimitesLegalesTests` (ahora valida contra K6b explícito),
  `EntregaDeModuloTests` (la de claves ofrecidas pasa a «K1 a K11»), `ParametrosDeCarteraTests`, `DocumentosDeK5Tests`,
  `RegistroCentralDeValoresNacionalesTests` (la semilla inserta el componente inflacionario) y `CadaParametroRespetaLaEntregaDeSuModulo`.
  **e2e que dependen de esto** (se escribieron para K11 y nunca corrieron con K6b): toda la carpeta `Lending/Deposits` (la fixture `CaptacionesE2E` exige
  K11; `AsambleaTests` y `CicloDeVidaDeCuentasTests` piden al menos K10; `DocumentosDeCaptacionesTests` al menos K8; `CdatTests`, `AhorroContractualTests`,
  `FondoDeLiquidezTests` e `InformesDeCaptacionesTests` por sus rutas), además de `ViviendaConUvrTests` (al menos K6b, sigue cumpliendo) y las de valores
  nacionales que cuentan lo sembrado.
- **E229 · Casos dorados (T233).** `CasosDoradosDeCaptacionesTests.Hay_al_menos_40_casos_y_cubren_lo_que_lista_FR_026`: hay 67 JSON (5 asamblea, 6 cargo,
  10 CDAT, 7 contractual, 7 embargo, 6 GMF, 9 interés, 7 liquidez, 8 retención, 2 tasa negociada) y cada cifra de FR-026 (481.244, 488.088, 79.741, 478.446,
  79.111, 6.884, 3.649, 6.676, 41.952, 2.465.306, 2.486.777, 21.471) está en el caso que la fija; los demás temas de FR-026, por su caso.
- **E230 · Sistema anterior (T232).** Búsqueda en `src/` (pantallas, plantillas, informes, semillas) y en los manuales: ningún texto visible lo nombra.
  `TextosDeCarteraTests` suma las rutas de Cartera (`API/Endpoints/Lending`, cuyos mensajes llegan a la pantalla) y `API/Reports/Captaciones`; ya leía
  `Application/Lending`, `Domain/Lending`, las pantallas de Cartera y `docs/manual/cartera*.md` (donde caen los manuales nuevos).
- **E231 · Revisión transversal.** Verdes y sin cambio: todas las reglas de arquitectura que nombran plan y tasks (las cuatro migraciones de E35 son los únicos
  nombres sin prueba), los permisos `Lending.*` sembrados por la API (163 entradas; E12 decía 162 antes de D44), `LasPantallasDicenQueEstanCargando` con
  «Cartera/Captaciones», `TodoEnlaceDelMenuTieneSuPagina` y `LaCarteraNoTieneValoresLegalesFijos`.
- **E232 · Enlaces a páginas que no existen.** El requisito `ProfileAllowsClasses` de captaciones mandaba a `/maestros/perfil-regulatorio` (la pantalla es
  `/admin/perfil-regulatorio`) y el aviso de parámetros comerciales de la activación de créditos (013) a `/ventas/parametros` (es `/inventario/parametros`).
  Corregidos; `LosEnlacesDeCarteraLlevanASuPagina` exige que toda ruta de pantalla literal de `Application/Lending` tenga su `@page`.
- **E233 · Texto de `CuentasAbandonadas.TrasladoHabilitado`.** Prometía un traslado «según la norma»; es la regla interna a no reclamados de E198, sin
  ICETEX, y su validación es de la contadora **y el abogado** (§23.2). El texto lo dice.
- **E234 · Cancelación por el contrato.** `closeAccount` en `RetirarAsync` sigue rechazado hasta que exista Recaudo (E99), con el mismo código
  `Lending.Savings.DeliveryNotAvailable`, pero ya no nombra una entrega «US5» (no lo es): dice que la cuenta se cancela desde Cartera y lleva
  `availableFrom = null`.
- **E235 · Documentación (T226 a T228, T231).** Manuales `docs/manual/cartera-captaciones.md` (índice con todo lo «pendiente de validar») y ocho temáticos
  `cartera-captaciones-*.md`; `docs/manual/cartera.md` con K7 a K11 y el menú nuevo (la sección del grupo heredado «Ahorros y aportes» se reemplazó);
  `contabilidad-contrato-de-contabilizacion.md` §10 (hechos, roles, dimensiones, tipos de comprobante y cuentas CUIF sugeridas); runbooks
  `docs/operaciones/captaciones-puesta-en-marcha.md` (con la sección vacía de T234) y `captaciones-despliegue.md`; P20 de `estado-y-pendientes.md`.
  `contracts/` cotejados con el código: 180 rutas REST nuevas de la 014 (171 sin los dobles de prueba), 74 heredadas retiradas, neto +106 (el contrato
  decía 168 y +95). El quickstart se corrigió (filtro e2e `FullyQualifiedName~IntegrationTests.Lending.Deposits`, porque «Collection=…» no es un filtro
  válido; `PagaduriasTests` corre en «Cartera e2e»; US9 con 40 cuentas, las 500 son de rendimiento; US2 Menor sin e2e, la cubre `PartesYExencionTests`).
- **E236 · Lo que la documentación encontró y queda anotado sin cambio de código:**
  - Las versiones de línea no admiten `ValidFrom` anterior a hoy y no hay plantilla de líneas (E115), pero la carga exige versión vigente al corte: las líneas
    se crean a más tardar el día del corte (runbook §9). La e2e fija el reloj en el corte.
  - `sugerencias-cuif-captaciones.json` no sugiere cuenta para los retornos de excedentes (`SurplusPayment`, 246515 en §22.2), ni para `IngresoPorCargos`,
    `CargoDeCuentaPorCobrar`, `EmbargosPorConsignar` e `IngresoPorPenalidad`; gasto de intereses y de bonificación del contractual sugieren la misma 615015.
    El código tiene cuatro motivos de devolución más que el contrato (`DeceasedDelivery`, `ContractualMaturity`, `UnclaimedPayment`,
    `GarnishmentConsignment`).
  - `Retencion.ComponenteInflacionarioAplica` describe la norma (persona natural no obligada a llevar contabilidad) y el código lo descuenta a toda persona
    natural: sigue la pregunta de §14.1.
  - `recordedAsOf` no llega a `/close-preview`, `/term-deposits/{id}/redeem/simulate` ni `/contributions/summary` (api.md §1.4 lo pedía): queda documentado
    como estado actual.
  - Las cuatro plantillas de rol de captaciones (D18) no traen `Lending.Deposits.Activate` ni `Lending.Migration.AcceptDifferences`: se asignan aparte.

### 26.1 Preguntas para el dueño

- **Plantillas de rol.** ¿El jefe de captaciones trae `Lending.Deposits.Activate` y `Lending.Migration.AcceptDifferences`? Propuesta: no (son actos de
  puesta en marcha; se asignan a quien la dirige).
- **Líneas con fecha del corte.** ¿Se admite crear una versión de línea de captación o de aportes con vigencia anterior a hoy **sólo** antes de activar,
  para cargar saldos con un corte pasado? Propuesta: sí, restringido a antes de la activación.

### 26.2 Para la contadora (pendiente de validar)

- Las cuentas CUIF sugeridas de §10.7 del contrato de contabilización y las que faltan (E236).
- Todo lo de `docs/manual/cartera-captaciones.md` «Pendiente de validar» (P2, P3, P8 a P12, P26), la matriz, los 67 casos dorados y los siete layouts (T235).

- **E237 · Excepción puntual al congelamiento de `CaptacionesAportes` (2026-10-19, respuesta del dueño a la pregunta 107, opción A).**
  La corrida e2e final destapó que `LND_PayrollAgreementVersions.EmployeeIdentification` se creaba `varchar(20)` con el valor por
  defecto `EmployeeIdAtPaymaster` (21 caracteres): en PostgreSQL la migración fallaba con `22001` y el host de pruebas no arrancaba
  (es el único default más largo que su columna en todas las migraciones). Como la migración nunca se aplicó en ningún ambiente,
  se corrigió **dentro** de `CaptacionesAportes` de los dos motores (columna a 30; migración, Designer, snapshot y
  `PayrollAgreementConfiguration`) y se actualizaron a propósito las cuatro huellas de `LasMigracionesCongeladasNoSeRegeneran`.
  Una migración nueva no servía: la congelada falla antes de poder correr. Sin cambios de modelo pendientes y paridad en verde.
  Desde que una migración sale de local, esta excepción deja de ser posible.

- **E238 · Primera numeración de un ámbito sin carrera (2026-10-20, corrida e2e final).** Dos aperturas simultáneas de la primera cuenta de una línea agregaban
  cada una su fila de `LND_NumberSequences` con `NextValue = 1` y la segunda caía en `UK_LND_MemberAccounts_Number` (500). `ICerrojoDeCartera.AsegurarNumeracionAsync`
  inserta la fila (`ON CONFLICT DO NOTHING` / `NOT EXISTS … UPDLOCK, HOLDLOCK`) antes de bloquearla; sin transacción no hace nada. Sin migración.
- **E239 · Límite de participación sin registrar (P12).** `ContextoDeLineasDeCaptacion.TopesDeAportesAsync` respondía `NationalValues.Missing` por
  `APORTES_LIMITE_PARTICIPACION_PN/PJ` (sin valor sembrado a propósito) y bloqueaba crear la línea de aportes; «sin registrar» es ahora «sin techo», como ya
  lo leía `ReglasDeAportes.LimiteAsync`.
- **E240 · Fixture de e2e de captaciones.** La cooperativa aislada no traía perfil regulatorio (`current: null`), política de datos `GeneralTreatment` ni alcance
  de Cartera para su administrador, y las pruebas usaban la oficina administrativa donde la cuenta pide la agencia contable (`AgenciaPublicId`).
- **E241 · Reverso de una decisión de asamblea (500).** El tipo del origen del mensaje de reverso era `ASM-000001-R` (no termina en dígitos, `Prefijo` lo devolvía entero) y no cabía en `OriginDocumentTypeCode varchar(10)`. Ahora es el prefijo de la decisión (`ASM-`).
- **E242 · Conciliación de captaciones al centavo.** El libro guarda la causación con seis decimales y Contabilidad recibe `amount` al centavo (`accruedAmount` lleva los seis); la conciliación compara cada movimiento redondeado al centavo.
- **E243 · El vencimiento del contractual lo emite el proceso.** `TrasladoEntreCuentasAplicado` admite proceso: el ciclo diario entrega el ahorro contractual por traslado a la vista (`ContractualMaturity`), y con `proceso: false` el emisor lanzaba (T11) y el contrato quedaba sin terminar. A confirmar por el dueño.
- **E244 · Reverso desde el crédito del cruce y del débito automático.** `PagoDeLaPantalla` rechazaba todo `OwnLoanPayoff`; ahora admite el que tiene operación de cuenta enlazada (cruce al retiro o débito), que arrastra el débito (`IArrastreDeReversoDePago`). La recogida de un desembolso sigue rechazada.
- **E245 · El débito automático ve los créditos de cualquier canal.** `DebitoAutomatico` pedía las cuotas con la consulta de nómina, que sólo trae los créditos con canal de pago nómina propia: un crédito de ventanilla nunca se debitaba. `ConsultaDeCuotasDeNomina` suma `TodosLosCanales` (por defecto falso, Nómina no cambia) y el débito lo pide. A confirmar por el dueño: un crédito de nómina que además tenga débito automático podría pagarse dos veces (se espera que no se autorice ambos).
- **E246 · Pruebas e2e de captaciones: lo que las 66 fallas eran de la prueba.** Perfil regulatorio y política de datos en la cooperativa base; alcance total del administrador; agencia contable (no la oficina) en las cuentas; una cooperativa por prueba que corre el ciclo; el comparativo del helper de matriz miraba `status == 1` (borrador) en vez de `Active = 2`; expectativas de retención con el componente inflacionario sembrado; `CTA` como noveno tipo de documento cruce; los cinco comprobantes de captaciones (MA, CC, TD, AO, RA); las vistas de captaciones en el registro de informes.

- **E247 · La e2e de la suite completa en PostgreSQL y el aprovisionamiento interrumpido (2026-10-21).** En la corrida completa de PostgreSQL dos
  pruebas de Ventas (`ReservaConcurrenteTests`, `CreditoProvisionalTests`) caían al aceptar la invitación del administrador con un 500
  (`42P01: relation "dbo.SEC_Users" does not exist`): el log del servidor muestra que el aprovisionamiento de la cooperativa falló con
  `57P03: the database system is not yet accepting connections` (el alta responde 200 y la cooperativa queda registrada sin su base,
  reparable con `POST /api/saas/tenants/{publicId}/provision`). Aisladas pasan; SQL Server pasó la suite completa. Es un tropiezo del
  contenedor de pruebas bajo carga, no del producto: el helper de cooperativas aisladas hace esa reparación una vez y reintenta.

- **E248 · El destino del contractual al vencer lo declara la línea y lo decide el asociado (decisión del dueño del 2026-10-21; refina E243).** «El traslado
  a la vista sí, pero debe quedar configurado en el concepto (la línea) cuando se cree, para que el asociado que quiera que se traslade se haga y el que no,
  no; debe quedar en el contrato lo que el asociado decida.» La versión de la línea contractual lleva `MaturityDestinations` (máscara
  `ContractualMaturityDestinations`: `TransferToDemand` 1, `DisbursementOrder` 2; 0 = ninguno; nulo en las versiones anteriores = ambos), parte de la huella
  de la versión; el editor de líneas lo captura. La instrucción del asociado ya vivía en la cuenta (`MaturityInstructionJson`, sellada en la apertura o
  cambiada con motivo): ahora se valida contra lo que admite la versión de la línea con la que se selló el contrato
  (`Lending.Contractual.DestinationNotAllowedByLine`) al abrir y al cambiarla. `CierreDeContractual.DestinoAsync` ejecuta sólo lo que dice la instrucción
  y la línea admite; sin eso el contrato queda vencido a disposición, sin mover dinero, y se levanta la alerta `Cartera.ContractualADisposicion`
  (permiso `Lending.Accounts.Parties.Manage`). El efecto `MoveToDemand` del incumplimiento sigue siendo de la línea. El mensaje `TrasladoEntreCuentasAplicado` por
  proceso sale, como en E243, sólo en este camino. Migración aditiva `ContractualDestinosAlVencer` (una columna nula en `LND_DepositLineVersions`). No hay
  forma de «borrar» la instrucción una vez dada: se cambia por otro destino admitido (a confirmar por el dueño si debe poder retirarse).
- **E249 · Libranza o débito automático, excluyentes (decisión del dueño del 2026-10-21; resuelve la duda de E245).** «No se pueden tener las dos cosas.»
  `ExclusionDeLibranzaYDebitoAutomatico` (`Application/Lending/Payroll`) es el único sitio de la regla: registrar un débito automático de un crédito con
  vínculo de libranza vigente (`ConLibranza`: nómina propia o pagaduría) responde `Lending.DirectDebit.LoanOnPayroll`; trasladar la libranza de un crédito que
  tiene un débito automático vigente responde `Lending.Payroll.LoanHasDirectDebit`. La autorización «todos los créditos» no nombra un crédito y no se puede
  rechazar al registrarla: el ciclo del débito automático **salta** los créditos con libranza, y por eso `TodosLosCanales` ya no puede duplicar un descuento de nómina
  (probado en la e2e `CargosYDebitoAutomaticoTests`). Hoy ningún camino lleva un crédito ya existente de otra forma de pago a la libranza (el desembolso y la
  novación crean créditos nuevos, sin autorizaciones; el traslado exige que ya sea libranza), así que el lado «débito → libranza» es una guarda de defensa que
  la e2e arma con un estado heredado en la base. La migración de saldos no trae autorizaciones de débito automático, sólo los vínculos de pago de los créditos: no
  puede dejar un crédito con las dos cosas, y las autorizaciones se registran después por la puerta que ya valida. Sin migración.

## 27. Respuestas del dueño a las preguntas abiertas 55–106 (2026-10-21)

El dueño contestó las preguntas abiertas de las tandas 2 a la 20 (55–106; las de la 013 que quedaron abiertas y las de §14.1 a §26.1). Esta sección **compara cada
respuesta con lo construido**: las que coinciden quedan «confirmada, sin cambio»; las que difieren se implementaron (E250–E252, con pruebas) o, si pedían
más de lo que se podía cerrar sin la corrida e2e, quedan **pendientes** con su motivo. **Manda el código.** Un solo cambio de esquema, aditivo:
`CaptacionesRespuestasDelDueno` (PostgreSQL `20261020175152`, SQL Server `20261020175119`): `COR_People.IsObligatedToKeepBooks`,
`LND_MigrationTermDepositRows` (`DestinationBankTransferCode`, `DestinationBankAccountType`, `DestinationBankAccountNumber`) y `LND_LiquidityFundHoldings`
(`TermTitleKind`, `AccruedInterest`, `CapitalizesInterest`); paridad y `has-pending-model-changes` limpios en los dos motores. Las e2e que cambian de
comportamiento (una de líneas de captación, el conteo de permisos) se **ajustaron y compilan; no se corrieron** (el disco no admite Docker).

### 27.1 Respuesta por respuesta

- **55 A**, **56 A**, **57 A**, **60 A** (la e2e de libranza con asociados con aportes es trabajo de la corrida final), **61 A**, **62 A**, **66 A**, **68 A**, **70 A**,
  **72 A**, **75 A**, **78 A**, **79 A**, **80 A**, **84 A**, **85 A**, **88 A**, **94 A**, **95 A**, **98 A**: confirmadas, sin cambio de código.
- **59 B · una línea de aportes o ahorro con descuento por pagaduría sin código de concepto bloquea el archivo (E250).** Antes avisaba (`withoutEquivalence`) y la
  línea no iba en ningún archivo. Ahora generar el archivo de un grupo responde `Lending.PayrollFile.AccountLineWithoutPaymasterCode` (422) con las líneas sin código y
  dónde corregirlas (convenio › «Grupos y equivalencias»); la vista previa sigue contando. Se bloquea sólo por las **cuotas de cuentas** (los créditos sin código ya
  tenían su propia regla).
- **63 B · obligado a llevar contabilidad = marca en el perfil tributario (E250).** `COR_People.IsObligatedToKeepBooks` (`PersonInput`, diálogo de persona «Obligado a llevar
  contabilidad»): a la persona natural **obligada** ya no se le descuenta el componente inflacionario de la retención sobre rendimientos (`ReglasDeRetencion.PerfilDe`).
  Las personas existentes quedan en «no obligada» (lo de antes).
- **64 B · fecha valor en un período liquidado: ajuste en el período abierto (E256).** Hecha: ver §27.7.
- **65 B · cancelar con cargos por cobrar: cobrarlos del saldo (E255).** Hecha: ver §27.6.
- **67 B · plantillas de importación de líneas de captación y de aportes (E251).** `lending.deposit-lines` (hojas `Lineas` y `Tramos`) y `lending.contribution-lines` (hoja
  `Lineas`), con revisión previa, todo-o-nada, las mismas reglas del alta unitaria (`ReglasDeLineaDeCaptacion`, topes de aportes, perfil, entrega), alcance de línea
  (fuera de alcance la línea «no existe»), y rutas `GET/POST /api/lending/lines/deposit|contribution/template.xlsx|import` (permisos `DepositLines.*` y `ContributionLines.*`).
  Cubren la tasa fija o por tramos y los datos de cada clase (CDAT, contractual, permanente); **los cargos y la tasa variable se siguen configurando en pantalla**.
- **69 · inactividad de la cuenta migrada — resuelta (E262, §27.13).** La migrada se mide como cualquier otra, desde su último movimiento; la revisión previa avisa cuántas se inactivarían la primera noche.
- **71 · el aprobador debe tener la oficina y la línea del caso en su alcance (E250).** `FuenteDeAprobacionDeCuenta.AlAprobarElNivelAsync` valida el alcance de Cartera
  **del aprobador** (`AlcanceDeCarteraDeUnUsuario`: oficinas y líneas de sus asignaciones y los permisos de alcance total de sus roles) contra la cuenta; si falta responde
  `Lending.Savings.ApproverOutOfScope`. Aplica con o sin política de niveles.
- **73 B · la prórroga conserva la tasa pactada (E250).** `RolloverRateRule.Agreed` conserva la tasa de la tabla sellada y el margen negociado; antes caía a la tabla vigente.
- **74 · intereses del CDAT también a la cuenta de un tercero (E250).** `paymentDestination` admite la cuenta a la vista viva de **otra persona** (en el alcance) con el
  permiso nuevo `Lending.TermDeposits.PayThirdParty` (ya no en la plantilla del jefe de captaciones, que enumera `Constitute/Renew/Redeem`) y la instrucción escrita del
  titular subida como adjunto (`thirdPartyAuthorizationAttachmentPublicId`, que queda sellada con el destino en la propuesta o en la instrucción al vencimiento); sin
  permiso o sin constancia, `Lending.TermDeposit.ThirdPartyPermissionRequired` / `.ThirdPartyAuthorizationRequired`. La retención es la de siempre sobre el interés del
  CDAT; el GMF se calcula con el hecho `TransferToOtherHolder` (gravado) en vez de `TermDepositRedemptionToAccount`. El capital (redención, disminución) sigue sólo al titular.
- **76 B · CDAT migrado: columnas de banco y cuenta para el giro (E252).** La parte `term-deposits` de la migración gana `bancoDelGiro` (código de transferencia del banco),
  `tipoDeCuentaDelGiro` y `numeroDeCuentaDelGiro`: con `DisbursementOrder` el giro va a esa cuenta; vacías, a la cuenta bancaria registrada del titular (lo de antes). El
  banco debe existir (`Import.Cell.NotFound` al aplicar la carga) y no van con `Account`.
- **77 A**: confirmada, sin cambio (la orden rechazada se reversa entera).
- **81 B · contractual incumplido con efecto «cancelar» sin instrucción va a la cuenta a la vista del asociado (E250).** `CicloDelContractual` trata `Cancel` como `MoveToDemand`
  al buscar destino cuando no hay instrucción.
- **82 B · embargo: recalcular en el momento de cada consignación (E254).** Hecha: ver §27.5.
- **83 C · concurrencia de embargos: alimentos primero y luego por fecha de recepción (E250).** `RetencionDeEmbargos.RecalcularAsync` ordena por `IsChildSupport` descendente y
  después `ReceivedOn` e `Id`.
- **86 · el lote de documentos nace programable (E250).** Parámetro `Documentos.LotesProgramados` (JSON `[{ "clase": "AccountStatement", "dia": 5 }, { "clase":
  "DepositTaxCertificate", "mes": 3, "dia": 1 }]`, vacío por defecto, pendiente de validar) y tarea `captaciones.lotes-de-documentos` (una vez al día desde las 6:00, por
  `IEjecutorEnCooperativa`) que corre el mismo `IssueLendingDocumentBatchCommand` de la mano; un documento ya emitido no se repite. Lanzarlo a mano sigue igual.
- **87 B · el título de un CDAT migrado se emite al activar (E250).** Al activar las captaciones, ya confirmada la transacción, cada CDAT de la carga recibe su título
  (`TitulosDeCdat.ReemitirVigenteAsync`), uno por uno y sin tumbar la activación: el que no sale queda en el registro y se emite desde la ficha.
- **89 B, 90 C, 91 B, 92 B+C, 93 B, 96 B+C — PENDIENTES.** Todas tocan la aplicación de la decisión de asamblea o la entrega por fallecimiento, donde un cambio mueve hechos
  contables por destinatario y órdenes de giro (E186–E190, E203); sin la corrida e2e no se tocan. Quedan especificadas por las respuestas (retención sobre revalorización y
  retornos con concepto configurable y «pendiente de validar con el tributarista»; base de retornos por plantilla; retirados del año con prorrata y giro; giro rechazado: regirar y
  respaldo en cuenta por pagar; excedente a ahorro sin cuenta: capitalizar en aportes).
- **97 · GMF en la entrega por fallecimiento y en saldos no reclamados (E253).** Implementada: ver §27.4.
- **99 B · activos líquidos del IRL leídos de Contabilidad (E250).** Puerto nuevo `IActivosLiquidosParaCartera` (en el espacio de la plataforma, junto a
  `IContabilidadParaCartera`, que conserva sus cinco métodos) y parámetro `RiesgoDeLiquidez.ActivosLiquidosContables` (JSON de prefijo de cuenta y descuento; vacío por defecto):
  el saldo positivo de cada prefijo entra como activo líquido con su descuento. La regla la fija la contadora (no repetir lo ya registrado como fondo constituido).
- **100 B · retiros esperados con estadística (E250).** `RetiroEsperadoDeLaVista` (puro): percentil al 95 % de las salidas netas diarias del ahorro a la vista × raíz de 30; con
  `RiesgoDeLiquidez.RetirosALaVistaMetodo = Automatico` (defecto) y 365 días de historia del libro (`DiasDeHistoriaDelRetiro`) se usa; antes, la fracción fija. El supuesto lo dice
  el informe.
- **101 C · seguro de depósitos con cotitulares (E250).** `SeguroDeDepositos.RepartoDeCotitulares` (`PartesIguales` por defecto, `ProporcionRegistrada`, `AlTitular`; pendiente de
  validar con Fogacoop): el saldo de una cuenta conjunta se reparte entre sus titulares (el titular absorbe el residuo) y cada uno tiene su tope.
- **102 B · F29 (159) con sólo los renglones de depósitos y aportes (E250).** Vista `liquidity-deposit-bands` (salidas contractuales y no contractuales de depósitos y aportes por
  banda del IRL, sin titulares); no genera el archivo F29 completo.
- **103 B · código contable del 9026 por la matriz — PENDIENTE** hasta cotejar el instructivo (T235): sólo si lo exige.
- **104 B · inversiones del fondo para el 9067 (E250).** El tesorero registra en una inversión CDT/CDAT su tipo, los intereses causados y si capitaliza
  (`TermTitleKind`, `AccruedInterest`, `CapitalizesInterest`; diálogo de «Registrar inversión»); el 9067 los toma (antes iban en 0 y «no capitaliza»).
- **105 A (E250).** `cartera.jefe-captaciones` **no** trae `Lending.Deposits.Activate` ni `Lending.Migration.AcceptDifferences` (verificado; sólo `Deposits.Migration` para cargar):
  se asignan a quien dirige la puesta en marcha. Prueba nueva que lo fija.
- **106 A · versión de línea con vigencia pasada sólo antes de activar (E250).** `ActivacionesDeCaptacion.FechaMinimaDeVersionAsync`: antes de activar la naturaleza no hay fecha
  mínima (se carga la historia de tarifas de la puesta en marcha; la versión sigue sin poder empezar antes de la última registrada); activada, «hoy» como antes
  (`Lending.DepositLine.Retroactive`).

### 27.2 Lo que queda para el dueño o para la corrida final

Pendientes de código: sólo la 103 (espera el cotejo T235) y las de terceros; 64, 65, 82, 89–93, 96 y 97 están construidas (E253–E261) y la 69 quedó resuelta en E262. Las e2e de 59, 67, 71, 74, 76, 86, 87, 99–101 y 104 no están escritas (la corrida final de los dos
motores las cubre una por una); sí se ajustó la de líneas de captación y el conteo de permisos (164). Los defectos nuevos son parámetros «pendientes de validar» de la contadora:
`Documentos.LotesProgramados`, `SeguroDeDepositos.RepartoDeCotitulares`, `RiesgoDeLiquidez.RetirosALaVistaMetodo`, `.DiasDeHistoriaDelRetiro`, `.ConfianzaDelRetiro` y
`.ActivosLiquidosContables`.

### 27.3 Pendientes de §27.1: causa exacta y diseño propuesto (sin cambios de código)

Se revisó el código de cada una antes de tocarlo; ninguna se dejó a medias en el árbol.

- **97 · GMF en la entrega por fallecimiento y en no reclamados.** En `NoReclamados.PagarAsync` el dinero está en el concepto `Unclaimed`, no en el capital: el GMF
  no sale de un retiro de capital como en `RetiroDeCuenta`, sino que se retiene **del pago** (neto = valor − GMF). Hace falta: `ReglasDeMovimientos.GmfAsync` con un hecho
  generador nuevo (`UnclaimedPayment`, `DeceasedDelivery`) en `HechosGeneradoresDeGmf` y en el parámetro `Gmf.HechosGeneradores`; filas `GmfCharged` sobre `Unclaimed`/`Gmf`;
  `GmfAEmitir` junto a `DevolucionOrdenada`/`TrasladoEntreCuentas` (y la orden de giro por el neto); el uso mensual (`RegistrarUsoDeGmf`); y la bandera por línea
  (`IsGmfSubject` de la versión no distingue el hecho: columna nueva `GmfOnDeceasedAndUnclaimed`, defecto «cobra», en `LND_DepositLineVersions`). En `Fallecimientos.EntregarPorcionAsync`
  se repite por porción. Toca además las reglas de la matriz contable (hechos y roles) de esos dos orígenes. Es una fase completa con e2e de contabilidad.
- **82 · recálculo del embargo en cada consignación:** `RetencionDeEmbargos.RecalcularAsync` exige el cerrojo de la persona (`LND_DebtorControls`) **antes** de los de sus cuentas;
  la consignación (`ConsignacionEnCuentas`) ya tiene la cuenta bloqueada, así que hay que reordenar el cerrojo en el contrato de cuentas (persona → cuentas) o recalcular
  tras soltar y volver a tomar. Sin esa decisión de orden de cerrojos se arriesga un abrazo mortal.
- **64 y 65:** requieren emitir hechos contables nuevos dentro de operaciones existentes (ajuste de causación en período abierto; `ChargeApplied` dentro de la cancelación) con su
  regla de matriz y su prueba contable.
- **89–93 y 96:** `AssemblyDistributionCommands` y `Fallecimientos` arman el hecho masivo y las órdenes por destinatario con un solo `s.Parametros.ConceptoDeExcedentes`/destino por
  decisión; capitalizar en aportes a quien no tiene cuenta de ahorro (93), retener (89), base por plantilla (90), prorrata de retirados (91), y regirar un giro rechazado (92, 96)
  exigen líneas con destino mixto, un hecho de retención nuevo y una fuente de «cuenta por pagar» respaldada; el cálculo de `RevalorizacionYExcedentes` también cambia.

### 27.4 E253 · el GMF en la entrega por fallecimiento y en los saldos no reclamados (respuesta 97, 2026-10-21)

La respuesta 97 del dueño —«el GMF **sí** se cobra en la entrega por fallecimiento y en los saldos no reclamados, como cualquier retiro»; la contadora lo valida—
quedó implementada con una migración aditiva nueva, `CaptacionesGmfEnEntregas` (PostgreSQL `20261020181323`, SQL Server `20261020181249`): `LND_DepositLineVersions.GmfOnDeceasedAndUnclaimed`
(`bool`, **defecto verdadero** también para las filas existentes). Paridad y `has-pending-model-changes` limpios en los dos motores.

- **Dos hechos generadores nuevos**, `DeceasedDelivery` (`HechosGeneradoresDeGmf.EntregaPorFallecimiento`) y `UnclaimedPayment` (`.PagoDeNoReclamado`), en `HechosGeneradoresDeGmf.Todos` y en
  el defecto de `Gmf.HechosGeneradores` como **gravados** (pendiente de validar por la contadora; una cooperativa que ya guardó el parámetro sin ellos debe agregarlos o no los cobra, porque «un
  hecho que el parámetro no nombra no genera»).
- **Una bandera por línea**, `GmfOnDeceasedAndUnclaimed` en la versión de la línea (`DepositLineVersion`, borrador, huella canónica, `DepositTaxInput`/`ImpuestosDeCaptacionRequest`, casilla en el
  editor de línea). **Defecto «cobra»**; sólo muerde si la línea ya está sujeta al GMF (`IsGmfSubject`). La decisión vive en un solo sitio, `ReglasDeMovimientos.GmfAsync`: para esos dos hechos la
  cuenta es «sujeta» si la línea lo es **y** la bandera está encendida; el motor (`GravamenMovimientosFinancieros`) no cambia. Los aportes (cuenta de aportes) no cobran GMF, como siempre.
- **El GMF se retiene del pago, no se suma**: la base es el valor entregado; el destinatario recibe el **neto** (valor − GMF) y el GMF queda en el concepto `Gmf` del libro. En `Fallecimientos.EntregarPorcionAsync`
  (por porción: dos beneficiarios al 50 % = dos GMF) y en `NoReclamados.PagarAsync` la orden de giro o el traslado llevan el neto; el uso mensual del tope exento
  (`RegistrarUsoDeGmf`, con el hecho generador) se escribe en la misma operación y la exención sigue siendo **una sola cuenta marcada por persona**. Sale del capital (y, si éste no alcanza, del
  interés por pagar: `Fallecimientos.DescontarGmf`); en no reclamados sale del concepto `Unclaimed`, donde está el dinero, y por eso `RegistroDeCuentas.ValidarParejas` admite ese concepto.
- **Hechos, en la misma transacción**: `DevolucionAEmitir.Gmf` agrega el acompañante `GmfRetenido` a la unidad de la `DevolucionOrdenada` (con su `OrdenDeGiroEmitida` por el neto); el traslado
  usa `TrasladoConGmfAsync`. Cartera sigue sin conocer cuentas. **Matriz contable**: `DVO` admite `GmfRetenido` como acompañante y `GMF` admite `NoReclamadosPorPagar` como rol de débito; el
  constructor de líneas debita ese rol (no el capital) cuando el generador es `UnclaimedPayment`. No se siembra ninguna regla ni cuenta: la contadora asigna las del `GmfPorPagar` por clase y los
  `NoReclamadosPorPagar` que ya sugería la plantilla.
- **Reverso**: el giro rechazado reversa la operación entera: devuelve el neto y el GMF a `Unclaimed` y el saldo vuelve a retenido (probado).
- **Idempotencia**: las dos operaciones siguen detrás de su `Idempotency-Key`/`Held` (`Lending.Unclaimed.AlreadyPaid`); la huella del pago ahora incluye el GMF.
- **Las clases se construyen sin `ReglasDeMovimientos` en las pruebas viejas** (parámetro opcional al final): sin él no hay GMF; en la API siempre se inyecta.

**Pruebas.** Domain: los dos hechos gravan por defecto, la línea «no cobra» y el hecho exento no cobran. Application (`CicloDeVidaDeCuentasTests.GmfEnEntregas`): fallecido con dos beneficiarios al 50 % (órdenes de 498.000,
GMF 2×2.000, dos usos, dos `GmfRetenido`, cuenta cancelada al girarse), la misma entrega con la línea en «no cobra» (500.000 enteros, sin GMF), no reclamados con GMF (neto, `Unclaimed` en cero,
reverso que lo restituye) y sin él, y el reparto del GMF entre conceptos. e2e (`CicloDeVidaDeCuentasTests`): fallecimiento con GMF (498.000), sin GMF por la bandera (500.000) y no reclamados con y sin GMF por
HTTP, verdes en PostgreSQL y SQL Server. **Pendiente de validar por la contadora**: que el GMF sea gravado en estas dos entregas y la regla de base (el valor entregado, retenido del pago); la
vista previa de la entrega por fallecimiento todavía no muestra el GMF por porción.

### 27.5 E254 · el embargo se recalcula en cada consignación (respuesta 82 B, 2026-10-21)

Con un embargo vigente, **cada consignación posterior al oficio se recalcula en el momento** contra el límite embargable y retiene lo que corresponda (antes sólo
retenía el saldo al recibir el oficio y el recálculo diario). Sin migración ni esquema nuevo.

- **Dónde:** `ConsignacionEnCuentas.AplicarAsync`, después de registrar la operación, los cargos y el ajuste de causación y antes de leer el disponible del
  comprobante: si la cuenta es de captación y su titular tiene un embargo activo (`RetencionDeEmbargos.TieneEmbargoVigenteAsync`), llama a
  `RetencionDeEmbargos.RecalcularAsync` (alimentos primero y luego por fecha de recepción, la 83), así el comprobante ya muestra el disponible con lo retenido. Si
  el recálculo falla (p. ej. falta el límite nacional) la consignación **no se pierde**: el comprobante lleva un aviso y lo retiene el recálculo diario.
- **Orden de cerrojos (persona primero, luego sus cuentas por Id):** el contrato ya tomaba setup → titulares → cuentas. Como el recálculo reescribe los bloqueos de
  **todas** las cuentas del titular, `ContratoDeCuentasDeCartera.ConsignarAsync` toma, en la misma pasada ordenada por Id, también las cuentas vivas de captación de
  los titulares con embargo (`RetencionDeEmbargos.CuentasDeLasPersonasEmbargadasAsync`). **Variante respecto del encargo:** la consulta de «¿tiene embargo?» se hace
  **después** de tomar la persona, no antes, porque los embargos se registran y levantan bajo ese cerrojo y entonces la lista es estable; así no hace falta
  re-verificar ni tomar cerrojos fuera de orden. Nadie pide el cerrojo de una persona con una cuenta ya tomada. Documentado en `ICerrojoDeCartera` y fijado por
  `ElOrdenDelCerrojoEsPersonaYLuegoSusCuentas` (ningún método del código de Application toma una persona después de una cuenta; y la consignación del contrato
  consulta las cuentas embargadas entre la persona y las cuentas).
- **Pruebas:** Application (`EmbargosTests`: consignación posterior retiene hasta el tope del oficio, varios embargos con alimentos primero, el desembargo libera lo
  retenido, dos consignaciones seguidas en cuentas distintas de la misma persona suman sobre el límite) y la e2e `R82_…` (retención en el momento y dos
  consignaciones simultáneas en cuentas distintas del mismo titular sin bloqueo mutuo), verdes en PostgreSQL y SQL Server.

### 27.6 E255 · cancelar una cuenta con cargos por cobrar los cobra del saldo (respuesta 65 B, 2026-10-21)

Al **cancelar una cuenta** con cargos pendientes (cuota de manejo, etc.), los cargos **se cobran del saldo dentro de la misma cancelación** y lo restante se entrega.
Antes la cancelación se bloqueaba con `PendingCharges`. Sin migración ni esquema nuevo (el valor nuevo del enum de operaciones se guarda como número).

- **Operación propia.** El cobro va en su propia operación `ChargeCollection` (valor 28, prefijo `CGC-`), no dentro de la `Cancellation`: así el hecho `CargoPendienteCobrado`
  (CGP) es el principal de **su** unidad contable (origen la operación `CGC-`, clase `AccountCharge`, clave `Confirmation`) y no se mezcla con la unidad de la liquidación
  (`ILQ`/`RTF`) ni con la del retiro o traslado de la entrega, cuyos acompañantes están fijados. Filas `ChargeApplied` (−por cobrar, −capital) con el mensaje del hecho; cada
  cargo pasa a `Collected` con `CollectedOperationId` = la operación `CGC-`. Cartera sigue sin conocer cuentas; el hecho nace en la misma transacción.
- **Orden.** (1) operación `CAN-`, (2) puesta al día de la causación y liquidación con su retención (el capital incluye ya el interés neto), (3) **cobro de los cargos**
  (`CargosDeCuentas.PendientesDeLaCancelacionAsync` / `CobrarEnCancelacionAsync`: en orden de causación, **enteros**, sin mínimo porque la cuenta se cierra), (4) entrega del
  resto con su GMF. El contractual cobra antes de `TerminarAsync` (con el capital de ese momento).
- **Lo que sigue impidiendo.** Los cargos se cobran enteros: si el capital no alcanza a cubrirlos todos, la parte sin cubrir sigue siendo el bloqueo `PendingCharges` («el saldo
  no alcanza a cubrirlos: condónelos»), en la vista previa y, con la transacción deshecha, en el comando (`Lending.Savings.CloseBlocked`). Un cargo ya cobrado no se vuelve a cobrar
  (idempotencia por el estado del cargo y la `Idempotency-Key` del comando). Sin `CargosDeCuentas` inyectado (pruebas viejas) lo por cobrar sigue impidiendo como antes.
- **Validación previa y reversos.** `HechosRepresentativosDeCaptaciones.CargoCobrado` suma el hecho a la pregunta de la pantalla si la cuenta tiene cargos pendientes. La operación
  `CGC-` no se reversa desde la pantalla (como `Charge`/`ChargeWaiver`); si Tesorería rechaza el giro de la entrega, el reverso es el del retiro y el cargo cobrado queda cobrado (era deuda).
  La ficha de operaciones la nombra «Cobro de cargos».
- **Pruebas.** Application (`InteresesDeCuentaTests`): cobra el cargo y entrega el resto (operación `CGC-`, cargo `Collected`, hecho CGP, cuenta cancelada) y el cargo que el saldo no
  cubre impide con `PendingCharges`. e2e (`CargosYDebitoAutomaticoTests.R65_…`, dos): cancelar con el cargo pendiente y con el cargo mayor que el saldo; verdes en PostgreSQL y SQL Server.
  Domain: el enum `MemberAccountOperationKind` con `ChargeCollection = 28`. **Pendiente de validar por la contadora:** que el cobro (CGP, comprobante `MA`) lleve la regla de matriz
  que ya existe para el cobro de cargos por consignación.

### 27.7 E256 · fecha valor en un período ya liquidado: la diferencia se ajusta en el período abierto (respuesta 64 B, 2026-10-21)

Un movimiento de ahorro (consignación, retiro, traslado o reverso) fechado en un período **cuyos intereses ya se liquidaron entra**, y la diferencia de intereses de
esos días se **ajusta en el período abierto**, sin reabrir ni reescribir lo liquidado. Antes (E97) el movimiento entraba pero los días liquidados no se ajustaban nunca.
Sin migración: la clase de fila nueva se guarda como número (`int`).

- **Qué se calcula.** `AjusteDeCausacionDeCuenta` ahora tiene dos partes que suman en **un solo hecho** `AjusteDeCausacionDeCaptacion` (causa `ValueDatedOperation`, los
  días que cambiaron —también los liquidados— y el hecho principal), porque un origen sólo admite un mensaje de ese tipo: (1) la **ventana abierta**, igual que antes
  (`AccrualAdjustment` fechado el último día causado); (2) los **períodos liquidados** que la fecha valor alcanza, período de liquidación por período
  (`DeLosPeriodosLiquidadosAsync`, desde la fecha valor hasta `LastSettledThrough`, con el calendario de períodos de la línea): lo que el motor da **con** la operación menos
  lo que daría **sin** ella (`LectorDeInteres.VentanaAsync(..., sinLaOperacion)` lee el libro de capital sin las filas de esa operación). Las dos veces con las tasas y
  los saldos de hoy: sólo cuenta el efecto de la operación, no una tasa registrada tarde ni lo que ya se corrigió.
- **Dónde queda.** Una fila nueva `SettledPeriodAdjustment` (valor 23 de `MemberAccountTransactionKind`, interés por pagar, hasta 6 decimales) **en la misma operación**, fechada el
  **primer día después de la última liquidación** (así la próxima liquidación, de cualquier período abierto, la incluye). **No** está en `ClasesDeLoCausado`: ni la
  causación diaria ni el ajuste comparan esa fila con la ventana del motor (si lo hicieran la corregirían de vuelta al otro día; probado). Sí está en la nueva
  `ClasesDeLoLiquidable`, que leen la liquidación (suma lo que abona), el extracto y la proyección regulatoria. No reactiva una cuenta inactiva (`NoSonDelTitular`).
- **Reverso.** El espejo de una operación **no copia** la fila `SettledPeriodAdjustment` (`ReversionDeCuentas`): el reverso calcula la suya, negativa, en el período abierto
  del momento del reverso (si se espejara con la fecha original y entre tanto hubo liquidación, esa fila quedaría fuera de toda liquidación). Probado: las dos filas suman cero.
- **Excepción del día de la liquidación.** Una fecha valor **igual** al día de la última liquidación no se ajusta (sólo las **anteriores**): es la de la cancelación, la entrega por
  fallecimiento, el pago de saldos no reclamados y el retiro del asociado, que liquidan a la fecha y entregan el mismo día; la cuenta se cierra y ninguna liquidación abonaría el
  ajuste (probado: sin la excepción la cancelación dejaba la cuenta en cierre con interés por pagar). Una consignación del propio día de cierre del período queda como en E97.
- **Lo que no cambia.** Lo liquidado (`InterestSettlements`, el abono y la retención ya practicada) no se toca; la retención de lo ajustado la calcula la liquidación en la que
  entra (en la retención diaria el ajuste cuenta en el primer día del período abierto: **pendiente de validar por la contadora**). Contabilidad: el hecho de ajuste se contabiliza
  en el período abierto con la fecha contable de la operación, como siempre; no se reabre un mes cerrado. Un total cero entre las dos partes no emite hecho (las filas quedan sin
  mensaje). Cuenta migrada: no se ajustan días anteriores al corte.
- **Pruebas.** Application (`InteresesDeCuentaTests`): la consignación fechada en diciembre liquidado entra, lo liquidado no cambia, la fila de 3 días liquidados vale lo esperado y va
  fechada el 1.º de enero, un solo hecho con las dos filas, el proceso no corrige nada después, la liquidación de enero la abona y no queda interés por pagar; el reverso deja las
  dos filas en cero; y la cancelación con el ajuste activo no ajusta su propia entrega del mismo día. e2e (`InteresesTests.R64_…`): el mismo caso por HTTP en la base real, con la liquidación del mes siguiente. Domain: el enum. Verdes en PostgreSQL y SQL Server.

### 27.8 E257 · excedentes a ahorro de quien no tiene cuenta: se capitalizan en sus aportes (respuesta 93 B, 2026-10-21)

Cuando la asamblea decide llevar el excedente a ahorro y un asociado **no tiene cuenta viva** en la línea de ahorro de destino, la decisión **ya no se bloquea**
(`Lending.AssemblyDistribution.DestinationAccountMissing` se retiró): su porción se **capitaliza en sus aportes**, sin abrirle ninguna cuenta. Sin migración.

- **Dónde.** `DistribucionesDeAsamblea.SimularAsync` sigue separando a los que no tienen cuenta de destino (`PersonasSinDestino`), pero ahora el aviso dice que su
  excedente se capitaliza en sus aportes. `ApplyAssemblyDistributionCommandHandler`: para esos asociados la operación `EXD-` queda en **su cuenta de aportes** con el
  concepto `Asamblea.ClaseDeAporteDeExcedentes` (ordinario por defecto), igual que el destino «a aportes»; a los demás se les abona en su ahorro (`Principal`). Una
  misma decisión queda con **destino mixto**: cada línea dice a dónde fue (`destinationAccountNumber` nulo = capitalizó).
- **Contabilidad.** El hecho masivo `ExcedentesDistribuidosV1` sigue siendo uno solo; el constructor asienta por concepto de cada línea (`Principal` al ahorro,
  el concepto de aportes a los aportes), así que la mezcla no cambia la matriz. La validación previa de pantalla (D23) pregunta ahora **por separado** por las
  combinaciones de dimensiones del ahorro (destino «a ahorro») y por las de las cuentas de aportes que capitalizan (destino «a aportes»).
- **Huella y reverso.** La huella ya incluía la cuenta de destino de cada línea (nula para quien capitaliza). El reverso entero espeja las filas de cada operación
  y sigue igual.
- **Pruebas.** Application (`AsambleaTests.Los_excedentes_a_ahorro_sin_cuenta_de_destino_se_capitalizan_en_los_aportes_del_asociado`: Ana recibe 10.000 en su ahorro,
  Beto aumenta su aporte ordinario y no se le abre cuenta) y e2e (`AsambleaTests.E257_…`, por HTTP). La e2e anterior que esperaba el bloqueo se reemplazó.
  **Pendiente de validar por la contadora:** que lo capitalizado use la clase de aporte de excedentes del parámetro y no una distinta.

### 27.9 E258 · el giro rechazado se regira y mientras tanto el valor sigue por pagar al destinatario (respuestas 92 y 96, 2026-10-21)

Si Tesorería **rechaza, anula o no cobra** el giro de un **excedente de la asamblea** (92) o de la **entrega por fallecimiento** a un beneficiario (96), el valor **no vuelve a
ninguna cuenta**: queda respaldado en la cuenta por pagar a favor de quien lo recibe, y la orden queda **pendiente de regiro**. Una persona la **regira**: una orden nueva
por el mismo valor, al mismo beneficiario y cuenta destino (o a los que indique), con su `OrdenDeGiroEmitida` v2. Migración aditiva `CaptacionesRegiroDeOrdenes`
(PostgreSQL `20261020204151`, SQL Server `20261020204112`): `LND_AccountPayoutOrders.ReissuesOrderId` (FK a sí misma) y el índice único por operación pasa a filtrado
(`ReissuesOrderId IS NULL`): un regiro comparte la operación de la orden que vuelve a girar. Paridad y `has-pending-model-changes` limpios en los dos motores.

- **El respaldo.** En los excedentes pagados no hay cambio contable: el hecho `ExcedentesDistribuidos` ya llevaba el valor del fondo a los retornos por pagar con el
  asociado de tercero, y el rechazo no lo toca (era lo construido, E189). En la **entrega por fallecimiento sí cambia el rechazo**: antes un giro rechazado reversaba la operación y
  devolvía el valor a la cuenta del fallecido; ahora **no reversa nada** (no se emite `OperacionDeCuentaReversada`): la «devolución por pagar» que la entrega ya había
  causado a favor del beneficiario sigue siendo su respaldo, y la cuenta sigue en cierre sin saldo. Se decide por el propósito de la orden: `AccountPayoutOrder.PropositosQueSeRegiran`
  (`SurplusPayment`, `DeceasedDelivery`). Los demás propósitos (devolución de aportes, cancelación, CDAT, embargo, **no reclamados**) siguen reintegrando como siempre.
- **Pendiente de regiro** (`OrdenesDeGiroDeCuentas.PendientesDeRegiro`): una orden de esos propósitos en `Rejected`, `Voided` o `CheckNotCashed` sin otra orden que la reemplace.
  Mientras haya una, **la cuenta del fallecido no se cancela y el caso no pasa a entregado** (`Fallecimientos.AlGirarAsync`).
- **Regirar.** `POST /api/lending/assembly-distributions/{id}/orders/{orderId}/reissue` (`Lending.Assembly.Approve`) y `POST /api/lending/deceased-cases/{id}/orders/{orderId}/reissue`
  (`Lending.DeceasedCases.Approve`): bajo el cerrojo persona → cuenta; la orden debe ser del trámite (misma operación / mismo caso); una sola vez por orden, pero un regiro que
  Tesorería vuelve a rechazar admite otro (`Lending.PayoutOrder.NotReissuable` si no espera regiro). La orden nueva sale a Tesorería y, cuando se gira, la cuenta se cancela y el
  caso se entrega como si se hubiera girado a la primera.
- **GMF (reconcilia con E253).** El GMF retenido en la entrega **se queda retenido** y **no se vuelve a cobrar** al regirar: el hecho generador fue el retiro y ocurrió; el regiro
  gira el mismo neto (valor − GMF). El rechazo del pago de un **saldo no reclamado** no cambia: sigue reversando neto y GMF a no reclamados.
- **Consumo del resultado.** Al corregir se halló un defecto previo: consumir como proceso el `Disbursed` de una orden de cuentas fallaba (`DevolucionGirada` exige una persona de origen,
  T11). Ahora `ConsumeTreasuryResult` pasa el usuario de origen del mensaje de Tesorería a `AplicarResultadoAsync` → `DevolucionGiradaAsync`. La tesorería de prueba
  (`EmitTestDisbursementResultCommand`) también informa resultados de órdenes de cuentas.
- **No hecho.** La parte C de las respuestas del informe original (pasado un plazo, el pendiente pasa a no reclamados) y la pantalla de regiro no se construyeron; la lista de
  pendientes de regiro por consulta tampoco. **Pendiente de validar por la contadora:** que la devolución por pagar de la entrega siga siendo el respaldo mientras la orden esté
  rechazada, sin reclasificarla a otra cuenta.
- **Pruebas.** Application: `AsambleaTests.E258_…` (excedente rechazado, no se regira lo que sigue emitido, regiro, una sola vez, decisión ajena) y
  `CicloDeVidaDeCuentasTests.E258_…` (entrega: sin reintegro ni GMF nuevo, cuenta en cierre y caso sin entregar hasta girar; un regiro anulado admite otro). e2e
  (`AsambleaTests.E258_…`, `CicloDeVidaDeCuentasTests.E258_…`): rechazo por la tesorería de prueba, regiro por HTTP y giro final. Verdes en PostgreSQL y SQL Server.

### 27.10 E259 · el asociado retirado durante el ejercicio participa a prorrata y se le gira lo que le toca (respuesta 91 B, 2026-10-21)

Quien dejó de ser asociado **durante el ejercicio** ya no queda fuera de los excedentes: participa **a prorrata del tiempo que lo fue** y se le **gira** lo que le toca (no se
capitaliza en aportes ni se lleva a ahorro: ya no tiene vínculo). Migración aditiva `CaptacionesAsambleaRetirados` (PostgreSQL `20261020213628`, SQL Server `20261020213550`):
`LND_AssemblyDistributionLines.WithdrawnDuringYear` (`bool`, defecto falso). Paridad y `has-pending-model-changes` limpios en los dos motores.

- **Quién es «retirado del ejercicio».** En los **excedentes** (no en la revalorización ni en la amortización, que son de aportes vivos), quien tiene un retiro **liquidado o pagado**
  (`Settled`/`Paid`) con fecha efectiva desde el 1 de enero del ejercicio en adelante. Los retiros aprobados o por rehacer siguen fuera, con su aviso de siempre. Se lee en
  `DistribucionesDeAsamblea.SimularAsync`; la cuenta es la del retiro (`ContributionAccountId`), aunque ya esté cerrada.
- **A prorrata del tiempo.** La base del retirado es **siempre el saldo promedio diario** (aunque la decisión use «saldo al cierre», que en su caso sería cero) con los movimientos
  **anteriores a la fecha efectiva del retiro** y esa fecha cancela el saldo (`RevalorizacionYExcedentes.HastaElRetiro`): cuenta los días hasta la **víspera** del retiro, y lo que
  la liquidación le devolvió después no le resta. Retirado después del cierre del ejercicio = fue asociado todo el año. Vive en el motor puro (`AsociadoEnLaDistribucion.RetiradoEl`);
  la explicación lo dice. El resto del cálculo (tasa, mayor residuo, prorrateo al fondo) no cambia.
- **Se le gira.** Al aplicar, la línea del retirado va **siempre por orden de giro** (`PayoutPurpose.SurplusPayment`, al beneficiario registrado de la persona), sea cual sea el
  destino de la decisión: no entra a ninguna cuenta ni abre ninguna. Una misma decisión queda con **destino mixto**: los demás capitalizan o van a ahorro como siempre. En el hecho
  `ExcedentesDistribuidos` el retirado viaja en `Payments` (del fondo de excedentes a la devolución por pagar, motivo `SurplusPayment`) y los demás en `Lines`; el constructor
  contable ya asienta las dos cosas en un solo hecho, así que **no cambia la matriz**. La validación previa (D23) pregunta aparte por las dimensiones de las cuentas de los retirados
  con destino «pago». Los giros rechazados se regiran como los demás excedentes pagados (E258).
- **No se reversa entera.** Como el pago en dinero (`PaymentNotReversible`): lo girado ya salió a Tesorería; se corrige con otra decisión.
- **Pantalla y DTO.** La línea trae `withdrawnDuringYear`; en la simulación la pantalla de asamblea la rotula «Giro (se retiró en el ejercicio)».
- **Pruebas.** Domain (`RevalorizacionYExcedentesTests`: el promedio hasta la víspera, el retiro posterior al cierre y la mezcla con quien sigue). Application
  (`AsambleaTests.E259_…`: Ana retirada el 16 de diciembre participa con 41.095,89 de base, se le gira, no entra nada a sus aportes, Beto capitaliza y la decisión no se reversa).
  e2e (`AsambleaTests.E259_…`, por HTTP con el retiro liquidado en la base). Verdes en PostgreSQL y SQL Server. **Pendiente de validar por la contadora:** que el tiempo cuente hasta la
  víspera del retiro y que la revalorización de quien se retiró no aplique.

### 27.11 E260 · retención en la fuente sobre la revalorización de aportes y los retornos cooperativos (respuesta 89 B, 2026-10-21)

Se practica **retención en la fuente** sobre lo que la asamblea reparte (la revalorización de aportes y los retornos o excedentes), con un **concepto de retención configurable
por cooperativa**. Todo **pendiente de validar por el tributarista**: el código no trae ningún valor legal; sin concepto o sin tarifa **no se retiene** (el defecto de una cooperativa
nueva). Migración aditiva `CaptacionesRetencionDeAsamblea` (PostgreSQL `20261020220510`, SQL Server `20261020220430`): `LND_AssemblyDistributionLines.WithheldAmount`
(`decimal(18,2)`, defecto 0), `WithholdingRate` (`decimal(9,6)`, nula) y `WithholdingConcept` (`nvarchar(60)`, nulo). Paridad y `has-pending-model-changes` limpios en los dos motores.

- **Parámetros** (`Asamblea.Retencion.*`, K10, general, «pendiente de validar por el tributarista»): `Concepto` (texto; vacío = no se retiene), `TarifaRevalorizacion` y `TarifaExcedentes`
  (fracciones; vacías = no se retiene esa clase) y `BaseMinimaUvt` (vacía o cero = sin base mínima). Cada cooperativa los fija por `POST /api/lending/parameters/{clave}/versions`
  con vigencia, y la decisión lee los del día en que se simula y se aplica. La amortización de aportes no retiene (es devolver lo que el asociado puso, no una renta).
- **Cálculo** (puro, `Domain/Lending/Deposits/RetencionDeAsamblea`): sobre lo **aplicado** a cada asociado después del prorrateo al fondo: al no contribuyente (art. 23 ET) y al autorretenedor no se
  les retiene; con base mínima, sólo desde `BaseMinimaUvt × UVT` (la UVT vigente del valor nacional, la misma fuente de la retención de intereses; sin ella, `NationalValues.Missing`); si no,
  aplicado × tarifa al peso o al centavo según `Asamblea.Redondeo`, sin pasar de lo aplicado. El perfil sale de `ReglasDeRetencion.PerfilDe` (el mismo de los rendimientos).
- **Qué recibe el asociado.** Lo **aplicado menos la retención** (neto): la operación de la cuenta, las filas del libro, la orden de giro del pago y el giro al retirado llevan el neto;
  la línea de la decisión guarda el bruto (`ProratedAmount`), la retención, la tarifa y el concepto. La simulación y la huella incluyen la retención (si el parámetro o la UVT cambian, la
  huella cambia y hay que volver a simular).
- **Hecho y contabilidad.** Sin hecho nuevo en el catálogo: la retención viaja como `withholdings[]` **dentro** de `RevalorizacionDeAportesAplicada` y `ExcedentesDistribuidos` —en
  la misma transacción, como `RetencionPracticada` es acompañante de su principal— con el `WithholdingDetailV1` del hecho de rendimientos (base, tarifa, UVT, perfil, año y
  `conceptCode`). Las líneas y los pagos son netos; el constructor debita el **mismo fondo** por el valor retenido y acredita el rol nuevo **`RetencionDeAsambleaPorPagar`** (rol 18 de
  captaciones y aportes, 44 en total: pasivo sin clase de captación, sucursal opcional; la cuenta la elige la contadora y la plantilla lo sugiere como fila opcional). La validación
  previa de pantalla pregunta también por ese rol cuando la decisión retiene (sin regla, «Bloquear» como siempre). Se prefirió un rol propio y no `RetencionPorPagar` porque éste exige la clase de captación.
- **Reverso entero.** La decisión con retención se reversa entera como antes: el `OperacionDeCuentaReversada` trae `withholdings[]` con lo que cada operación retuvo y el constructor lo
  devuelve del pasivo al fondo (probado). Los excedentes pagados siguen sin reversarse.
- **Pantalla.** La simulación y lo aplicado muestran la retención por asociado y el total.
- **No hecho.** El **certificado de retención** al asociado por este concepto (los certificados de captaciones siguen siendo los de rendimientos) y la declaración/exógena quedan para
  cuando el tributarista valide el concepto; tampoco se retiene en el retiro del asociado (su devolución de aportes no es renta).
- **Pruebas.** Domain (`RetencionDeAsambleaTests`: tarifa, redondeo, perfiles, base mínima, política sin concepto). Application (`AsambleaTests.E260_…`: revalorización con autorretenedor,
  retorno pagado con el neto, sin concepto, base mínima con y sin UVT; `HechosDeAsambleaTests`: fondo, retención por pagar y reverso) y los contadores de roles (44) y de parámetros (72).
  e2e (`AsambleaTests.E260_…`: excedentes a aportes con retención y reverso, y excedentes pagados con el neto). Verdes en PostgreSQL y SQL Server.

### 27.12 E261 · la base de los retornos la define la asamblea y la cooperativa la sube por plantilla (respuesta 90 C, 2026-10-21)

Los **retornos cooperativos** pueden repartirse sobre una base que la asamblea define —el uso de servicios, los ahorros, lo que decida— y no sobre el saldo de aportes. La cooperativa
la **sube por plantilla** de importación. Migración aditiva `CaptacionesBaseDeRetornosPorPlantilla` (PostgreSQL `20261020223529`, SQL Server `20261020223449`): la tabla
`LND_AssemblyReturnBases` (hecho de sólo inserción, único por decisión, versión de la carga y persona; `BaseAmount >= 0`; concepto de hasta 120 caracteres). Paridad y
`has-pending-model-changes` limpios en los dos motores.

- **La decisión.** `RevaluationBase.UploadedBase` (3), sólo en la distribución de excedentes (el validador lo rechaza en la revalorización y la amortización). La decisión se
  registra como siempre, con porcentaje o valor y fondo; la base la trae el archivo.
- **La plantilla** `lending.assembly-return-bases` (hoja `Datos`: `decision`, `persona`, `base`, `concepto`), en el registro de plantillas de Cartera (`/cartera/plantillas`, K10, datos
  personales) y en `GET/POST /api/lending/assembly-distributions/return-bases/template.xlsx|import?mode=review|apply` con `Lending.Assembly.Register`. Mismas reglas del
  patrón de las plantillas: **todo o nada**, **revisión previa** que no guarda, **error por fila y columna**, `Idempotency-Key`. Reglas propias: todo el archivo es de **una** decisión
  que exista, sea de excedentes, esté en borrador y use la base subida; cada persona debe existir (la plantilla no crea personas) y ser asociada con aportes vivos o haberse retirado
  en el ejercicio (E259); un retiro en trámite la deja fuera; la base no es negativa. La plantilla **no borra**: una carga nueva es una **versión** (`LoadVersion` + 1) que repite lo
  anterior y cambia lo que trae el archivo, y un `0` saca al asociado; un archivo sin cambios no crea versión. Aplicada la decisión, no admite más cargas.
- **El reparto.** `SimularAsync` toma la **última** carga: la base de cada asociado es la subida (`AsociadoEnLaDistribucion.BaseSubida`; quien no está en el archivo o está en cero no
  participa, aunque tenga aportes; con varias cuentas de aportes, la base va a la primera). El resto del motor no cambia: tasa o valor sobre la base total, redondeo, mayor residuo y
  prorrateo al fondo; el destino (aportes, ahorro o pago), la retención (E260) y el giro del retirado (E259) siguen como están. Sin archivo, la simulación avisa y nadie participa; avisa
  también de quien tiene aportes y no está en el archivo y de quien está y ya no participa. La huella ya incluía la base de cada línea: otra carga cambia lo que se aplicaría y hay que
  volver a simular. Lo aplicado queda en las líneas de la decisión (`BaseAmount`).
- **Pantalla.** La base «Base subida por plantilla» aparece al registrar excedentes, con el aviso de dónde se sube el archivo.
- **No hecho.** Una pantalla propia para la carga (se usa la de plantillas de Cartera) y mostrar el concepto de cada fila en la simulación (queda guardado).
- **Pruebas.** Domain (`RevalorizacionYExcedentesTests`: la base subida manda sobre los saldos, el que no está o está en cero no participa). Application (`AsambleaTests.E261_…`: errores por
  fila y columna, todo o nada, revisar no guarda, carga, corrección y versión, el cero, aplicar y no admitir más cargas; el validador y la decisión con otra base) y los contadores del
  modelo (184 tablas y la base entre los hechos de sólo inserción). e2e (`AsambleaTests.E261_…`: plantilla, revisión con error, aplicación y reparto por HTTP). Verdes en PostgreSQL y SQL Server.

### 27.13 E262 · la cuenta migrada se inactiva como cualquier otra (respuesta 69, 2026-10-22)

Respuesta del dueño: «las cuentas migradas se inactivan de la misma forma que una cuenta creada desde el aplicativo». Se quitó la regla especial de E119 A para la **inactividad**:
`Inactividad.DesdeCuando` mide desde el último movimiento del titular (que la carga trae en `fechaUltimoMovimientoDelTitular`), o la activación, o la apertura, igual en toda
cuenta; la migrada ya no se ancla al corte. Sin migración.

- **Advertencia, no bloqueo.** La primera noche tras activar, toda cuenta migrada activa con último movimiento anterior al plazo de inactividad de su línea queda inactiva y su
  titular recibe el aviso `INACTIVA`. La validación entre partes (la revisión previa de la carga de captaciones) añade el aviso `Lending.Migration.InactivityOnActivation` con cuántas
  cuentas serían (`ValidacionEntrePartesDeCaptaciones.CuentasQueSeInactivarianAlActivar`, pura; mide a la fecha de hoy o al día siguiente del corte, lo que sea más tarde).
- **Lo que no cambia.** El plazo de **no reclamados** de una cuenta migrada ya inactiva sigue contando desde el corte (E119 A): `Inactividad.DesdeCuandoParaNoReclamados` conserva la
  regla anterior, porque trasladar saldos abandonados a no reclamados por el solo hecho de migrar no fue lo que el dueño respondió. Una cuenta migrada ya `Inactive` sigue naciendo
  inactiva desde el corte.
- **Pruebas.** Application (`CicloDeVidaDeCuentasTests`: `DesdeCuando` de la migrada, `DesdeCuandoParaNoReclamados`, el conteo de la revisión previa). Manual de inactividad y de líneas y apertura.

### 27.14 E263 · la instrucción de vencimiento del contractual se puede retirar (decisión del dueño del 2026-10-22; refina E248)

E248 dejó dicho que la instrucción, una vez dada, sólo se cambiaba por otro destino admitido. El dueño pidió que **pueda retirarse**. Sin migración: la cuenta ya guarda su
instrucción en `MaturityInstructionJson`, y el retiro es una **marca** en ese mismo JSON (`InstruccionDeContractual.Retirada`, con usuario, instante y motivo) y no un `null`:
la instrucción sellada al abrir es hecho y no se edita, y `null` significaba «rige la sellada».

- **Comando** `WithdrawContractualMaturityInstructionCommand` (`POST /api/lending/accounts/{id}/maturity-instruction/withdraw`, `Lending.Accounts.Parties.Manage`, el mismo permiso que
  la cambia; con `Idempotency-Key` y motivo obligatorio). Las mismas guardas que cambiarla: sólo un contractual (`Lending.Savings.NotContractual`) y vigente
  (`Lending.Contractual.NotCurrent`: vencido, cumplido, cancelado). Retirar lo ya retirado responde el contrato sin escribir. Cambiar la instrucción después de retirarla es posible
  mientras el contrato esté vigente.
- **Efecto.** `CierreDeContractual.DestinoAsync` no mueve nada con una instrucción retirada (ni la sellada); queda el efecto de la línea ante el incumplimiento (`MoveToDemand`), que
  es de la línea. El contrato vence a disposición, se levanta `Cartera.ContractualADisposicion` y la lectura del contrato muestra «sin instrucción».
- **Cliente y pantalla.** `CaptacionesClient.RetirarInstruccionDeContratoAsync`; botón «Retirar la instrucción» en la ficha del contrato (con el motivo del formulario).
- **Pruebas.** Application (`AhorroContractualTests.E263_…`: retirada y vencimiento a disposición sin mover dinero y con idempotencia, retiro de la sellada al abrir, rechazo tras el
  vencimiento y en una cuenta que no es contractual), Shared (ruta, método y clave) y e2e (`E263_el_asociado_retira_la_instruccion_…`, PostgreSQL y SQL Server). Manual del contractual.

### 27.15 E264 · por ahora los avisos salen por correo electrónico (decisión del dueño del 2026-10-22)

«El proveedor de SMS y WhatsApp queda para otra fase; por ahora los avisos serían como correo electrónico.» **El diseño ya lo tenía** (feature 013, K1; T0083, T74): el correo
es un canal real, `CanalDeCorreo` sobre `IEmailSender` (código `CORREO`, `Capacidades` con adjuntos y sin consulta de entrega), registrado por `AddApplicationServices`, y
`ColaDeMensajesAlDeudor.CodigoDeCanalAsync` sella `CORREO` para todo mensaje por correo (nunca `SIMULADO`); sólo SMS y WhatsApp caen en `SIMULADO` cuando la cooperativa no tiene
proveedor configurado, y la guardia (`GuardiaDeCanalesExternos`, `RealDelivery`) les quita el efecto en producción: el mensaje no sale y queda `Simulated`. Se verificó la
cadena completa **sin cambios de código**:

- **Quién usa el correo.** `EnvioDeCobranza` prueba correo, SMS y WhatsApp en ese orden (etapa de cobranza, aviso previo, aviso del gasto); los informativos de captaciones
  (inactividad `PREINACTIV`/`INACTIVA`, cambio de tasa, mayoría de edad, contractual, no reclamados) y de Cartera (abono, libranza, no aplicados, traslado de libranza), el envío de
  documentos y de simulaciones piden `MessageChannel.Email` directamente. Las plantillas sembradas traen una fila por canal, así que cada aviso tiene su correo.
- **Reglas que se conservan.** La ventana legal de la Ley 2300 (horario, tope diario, canal fijado en la semana) y las exclusiones (sin autorización del canal —o la de tratamiento
  general en la entrega de documentos—, evidencia exigida a la cobranza, sin destino, fallecido, insolvencia) se evalúan al encolar y otra vez al despachar, igual para los tres canales.
  El destino se copia del maestro al despachar. El registro: `Sent` con `CORREO-{id}` y el destino; el correo no consulta entrega (queda en `Sent`, no pasa a `Delivered`).
- **Lo que sigue abierto.** T0931 (batería de conformidad con los adaptadores reales de SMS y WhatsApp) y T0952 (los adaptadores) quedan abiertas hasta que haya proveedor; mientras
  tanto SMS y WhatsApp siguen `SIMULADO` y el aviso previo con efecto legal en producción es el que sale por correo o por carta con guía.
- **Pruebas nuevas (e2e, PostgreSQL y SQL Server).** `MensajesAlDeudorTests.E264_la_entrega_por_correo_sale_de_verdad_…` (sale por el `IEmailSender` de la fixture, `fx.Emails`, con su
  evidencia; no se repite al despachar de nuevo; el SMS de la misma cooperativa sigue simulado), `…E264_sin_autorizacion_de_tratamiento_el_correo_no_sale_…` y
  `CicloDeVidaDeCuentasTests.E264_el_aviso_de_inactividad_sale_por_correo_solo_con_la_autorizacion_del_canal` (un informativo de captaciones: canal `CORREO`, sin la autorización
  del canal de contacto no se escribe, con ella sale y deja su referencia).

### 27.16 E265 · versión 2 del contrato de cuentas y cancelación con entrega por Recaudo (enmiendas E-02 a y E-02 d de la 015, R1, 2026-10-24)

- **E-02 a, versión 2** (contratos-v2.md §3.1 de la 015): `IContratoDeCuentas.VersionDelContrato = 2` y `ContratosDeIntegracion.VersionesDelContratoDeCuentas = [1, 2]`;
  `ConsignacionAAplicar.BankItem` (`PartidaBancariaCitada`). Un recaudador con sólo la v1 que la manda recibe `Integration.Participant.VersionNotSupported`; con
  `ValueDate = MovementDate` se exime la ventana de `Captaciones.DiasMaximosFechaValor` (`ConsignacionEnCuentas.FechaValorAsync(..., sinVentana)`), y también con la línea de
  pagaduría ya verificada (`DeductionFileLinePublicId`), como Cartera; con otra fecha, `Lending.Payment.BankItemDateMismatch`. `LND_MemberAccountOperations.BankItemPublicId` va en la
  migración nueva y aditiva `CarteraYCaptacionesConPartidaCitada` (las de la 014 siguen congeladas). Sin partida, la huella de la consignación es la de la v1. La activación de
  captaciones y aportes sigue aceptando la v1.
- **E-02 d, cancelación con entrega** (contratos-v2.md §3.4 de la 015): `RetirarAsync` con `CloseAccount = true` de un recaudador `Receipts` real deja de responder
  `Lending.Savings.DeliveryNotAvailable`: `CancelacionConEntregaPorElContrato` liquida a la fecha, cobra los cargos del saldo y el GMF de la cancelación y autoriza el retiro de
  **todo el disponible** en efectivo o cheque, que Recaudo entrega con el mismo egreso; la cuenta queda cancelada. La simulación nueva `Operation = "Close"` devuelve
  `SimulacionDeCuentaDto.AmountToDeliver` (campo opcional nuevo) y el retiro pide exactamente ese valor (si cambió, `Lending.Savings.CloseAmountChanged`, nuevo, y nada se escribe).
  El contractual y los demás recaudadores siguen cancelándose desde Cartera.

### 27.17 E266 · lectura del débito automático y código del pendiente de aprobación para Recaudo (feature 015, R1, 2026-10-24)

Dos piezas aditivas, sin migración ni cambio de comportamiento de Captaciones: (a) el puerto de sólo lectura
`Application/Common/Integration/Lending/IDebitoAutomaticoParaConsolidado.PorDiaAsync` (implementado en
`Lending/Integration/DebitoAutomaticoParaConsolidado` sobre las operaciones `DirectDebit` no reversadas, por fecha valor),
para que el consolidado de Recaudo muestre el débito automático (D26 de la 015); `LaCarteraSoloSeTocaPorSusInterfaces` pasa
a diez puertos; (b) la constante `CodigosDelContratoDeCuentas.PendienteDeAprobacion` (`Lending.Savings.PendingApproval`) en
los DTO del contrato, para que el egreso de caja de Recaudo distinga el retiro sobre la atribución sin tocar
`ErroresDeMovimientos` por dentro; una prueba fija que ambos códigos son el mismo.
