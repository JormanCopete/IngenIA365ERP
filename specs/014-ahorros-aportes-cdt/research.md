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
