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
